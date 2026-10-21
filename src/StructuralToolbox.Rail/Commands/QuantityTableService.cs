using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace StructuralToolbox.Commands
{
    internal static class QuantityTableService
    {
        internal static ObjectId AppendRailTable(Database database,
            Transaction transaction, Point3d position, double meters)
        {
            Table table = CreateTable(database, position, 3, 2);
            table.Cells[0, 0].TextString = "导轨统计";
            table.MergeCells(CellRange.Create(table, 0, 0, 0, 1));
            table.Cells[1, 0].TextString = "项目";
            table.Cells[1, 1].TextString = "总长度（m）";
            table.Cells[2, 0].TextString = "导轨";
            table.Cells[2, 1].TextString = meters.ToString("0.00");
            return Append(database, transaction, table);
        }

        private static Table CreateTable(Database database, Point3d position,
            int rows, int columns)
        {
            Table table = new Table();
            table.TableStyle = database.Tablestyle;
            table.SetSize(rows, columns);
            table.Position = position;
            table.SetRowHeight(250.0);
            table.SetColumnWidth(1200.0);
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    table.Cells[row, column].TextHeight = 120.0;
            table.GenerateLayout();
            return table;
        }

        private static ObjectId Append(Database database, Transaction transaction,
            Table table)
        {
            BlockTable blockTable = (BlockTable)transaction.GetObject(
                database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            ObjectId id = modelSpace.AppendEntity(table);
            transaction.AddNewlyCreatedDBObject(table, true);
            return id;
        }
    }
}
