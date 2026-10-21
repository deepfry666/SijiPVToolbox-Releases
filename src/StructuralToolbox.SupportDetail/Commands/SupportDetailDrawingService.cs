using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructuralToolbox.SupportDetail.Core;
using StructuralToolbox.SupportDetail.Settings;

namespace StructuralToolbox.SupportDetail.Commands
{
    internal sealed class SupportDetailInstanceData
    {
        public string InstanceId;
        public Point3d Origin;
        public SupportDetailSettings Settings;
    }

    internal sealed class SupportDetailDrawSummary
    {
        public int EntityCount;
        public int DimensionCount;
        public int PierCount;
        public int ColumnCount;
        public bool FixedDetailsInserted;
        public bool MaterialTableInserted;
        public IList<ObjectId> EntityIds = new List<ObjectId>();
    }

    internal static class SupportDetailDrawingService
    {
        internal const string RegAppName = "STPV_SUPPORT_DETAIL";
        internal const string EntityMarker = "SUPPORT_DETAIL_ENTITY";
        internal const string EntityVersion = "0.1";
        internal const string DictionaryName = "STPV_SUPPORT_DETAIL_INSTANCES";
        internal const string RecordVersion = "0.1";
        internal const string FixedDetailBlockName =
            "STPV_SUPPORT_FIXED_DETAILS_V01";

        public static ObjectId EnsureLayer(Database database,
            Transaction transaction, string layerName)
        {
            LayerTable table = (LayerTable)transaction.GetObject(
                database.LayerTableId, OpenMode.ForRead);
            if (table.Has(layerName))
            {
                LayerTableRecord existing =
                    (LayerTableRecord)transaction.GetObject(table[layerName],
                        OpenMode.ForRead);
                if (existing.IsLocked || existing.IsFrozen || existing.IsOff)
                {
                    throw new InvalidOperationException(
                        "输出图层【" + layerName +
                        "】已锁定、冻结或关闭，请恢复后重试。");
                }
                return existing.ObjectId;
            }
            table.UpgradeOpen();
            LayerTableRecord record = new LayerTableRecord();
            record.Name = layerName;
            record.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
            return record.ObjectId;
        }

        public static SupportDetailDrawSummary AppendInstance(
            Database database, Transaction transaction, ObjectId layerId,
            SupportDetailGeometry geometry, Point3d origin, string instanceId,
            string fixedDetailLibraryPath)
        {
            if (geometry == null)
                throw new ArgumentNullException("geometry");
            if (String.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("实例 ID 不能为空。", "instanceId");

            EnsureRegApp(database, transaction);
            ObjectId dimensionStyleId = EnsureDimensionStyle(database,
                transaction);
            BlockTableRecord modelSpace =
                (BlockTableRecord)transaction.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(database),
                    OpenMode.ForWrite);
            SupportDetailDrawSummary summary = new SupportDetailDrawSummary();
            double textHeight = Math.Max(70.0,
                Math.Min(160.0, geometry.BeamLength / 32.0));

            DrawGround(database, transaction, modelSpace, layerId, geometry,
                origin, instanceId, summary);
            DrawBeam(database, transaction, modelSpace, layerId, geometry,
                origin, instanceId, summary);
            DrawPanels(database, transaction, modelSpace, layerId, geometry,
                origin, instanceId, summary);
            DrawRailsAndClamps(database, transaction, modelSpace, layerId,
                geometry, origin, instanceId, summary);
            DrawPiersColumnsAndTriangles(database, transaction, modelSpace,
                layerId, geometry, origin, instanceId, summary);
            if (geometry.Settings.IncludeBackPull)
                DrawBackPull(database, transaction, modelSpace, layerId,
                    geometry, origin, instanceId, summary);
            DrawAnnotations(database, transaction, modelSpace, layerId,
                dimensionStyleId, geometry, origin, textHeight, instanceId,
                summary);
            if (geometry.Settings.IncludeNodeDetails)
            {
                summary.FixedDetailsInserted = InsertFixedDetails(database,
                    transaction, modelSpace, layerId, geometry, origin,
                    instanceId, fixedDetailLibraryPath, summary);
            }
            if (geometry.Settings.IncludeMaterialTable)
            {
                DrawMaterialTable(database, transaction, modelSpace, layerId,
                    geometry, origin, textHeight, instanceId, summary);
                summary.MaterialTableInserted = true;
            }
            WriteInstanceRecord(database, transaction, new SupportDetailInstanceData
            {
                InstanceId = instanceId,
                Origin = origin,
                Settings = geometry.Settings
            });
            return summary;
        }

