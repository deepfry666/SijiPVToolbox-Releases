using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Ballast.Core;
using StructuralToolbox.Ballast.Settings;
using StructuralToolbox.Ballast.UI;
using StructuralToolbox.ArrayRecognition.Core;
using StructuralToolbox.ArrayRecognition.Commands;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.Ballast.Commands
{
    public sealed class BallastCommands
    {
        private sealed class BallastWorkflowSession
        {
            public string DocumentIdentity;
            public List<BallastPanel> Panels;
            public List<ObjectId> ExistingToolFrames;
            public List<ObjectId> ComponentIds;
            public string ComponentFingerprint;
            public BallastArrayDetectionResult Detection;
            public ArrayRecognitionSummary ArraySummary;
            public ArrayComponent Sample;
            public List<ArraySelectionWindow> Windows;
            public string BallastLayerName;
        }

        private sealed class ArraySelectionWindow
        {
            public Point3d First;
            public Point3d Second;
        }

        private sealed class ArrayWindowSelection
        {
            public List<ObjectId> ObjectIds;
            public List<ArraySelectionWindow> Windows;
        }

        private static BallastWorkflowSession _workflow;

        [CommandMethod("STPVBALLASTINFO", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void InspectBallast()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            PromptEntityOptions prompt = new PromptEntityOptions(
                "\n请选择本工具生成的配重榀: ");
            prompt.AddAllowedClass(typeof(BlockReference), true);
            PromptEntityResult selected = editor.GetEntity(prompt);
            if (selected.Status != PromptStatus.OK)
                return;

            using (Transaction transaction =
                document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                BlockReference reference = transaction.GetObject(selected.ObjectId,
                    OpenMode.ForRead, false) as BlockReference;
                if (reference == null)
                    return;
                using (ResultBuffer buffer = reference.GetXDataForApplication(
                    BallastDrawingService.ToolRegAppName))
                {
                    if (buffer == null)
                    {
                        editor.WriteMessage("\n该对象不是本工具生成的配重榀。 ");
                        return;
                    }
                    TypedValue[] values = buffer.AsArray();
                    string runId = GetXDataString(values, 2);
                    string arrayId = GetXDataString(values, 3);
                    int frameIndex = GetXDataInteger(values, 4);
                    bool conflict = GetXDataInteger(values, 5) != 0;
                    string reason = GetXDataString(values, 6);
                    editor.WriteMessage(
                        "\n配重信息：阵列 {0}，第 {1} 榀，批次 {2}。",
                        arrayId, frameIndex + 1, runId);
                    editor.WriteMessage("\n状态：{0}",
                        conflict ? "需要人工复核" : "正常");
                    if (!String.IsNullOrWhiteSpace(reason))
                        editor.WriteMessage("\n原因：{0}", reason);
                }
                transaction.Commit();
            }
        }

        [CommandMethod("STPVBALLASTDELETE", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void DeleteBallasts()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            PromptSelectionOptions selectionOptions = new PromptSelectionOptions();
            selectionOptions.MessageForAdding =
                "\n请选择要删除的本工具配重榀: ";
            PromptSelectionResult selection = editor.GetSelection(selectionOptions);
            if (selection.Status != PromptStatus.OK)
                return;

            PromptKeywordOptions confirm = new PromptKeywordOptions(
                "\n只删除未被人工移动、旋转或缩放的工具配重榀。"
                + "[删除(D)/取消(X)] <取消>: ");
            confirm.AllowNone = true;
            confirm.Keywords.Add("D");
            confirm.Keywords.Add("X");
            confirm.Keywords.Default = "X";
            PromptResult decision = editor.GetKeywords(confirm);
            if (decision.Status != PromptStatus.OK ||
                !String.Equals(decision.StringResult, "D",
                    StringComparison.OrdinalIgnoreCase))
                return;

            int protectedCount;
            int erasedCount;
            using (Transaction transaction =
                document.Database.TransactionManager.StartTransaction())
            {
                erasedCount = BallastDrawingService.EraseSelectedUnmodifiedFrames(
                    transaction, selection.Value.GetObjectIds(), out protectedCount);
                transaction.Commit();
            }
            editor.Regen();
            editor.WriteMessage(
                "\n已删除 {0} 个未修改配重榀；保护 {1} 个人工修改或旧版对象。",
                erasedCount, protectedCount);
        }

        [CommandMethod("STPVBALLAST", CommandFlags.Modal)]
        public void PlaceBallasts()
        {
            BallastWorkflowBridge.Show();
        }

        [CommandMethod("STPVBALLASTDETECT", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void DetectBallastArrays()
        {
            DetectSharedArrays();
        }

        private void DetectSharedArrays()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            Database database = document.Database;
            BallastSettings settings = BallastSettingsStore.Load();
            try
            {
                BallastLayoutOptions options = settings.ToOptions();
                PromptEntityOptions samplePrompt = new PromptEntityOptions(
                    "\n选择组件样本（闭合二维多段线或顶层块参照）: ");
                samplePrompt.SetRejectMessage(
                    "\n请选择闭合二维多段线或顶层块参照。 ");
                samplePrompt.AddAllowedClass(typeof(Polyline), true);
                samplePrompt.AddAllowedClass(typeof(Polyline2d), true);
                samplePrompt.AddAllowedClass(typeof(BlockReference), true);
                PromptEntityResult sampleResult = editor.GetEntity(samplePrompt);
                if (sampleResult.Status != PromptStatus.OK)
                {
                    BallastWorkflowBridge.Error("阵列识别已取消。");
                    return;
                }
                ArrayComponent sample;
                using (Transaction transaction =
                    database.TransactionManager.StartOpenCloseTransaction())
                {
                    Entity entity = transaction.GetObject(sampleResult.ObjectId,
                        OpenMode.ForRead, false) as Entity;
                    string reason;
                    if (!ArrayComponentReader.TryRead(entity, transaction,
                        out sample, out reason))
                    {
                        BallastWorkflowBridge.Error("所选组件样本无效：" + reason);
                        return;
                    }
                    transaction.Commit();
                }

                ArrayWindowSelection windowSelection = PromptArrayWindows(editor);
                if (windowSelection == null)
                {
                    BallastWorkflowBridge.Error("阵列识别已取消。");
                    return;
                }
                List<ArrayComponent> candidates = new List<ArrayComponent>();
                List<ObjectId> componentIds = new List<ObjectId>();
                List<ObjectId> existingFrames = new List<ObjectId>();
                Dictionary<string, int> rejected =
                    new Dictionary<string, int>(StringComparer.Ordinal);
                using (Transaction transaction =
                    database.TransactionManager.StartOpenCloseTransaction())
                {
                    for (int index = 0; index < windowSelection.ObjectIds.Count; index++)
                    {
                        Entity entity = transaction.GetObject(
                            windowSelection.ObjectIds[index],
                            OpenMode.ForRead, false) as Entity;
                        if (entity == null)
                            continue;
                        if (BallastDrawingService.IsToolFrame(entity))
                        {
                            existingFrames.Add(entity.ObjectId);
                            continue;
                        }
                        if (String.Equals(entity.Layer, options.LayerName,
                            StringComparison.OrdinalIgnoreCase))
                            continue;
                        ArrayComponent component;
                        string reason;
                        if (ArrayComponentReader.TryRead(entity, transaction,
                            out component, out reason) &&
                            MatchesSample(component, sample, out reason))
                        {
                            candidates.Add(component);
                            componentIds.Add(entity.ObjectId);
                        }
                        else
                            AddReason(rejected, reason);
                    }
                    transaction.Commit();
                }
                if (candidates.Count == 0)
                {
                    BallastWorkflowBridge.Error(
                        "没有识别到与样本同图层、同规格的有效组件。" +
                        BuildRejectedDetails(rejected));
                    return;
                }

                ArrayRecognitionResult recognition = ArrayRecognitionEngine.Detect(
                    candidates, new ArrayRecognitionOptions
                    {
                        MaximumOutlineGap = options.MaxComponentGap
                    });
                List<BallastPanel> panels = ToBallastPanels(candidates);
                BallastArrayDetectionResult detection =
                    BallastLayoutEngine.DetectArrays(panels, options);
                if (detection.Arrays.Count == 0)
                {
                    BallastWorkflowBridge.Error(
                        "没有识别到完整阵列。" +
                        BuildInvalidDetails(detection.InvalidArrays) +
                        BuildRejectedDetails(rejected));
                    return;
                }
                for (int index = 0; index < detection.Arrays.Count; index++)
                    detection.Arrays[index].Id = "A" + (index + 1).ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                List<int> rowCounts = new List<int>();
                Dictionary<int, int> counts = new Dictionary<int, int>();
                for (int index = 0; index < detection.Arrays.Count; index++)
                {
                    int rows = detection.Arrays[index].RowsAlongLong;
                    int count;
                    if (!counts.TryGetValue(rows, out count))
                    {
                        count = 0;
                        rowCounts.Add(rows);
                    }
                    counts[rows] = count + 1;
                }
                rowCounts.Sort();
                ArrayRecognitionSummary summary =
                    ArrayRecognitionStatistics.Summarize(recognition);
                _workflow = new BallastWorkflowSession
                {
                    DocumentIdentity = GetDocumentIdentity(document),
                    Panels = panels, ComponentIds = componentIds,
                    ComponentFingerprint =
                        ArrayRecognitionFingerprint.Create(candidates),
                    ExistingToolFrames = existingFrames, Detection = detection,
                    ArraySummary = summary, Sample = sample,
                    Windows = windowSelection.Windows,
                    BallastLayerName = options.LayerName
                };
                string details = BuildArraySummaryDetails(summary) +
                    BuildInvalidDetails(detection.InvalidArrays) +
                    BuildRejectedDetails(rejected);
                BallastWorkflowBridge.Detection(
                    summary.RecognizedComponentCount, summary.RecognizedArrayCount,
                    rowCounts, counts, details);
            }
            catch (System.Exception exception)
            {
                _workflow = null;
                BallastWorkflowBridge.Error("阵列识别失败：" + exception.Message);
            }
        }

        [CommandMethod("STPVBALLASTGENERATE", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void GenerateBallasts()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            if (_workflow == null ||
                !String.Equals(_workflow.DocumentIdentity,
                    GetDocumentIdentity(document),
                    StringComparison.OrdinalIgnoreCase))
            {
                BallastWorkflowBridge.Error(
                    "当前图纸没有可用的阵列识别结果，请重新识别。 ");
                return;
            }
            if (!ValidateWorkflow(document))
            {
                BallastWorkflowBridge.Error(
                    "组件对象已被修改、删除或替换，识别结果已过期，请重新识别。");
                return;
            }

            try
            {
                BallastSettings settings = BallastSettingsStore.Load();
                BallastLayoutOptions options = settings.ToOptions();
                BallastLayoutResult layout = BallastLayoutEngine.Create(
                    _workflow.Detection, options);
                if (layout.Plans.Count == 0)
                {
                    BallastWorkflowBridge.Error(
                        "没有生成可用配重阵列。" +
                        BuildInvalidDetails(layout.InvalidArrays));
                    return;
                }

                int protectedCount;
                int erasedCount;
                BallastDrawingService.BallastDrawSummary summary;
                using (Transaction transaction =
                    document.Database.TransactionManager.StartTransaction())
                {
                    erasedCount =
                        BallastDrawingService.EraseSelectedUnmodifiedFrames(
                            transaction, _workflow.ExistingToolFrames,
                            out protectedCount);
                    ObjectId layerId = BallastDrawingService.EnsureBallastLayer(
                        document.Database, transaction, options.LayerName);
                    summary = BallastDrawingService.AppendBallasts(
                        document.Database, transaction, layerId, layout,
                        "R" + Guid.NewGuid().ToString("N"), options);
                    transaction.Commit();
                }
                document.Editor.Regen();
                BallastWorkflowBridge.Generated(summary.FrameCount,
                    summary.BallastCount, summary.ConflictFrameCount,
                    erasedCount, protectedCount,
                    BuildGenerationDetails(layout));
                _workflow.ExistingToolFrames = summary.FrameIds;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                BallastWorkflowBridge.Error(
                    "生成配重失败：" + exception.Message);
            }
            catch (System.Exception exception)
            {
                BallastWorkflowBridge.Error(
                    "生成配重失败：" + exception.Message);
            }
        }

        [CommandMethod("STPVBALLASTARRAYSETTINGS", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void ConfigureArrayOverrides()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null || _workflow == null ||
                !String.Equals(_workflow.DocumentIdentity,
                    GetDocumentIdentity(document),
                    StringComparison.OrdinalIgnoreCase))
            {
                BallastWorkflowBridge.Error("请先完成组件阵列识别。 ");
                return;
            }
            BallastSettings settings = BallastSettingsStore.Load();
            if (!ValidateWorkflow(document))
            {
                BallastWorkflowBridge.Error(
                    "组件对象已变化，特殊阵列设置已停止，请重新识别。");
                return;
            }
            BallastArrayDetectionResult detection = _workflow.Detection;
            using (BallastArrayRulesDialog dialog =
                new BallastArrayRulesDialog(settings, detection.Arrays))
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    BallastSettingsStore.Save(settings);
            }
        }

        [CommandMethod("STPVBALLASTARRAYTABLE", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void InsertArrayTable()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null || _workflow == null ||
                !String.Equals(_workflow.DocumentIdentity,
                    GetDocumentIdentity(document),
                    StringComparison.OrdinalIgnoreCase))
            {
                BallastWorkflowBridge.Error("请先完成组件阵列识别。");
                return;
            }
            if (!ValidateWorkflow(document))
            {
                BallastWorkflowBridge.Error(
                    "组件对象已变化，阵列清单未插入，请重新识别。");
                return;
            }
            PromptPointResult point = document.Editor.GetPoint(
                "\n指定组件阵列统计表插入点: ");
            if (point.Status != PromptStatus.OK)
                return;
            using (Transaction transaction =
                document.Database.TransactionManager.StartTransaction())
            {
                QuantityTableService.AppendArrayTable(document.Database,
                    transaction, point.Value, _workflow.ArraySummary);
                transaction.Commit();
            }
            document.Editor.Regen();
            BallastWorkflowBridge.ArrayTableInserted();
        }

        [CommandMethod("STPVBALLASTPIERRECOGNIZE", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void RecognizeSinglePiers()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            BallastSettings settings = BallastSettingsStore.Load();
            int recognized = 0;
            int alreadyRecognized = 0;
            int rejected = 0;
            while (true)
            {
                PromptEntityOptions prompt = new PromptEntityOptions(
                    "\n点选要识别的单个配重墩，回车结束: ");
                prompt.AllowNone = true;
                PromptEntityResult selected = editor.GetEntity(prompt);
                if (selected.Status == PromptStatus.None)
                    break;
                if (selected.Status != PromptStatus.OK)
                    return;

                BallastStatisticItem item;
                string reason;
                using (Transaction read = document.Database.TransactionManager
                    .StartOpenCloseTransaction())
                {
                    Entity entity = read.GetObject(selected.ObjectId,
                        OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                    {
                        rejected++;
                        editor.WriteMessage("\n所选对象无法读取。 ");
                        continue;
                    }
                    if (BallastDrawingService.IsToolFrame(entity))
                    {
                        rejected++;
                        editor.WriteMessage(
                            "\n所选对象是一榀配重，请点选单独绘制的圆墩或方墩。 ");
                        continue;
                    }
                    item = BallastDrawingService.ReadRecognizedSinglePier(
                        read, entity);
                    if (item != null)
                    {
                        alreadyRecognized++;
                        editor.WriteMessage("\n该对象已识别为单墩：{0}。",
                            item.Specification);
                        continue;
                    }
                    if (!BallastDrawingService.TryCreateManualPierStatistic(
                            entity, settings, out item, out reason))
                    {
                        rejected++;
                        editor.WriteMessage("\n未识别：{0}", reason);
                        continue;
                    }
                }

                PromptKeywordOptions confirm = new PromptKeywordOptions(
                    "\n识别为 " + item.Specification +
                    " [确认(Y)/跳过(N)] <确认>: ");
                confirm.AllowNone = true;
                confirm.Keywords.Add("Y");
                confirm.Keywords.Add("N");
                confirm.Keywords.Default = "Y";
                PromptResult decision = editor.GetKeywords(confirm);
                if (decision.Status == PromptStatus.Cancel)
                    return;
                if (decision.Status != PromptStatus.None &&
                    (decision.Status != PromptStatus.OK ||
                     !String.Equals(decision.StringResult, "Y",
                        StringComparison.OrdinalIgnoreCase)))
                    continue;

                using (Transaction write = document.Database.TransactionManager
                    .StartTransaction())
                {
                    BallastDrawingService.EnsureRegApp(document.Database, write);
                    Entity entity = write.GetObject(selected.ObjectId,
                        OpenMode.ForWrite, false) as Entity;
                    if (entity == null)
                        throw new InvalidOperationException("所选对象已不可用。 ");
                    BallastDrawingService.MarkSinglePier(entity, item);
                    write.Commit();
                }
                recognized++;
            }
            editor.Regen();
            editor.WriteMessage(
                "\n单墩识别完成：新增 {0} 个，已有 {1} 个，未识别 {2} 个。",
                recognized, alreadyRecognized, rejected);
        }

        [CommandMethod("STPVBALLASTSTAT", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void Statistics()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            PromptPointResult first = editor.GetPoint("\n指定统计范围第一个角点: ");
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

            BallastStatisticSummary summary = new BallastStatisticSummary();
            List<BallastStatisticItem> items = new List<BallastStatisticItem>();
            int nestedCount = 0;
            int standaloneCount = 0;
            using (Transaction transaction = document.Database.TransactionManager
                .StartTransaction())
            {
                ObjectId[] ids = selection.Value.GetObjectIds();
                for (int index = 0; index < ids.Length; index++)
                {
                    Entity entity = transaction.GetObject(ids[index],
                        OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                        continue;
                    BlockReference frame = entity as BlockReference;
                    IList<BallastStatisticItem> frameItems =
                        BallastDrawingService.ReadStatisticItems(transaction,
                            frame, first.Value, second.Value);
                    if (BallastDrawingService.IsToolFrame(entity))
                    {
                        items.AddRange(frameItems);
                        nestedCount += frameItems.Count;
                        continue;
                    }
                    BallastStatisticItem standalone =
                        BallastDrawingService.ReadTopLevelSinglePier(transaction,
                            entity, first.Value, second.Value);
                    if (standalone != null)
                    {
                        items.Add(standalone);
                        standaloneCount++;
                    }
                }
                summary = BallastStatistics.Summarize(items);
                if (summary.TotalQuantity > 0)
                {
                    PromptPointResult tablePoint = editor.GetPoint(
                        "\n指定配重统计表插入点: ");
                    if (tablePoint.Status == PromptStatus.OK)
                        QuantityTableService.AppendBallastTable(document.Database,
                            transaction, tablePoint.Value, summary);
                }
                transaction.Commit();
            }
            if (summary.TotalQuantity == 0)
            {
                editor.WriteMessage(
                    "\n范围内没有已识别单墩；手画墩请先使用点选识别。 ");
                return;
            }
            editor.WriteMessage(
                "\n配重块统计完成：{0} 个（榀内 {1} 个，独立 {2} 个）。",
                summary.TotalQuantity, nestedCount, standaloneCount);
        }

        private static string GetDocumentIdentity(Document document)
        {
            if (document == null)
                return String.Empty;
            string name = document.Name;
            if (String.IsNullOrWhiteSpace(name) && document.Database != null)
                name = document.Database.Filename;
            if (String.IsNullOrWhiteSpace(name))
                return String.Empty;
            name = name.Trim();
            try
            {
                return System.IO.Path.GetFullPath(name).TrimEnd('\\');
            }
            catch (System.Exception)
            {
                return name;
            }
        }

        private static string BuildDetectionDetails(
            IDictionary<string, int> rejectedReasons,
            IList<BallastInvalidArray> invalidArrays, int existingFrameCount)
        {
            List<string> details = new List<string>();
            if (existingFrameCount > 0)
                details.Add("检测到 " + existingFrameCount +
                    " 个旧配重榀，生成时将更新未被人工修改的对象。");
            string rejected = BuildRejectedDetails(rejectedReasons);
            if (!String.IsNullOrWhiteSpace(rejected))
                details.Add(rejected);
            string invalid = BuildInvalidDetails(invalidArrays).Trim();
            if (!String.IsNullOrWhiteSpace(invalid))
                details.Add(invalid);
            return String.Join("\n", details.ToArray());
        }

        private static string BuildGenerationDetails(BallastLayoutResult layout)
        {
            List<string> details = new List<string>();
            details.Add("可使用一次 UNDO 撤销本次生成。");
            string invalid = BuildInvalidDetails(layout.InvalidArrays).Trim();
            if (!String.IsNullOrWhiteSpace(invalid))
                details.Add(invalid);
            string conflicts = BuildConflictDetails(layout);
            if (!String.IsNullOrWhiteSpace(conflicts))
                details.Add(conflicts);
            return String.Join("\n", details.ToArray());
        }

        private static string BuildRejectedDetails(
            IDictionary<string, int> rejectedReasons)
        {
            if (rejectedReasons == null || rejectedReasons.Count == 0)
                return String.Empty;
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, int> entry in rejectedReasons)
                lines.Add(entry.Key + " " + entry.Value.ToString() + " 个");
            return "未识别对象：" + String.Join("；", lines.ToArray());
        }

        private static string BuildInvalidDetails(
            IList<BallastInvalidArray> invalidArrays)
        {
            if (invalidArrays == null || invalidArrays.Count == 0)
                return String.Empty;
            Dictionary<string, int> counts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < invalidArrays.Count; index++)
            {
                BallastInvalidArray invalid = invalidArrays[index];
                string reason = String.IsNullOrWhiteSpace(invalid.Reason)
                    ? "未知原因" : invalid.Reason.Trim();
                int count;
                if (!counts.TryGetValue(reason, out count))
                    count = 0;
                counts[reason] = count + 1;
            }
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, int> entry in counts)
                lines.Add(entry.Key + " " + entry.Value + " 个");
            return "\n异常阵列：" + String.Join("；", lines.ToArray());
        }

        private static string BuildConflictDetails(BallastLayoutResult layout)
        {
            if (layout == null || layout.Plans == null)
                return String.Empty;
            Dictionary<string, int> counts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            for (int planIndex = 0; planIndex < layout.Plans.Count; planIndex++)
            {
                BallastArrayPlan plan = layout.Plans[planIndex];
                if (plan == null || plan.Frames == null)
                    continue;
                for (int frameIndex = 0; frameIndex < plan.Frames.Count; frameIndex++)
                {
                    BallastFramePlan frame = plan.Frames[frameIndex];
                    if (frame == null || frame.Ballasts == null)
                        continue;
                    for (int pointIndex = 0;
                        pointIndex < frame.Ballasts.Count; pointIndex++)
                    {
                        BallastPointPlan point = frame.Ballasts[pointIndex];
                        if (point == null || !point.Conflict ||
                            String.IsNullOrWhiteSpace(point.ConflictReason))
                            continue;
                        int count;
                        if (!counts.TryGetValue(point.ConflictReason, out count))
                            count = 0;
                        counts[point.ConflictReason] = count + 1;
                    }
                }
            }
            if (counts.Count == 0)
                return String.Empty;
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, int> entry in counts)
                lines.Add(entry.Key + " " + entry.Value.ToString() + " 个");
            return "红色问题原因：" + String.Join("；", lines.ToArray());
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
            try
            {
                return Convert.ToInt32(values[index].Value);
            }
            catch
            {
                return 0;
            }
        }

        private static bool MatchesSample(BallastPanel panel,
            BallastPanel sample, out string reason)
        {
            reason = String.Empty;
            if (panel == null || sample == null)
            {
                reason = "组件数据为空";
                return false;
            }
            if (Math.Abs(panel.LongLength - sample.LongLength) > 0.5 ||
                Math.Abs(panel.ShortLength - sample.ShortLength) > 0.5)
            {
                reason = "矩形尺寸与样本组件不一致";
                return false;
            }
            double dot = Math.Abs(BallastVec2.Dot(panel.LongAxis,
                sample.LongAxis));
            if (dot < Math.Cos(0.05 * Math.PI / 180.0))
            {
                reason = "矩形方向与样本组件不一致";
                return false;
            }
            if (Math.Abs(panel.Elevation - sample.Elevation) > 0.01)
            {
                reason = "矩形标高与样本组件不一致";
                return false;
            }
            return true;
        }

        private static bool FullyInsideWindow(BallastPanel panel,
            Point3d first, Point3d second, out string reason)
        {
            reason = String.Empty;
            double minimumX = Math.Min(first.X, second.X);
            double maximumX = Math.Max(first.X, second.X);
            double minimumY = Math.Min(first.Y, second.Y);
            double maximumY = Math.Max(first.Y, second.Y);
            BallastVec2 halfLong = panel.LongAxis * (panel.LongLength / 2.0);
            BallastVec2 halfShort = panel.ShortAxis * (panel.ShortLength / 2.0);
            BallastVec2[] corners = new BallastVec2[]
            {
                panel.Center - halfLong - halfShort,
                panel.Center - halfLong + halfShort,
                panel.Center + halfLong - halfShort,
                panel.Center + halfLong + halfShort
            };
            for (int index = 0; index < corners.Length; index++)
            {
                if (corners[index].X < minimumX - 0.01 ||
                    corners[index].X > maximumX + 0.01 ||
                    corners[index].Y < minimumY - 0.01 ||
                    corners[index].Y > maximumY + 0.01)
                {
                    reason = "组件未完全位于框选范围内";
                    return false;
                }
            }
            return true;
        }

        private static ArrayWindowSelection PromptArrayWindows(Editor editor)
        {
            Dictionary<ObjectId, bool> ids = new Dictionary<ObjectId, bool>();
            List<ArraySelectionWindow> windows =
                new List<ArraySelectionWindow>();
            while (true)
            {
                PromptPointResult first = editor.GetPoint(
                    "\n指定处理范围第一个角点: ");
                if (first.Status != PromptStatus.OK)
                    return null;
                PromptCornerOptions corner = new PromptCornerOptions(
                    "\n指定另一个角点: ", first.Value);
                PromptPointResult second = editor.GetCorner(corner);
                if (second.Status != PromptStatus.OK)
                    return null;
                PromptSelectionResult selection = editor.SelectCrossingWindow(
                    first.Value, second.Value);
                windows.Add(new ArraySelectionWindow
                {
                    First = first.Value,
                    Second = second.Value
                });
                if (selection.Status == PromptStatus.OK)
                    foreach (ObjectId id in selection.Value.GetObjectIds())
                        ids[id] = true;
                PromptKeywordOptions more = new PromptKeywordOptions(
                    "\n继续添加框选范围？[继续(C)/完成(F)/取消(X)] <完成>: ");
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
            return new ArrayWindowSelection
            {
                ObjectIds = new List<ObjectId>(ids.Keys),
                Windows = windows
            };
        }

        private static bool MatchesSample(ArrayComponent component,
            ArrayComponent sample, out string reason)
        {
            reason = String.Empty;
            if (!String.Equals(component.LayerName, sample.LayerName,
                StringComparison.OrdinalIgnoreCase))
            {
                reason = "对象图层与样本组件不一致";
                return false;
            }
            if (Math.Abs(component.LongLength - sample.LongLength) > 0.5 ||
                Math.Abs(component.ShortLength - sample.ShortLength) > 0.5)
            {
                reason = "组件尺寸与样本组件不一致";
                return false;
            }
            return true;
        }

        private static List<BallastPanel> ToBallastPanels(
            IList<ArrayComponent> components)
        {
            List<BallastPanel> result = new List<BallastPanel>();
            for (int index = 0; index < components.Count; index++)
            {
                ArrayComponent item = components[index];
                result.Add(new BallastPanel(
                    new BallastVec2(item.Center.X, item.Center.Y),
                    new BallastVec2(item.LongAxis.X, item.LongAxis.Y),
                    new BallastVec2(item.ShortAxis.X, item.ShortAxis.Y),
                    item.LongLength, item.ShortLength, item.Elevation, item.SourceId));
            }
            return result;
        }

        private static void AddReason(IDictionary<string, int> reasons, string reason)
        {
            if (String.IsNullOrWhiteSpace(reason))
                reason = "未知原因";
            int count;
            if (!reasons.TryGetValue(reason, out count))
                count = 0;
            reasons[reason] = count + 1;
        }

        private static bool ValidateWorkflow(Document document)
        {
            try
            {
                Dictionary<ObjectId, bool> selected =
                    new Dictionary<ObjectId, bool>();
                for (int windowIndex = 0;
                    windowIndex < _workflow.Windows.Count; windowIndex++)
                {
                    ArraySelectionWindow window = _workflow.Windows[windowIndex];
                    PromptSelectionResult selection = document.Editor.SelectCrossingWindow(
                        window.First, window.Second);
                    if (selection.Status != PromptStatus.OK)
                        continue;
                    foreach (ObjectId id in selection.Value.GetObjectIds())
                        selected[id] = true;
                }
                List<ArrayComponent> current = new List<ArrayComponent>();
                using (Transaction transaction = document.Database.TransactionManager
                    .StartOpenCloseTransaction())
                {
                    foreach (ObjectId id in selected.Keys)
                    {
                        Entity entity = transaction.GetObject(id,
                            OpenMode.ForRead, false) as Entity;
                        if (entity == null || BallastDrawingService.IsToolFrame(entity) ||
                            String.Equals(entity.Layer, _workflow.BallastLayerName,
                                StringComparison.OrdinalIgnoreCase))
                            continue;
                        ArrayComponent component;
                        string reason;
                        if (ArrayComponentReader.TryRead(entity, transaction,
                            out component, out reason) &&
                            MatchesSample(component, _workflow.Sample, out reason))
                            current.Add(component);
                    }
                    transaction.Commit();
                }
                return current.Count == _workflow.ComponentIds.Count && String.Equals(
                        ArrayRecognitionFingerprint.Create(current),
                        _workflow.ComponentFingerprint, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static string BuildArraySummaryDetails(
            ArrayRecognitionSummary summary)
        {
            List<string> rows = new List<string>();
            for (int index = 0; index < summary.Entries.Count; index++)
            {
                ArraySummaryEntry entry = summary.Entries[index];
                rows.Add(entry.CountAlongLong + "×" + entry.CountAlongShort +
                    "：" + entry.ArrayCount + " 个阵列 / " +
                    entry.TotalComponentCount + " 块组件");
            }
            rows.Add("正确识别合计：" + summary.RecognizedArrayCount +
                " 个阵列 / " + summary.RecognizedComponentCount + " 块组件");
            if (summary.InvalidArrayCount > 0)
                rows.Add("未正确识别合计：" + summary.InvalidArrayCount +
                    " 组 / " + summary.InvalidComponentCount + " 块组件");
            return String.Join("\n", rows.ToArray());
        }

    }
}
