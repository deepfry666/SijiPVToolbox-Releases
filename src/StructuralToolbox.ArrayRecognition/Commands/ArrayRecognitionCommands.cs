using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.ArrayRecognition.Core;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.ArrayRecognition.Commands
{
    public sealed class ArrayRecognitionCommands
    {
        private sealed class Session
        {
            public string DocumentIdentity;
            public ArrayRecognitionResult Result;
            public string Fingerprint;
            public List<ObjectId> ComponentIds;
            public List<SelectionWindow> Windows;
        }

        private sealed class SelectionWindow
        {
            public Point3d First;
            public Point3d Second;
        }

        private sealed class WindowSelection
        {
            public List<ObjectId> ObjectIds;
            public List<SelectionWindow> Windows;
        }

        private static Session _session;

        [CommandMethod("STPVARRAYRECOGNIZE", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void Recognize()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            PromptDoubleOptions gapPrompt = new PromptDoubleOptions(
                "\n输入阵列外轮廓断开净距 G (mm) <20>: ");
            gapPrompt.AllowNone = true;
            gapPrompt.AllowNegative = false;
            gapPrompt.DefaultValue = 20.0;
            PromptDoubleResult gapResult = editor.GetDouble(gapPrompt);
            if (gapResult.Status != PromptStatus.OK &&
                gapResult.Status != PromptStatus.None)
                return;
            double gap = gapResult.Status == PromptStatus.OK
                ? gapResult.Value : 20.0;

            WindowSelection selection = SelectMultipleWindows(editor);
            if (selection == null)
                return;
            Dictionary<string, int> rejected;
            List<ArrayComponent> components = ReadComponents(
                document.Database, selection.ObjectIds, out rejected);
            HashSet<string> acceptedHandles = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < components.Count; index++)
                acceptedHandles.Add(components[index].SourceId);
            List<ObjectId> componentIds = new List<ObjectId>();
            for (int index = 0; index < selection.ObjectIds.Count; index++)
                if (acceptedHandles.Contains(
                    selection.ObjectIds[index].Handle.ToString()))
                    componentIds.Add(selection.ObjectIds[index]);
            ArrayRecognitionResult result = ArrayRecognitionEngine.Detect(components,
                new ArrayRecognitionOptions { MaximumOutlineGap = gap });
            _session = new Session
            {
                DocumentIdentity = DocumentIdentity(document),
                Result = result,
                Fingerprint = ArrayRecognitionFingerprint.Create(components),
                ComponentIds = componentIds,
                Windows = selection.Windows
            };
            ArrayRecognitionSummary summary =
                ArrayRecognitionStatistics.Summarize(result);
            editor.WriteMessage("\n组件阵列识别完成：正确识别 {0} 个阵列、{1} 块组件；" +
                "未正确识别 {2} 组、{3} 块组件。",
                summary.RecognizedArrayCount, summary.RecognizedComponentCount,
                summary.InvalidArrayCount, summary.InvalidComponentCount);
            WriteRejected(editor, rejected);
        }

        [CommandMethod("STPVARRAYTABLE", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void InsertTable()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null || _session == null ||
                !String.Equals(_session.DocumentIdentity, DocumentIdentity(document),
                    StringComparison.OrdinalIgnoreCase))
            {
                if (document != null)
                    document.Editor.WriteMessage("\n请先执行 STPVARRAYRECOGNIZE。");
                return;
            }
            if (!ValidateSession(document))
            {
                document.Editor.WriteMessage(
                    "\n组件对象已变化，识别结果已过期，请重新识别。");
                return;
            }
            PromptPointResult point = document.Editor.GetPoint(
                "\n指定组件阵列统计表插入点: ");
            if (point.Status != PromptStatus.OK)
                return;
            using (Transaction transaction =
                document.Database.TransactionManager.StartTransaction())
            {
                AppendTable(document.Database, transaction, point.Value,
                    ArrayRecognitionStatistics.Summarize(_session.Result));
                transaction.Commit();
            }
            document.Editor.Regen();
        }

        public static ObjectId AppendTable(Database database,
            Transaction transaction, Point3d position,
            ArrayRecognitionSummary summary)
        {
            int entries = summary == null ? 0 : summary.Entries.Count;
            Table table = new Table();
            table.TableStyle = database.Tablestyle;
            table.Position = position;
            table.SetSize(entries + 4, 5);
            table.SetRowHeight(250);
            table.SetColumnWidth(1400);
            table.Cells[0, 0].TextString = "组件阵列统计表";
            table.MergeCells(CellRange.Create(table, 0, 0, 0, 4));
            string[] headers = { "阵列规格", "单阵列组件数",
                "相同阵列数量", "组件总数", "识别状态" };
            for (int column = 0; column < headers.Length; column++)
                table.Cells[1, column].TextString = headers[column];
            for (int index = 0; index < entries; index++)
            {
                ArraySummaryEntry entry = summary.Entries[index];
                int row = index + 2;
                table.Cells[row, 0].TextString = entry.Specification;
                table.Cells[row, 1].TextString = entry.ComponentsPerArray.ToString();
                table.Cells[row, 2].TextString = entry.ArrayCount.ToString();
                table.Cells[row, 3].TextString = entry.TotalComponentCount.ToString();
                table.Cells[row, 4].TextString = "正确识别";
            }
            int validRow = entries + 2;
            table.Cells[validRow, 0].TextString = "正确识别合计";
            table.MergeCells(CellRange.Create(table, validRow, 0, validRow, 2));
            table.Cells[validRow, 3].TextString = summary.RecognizedComponentCount.ToString();
            table.Cells[validRow, 4].TextString = summary.RecognizedArrayCount + " 个阵列";
            int invalidRow = entries + 3;
            table.Cells[invalidRow, 0].TextString = "未正确识别合计";
            table.MergeCells(CellRange.Create(table, invalidRow, 0, invalidRow, 2));
            table.Cells[invalidRow, 3].TextString = summary.InvalidComponentCount.ToString();
            table.Cells[invalidRow, 4].TextString = summary.InvalidArrayCount + " 组";
            for (int row = 0; row < entries + 4; row++)
                for (int column = 0; column < 5; column++)
                    table.Cells[row, column].TextHeight = 120;
            table.GenerateLayout();
            BlockTable blocks = (BlockTable)transaction.GetObject(
                database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord model = (BlockTableRecord)transaction.GetObject(
                blocks[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            ObjectId id = model.AppendEntity(table);
            transaction.AddNewlyCreatedDBObject(table, true);
            return id;
        }

        private static WindowSelection SelectMultipleWindows(Editor editor)
        {
            Dictionary<ObjectId, bool> ids = new Dictionary<ObjectId, bool>();
            List<SelectionWindow> windows = new List<SelectionWindow>();
            while (true)
            {
                PromptPointResult first = editor.GetPoint(
                    "\n指定阵列识别窗口第一个角点，或按 Esc 结束: ");
                if (first.Status != PromptStatus.OK)
                    return ids.Count == 0 ? null : new WindowSelection
                    {
                        ObjectIds = new List<ObjectId>(ids.Keys),
                        Windows = windows
                    };
                PromptCornerOptions corner = new PromptCornerOptions(
                    "\n指定另一个角点: ", first.Value);
                PromptPointResult second = editor.GetCorner(corner);
                if (second.Status != PromptStatus.OK)
                    return null;
                PromptSelectionResult selection = editor.SelectCrossingWindow(
                    first.Value, second.Value);
                windows.Add(new SelectionWindow
                {
                    First = first.Value,
                    Second = second.Value
                });
                if (selection.Status == PromptStatus.OK)
                    foreach (ObjectId id in selection.Value.GetObjectIds())
                        ids[id] = true;
                PromptKeywordOptions more = new PromptKeywordOptions(
                    "\n继续添加识别窗口？[继续(C)/完成(F)/取消(X)] <完成>: ");
                more.AllowNone = true;
                more.Keywords.Add("C"); more.Keywords.Add("F");
                more.Keywords.Add("X"); more.Keywords.Default = "F";
                PromptResult answer = editor.GetKeywords(more);
                if (answer.Status == PromptStatus.Cancel ||
                    String.Equals(answer.StringResult, "X",
                        StringComparison.OrdinalIgnoreCase))
                    return null;
                if (answer.Status == PromptStatus.None ||
                    String.Equals(answer.StringResult, "F",
                        StringComparison.OrdinalIgnoreCase))
                    break;
            }
            return new WindowSelection
            {
                ObjectIds = new List<ObjectId>(ids.Keys),
                Windows = windows
            };
        }

        public static List<ArrayComponent> ReadComponents(Database database,
            IEnumerable<ObjectId> ids, out Dictionary<string, int> rejected)
        {
            rejected = new Dictionary<string, int>(StringComparer.Ordinal);
            List<ArrayComponent> result = new List<ArrayComponent>();
            using (Transaction transaction =
                database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    Entity entity = transaction.GetObject(id, OpenMode.ForRead, false)
                        as Entity;
                    ArrayComponent component;
                    string reason;
                    if (ArrayComponentReader.TryRead(entity, transaction,
                        out component, out reason))
                        result.Add(component);
                    else
                    {
                        int count;
                        if (!rejected.TryGetValue(reason, out count))
                            count = 0;
                        rejected[reason] = count + 1;
                    }
                }
                transaction.Commit();
            }
            return result;
        }

        private static void WriteRejected(Editor editor,
            Dictionary<string, int> rejected)
        {
            foreach (KeyValuePair<string, int> entry in rejected)
                editor.WriteMessage("\n未识别：{0}，{1} 个。",
                    entry.Key, entry.Value);
        }

        private static string DocumentIdentity(Document document)
        {
            return document == null ? String.Empty :
                document.Database.Filename + "|" + document.GetHashCode();
        }

        private static bool ValidateSession(Document document)
        {
            try
            {
                Dictionary<ObjectId, bool> ids = new Dictionary<ObjectId, bool>();
                for (int index = 0; index < _session.Windows.Count; index++)
                {
                    SelectionWindow window = _session.Windows[index];
                    PromptSelectionResult selection = document.Editor.SelectCrossingWindow(
                        window.First, window.Second);
                    if (selection.Status != PromptStatus.OK)
                        continue;
                    foreach (ObjectId id in selection.Value.GetObjectIds())
                        ids[id] = true;
                }
                Dictionary<string, int> rejected;
                List<ArrayComponent> current = ReadComponents(document.Database,
                    ids.Keys, out rejected);
                return String.Equals(
                    ArrayRecognitionFingerprint.Create(current),
                    _session.Fingerprint, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }
    }
}
