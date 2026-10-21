using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructuralToolbox.Core;
using StructuralToolbox.Settings;

namespace StructuralToolbox.Commands
{
    [Flags]
    internal enum ExistingRailState
    {
        None = 0,
        ToolRail = 1,
        OutputLayerEntity = 2,
        LegacyRail = 4
    }

    internal sealed class ToolEntityData
    {
        public string Kind { get; set; }
        public string Version { get; set; }
        public string RunId { get; set; }
        public string RailRunId { get; set; }
        public int? EntityIndex { get; set; }
        public string SegmentId { get; set; }
        public string DirectionGroupId { get; set; }
        public string IslandId { get; set; }
        public string ComponentRowId { get; set; }
        public int? RailIndex { get; set; }
        public string SourceHash { get; set; }
        public string ComponentLayer { get; set; }
        public string RailGeometryHash { get; set; }
        public string ComponentGeometryHash { get; set; }
    }

    internal sealed class RailRunMetadata
    {
        public RailRunMetadata()
        {
            CreatedObjectIds = new List<ObjectId>();
        }

        public string ComponentLayer { get; set; }
        public string ComponentGeometryHash { get; set; }
        public string RailGeometryHash { get; set; }
        public ICollection<ObjectId> CreatedObjectIds { get; private set; }
    }

    internal sealed class ToolRunWindowInspection
    {
        public ToolRunWindowInspection()
        {
            RunIds = new HashSet<string>(StringComparer.Ordinal);
            PartialRunIds = new HashSet<string>(StringComparer.Ordinal);
        }

        public ISet<string> RunIds { get; private set; }
        public ISet<string> PartialRunIds { get; private set; }
    }

    internal sealed class RailDrawingStyle
    {
        private bool _previewUsesSelectedColor = true;
        private System.Drawing.Color _previewRgb;

        public string LayerName { get; set; }
        public double GlobalWidth { get; set; }
        public RailColorApplication ColorApplication { get; set; }
        public RailColorKind ColorKind { get; set; }
        public int ColorIndex { get; set; }
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public bool ChangeExistingLayerColor { get; set; }

        public Color CreateSelectedColor()
        {
            if (ColorKind == RailColorKind.TrueColor)
                return Color.FromRgb(Red, Green, Blue);
            return Color.FromColorIndex(ColorMethod.ByAci, (short)ColorIndex);
        }

        public Color CreatePreviewColor()
        {
            if (_previewUsesSelectedColor)
                return CreateSelectedColor();
            return Color.FromRgb(_previewRgb.R, _previewRgb.G, _previewRgb.B);
        }

        public void UseExistingLayerColorForPreview(System.Drawing.Color color)
        {
            _previewRgb = color;
            _previewUsesSelectedColor = false;
        }
    }

    internal sealed class RailLayerInspection
    {
        public bool Exists { get; set; }
        public bool ColorMatches { get; set; }
        public System.Drawing.Color ExistingColor { get; set; }
    }

    internal static class RailDrawingService
    {
        internal const string ToolRegAppName = "FOURJTOOLS";
        internal const string RailEntityKind = "RAIL";
        internal const string ToolEntityVersion = "1.0.8";
        private const string StructuredMetadataMarker = "META_V3";
        private const string LegacyRailLayerName = "导轨";

        public static RailDrawingStyle CreateStyle(RailSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            settings.Normalize();
            try
            {
                SymbolUtilityServices.ValidateSymbolName(settings.RailLayerName, true);
            }
            catch (System.Exception exception)
            {
                throw new InvalidOperationException(
                    "输出图层名称无效：“" + settings.RailLayerName + "”。", exception);
            }

            return new RailDrawingStyle
            {
                LayerName = settings.RailLayerName,
                GlobalWidth = settings.RailGlobalWidth,
                ColorApplication = settings.RailColorApplication,
                ColorKind = settings.RailColorKind,
                ColorIndex = settings.RailColorIndex,
                Red = (byte)settings.RailColorRed,
                Green = (byte)settings.RailColorGreen,
                Blue = (byte)settings.RailColorBlue
            };
        }