        public static bool TryReadEntityInstance(Entity entity,
            out string instanceId, out string role)
        {
            instanceId = null;
            role = null;
            if (entity == null)
                return false;
            using (ResultBuffer buffer = entity.GetXDataForApplication(
                RegAppName))
            {
                if (buffer == null)
                    return false;
                TypedValue[] values = buffer.AsArray();
                if (values.Length < 5 ||
                    !String.Equals(GetString(values, 1), EntityMarker,
                        StringComparison.Ordinal) ||
                    !String.Equals(GetString(values, 2), EntityVersion,
                        StringComparison.Ordinal))
                {
                    return false;
                }
                instanceId = GetString(values, 3);
                role = GetString(values, 4);
                return !String.IsNullOrWhiteSpace(instanceId);
            }
        }

        public static SupportDetailInstanceData ReadInstanceRecord(
            Database database, Transaction transaction, string instanceId)
        {
            DBDictionary root = OpenInstanceDictionary(database, transaction,
                OpenMode.ForRead, false);
            if (root == null || !root.Contains(instanceId))
                throw new InvalidOperationException(
                    "DWG 中找不到该支架实例的参数记录。");
            Xrecord record = transaction.GetObject(root.GetAt(instanceId),
                OpenMode.ForRead, false) as Xrecord;
            if (record == null || record.Data == null)
                throw new InvalidOperationException("支架实例参数记录已损坏。");
            TypedValue[] values = record.Data.AsArray();
            if (values.Length < 5 ||
                !String.Equals(GetString(values, 0), RecordVersion,
                    StringComparison.Ordinal) ||
                !String.Equals(GetString(values, 1), instanceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("支架实例参数记录版本不兼容。");
            }
            List<string> chunks = new List<string>();
            for (int index = 4; index < values.Length; index++)
                chunks.Add(Convert.ToString(values[index].Value,
                    CultureInfo.InvariantCulture));
            string settingsText = SupportDetailMetadataCodec.JoinChunks(chunks);
            return new SupportDetailInstanceData
            {
                InstanceId = instanceId,
                Origin = new Point3d(
                    Convert.ToDouble(values[2].Value,
                        CultureInfo.InvariantCulture),
                    Convert.ToDouble(values[3].Value,
                        CultureInfo.InvariantCulture), 0.0),
                Settings = SupportDetailMetadataCodec.DeserializeSettings(
                    settingsText)
            };
        }

        public static IList<ObjectId> FindInstanceEntities(Database database,
            Transaction transaction, string instanceId)
        {
            List<ObjectId> result = new List<ObjectId>();
            BlockTableRecord modelSpace =
                (BlockTableRecord)transaction.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(database),
                    OpenMode.ForRead);
            foreach (ObjectId objectId in modelSpace)
            {
                if (objectId.IsErased)
                    continue;
                Entity entity = transaction.GetObject(objectId,
                    OpenMode.ForRead, false) as Entity;
                string entityInstanceId;
                string role;
                if (TryReadEntityInstance(entity, out entityInstanceId, out role) &&
                    String.Equals(entityInstanceId, instanceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(objectId);
                }
            }
            return result;
        }

        public static int EraseInstanceEntities(Database database,
            Transaction transaction, string instanceId)
        {
            IList<ObjectId> objectIds = FindInstanceEntities(database,
                transaction, instanceId);
            int erased = 0;
            for (int index = 0; index < objectIds.Count; index++)
            {
                Entity entity = transaction.GetObject(objectIds[index],
                    OpenMode.ForWrite, false) as Entity;
                if (entity == null || entity.IsErased)
                    continue;
                entity.Erase();
                erased++;
            }
            return erased;
        }

        public static void EraseInstanceRecord(Database database,
            Transaction transaction, string instanceId)
        {
            DBDictionary root = OpenInstanceDictionary(database, transaction,
                OpenMode.ForWrite, false);
            if (root == null || !root.Contains(instanceId))
                return;
            ObjectId recordId = root.GetAt(instanceId);
            root.Remove(instanceId);
            DBObject record = transaction.GetObject(recordId,
                OpenMode.ForWrite, false);
            if (record != null && !record.IsErased)
                record.Erase();
        }

        private static void DrawGround(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            string instanceId, SupportDetailDrawSummary summary)
        {
            double minimum = Math.Min(geometry.BeamStart.X,
                geometry.BeamEnd.X) - 500.0;
            double maximum = Math.Max(geometry.BeamStart.X,
                geometry.BeamEnd.X) + 500.0;
            AddEntity(database, transaction, modelSpace, layerId,
                new Line(new Point3d(origin.X + minimum, origin.Y, origin.Z),
                    new Point3d(origin.X + maximum, origin.Y, origin.Z)),
                instanceId, "GROUND", summary);
        }

        private static void DrawBeam(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            string instanceId, SupportDetailDrawSummary summary)
        {
            Polyline beam = CreateStrip(geometry.BeamStart, geometry.BeamEnd,
                geometry.BeamNormal, geometry.Settings.BeamWidth, origin);
            AddEntity(database, transaction, modelSpace, layerId, beam,
                instanceId, "BEAM", summary);
        }

        private static void DrawPanels(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            string instanceId, SupportDetailDrawSummary summary)
        {
            for (int index = 0; index < geometry.Panels.Count; index++)
            {
                SupportDetailPanelGeometry panel = geometry.Panels[index];
                Polyline outline = CreateStrip(panel.Start, panel.End,
                    geometry.BeamNormal, geometry.Settings.PanelThickness,
                    origin);
                AddEntity(database, transaction, modelSpace, layerId, outline,
                    instanceId, "PANEL_" + (index + 1).ToString(
                        CultureInfo.InvariantCulture), summary);
            }
        }

        private static void DrawRailsAndClamps(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            string instanceId, SupportDetailDrawSummary summary)
        {
            for (int index = 0; index < geometry.Rails.Count; index++)
            {
                SupportDetailRailGeometry rail = geometry.Rails[index];
                SupportDetailVector along = geometry.BeamDirection * 30.0;
                SupportDetailVector normal = geometry.BeamNormal * 42.0;
                Polyline railOutline = CreatePolygon(new[]
                {
                    rail.Point + along * -1.0 + normal * -1.0,
                    rail.Point + along + normal * -1.0,
                    rail.Point + along + normal,
                    rail.Point + along * -1.0 + normal
                }, origin, true);
                AddEntity(database, transaction, modelSpace, layerId,
                    railOutline, instanceId, "RAIL_" +
                        (index + 1).ToString(CultureInfo.InvariantCulture),
                    summary);
                Circle clamp = new Circle(ToPoint3d(rail.Point +
                    geometry.BeamNormal * 72.0, origin), Vector3d.ZAxis, 18.0);
                AddEntity(database, transaction, modelSpace, layerId, clamp,
                    instanceId, "CLAMP_" +
                        (index + 1).ToString(CultureInfo.InvariantCulture),
                    summary);
            }
        }

        private static void DrawPiersColumnsAndTriangles(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            string instanceId, SupportDetailDrawSummary summary)
        {
            SupportDetailSettings settings = geometry.Settings;
            double pierWidth = settings.PierShape ==
                SupportDetailPierShape.Circular
                ? settings.PierDiameter : settings.PierLength;
            for (int index = 0; index < geometry.Piers.Count; index++)
            {
                SupportDetailPierGeometry pier = geometry.Piers[index];
                double left = pier.Center.X - pierWidth / 2.0;
                double right = pier.Center.X + pierWidth / 2.0;
                Polyline pierOutline = CreateRectangle(left, 0.0, right,
                    settings.PierHeight, origin);
                AddEntity(database, transaction, modelSpace, layerId,
                    pierOutline, instanceId, "PIER_" +
                        (index + 1).ToString(CultureInfo.InvariantCulture),
                    summary);
                summary.PierCount++;

                AddConcreteMarks(database, transaction, modelSpace, layerId,
                    left, right, settings.PierHeight, origin, instanceId,
                    index, summary);

                double columnHalfWidth = 20.5;
                double columnTop = settings.PierHeight +
                    pier.ActualColumnHeight;
                Polyline column = CreateRectangle(
                    pier.Center.X - columnHalfWidth, settings.PierHeight,
                    pier.Center.X + columnHalfWidth, columnTop, origin);
                AddEntity(database, transaction, modelSpace, layerId, column,
                    instanceId, "COLUMN_" +
                        (index + 1).ToString(CultureInfo.InvariantCulture),
                    summary);
                summary.ColumnCount++;

                SupportDetailTriangleGeometry triangle =
                    geometry.Triangles[index];
                Polyline triangleOutline = CreatePolygon(new[]
                {
                    triangle.LeftBase,
                    triangle.RightBase,
                    triangle.Apex
                }, origin, true);
                AddEntity(database, transaction, modelSpace, layerId,
                    triangleOutline, instanceId, "TRIANGLE_" +
                        (index + 1).ToString(CultureInfo.InvariantCulture),
                    summary);

                DrawAnchorBolt(database, transaction, modelSpace, layerId,
                    pier, origin, instanceId, index, summary);
            }
        }

        private static void DrawAnchorBolt(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailPierGeometry pier, Point3d origin,
            string instanceId, int index, SupportDetailDrawSummary summary)
        {
            double top = pier.TopElevation + 55.0;
            double bottom = Math.Max(20.0, pier.TopElevation - 125.0);
            Polyline uBolt = CreatePolygon(new[]
            {
                new SupportDetailPoint(pier.Center.X - 45.0, top),
                new SupportDetailPoint(pier.Center.X - 45.0, bottom),
                new SupportDetailPoint(pier.Center.X + 45.0, bottom),
                new SupportDetailPoint(pier.Center.X + 45.0, top)
            }, origin, false);
            AddEntity(database, transaction, modelSpace, layerId, uBolt,
                instanceId, "ANCHOR_BOLT_" + (index + 1).ToString(
                    CultureInfo.InvariantCulture), summary);
        }

        private static void AddConcreteMarks(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, double left, double right, double height,
            Point3d origin, string instanceId, int pierIndex,
            SupportDetailDrawSummary summary)
        {
            double width = right - left;
            for (int mark = 1; mark <= 3; mark++)
            {
                double x = left + width * mark / 4.0;
                Line line = new Line(
                    new Point3d(origin.X + x - 35.0,
                        origin.Y + height * 0.25, origin.Z),
                    new Point3d(origin.X + x + 35.0,
                        origin.Y + height * 0.45, origin.Z));
                AddEntity(database, transaction, modelSpace, layerId, line,
                    instanceId, "PIER_HATCH_" +
                        (pierIndex + 1).ToString(CultureInfo.InvariantCulture) +
                        "_" + mark.ToString(CultureInfo.InvariantCulture),
                    summary);
            }
        }

        private static void DrawBackPull(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            string instanceId, SupportDetailDrawSummary summary)
        {
            if (geometry.Piers.Count < 2)
                return;
            SupportDetailPierGeometry first = geometry.Piers[0];
            SupportDetailTriangleGeometry last =
                geometry.Triangles[geometry.Triangles.Count - 1];
            Point3d start = new Point3d(origin.X + first.Center.X,
                origin.Y + first.TopElevation +
                    first.ActualColumnHeight * 0.25, origin.Z);
            Point3d end = ToPoint3d(last.Apex, origin);
            AddEntity(database, transaction, modelSpace, layerId,
                new Line(start, end), instanceId, "BACK_PULL", summary);
        }

        private static void DrawAnnotations(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, ObjectId dimensionStyleId,
            SupportDetailGeometry geometry, Point3d origin, double textHeight,
            string instanceId, SupportDetailDrawSummary summary)
        {
            SupportDetailSettings settings = geometry.Settings;
            SupportDetailVector normal = geometry.BeamNormal;
            SupportDetailPanelGeometry firstPanel = geometry.Panels[0];
            SupportDetailPanelGeometry lastPanel =
                geometry.Panels[geometry.Panels.Count - 1];

            AddAlignedDimension(database, transaction, modelSpace, layerId,
                dimensionStyleId, firstPanel.Start, firstPanel.End,
                firstPanel.Start + normal * 300.0, origin, instanceId,
                "DIM_COMPONENT_L", summary);
            AddAlignedDimension(database, transaction, modelSpace, layerId,
                dimensionStyleId, firstPanel.Start, lastPanel.End,
                firstPanel.Start + normal * 580.0, origin, instanceId,
                "DIM_ARRAY_TOTAL", summary);
            AddAlignedDimension(database, transaction, modelSpace, layerId,
                dimensionStyleId, geometry.BeamStart,
                geometry.Triangles[0].BeamPoint,
                geometry.BeamStart + normal * 820.0,
                origin, instanceId, "DIM_OVERHANG_FRONT", summary);
            AddAlignedDimension(database, transaction, modelSpace, layerId,
                dimensionStyleId,
                geometry.Triangles[geometry.Triangles.Count - 1].BeamPoint,
                geometry.BeamEnd,
                geometry.Triangles[geometry.Triangles.Count - 1].BeamPoint +
                    normal * 820.0, origin, instanceId,
                "DIM_OVERHANG_REAR", summary);

            List<SupportDetailRailGeometry> firstPanelRails =
                new List<SupportDetailRailGeometry>();
            for (int index = 0; index < geometry.Rails.Count; index++)
                if (geometry.Rails[index].PanelIndex == 0)
                    firstPanelRails.Add(geometry.Rails[index]);
            if (firstPanelRails.Count >= 2)
            {
                AddAlignedDimension(database, transaction, modelSpace, layerId,
                    dimensionStyleId, firstPanelRails[0].Point,
                    firstPanelRails[1].Point,
                    firstPanelRails[0].Point + normal * 450.0,
                    origin, instanceId, "DIM_RAIL_R", summary);
            }

            for (int index = 1; index < geometry.Piers.Count; index++)
            {
                SupportDetailPoint previous = new SupportDetailPoint(
                    geometry.Piers[index - 1].Center.X, 0.0);
                SupportDetailPoint current = new SupportDetailPoint(
                    geometry.Piers[index].Center.X, 0.0);
                AddAlignedDimension(database, transaction, modelSpace, layerId,
                    dimensionStyleId, previous, current,
                    new SupportDetailPoint(previous.X, -300.0), origin,
                    instanceId, "DIM_PIER_SPACING_" + index.ToString(
                        CultureInfo.InvariantCulture), summary);
            }

            if (geometry.Piers.Count > 0)
            {
                SupportDetailPierGeometry pier = geometry.Piers[0];
                double pierWidth = settings.PierShape ==
                    SupportDetailPierShape.Circular
                    ? settings.PierDiameter : settings.PierLength;
                SupportDetailPoint heightStart = new SupportDetailPoint(
                    pier.Center.X, settings.PierHeight);
                SupportDetailPoint heightEnd = new SupportDetailPoint(
                    pier.Center.X,
                    settings.PierHeight + pier.ActualColumnHeight);
                AddAlignedDimension(database, transaction, modelSpace, layerId,
                    dimensionStyleId, heightStart, heightEnd,
                    new SupportDetailPoint(pier.Center.X -
                        pierWidth / 2.0 - 260.0, settings.PierHeight),
                    origin, instanceId, "DIM_COLUMN_H", summary);
            }

            double highest = Math.Max(geometry.BeamStart.Y,
                geometry.BeamEnd.Y);
            AddText(database, transaction, modelSpace, layerId,
                new Point3d(origin.X, origin.Y + highest + 1050.0, origin.Z),
                "混凝土屋面支架大样", textHeight * 1.35,
                instanceId, "TITLE", summary);
            AddText(database, transaction, modelSpace, layerId,
                new Point3d(origin.X, origin.Y + highest + 760.0, origin.Z),
                "N=" + settings.ComponentCount.ToString(
                    CultureInfo.InvariantCulture) + "  α=" +
                    settings.SlopeDegrees.ToString("0.###",
                        CultureInfo.InvariantCulture) + "°  G=" +
                    settings.ComponentGap.ToString("0.#",
                        CultureInfo.InvariantCulture) + "mm",
                textHeight, instanceId, "PARAMETER_NOTE", summary);
            AddText(database, transaction, modelSpace, layerId,
                new Point3d(origin.X, origin.Y - 950.0, origin.Z),
                "斜梁为整根直梁；H 为第一个/最低基准墩位立柱高度。",
                textHeight * 0.8, instanceId, "GEOMETRY_NOTE", summary);
        }

        private static bool InsertFixedDetails(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            string instanceId, string libraryPath,
            SupportDetailDrawSummary summary)
        {
            if (String.IsNullOrWhiteSpace(libraryPath) ||
                !File.Exists(libraryPath))
            {
                DrawFallbackNodeDetails(database, transaction, modelSpace,
                    layerId, geometry, origin, instanceId, summary);
                return false;
            }

            BlockTable blockTable = (BlockTable)transaction.GetObject(
                database.BlockTableId, OpenMode.ForRead);
            ObjectId blockDefinitionId;
            if (blockTable.Has(FixedDetailBlockName))
            {
                blockDefinitionId = blockTable[FixedDetailBlockName];
            }
            else
            {
                using (Database source = new Database(false, true))
                {
                    source.ReadDwgFile(libraryPath,
                        FileOpenMode.OpenForReadAndAllShare, true, String.Empty);
                    source.CloseInput(true);
                    blockDefinitionId = database.Insert(FixedDetailBlockName,
                        source, false);
                }
            }

            double x = Math.Max(geometry.BeamStart.X,
                geometry.BeamEnd.X) + 1400.0;
            BlockReference reference = new BlockReference(
                new Point3d(origin.X + x, origin.Y, origin.Z),
                blockDefinitionId);
            reference.ScaleFactors = new Scale3d(0.30);
            AddEntity(database, transaction, modelSpace, layerId, reference,
                instanceId, "FIXED_NODE_DETAILS", summary);
            return true;
        }

        private static void DrawFallbackNodeDetails(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            string instanceId, SupportDetailDrawSummary summary)
        {
            double startX = Math.Max(geometry.BeamStart.X,
                geometry.BeamEnd.X) + 1400.0;
            double boxWidth = 1250.0;
            double boxHeight = 1100.0;
            for (int index = 0; index < 4; index++)
            {
                double x = startX + (index % 2) * (boxWidth + 220.0);
                double y = (index / 2) * (boxHeight + 260.0);
                Polyline box = CreateRectangle(x, y, x + boxWidth,
                    y + boxHeight, origin);
                AddEntity(database, transaction, modelSpace, layerId, box,
                    instanceId, "NODE_" + (char)('A' + index), summary);
                AddText(database, transaction, modelSpace, layerId,
                    new Point3d(origin.X + x + 40.0,
                        origin.Y + y + boxHeight - 140.0, origin.Z),
                    "节点" + (char)('A' + index), 100.0, instanceId,
                    "NODE_LABEL_" + (char)('A' + index), summary);
                DrawFallbackNodeSymbol(database, transaction, modelSpace,
                    layerId, x, y, index, origin, instanceId, summary);
            }
        }

        private static void DrawFallbackNodeSymbol(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, double x, double y, int nodeIndex,
            Point3d origin, string instanceId, SupportDetailDrawSummary summary)
        {
            double centerX = x + 625.0;
            double baseY = y + 230.0;
            AddEntity(database, transaction, modelSpace, layerId,
                new Line(new Point3d(origin.X + centerX - 300.0,
                    origin.Y + baseY, origin.Z),
                    new Point3d(origin.X + centerX + 300.0,
                        origin.Y + baseY, origin.Z)),
                instanceId, "NODE_SYMBOL_BASE_" + nodeIndex, summary);
            AddEntity(database, transaction, modelSpace, layerId,
                new Line(new Point3d(origin.X + centerX,
                    origin.Y + baseY, origin.Z),
                    new Point3d(origin.X + centerX +
                        (nodeIndex == 3 ? 260.0 : 0.0),
                        origin.Y + baseY + 470.0, origin.Z)),
                instanceId, "NODE_SYMBOL_MEMBER_" + nodeIndex, summary);
        }

        private static void DrawMaterialTable(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, SupportDetailGeometry geometry, Point3d origin,
            double textHeight, string instanceId,
            SupportDetailDrawSummary summary)
        {
            string[,] rows =
            {
                { "序号", "主要材料名称", "材质", "备注" },
                { "1", "光伏组件", "", "N=" + geometry.Settings.ComponentCount },
                { "2", "檩条 U41×52×2.0", "S350GD", "" },
                { "3", "斜梁 U41×52×2.0", "S350GD", "整根" },
                { "4", "立柱 U41×41×2.0", "S350GD", "" },
                { "5", "背拉 U41×41×2.0", "S350GD", "后拉" },
                { "6", "三角件 180×90×3.0", "S250GD", "" },
                { "7", "边压块 / 中压块", "6005-T5", "AA15" },
                { "8", "M12 地脚 U 形螺栓", "", "" },
                { "9", "檩条连接件 U50×50×3.0", "S350GD", "" },
                { "10", BuildPierMaterialText(geometry.Settings), "C30", "" }
            };
            int rowCount = rows.GetLength(0) + 1;
            Table table = new Table();
            table.SetDatabaseDefaults(database);
            table.TableStyle = database.Tablestyle;
            table.SetSize(rowCount, 4);
            table.Position = new Point3d(origin.X,
                origin.Y - 1350.0, origin.Z);
            table.SetRowHeight(Math.Max(210.0, textHeight * 2.0));
            table.Columns[0].Width = 650.0;
            table.Columns[1].Width = 2700.0;
            table.Columns[2].Width = 1100.0;
            table.Columns[3].Width = 1050.0;
            table.Cells[0, 0].TextString = "材料表";
            table.MergeCells(CellRange.Create(table, 0, 0, 0, 3));
            table.Cells[0, 0].TextHeight = textHeight;
            table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
            for (int row = 0; row < rows.GetLength(0); row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    table.Cells[row + 1, column].TextString = rows[row, column];
                    table.Cells[row + 1, column].TextHeight = textHeight * 0.72;
                    table.Cells[row + 1, column].Alignment = column == 1
                        ? CellAlignment.MiddleLeft
                        : CellAlignment.MiddleCenter;
                }
            }
            table.GenerateLayout();
            AddEntity(database, transaction, modelSpace, layerId, table,
                instanceId, "MATERIAL_TABLE", summary);
        }

