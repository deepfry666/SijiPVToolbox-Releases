using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StructuralToolbox.Ballast.UI;
using StructuralToolbox.Update.UI;
using StructuralToolbox.UI;

internal static class ToolboxUiLayoutTests
{
    private static int _passed;

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            VerifyPaletteSizeIsolationSourceContract();
            VerifyReleaseIdentitySourceContract();
            VerifyHomeAtScale(1.0f, "100 percent home");
            VerifyHomeAtScale(1.25f, "125 percent home");
            VerifyHomeAtScale(1.5f, "150 percent home");
            VerifyHomeAtScale(2.0f, "200 percent home");
            VerifyRailPageAtScale(1.0f, "100 percent rail page");
            VerifyRailPageAtScale(1.25f, "125 percent rail page");
            VerifyRailPageAtScale(1.5f, "150 percent rail page");
            VerifyBallastPageAtScale(1.0f, "100 percent ballast page");
            VerifyBallastPageAtScale(1.25f, "125 percent ballast page");
            VerifyBallastPageAtScale(1.5f, "150 percent ballast page");
            VerifyBallastPageAtScale(2.0f, "200 percent ballast page");
            VerifyUpdatePageAtScale(1.0f, "100 percent update page");
            VerifyUpdatePageAtScale(1.25f, "125 percent update page");
            VerifyUpdatePageAtScale(1.5f, "150 percent update page");
            VerifyUpdatePageAtScale(2.0f, "200 percent update page");
            VerifyCompactPageScrolls(typeof(ToolboxControl), "compact rail page");
            VerifyCompactBallastPageScrolls();
            VerifyCompactBallastFooter();
            VerifyPageSwitchPreservesHostSize();
            VerifyNestedBackNavigation();
            VerifyShellWithoutFeatureModules();
            VerifyBallastWorkflowSourceContract();