        public static RailLayerInspection InspectLayer(Database database,
            Transaction transaction, RailDrawingStyle style)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(
                database.LayerTableId, OpenMode.ForRead);
            if (!layerTable.Has(style.LayerName))
                return new RailLayerInspection { Exists = false, ColorMatches = true };

            LayerTableRecord existing = (LayerTableRecord)transaction.GetObject(
                layerTable[style.LayerName], OpenMode.ForRead);
            ValidateWritableLayer(existing, style.LayerName);

            using (Color selectedColor = style.CreateSelectedColor())
            using (Color existingColor = existing.Color)
            {
                return new RailLayerInspection
                {
                    Exists = true,
                    ColorMatches = ColorsMatch(existingColor, selectedColor),
                    ExistingColor = existingColor.ColorValue
                };
            }
        }

        public static ObjectId GetOrCreateRailLayer(Database database, Transaction transaction,
            RailDrawingStyle style)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(
                database.LayerTableId, OpenMode.ForRead);

            if (layerTable.Has(style.LayerName))
            {
                ObjectId existingId = layerTable[style.LayerName];
                LayerTableRecord existing = (LayerTableRecord)transaction.GetObject(
                    existingId, OpenMode.ForRead);
                ValidateWritableLayer(existing, style.LayerName);
                if (style.ChangeExistingLayerColor)
                {
                    existing.UpgradeOpen();
                    using (Color selectedColor = style.CreateSelectedColor())
                        existing.Color = selectedColor;
                }
                return existingId;
            }