        private static string BuildPierMaterialText(
            SupportDetailSettings settings)
        {
            if (settings.PierShape == SupportDetailPierShape.Circular)
            {
                return "混凝土圆形支墩 D" +
                    settings.PierDiameter.ToString("0.#",
                        CultureInfo.InvariantCulture) + "×" +
                    settings.PierHeight.ToString("0.#",
                        CultureInfo.InvariantCulture);
            }
            return "混凝土矩形支墩 " +
                settings.PierLength.ToString("0.#",
                    CultureInfo.InvariantCulture) + "×" +
                settings.PierWidth.ToString("0.#",
                    CultureInfo.InvariantCulture) + "×" +
                settings.PierHeight.ToString("0.#",
                    CultureInfo.InvariantCulture);
        }

        private static ObjectId AddEntity(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, Entity entity, string instanceId, string role,
            SupportDetailDrawSummary summary)
        {
            entity.SetDatabaseDefaults(database);
            entity.LayerId = layerId;
            entity.Color = Color.FromColorIndex(ColorMethod.ByAci, 256);
            using (ResultBuffer metadata = CreateEntityMetadata(instanceId, role))
                entity.XData = metadata;
            ObjectId objectId = modelSpace.AppendEntity(entity);
            transaction.AddNewlyCreatedDBObject(entity, true);
            summary.EntityCount++;
            summary.EntityIds.Add(objectId);
            return objectId;
        }

