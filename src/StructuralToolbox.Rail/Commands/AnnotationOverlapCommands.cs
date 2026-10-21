using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Core;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.Commands
{
    public sealed class AnnotationOverlapCommands
    {
        [CommandMethod("STDIMOVERLAP",
            CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        public void ProcessOverlappingAnnotations()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;

            Editor editor = document.Editor;
            Database database = document.Database;
            PromptPointResult firstCorner = editor.GetPoint(
                "\n框选要检查的标注/文字范围，第一角点: ");
            if (firstCorner.Status != PromptStatus.OK)
                return;
            PromptCornerOptions secondCornerOptions =
                new PromptCornerOptions(
                    "\n第二角点（框选范围内的标注和文字）: ",
                    firstCorner.Value);
            PromptPointResult secondCorner =
                editor.GetCorner(secondCornerOptions);
            if (secondCorner.Status != PromptStatus.OK)
                return;

            PromptSelectionResult selection = editor.SelectCrossingWindow(
                firstCorner.Value, secondCorner.Value);
            if (selection.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n未框选到任何对象，已退出。");
                return;
            }

            List<AnnotationOverlapItem> items =
                new List<AnnotationOverlapItem>();
            List<ObjectId> itemIds = new List<ObjectId>();
            using (Transaction transaction =
                database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId objectId in selection.Value.GetObjectIds())
                {
                    AnnotationOverlapItem item =
                        AnnotationOverlapCleanupService.CreateItem(
                            transaction, objectId);
                    if (item == null)
                        continue;
                    items.Add(item);
                    itemIds.Add(objectId);
                }
                transaction.Commit();
            }

            if (items.Count == 0)
            {
                editor.WriteMessage(
                    "\n框选范围内没有可检查的标注（DIMENSION）或文字。");
                return;
            }

            double margin = ComputeMargin(items);
            List<AnnotationOverlapPair> pairs =
                AnnotationOverlapSolver.FindOverlappingPairs(items, margin);
            if (pairs.Count == 0)
            {
                editor.WriteMessage(
                    "\n共检查 {0} 个标注/文字，未发现重叠。", items.Count);
                return;
            }

            editor.WriteMessage(
                "\n共检查 {0} 个标注/文字，发现 {1} 处重叠。",
                items.Count, pairs.Count);
            int autoCleaned = 0;
            int manualMoved = 0;
            int unresolved = 0;

            for (int pairIndex = 0; pairIndex < pairs.Count; pairIndex++)
            {
                AnnotationOverlapPair pair = pairs[pairIndex];
                int firstIndex = pair.FirstIndex;
                int secondIndex = pair.SecondIndex;
                if (!TryHighlightPair(editor, itemIds[firstIndex],
                    itemIds[secondIndex]))
                {
                    // 高亮失败不阻断，只在命令行继续。
                }

                string prompt = String.Format(
                    "标注重叠处理 {0}/{1}\n{2}\n{3}\n\n" +
                    "自动清理会把后面的标注/文字移到空位；手动移动由你指定新位置。",
                    pairIndex + 1, pairs.Count,
                    DescribeItem(items[firstIndex]),
                    DescribeItem(items[secondIndex]));
                using (OverlapCleanupDialog dialog =
                    new OverlapCleanupDialog(prompt))
                {
                    DialogResult dialogResult = dialog.ShowDialog();
                    if (dialogResult == DialogResult.Cancel ||
                        dialog.SelectedAction == OverlapAction.Exit)
                    {
                        editor.WriteMessage(
                            "\n用户退出。已清理 {0} 处，未处理 {1} 处。",
                            autoCleaned + manualMoved,
                            pairs.Count - pairIndex);
                        break;
                    }

                    if (dialog.SelectedAction == OverlapAction.AutomaticClean)
                    {
                        double deltaX;
                        double deltaY;
                        double placementMargin = ComputePlacementMargin(
                            items);
                        if (!AnnotationOverlapSolver.TryFindMoveOffset(
                            items, secondIndex, placementMargin,
                            out deltaX, out deltaY))
                        {
                            AcApplication.ShowAlertDialog(
                                "没有找到足以避开其它标注/文字的空位，" +
                                "请使用手动移动。");
                            unresolved++;
                            continue;
                        }
                        if (AnnotationOverlapCleanupService.TryMoveItem(
                            database, itemIds[secondIndex], deltaX, deltaY))
                        {
                            autoCleaned++;
                        }
                        else
                        {
                            AcApplication.ShowAlertDialog(
                                "移动标注/文字失败，请手动处理。");
                            unresolved++;
                        }
                        continue;
                    }

                    if (dialog.SelectedAction == OverlapAction.ManualMove)
                    {
                        PromptPointResult target = editor.GetPoint(
                            "\n指定标注/文字文字的新位置: ");
                        if (target.Status != PromptStatus.OK)
                        {
                            unresolved++;
                            continue;
                        }
                        if (AnnotationOverlapCleanupService.TryMoveItemToPoint(
                            database, itemIds[secondIndex], target.Value))
                        {
                            manualMoved++;
                        }
                        else
                        {
                            unresolved++;
                        }
                        continue;
                    }

                    if (dialog.SelectedAction == OverlapAction.Skip)
                    {
                        unresolved++;
                    }
                }
            }

            TryClearHighlight(editor);
            editor.WriteMessage(
                "\n标注重叠处理结束：自动清理 {0}，手动移动 {1}，未处理 {2}。",
                autoCleaned, manualMoved, unresolved);
        }

        private static double ComputeMargin(
            IList<AnnotationOverlapItem> items)
        {
            double maxTextHeight = 0.0;
            for (int index = 0; index < items.Count; index++)
            {
                double height = items[index] == null ? 0.0 :
                    items[index].TextHeight;
                if (height > maxTextHeight)
                    maxTextHeight = height;
            }
            return Math.Max(0.0, maxTextHeight * 0.10);
        }

        private static double ComputePlacementMargin(
            IList<AnnotationOverlapItem> items)
        {
            return Math.Max(0.15, ComputeMargin(items));
        }

        private static string DescribeItem(AnnotationOverlapItem item)
        {
            if (item == null)
                return "未知对象";
            string kind = item.Kind == AnnotationOverlapKind.Dimension
                ? "标注"
                : "文字";
            return String.Format("{0}（{1}）图层：{2}",
                item.DisplayName, kind, item.Layer ?? "-");
        }

        private static bool TryHighlightPair(Editor editor,
            ObjectId firstId, ObjectId secondId)
        {
            try
            {
                editor.SetImpliedSelection(
                    new ObjectId[] { firstId, secondId });
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static void TryClearHighlight(Editor editor)
        {
            try
            {
                editor.SetImpliedSelection(new ObjectId[0]);
            }
            catch (System.Exception)
            {
            }
        }
    }

    internal static class AnnotationOverlapCleanupService
    {
        internal static AnnotationOverlapItem CreateItem(
            Transaction transaction, ObjectId objectId)
        {
            Entity entity = transaction.GetObject(objectId,
                OpenMode.ForRead, false) as Entity;
            if (entity == null || entity.IsErased)
                return null;

            Dimension dimension = entity as Dimension;
            if (dimension != null)
                return CreateDimensionItem(dimension);

            DBText dbText = entity as DBText;
            if (dbText != null)
                return CreateTextItem(dbText, dbText.Height);

            MText mText = entity as MText;
            if (mText != null)
                return CreateTextItem(mText, mText.TextHeight);

            return null;
        }

        internal static bool TryMoveItem(Database database,
            ObjectId objectId, double deltaX, double deltaY)
        {
            using (Transaction transaction =
                database.TransactionManager.StartTransaction())
            {
                Entity entity = transaction.GetObject(objectId,
                    OpenMode.ForWrite, false) as Entity;
                if (entity == null)
                {
                    transaction.Abort();
                    return false;
                }
                ApplyMove(entity, deltaX, deltaY);
                transaction.Commit();
            }
            return true;
        }

        internal static bool TryMoveItemToPoint(Database database,
            ObjectId objectId, Point3d target)
        {
            try
            {
                using (Transaction transaction =
                    database.TransactionManager.StartTransaction())
                {
                    Entity entity = transaction.GetObject(objectId,
                        OpenMode.ForWrite, false) as Entity;
                    if (entity == null)
                    {
                        transaction.Abort();
                        return false;
                    }
                    Point3d current = GetCurrentPosition(entity);
                    ApplyMove(entity, target.X - current.X,
                        target.Y - current.Y);
                    transaction.Commit();
                }
                return true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return false;
            }
        }

        private static AnnotationOverlapItem CreateDimensionItem(
            Dimension dimension)
        {
            AnnotationOverlapItem item = new AnnotationOverlapItem();
            item.Kind = AnnotationOverlapKind.Dimension;
            item.DisplayName = dimension.Handle == null
                ? "标注"
                : "标注 H" + dimension.Handle.ToString();
            item.Layer = dimension.Layer;
            item.TextHeight = ResolveDimensionTextHeight(dimension);
            item.TextBounds.AddRange(GetDimensionTextBounds(dimension));
            item.FullBounds.AddRange(GetEntityBounds(dimension));
            return item;
        }

        private static AnnotationOverlapItem CreateTextItem(Entity text,
            double textHeight)
        {
            AnnotationOverlapItem item = new AnnotationOverlapItem();
            item.Kind = AnnotationOverlapKind.Text;
            item.DisplayName = text.Handle == null
                ? "文字"
                : "文字 H" + text.Handle.ToString();
            item.Layer = text.Layer;
            item.TextHeight = textHeight > 0.0 ? textHeight : 2.5;
            item.TextBounds.AddRange(GetEntityBounds(text));
            item.FullBounds.AddRange(GetEntityBounds(text));
            return item;
        }

        private static double ResolveDimensionTextHeight(Dimension dimension)
        {
            try
            {
                double height = dimension.Dimtxt *
                    Math.Max(1.0, dimension.Dimscale);
                return height > 0.0 ? height : 2.5;
            }
            catch (System.Exception)
            {
                return 2.5;
            }
        }

        private static List<OverlapRect> GetDimensionTextBounds(
            Dimension dimension)
        {
            List<OverlapRect> result = new List<OverlapRect>();
            DBObjectCollection exploded = new DBObjectCollection();
            try
            {
                dimension.Explode(exploded);
                foreach (DBObject child in exploded)
                {
                    CollectDimensionTextBounds(child as Entity, result, 0);
                }
            }
            catch (System.Exception)
            {
            }
            finally
            {
                foreach (DBObject child in exploded)
                    child.Dispose();
                exploded.Dispose();
            }

            if (result.Count == 0)
            {
                Point3d position = dimension.TextPosition;
                double height = Math.Max(2.5,
                    ResolveDimensionTextHeight(dimension));
                result.Add(new OverlapRect(
                    position.X - (height * 2.5),
                    position.Y - (height * 0.55),
                    position.X + (height * 2.5),
                    position.Y + (height * 0.55)));
            }
            return result;
        }

        private static void CollectDimensionTextBounds(Entity entity,
            ICollection<OverlapRect> output, int depth)
        {
            if (entity == null || depth > 4)
                return;
            if (entity is DBText || entity is MText)
            {
                foreach (OverlapRect rect in GetEntityBounds(entity))
                    output.Add(rect);
                return;
            }

            BlockReference block = entity as BlockReference;
            if (block == null)
                return;

            DBObjectCollection children = new DBObjectCollection();
            try
            {
                block.Explode(children);
                foreach (DBObject child in children)
                {
                    CollectDimensionTextBounds(child as Entity, output,
                        depth + 1);
                }
            }
            catch (System.Exception)
            {
            }
            finally
            {
                foreach (DBObject child in children)
                    child.Dispose();
                children.Dispose();
            }
        }

        private static List<OverlapRect> GetEntityBounds(Entity entity)
        {
            List<OverlapRect> result = new List<OverlapRect>();
            OverlapRect bounds;
            if (TryGetEntityBounds(entity, out bounds))
                result.Add(bounds);
            return result;
        }

        private static bool TryGetEntityBounds(Entity entity,
            out OverlapRect bounds)
        {
            bounds = new OverlapRect(0.0, 0.0, 0.0, 0.0);
            if (entity == null)
                return false;
            try
            {
                Extents3d extents = entity.GeometricExtents;
                bounds = new OverlapRect(
                    extents.MinPoint.X, extents.MinPoint.Y,
                    extents.MaxPoint.X, extents.MaxPoint.Y);
                return true;
            }
            catch (System.Exception)
            {
                try
                {
                    Extents3d? drawableBounds = entity.Bounds;
                    if (!drawableBounds.HasValue)
                        return false;
                    Extents3d extents = drawableBounds.Value;
                    bounds = new OverlapRect(
                        extents.MinPoint.X, extents.MinPoint.Y,
                        extents.MaxPoint.X, extents.MaxPoint.Y);
                    return true;
                }
                catch (System.Exception)
                {
                    return false;
                }
            }
        }

        private static void ApplyMove(Entity entity,
            double deltaX, double deltaY)
        {
            Dimension dimension = entity as Dimension;
            if (dimension != null)
            {
                Point3d position = dimension.TextPosition;
                dimension.TextPosition = new Point3d(
                    position.X + deltaX, position.Y + deltaY, position.Z);
                dimension.UsingDefaultTextPosition = false;
                dimension.Dimtmove = 0;
                dimension.RecomputeDimensionBlock(true);
                return;
            }

            DBText dbText = entity as DBText;
            if (dbText != null)
            {
                Point3d position = dbText.Position;
                dbText.Position = new Point3d(
                    position.X + deltaX, position.Y + deltaY, position.Z);
                return;
            }

            MText mText = entity as MText;
            if (mText != null)
            {
                Point3d position = mText.Location;
                mText.Location = new Point3d(
                    position.X + deltaX, position.Y + deltaY, position.Z);
            }
        }

        private static Point3d GetCurrentPosition(Entity entity)
        {
            Dimension dimension = entity as Dimension;
            if (dimension != null)
                return dimension.TextPosition;

            DBText dbText = entity as DBText;
            if (dbText != null)
                return dbText.Position;

            MText mText = entity as MText;
            if (mText != null)
                return mText.Location;

            return new Point3d(0.0, 0.0, 0.0);
        }
    }

    internal enum OverlapAction
    {
        AutomaticClean,
        ManualMove,
        Skip,
        Exit
    }

    internal sealed class OverlapCleanupDialog : Form
    {
        internal OverlapAction SelectedAction { get; private set; }

        internal OverlapCleanupDialog(string message)
        {
            Text = "标注重叠处理";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 260);
            Font = new System.Drawing.Font(Font.FontFamily, 9.0f,
                System.Drawing.FontStyle.Regular);
            BackColor = Color.FromArgb(245, 246, 248);

            Label info = new Label();
            info.Text = message;
            info.SetBounds(18, 14, 424, 100);
            info.AutoEllipsis = true;
            info.ForeColor = Color.FromArgb(32, 35, 40);
            Controls.Add(info);

            Button automatic = CreateButton("自动清理", 18, 128);
            automatic.Click += (sender, args) =>
            {
                SelectedAction = OverlapAction.AutomaticClean;
                DialogResult = DialogResult.OK;
            };
            Controls.Add(automatic);

            Button manual = CreateButton("手动移动", 126, 128);
            manual.Click += (sender, args) =>
            {
                SelectedAction = OverlapAction.ManualMove;
                DialogResult = DialogResult.OK;
            };
            Controls.Add(manual);

            Button skip = CreateButton("跳过", 234, 128);
            skip.Click += (sender, args) =>
            {
                SelectedAction = OverlapAction.Skip;
                DialogResult = DialogResult.OK;
            };
            Controls.Add(skip);

            Button exit = CreateButton("退出", 342, 128);
            exit.Click += (sender, args) =>
            {
                SelectedAction = OverlapAction.Exit;
                DialogResult = DialogResult.Cancel;
            };
            Controls.Add(exit);

            Label hint = new Label();
            hint.Text = "自动清理：把后面的标注/文字移到空位；手动移动：点一个新位置。";
            hint.SetBounds(18, 182, 424, 52);
            hint.AutoEllipsis = true;
            hint.ForeColor = Color.FromArgb(105, 108, 114);
            Controls.Add(hint);
        }

        private Button CreateButton(string text, int x, int y)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(x, y, 100, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor =
                Color.FromArgb(190, 194, 201);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(32, 35, 40);
            return button;
        }
    }
}