            layerTable.UpgradeOpen();
            LayerTableRecord layer = new LayerTableRecord();
            bool registered = false;
            try
            {
                layer.Name = style.LayerName;
                using (Color selectedColor = style.CreateSelectedColor())
                    layer.Color = selectedColor;
                ObjectId layerId = layerTable.Add(layer);
                transaction.AddNewlyCreatedDBObject(layer, true);
                registered = true;
                return layerId;
            }
            finally
            {
                if (!registered)
                    layer.Dispose();
            }
        }

        public static ExistingRailState FindRailEntitiesInWindow(Database database,
            Transaction transaction, Point3d firstCorner, Point3d secondCorner,
            string outputLayerName)
        {
            double minimumX = Math.Min(firstCorner.X, secondCorner.X);
            double maximumX = Math.Max(firstCorner.X, secondCorner.X);
            double minimumY = Math.Min(firstCorner.Y, secondCorner.Y);
            double maximumY = Math.Max(firstCorner.Y, secondCorner.Y);
            ExistingRailState state = ExistingRailState.None;

            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (entity == null)
                    continue;

                ToolEntityData toolData;
                bool isToolEntity = TryGetToolEntityData(entity, out toolData);
                bool isToolRail = isToolEntity && String.Equals(toolData.Kind,
                    RailEntityKind, StringComparison.Ordinal);
                bool isOutputLayer = String.Equals(entity.Layer, outputLayerName,
                    StringComparison.OrdinalIgnoreCase);
                bool isLegacyRail = !isToolRail && !isOutputLayer &&
                    IsLegacyRail(entity, outputLayerName);
                if (!isToolRail && !isOutputLayer && !isLegacyRail)
                    continue;

                try
                {
                    Extents3d extents = entity.GeometricExtents;
                    if (extents.MaxPoint.X < minimumX || extents.MinPoint.X > maximumX ||
                        extents.MaxPoint.Y < minimumY || extents.MinPoint.Y > maximumY)
                    {
                        continue;
                    }

                    if (isToolRail)
                        state |= ExistingRailState.ToolRail;
                    else if (isLegacyRail)
                        state |= ExistingRailState.LegacyRail;
                    else
                        state |= ExistingRailState.OutputLayerEntity;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    // Entities without geometric extents cannot intersect a finite selection window.
                }
            }

            return state;
        }

        public static bool IsToolRail(Entity entity)
        {
            ToolEntityData data;
            return TryGetToolEntityData(entity, out data) &&
                String.Equals(data.Kind, RailEntityKind, StringComparison.Ordinal);
        }

        internal static bool TryGetToolEntityData(Entity entity, out ToolEntityData data)
        {
            data = null;
            if (entity == null)
                return false;

            using (ResultBuffer xdata = entity.GetXDataForApplication(ToolRegAppName))
            {
                if (xdata == null)
                    return false;
                TypedValue[] values = xdata.AsArray();
                if (values.Length < 4 ||
                    values[0].TypeCode != (int)DxfCode.ExtendedDataRegAppName ||
                    !String.Equals(values[0].Value as string, ToolRegAppName,
                        StringComparison.Ordinal) ||
                    values[1].TypeCode != (int)DxfCode.ExtendedDataAsciiString ||
                    values[2].TypeCode != (int)DxfCode.ExtendedDataAsciiString ||
                    values[3].TypeCode != (int)DxfCode.ExtendedDataAsciiString)
                {
                    return false;
                }

                string kind = values[1].Value as string;
                string version = values[2].Value as string;
                string runId = values[3].Value as string;
                if (String.IsNullOrWhiteSpace(kind) || String.IsNullOrWhiteSpace(runId))
                    return false;
                if (!String.Equals(kind, RailEntityKind,
                    StringComparison.Ordinal))
                    return false;

                data = new ToolEntityData
                {
                    Kind = kind,
                    Version = version ?? String.Empty,
                    RunId = runId,
                    RailRunId = String.Equals(kind, RailEntityKind,
                        StringComparison.Ordinal) ? runId : String.Empty
                };
                bool isStructuredMetadata = values.Length > 4 &&
                    values[4].TypeCode == (int)DxfCode.ExtendedDataAsciiString &&
                    String.Equals(values[4].Value as string,
                        StructuredMetadataMarker, StringComparison.Ordinal);
                if (isStructuredMetadata)
                {
                    ParseStructuredMetadata(values, 5, data);
                }
                else
                {
                    ParseLegacyMetadata(values, data);
                }

                return true;
            }
        }

        private static void ParseStructuredMetadata(TypedValue[] values,
            int startIndex, ToolEntityData data)
        {
            Dictionary<string, TypedValue> metadata =
                new Dictionary<string, TypedValue>(StringComparer.Ordinal);
            for (int index = startIndex; index + 1 < values.Length; index += 2)
            {
                if (values[index].TypeCode !=
                    (int)DxfCode.ExtendedDataAsciiString)
                {
                    continue;
                }
                string key = values[index].Value as string;
                if (!String.IsNullOrWhiteSpace(key))
                {
                    metadata[key] = values[index + 1];
                }
            }

            data.EntityIndex = GetMetadataInteger(metadata, "ENTITY_INDEX");
            data.SegmentId = GetMetadataString(metadata, "SEGMENT_ID");
            data.DirectionGroupId = GetMetadataString(
                metadata, "DIRECTION_GROUP_ID");
            data.IslandId = GetMetadataString(metadata, "ISLAND_ID");
            data.ComponentRowId = GetMetadataString(metadata, "COMPONENT_ROW_ID");
            data.RailIndex = GetMetadataInteger(metadata, "RAIL_INDEX");
            data.SourceHash = GetMetadataString(metadata, "SOURCE_HASH");
            data.ComponentLayer = GetMetadataString(metadata, "COMPONENT_LAYER");
            data.RailGeometryHash = GetMetadataString(
                metadata, "RAIL_GEOMETRY_HASH");
            data.ComponentGeometryHash = GetMetadataString(
                metadata, "COMPONENT_GEOMETRY_HASH");
        }

        private static void ParseLegacyMetadata(TypedValue[] values,
            ToolEntityData data)
        {
            data.EntityIndex = GetInteger(values, 4);
            if (String.Equals(data.Kind, RailEntityKind, StringComparison.Ordinal))
            {
                data.SegmentId = GetString(values, 5);
                data.DirectionGroupId = GetString(values, 6);
                data.IslandId = GetString(values, 7);
                data.ComponentRowId = GetString(values, 8);
                data.RailIndex = GetInteger(values, 9);
                data.SourceHash = GetString(values, 10);
            }
        }

        private static string GetMetadataString(
            IDictionary<string, TypedValue> metadata, string key)
        {
            TypedValue value;
            return metadata.TryGetValue(key, out value) &&
                value.TypeCode == (int)DxfCode.ExtendedDataAsciiString
                ? value.Value as string ?? String.Empty
                : String.Empty;
        }

        private static int? GetMetadataInteger(
            IDictionary<string, TypedValue> metadata, string key)
        {
            TypedValue value;
            if (!metadata.TryGetValue(key, out value))
                return null;
            if (value.TypeCode != (int)DxfCode.ExtendedDataInteger16 &&
                value.TypeCode != (int)DxfCode.ExtendedDataInteger32)
            {
                return null;
            }
            return Convert.ToInt32(value.Value,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string GetString(TypedValue[] values, int index)
        {
            return index < values.Length && values[index].TypeCode ==
                (int)DxfCode.ExtendedDataAsciiString
                ? values[index].Value as string ?? String.Empty
                : String.Empty;
        }

        private static int? GetInteger(TypedValue[] values, int index)
        {
            if (index >= values.Length ||
                (values[index].TypeCode != (int)DxfCode.ExtendedDataInteger16 &&
                values[index].TypeCode != (int)DxfCode.ExtendedDataInteger32))
            {
                return null;
            }
            return Convert.ToInt32(values[index].Value,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static ToolRunWindowInspection InspectSelectedToolRuns(
            Database database, Transaction transaction,
            IEnumerable<ObjectId> selectedObjectIds)
        {
            if (database == null)
                throw new ArgumentNullException("database");
            if (transaction == null)
                throw new ArgumentNullException("transaction");
            if (selectedObjectIds == null)
                throw new ArgumentNullException("selectedObjectIds");

            HashSet<ObjectId> selected = new HashSet<ObjectId>(
                selectedObjectIds);
            ToolRunWindowInspection inspection = new ToolRunWindowInspection();
            Dictionary<string, int> totalByRun =
                new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> selectedByRun =
                new Dictionary<string, int>(StringComparer.Ordinal);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                ToolEntityData data;
                if (!TryGetToolEntityData(entity, out data) ||
                    !String.Equals(data.Kind, RailEntityKind, StringComparison.Ordinal))
                {
                    continue;
                }
                int total;
                totalByRun.TryGetValue(data.RailRunId, out total);
                totalByRun[data.RailRunId] = total + 1;
                if (!selected.Contains(objectId))
                    continue;
                inspection.RunIds.Add(data.RailRunId);
                int selectedCount;
                selectedByRun.TryGetValue(data.RailRunId, out selectedCount);
                selectedByRun[data.RailRunId] = selectedCount + 1;
            }
            foreach (string runId in inspection.RunIds)
            {
                int total = totalByRun[runId];
                int selectedCount;
                selectedByRun.TryGetValue(runId, out selectedCount);
                if (selectedCount != total)
                {
                    inspection.PartialRunIds.Add(runId);
                }
            }
            return inspection;
        }

        internal static int EraseToolRailsForRuns(Database database,
            Transaction transaction, ISet<string> runIds)
        {
            if (runIds == null || runIds.Count == 0)
                return 0;

            int erased = 0;
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                ToolEntityData data;
                if (!TryGetToolEntityData(entity, out data))
                    continue;
                bool eraseRail = String.Equals(data.Kind, RailEntityKind,
                    StringComparison.Ordinal) && runIds.Contains(data.RailRunId);
                if (!eraseRail)
                {
                    continue;
                }

                entity.UpgradeOpen();
                entity.Erase();
                erased++;
            }
            return erased;
        }

        private static bool IsLegacyRail(Entity entity, string outputLayerName)
        {
            if (String.Equals(outputLayerName, LegacyRailLayerName,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Polyline polyline = entity as Polyline;
            return polyline != null && !polyline.Closed &&
                polyline.NumberOfVertices == 2 &&
                String.Equals(entity.Layer, LegacyRailLayerName,
                    StringComparison.OrdinalIgnoreCase);
        }

        public static int AppendRails(Database database, Transaction transaction,
            ObjectId layerId, IList<RailSegment> segments, RailDrawingStyle style)
        {
            return AppendRails(database, transaction, layerId, segments, style,
                Guid.NewGuid().ToString("N"));
        }

        internal static int AppendRails(Database database, Transaction transaction,
            ObjectId layerId, IList<RailSegment> segments, RailDrawingStyle style,
            string runId)
        {
            return AppendRails(database, transaction, layerId, segments, null,
                style, runId);
        }

        internal static int AppendRails(Database database, Transaction transaction,
            ObjectId layerId, RailLayoutResult layout, RailDrawingStyle style,
            string runId)
        {
            return AppendRails(database, transaction, layerId, layout, style,
                runId, null);
        }

        internal static int AppendRails(Database database, Transaction transaction,
            ObjectId layerId, RailLayoutResult layout, RailDrawingStyle style,
            string fallbackRunId, IDictionary<string, string> runIdsByIsland)
        {
            return AppendRails(database, transaction, layerId, layout, style,
                fallbackRunId, runIdsByIsland, null);
        }

        internal static int AppendRails(Database database, Transaction transaction,
            ObjectId layerId, RailLayoutResult layout, RailDrawingStyle style,
            string fallbackRunId, IDictionary<string, string> runIdsByIsland,
            RailRunMetadata runMetadata)
        {
            if (layout == null)
                throw new ArgumentNullException("layout");
            IList<RailSegmentMetadata> metadata =
                layout.SegmentMetadata != null &&
                layout.SegmentMetadata.Count == layout.Segments.Count
                    ? layout.SegmentMetadata
                    : null;
            return AppendRails(database, transaction, layerId, layout.Segments,
                metadata, style, fallbackRunId, runIdsByIsland, runMetadata);
        }

        private static int AppendRails(Database database, Transaction transaction,
            ObjectId layerId, IList<RailSegment> segments,
            IList<RailSegmentMetadata> metadata, RailDrawingStyle style,
            string runId)
        {
            return AppendRails(database, transaction, layerId, segments, metadata,
                style, runId, null, null);
        }

        private static int AppendRails(Database database, Transaction transaction,
            ObjectId layerId, IList<RailSegment> segments,
            IList<RailSegmentMetadata> metadata, RailDrawingStyle style,
            string fallbackRunId, IDictionary<string, string> runIdsByIsland)
        {
            return AppendRails(database, transaction, layerId, segments, metadata,
                style, fallbackRunId, runIdsByIsland, null);
        }

        private static int AppendRails(Database database, Transaction transaction,
            ObjectId layerId, IList<RailSegment> segments,
            IList<RailSegmentMetadata> metadata, RailDrawingStyle style,
            string fallbackRunId, IDictionary<string, string> runIdsByIsland,
            RailRunMetadata runMetadata)
        {
            EnsureRegApp(database, transaction);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
            if (String.IsNullOrWhiteSpace(fallbackRunId))
                throw new ArgumentException("Run id is required.", "runId");

            int created = 0;
            for (int index = 0; index < segments.Count; index++)
            {
                RailSegment segment = segments[index];
                Polyline rail = CreatePolyline(segment, style.GlobalWidth);
                bool registered = false;
                try
                {
                    rail.SetDatabaseDefaults(database);
                    rail.LayerId = layerId;
                    rail.ConstantWidth = style.GlobalWidth;
                    if (style.ColorApplication == RailColorApplication.ByEntity)
                    {
                        using (Color selectedColor = style.CreateSelectedColor())
                            rail.Color = selectedColor;
                    }
                    else
                        rail.ColorIndex = 256;

                    RailSegmentMetadata segmentMetadata = metadata == null
                        ? null
                        : metadata[index];
                    string entityRunId = fallbackRunId;
                    string islandRunId;
                    if (segmentMetadata != null && runIdsByIsland != null &&
                        runIdsByIsland.TryGetValue(
                            segmentMetadata.IslandId, out islandRunId) &&
                        !String.IsNullOrWhiteSpace(islandRunId))
                    {
                        entityRunId = islandRunId;
                    }
                    using (ResultBuffer xdata = CreateRailXData(
                        entityRunId, index, segmentMetadata, segment,
                        runMetadata))
                        rail.XData = xdata;

                    modelSpace.AppendEntity(rail);
                    transaction.AddNewlyCreatedDBObject(rail, true);
                    if (runMetadata != null &&
                        runMetadata.CreatedObjectIds != null)
                    {
                        runMetadata.CreatedObjectIds.Add(rail.ObjectId);
                    }
                    registered = true;
                    created++;
                }
                finally
                {
                    if (!registered)
                        rail.Dispose();
                }
            }

            return created;
        }

        private static Polyline CreatePolyline(RailSegment segment, double globalWidth)
        {
            Polyline rail = new Polyline(2);
            rail.AddVertexAt(0, new Point2d(segment.Start.X, segment.Start.Y), 0.0, 0.0, 0.0);
            rail.AddVertexAt(1, new Point2d(segment.End.X, segment.End.Y), 0.0, 0.0, 0.0);
            rail.Elevation = segment.Elevation;
            rail.ConstantWidth = globalWidth;
            rail.Closed = false;
            return rail;
        }

        private static ResultBuffer CreateRailXData(string runId, int segmentIndex,
            RailSegmentMetadata metadata, RailSegment segment,
            RailRunMetadata runMetadata)
        {
            List<KeyValuePair<string, TypedValue>> values =
                new List<KeyValuePair<string, TypedValue>>();
            values.Add(MetadataInteger("ENTITY_INDEX", segmentIndex));
            if (metadata != null)
            {
                values.Add(MetadataString("SEGMENT_ID", metadata.Id));
                values.Add(MetadataString("DIRECTION_GROUP_ID",
                    metadata.DirectionGroupId));
                values.Add(MetadataString("ISLAND_ID", metadata.IslandId));
                values.Add(MetadataString("COMPONENT_ROW_ID",
                    metadata.ComponentRowId));
                values.Add(MetadataInteger("RAIL_INDEX", metadata.RailIndex));
                values.Add(MetadataString("SOURCE_HASH",
                    CreateSourceHash(metadata.SourcePanelIds)));
            }
            if (runMetadata != null)
            {
                values.Add(MetadataString("COMPONENT_LAYER",
                    runMetadata.ComponentLayer));
                values.Add(MetadataString("COMPONENT_GEOMETRY_HASH",
                    runMetadata.ComponentGeometryHash));
                if (!String.IsNullOrWhiteSpace(runMetadata.RailGeometryHash))
                {
                    values.Add(MetadataString("RAIL_GEOMETRY_HASH",
                        runMetadata.RailGeometryHash));
                }
            }
            if (String.IsNullOrWhiteSpace(runMetadata == null
                ? null
                : runMetadata.RailGeometryHash))
            {
                values.Add(MetadataString("RAIL_GEOMETRY_HASH",
                    CreateSegmentHash(segment)));
            }
            return CreateStructuredToolXData(RailEntityKind, runId, values);
        }

        private static TypedValue XDataString(string value, string fieldName)
        {
            return new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                ToolMetadataCodec.RequireXDataString(value, fieldName));
        }

        private static KeyValuePair<string, TypedValue> MetadataString(
            string key, string value)
        {
            return new KeyValuePair<string, TypedValue>(
                key, XDataString(value, key));
        }

        private static KeyValuePair<string, TypedValue> MetadataInteger(
            string key, int value)
        {
            return new KeyValuePair<string, TypedValue>(key,
                new TypedValue((int)DxfCode.ExtendedDataInteger32, value));
        }

        private static ResultBuffer CreateStructuredToolXData(string kind,
            string runId, IList<KeyValuePair<string, TypedValue>> metadata)
        {
            List<TypedValue> values = new List<TypedValue>();
            values.Add(new TypedValue(
                (int)DxfCode.ExtendedDataRegAppName, ToolRegAppName));
            values.Add(XDataString(kind, "KIND"));
            values.Add(XDataString(ToolEntityVersion, "VERSION"));
            values.Add(XDataString(runId, "RUN_ID"));
            values.Add(XDataString(StructuredMetadataMarker, "METADATA_FORMAT"));
            if (metadata != null)
            {
                for (int index = 0; index < metadata.Count; index++)
                {
                    values.Add(XDataString(metadata[index].Key, "METADATA_KEY"));
                    values.Add(metadata[index].Value);
                }
            }
            return new ResultBuffer(values.ToArray());
        }

        private static string CreateSegmentHash(RailSegment segment)
        {
            if (segment == null)
                return String.Empty;
            string source = String.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:R},{1:R},{2:R}|{3:R},{4:R},{5:R}",
                segment.Start.X, segment.Start.Y, segment.Elevation,
                segment.End.X, segment.End.Y, segment.Elevation);
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(source));
                return BitConverter.ToString(digest, 0, 12).Replace("-", String.Empty);
            }
        }

        private static string CreateSourceHash(IList<string> sourceIds)
        {
            StringBuilder source = new StringBuilder();
            if (sourceIds != null)
            {
                for (int index = 0; index < sourceIds.Count; index++)
                {
                    if (index > 0)
                        source.Append('|');
                    source.Append(sourceIds[index] ?? String.Empty);
                }
            }
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(source.ToString()));
                return BitConverter.ToString(digest, 0, 12).Replace("-", String.Empty);
            }
        }

        internal static void EnsureRegApp(Database database, Transaction transaction)
        {
            RegAppTable table = (RegAppTable)transaction.GetObject(
                database.RegAppTableId, OpenMode.ForRead);
            if (table.Has(ToolRegAppName))
                return;

            table.UpgradeOpen();
            RegAppTableRecord record = new RegAppTableRecord();
            bool registered = false;
            try
            {
                record.Name = ToolRegAppName;
                table.Add(record);
                transaction.AddNewlyCreatedDBObject(record, true);
                registered = true;
            }
            finally
            {
                if (!registered)
                    record.Dispose();
            }
        }

        private static void ValidateWritableLayer(LayerTableRecord layer, string layerName)
        {
            if (layer.IsLocked)
                throw new InvalidOperationException("图层“" + layerName + "”已锁定，请先解锁。 ");
            if (layer.IsOff || layer.IsFrozen)
            {
                throw new InvalidOperationException(
                    "图层“" + layerName + "”已关闭或冻结，请先打开并解冻。 ");
            }
        }

        private static bool ColorsMatch(Color first, Color second)
        {
            if (first.ColorMethod == ColorMethod.ByAci &&
                second.ColorMethod == ColorMethod.ByAci &&
                first.ColorIndex == second.ColorIndex)
            {
                return true;
            }
            return first.ColorValue.ToArgb() == second.ColorValue.ToArgb();
        }

        public sealed class PreviewSession : IDisposable
        {
            private static readonly List<Entity> FailedCleanupEntities = new List<Entity>();
            private readonly List<Entity> _entities = new List<Entity>();
            private bool _disposed;

            public PreviewSession(IList<RailSegment> segments, RailDrawingStyle style)
            {
                if (!RetryFailedCleanup())
                {
                    throw new InvalidOperationException(
                        "上一次导轨预览仍未能从图形系统中清除。请执行 REGEN 后重试。 ");
                }

                IntegerCollection viewports = new IntegerCollection();
                for (int index = 0; index < segments.Count; index++)
                {
                    Polyline preview = CreatePolyline(segments[index], style.GlobalWidth);
                    using (Color previewColor = style.CreatePreviewColor())
                        preview.Color = previewColor;
                    bool added;
                    try
                    {
                        added = Autodesk.AutoCAD.GraphicsInterface.TransientManager
                            .CurrentTransientManager.AddTransient(
                                preview,
                                Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode.DirectShortTerm,
                                128,
                                viewports);
                    }
                    catch (System.Exception exception)
                    {
                        bool cleanupSucceeded = TryRemoveTransient(preview, viewports);
                        Dispose();
                        if (!cleanupSucceeded || !CleanupSucceeded)
                        {
                            throw new InvalidOperationException(
                                "导轨预览初始化失败，且图形系统清理未完全成功。请执行 REGEN 后重试。",
                                exception);
                        }
                        throw new InvalidOperationException(
                            "导轨预览未能完整显示，本次不会生成实体。", exception);
                    }

                    if (!added)
                    {
                        preview.Dispose();
                        Dispose();
                        throw new InvalidOperationException(
                            "导轨预览未能完整显示，本次不会生成实体。 ");
                    }
                    _entities.Add(preview);
                }
            }

            public bool CleanupSucceeded { get; private set; }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                CleanupSucceeded = true;
                IntegerCollection viewports = new IntegerCollection();
                for (int index = 0; index < _entities.Count; index++)
                {
                    Entity entity = _entities[index];
                    if (!TryRemoveTransient(entity, viewports))
                        CleanupSucceeded = false;
                }
                _entities.Clear();
            }

            private static bool TryRemoveTransient(Entity entity, IntegerCollection viewports)
            {
                try
                {
                    bool erased = Autodesk.AutoCAD.GraphicsInterface.TransientManager
                        .CurrentTransientManager.EraseTransient(entity, viewports);
                    if (!erased)
                    {
                        RetainFailedCleanupEntity(entity);
                        return false;
                    }
                    entity.Dispose();
                    return true;
                }
                catch
                {
                    RetainFailedCleanupEntity(entity);
                    return false;
                }
            }

            private static void RetainFailedCleanupEntity(Entity entity)
            {
                lock (FailedCleanupEntities)
                {
                    if (!FailedCleanupEntities.Contains(entity))
                        FailedCleanupEntities.Add(entity);
                }
            }

            private static bool RetryFailedCleanup()
            {
                lock (FailedCleanupEntities)
                {
                    IntegerCollection viewports = new IntegerCollection();
                    for (int index = FailedCleanupEntities.Count - 1; index >= 0; index--)
                    {
                        Entity entity = FailedCleanupEntities[index];
                        try
                        {
                            bool erased = Autodesk.AutoCAD.GraphicsInterface.TransientManager
                                .CurrentTransientManager.EraseTransient(entity, viewports);
                            if (!erased)
                                continue;

                            entity.Dispose();
                            FailedCleanupEntities.RemoveAt(index);
                        }
                        catch
                        {
                            // Keep the drawable alive while AutoCAD may still reference it.
                        }
                    }

                    return FailedCleanupEntities.Count == 0;
                }
            }
        }
    }
}