        private static void AddAlignedDimension(Database database,
            Transaction transaction, BlockTableRecord modelSpace,
            ObjectId layerId, ObjectId dimensionStyleId,
            SupportDetailPoint first, SupportDetailPoint second,
            SupportDetailPoint linePoint, Point3d origin, string instanceId,
            string role, SupportDetailDrawSummary summary)
        {
            AlignedDimension dimension = new AlignedDimension(
                ToPoint3d(first, origin), ToPoint3d(second, origin),
                ToPoint3d(linePoint, origin), String.Empty, dimensionStyleId);
            AddEntity(database, transaction, modelSpace, layerId, dimension,
                instanceId, role, summary);
            summary.DimensionCount++;
        }

        private static void AddText(Database database, Transaction transaction,
            BlockTableRecord modelSpace, ObjectId layerId, Point3d position,
            string text, double height, string instanceId, string role,
            SupportDetailDrawSummary summary)
        {
            DBText entity = new DBText();
            entity.Position = position;
            entity.TextString = text ?? String.Empty;
            entity.Height = Math.Max(25.0, height);
            entity.TextStyleId = database.Textstyle;
            AddEntity(database, transaction, modelSpace, layerId, entity,
                instanceId, role, summary);
        }

        private static Polyline CreateStrip(SupportDetailPoint start,
            SupportDetailPoint end, SupportDetailVector normal, double width,
            Point3d origin)
        {
            SupportDetailVector offset = normal.Unit() * (width / 2.0);
            return CreatePolygon(new[]
            {
                start + offset,
                end + offset,
                end + offset * -1.0,
                start + offset * -1.0
            }, origin, true);
        }

