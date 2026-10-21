using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructuralToolbox.Layout.Core;

namespace StructuralToolbox.Layout.Commands
{
    internal static class LayoutDrawingService
    {
        internal const string RegAppName = "STPV_LAYOUT";
        internal const string Marker = "COMPONENT_LAYOUT";
        public static ObjectId EnsureLayer(Database database, Transaction transaction, string layerName)
        {
            LayerTable table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (table.Has(layerName))
            {
                LayerTableRecord existing = (LayerTableRecord)transaction.GetObject(table[layerName], OpenMode.ForRead);
                if (existing.IsLocked || existing.IsFrozen || existing.IsOff) throw new InvalidOperationException("组件输出图层【" + layerName + "】已锁定、冻结或关闭。");
                return existing.ObjectId;
            }
            table.UpgradeOpen(); LayerTableRecord record = new LayerTableRecord(); record.Name = layerName; record.Color = Color.FromColorIndex(ColorMethod.ByAci, 7); table.Add(record); transaction.AddNewlyCreatedDBObject(record, true); return record.ObjectId;
        }
        public static void EnsureRegApp(Database database, Transaction transaction)
        {
            RegAppTable table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
            if (table.Has(RegAppName)) return;
            table.UpgradeOpen(); RegAppTableRecord record = new RegAppTableRecord(); record.Name = RegAppName; table.Add(record); transaction.AddNewlyCreatedDBObject(record, true);
        }
        public static int AppendRectangles(Database database, Transaction transaction, ObjectId layerId, LayoutResult result, double elevation, LayoutSettingsSnapshot snapshot, string runId)
        {
            EnsureRegApp(database, transaction);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
            int count = 0;
            for (int i = 0; i < result.Rectangles.Count; i++)
            {
                LayoutRectangle item = result.Rectangles[i]; Polyline polyline = new Polyline(4); IList<LayoutPoint> corners = item.Corners;
                for (int c = 0; c < corners.Count; c++) polyline.AddVertexAt(c, new Point2d(corners[c].X, corners[c].Y), 0.0, 0.0, 0.0);
                polyline.Closed = true; polyline.Elevation = elevation; polyline.SetDatabaseDefaults(database); polyline.LayerId = layerId; polyline.ColorIndex = item.Conflict ? 1 : 256;
                using (ResultBuffer buffer = CreateXData(item, snapshot, runId)) polyline.XData = buffer;
                modelSpace.AppendEntity(polyline); transaction.AddNewlyCreatedDBObject(polyline, true); count++;
            }
            return count;
        }
        private static ResultBuffer CreateXData(LayoutRectangle item, LayoutSettingsSnapshot snapshot, string runId)
        {
            List<TypedValue> values = new List<TypedValue>(); values.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName));
            Add(values, Marker); Add(values, "0.1"); Add(values, runId); Add(values, item.ArrayId); Add(values, item.Conflict ? "1" : "0");
            Add(values, snapshot.Length.ToString("R", CultureInfo.InvariantCulture)); Add(values, snapshot.Width.ToString("R", CultureInfo.InvariantCulture)); Add(values, snapshot.PowerWp.ToString("R", CultureInfo.InvariantCulture));
            Add(values, snapshot.SlopeDegrees.ToString("R", CultureInfo.InvariantCulture)); Add(values, snapshot.Orientation); Add(values, snapshot.Rows.ToString(CultureInfo.InvariantCulture)); Add(values, snapshot.StringSize.ToString(CultureInfo.InvariantCulture));
            return new ResultBuffer(values.ToArray());
        }
        private static void Add(ICollection<TypedValue> values, string value) { values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, value ?? String.Empty)); }
    }
    internal sealed class LayoutSettingsSnapshot
    {
        public double Length; public double Width; public double PowerWp; public double SlopeDegrees; public string Orientation; public int Rows; public int StringSize;
    }
}
