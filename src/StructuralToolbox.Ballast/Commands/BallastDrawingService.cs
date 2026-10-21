using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructuralToolbox.Ballast.Core;
using StructuralToolbox.Ballast.Settings;

namespace StructuralToolbox.Ballast.Commands
{
    internal static class BallastDrawingService
    {
        internal const string ToolRegAppName = "BALLAST_TOOL";
        internal const string MarkerValue = "BALLAST";
        internal const string PierMarkerValue = "BALLAST_PIER";
        private const int PierMetadataVersion = 1;

        public static ObjectId EnsureBallastLayer(Database database,
            Transaction transaction, string layerName)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(
                database.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(layerName))
            {
                LayerTableRecord existing = (LayerTableRecord)transaction.GetObject(
                    layerTable[layerName], OpenMode.ForRead);
                if (existing.IsLocked)
                    throw new InvalidOperationException(
                        "输出图层【" + layerName + "】已锁定，请解锁后重试。 ");
                if (existing.IsFrozen || existing.IsOff)
                    throw new InvalidOperationException(
                        "输出图层【" + layerName + "】已冻结或关闭，请恢复后重试。 ");
                return existing.ObjectId;
            }

            layerTable.UpgradeOpen();
            LayerTableRecord record = new LayerTableRecord();
            record.Name = layerName;
            record.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)7);
            layerTable.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
            return record.ObjectId;
        }

        public static void EnsureRegApp(Database database, Transaction transaction)
        {
            RegAppTable table = (RegAppTable)transaction.GetObject(
                database.RegAppTableId, OpenMode.ForRead);
            if (table.Has(ToolRegAppName))
                return;
            table.UpgradeOpen();
            RegAppTableRecord record = new RegAppTableRecord();
            record.Name = ToolRegAppName;
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        public static bool IsToolFrame(Entity entity)
        {
            if (entity == null)
                return false;
            using (ResultBuffer buffer = entity.GetXDataForApplication(
                ToolRegAppName))
            {
                if (buffer == null)
                    return false;
                TypedValue[] values = buffer.AsArray();
                return values.Length >= 2 &&
                    values[1].TypeCode == (int)DxfCode.ExtendedDataAsciiString &&
                    String.Equals(values[1].Value as string, MarkerValue,
                        StringComparison.Ordinal);
            }
        }

        public static int EraseSelectedUnmodifiedFrames(Transaction transaction,
            IEnumerable<ObjectId> objectIds, out int protectedCount)
        {
            protectedCount = 0;
            int erased = 0;
            if (objectIds == null)
                return erased;
            foreach (ObjectId objectId in objectIds)
            {
                BlockReference reference = transaction.GetObject(objectId,
                    OpenMode.ForRead, false) as BlockReference;
                if (reference == null || !IsToolFrame(reference))
                    continue;
                if (!IsUnmodified(reference))
                {
                    protectedCount++;
                    continue;
                }
                reference.UpgradeOpen();
                reference.Erase();
                erased++;
            }
            return erased;
        }

        public static BallastDrawSummary AppendBallasts(Database database,
            Transaction transaction, ObjectId layerId,
            BallastLayoutResult layout, string runId,
            BallastLayoutOptions options)
        {
            EnsureRegApp(database, transaction);
            BlockTable blockTable = (BlockTable)transaction.GetObject(
                database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            Dictionary<string, ObjectId> ballastBlocks =
                new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ObjectId> frameBlocks =
                new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);

            ResolveAutomaticColors(layout);

            BallastDrawSummary summary = new BallastDrawSummary();
            summary.FrameIds = new List<ObjectId>();
            summary.Items = new List<BallastStatisticItem>();
            for (int planIndex = 0; planIndex < layout.Plans.Count; planIndex++)
            {
                BallastArrayPlan plan = layout.Plans[planIndex];
                BallastLayoutOptions planOptions = plan.Options ?? options;
                ObjectId frameBtrId = EnsureFrameBlock(database, transaction,
                    blockTable, layerId, plan, planOptions, ballastBlocks,
                    frameBlocks);
                double rotation = Math.Atan2(plan.Array.LongAxis.Y,
                    plan.Array.LongAxis.X);

                for (int frameIndex = 0; frameIndex < plan.Frames.Count; frameIndex++)
                {
                    BallastFramePlan frame = plan.Frames[frameIndex];
                    Point3d insertion = ToPoint3d(frame.Center, plan.Array.Panels);
                    BlockReference frameRef = new BlockReference(insertion, frameBtrId);
                    frameRef.Rotation = rotation;
                    frameRef.Color = frame.HasConflicts()
                        ? Color.FromColorIndex(ColorMethod.ByAci, 1)
                        : CreateEntityColor(planOptions);
                    frameRef.LayerId = layerId;
                    WriteMetadata(frameRef, runId, plan.ArrayId,
                        frameIndex, frame.HasConflicts(),
                        BuildConflictReason(frame), planOptions,
                        frame.Ballasts == null ? 0 : frame.Ballasts.Count);
                    modelSpace.AppendEntity(frameRef);
                    transaction.AddNewlyCreatedDBObject(frameRef, true);
                    summary.FrameIds.Add(frameRef.ObjectId);

                    summary.FrameCount++;
                    if (frame.HasConflicts())
                        summary.ConflictFrameCount++;
                    if (frame.Ballasts != null)
                    {
                        summary.BallastCount += frame.Ballasts.Count;
                        summary.Items.Add(CreateStatisticItem(planOptions,
                            frame.Ballasts.Count));
                    }
                }
            }
            return summary;
        }

        private static ObjectId EnsureFrameBlock(Database database,
            Transaction transaction, BlockTable blockTable,
            ObjectId layerId, BallastArrayPlan plan, BallastLayoutOptions options,
            IDictionary<string, ObjectId> ballastBlocks,
            IDictionary<string, ObjectId> frameBlocks)
        {
            string ballastBlockName = BuildBallastBlockName(options);
            ObjectId ballastBtrId;
            if (!ballastBlocks.TryGetValue(ballastBlockName, out ballastBtrId))
            {
                ballastBtrId = EnsureBallastBlock(database, transaction,
                    blockTable, layerId, options, ballastBlockName);
                ballastBlocks[ballastBlockName] = ballastBtrId;
            }

            List<double> ballastOffsets = BuildBallastOffsets(plan);
            string frameBlockName = BuildFrameBlockName(ballastBlockName,
                ballastOffsets);
            ObjectId frameBtrId;
            if (!frameBlocks.TryGetValue(frameBlockName, out frameBtrId))
            {
                frameBtrId = EnsureFrameBlockCore(database, transaction,
                    blockTable, layerId, ballastBtrId, ballastOffsets,
                    frameBlockName);
                frameBlocks[frameBlockName] = frameBtrId;
            }
            return frameBtrId;
        }

        private static ObjectId EnsureBallastBlock(Database database,
            Transaction transaction, BlockTable blockTable,
            ObjectId layerId, BallastLayoutOptions options, string blockName)
        {
            if (blockTable.Has(blockName))
            {
                ObjectId existingId = blockTable[blockName];
                BlockTableRecord existing = transaction.GetObject(existingId,
                    OpenMode.ForWrite, false) as BlockTableRecord;
                if (existing != null)
                    WritePierMetadata(existing,
                        CreateStatisticItem(options, 1));
                return existingId;
            }

            blockTable.UpgradeOpen();
            BlockTableRecord record = new BlockTableRecord();
            record.Name = blockName;
            blockTable.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);

            double halfLong = options.ExtentAlongLong() / 2.0;
            double halfShort = options.ExtentAlongShort() / 2.0;
            if (options.Shape == BallastShape.Circle)
            {
                Circle outer = new Circle(Point3d.Origin, Vector3d.ZAxis,
                    options.Diameter / 2.0);
                SetByBlock(outer);
                outer.LayerId = layerId;
                ObjectId circleId = record.AppendEntity(outer);
                transaction.AddNewlyCreatedDBObject(outer, true);

                AddHatch(database, transaction, record, layerId,
                    HatchLoopTypes.Outermost, new ObjectIdCollection { circleId });
            }
            else
            {
                Polyline rect = BuildCenteredRect(halfLong, halfShort);
                SetByBlock(rect);
                rect.LayerId = layerId;
                ObjectId rectId = record.AppendEntity(rect);
                transaction.AddNewlyCreatedDBObject(rect, true);
                AddHatch(database, transaction, record, layerId,
                    HatchLoopTypes.Outermost, new ObjectIdCollection { rectId });
            }

            double cross = options.CrossLineLength / 2.0;
            AddLine(database, transaction, record, layerId,
                new Point3d(-cross, 0.0, 0.0),
                new Point3d(cross, 0.0, 0.0));
            AddLine(database, transaction, record, layerId,
                new Point3d(0.0, -cross, 0.0),
                new Point3d(0.0, cross, 0.0));
            WritePierMetadata(record, CreateStatisticItem(options, 1));
            return record.ObjectId;
        }

        private static ObjectId EnsureFrameBlockCore(Database database,
            Transaction transaction, BlockTable blockTable,
            ObjectId layerId, ObjectId ballastBlockId, IList<double> offsets,
            string frameBlockName)
        {
            if (blockTable.Has(frameBlockName))
                return blockTable[frameBlockName];

            blockTable.UpgradeOpen();
            BlockTableRecord frame = new BlockTableRecord();
            frame.Name = frameBlockName;
            blockTable.Add(frame);
            transaction.AddNewlyCreatedDBObject(frame, true);

            for (int index = 0; index < offsets.Count; index++)
            {
                BlockReference ballastRef = new BlockReference(
                    new Point3d(offsets[index], 0.0, 0.0),
                    ballastBlockId);
                SetByBlock(ballastRef);
                ballastRef.LayerId = layerId;
                frame.AppendEntity(ballastRef);
                transaction.AddNewlyCreatedDBObject(ballastRef, true);
            }
            return frame.ObjectId;
        }

        private static void AddHatch(Database database, Transaction transaction,
            BlockTableRecord record, ObjectId layerId, HatchLoopTypes loopType,
            ObjectIdCollection loopIds)
        {
            Hatch hatch = new Hatch();
            hatch.SetHatchPattern(HatchPatternType.PreDefined, "AR-CONC");
            hatch.PatternScale = 1.0;
            hatch.PatternAngle = 0.0;
            hatch.HatchStyle = HatchStyle.Outer;
            SetByBlock(hatch);
            hatch.LayerId = layerId;
            record.AppendEntity(hatch);
            transaction.AddNewlyCreatedDBObject(hatch, true);
            hatch.AppendLoop(loopType, loopIds);
            hatch.EvaluateHatch(true);
        }

        private static void AddLine(Database database, Transaction transaction,
            BlockTableRecord record, ObjectId layerId,
            Point3d start, Point3d end)
        {
            Line line = new Line(start, end);
            SetByBlock(line);
            line.LayerId = layerId;
            record.AppendEntity(line);
            transaction.AddNewlyCreatedDBObject(line, true);
        }

        private static Polyline BuildCenteredRect(double halfLong, double halfShort)
        {
            Polyline rect = new Polyline();
            rect.AddVertexAt(0, new Point2d(-halfLong, -halfShort), 0.0, 0.0, 0.0);
            rect.AddVertexAt(1, new Point2d(halfLong, -halfShort), 0.0, 0.0, 0.0);
            rect.AddVertexAt(2, new Point2d(halfLong, halfShort), 0.0, 0.0, 0.0);
            rect.AddVertexAt(3, new Point2d(-halfLong, halfShort), 0.0, 0.0, 0.0);
            rect.Closed = true;
            return rect;
        }

        private static void SetByBlock(Entity entity)
        {
            entity.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)0);
        }

        private static string BuildBallastBlockName(BallastLayoutOptions options)
        {
            if (options.Shape == BallastShape.Circle)
                return "PZ_圆_D" + FormatKey(options.Diameter) + "_L" +
                    FormatKey(options.CrossLineLength) + "_H" +
                    FormatKey(options.Height) + "_C" + ColorKey(options) + "_Y" +
                    HashText(options.LayerName);
            return "PZ_方_" + FormatKey(options.RectLength) + "x" +
                FormatKey(options.RectWidth) + "_L" +
                FormatKey(options.CrossLineLength) + "_H" +
                FormatKey(options.Height) + "_C" + ColorKey(options) + "_Y" +
                HashText(options.LayerName);
        }

        private static Color CreateEntityColor(BallastLayoutOptions options)
        {
            if (options == null || options.ColorMode == BallastColorMode.ByLayer)
                return Color.FromColorIndex(ColorMethod.ByAci, 256);
            if (options.ColorMode == BallastColorMode.TrueColor)
                return Color.FromRgb((byte)options.ColorRed,
                    (byte)options.ColorGreen, (byte)options.ColorBlue);
            return Color.FromColorIndex(ColorMethod.ByAci,
                (short)Math.Max(1, Math.Min(255, options.ColorIndex)));
        }

        private static void ResolveAutomaticColors(BallastLayoutResult layout)
        {
            if (layout == null || layout.Plans == null)
                return;
            int[] palette = { 3, 4, 2, 6, 5, 1 };
            Dictionary<string, int> assigned = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < layout.Plans.Count; index++)
            {
                BallastLayoutOptions options = layout.Plans[index].Options;
                if (options == null || options.ColorMode != BallastColorMode.ByLayer)
                    continue;
                string key = SpecificationKey(options);
                int colorIndex;
                if (!assigned.TryGetValue(key, out colorIndex))
                {
                    colorIndex = assigned.Count == 0
                        ? 0 : palette[(assigned.Count - 1) % palette.Length];
                    assigned.Add(key, colorIndex);
                }
                if (colorIndex > 0)
                {
                    options.ColorMode = BallastColorMode.Aci;
                    options.ColorIndex = colorIndex;
                }
            }
        }

        private static string SpecificationKey(BallastLayoutOptions options)
        {
            return ((int)options.Shape).ToString(CultureInfo.InvariantCulture) + "|" +
                FormatKey(options.Diameter) + "|" + FormatKey(options.RectLength) +
                "|" + FormatKey(options.RectWidth) + "|" +
                FormatKey(options.Height);
        }

        private static string ColorKey(BallastLayoutOptions options)
        {
            if (options == null || options.ColorMode == BallastColorMode.ByLayer)
                return "BL";
            if (options.ColorMode == BallastColorMode.TrueColor)
                return "RGB" + options.ColorRed.ToString() + "_" +
                    options.ColorGreen.ToString() + "_" + options.ColorBlue.ToString();
            return "ACI" + options.ColorIndex.ToString();
        }

        private static string BuildFrameBlockName(string ballastBlockName,
            IList<double> offsets)
        {
            string signature = String.Empty;
            for (int index = 0; index < offsets.Count; index++)
            {
                if (index > 0)
                    signature += ",";
                signature += offsets[index].ToString("0.###",
                    CultureInfo.InvariantCulture);
            }
            uint hash = 2166136261;
            for (int index = 0; index < signature.Length; index++)
            {
                hash ^= signature[index];
                hash *= 16777619;
            }
            return "PZ_榀_" + ballastBlockName + "_N" +
                offsets.Count.ToString(CultureInfo.InvariantCulture) +
                "_K" + hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string FormatKey(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture)
                .Replace("-", "M").Replace(".", "P");
        }

        private static string HashText(string value)
        {
            string text = value ?? String.Empty;
            uint hash = 2166136261;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= 16777619;
            }
            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static void WriteMetadata(BlockReference frameRef, string runId,
            string arrayId, int frameIndex, bool conflict, string conflictReason,
            BallastLayoutOptions options, int quantity)
        {
            ResultBuffer buffer = new ResultBuffer();
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName,
                ToolRegAppName));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                MarkerValue));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                runId ?? String.Empty));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                arrayId ?? String.Empty));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataInteger32,
                frameIndex));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataInteger32,
                conflict ? 1 : 0));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                conflictReason ?? String.Empty));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                frameRef.Position.X.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                frameRef.Position.Y.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                frameRef.Rotation.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                frameRef.ScaleFactors.X.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                frameRef.ScaleFactors.Y.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                frameRef.ScaleFactors.Z.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataInteger32,
                options == null ? 0 : (int)options.Shape));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                (options == null ? 0.0 : options.Diameter).ToString("R",
                    CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                (options == null ? 0.0 : options.RectLength).ToString("R",
                    CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                (options == null ? 0.0 : options.RectWidth).ToString("R",
                    CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                (options == null ? 0.0 : options.Height).ToString("R",
                    CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataInteger32,
                quantity));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                options == null ? "默认" : GetColorLabel(options)));
            frameRef.XData = buffer;
        }

        private static void WritePierMetadata(DBObject target,
            BallastStatisticItem item)
        {
            if (target == null || item == null)
                return;
            ResultBuffer buffer = new ResultBuffer();
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName,
                ToolRegAppName));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                PierMarkerValue));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataInteger32,
                PierMetadataVersion));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataInteger32,
                (int)item.Shape));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                item.Diameter.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                item.RectLength.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                item.RectWidth.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                item.Height.ToString("R", CultureInfo.InvariantCulture)));
            buffer.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                String.IsNullOrWhiteSpace(item.ColorLabel)
                    ? "默认" : item.ColorLabel.Trim()));
            target.XData = buffer;
        }

        internal static void MarkSinglePier(Entity entity,
            BallastStatisticItem item)
        {
            if (entity == null || item == null)
                throw new ArgumentNullException(entity == null
                    ? "entity" : "item");
            if (!entity.IsWriteEnabled)
                entity.UpgradeOpen();
            WritePierMetadata(entity, CopyStatisticItem(item, 1));
        }

        internal static BallastStatisticItem ReadRecognizedSinglePier(
            Transaction transaction, Entity entity)
        {
            if (entity == null || IsToolFrame(entity))
                return null;
            BallastStatisticItem marked = ReadPierMetadata(entity);
            if (marked != null)
                return CopyStatisticItem(marked, 1);

            BlockReference reference = entity as BlockReference;
            if (transaction == null || reference == null)
                return null;
            BlockTableRecord definition = transaction.GetObject(
                reference.BlockTableRecord, OpenMode.ForRead, false)
                as BlockTableRecord;
            if (definition == null || definition.IsFromExternalReference)
                return null;
            BallastStatisticItem definitionItem = ReadPierMetadata(definition);
            if (definitionItem != null && ValidatePierDefinition(
                    transaction, definition, definitionItem))
                return CopyStatisticItem(definitionItem, 1);
            BallastStatisticItem legacy;
            return BallastPierIdentity.TryParseToolBlockName(
                    definition.Name, out legacy) &&
                ValidatePierDefinition(transaction, definition, legacy)
                    ? legacy : null;
        }

        internal static BallastStatisticItem ReadTopLevelSinglePier(
            Transaction transaction, Entity entity,
            Point3d firstCorner, Point3d secondCorner)
        {
            BallastStatisticItem item = ReadRecognizedSinglePier(
                transaction, entity);
            Point3d center;
            if (item == null || !TryGetEntityCenter(entity, out center) ||
                !ContainsCenter(center, firstCorner, secondCorner))
                return null;
            return item;
        }

        internal static bool TryCreateManualPierStatistic(Entity entity,
            BallastSettings settings, out BallastStatisticItem item,
            out string reason)
        {
            item = null;
            reason = String.Empty;
            Circle circle = entity as Circle;
            if (circle != null)
            {
                double diameter = circle.Radius * 2.0;
                if (diameter <= 0.01)
                {
                    reason = "圆的直径无效。";
                    return false;
                }
                item = new BallastStatisticItem
                {
                    Shape = BallastShape.Circle,
                    Diameter = diameter,
                    Height = FindConfiguredHeight(settings,
                        BallastShape.Circle, diameter, 0.0),
                    ColorLabel = GetEntityColorLabel(circle),
                    Quantity = 1
                };
                return true;
            }

            Polyline polyline = entity as Polyline;
            double length;
            double width;
            if (polyline != null && TryMeasureRectangle(polyline,
                    out length, out width, out reason))
            {
                item = new BallastStatisticItem
                {
                    Shape = BallastShape.Rect,
                    RectLength = length,
                    RectWidth = width,
                    Height = FindConfiguredHeight(settings,
                        BallastShape.Rect, length, width),
                    ColorLabel = GetEntityColorLabel(polyline),
                    Quantity = 1
                };
                return true;
            }
            if (polyline == null)
                reason = "仅支持圆、闭合矩形或本工具的标准单墩块。";
            return false;
        }

        private static BallastStatisticItem ReadPierMetadata(DBObject target)
        {
            if (target == null)
                return null;
            using (ResultBuffer buffer = target.GetXDataForApplication(
                ToolRegAppName))
            {
                if (buffer == null)
                    return null;
                TypedValue[] values = buffer.AsArray();
                if (values.Length < 9 ||
                    !String.Equals(GetXDataString(values, 1), PierMarkerValue,
                        StringComparison.Ordinal) ||
                    GetXDataInteger(values, 2) != PierMetadataVersion)
                    return null;
                BallastShape shape = (BallastShape)GetXDataInteger(values, 3);
                double diameter;
                double rectLength;
                double rectWidth;
                double height;
                if (!Enum.IsDefined(typeof(BallastShape), shape) ||
                    !TryParseDouble(values[4], out diameter) ||
                    !TryParseDouble(values[5], out rectLength) ||
                    !TryParseDouble(values[6], out rectWidth) ||
                    !TryParseDouble(values[7], out height) || height <= 0.0 ||
                    (shape == BallastShape.Circle && diameter <= 0.0) ||
                    (shape == BallastShape.Rect &&
                        (rectLength <= 0.0 || rectWidth <= 0.0)))
                    return null;
                return new BallastStatisticItem
                {
                    Shape = shape,
                    Diameter = diameter,
                    RectLength = Math.Max(rectLength, rectWidth),
                    RectWidth = Math.Min(rectLength, rectWidth),
                    Height = height,
                    ColorLabel = GetXDataString(values, 8),
                    Quantity = 1
                };
            }
        }

        internal static BallastStatisticItem ReadStatisticItem(
            BlockReference reference)
        {
            if (reference == null)
                return null;
            using (ResultBuffer buffer = reference.GetXDataForApplication(
                ToolRegAppName))
            {
                if (buffer == null)
                    return null;
                TypedValue[] values = buffer.AsArray();
                if (values.Length < 20 || !IsToolFrame(reference))
                    return null;
                double diameter;
                double rectLength;
                double rectWidth;
                double height;
                if (!TryParseDouble(values[14], out diameter) ||
                    !TryParseDouble(values[15], out rectLength) ||
                    !TryParseDouble(values[16], out rectWidth) ||
                    !TryParseDouble(values[17], out height))
                    return null;
                return new BallastStatisticItem
                {
                    Shape = (BallastShape)GetXDataInteger(values, 13),
                    Diameter = diameter,
                    RectLength = rectLength,
                    RectWidth = rectWidth,
                    Height = height,
                    Quantity = Math.Max(0, GetXDataInteger(values, 18)),
                    ColorLabel = GetXDataString(values, 19)
                };
            }
        }

        internal static IList<BallastStatisticItem> ReadStatisticItems(
            Transaction transaction, BlockReference frame,
            Point3d firstCorner, Point3d secondCorner)
        {
            List<BallastStatisticItem> result =
                new List<BallastStatisticItem>();
            BallastStatisticItem template = ReadStatisticItem(frame);
            if (transaction == null || template == null)
                return result;

            double minimumX = Math.Min(firstCorner.X, secondCorner.X);
            double maximumX = Math.Max(firstCorner.X, secondCorner.X);
            double minimumY = Math.Min(firstCorner.Y, secondCorner.Y);
            double maximumY = Math.Max(firstCorner.Y, secondCorner.Y);
            BlockTableRecord definition = transaction.GetObject(
                frame.BlockTableRecord, OpenMode.ForRead, false)
                as BlockTableRecord;
            if (definition == null)
                return result;

            int nestedCount = 0;
            foreach (ObjectId objectId in definition)
            {
                BlockReference ballast = transaction.GetObject(objectId,
                    OpenMode.ForRead, false) as BlockReference;
                if (ballast == null)
                    continue;
                BallastStatisticItem pier = ReadRecognizedSinglePier(
                    transaction, ballast);
                if (pier == null)
                    continue;
                nestedCount++;
                Point3d center = ballast.Position.TransformBy(
                    frame.BlockTransform);
                if (center.X < minimumX - 0.01 ||
                    center.X > maximumX + 0.01 ||
                    center.Y < minimumY - 0.01 ||
                    center.Y > maximumY + 0.01)
                    continue;
                result.Add(CopyStatisticItem(pier, 1));
            }

            if (result.Count == 0 && nestedCount == 0)
            {
                // Preserve statistics for older frame definitions that did not
                // contain nested ballast references.
                result.Add(template);
            }
            return result;
        }

        private static BallastStatisticItem CopyStatisticItem(
            BallastStatisticItem source, int quantity)
        {
            return new BallastStatisticItem
            {
                Shape = source.Shape,
                Diameter = source.Diameter,
                RectLength = source.RectLength,
                RectWidth = source.RectWidth,
                Height = source.Height,
                ColorLabel = source.ColorLabel,
                Quantity = quantity
            };
        }

        private static bool ContainsCenter(Point3d center,
            Point3d firstCorner, Point3d secondCorner)
        {
            double minimumX = Math.Min(firstCorner.X, secondCorner.X);
            double maximumX = Math.Max(firstCorner.X, secondCorner.X);
            double minimumY = Math.Min(firstCorner.Y, secondCorner.Y);
            double maximumY = Math.Max(firstCorner.Y, secondCorner.Y);
            return center.X >= minimumX - 0.01 &&
                center.X <= maximumX + 0.01 &&
                center.Y >= minimumY - 0.01 &&
                center.Y <= maximumY + 0.01;
        }

        private static bool TryGetEntityCenter(Entity entity, out Point3d center)
        {
            center = Point3d.Origin;
            BlockReference reference = entity as BlockReference;
            if (reference != null)
            {
                center = reference.Position;
                return true;
            }
            Circle circle = entity as Circle;
            if (circle != null)
            {
                center = circle.Center;
                return true;
            }
            try
            {
                Extents3d extents = entity.GeometricExtents;
                center = new Point3d(
                    (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                    (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0,
                    (extents.MinPoint.Z + extents.MaxPoint.Z) / 2.0);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static bool TryMeasureRectangle(Polyline polyline,
            out double length, out double width, out string reason)
        {
            length = 0.0;
            width = 0.0;
            reason = String.Empty;
            if (polyline == null || !polyline.Closed ||
                polyline.NumberOfVertices != 4)
            {
                reason = "方墩必须是闭合的四顶点多段线。";
                return false;
            }
            Point2d[] points = new Point2d[4];
            for (int index = 0; index < 4; index++)
            {
                if (Math.Abs(polyline.GetBulgeAt(index)) > 1e-9)
                {
                    reason = "方墩多段线不能包含圆弧段。";
                    return false;
                }
                points[index] = polyline.GetPoint2dAt(index);
            }
            double[] edgeX = new double[4];
            double[] edgeY = new double[4];
            double[] edgeLength = new double[4];
            for (int index = 0; index < 4; index++)
            {
                Point2d start = points[index];
                Point2d end = points[(index + 1) % 4];
                edgeX[index] = end.X - start.X;
                edgeY[index] = end.Y - start.Y;
                edgeLength[index] = Math.Sqrt(edgeX[index] * edgeX[index] +
                    edgeY[index] * edgeY[index]);
                if (edgeLength[index] <= 0.01)
                {
                    reason = "方墩包含零长度边。";
                    return false;
                }
            }
            for (int index = 0; index < 4; index++)
            {
                int next = (index + 1) % 4;
                double dot = edgeX[index] * edgeX[next] +
                    edgeY[index] * edgeY[next];
                if (Math.Abs(dot) > edgeLength[index] *
                    edgeLength[next] * 1e-6)
                {
                    reason = "所选闭合多段线不是矩形。";
                    return false;
                }
            }
            if (Math.Abs(edgeLength[0] - edgeLength[2]) > 0.5 ||
                Math.Abs(edgeLength[1] - edgeLength[3]) > 0.5)
            {
                reason = "所选闭合多段线的对边长度不一致。";
                return false;
            }
            length = Math.Max(edgeLength[0], edgeLength[1]);
            width = Math.Min(edgeLength[0], edgeLength[1]);
            return true;
        }

        private static bool ValidatePierDefinition(Transaction transaction,
            BlockTableRecord definition, BallastStatisticItem expected)
        {
            if (transaction == null || definition == null || expected == null)
                return false;
            foreach (ObjectId objectId in definition)
            {
                Entity entity = transaction.GetObject(objectId,
                    OpenMode.ForRead, false) as Entity;
                Circle circle = entity as Circle;
                if (expected.Shape == BallastShape.Circle && circle != null &&
                    Math.Abs(circle.Radius * 2.0 - expected.Diameter) <= 0.5)
                    return true;
                Polyline polyline = entity as Polyline;
                double length;
                double width;
                string reason;
                if (expected.Shape == BallastShape.Rect && polyline != null &&
                    TryMeasureRectangle(polyline, out length, out width,
                        out reason) &&
                    Math.Abs(length - expected.RectLength) <= 0.5 &&
                    Math.Abs(width - expected.RectWidth) <= 0.5)
                    return true;
            }
            return false;
        }

        private static double FindConfiguredHeight(BallastSettings settings,
            BallastShape shape, double first, double second)
        {
            if (settings != null && settings.RowRules != null)
                for (int index = 0; index < settings.RowRules.Count; index++)
                {
                    BallastRowRule rule = settings.RowRules[index];
                    if (rule == null || rule.Shape != shape)
                        continue;
                    if (shape == BallastShape.Circle &&
                        Math.Abs(rule.Diameter - first) <= 0.5)
                        return rule.Height;
                    if (shape == BallastShape.Rect &&
                        ((Math.Abs(rule.RectLength - first) <= 0.5 &&
                          Math.Abs(rule.RectWidth - second) <= 0.5) ||
                         (Math.Abs(rule.RectLength - second) <= 0.5 &&
                          Math.Abs(rule.RectWidth - first) <= 0.5)))
                        return rule.Height;
                }
            return settings == null ? 0.0 : settings.Height;
        }

        private static string GetEntityColorLabel(Entity entity)
        {
            Color color = entity == null ? null : entity.Color;
            if (color == null || color.IsByLayer || color.IsByBlock)
                return "默认";
            if (color.ColorMethod == ColorMethod.ByAci)
                return "ACI " + color.ColorIndex.ToString(
                    CultureInfo.InvariantCulture);
            return String.Format(CultureInfo.InvariantCulture,
                "RGB {0},{1},{2}", color.Red, color.Green, color.Blue);
        }

        internal static string GetColorLabel(BallastLayoutOptions options)
        {
            if (options == null || options.ColorMode == BallastColorMode.ByLayer)
                return "默认";
            if (options.ColorMode == BallastColorMode.TrueColor)
                return String.Format(CultureInfo.InvariantCulture,
                    "RGB {0},{1},{2}", options.ColorRed, options.ColorGreen,
                    options.ColorBlue);
            return "ACI " + options.ColorIndex.ToString(
                CultureInfo.InvariantCulture);
        }

        private static BallastStatisticItem CreateStatisticItem(
            BallastLayoutOptions options, int quantity)
        {
            return new BallastStatisticItem
            {
                Shape = options.Shape,
                Diameter = options.Diameter,
                RectLength = options.RectLength,
                RectWidth = options.RectWidth,
                Height = options.Height,
                Quantity = quantity,
                ColorLabel = GetColorLabel(options)
            };
        }

        private static bool IsUnmodified(BlockReference reference)
        {
            using (ResultBuffer buffer = reference.GetXDataForApplication(
                ToolRegAppName))
            {
                if (buffer == null)
                    return false;
                TypedValue[] values = buffer.AsArray();
                if (values.Length < 13)
                    return false;
                double x;
                double y;
                double rotation;
                double scaleX;
                double scaleY;
                double scaleZ;
                if (!TryParseDouble(values[7], out x) ||
                    !TryParseDouble(values[8], out y) ||
                    !TryParseDouble(values[9], out rotation) ||
                    !TryParseDouble(values[10], out scaleX) ||
                    !TryParseDouble(values[11], out scaleY) ||
                    !TryParseDouble(values[12], out scaleZ))
                {
                    return false;
                }
                return Math.Abs(reference.Position.X - x) <= 0.01 &&
                    Math.Abs(reference.Position.Y - y) <= 0.01 &&
                    Math.Abs(reference.Rotation - rotation) <= 1e-8 &&
                    Math.Abs(reference.ScaleFactors.X - scaleX) <= 1e-8 &&
                    Math.Abs(reference.ScaleFactors.Y - scaleY) <= 1e-8 &&
                    Math.Abs(reference.ScaleFactors.Z - scaleZ) <= 1e-8;
            }
        }

        private static bool TryParseDouble(TypedValue value, out double result)
        {
            string text = value.Value as string;
            return Double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out result);
        }

        private static string GetXDataString(TypedValue[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length)
                return String.Empty;
            return values[index].Value as string ?? String.Empty;
        }

        private static int GetXDataInteger(TypedValue[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length ||
                values[index].Value == null)
                return 0;
            try { return Convert.ToInt32(values[index].Value); }
            catch { return 0; }
        }

        private static List<double> BuildBallastOffsets(BallastArrayPlan plan)
        {
            List<double> offsets = new List<double>();
            if (plan == null || plan.Array == null || plan.Frames == null ||
                plan.Frames.Count == 0 || plan.Frames[0].Ballasts == null)
                return offsets;
            BallastFramePlan frame = plan.Frames[0];
            for (int index = 0; index < frame.Ballasts.Count; index++)
            {
                offsets.Add(BallastVec2.Dot(
                    frame.Ballasts[index].Center - frame.Center,
                    plan.Array.LongAxis));
            }
            return offsets;
        }

        private static string BuildConflictReason(BallastFramePlan frame)
        {
            if (frame == null || frame.Ballasts == null)
                return String.Empty;
            List<string> reasons = new List<string>();
            for (int index = 0; index < frame.Ballasts.Count; index++)
            {
                string reason = frame.Ballasts[index].ConflictReason;
                if (!String.IsNullOrWhiteSpace(reason) && !reasons.Contains(reason))
                    reasons.Add(reason);
            }
            return String.Join("；", reasons.ToArray());
        }

        internal sealed class BallastDrawSummary
        {
            public int FrameCount;
            public int BallastCount;
            public int ConflictFrameCount;
            public List<ObjectId> FrameIds;
            public List<BallastStatisticItem> Items;
        }

        private static Point3d ToPoint3d(BallastVec2 point,
            IList<BallastPanel> panels)
        {
            return new Point3d(point.X, point.Y, 0.0);
        }
    }
}