        private static Polyline CreateRectangle(double left, double bottom,
            double right, double top, Point3d origin)
        {
            double minimumX = Math.Min(left, right);
            double maximumX = Math.Max(left, right);
            double minimumY = Math.Min(bottom, top);
            double maximumY = Math.Max(bottom, top);
            return CreatePolygon(new[]
            {
                new SupportDetailPoint(minimumX, minimumY),
                new SupportDetailPoint(maximumX, minimumY),
                new SupportDetailPoint(maximumX, maximumY),
                new SupportDetailPoint(minimumX, maximumY)
            }, origin, true);
        }

        private static Polyline CreatePolygon(
            IList<SupportDetailPoint> points, Point3d origin, bool closed)
        {
            Polyline polyline = new Polyline(points.Count);
            for (int index = 0; index < points.Count; index++)
            {
                polyline.AddVertexAt(index, new Point2d(
                    origin.X + points[index].X,
                    origin.Y + points[index].Y), 0.0, 0.0, 0.0);
            }
            polyline.Elevation = origin.Z;
            polyline.Closed = closed;
            return polyline;
        }

        private static Point3d ToPoint3d(SupportDetailPoint point,
            Point3d origin)
        {
            return new Point3d(origin.X + point.X, origin.Y + point.Y,
                origin.Z);
        }

