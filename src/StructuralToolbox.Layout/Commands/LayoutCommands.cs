using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Layout.Core;
using StructuralToolbox.Layout.Settings;
using StructuralToolbox.Layout.UI;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.Layout.Commands
{
    public sealed class LayoutCommands
    {
        [CommandMethod("STPVLAYOUTCHECK", CommandFlags.Modal)]
        public void CheckLayoutModule()
        {
            Document document =
                AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            LayoutInput input = new LayoutInput
            {
                Boundary = new LayoutPolygon(new[]
                {
                    new LayoutPoint(0, 0),
                    new LayoutPoint(10000, 0),
                    new LayoutPoint(10000, 5000),
                    new LayoutPoint(0, 5000)
                })
            };
            LayoutOptions options = new LayoutOptions
            {
                Orientation = LayoutOrientation.Vertical,
                AllowedRows = new[] { 1 },
                Direction = new LayoutVector(1, 0),
                ObstacleClearance = 200.0
            };
            ObstacleSelectionSummary summary =
                new ObstacleSelectionSummary();
            using (Line line = new Line(new Point3d(4000, 0, 0),
                new Point3d(4000, 5000, 0)))
                ClassifyObstacle(line, false, input, summary);
            using (Polyline path = new Polyline())
            {
                path.AddVertexAt(0, new Point2d(7000, 0), 0, 0, 0);
                path.AddVertexAt(1, new Point2d(7000, 2500), 0, 0, 0);
                path.AddVertexAt(2, new Point2d(9000, 2500), 0, 0, 0);
                ClassifyObstacle(path, false, input, summary);
            }
            if (summary.LineCount != 1 || summary.OpenPolylineCount != 1 ||
                summary.SelectedCount != 2 || summary.RecognizedCount != 2 ||
                summary.IgnoredCount != 0)
                throw new InvalidOperationException(
                    "Layout obstacle selection diagnostic failed.");
            using (DBText unsupported = new DBText())
            {
                LayoutInput ignoredInput = new LayoutInput
                {
                    Boundary = input.Boundary
                };
                ObstacleSelectionSummary ignored =
                    new ObstacleSelectionSummary();
                ClassifyObstacle(unsupported, false, ignoredInput, ignored);
                if (ignored.SelectedCount != 1 ||
                    ignored.RecognizedCount != 0 || ignored.IgnoredCount != 1)
                    throw new InvalidOperationException(
                        "Layout unsupported obstacle diagnostic failed.");

                LayoutInput mixedInput = new LayoutInput
                {
                    Boundary = input.Boundary
                };
                ObstacleSelectionSummary mixed =
                    new ObstacleSelectionSummary();
                using (Line mixedLine = new Line(new Point3d(1000, 0, 0),
                    new Point3d(1000, 5000, 0)))
                    ClassifyObstacle(mixedLine, false, mixedInput, mixed);
                ClassifyObstacle(unsupported, false, mixedInput, mixed);
                if (mixed.SelectedCount != 2 ||
                    mixed.RecognizedCount != 1 || mixed.IgnoredCount != 1 ||
                    mixed.SelectedCount != mixed.RecognizedCount +
                        mixed.IgnoredCount)
                    throw new InvalidOperationException(
                        "Layout mixed obstacle diagnostic failed.");
            }
            LayoutResult result = LayoutEngine.Solve(input, options);
            if (result.ComponentCount <= 0)
                throw new InvalidOperationException(
                    "Layout core diagnostic produced no components.");
            VerifyBoundaryReadingDiagnostic(document.Editor);
            LayoutSettings rowSettings = new LayoutSettings { Rows = 3 };
            LayoutOptions rowOptions = rowSettings.ToOptions(
                new LayoutVector(1, 0));
            if (rowOptions.AllowedRows.Length != 3 ||
                rowOptions.AllowedRows[0] != 1 ||
                rowOptions.AllowedRows[1] != 2 ||
                rowOptions.AllowedRows[2] != 3)
                throw new InvalidOperationException(
                    "Layout maximum row diagnostic failed.");
            document.Editor.WriteMessage(
                "\nSTPVLAYOUT-CHECK VERSION={0} CORE=PASS COMPONENTS={1} LINES=PASS SUMMARY=PASS BOUNDARY=PASS ROWS=PASS",
                typeof(LayoutCommands).Assembly.GetName().Version,
                result.ComponentCount);
        }

        [CommandMethod("STPVLAYOUT", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        public void CreateLayout()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument; if (document == null) return;
            Editor editor = document.Editor; Database database = document.Database;
            try
            {
                if (database.Insunits != UnitsValue.Millimeters &&
                    database.Insunits != UnitsValue.Undefined)
                {
                    AcApplication.ShowAlertDialog(
                        "组件自动排布首版只按毫米计算。\n" +
                        "当前图纸 INSUNITS 为【" + database.Insunits +
                        "】，请先确认并转换图纸单位。");
                    return;
                }
                if (database.Insunits == UnitsValue.Undefined)
                    editor.WriteMessage(
                        "\n注意：当前图纸 INSUNITS 未定义，本次按毫米处理。 ");
                ObjectId boundaryId; LayoutPolygon boundary; double elevation;
                if (!PromptBoundary(editor, database, out boundaryId, out boundary, out elevation)) return;
                LayoutVector direction; if (!PromptDirection(editor, out direction)) return;
                LayoutSettings settings = LayoutSettingsStore.Load();
                using (LayoutDialog dialog = new LayoutDialog(settings))
                {
                    if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dialog) != System.Windows.Forms.DialogResult.OK) return;
                    settings = dialog.Settings; LayoutSettingsStore.Save(settings);
                }
                LayoutOptions options = settings.ToOptions(direction);
                LayoutInput input = new LayoutInput { Boundary = boundary };
                if (!PromptObstacles(editor, database, boundaryId, input))
                    return;
                LayoutResult result = LayoutEngine.Solve(input, options);
                if (result.ComponentCount == 0) { editor.WriteMessage("\n没有找到满足边界、障碍、排数和容量规则的组件位置。 "); return; }
                editor.WriteMessage("\n首版预览：{0} 块组件，{1:0.###} kWp，朝向 {2}，冲突 {3} 块。", result.ComponentCount, result.CapacityKwp, result.OrientationName, result.ConflictCount);
                PreviewAndCommit(editor, database, result, elevation, settings);
            }
            catch (System.Exception exception) { editor.WriteMessage("\n组件自动排布失败：{0}", exception.Message); }
        }

        private static bool PromptBoundary(Editor editor, Database database, out ObjectId boundaryId, out LayoutPolygon polygon, out double elevation)
        {
            boundaryId = ObjectId.Null; polygon = null; elevation = 0.0;
            PromptEntityOptions prompt = new PromptEntityOptions("\n请选择当前屋面的闭合轻量多段线边界: "); prompt.SetRejectMessage("\n请选择闭合轻量多段线。 "); prompt.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult selected = editor.GetEntity(prompt); if (selected.Status != PromptStatus.OK) return false;
            using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                Polyline polyline = transaction.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Polyline; string reason = "对象不是轻量多段线";
                if (polyline == null || !TryReadPolygon(polyline, out polygon, out reason)) { editor.WriteMessage("\n屋面边界无效：{0}", reason); return false; }
                boundaryId = selected.ObjectId; elevation = polyline.Elevation; transaction.Commit();
            }
            return true;
        }
        private static bool PromptDirection(Editor editor, out LayoutVector direction)
        {
            direction = new LayoutVector(1, 0); PromptPointResult first = editor.GetPoint("\n指定排布方向 D 的第一点: "); if (first.Status != PromptStatus.OK) return false;
            PromptPointResult second = editor.GetPoint("\n指定排布方向 D 的第二点: "); if (second.Status != PromptStatus.OK) return false;
            try { direction = new LayoutVector(second.Value.X - first.Value.X, second.Value.Y - first.Value.Y).Normalized(); return true; }
            catch (ArgumentException) { editor.WriteMessage("\n排布方向 D 长度必须大于零。 "); return false; }
        }
        private static bool PromptObstacles(Editor editor, Database database,
            ObjectId boundaryId, LayoutInput input)
        {
            while (true)
            {
                PromptSelectionOptions options = new PromptSelectionOptions();
                options.MessageForAdding =
                    "\n选择屋面内部障碍（支持闭合多段线、圆、LINE、开放直线多段线；开放线直接按 O 退距；无障碍按 Enter）: ";
                PromptSelectionResult selected = editor.GetSelection(options);
                if (selected.Status == PromptStatus.Cancel) return false;
                if (selected.Status != PromptStatus.OK)
                {
                    editor.WriteMessage(
                        "\n障碍识别：共选择 0 个；已明确按无障碍继续。 ");
                    return true;
                }

                LayoutInput recognized = new LayoutInput
                {
                    Boundary = input.Boundary
                };
                ObstacleSelectionSummary summary =
                    new ObstacleSelectionSummary();
                using (Transaction transaction =
                    database.TransactionManager.StartOpenCloseTransaction())
                {
                    ObjectId[] ids = selected.Value.GetObjectIds();
                    for (int i = 0; i < ids.Length; i++)
                    {
                        Entity entity = transaction.GetObject(ids[i],
                            OpenMode.ForRead, false) as Entity;
                        ClassifyObstacle(entity, ids[i] == boundaryId,
                            recognized, summary);
                    }
                    transaction.Commit();
                }
                editor.WriteMessage("\n{0} ", summary.ToMessage());

                if (summary.RecognizedCount == 0)
                {
                    PromptKeywordOptions decision =
                        new PromptKeywordOptions(
                            "\n所选对象全部未识别。[重新选择(R)/按无障碍继续(N)/取消(X)] <重新选择>: ");
                    decision.AllowNone = true;
                    decision.Keywords.Add("R");
                    decision.Keywords.Add("N");
                    decision.Keywords.Add("X");
                    decision.Keywords.Default = "R";
                    PromptResult answer = editor.GetKeywords(decision);
                    if (answer.Status == PromptStatus.Cancel) return false;
                    string keyword = answer.Status == PromptStatus.None
                        ? "R" : answer.StringResult;
                    if (String.Equals(keyword, "N",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        editor.WriteMessage(
                            "\n已明确选择按无障碍继续。 ");
                        return true;
                    }
                    if (String.Equals(keyword, "X",
                        StringComparison.OrdinalIgnoreCase)) return false;
                    continue;
                }

                AddRecognizedObstacles(input, recognized);
                return true;
            }
        }

        private static void AddRecognizedObstacles(LayoutInput target,
            LayoutInput source)
        {
            for (int i = 0; i < source.Obstacles.Count; i++)
                target.Obstacles.Add(source.Obstacles[i]);
            for (int i = 0; i < source.Circles.Count; i++)
                target.Circles.Add(source.Circles[i]);
            for (int i = 0; i < source.LinearObstacles.Count; i++)
                target.LinearObstacles.Add(source.LinearObstacles[i]);
        }

        private static void ClassifyObstacle(Entity entity, bool isBoundary,
            LayoutInput input, ObstacleSelectionSummary summary)
        {
            if (isBoundary)
            {
                summary.AddIgnored("屋面边界本身不作为障碍");
                return;
            }
            if (entity == null)
            {
                summary.AddIgnored("对象无法读取");
                return;
            }

            Polyline polyline = entity as Polyline;
            if (polyline != null)
            {
                string reason;
                if (polyline.Closed)
                {
                    LayoutPolygon polygon;
                    if (TryReadPolygon(polyline, out polygon, out reason))
                    {
                        input.Obstacles.Add(polygon);
                        summary.AddPolygon();
                    }
                    else summary.AddIgnored("闭合多段线：" + reason);
                    return;
                }
                LayoutPath path;
                if (TryReadPath(polyline, out path, out reason))
                {
                    input.LinearObstacles.Add(path);
                    summary.AddOpenPolyline();
                }
                else summary.AddIgnored("开放多段线：" + reason);
                return;
            }

            Line line = entity as Line;
            if (line != null)
            {
                LayoutPoint start = new LayoutPoint(line.StartPoint.X,
                    line.StartPoint.Y);
                LayoutPoint end = new LayoutPoint(line.EndPoint.X,
                    line.EndPoint.Y);
                if ((end - start).Length <= 1e-9)
                    summary.AddIgnored("LINE 在 XY 平面长度为零");
                else
                {
                    input.LinearObstacles.Add(new LayoutPath(
                        new[] { start, end }));
                    summary.AddLine();
                }
                return;
            }

            Circle circle = entity as Circle;
            if (circle != null)
            {
                if (circle.Radius <= 0.0)
                    summary.AddIgnored("CIRCLE 半径必须大于零");
                else
                {
                    input.Circles.Add(new LayoutCircle(new LayoutPoint(
                        circle.Center.X, circle.Center.Y), circle.Radius));
                    summary.AddCircle();
                }
                return;
            }

            if (entity is BlockReference)
                summary.AddIgnored("块参照或外部参照未展开");
            else
                summary.AddIgnored("不支持的对象类型 " +
                    entity.GetType().Name);
        }

        private static bool TryReadPath(Polyline polyline,
            out LayoutPath path, out string reason)
        {
            path = null;
            reason = String.Empty;
            if (polyline.Closed)
            {
                reason = "多段线已经闭合";
                return false;
            }
            if (Math.Abs(polyline.Normal.X) > 1e-9 ||
                Math.Abs(polyline.Normal.Y) > 1e-9 ||
                polyline.Normal.Z < 0.999999)
            {
                reason = "多段线不是正Z法向二维对象";
                return false;
            }
            List<LayoutPoint> points = new List<LayoutPoint>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                if (Math.Abs(polyline.GetBulgeAt(i)) > 1e-9)
                {
                    reason = "多段线包含圆弧段";
                    return false;
                }
                Point2d point = polyline.GetPoint2dAt(i);
                points.Add(new LayoutPoint(point.X, point.Y));
            }
            try
            {
                path = new LayoutPath(points);
                return true;
            }
            catch (System.Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }
        private static void PreviewAndCommit(Editor editor, Database database, LayoutResult result, double elevation, LayoutSettings settings)
        {
            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = LayoutDrawingService.EnsureLayer(database, transaction, settings.OutputLayer);
                LayoutSettingsSnapshot snapshot = new LayoutSettingsSnapshot
                {
                    Length = settings.Length,
                    Width = settings.Width,
                    PowerWp = settings.PowerWp,
                    SlopeDegrees = settings.SlopeDegrees,
                    Orientation = result.OrientationName,
                    Rows = result.Rows,
                    StringSize = settings.StringSize
                };
                LayoutDrawingService.AppendRectangles(database, transaction, layerId, result, elevation, snapshot, Guid.NewGuid().ToString("N")); database.TransactionManager.QueueForGraphicsFlush(); editor.Regen();
                PromptKeywordOptions confirm = new PromptKeywordOptions("\n当前显示的是组件自动排布预览。[确认(C)/取消(X)] <确认>: "); confirm.AllowNone = true; confirm.Keywords.Add("C"); confirm.Keywords.Add("X"); confirm.Keywords.Default = "C"; PromptResult decision = editor.GetKeywords(confirm);
                string keyword = decision.Status == PromptStatus.None ? "C" : decision.StringResult;
                if (String.Equals(keyword, "C", StringComparison.OrdinalIgnoreCase)) { transaction.Commit(); editor.WriteMessage("\n已生成 {0} 块组件，可使用一次 UNDO 撤销。 ", result.ComponentCount); }
                else editor.WriteMessage("\n已取消，图纸未发生修改。 ");
            }
            editor.Regen();
        }
        private static bool TryReadPolygon(Polyline polyline, out LayoutPolygon polygon, out string reason)
        {
            const double closureTolerance = 0.1;
            polygon = null;
            reason = String.Empty;
            if (Math.Abs(polyline.Normal.X) > 1e-9 ||
                Math.Abs(polyline.Normal.Y) > 1e-9 ||
                polyline.Normal.Z < 0.999999)
            {
                reason = "多段线不是正Z法向二维对象";
                return false;
            }
            int segmentCount = polyline.Closed
                ? polyline.NumberOfVertices
                : Math.Max(0, polyline.NumberOfVertices - 1);
            for (int i = 0; i < segmentCount; i++)
                if (Math.Abs(polyline.GetBulgeAt(i)) > 1e-9)
                {
                    reason = "多段线包含圆弧段";
                    return false;
                }
            List<LayoutPoint> points = new List<LayoutPoint>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point2d point = polyline.GetPoint2dAt(i);
                points.Add(new LayoutPoint(point.X, point.Y));
            }
            return LayoutBoundaryReader.TryCreate(points, polyline.Closed,
                closureTolerance, out polygon, out reason);
        }

        private static void VerifyBoundaryReadingDiagnostic(Editor editor)
        {
            LayoutPolygon polygon;
            string reason;
            using (Polyline nearClosed = new Polyline())
            {
                nearClosed.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                nearClosed.AddVertexAt(1, new Point2d(1000, 0), 0, 0, 0);
                nearClosed.AddVertexAt(2, new Point2d(1000, 1000), 0, 0, 0);
                nearClosed.AddVertexAt(3, new Point2d(0, 1000), 0, 0, 0);
                nearClosed.AddVertexAt(4, new Point2d(0.05, 0), 0, 0, 0);
                if (!TryReadPolygon(nearClosed, out polygon, out reason))
                    throw new InvalidOperationException(
                        "Near-closed boundary diagnostic failed: " + reason);
                LayoutEngine.Solve(new LayoutInput { Boundary = polygon },
                    new LayoutOptions
                    {
                        Orientation = LayoutOrientation.Vertical,
                        AllowedRows = new[] { 1 },
                        Direction = new LayoutVector(1, 0)
                    });
            }
        }
    }
}
