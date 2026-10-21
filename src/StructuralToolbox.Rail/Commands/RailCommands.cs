using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Core;
using StructuralToolbox.Settings;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.Commands
{
    public sealed partial class RailCommands
    {
        private sealed class RailPreviewDecision
        {
            public bool Confirmed { get; set; }
            public int CreatedRailCount { get; set; }
            public int ErasedEntityCount { get; set; }
        }

        [CommandMethod("STPVRAIL", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        public void DrawRails()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;

            Editor editor = document.Editor;
            Database database = document.Database;
            RailSettings settings = RailSettingsStore.Load();

#if INTERNAL_VERIFY
            InternalRealDrawingTiming.BeginCommand();
#endif
            try
            {
                RailDrawingStyle drawingStyle = RailDrawingService.CreateStyle(settings);
                RailLayerInspection layerInspection;
                using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
                {
                    layerInspection = RailDrawingService.InspectLayer(
                        database, transaction, drawingStyle);
                    transaction.Commit();
                }
                if (!ResolveExistingLayerColor(editor, drawingStyle, layerInspection))
                    return;

                PromptEntityOptions samplePrompt = new PromptEntityOptions(
                    "\n请选择一个组件闭合多段线作为图层样本: ");
                samplePrompt.SetRejectMessage("\n请选择模型空间中的闭合轻量多段线。 ");
                samplePrompt.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult sampleResult = editor.GetEntity(samplePrompt);
                if (sampleResult.Status != PromptStatus.OK)
                    return;

                string componentLayer;
                using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
                {
                    Polyline sample = transaction.GetObject(
                        sampleResult.ObjectId, OpenMode.ForRead, false) as Polyline;
                    PanelRect samplePanel;
                    string sampleReason = "对象不是轻量多段线";
                    if (sample == null || !PanelRectangleReader.TryRead(sample, out samplePanel, out sampleReason))
                    {
                        AcApplication.ShowAlertDialog("所选对象不是有效组件矩形：" + sampleReason);
                        return;
                    }
                    componentLayer = sample.Layer;
                    transaction.Commit();
                }

                if (String.Equals(componentLayer, drawingStyle.LayerName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    AcApplication.ShowAlertDialog(
                        "样本组件不能位于输出图层【" + drawingStyle.LayerName + "】。 ");
                    return;
                }

                editor.WriteMessage("\n============================================================");
                editor.WriteMessage("\n四季光伏工具箱 - 自动画导轨");
                editor.WriteMessage("\n注意：本次仅识别图层【{0}】中的闭合矩形多段线。", componentLayer);
                editor.WriteMessage("\n输出图层：【{0}】；多段线全局宽度：{1:0.##}。",
                    drawingStyle.LayerName, drawingStyle.GlobalWidth);
                editor.WriteMessage("\n本流程仅生成导轨，不生成或修改导轨尺寸标注。 ");
                editor.WriteMessage("\n其他图层中的建筑轮廓、文字和多段线将全部忽略。 ");
                editor.WriteMessage("\n============================================================");

                PromptPointResult firstCorner = editor.GetPoint(
                    "\n指定组件框选区域的第一个角点: ");
                if (firstCorner.Status != PromptStatus.OK)
                    return;
                PromptCornerOptions secondCornerOptions = new PromptCornerOptions(
                    String.Format("\n指定另一个角点（仅处理图层【{0}】）: ", componentLayer),
                    firstCorner.Value);
                PromptPointResult secondCorner = editor.GetCorner(secondCornerOptions);
                if (secondCorner.Status != PromptStatus.OK)
                    return;

                PromptSelectionResult selectionResult = editor.SelectCrossingWindow(
                    firstCorner.Value, secondCorner.Value);
                if (selectionResult.Status != PromptStatus.OK)
                    return;

                List<PanelRect> panels = new List<PanelRect>();
                Dictionary<string, int> rejectedReasons =
                    new Dictionary<string, int>(StringComparer.Ordinal);
                ExistingRailState existingRailState;
                ToolRunWindowInspection replacementInspection;
                ObjectId[] selectedIds = selectionResult.Value.GetObjectIds();
                using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
                {
                    existingRailState = RailDrawingService.FindRailEntitiesInWindow(
                        database, transaction, firstCorner.Value, secondCorner.Value,
                        drawingStyle.LayerName);
                    replacementInspection =
                        RailDrawingService.InspectSelectedToolRuns(
                            database, transaction, selectedIds);
                    for (int index = 0; index < selectedIds.Length; index++)
                    {
                        Entity entity = transaction.GetObject(
                            selectedIds[index], OpenMode.ForRead, false) as Entity;
                        if (entity == null)
                            continue;
                        if (RailDrawingService.IsToolRail(entity) ||
                            String.Equals(entity.Layer, drawingStyle.LayerName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (!String.Equals(entity.Layer, componentLayer,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        Polyline polyline = entity as Polyline;
                        PanelRect panel;
                        string reason = "对象不是轻量多段线";
                        if (polyline != null && PanelRectangleReader.TryRead(polyline, out panel, out reason))
                        {
                            panels.Add(panel);
                        }
                        else
                        {
                            int reasonCount;
                            if (!rejectedReasons.TryGetValue(reason, out reasonCount))
                                reasonCount = 0;
                            rejectedReasons[reason] = reasonCount + 1;
                        }
                    }

                    transaction.Commit();
                }

                if (panels.Count == 0)
                {
                    AcApplication.ShowAlertDialog(
                        "框选范围内没有识别到有效组件。\n仅支持样本图层中的闭合矩形轻量多段线。 ");
                    return;
                }
                if (replacementInspection.PartialRunIds.Count > 0)
                {
                    AcApplication.ShowAlertDialog(String.Format(
                        "本次框选只覆盖了 {0} 批旧导轨的一部分。\n" +
                        "为避免删除框外成果，本次已停止。" +
                        "请重新框选这些旧批次的全部组件后再试。",
                        replacementInspection.PartialRunIds.Count));
                    return;
                }
                ISet<string> replacementRunIds = replacementInspection.RunIds;
                if (replacementRunIds.Count > 0 &&
                    !ConfirmReplaceToolEntities(editor, replacementRunIds.Count))
                {
                    return;
                }
                if ((existingRailState & ExistingRailState.OutputLayerEntity) != 0 &&
                    !ConfirmManualOutputLayerEntities(editor, drawingStyle.LayerName))
                {
                    return;
                }
                if ((existingRailState & ExistingRailState.LegacyRail) != 0 &&
                    !ConfirmLegacyRails(editor))
                {
                    return;
                }
                if (settings.RailCount > 1)
                {
                    if (settings.TotalSpacing <= 0.0)
                    {
                        AcApplication.ShowAlertDialog(
                            "导轨数量大于1时，首末导轨总间距必须大于0。 ");
                        return;
                    }
                    for (int index = 0; index < panels.Count; index++)
                    {
                        if (settings.TotalSpacing >= panels[index].LongLength - 0.01)
                        {
                            AcApplication.ShowAlertDialog(
                                "首末导轨总间距必须小于所有组件的长边尺寸。\n请在四季光伏工具箱中调整参数后重试。 ");
                            return;
                        }
                    }
                }

                RailLayoutResult layout;
#if INTERNAL_VERIFY
                InternalRealDrawingTiming.BeginPlanning();
                try
#endif
                {
                    RailLayoutOptions layoutOptions = new RailLayoutOptions();
                    layoutOptions.RailCount = settings.RailCount;
                    layoutOptions.TotalSpacing = settings.TotalSpacing;
                    layoutOptions.MaximumContinuityGap = settings.MaximumContinuityGap;
                    layoutOptions.EndOverhang = settings.EndOverhang;
                    layout = RailLayoutEngine.Create(panels, layoutOptions);
                    if (layout.Segments.Count == 0)
                    {
                        AcApplication.ShowAlertDialog("没有生成可用导轨段。 ");
                        return;
                    }
                }
#if INTERNAL_VERIFY
                finally
                {
                    InternalRealDrawingTiming.EndPlanning();
                }
#endif
                string componentGeometryHash =
                    GeometryFingerprint.CreatePanels(panels);
                string railGeometryHash =
                    GeometryFingerprint.CreateSegments(layout.Segments);

                editor.WriteMessage(
                    "\n识别组件 {0} 块，自动分为 {1} 个方向组；预览导轨 {2} 段。",
                    panels.Count, layout.PanelGroupCount, layout.Segments.Count);
                WriteRejectedSummary(editor, rejectedReasons);

                string fallbackRailRunId = Guid.NewGuid().ToString("N");
                Dictionary<string, string> runIdsByIsland =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                for (int islandIndex = 0;
                    islandIndex < layout.ArrayIslands.Count; islandIndex++)
                {
                    string islandId = layout.ArrayIslands[islandIndex].Id;
                    string islandRunId = Guid.NewGuid().ToString("N");
                    runIdsByIsland[islandId] = islandRunId;
                }

                RailRunMetadata runMetadata = new RailRunMetadata
                {
                    ComponentLayer = componentLayer,
                    ComponentGeometryHash = componentGeometryHash,
                    RailGeometryHash = railGeometryHash
                };
                RailPreviewDecision previewDecision;
                previewDecision = RunPreview(editor, database, layout,
                    drawingStyle, replacementRunIds, fallbackRailRunId,
                    runIdsByIsland, runMetadata);
                if (!previewDecision.Confirmed)
                {
                    editor.WriteMessage("\n已取消，图纸未发生修改。 ");
                    return;
                }

                int createdCount = previewDecision.CreatedRailCount;
                int erasedCount = previewDecision.ErasedEntityCount;
                editor.WriteMessage(
                    "\n完成：已生成 {0} 条导轨；" +
                    "未生成或修改尺寸标注，替换旧导轨 {1} 个。" +
                    "可使用一次 UNDO 撤销。 ",
                    createdCount, erasedCount);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                editor.WriteMessage("\n自动画导轨失败：{0}", exception.Message);
            }
            catch (System.Exception exception)
            {
                editor.WriteMessage("\n自动画导轨失败：{0}", exception.Message);
            }
#if INTERNAL_VERIFY
            finally
            {
                InternalRealDrawingTiming.EndCommand();
            }
#endif
        }

        [CommandMethod("STPVRAILSTAT", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void Statistics()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            PromptKeywordOptions mode = new PromptKeywordOptions(
                "\n统计对象 [工具生成(T)/选定范围全部(A)] <工具生成>: ");
            mode.AllowNone = true;
            mode.Keywords.Add("T");
            mode.Keywords.Add("A");
            mode.Keywords.Default = "T";
            PromptResult modeResult = editor.GetKeywords(mode);
            if (modeResult.Status != PromptStatus.OK &&
                modeResult.Status != PromptStatus.None)
                return;
            bool allObjects = String.Equals(modeResult.StringResult, "A",
                StringComparison.OrdinalIgnoreCase);
            RailSettings settings = RailSettingsStore.Load();
            PromptPointResult first = editor.GetPoint("\n指定导轨统计范围第一个角点: ");
            if (first.Status != PromptStatus.OK)
                return;
            PromptCornerOptions corner = new PromptCornerOptions(
                "\n指定另一个角点: ", first.Value);
            PromptPointResult second = editor.GetCorner(corner);
            if (second.Status != PromptStatus.OK)
                return;
            PromptSelectionResult selection = editor.SelectCrossingWindow(
                first.Value, second.Value);
            if (selection.Status != PromptStatus.OK)
                return;

            double millimeters = 0.0;
            using (Transaction transaction = document.Database.TransactionManager
                .StartTransaction())
            {
                ObjectId[] ids = selection.Value.GetObjectIds();
                for (int index = 0; index < ids.Length; index++)
                {
                    Entity entity = transaction.GetObject(ids[index],
                        OpenMode.ForRead, false) as Entity;
                    if (entity == null || (!allObjects &&
                        !RailDrawingService.IsToolRail(entity)))
                        continue;
                    if (allObjects && !String.Equals(entity.Layer,
                        settings.RailLayerName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    millimeters += GetCurveLength(entity);
                }
                PromptPointResult tablePoint = editor.GetPoint(
                    "\n指定导轨统计表插入点: ");
                if (tablePoint.Status == PromptStatus.OK)
                    QuantityTableService.AppendRailTable(document.Database,
                        transaction, tablePoint.Value,
                        RailStatistics.ToMeters(millimeters));
                transaction.Commit();
            }
            editor.WriteMessage("\n导轨统计完成：{0:0.00} m。",
                RailStatistics.ToMeters(millimeters));
        }

        private static double GetCurveLength(Entity entity)
        {
            Line line = entity as Line;
            if (line != null)
                return line.Length;
            Polyline polyline = entity as Polyline;
            if (polyline != null)
                return polyline.Length;
            Curve curve = entity as Curve;
            if (curve == null)
                return 0.0;
            try
            {
                return curve.GetDistanceAtParameter(curve.EndParam) -
                    curve.GetDistanceAtParameter(curve.StartParam);
            }
            catch
            {
                return 0.0;
            }
        }

        private static RailPreviewDecision RunPreview(Editor editor,
            Database database, RailLayoutResult layout,
            RailDrawingStyle railStyle, ISet<string> replacementRunIds,
            string fallbackRailRunId,
            IDictionary<string, string> runIdsByIsland,
            RailRunMetadata runMetadata)
        {
            RailPreviewDecision decision = new RailPreviewDecision();
            string keyword = "X";
            int erasedCount = 0;
            int createdRailCount = 0;
            using (Transaction transaction =
                database.TransactionManager.StartTransaction())
            {
                runMetadata.CreatedObjectIds.Clear();
                erasedCount = RailDrawingService.EraseToolRailsForRuns(
                    database, transaction, replacementRunIds);
                ObjectId railLayerId =
                    RailDrawingService.GetOrCreateRailLayer(
                        database, transaction, railStyle);
                createdRailCount = RailDrawingService.AppendRails(
                    database, transaction, railLayerId, layout, railStyle,
                    fallbackRailRunId, runIdsByIsland, runMetadata);

                database.TransactionManager.QueueForGraphicsFlush();
                editor.Regen();

                PromptKeywordOptions options = new PromptKeywordOptions(
                    "\n当前显示的是将要提交的原生导轨（不生成导轨尺寸标注）。" +
                    "[确认(C)/取消(X)] <确认>: ");
                options.AllowNone = true;
                options.Keywords.Add("C");
                options.Keywords.Add("X");
                options.Keywords.Default = "C";
                PromptResult promptResult = editor.GetKeywords(options);
                if (promptResult.Status == PromptStatus.None)
                    keyword = "C";
                else if (promptResult.Status == PromptStatus.OK)
                    keyword = promptResult.StringResult;

                if (String.Equals(keyword, "C",
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Commit();
                    decision.Confirmed = true;
                    decision.CreatedRailCount = createdRailCount;
                    decision.ErasedEntityCount = erasedCount;
                }
            }
            editor.Regen();
            return decision;
        }

        private static bool ConfirmReplaceToolEntities(Editor editor, int runCount)
        {
            PromptKeywordOptions prompt = new PromptKeywordOptions(String.Format(
                "\n框选范围内检测到 {0} 批本工具生成的导轨。" +
                "确认后只替换这些批次的工具导轨，尺寸标注不会删除，" +
                "手工实体也不会删除。[替换(R)/取消(X)] <取消>: ", runCount));
            prompt.AllowNone = true;
            prompt.Keywords.Add("R");
            prompt.Keywords.Add("X");
            prompt.Keywords.Default = "X";
            PromptResult result = editor.GetKeywords(prompt);
            return result.Status == PromptStatus.OK &&
                String.Equals(result.StringResult, "R", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ConfirmManualOutputLayerEntities(Editor editor,
            string layerName)
        {
            PromptKeywordOptions prompt = new PromptKeywordOptions(String.Format(
                "\n导轨输出图层【{0}】在框选范围内含有非本工具实体。" +
                "程序不会删除或改动它们，是否继续？" +
                "[继续(C)/取消(X)] <取消>: ", layerName));
            prompt.AllowNone = true;
            prompt.Keywords.Add("C");
            prompt.Keywords.Add("X");
            prompt.Keywords.Default = "X";
            PromptResult result = editor.GetKeywords(prompt);
            return result.Status == PromptStatus.OK &&
                String.Equals(result.StringResult, "C", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ResolveExistingLayerColor(Editor editor, RailDrawingStyle style,
            RailLayerInspection inspection)
        {
            if (style.ColorApplication != RailColorApplication.ByLayer ||
                !inspection.Exists || inspection.ColorMatches)
            {
                return true;
            }

            PromptKeywordOptions prompt = new PromptKeywordOptions(String.Format(
                "\n图层【{0}】已存在且颜色与设置不同。" +
                "[使用现有颜色(E)/修改图层颜色(M)/取消(X)] <使用现有颜色>: ",
                style.LayerName));
            prompt.AllowNone = true;
            prompt.Keywords.Add("E");
            prompt.Keywords.Add("M");
            prompt.Keywords.Add("X");
            prompt.Keywords.Default = "E";
            PromptResult result = editor.GetKeywords(prompt);
            if (result.Status != PromptStatus.None && result.Status != PromptStatus.OK)
                return false;
            if (
                (result.Status == PromptStatus.OK &&
                String.Equals(result.StringResult, "X", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            bool modify = result.Status == PromptStatus.OK &&
                String.Equals(result.StringResult, "M", StringComparison.OrdinalIgnoreCase);
            style.ChangeExistingLayerColor = modify;
            if (!modify)
                style.UseExistingLayerColorForPreview(inspection.ExistingColor);
            return true;
        }

        private static bool ConfirmLegacyRails(Editor editor)
        {
            PromptKeywordOptions prompt = new PromptKeywordOptions(
                "\n检测到默认图层【导轨】中可能存在旧版本导轨或辅助线。" +
                "为避免重复绘制，建议取消；确认要继续吗？[继续(C)/取消(X)] <取消>: ");
            prompt.AllowNone = true;
            prompt.Keywords.Add("C");
            prompt.Keywords.Add("X");
            prompt.Keywords.Default = "X";
            PromptResult result = editor.GetKeywords(prompt);
            return result.Status == PromptStatus.OK &&
                String.Equals(result.StringResult, "C", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteRejectedSummary(Editor editor,
            IDictionary<string, int> rejectedReasons)
        {
            if (rejectedReasons.Count == 0)
                return;

            int total = 0;
            foreach (KeyValuePair<string, int> pair in rejectedReasons)
                total += pair.Value;
            editor.WriteMessage("\n同图层中另有 {0} 个对象被忽略：", total);
            foreach (KeyValuePair<string, int> pair in rejectedReasons)
                editor.WriteMessage("{0}{1} 个；", pair.Key, pair.Value);
        }
    }
}