            Console.WriteLine("PASS {0} toolbox UI layout tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL toolbox UI layout tests: {0}",
                exception.Message);
            return 1;
        }
    }

    private static void VerifyPaletteSizeIsolationSourceContract()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
        string palettePath = Path.Combine(projectRoot, "src",
            "StructuralToolbox", "UI", "ToolboxPalette.cs");
        Require(File.Exists(palettePath),
            "palette source is unavailable for the saved-size regression check.");

        string source = File.ReadAllText(palettePath).Replace("\r\n", "\n");
        Require(source.Contains("private const int PaletteLayoutVersion = 6;") &&
            source.Contains("ApplyLayoutMigration();"),
            "v1.0.8 does not preserve the v1.0.0 palette migration.");
        Require(!source.Contains("PreferredPaletteSize") &&
            !source.Contains("ModulePageShown"),
            "a module can still resize the shared palette while navigating.");

        string applyBody = MethodBody(source,
            "private static void ApplyLayoutMigration",
            "private static void ScheduleLayoutCorrection");
        Require(applyBody.Contains("_layoutMigrationRequired") &&
            applyBody.Contains("ScheduleLayoutCorrection();"),
            "layout reset is not limited to the one-time version migration.");

        string scheduleBody = MethodBody(source,
            "private static void ScheduleLayoutCorrection()",
            "private static void OnLayoutCorrectionIdle");
        int immediateClamp = scheduleBody.IndexOf(
            "TryApplyMigrationSize(palette, _pendingPaletteSize);",
            StringComparison.Ordinal);
        int idleHook = scheduleBody.IndexOf(
            "AcApplication.Idle += OnLayoutCorrectionIdle;",
            StringComparison.Ordinal);
        Require(immediateClamp >= 0 && idleHook > immediateClamp,
            "migration size is not applied before Idle retries are attached.");
        Require(scheduleBody.Contains(
                "_pendingPaletteSize = HomePaletteSize;") &&
            scheduleBody.Contains(
                "_remainingLayoutCorrectionPasses = LayoutCorrectionPasses;"),
            "migration does not restore the compact home size deterministically.");

        string clampBody = MethodBody(source,
            "private static bool TryApplyMigrationSize",
            "private static void CancelLayoutCorrection");
        Require(clampBody.Contains(
                "requestedSize.Width, requestedSize.Height") &&
            !clampBody.Contains("Math.Max"),
            "one-time migration cannot shrink the stale expanded palette.");
        _passed++;
    }

    private static string MethodBody(string source, string startMarker,
        string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, Math.Max(0, start),
            StringComparison.Ordinal);
        Require(start >= 0 && end > start,
            "palette source contract markers are missing: " + startMarker);
        return source.Substring(start, end - start);
    }

    private static void VerifyReleaseIdentitySourceContract()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
        string pluginEntryPath = Path.Combine(projectRoot, "src",
            "StructuralToolbox", "PluginEntry.cs");
        Require(File.Exists(pluginEntryPath),
            "plugin startup source is unavailable for release classification check.");

        string source = File.ReadAllText(pluginEntryPath);
        Require(source.Contains("四季光伏工具箱 1.0.8 已加载。"),
            "AutoCAD startup message does not identify v1.0.8.");
        Require(!source.Contains("TEST_UNSIGNED") &&
            !source.Contains("未签名测试版"),
            "AutoCAD startup message exposes internal signature classification.");

        string aboutPath = Path.Combine(projectRoot, "src",
            "StructuralToolbox", "Commands", "ToolboxCommands.cs");
        string aboutSource = File.ReadAllText(aboutPath);
        Require(aboutSource.Contains("四季光伏工具箱 1.0.8") &&
            !aboutSource.Contains("TEST_UNSIGNED") &&
            !aboutSource.Contains("未签名测试版"),
            "About text exposes stale version or internal signature classification.");
        _passed++;
    }

    private static void VerifyHomeAtScale(float scale, string context)
    {
        Size baseSize = new Size(380, 580);
        using (Form host = CreateHost(baseSize, scale))
        using (ToolboxShellControl shell = CreateShell(host, scale))
        {
            Button railButton = FindNamed<Button>(shell, "OpenRailTool");
            Button ballastButton = FindNamed<Button>(shell, "OpenBallastTool");
            Button updateButton = FindNamed<Button>(shell, "OpenOnlineUpdate");
            Label version = FindNamed<Label>(shell, "ToolboxVersion");

            Require(railButton.Visible,
                context + ": rail tool entry is hidden.");
            Require(ballastButton.Visible && ballastButton.Text == "自动布置配重",
                context + ": ballast tool entry is missing or mislabeled.");
            Require(updateButton.Visible && updateButton.Text == "检查软件更新",
                context + ": update entry is missing or mislabeled.");
            Require(CountNamed(shell, "OpenSupportDetailTool") == 0,
                context + ": removed support detail entry is still visible.");
            Require(CountNamed(shell, "OpenAnnotationTool") == 0,
                context + ": annotation is incorrectly exposed as a home tool.");
            Require(version.Visible && version.Text == "版本 1.0.8",
                context + ": visible version is missing or stale.");
            Require(CountNamed(shell, "ReleaseClassification") == 0,
                context + ": internal signature classification is still visible.");
            RequireFits(railButton, context + ": rail entry");
            RequireFits(ballastButton, context + ": ballast entry");
            RequireFits(updateButton, context + ": update entry");
        }

        _passed++;
    }

    private static void VerifyRailPageAtScale(float scale, string context)
    {
        Size baseSize = new Size(420, 740);
        using (Form host = CreateHost(baseSize, scale))
        using (ToolboxShellControl shell = CreateShell(host, scale))
        {
            FindNamed<Button>(shell, "OpenRailTool").PerformClick();
            PerformLayout(host, shell);

            ToolboxControl page = FindVisible<ToolboxControl>(shell);
            Button drawButton = FindButton(page, "自动画导轨");
            Button resetButton = FindButton(page, "恢复默认参数");
            Panel scrollHost = FindScrollHost(page);

            VerifyFixedFooter(page, drawButton, resetButton, scrollHost, context);
            Require(CountNamed(page, "OpenAnnotationSettings") == 0 &&
                CountNamed(page, "LinkAnnotationAfterDrawing") == 0 &&
                CountNamed(page, "AnnotationLayerName") == 0,
                context + ": annotation controls remain on the rail page.");
            VerifyComboTextFits(FindNamed<ComboBox>(page,
                "RailColorApplication"), context + ": rail color application");
        }

        _passed++;
    }

    private static void VerifyBallastPageAtScale(float scale, string context)
    {
        Size baseSize = new Size(440, 780);
        using (Form host = CreateHost(baseSize, scale))
        using (ToolboxShellControl shell = CreateShell(host, scale))
        {
            FindNamed<Button>(shell, "OpenBallastTool").PerformClick();
            PerformLayout(host, shell);

            BallastToolboxControl page = FindVisible<BallastToolboxControl>(shell);
            Button primary = FindNamed<Button>(page, "BallastPrimaryAction");
            Button reset = FindNamed<Button>(page, "ResetBallastDefaults");
            Button recognizePier = FindNamed<Button>(page,
                "BallastPierRecognition");
            Panel scrollHost = FindScrollHost(page);
            Require(primary.Text == "识别组件阵列",
                context + ": initial action is not array detection.");
            VerifyFixedFooter(page, primary, reset, scrollHost, context);
            Require(recognizePier.Text == "点选识别单墩",
                context + ": single-pier recognition action is unavailable.");
            RequireFits(recognizePier,
                context + ": single-pier recognition action");

            Dictionary<int, int> counts = new Dictionary<int, int>();
            counts[1] = 17;
            counts[2] = 154;
            page.ApplyDetection(325, 171, new int[] { 1, 2 }, counts,
                "测试识别结果");
            PerformLayout(host, shell);

            Require(primary.Text == "生成配重" && primary.Enabled,
                context + ": generation action is unavailable after detection.");
            TableLayoutPanel rules = FindNamed<TableLayoutPanel>(page,
                "BallastRulesHost");
            Require(rules.Controls.Count == 2,
                context + ": rules are not grouped by the two detected row counts.");
            Require(CountVisibleTextBoxes(rules, "圆墩直径 (mm)") == 2,
                context + ": circle dimensions are not shown once per row type.");
            Require(CountVisibleTextBoxes(rules, "方墩尺寸 (mm)") == 0,
                context + ": rectangle dimensions are visible for circle rules.");
            Control firstRule = rules.GetControlFromPosition(0, 0);
            BallastPlacementDiagram diagram =
                FindNamed<BallastPlacementDiagram>(firstRule,
                    "BallastPlacementDiagram");
            Require(!diagram.Visible,
                context + ": custom placement diagram is visible in symmetric mode.");
            ComboBox placementMode = FindSingle<ComboBox>(firstRule,
                delegate(ComboBox item)
                {
                    return item.Items.Count == 2 &&
                        item.Items[0].ToString() == "默认前后对称" &&
                        item.Items[1].ToString() == "自定义前侧";
                });
            placementMode.SelectedIndex = 1;
            PerformLayout(host, shell);
            Require(diagram.Visible && diagram.Height >= 140,
                context + ": custom placement diagram is not shown in custom mode.");
            Require(CountVisibleTextBoxes(firstRule,
                    "前侧中心边距 F (mm)") == 1 &&
                CountVisibleTextBoxes(firstRule,
                    "配重中心间距 S (mm)") == 1,
                context + ": custom placement fields do not match diagram labels.");
            ComboBox firstShape = FindSingle<ComboBox>(firstRule,
                delegate(ComboBox item)
                {
                    return item.Items.Count == 2 &&
                        item.Items[0].ToString() == "圆墩";
                });
            firstShape.SelectedIndex = 1;
            PerformLayout(host, shell);
            Require(CountVisibleTextBoxes(rules, "圆墩直径 (mm)") == 1 &&
                CountVisibleTextBoxes(rules, "方墩尺寸 (mm)") == 1,
                context + ": shape selection does not switch its dimension fields.");
            RequireFits(primary, context + ": ballast primary action");
        }
        _passed++;
    }

    private static void VerifyUpdatePageAtScale(float scale, string context)
    {
        Size baseSize = new Size(380, 580);
        using (Form host = CreateHost(baseSize, scale))
        using (ToolboxShellControl shell = CreateShell(host, scale))
        {
            FindNamed<Button>(shell, "OpenOnlineUpdate").PerformClick();
            PerformLayout(host, shell);

            UpdateCheckControl page = FindVisible<UpdateCheckControl>(shell);
            Button check = FindNamed<Button>(page, "CheckUpdateButton");
            TextBox status = FindNamed<TextBox>(page, "UpdateStatus");
            Label description = FindNamed<Label>(page, "UpdateDescription");
            Require(CountNamed(page, "UpdateSourceUrl") == 0 &&
                CountNamed(page, "UpdateSourceRow") == 0 &&
                CountNamed(page, "UpdateSourceHint") == 0,
                context + ": update source controls are still rendered.");
            RequireVisibleInside(page, check, context + ": check action");
            RequireVisibleInside(page, status, context + ": status area");
            RequireFits(check, context + ": check action");
            Require(status.Multiline && status.ScrollBars == ScrollBars.Vertical &&
                status.Dock == DockStyle.Fill,
                context + ": status area does not absorb available height.");
            Require(description.MaximumSize.Width <= page.ClientSize.Width,
                context + ": description still requests a wider palette.");
        }
        _passed++;
    }

    private static void VerifyCompactPageScrolls(Type pageType,
            string context)
    {
        using (Form host = CreateHost(new Size(300, 280), 1.0f))
        using (ToolboxShellControl shell = CreateShell(host, 1.0f))
        {
            FindNamed<Button>(shell, "OpenRailTool").PerformClick();
            PerformLayout(host, shell);

            Control page = FindVisible(shell, pageType);
            Panel scrollHost = FindScrollHost(page);
            Require(scrollHost.VerticalScroll.Visible,
                context + ": vertical scrolling is unavailable.");
            Require(!scrollHost.HorizontalScroll.Visible,
                context + ": unexpected horizontal scrollbar.");
        }

        _passed++;
    }

    private static void VerifyCompactBallastPageScrolls()
    {
        using (Form host = CreateHost(new Size(320, 300), 1.0f))
        using (ToolboxShellControl shell = CreateShell(host, 1.0f))
        {
            FindNamed<Button>(shell, "OpenBallastTool").PerformClick();
            PerformLayout(host, shell);
            BallastToolboxControl page = FindVisible<BallastToolboxControl>(shell);
            Dictionary<int, int> counts = new Dictionary<int, int>();
            counts[1] = 2;
            counts[2] = 3;
            counts[3] = 1;
            page.ApplyDetection(12, 6, new int[] { 1, 2, 3 }, counts, "");
            PerformLayout(host, shell);
            Panel scrollHost = FindScrollHost(page);
            Require(scrollHost.VerticalScroll.Visible,
                "compact ballast page: vertical scrolling is unavailable.");
            Require(!scrollHost.HorizontalScroll.Visible,
                "compact ballast page: unexpected horizontal scrollbar.");
        }
        _passed++;
    }

    private static void VerifyCompactBallastFooter()
    {
        VerifyCompactBallastFooterAtScale(1.0f, "100 percent compact ballast footer");
        VerifyCompactBallastFooterAtScale(1.25f, "125 percent compact ballast footer");
        VerifyCompactBallastFooterAtScale(1.5f, "150 percent compact ballast footer");
        VerifyCompactBallastFooterAtScale(2.0f, "200 percent compact ballast footer");
        _passed++;
    }

    private static void VerifyCompactBallastFooterAtScale(float scale,
        string context)
    {
        using (Form host = CreateHost(new Size(340, 580), scale))
        using (ToolboxShellControl shell = CreateShell(host, scale))
        {
            FindNamed<Button>(shell, "OpenBallastTool").PerformClick();
            PerformLayout(host, shell);
            BallastToolboxControl page = FindVisible<BallastToolboxControl>(shell);
            TableLayoutPanel footer = FindNamed<TableLayoutPanel>(page,
                "BallastActionFooter");
            Button primary = FindNamed<Button>(page, "BallastPrimaryAction");
            Button redetect = FindNamed<Button>(page, "RedetectBallastArrays");
            Button reset = FindNamed<Button>(page, "ResetBallastDefaults");
            Button statistics = FindNamed<Button>(page, "BallastStatistics");
            Button arrayTable = FindNamed<Button>(page, "BallastArrayTable");
            BallastArrayGapDiagram gapDiagram =
                FindNamed<BallastArrayGapDiagram>(page,
                    "BallastArrayGapDiagram");
            RequireVisibleInside(page, gapDiagram,
                context + ": array gap G diagram");
            Require(!arrayTable.Enabled,
                context + ": array table enabled before detection");

            Require(!redetect.Visible &&
                footer.ColumnStyles[1].SizeType == SizeType.Absolute &&
                footer.ColumnStyles[1].Width == 0.0f,
                context + ": hidden redetect action still occupies width.");
            VerifyFooterButtonsFit(page, context + " before detection",
                primary, reset, statistics);

            Dictionary<int, int> counts = new Dictionary<int, int>();
            counts[2] = 4;
            page.ApplyDetection(8, 4, new int[] { 2 }, counts, "");
            PerformLayout(host, shell);

            Require(redetect.Visible,
                context + ": redetect action is hidden after detection.");
            Require(arrayTable.Enabled,
                context + ": array table action is unavailable after detection.");
            RequireFits(arrayTable, context + ": array table action");
            VerifyFooterButtonsFit(page, context + " after detection",
                primary, redetect, reset, statistics);
        }
    }

    private static void VerifyFooterButtonsFit(Control page, string context,
        params Button[] buttons)
    {
        for (int index = 0; index < buttons.Length; index++)
        {
            RequireVisibleInside(page, buttons[index],
                context + ": " + buttons[index].Name);
            RequireFits(buttons[index],
                context + ": " + buttons[index].Name);
        }
    }

    private static void VerifyPageSwitchPreservesHostSize()
    {
        using (Form host = CreateHost(new Size(380, 580), 1.0f))
        using (ToolboxShellControl shell = CreateShell(host, 1.0f))
        {
            Size initialSize = host.ClientSize;
            for (int index = 0; index < 10; index++)
            {
                FindNamed<Button>(shell, "OpenOnlineUpdate").PerformClick();
                PerformLayout(host, shell);
                FindButton(shell, "←").PerformClick();
                PerformLayout(host, shell);
                FindNamed<Button>(shell, "OpenRailTool").PerformClick();
                PerformLayout(host, shell);
                FindButton(shell, "←").PerformClick();
                PerformLayout(host, shell);
            }
            Require(host.ClientSize == initialSize,
                "module navigation changed the user-controlled host size.");
        }
        _passed++;
    }

        private static void VerifyNestedBackNavigation()
        {
            using (Form host = CreateHost(new Size(420, 660), 1.0f))
            using (ToolboxShellControl shell = CreateShell(host, 1.0f))
            {
                FindNamed<Button>(shell, "OpenRailTool").PerformClick();
                PerformLayout(host, shell);

                FindButton(shell, "←").PerformClick();
                PerformLayout(host, shell);
                Require(FindVisible<ToolboxHomeControl>(shell) != null,
                    "rail page back button did not return to home.");

                FindNamed<Button>(shell, "OpenBallastTool").PerformClick();
                PerformLayout(host, shell);
                FindButton(shell, "←").PerformClick();
                PerformLayout(host, shell);
                Require(FindVisible<ToolboxHomeControl>(shell) != null,
                    "ballast page back button did not return to home.");
            }
            _passed++;
        }

    private static void VerifyShellWithoutFeatureModules()
    {
        using (Form host = CreateHost(new Size(380, 580), 1.0f))
        using (ToolboxShellControl shell = new ToolboxShellControl(
            new List<StructuralToolbox.Contracts.IToolboxModule>()))
        {
            host.Controls.Add(shell);
            host.Show();
            PerformLayout(host, shell);
            Require(FindVisible<ToolboxHomeControl>(shell) != null,
                "shell cannot open when feature modules are absent.");
            Require(CountNamed(shell, "OpenRailTool") == 0 &&
                CountNamed(shell, "OpenBallastTool") == 0,
                "missing feature modules leave broken home buttons.");
        }
        _passed++;
    }

    private static void VerifyBallastWorkflowSourceContract()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
        string commandsPath = Path.Combine(projectRoot, "src",
            "StructuralToolbox.Ballast", "Commands", "BallastCommands.cs");
        string source = File.ReadAllText(commandsPath).Replace("\r\n", "\n");
        string placeBody = MethodBody(source,
            "public void PlaceBallasts()",
            "[CommandMethod(\"STPVBALLASTDETECT\"");
        Require(placeBody.Contains("BallastWorkflowBridge.Show();"),
            "STPVBALLAST does not open the integrated palette page.");
        Require(!source.Contains("new BallastRowRulesDialog") &&
            !source.Contains("rowDialog.ShowDialog()"),
            "main ballast workflow still opens the detached row-rules dialog.");
        Require(source.Contains("STPVBALLASTDETECT") &&
            source.Contains("STPVBALLASTGENERATE"),
            "palette workflow is not split into detection and generation stages.");
        Require(source.Contains("STPVBALLASTPIERRECOGNIZE") &&
            source.Contains("ReadTopLevelSinglePier") &&
            !source.Contains("统计对象 [工具生成(T)/选定范围全部(A)]"),
            "ballast statistics do not use the single-pier recognition workflow.");
        Require(!source.Contains(
                "ReferenceEquals(_workflow.Database, document.Database)"),
            "ballast workflow still compares transient Database wrappers by reference.");
        Require(source.Contains("DocumentIdentity = GetDocumentIdentity(document)") &&
            source.Contains("StringComparison.OrdinalIgnoreCase"),
            "ballast workflow does not preserve detection results by stable document identity.");

        string manifestPath = Path.Combine(projectRoot, "bundle",
            "ZhStructureToolbox.bundle", "PackageContents.xml");
        string manifest = File.ReadAllText(manifestPath);
        Require(manifest.Contains("Global=\"STPVBALLAST\"") &&
            manifest.Contains("Global=\"STPVBALLASTDETECT\"") &&
            manifest.Contains("Global=\"STPVBALLASTGENERATE\""),
            "bundle manifest does not declare the integrated ballast workflow commands.");
        _passed++;
    }

    private static int CountVisibleTextBoxes(Control root, string labelText)
    {
        List<Label> labels = new List<Label>();
        Collect(root, delegate(Label item)
        {
            return item.Visible && item.Text == labelText;
        }, labels);
        return labels.Count;
    }

    private static Form CreateHost(Size baseSize, float scale)
    {
        Form host = new Form();
        host.FormBorderStyle = FormBorderStyle.None;
        host.ShowInTaskbar = false;
        host.StartPosition = FormStartPosition.Manual;
        host.Location = new Point(-3000, -3000);
        host.ClientSize = new Size(
            (int)Math.Ceiling(baseSize.Width * scale),
            (int)Math.Ceiling(baseSize.Height * scale));
        return host;
    }

    private static ToolboxShellControl CreateShell(Form host, float scale)
    {
        ToolboxShellControl shell = new ToolboxShellControl();
        host.Controls.Add(shell);
        host.Show();
        Application.DoEvents();
        if (Math.Abs(scale - 1.0f) > 0.001f)
            shell.Scale(new SizeF(scale, scale));
        PerformLayout(host, shell);
        return shell;
    }

    private static void PerformLayout(Form host, Control shell)
    {
        host.PerformLayout();
        shell.PerformLayout();
        Application.DoEvents();
    }

    private static void VerifyFixedFooter(Control page, Button primary,
        Button reset, Panel scrollHost, string context)
    {
        TableLayoutPanel footer = primary.Parent as TableLayoutPanel;
        Require(footer != null && footer.Dock == DockStyle.Bottom,
            context + ": primary command is not in a fixed bottom footer.");
        RequireVisibleInside(page, primary, context + ": primary command");
        RequireVisibleInside(page, reset, context + ": reset command");
        Require(scrollHost.Bounds.Bottom <= footer.Bounds.Top,
            context + ": settings overlap the fixed footer.");
        Require(!scrollHost.HorizontalScroll.Visible,
            context + ": settings have a horizontal scrollbar.");
        RequireFits(primary, context + ": primary command");
        RequireFits(reset, context + ": reset command");
    }

    private static Panel FindScrollHost(Control page)
    {
        return FindSingle<Panel>(page, delegate(Panel item)
        {
            return item.AutoScroll;
        });
    }

    private static Button FindButton(Control root, string text)
    {
        return FindSingle<Button>(root, delegate(Button item)
        {
            return item.Text == text;
        });
    }

    private static T FindNamed<T>(Control root, string name) where T : Control
    {
        return FindSingle<T>(root, delegate(T item)
        {
            return item.Name == name;
        });
    }

    private static T FindVisible<T>(Control root) where T : Control
    {
        return FindSingle<T>(root, delegate(T item)
        {
            return item.Visible;
        });
    }

    private static Control FindVisible(Control root, Type type)
    {
        List<Control> matches = new List<Control>();
        CollectControls(root, matches);
        matches.RemoveAll(delegate(Control item)
        {
            return item.GetType() != type || !item.Visible;
        });
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(String.Format(
                "Expected one visible {0}, found {1}.", type.Name, matches.Count));
        }
        return matches[0];
    }

    private static int CountNamed(Control root, string name)
    {
        List<Control> controls = new List<Control>();
        CollectControls(root, controls);
        return controls.FindAll(delegate(Control item)
        {
            return item.Name == name;
        }).Count;
    }

    private static void VerifyComboTextFits(ComboBox comboBox, string context)
    {
        int availableTextWidth = comboBox.ClientSize.Width -
            SystemInformation.VerticalScrollBarWidth;
        int measuredTextWidth = TextRenderer.MeasureText(comboBox.Text,
            comboBox.Font).Width;
        Require(measuredTextWidth <= availableTextWidth,
            String.Format("{0} text is clipped ({1}>{2}).",
                context, measuredTextWidth, availableTextWidth));
    }

    private static void RequireFits(Button button, string context)
    {
        Size measured = TextRenderer.MeasureText(button.Text, button.Font);
        Require(measured.Width <= Math.Max(0,
            button.ClientSize.Width - button.Padding.Horizontal),
            context + " text is clipped horizontally.");
        Require(measured.Height <= button.ClientSize.Height,
            context + " text is clipped vertically.");
    }

    private static void RequireVisibleInside(Control ancestor, Control child,
        string context)
    {
        Rectangle bounds = ancestor.RectangleToClient(
            child.RectangleToScreen(child.ClientRectangle));
        Require(child.Visible, context + " is hidden.");
        Require(bounds.Left >= 0 && bounds.Top >= 0 &&
            bounds.Right <= ancestor.ClientSize.Width &&
            bounds.Bottom <= ancestor.ClientSize.Height,
            context + " is clipped outside the page.");
    }

    private static T FindSingle<T>(Control root, Predicate<T> predicate)
        where T : Control
    {
        List<T> matches = new List<T>();
        Collect(root, predicate, matches);
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(String.Format(
                "Expected one {0}, found {1}.", typeof(T).Name, matches.Count));
        }
        return matches[0];
    }

    private static void Collect<T>(Control parent, Predicate<T> predicate,
        IList<T> matches) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            T typed = child as T;
            if (typed != null && predicate(typed))
                matches.Add(typed);
            Collect(child, predicate, matches);
        }
    }

    private static void CollectControls(Control parent, IList<Control> matches)
    {
        foreach (Control child in parent.Controls)
        {
            matches.Add(child);
            CollectControls(child, matches);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