        private static ResultBuffer CreateEntityMetadata(string instanceId,
            string role)
        {
            return new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName,
                    RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                    EntityMarker),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                    EntityVersion),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                    instanceId),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                    role ?? String.Empty));
        }

        private static void WriteInstanceRecord(Database database,
            Transaction transaction, SupportDetailInstanceData data)
        {
            DBDictionary root = OpenInstanceDictionary(database, transaction,
                OpenMode.ForWrite, true);
            string serialized = SupportDetailMetadataCodec.SerializeSettings(
                data.Settings);
            IList<string> chunks =
                SupportDetailMetadataCodec.SplitIntoChunks(serialized);
            List<TypedValue> values = new List<TypedValue>();
            values.Add(new TypedValue((int)DxfCode.Text, RecordVersion));
            values.Add(new TypedValue((int)DxfCode.Text, data.InstanceId));
            values.Add(new TypedValue((int)DxfCode.Real, data.Origin.X));
            values.Add(new TypedValue((int)DxfCode.Real, data.Origin.Y));
            for (int index = 0; index < chunks.Count; index++)
                values.Add(new TypedValue((int)DxfCode.Text, chunks[index]));
            ResultBuffer buffer = new ResultBuffer(values.ToArray());

            if (root.Contains(data.InstanceId))
            {
                Xrecord existing = transaction.GetObject(
                    root.GetAt(data.InstanceId), OpenMode.ForWrite, false)
                    as Xrecord;
                if (existing == null)
                    throw new InvalidOperationException(
                        "同名支架参数记录不是 Xrecord。");
                if (existing.Data != null)
                    existing.Data.Dispose();
                existing.Data = buffer;
                return;
            }

            Xrecord record = new Xrecord();
            record.Data = buffer;
            root.SetAt(data.InstanceId, record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        private static DBDictionary OpenInstanceDictionary(Database database,
            Transaction transaction, OpenMode mode, bool create)
        {
            DBDictionary namedObjects = (DBDictionary)transaction.GetObject(
                database.NamedObjectsDictionaryId,
                create ? OpenMode.ForWrite : OpenMode.ForRead);
            if (namedObjects.Contains(DictionaryName))
            {
                return transaction.GetObject(namedObjects.GetAt(DictionaryName),
                    mode, false) as DBDictionary;
            }
            if (!create)
                return null;
            DBDictionary root = new DBDictionary();
            namedObjects.SetAt(DictionaryName, root);
            transaction.AddNewlyCreatedDBObject(root, true);
            return root;
        }

        private static void EnsureRegApp(Database database,
            Transaction transaction)
        {
            RegAppTable table = (RegAppTable)transaction.GetObject(
                database.RegAppTableId, OpenMode.ForRead);
            if (table.Has(RegAppName))
                return;
            table.UpgradeOpen();
            RegAppTableRecord record = new RegAppTableRecord();
            record.Name = RegAppName;
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        private static ObjectId EnsureDimensionStyle(Database database,
            Transaction transaction)
        {
            const string styleName = "STPV_SUPPORT_DIM";
            DimStyleTable table = (DimStyleTable)transaction.GetObject(
                database.DimStyleTableId, OpenMode.ForRead);
            if (table.Has(styleName))
                return table[styleName];
            table.UpgradeOpen();
            DimStyleTableRecord record = new DimStyleTableRecord();
            record.Name = styleName;
            record.Dimtxt = 95.0;
            record.Dimasz = 70.0;
            record.Dimexe = 55.0;
            record.Dimexo = 25.0;
            record.Dimgap = 28.0;
            record.Dimdec = 0;
            record.Dimtxsty = database.Textstyle;
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
            return record.ObjectId;
        }

        private static string GetString(TypedValue[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length ||
                values[index].Value == null)
                return String.Empty;
            return Convert.ToString(values[index].Value,
                CultureInfo.InvariantCulture) ?? String.Empty;
        }
    }
}
