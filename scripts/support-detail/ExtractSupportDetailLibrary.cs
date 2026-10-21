using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.SupportDetail.LibraryExtraction
{
    public sealed class ExtractSupportDetailLibrary
    {
        private sealed class SelectionWindow
        {
            public double MinimumX;
            public double MinimumY;
            public double MaximumX;
            public double MaximumY;

            public bool Intersects(Extents3d extents)
            {
                return extents.MaxPoint.X >= MinimumX &&
                    extents.MinPoint.X <= MaximumX &&
                    extents.MaxPoint.Y >= MinimumY &&
                    extents.MinPoint.Y <= MaximumY;
            }
        }

        [CommandMethod("SD_EXTRACT_FIXED_DETAILS", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void Extract()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            const string output =
                "Q:/src/StructuralToolbox.SupportDetail/Library/" +
                "SupportDetailFixedDetails.dwg";
            SelectionWindow[] windows =
            {
                Window(-538650.0, 15000.0, -524900.0, 23850.0),
                Window(-539850.0, 4300.0, -520500.0, 12500.0),
                Window(-540500.0, -7600.0, -520000.0, 1000.0),
                Window(-517000.0, 14800.0, -505500.0, 24000.0),
                Window(-517000.0, 3000.0, -505000.0, 12800.0),
                Window(-501000.0, 5500.0, -482000.0, 24200.0)
            };
            ObjectIdCollection selected = new ObjectIdCollection();
            using (Transaction transaction = document.Database.
                TransactionManager.StartOpenCloseTransaction())
            {
                BlockTableRecord modelSpace =
                    (BlockTableRecord)transaction.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(
                            document.Database), OpenMode.ForRead);
                foreach (ObjectId objectId in modelSpace)
                {
                    Entity entity = transaction.GetObject(objectId,
                        OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                        continue;
                    Extents3d extents;
                    try
                    {
                        extents = entity.GeometricExtents;
                    }
                    catch
                    {
                        continue;
                    }
                    for (int index = 0; index < windows.Length; index++)
                    {
                        if (!windows[index].Intersects(extents))
                            continue;
                        selected.Add(objectId);
                        break;
                    }
                }
                transaction.Commit();
            }
            if (selected.Count == 0)
                throw new InvalidOperationException(
                    "The configured detail regions contain no entities.");

            using (Database target = document.Database.Wblock(selected,
                new Point3d(-540500.0, -7600.0, 0.0)))
            {
                target.SaveAs(output, DwgVersion.Current);
            }
            document.Editor.WriteMessage(
                "\nSUPPORT_DETAIL_LIBRARY=PASS ENTITIES={0} OUTPUT={1}",
                selected.Count, output);
        }

        private static SelectionWindow Window(double minimumX,
            double minimumY, double maximumX, double maximumY)
        {
            return new SelectionWindow
            {
                MinimumX = minimumX,
                MinimumY = minimumY,
                MaximumX = maximumX,
                MaximumY = maximumY
            };
        }
    }
}
