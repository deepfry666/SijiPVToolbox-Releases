using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.DrawingTranslation.Core;
using StructuralToolbox.DrawingTranslation.Api;
using StructuralToolbox.DrawingTranslation.Diagnostics;
using StructuralToolbox.DrawingTranslation.UI;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.DrawingTranslation.Commands
{
    public sealed class TranslationCommands : IExtensionApplication
    {
        private const int MaximumItems = 500;

        // NETLOAD 时就把“哪一份 DLL 被加载”写进日志，这是判定命令
        // 实际执行了哪份程序集的可靠证据。
        public void Initialize()
        {
            TranslationDiagnostics.Log("模块 NETLOAD：" +
                DescribeThisAssembly() + "；" +
                TranslationDiagnostics.DescribeLoadedModule().
                    Replace("\n", " "));
            ReportStartupWarning();
        }

        // NETLOAD 时就把“这次加载不会生效”说清楚：AutoCAD 不会替换已经
        // 注册的命令，用户看到命令行或弹窗才知道要完全退出 AutoCAD。
        private static void ReportStartupWarning()
        {
            string warning = TranslationDiagnostics.StartupWarning();
            if (!String.IsNullOrEmpty(warning))
            {
                TranslationDiagnostics.Log("模块 NETLOAD 警告：" +
                    warning.Replace("\n", " | "));
                WriteMessage(warning);
                ShowAlert(warning);
                return;
            }
            // 从 NetLoad 对话框的历史记录里点到旧文件时会走到这里：新版本
            // 已经在磁盘上，本次加载的却是旧的，必须当场说清楚。
            string stale = TranslationDiagnostics.StaleDeliveryWarning();
            if (String.IsNullOrEmpty(stale))
                return;
            TranslationDiagnostics.Log("模块 NETLOAD 提示：" +
                stale.Replace("\n", " | "));
            WriteMessage(stale);
            ShowAlert(stale);
        }

        // 会话里已经加载了更新的模块，或者磁盘上已经有更新版本时，都不能
        // 继续执行：AutoCAD 不会替换已注册的命令，继续跑只会写出旧结果。
        private static string BlockingWarning()
        {
            string loaded = TranslationDiagnostics.StaleVersionWarning();
            if (!String.IsNullOrEmpty(loaded))
                return loaded;
            return TranslationDiagnostics.StaleDeliveryWarning();
        }

        private static void WriteMessage(string message)
        {
            if (String.IsNullOrEmpty(message))
                return;
            try
            {
                Document document =
                    AcApplication.DocumentManager.MdiActiveDocument;
                Editor editor = document == null ? null : document.Editor;
                if (editor == null)
                    return;
                editor.WriteMessage("\n" + message + "\n");
            }
            catch (System.Exception)
            {
            }
        }

        // 命令行窗口没有主窗口，弹模态对话框会把脚本卡死；只有存在主窗口
        // 时才弹提示，其余情况靠命令行和日志。
        private static bool CanShowAlert()
        {
            try
            {
                string process = System.Diagnostics.Process.
                    GetCurrentProcess().ProcessName;
                return !process.StartsWith("accoreconsole",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static void ShowAlert(string message)
        {
            if (String.IsNullOrEmpty(message) || !CanShowAlert())
                return;
            try { AcApplication.ShowAlertDialog(message); }
            catch (System.Exception) { }
        }

        public void Terminate()
        {
            TranslationDiagnostics.Log("模块卸载：" + DescribeThisAssembly());
        }

        [CommandMethod("STPVTRANSLATE", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void TranslateSelectedText()
        {
            RunTranslation(false);
        }

        [CommandMethod("STPVTRANSLATECONFIRM", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void TranslateSelectedTextWithConfirmation()
        {
            RunTranslation(true);
        }

        [CommandMethod("STPVTRANSLATESETTINGS", CommandFlags.Modal)]
        public void OpenSettings()
        {
            using (TranslationApiSettingsDialog dialog =
                new TranslationApiSettingsDialog(
                    TranslationApiSettingsStore.ForCurrentUser()))
                Autodesk.AutoCAD.ApplicationServices.Application.
                    ShowModalDialog(dialog);
        }

        [CommandMethod("STPVTRANSLATEINFO", CommandFlags.Modal)]
        public void ReportStatus()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            Editor editor = document == null ? null : document.Editor;
            if (editor == null)
                return;
            string version = "（未知）";
            string location = "（未知）";
            string modified = "（未知）";
            try
            {
                System.Reflection.Assembly assembly =
                    typeof(TranslationCommands).Assembly;
                version = assembly.GetName().Version.ToString();
                location = assembly.Location;
                if (!String.IsNullOrEmpty(location) &&
                    System.IO.File.Exists(location))
                    modified = System.IO.File.GetLastWriteTime(location).
                        ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (System.Exception) { }
            string report = String.Format(
                "\n图纸文字翻译模块版本 {0}\nDLL：{1}\nDLL 修改时间：{2}",
                version, location, modified);
            try
            {
                TranslationApiSettings settings = TranslationApiSettingsStore.
                    ForCurrentUser().Load();
                string effective;
                try { effective = settings.ChatCompletionsUri().AbsoluteUri; }
                catch (InvalidOperationException) { effective = "（尚未有效配置）"; }
                report += String.Format(
                    "\n接口地址：{0}\n实际请求：{1}\n模型：{2}\n目标语言：{3}" +
                    "\n网络连接：{4}\n密钥：{5}",
                    Text(settings.Endpoint), effective, Text(settings.Model),
                    Text(settings.TargetLanguage), ProxyName(settings),
                    String.IsNullOrEmpty(settings.ApiKey) ? "未配置" : "已配置");
            }
            catch (System.Exception exception)
            {
                report += "\n读取翻译 API 设置失败：" + exception.Message;
            }
            report += "\n模块加载：" + TranslationDiagnostics.DescribeLoadedModule();
            string stale = BlockingWarning();
            if (!String.IsNullOrEmpty(stale))
                report += "\n" + stale;
            report += "\n日志：" + TranslationDiagnostics.LogPath();
            TranslationDiagnostics.Log("STPVTRANSLATEINFO " +
                report.Replace("\n", " | "));
            editor.WriteMessage(report);
            try { AcApplication.ShowAlertDialog(report); }
            catch (System.Exception) { }
        }

        // 不选对象、不改图纸的一条自检命令：把两条示例文字发给当前配置的
        // 接口，把连通性、模型和返回内容直接打在命令行上。命令行窗口
        // （accoreconsole）也能执行，交付前可以脚本化复验。
        [CommandMethod("STPVTRANSLATETEST", CommandFlags.Modal)]
        public void RunApiSelfTest()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            Editor editor = document == null ? null : document.Editor;
            if (editor == null)
                return;
            string staleWarning = BlockingWarning();
            if (!String.IsNullOrEmpty(staleWarning))
            {
                TranslationDiagnostics.Log("STPVTRANSLATETEST 中止：" +
                    staleWarning.Replace("\n", " | "));
                WriteMessage(staleWarning);
                ShowAlert(staleWarning);
                return;
            }
            TranslationApiSettings settings;
            try
            {
                settings = TranslationApiSettingsStore.ForCurrentUser().Load();
                settings.Validate();
            }
            catch (System.Exception exception)
            {
                string problem = "\n翻译接口自检：配置不可用 —— " + exception.Message +
                    "\n请执行 STPVTRANSLATESETTINGS 填写接口地址、模型和密钥后重试。" +
                    "\n图纸未做任何修改。";
                TranslationDiagnostics.Log("STPV_TRANSLATE_SELFTEST_FAIL 配置：" +
                    exception.Message);
                editor.WriteMessage(problem);
                return;
            }
            string effective;
            try { effective = settings.ChatCompletionsUri().AbsoluteUri; }
            catch (InvalidOperationException) { effective = "（无效配置）"; }
            List<string> samples = new List<string>();
            samples.Add("北立面支架");
            samples.Add("屋面组件数量");
            editor.WriteMessage("\n正在自检翻译接口（{0} 条示例文字，不修改图纸）...",
                samples.Count);
            TranslationDiagnostics.Log(String.Format(
                "STPVTRANSLATETEST 开始；{0}；接口 {1}；模型 {2}；目标语言 {3}；网络 {4}",
                DescribeThisAssembly(), effective, settings.Model,
                settings.TargetLanguage, ProxyName(settings)));
            System.Diagnostics.Stopwatch watch =
                System.Diagnostics.Stopwatch.StartNew();
            try
            {
                IList<string> translated;
                using (OpenAiCompatibleTranslator translator =
                    OpenAiCompatibleTranslator.Create(settings,
                        TimeSpan.FromSeconds(120)))
                {
                    translated = translator.TranslateAsync(settings, samples,
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                watch.Stop();
                if (translated == null || translated.Count != samples.Count)
                    throw new InvalidOperationException(
                        "接口返回的条目数量与请求不一致。");
                List<string> lines = new List<string>();
                lines.Add(String.Empty);
                lines.Add("翻译接口自检通过（模块 " +
                    TranslationDiagnostics.VersionOf(
                        typeof(TranslationCommands).Assembly) + "）");
                lines.Add("接口：" + effective);
                lines.Add("模型：" + Text(settings.Model) + "　目标语言：" +
                    Text(settings.TargetLanguage) + "　网络：" +
                    ProxyName(settings));
                for (int index = 0; index < samples.Count; index++)
                {
                    lines.Add(String.Format("示例：{0} → {1}", samples[index],
                        Text(translated[index])));
                }
                lines.Add(String.Format("用时：{0:0.0} 秒",
                    watch.Elapsed.TotalSeconds));
                lines.Add("图纸未做任何修改。");
                editor.WriteMessage(String.Join("\n", lines.ToArray()));
                TranslationDiagnostics.Log(String.Format(
                    "STPV_TRANSLATE_SELFTEST_PASS samples={0} first={1} 用时 {2:0.0} 秒",
                    samples.Count, Text(translated[0]),
                    watch.Elapsed.TotalSeconds));
            }
            catch (System.Exception exception)
            {
                watch.Stop();
                List<string> lines = new List<string>();
                lines.Add(String.Empty);
                lines.Add("翻译接口自检失败：" + exception.Message);
                lines.Add("接口：" + effective);
                lines.Add("模型：" + Text(settings.Model) + "　网络：" +
                    ProxyName(settings));
                lines.Add("图纸未做任何修改。");
                lines.Add("可执行 STPVTRANSLATEINFO 查看版本号，或 " +
                    "STPVTRANSLATELOG 打开日志。");
                editor.WriteMessage(String.Join("\n", lines.ToArray()));
                TranslationDiagnostics.Log("STPV_TRANSLATE_SELFTEST_FAIL " +
                    exception.GetType().Name + "：" + exception.Message);
            }
        }

        private static string DescribeThisAssembly()
        {
            System.Reflection.Assembly assembly =
                typeof(TranslationCommands).Assembly;
            return "版本 " + TranslationDiagnostics.VersionOf(assembly) +
                "：" + TranslationDiagnostics.LocationOf(assembly);
        }

        [CommandMethod("STPVTRANSLATELOG", CommandFlags.Modal)]
        public void OpenDiagnosticLog()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            Editor editor = document == null ? null : document.Editor;
            string path = TranslationDiagnostics.LogPath();
            try
            {
                TranslationDiagnostics.Log("STPVTRANSLATELOG 打开日志；" +
                    DescribeThisAssembly());
                if (!System.IO.File.Exists(path))
                    System.IO.File.WriteAllText(path, String.Empty,
                        System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start("notepad.exe",
                    "\"" + path + "\"");
                if (editor != null)
                    editor.WriteMessage("\n翻译日志：" + path);
            }
            catch (System.Exception exception)
            {
                if (editor != null)
                    editor.WriteMessage("\n打开翻译日志失败：" +
                        exception.Message + "\n日志文件：" + path);
            }
        }

        private static string Text(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? "（未填写）" : value.Trim();
        }

        private static string ProxyName(TranslationApiSettings settings)
        {
            if (settings.ProxyMode == TranslationProxyMode.System)
                return "跟随系统代理";
            if (settings.ProxyMode == TranslationProxyMode.Custom)
                return "自定义代理 " + settings.ProxyAddress;
            return "直连";
        }

        private static void RunTranslation(bool confirmBeforeReplace)
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            string commandName = confirmBeforeReplace
                ? "STPVTRANSLATECONFIRM" : "STPVTRANSLATE";
            TranslationDiagnostics.Log(commandName + " 开始；" +
                DescribeThisAssembly() + "；" +
                TranslationDiagnostics.DescribeLoadedModule() + "；图形 " +
                document.Name);
            string staleWarning = BlockingWarning();
            if (!String.IsNullOrEmpty(staleWarning))
            {
                TranslationDiagnostics.Log(commandName + " 中止：" +
                    staleWarning.Replace("\n", " | "));
                WriteMessage(staleWarning);
                ShowAlert(staleWarning);
                return;
            }
            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = "\n选择要翻译的文字或表格: ";
            PromptSelectionResult selection = editor.GetSelection(options);
            if (selection.Status != PromptStatus.OK)
                return;

            try
            {
                TranslationApiSettingsStore store =
                    TranslationApiSettingsStore.ForCurrentUser();
                TranslationApiSettings settings = store.Load();
                try { settings.Validate(); }
                catch (InvalidOperationException)
                {
                    using (TranslationApiSettingsDialog setup =
                        new TranslationApiSettingsDialog(store))
                    {
                        if (Autodesk.AutoCAD.ApplicationServices.Application.
                            ShowModalDialog(setup) != DialogResult.OK)
                            return;
                    }
                    settings = store.Load();
                }
                string effectiveUri;
                try { effectiveUri = settings.ChatCompletionsUri().AbsoluteUri; }
                catch (InvalidOperationException) { effectiveUri = "（无效配置）"; }
                TranslationDiagnostics.Log("配置：接口 " + effectiveUri +
                    "；模型 " + settings.Model + "；目标语言 " +
                    settings.TargetLanguage + "；本次写入 " +
                    (confirmBeforeReplace ? "确认后替换" : "直接替换") +
                    "；网络 " +
                    ProxyName(settings) + "；密钥 " +
                    (String.IsNullOrEmpty(settings.ApiKey) ? "未配置" : "已配置"));
                int skipped;
                IDictionary<string, int> skippedReasons;
                IList<TranslationTarget> targets = ReadTargets(document.Database,
                    selection.Value.GetObjectIds(), out skipped,
                    out skippedReasons);
                if (targets.Count == 0)
                {
                    editor.WriteMessage("\n所选对象中没有可编辑的顶层文字。");
                    TranslationDiagnostics.Log("所选对象里没有可翻译文字，图纸未修改。");
                    return;
                }
                TranslationDiagnostics.Log(String.Format("选中 {0} 项文字，跳过 {1} 项。",
                    targets.Count, skipped));
                string skipSummary = FormatSkipReasons(skippedReasons);
                if (skipSummary.Length > 0)
                    editor.WriteMessage("\n跳过摘要：" + skipSummary);
                if (!confirmBeforeReplace)
                {
                    List<string> source = new List<string>();
                    foreach (TranslationTarget target in targets)
                        source.Add(target.Item.TranslatableContent);
                    editor.WriteMessage("\n正在翻译并直接替换 {0} 项文字...",
                        source.Count);
                    TranslationDiagnostics.Log(String.Format(
                        "直接替换：请求翻译 {0} 条。", source.Count));
                    IList<string> translated;
                    using (OpenAiCompatibleTranslator translator =
                        OpenAiCompatibleTranslator.Create(settings,
                            TimeSpan.FromSeconds(120)))
                    {
                        translated = translator.TranslateAsync(settings,
                            source, CancellationToken.None).
                            GetAwaiter().GetResult();
                    }
                    List<TranslationEdit> edits = BuildValidatedEdits(targets,
                        translated);
                    if (edits.Count == 0)
                    {
                        editor.WriteMessage("\n翻译结果与原文相同，未修改图纸。");
                        TranslationDiagnostics.Log(
                            "直接替换结果与原文相同，未修改图纸。");
                        return;
                    }
                    int staleDirect;
                    int replacedDirect = ReplaceEdits(document.Database,
                        edits, out staleDirect);
                    editor.Regen();
                    editor.WriteMessage(
                        "\n已直接替换 {0} 项，因原文变化跳过 {1} 项。（{2}）",
                        replacedDirect, staleDirect,
                        TranslationDiagnostics.ModuleStamp());
                    TranslationDiagnostics.Log(String.Format(
                        "直接替换完成：写入 {0} 项，跳过 {1} 项。",
                        replacedDirect, staleDirect));
                    return;
                }
                using (TranslationReviewDialog dialog =
                    new TranslationReviewDialog(targets, skipped))
                {
                    if (Autodesk.AutoCAD.ApplicationServices.Application.
                        ShowModalDialog(dialog) != DialogResult.OK)
                    {
                        TranslationDiagnostics.Log("用户在确认窗取消写入，图纸未修改。");
                        return;
                    }
                    int stale;
                    int written = ReplaceEdits(document.Database,
                        dialog.Edits, out stale);
                    editor.Regen();
                    editor.WriteMessage(
                        "\n确认后已替换 {0} 项，因原文变化跳过 {1} 项。（{2}）",
                        written, stale,
                        TranslationDiagnostics.ModuleStamp());
                    TranslationDiagnostics.Log(String.Format(
                        "确认后替换完成：写入 {0} 项，跳过 {1} 项。",
                        written, stale));
                }
            }
            catch (System.Exception exception)
            {
                TranslationDiagnostics.Log("失败：" + DescribeThisAssembly() +
                    "；" + exception);
                string failure = "翻译或写入失败，图纸未修改（" +
                    TranslationDiagnostics.ModuleStamp() + "）：\n" +
                    (exception is HttpRequestException
                        ? "无法连接翻译服务，请检查网络、代理和接口地址。"
                        : exception.Message) +
                    TranslationDiagnostics.DuplicateWarning();
                WriteMessage(failure);
                ShowAlert(failure);
            }
        }

        private static List<TranslationEdit> BuildValidatedEdits(
            IList<TranslationTarget> targets, IList<string> translated)
        {
            if (translated == null || translated.Count != targets.Count)
                throw new InvalidOperationException("翻译结果数量与所选文字不一致。");
            List<TranslationEdit> edits = new List<TranslationEdit>();
            for (int index = 0; index < targets.Count; index++)
            {
                string restored;
                string error;
                if (!targets[index].Item.Protection.TryRestore(
                    translated[index], out restored, out error))
                {
                    throw new InvalidOperationException(String.Format(
                        "第 {0} 项译文无效：{1}", index + 1, error));
                }
                if (!String.Equals(restored, targets[index].Item.RawContent,
                    StringComparison.Ordinal))
                    edits.Add(new TranslationEdit(targets[index], restored));
            }
            return edits;
        }

        internal static IList<TranslationTarget> ReadTargets(Database database,
            ObjectId[] selected, out int skipped)
        {
            IDictionary<string, int> ignored;
            return ReadTargets(database, selected, out skipped, out ignored);
        }

        internal static IList<TranslationTarget> ReadTargets(Database database,
            ObjectId[] selected, out int skipped,
            out IDictionary<string, int> skippedReasons)
        {
            List<TranslationTarget> targets = new List<TranslationTarget>();
            HashSet<ObjectId> seen = new HashSet<ObjectId>();
            Dictionary<string, int> reasons = new Dictionary<string, int>();
            skippedReasons = reasons;
            skipped = 0;
            ObjectId modelSpace = SymbolUtilityServices.
                GetBlockModelSpaceId(database);
            using (Transaction transaction = database.TransactionManager.
                StartOpenCloseTransaction())
            {
                foreach (ObjectId id in selected)
                {
                    if (!seen.Add(id))
                        continue;
                    Entity entity = transaction.GetObject(id,
                        OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                    {
                        skipped++;
                        AddSkip(reasons, "无法读取对象");
                        continue;
                    }
                    AttributeReference attribute = entity as AttributeReference;
                    if (attribute != null)
                    {
                        BlockReference owner = transaction.GetObject(
                            attribute.OwnerId, OpenMode.ForRead, false) as
                            BlockReference;
                        BlockTableRecord block = owner == null ? null :
                            transaction.GetObject(owner.BlockTableRecord,
                                OpenMode.ForRead, false) as BlockTableRecord;
                        if (owner == null || block == null ||
                            block.IsFromExternalReference || attribute.IsConstant)
                        {
                            skipped++;
                            AddSkip(reasons, block != null &&
                                block.IsFromExternalReference
                                ? "外部参照块属性" : "常量块属性");
                            continue;
                        }
                        AddText(targets, id, -1, -1,
                            entity.Handle.ToString(), DrawingTextKind.AttributeReference,
                            attribute.TextString, attribute.HasFields,
                            ref skipped, reasons);
                        continue;
                    }
                    if (entity.OwnerId != modelSpace)
                    {
                        skipped++;
                        AddSkip(reasons, "非模型空间顶层对象");
                        continue;
                    }
                    DBText dbText = entity as DBText;
                    if (dbText != null)
                    {
                        AddText(targets, id, -1, -1, entity.Handle.ToString(),
                            DrawingTextKind.DbText, dbText.TextString,
                            dbText.HasFields, ref skipped, reasons);
                    }
                    else
                    {
                        MText mText = entity as MText;
                        if (mText != null)
                        {
                            AddText(targets, id, -1, -1,
                                entity.Handle.ToString(), DrawingTextKind.MText,
                                mText.Contents, mText.HasFields, ref skipped,
                                reasons);
                        }
                        else
                        {
                            Table table = entity as Table;
                            if (table != null)
                                AddTableCells(targets, id, table,
                                    ref skipped, reasons);
                            else
                            {
                                skipped++;
                                AddSkip(reasons, SkipReasonFor(entity));
                            }
                        }
                    }
                }
                transaction.Commit();
            }
            return targets;
        }

        private static void AddTableCells(IList<TranslationTarget> targets,
            ObjectId id, Table table, ref int skipped,
            IDictionary<string, int> reasons)
        {
            for (int row = 0; row < table.Rows.Count; row++)
            {
                for (int column = 0; column < table.Columns.Count; column++)
                {
                    Cell cell = table.Cells[row, column];
                    if (!IsPlainCell(cell))
                    {
                        skipped++;
                        AddSkip(reasons, "不可编辑或含公式的表格单元格");
                        continue;
                    }
                    string raw = cell.Contents[0].TextString;
                    AddText(targets, id, row, column,
                        table.Handle.ToString(), DrawingTextKind.TableCell,
                        raw, false, ref skipped, reasons);
                }
            }
        }

        private static void AddText(IList<TranslationTarget> targets,
            ObjectId id, int row, int column, string handle,
            DrawingTextKind kind, string raw, bool hasField,
            ref int skipped, IDictionary<string, int> reasons)
        {
            if (String.IsNullOrWhiteSpace(raw))
            {
                skipped++;
                AddSkip(reasons, "空文字");
                return;
            }
            DrawingTextItem item = DrawingTextItemFactory.Create(handle,
                kind, raw, hasField || raw.Contains("%<"), false);
            if (!item.CanWrite)
            {
                skipped++;
                AddSkip(reasons, item.SkipReason);
                return;
            }
            if (targets.Count >= MaximumItems)
                throw new InvalidOperationException(
                    "一次最多处理 500 项文字，请缩小选择范围。");
            targets.Add(new TranslationTarget(id, row, column, item));
        }

        private static void AddSkip(IDictionary<string, int> reasons,
            string reason)
        {
            if (reasons == null)
                return;
            string key = String.IsNullOrWhiteSpace(reason) ? "未分类" : reason;
            int count;
            reasons.TryGetValue(key, out count);
            reasons[key] = count + 1;
        }

        private static string SkipReasonFor(Entity entity)
        {
            if (entity is BlockReference)
                return "块参照（请直接选择其中的块属性）";
            if (entity is MLeader)
                return "多重引线文字首版跳过";
            if (entity is Dimension)
                return "尺寸标注可能包含真实测量值";
            return entity.GetType().Name + "不是可翻译文字";
        }

        private static string FormatSkipReasons(
            IDictionary<string, int> reasons)
        {
            if (reasons == null || reasons.Count == 0)
                return String.Empty;
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> item in reasons)
                parts.Add(item.Key + " " + item.Value + " 项");
            return String.Join("；", parts.ToArray());
        }

        internal static int ReplaceEdits(Database database,
            IList<TranslationEdit> edits, out int stale)
        {
            stale = 0;
            int replaced = 0;
            ObjectId modelSpace = SymbolUtilityServices.
                GetBlockModelSpaceId(database);
            using (Transaction transaction = database.TransactionManager.
                StartTransaction())
            {
                BlockTableRecord model = (BlockTableRecord)transaction.GetObject(
                    modelSpace, OpenMode.ForWrite);
                foreach (TranslationEdit edit in edits)
                {
                    TranslationTarget target = edit.Target;
                    if (!target.ObjectId.IsValid || target.ObjectId.IsErased ||
                        target.ObjectId.IsEffectivelyErased)
                    {
                        stale++;
                        continue;
                    }
                    Entity entity = transaction.GetObject(target.ObjectId,
                        OpenMode.ForRead, false) as Entity;
                    if (entity == null || !IsWritableOwner(transaction, entity,
                        modelSpace) ||
                        entity.Handle.ToString() != target.Item.Handle ||
                        !String.Equals(ReadCurrent(entity, target),
                            target.Item.RawContent, StringComparison.Ordinal))
                    {
                        stale++;
                        continue;
                    }
                    entity.UpgradeOpen();
                    AttributeReference attribute = entity as AttributeReference;
                    DBText dbText = entity as DBText;
                    MText mText = entity as MText;
                    if (attribute != null)
                        SetAttributeTranslation(attribute, target.Item.RawContent,
                            edit.Value);
                    else if (dbText != null)
                    {
                        MText replacement = CreateWrappedSingleLineText(database,
                            dbText, edit.Value);
                        model.AppendEntity(replacement);
                        transaction.AddNewlyCreatedDBObject(replacement, true);
                        dbText.Erase();
                    }
                    else if (mText != null)
                    {
                        double width = ResolveMTextWidth(mText);
                        mText.Width = width;
                        mText.Contents = ToMTextContents(edit.Value);
                    }
                    else
                    {
                        Table table = (Table)entity;
                        table.Cells[target.Row, target.Column].Contents[0].TextString =
                            edit.Value;
                        table.GenerateLayout();
                    }
                    replaced++;
                }
                transaction.Commit();
            }
            return replaced;
        }

        private static string ReadCurrent(Entity entity,
            TranslationTarget target)
        {
            if (target.Item.Kind == DrawingTextKind.AttributeReference)
            {
                AttributeReference attribute = entity as AttributeReference;
                return attribute == null || attribute.HasFields
                    ? null : attribute.TextString;
            }
            if (target.Item.Kind == DrawingTextKind.DbText)
            {
                DBText text = entity as DBText;
                return text == null || text.HasFields ? null : text.TextString;
            }
            if (target.Item.Kind == DrawingTextKind.MText)
            {
                MText text = entity as MText;
                return text == null || text.HasFields ? null : text.Contents;
            }
            Table table = entity as Table;
            if (table == null || target.Row < 0 || target.Column < 0 ||
                target.Row >= table.Rows.Count ||
                target.Column >= table.Columns.Count)
                return null;
            Cell cell = table.Cells[target.Row, target.Column];
            if (!IsPlainCell(cell))
                return null;
            return cell.Contents[0].TextString;
        }

        private static bool IsWritableOwner(Transaction transaction,
            Entity entity, ObjectId modelSpace)
        {
            if (entity.OwnerId == modelSpace)
                return true;
            AttributeReference attribute = entity as AttributeReference;
            if (attribute == null)
                return false;
            BlockReference owner = transaction.GetObject(attribute.OwnerId,
                OpenMode.ForRead, false) as BlockReference;
            if (owner == null)
                return false;
            BlockTableRecord block = transaction.GetObject(
                owner.BlockTableRecord, OpenMode.ForRead, false) as
                BlockTableRecord;
            return block != null && !block.IsFromExternalReference &&
                !attribute.IsConstant;
        }

        private static bool IsPlainCell(Cell cell)
        {
            return cell.IsMerged != true &&
                cell.IsContentEditable == true &&
                cell.Contents.Count == 1 &&
                cell.Contents[0].ContentTypes == CellContentTypes.Value &&
                !cell.Contents[0].HasFormula &&
                cell.Contents[0].FieldId == ObjectId.Null;
        }

        private static MText CreateWrappedSingleLineText(Database database,
            DBText source, string translated)
        {
            MText result = new MText();
            result.SetDatabaseDefaults(database);
            result.SetPropertiesFrom(source);
            ConfigureWrappedSingleLineText(result, source, translated);
            return result;
        }

        private static void ConfigureWrappedSingleLineText(MText result,
            DBText source, string translated)
        {
            Vector3d right = Vector3d.XAxis.RotateBy(source.Rotation,
                source.Normal).GetNormal();
            Vector3d up = source.Normal.CrossProduct(right).GetNormal();
            double estimated = Math.Max(source.Height * 4.0,
                source.Height * Math.Max(1, source.TextString.Length) *
                Math.Max(0.2, source.WidthFactor) * 0.75);
            double left;
            double top;
            double bottom;
            double width;
            double plane;
            GetProjectedFrame(source, right, up, source.Normal,
                source.Position, estimated, source.Height,
                out left, out top, out bottom, out width, out plane);
            result.Attachment = AttachmentPoint.TopLeft;
            result.Normal = source.Normal;
            result.Direction = right;
            result.TextStyleId = source.TextStyleId;
            result.TextHeight = source.Height;
            result.Width = width;
            result.Contents = ToMTextContents(translated);
            result.Location = PointOnFrame(right, left, up, top,
                source.Normal, plane);
        }

        private static void SetAttributeTranslation(AttributeReference attribute,
            string original, string translated)
        {
            attribute.TextString = translated;
            if (attribute.IsMTextAttribute)
            {
                attribute.UpdateMTextAttribute();
                return;
            }
            double originalLength = Math.Max(1, (original ?? String.Empty).Length);
            double translatedLength = Math.Max(1,
                (translated ?? String.Empty).Length);
            double scale = Math.Min(1.0, originalLength / translatedLength);
            attribute.WidthFactor = Math.Max(0.5,
                Math.Min(attribute.WidthFactor, attribute.WidthFactor * scale));
        }

        private static double ResolveMTextWidth(MText text)
        {
            if (text.Width > text.TextHeight)
                return text.Width;
            double left;
            double top;
            double bottom;
            double width;
            double plane;
            Vector3d right = text.Direction.GetNormal();
            Vector3d up = text.Normal.CrossProduct(right).GetNormal();
            GetProjectedFrame(text, right, up, text.Normal, text.Location,
                Math.Max(text.TextHeight * 4.0, text.ActualWidth),
                text.TextHeight, out left, out top, out bottom, out width,
                out plane);
            return width;
        }

        private static void GetProjectedFrame(Entity entity, Vector3d right,
            Vector3d up, Vector3d normal, Point3d fallback,
            double fallbackWidth, double fallbackHeight, out double left,
            out double top, out double bottom, out double width,
            out double plane)
        {
            left = fallback.GetAsVector().DotProduct(right);
            double fallbackUp = fallback.GetAsVector().DotProduct(up);
            top = fallbackUp + Math.Max(fallbackHeight, 1.0);
            bottom = fallbackUp;
            width = Math.Max(fallbackWidth, Math.Max(fallbackHeight, 1.0) * 4.0);
            plane = fallback.GetAsVector().DotProduct(normal);
            try
            {
                Extents3d extents = entity.GeometricExtents;
                double minRight = Double.PositiveInfinity;
                double maxRight = Double.NegativeInfinity;
                double minUp = Double.PositiveInfinity;
                double maxUp = Double.NegativeInfinity;
                foreach (Point3d corner in ExtentCorners(extents))
                {
                    Vector3d vector = corner.GetAsVector();
                    double alongRight = vector.DotProduct(right);
                    double alongUp = vector.DotProduct(up);
                    minRight = Math.Min(minRight, alongRight);
                    maxRight = Math.Max(maxRight, alongRight);
                    minUp = Math.Min(minUp, alongUp);
                    maxUp = Math.Max(maxUp, alongUp);
                }
                if (!Double.IsInfinity(minRight) && maxRight > minRight)
                {
                    left = minRight;
                    width = Math.Max(maxRight - minRight,
                        Math.Max(fallbackHeight, 1.0) * 4.0);
                    top = maxUp;
                    bottom = minUp;
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { }
        }

        private static IEnumerable<Point3d> ExtentCorners(Extents3d extents)
        {
            Point3d min = extents.MinPoint;
            Point3d max = extents.MaxPoint;
            yield return new Point3d(min.X, min.Y, min.Z);
            yield return new Point3d(min.X, min.Y, max.Z);
            yield return new Point3d(min.X, max.Y, min.Z);
            yield return new Point3d(min.X, max.Y, max.Z);
            yield return new Point3d(max.X, min.Y, min.Z);
            yield return new Point3d(max.X, min.Y, max.Z);
            yield return new Point3d(max.X, max.Y, min.Z);
            yield return new Point3d(max.X, max.Y, max.Z);
        }

        private static Point3d PointOnFrame(Vector3d right, double x,
            Vector3d up, double y, Vector3d normal, double z)
        {
            return Point3d.Origin + right * x + up * y + normal * z;
        }

        private static string ToMTextContents(string value)
        {
            return (value ?? String.Empty).Replace("\r\n", "\n").
                Replace("\r", "\n").Replace("\n", "\\P");
        }
    }
}
