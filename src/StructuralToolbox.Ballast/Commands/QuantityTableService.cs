using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructuralToolbox.Ballast.Core;
using StructuralToolbox.ArrayRecognition.Core;

namespace StructuralToolbox.Ballast.Commands
{
    internal static class QuantityTableService
    {
        internal static ObjectId AppendArrayTable(Database database,
            Transaction transaction, Point3d position,
            ArrayRecognitionSummary summary)
        {
            int count = summary == null ? 0 : summary.Entries.Count;
            Table table = CreateTable(database, position, count + 4, 5);
            table.Cells[0, 0].TextString = "组件阵列统计表";
            table.MergeCells(CellRange.Create(table, 0, 0, 0, 4));
            string[] headers = { "阵列规格", "单阵列组件数",
                "相同阵列数量", "组件总数", "识别状态" };
            for (int column = 0; column < headers.Length; column++)
                table.Cells[1, column].TextString = headers[column];
            for (int index = 0; index < count; index++)
            {
                ArraySummaryEntry entry = summary.Entries[index];
                int row = index + 2;
                table.Cells[row, 0].TextString = entry.Specification;
                table.Cells[row, 1].TextString = entry.ComponentsPerArray.ToString();
                table.Cells[row, 2].TextString = entry.ArrayCount.ToString();
                table.Cells[row, 3].TextString = entry.TotalComponentCount.ToString();
                table.Cells[row, 4].TextString = "正确识别";
            }
            int valid = count + 2;
            table.Cells[valid, 0].TextString = "正确识别合计";
            table.MergeCells(CellRange.Create(table, valid, 0, valid, 2));
            table.Cells[valid, 3].TextString = summary.RecognizedComponentCount.ToString();
            table.Cells[valid, 4].TextString = summary.RecognizedArrayCount + " 个阵列";
            int invalid = count + 3;
            table.Cells[invalid, 0].TextString = "未正确识别合计";
            table.MergeCells(CellRange.Create(table, invalid, 0, invalid, 2));
            table.Cells[invalid, 3].TextString = summary.InvalidComponentCount.ToString();
            table.Cells[invalid, 4].TextString = summary.InvalidArrayCount + " 组";
            return Append(database, transaction, table);
        }

        internal static ObjectId AppendBallastTable(Database database,
            Transaction transaction, Point3d position,
            BallastStatisticSummary summary)
        {
            int entryCount = summary == null || summary.Entries == null
                ? 0 : summary.Entries.Count;
            Table table = CreateTable(database, position, entryCount + 3, 5);
            table.Cells[0, 0].TextString = "配重块统计";
            table.MergeCells(CellRange.Create(table, 0, 0, 0, 4));
            string[] headers = { "序号", "形状", "规格", "颜色", "数量" };
            for (int column = 0; column < headers.Length; column++)
                table.Cells[1, column].TextString = headers[column];
            for (int index = 0; index < entryCount; index++)
            {
                BallastStatisticEntry entry = summary.Entries[index];
                int row = index + 2;
                table.Cells[row, 0].TextString = (index + 1).ToString();
                table.Cells[row, 1].TextString = entry.Shape == BallastShape.Circle
                    ? "圆墩" : "方墩";
                table.Cells[row, 2].TextString = entry.Specification;
                table.Cells[row, 3].TextString = entry.ColorLabel;
                table.Cells[row, 4].TextString = entry.Quantity.ToString();
            }
            int totalRow = entryCount + 2;
            table.Cells[totalRow, 0].TextString = "合计";
            table.MergeCells(CellRange.Create(table, totalRow, 0,
                totalRow, 3));
            table.Cells[totalRow, 4].TextString =
                (summary == null ? 0 : summary.TotalQuantity).ToString();
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
