using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcColorDialog = Autodesk.AutoCAD.Windows.ColorDialog;
using AcColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using StructuralToolbox.Ballast.Core;
using StructuralToolbox.Ballast.Settings;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.Ballast.UI
{
    internal sealed class BallastToolboxControl : UserControl
    {
        private sealed class RowRuleEditor
        {
            public int Rows;
            public ComboBox Shape;
            public Label DimensionLabel;
            public Panel DimensionHost;
            public NumericUpDown Diameter;
            public NumericUpDown RectLength;
            public NumericUpDown RectWidth;
            public NumericUpDown Height;
            public NumericUpDown CrossLine;
            public ComboBox CountMode;
            public NumericUpDown Count;
            public NumericUpDown BallastSpacing;
            public NumericUpDown FrameSpacing;
            public ComboBox PlacementMode;
            public NumericUpDown FrontClearance;
            public BallastPlacementDiagram PlacementDiagram;
            public Button ColorButton;
            public Panel ColorSwatch;
            public BallastColorMode ColorMode;
            public int ColorIndex;
            public int ColorRed;
            public int ColorGreen;
            public int ColorBlue;
        }

        private readonly NumericUpDown _maximumComponentGap;
        private readonly TextBox _ballastLayerName;
        private readonly Label _status;
        private readonly TableLayoutPanel _rulesHost;
        private readonly TableLayoutPanel _footer;
        private readonly Button _primaryButton;
        private readonly Button _redetectButton;
        private readonly Button _arrayTableButton;
        private readonly List<RowRuleEditor> _editors =
            new List<RowRuleEditor>();
        private readonly List<int> _rowCounts = new List<int>();
        private readonly Dictionary<int, int> _arrayCounts =
            new Dictionary<int, int>();
        private bool _hasDetection;

        public BallastToolboxControl()
        {
            Dock = DockStyle.Fill;
            AutoScroll = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(245, 246, 248);

            Panel scrollHost = new Panel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = BackColor;
            scrollHost.Padding = new Padding(10, 8, 10, 4);
            Controls.Add(scrollHost);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layout.ColumnCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53.0f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47.0f));
            scrollHost.Controls.Add(layout);

            Label notice = new Label();
            notice.Text = "支持：顶层闭合二维多段线、块参照；可多次框选";
            notice.AutoSize = true;
            notice.MaximumSize = new Size(320, 0);
            notice.BackColor = Color.FromArgb(255, 248, 218);
            notice.ForeColor = Color.FromArgb(128, 74, 0);
            notice.BorderStyle = BorderStyle.FixedSingle;
            notice.Padding = new Padding(6, 4, 6, 4);
            notice.Margin = new Padding(0, 0, 0, 6);
            layout.Controls.Add(notice, 0, 0);
            layout.SetColumnSpan(notice, 2);

            AddSectionHeader(layout, 1, "阵列识别");
            _maximumComponentGap = CreateNumeric(0, 5000, 1, 1);
            _maximumComponentGap.Name = "BallastMaxComponentGap";
            AddSettingRow(layout, 2, "阵列断开净距 (mm)",
                _maximumComponentGap);

            BallastArrayGapDiagram gapDiagram = new BallastArrayGapDiagram();
            gapDiagram.Name = "BallastArrayGapDiagram";
            gapDiagram.Dock = DockStyle.Top;
            gapDiagram.Height = 112;
            gapDiagram.Margin = new Padding(0, 2, 0, 6);
            layout.Controls.Add(gapDiagram, 0, 3);
            layout.SetColumnSpan(gapDiagram, 2);

            _ballastLayerName = new TextBox();
            _ballastLayerName.Name = "BallastLayerName";
            _ballastLayerName.Dock = DockStyle.Fill;
            _ballastLayerName.Margin = new Padding(4, 2, 0, 2);
            AddSettingRow(layout, 4, "配重图层", _ballastLayerName);

            _status = new Label();
            _status.Name = "BallastStatus";
            _status.AutoSize = true;
            _status.MaximumSize = new Size(320, 0);
            _status.Padding = new Padding(7, 6, 7, 6);
            _status.Margin = new Padding(0, 8, 0, 8);
            layout.Controls.Add(_status, 0, 5);
            layout.SetColumnSpan(_status, 2);

            AddSectionHeader(layout, 6, "按组件排数设置配重");
            _rulesHost = new TableLayoutPanel();
            _rulesHost.Name = "BallastRulesHost";
            _rulesHost.Dock = DockStyle.Top;
            _rulesHost.AutoSize = true;
            _rulesHost.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _rulesHost.ColumnCount = 1;
            _rulesHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            _rulesHost.Margin = Padding.Empty;
            layout.Controls.Add(_rulesHost, 0, 7);
            layout.SetColumnSpan(_rulesHost, 2);

            Button specialButton = new Button();
            specialButton.Name = "BallastArrayOverrides";
            specialButton.Text = "特殊阵列覆盖设置";
            specialButton.AutoSize = true;
            specialButton.Margin = new Padding(0, 4, 0, 8);
            specialButton.Click += delegate { SendCommand("STPVBALLASTARRAYSETTINGS "); };
            layout.Controls.Add(specialButton, 0, 8);
            layout.SetColumnSpan(specialButton, 2);

            _arrayTableButton = new Button();
            _arrayTableButton.Name = "BallastArrayTable";
            _arrayTableButton.Text = "插入组件阵列清单";
            _arrayTableButton.AutoSize = true;
            _arrayTableButton.Enabled = false;
            _arrayTableButton.Margin = new Padding(0, 0, 0, 8);
            _arrayTableButton.Click += delegate
            {
                SendCommand("STPVBALLASTARRAYTABLE ");
            };
            layout.Controls.Add(_arrayTableButton, 0, 9);
            layout.SetColumnSpan(_arrayTableButton, 2);

            Button recognizePierButton = new Button();
            recognizePierButton.Name = "BallastPierRecognition";
            recognizePierButton.Text = "点选识别单墩";
            recognizePierButton.AutoSize = true;
            recognizePierButton.Margin = new Padding(0, 0, 0, 8);
            recognizePierButton.Click += delegate
            {
                SendCommand("STPVBALLASTPIERRECOGNIZE ");
            };
            layout.Controls.Add(recognizePierButton, 0, 10);
            layout.SetColumnSpan(recognizePierButton, 2);

            _footer = new TableLayoutPanel();
            _footer.Name = "BallastActionFooter";
            _footer.Dock = DockStyle.Bottom;
            _footer.Height = 50;
            _footer.Padding = new Padding(10, 4, 10, 4);
            _footer.BackColor = BackColor;
            _footer.ColumnCount = 4;
            _footer.RowCount = 1;
            for (int column = 0; column < _footer.ColumnCount; column++)
                _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25.0f));
            _footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            Controls.Add(_footer);

            _primaryButton = new Button();
            _primaryButton.Name = "BallastPrimaryAction";
            _primaryButton.Dock = DockStyle.Fill;
            _primaryButton.FlatStyle = FlatStyle.Flat;
            _primaryButton.BackColor = Color.FromArgb(176, 0, 176);
            _primaryButton.ForeColor = Color.White;
            _primaryButton.Font = new Font(Font.FontFamily, 10.0f, FontStyle.Bold);
            _primaryButton.Margin = new Padding(0, 0, 8, 0);
            _primaryButton.Click += OnPrimaryAction;
            _footer.Controls.Add(_primaryButton, 0, 0);

            _redetectButton = new Button();
            _redetectButton.Name = "RedetectBallastArrays";
            _redetectButton.Text = "重新识别";
            _redetectButton.Dock = DockStyle.Fill;
            _redetectButton.Margin = new Padding(0, 0, 8, 0);
            _redetectButton.Click += OnDetect;
            _footer.Controls.Add(_redetectButton, 1, 0);

            Button resetButton = new Button();
            resetButton.Name = "ResetBallastDefaults";
            resetButton.Text = "恢复默认参数";
            resetButton.Dock = DockStyle.Fill;
            resetButton.Margin = Padding.Empty;
            resetButton.Click += OnResetDefaults;
            _footer.Controls.Add(resetButton, 2, 0);

            Button statButton = new Button();
            statButton.Name = "BallastStatistics";
            statButton.Text = "框选统计";
            statButton.Dock = DockStyle.Fill;
            statButton.Margin = Padding.Empty;
            statButton.Click += delegate { SendCommand("STPVBALLASTSTAT "); };
            _footer.Controls.Add(statButton, 3, 0);

            LoadInitialSettings();
            ShowReady();
        }

        public void ApplyDetection(int componentCount, int arrayCount,
            IList<int> rowCounts, IDictionary<int, int> arrayCounts,
            string details)
        {
            _rowCounts.Clear();
            _arrayCounts.Clear();
            if (rowCounts != null)
            {
                for (int index = 0; index < rowCounts.Count; index++)
                    _rowCounts.Add(rowCounts[index]);
            }
            if (arrayCounts != null)
            {
                foreach (KeyValuePair<int, int> entry in arrayCounts)
                    _arrayCounts[entry.Key] = entry.Value;
            }

            _hasDetection = _rowCounts.Count > 0;
            _maximumComponentGap.Enabled = !_hasDetection;
            BuildRuleEditors(BallastSettingsStore.Load());
            _primaryButton.Text = _hasDetection ? "生成配重" : "识别组件阵列";
            _primaryButton.Enabled = true;
            _redetectButton.Visible = _hasDetection;
            _redetectButton.Enabled = true;
            _arrayTableButton.Enabled = _hasDetection;
            UpdateFooterLayout();
            SetStatus(String.Format("已识别 {0} 个组件、{1} 个阵列。{2}",
                componentCount, arrayCount,
                String.IsNullOrWhiteSpace(details) ? String.Empty : "\n" + details),
                Color.FromArgb(226, 246, 232), Color.FromArgb(27, 102, 52));
        }

        public void ShowBusy(string message)
        {
            _primaryButton.Enabled = false;
            _redetectButton.Enabled = false;
            SetStatus(message, Color.FromArgb(232, 239, 249),
                Color.FromArgb(40, 77, 116));
        }

        public void ShowError(string message)
        {
            _primaryButton.Enabled = true;
            _redetectButton.Enabled = true;
            SetStatus(message, Color.FromArgb(253, 232, 232),
                Color.FromArgb(156, 35, 35));
        }

        public void ShowGenerated(int frameCount, int ballastCount,
            int conflictFrameCount, int erasedFrameCount,
            int protectedFrameCount, string details)
        {
            _primaryButton.Enabled = true;
            _redetectButton.Enabled = true;
            string message = String.Format(
                "生成完成：{0} 榀、{1} 个配重块。\n"
                + "问题榀 {2}；替换旧榀 {3}；保护人工修改榀 {4}。",
                frameCount, ballastCount, conflictFrameCount,
                erasedFrameCount, protectedFrameCount);
            if (!String.IsNullOrWhiteSpace(details))
                message += "\n" + details;
            SetStatus(message,
                conflictFrameCount > 0
                    ? Color.FromArgb(255, 242, 220)
                    : Color.FromArgb(226, 246, 232),
                conflictFrameCount > 0
                    ? Color.FromArgb(145, 76, 0)
                    : Color.FromArgb(27, 102, 52));
        }

        public void ShowArrayTableInserted()
        {
            SetStatus("组件阵列清单已插入；可使用一次 UNDO 单独撤销该表。",
                Color.FromArgb(226, 246, 232), Color.FromArgb(27, 102, 52));
        }

        public void ClearDetection(string message)
        {
            _hasDetection = false;
            _rowCounts.Clear();
            _arrayCounts.Clear();
            _editors.Clear();
            _rulesHost.Controls.Clear();
            _maximumComponentGap.Enabled = true;
            _primaryButton.Text = "识别组件阵列";
            _primaryButton.Enabled = true;
            _redetectButton.Visible = false;
            _arrayTableButton.Enabled = false;
            UpdateFooterLayout();
            SetStatus(message, Color.FromArgb(238, 239, 242),
                Color.FromArgb(70, 74, 80));
        }

        private void UpdateFooterLayout()
        {
            float[] widths = _hasDetection
                ? new float[] { 25.0f, 24.0f, 31.0f, 20.0f }
                : new float[] { 43.0f, 0.0f, 32.0f, 25.0f };

            _footer.SuspendLayout();
            for (int column = 0; column < widths.Length; column++)
            {
                ColumnStyle style = _footer.ColumnStyles[column];
                style.SizeType = !_hasDetection && column == 1
                    ? SizeType.Absolute
                    : SizeType.Percent;
                style.Width = widths[column];
            }
            _footer.ResumeLayout(true);
        }

        private void LoadInitialSettings()
        {
            BallastSettings settings = BallastSettingsStore.Load();
            _maximumComponentGap.Value = Clamp(settings.MaxComponentGap,
                _maximumComponentGap);
            _ballastLayerName.Text = settings.LayerName;
            _ballastLayerName.Text = settings.LayerName;
        }

        private void ShowReady()
        {
            ClearDetection("先设置阵列断开净距，再识别组件阵列。识别完成后，"
                + "这里会按组件排数显示配重参数。 ");
        }

        private void BuildRuleEditors(BallastSettings settings)
        {
            _rulesHost.SuspendLayout();
            _rulesHost.Controls.Clear();
            _editors.Clear();
            _rulesHost.RowStyles.Clear();
            _rulesHost.RowCount = _rowCounts.Count;
            for (int index = 0; index < _rowCounts.Count; index++)
            {
                int rows = _rowCounts[index];
                RowRuleEditor editor = CreateRuleEditor(rows, settings);
                _editors.Add(editor);
                _rulesHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _rulesHost.Controls.Add(CreateRuleSection(editor), 0, index);
            }
            _rulesHost.ResumeLayout(true);
        }

        private RowRuleEditor CreateRuleEditor(int rows,
            BallastSettings settings)
        {
            BallastRowRule rule = FindRule(settings.RowRules, rows) ??
                new BallastRowRule
                {
                    RowsAlongLong = rows,
                    Shape = settings.Shape,
                    Diameter = settings.Diameter,
                    RectLength = settings.RectLength,
                    RectWidth = settings.RectWidth,
                    Height = settings.Height,
                    CrossLineLength = settings.CrossLineLength,
                    CountMode = BallastCountMode.AutoByRows,
                    ManualCount = rows + 1,
                    BallastCenterSpacing = settings.BallastCenterSpacing,
                    FrameCenterSpacing = settings.FrameCenterSpacing,
                    PlacementMode = settings.PlacementMode,
                    FrontCenterClearance = settings.FrontCenterClearance,
                    ColorMode = settings.ColorMode,
                    ColorIndex = settings.ColorIndex,
                    ColorRed = settings.ColorRed,
                    ColorGreen = settings.ColorGreen,
                    ColorBlue = settings.ColorBlue
                };
            RowRuleEditor editor = new RowRuleEditor();
            editor.Rows = rows;
            editor.Shape = CreateCombo("圆墩", "方墩");
            editor.DimensionLabel = CreateSettingLabel("圆墩直径 (mm)");
            editor.DimensionHost = new Panel();
            editor.DimensionHost.Dock = DockStyle.Fill;
            editor.DimensionHost.MinimumSize = new Size(0, 26);
            editor.Diameter = CreateNumeric(10, 5000, 1, 10);
            editor.RectLength = CreateNumeric(10, 5000, 1, 10);
            editor.RectWidth = CreateNumeric(10, 5000, 1, 10);
            editor.Height = CreateNumeric(1, 5000, 1, 10);
            editor.CrossLine = CreateNumeric(100, 20000, 1, 100);
            editor.CountMode = CreateCombo("自动：排数 + 1", "手动指定");
            editor.Count = CreateNumeric(1, 50, 0, 1);
            editor.BallastSpacing = CreateNumeric(100, 20000, 1, 10);
            editor.FrameSpacing = CreateNumeric(100, 20000, 1, 10);
            editor.PlacementMode = CreateCombo("默认前后对称", "自定义前侧");
            editor.FrontClearance = CreateNumeric(0, 20000, 1, 10);
            editor.PlacementDiagram = new BallastPlacementDiagram();
            editor.ColorSwatch = new Panel();
            editor.ColorSwatch.Size = new Size(22, 22);
            editor.ColorSwatch.Margin = new Padding(0, 2, 6, 0);
            editor.ColorSwatch.BorderStyle = BorderStyle.FixedSingle;
            editor.ColorButton = new Button();
            editor.ColorButton.Text = "默认颜色";
            editor.ColorButton.AutoSize = true;
            editor.ColorButton.Tag = editor;
            editor.ColorButton.Click += OnChooseBallastColor;
            editor.Shape.SelectedIndex = rule.Shape == BallastShape.Circle ? 0 : 1;
            editor.Diameter.Value = Clamp(rule.Diameter, editor.Diameter);
            editor.RectLength.Value = Clamp(rule.RectLength, editor.RectLength);
            editor.RectWidth.Value = Clamp(rule.RectWidth, editor.RectWidth);
            editor.Height.Value = Clamp(rule.Height <= 0.0 ? settings.Height : rule.Height,
                editor.Height);
            editor.CrossLine.Value = Clamp(rule.CrossLineLength, editor.CrossLine);
            editor.CountMode.SelectedIndex =
                rule.CountMode == BallastCountMode.AutoByRows ? 0 : 1;
            editor.Count.Value = Clamp(rule.ManualCount, editor.Count);
            editor.BallastSpacing.Value = Clamp(rule.BallastCenterSpacing,
                editor.BallastSpacing);
            editor.FrameSpacing.Value = Clamp(rule.FrameCenterSpacing,
                editor.FrameSpacing);
            editor.PlacementMode.SelectedIndex = rule.PlacementMode ==
                BallastPlacementMode.CustomFront ? 1 : 0;
            editor.FrontClearance.Value = Clamp(rule.FrontCenterClearance,
                editor.FrontClearance);
            editor.ColorMode = rule.ColorMode;
            editor.ColorIndex = rule.ColorIndex;
            editor.ColorRed = rule.ColorRed;
            editor.ColorGreen = rule.ColorGreen;
            editor.ColorBlue = rule.ColorBlue;
            UpdateBallastColorPresentation(editor);
            editor.Shape.SelectedIndexChanged += delegate
            {
                UpdateDimensionEditor(editor);
            };
            editor.CountMode.SelectedIndexChanged += delegate
            {
                editor.Count.Enabled = editor.CountMode.SelectedIndex == 1;
            };
            editor.PlacementMode.SelectedIndexChanged += delegate
            {
                editor.FrontClearance.Enabled = editor.PlacementMode.SelectedIndex == 1;
                editor.PlacementDiagram.Visible =
                    editor.PlacementMode.SelectedIndex == 1;
            };
            editor.FrontClearance.Enabled = editor.PlacementMode.SelectedIndex == 1;
            editor.PlacementDiagram.Visible =
                editor.PlacementMode.SelectedIndex == 1;
            editor.Count.Enabled = editor.CountMode.SelectedIndex == 1;
            UpdateDimensionEditor(editor);
            return editor;
        }

        private Control CreateRuleSection(RowRuleEditor editor)
        {
            TableLayoutPanel section = new TableLayoutPanel();
            section.Dock = DockStyle.Top;
            section.AutoSize = true;
            section.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            section.BackColor = Color.White;
            section.Padding = new Padding(8, 7, 8, 8);
            section.Margin = new Padding(0, 0, 0, 8);
            section.ColumnCount = 2;
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53.0f));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47.0f));
            section.RowCount = 13;
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 28.0f));
            for (int row = 1; row < 10; row++)
                section.RowStyles.Add(new RowStyle(SizeType.Absolute, 34.0f));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 34.0f));
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 34.0f));

            int arrayCount;
            string countText = _arrayCounts.TryGetValue(editor.Rows,
                out arrayCount) ? arrayCount + " 个阵列" : "阵列";
            Label title = new Label();
            title.Text = editor.Rows + " 排组件  ·  " + countText;
            title.AutoSize = true;
            title.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(122, 0, 122);
            title.Margin = new Padding(0, 0, 0, 5);
            section.Controls.Add(title, 0, 0);
            section.SetColumnSpan(title, 2);
            AddSettingRow(section, 1, "配重形状", editor.Shape);
            section.Controls.Add(editor.DimensionLabel, 0, 2);
            section.Controls.Add(editor.DimensionHost, 1, 2);
            AddSettingRow(section, 3, "定位线长度 (mm)", editor.CrossLine);
            AddSettingRow(section, 4, "配重数量模式", editor.CountMode);
            AddSettingRow(section, 5, "单榀配重数量", editor.Count);
            AddSettingRow(section, 6, "配重中心间距 S (mm)", editor.BallastSpacing);
            AddSettingRow(section, 7, "榀中心距 (mm)", editor.FrameSpacing);
            AddSettingRow(section, 8, "前后布置", editor.PlacementMode);
            AddSettingRow(section, 9, "前侧中心边距 F (mm)", editor.FrontClearance);
            section.Controls.Add(editor.PlacementDiagram, 0, 10);
            section.SetColumnSpan(editor.PlacementDiagram, 2);
            AddSettingRow(section, 11, "配重高度 (mm)", editor.Height);
            FlowLayoutPanel color = new FlowLayoutPanel();
            color.AutoSize = true;
            color.WrapContents = false;
            color.Controls.Add(editor.ColorSwatch);
            color.Controls.Add(editor.ColorButton);
            AddSettingRow(section, 12, "尺寸颜色", color);
            return section;
        }

        private static void UpdateDimensionEditor(RowRuleEditor editor)
        {
            editor.DimensionHost.Controls.Clear();
            if (editor.Shape.SelectedIndex == 1)
            {
                editor.DimensionLabel.Text = "方墩尺寸 (mm)";
                TableLayoutPanel dimensions = new TableLayoutPanel();
                dimensions.Dock = DockStyle.Fill;
                dimensions.ColumnCount = 3;
                dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
                dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18.0f));
                dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
                Label separator = new Label();
                separator.Text = "x";
                separator.Dock = DockStyle.Fill;
                separator.TextAlign = ContentAlignment.MiddleCenter;
                dimensions.Controls.Add(editor.RectLength, 0, 0);
                dimensions.Controls.Add(separator, 1, 0);
                dimensions.Controls.Add(editor.RectWidth, 2, 0);
                editor.DimensionHost.Controls.Add(dimensions);
            }
            else
            {
                editor.DimensionLabel.Text = "圆墩直径 (mm)";
                editor.DimensionHost.Controls.Add(editor.Diameter);
            }
        }

        private void OnPrimaryAction(object sender, EventArgs eventArgs)
        {
            if (_hasDetection)
                OnGenerate(sender, eventArgs);
            else
                OnDetect(sender, eventArgs);
        }

        private void OnDetect(object sender, EventArgs eventArgs)
        {
            BallastSettings settings = BallastSettingsStore.Load();
            settings.MaxComponentGap = (double)_maximumComponentGap.Value;
            settings.LayerName = _ballastLayerName.Text;
            if (String.IsNullOrWhiteSpace(settings.LayerName))
            {
                ShowError("配重图层名称不能为空。 ");
                _ballastLayerName.Focus();
                return;
            }
            BallastSettingsStore.Save(settings);
            ClearDetection("请在图面点选组件样本，然后框选处理范围。 ");
            ShowBusy("正在等待图面选择……");
            SendCommand("STPVBALLASTDETECT ");
        }

        private void OnGenerate(object sender, EventArgs eventArgs)
        {
            SaveRules();
            ShowBusy("正在生成配重……");
            SendCommand("STPVBALLASTGENERATE ");
        }

        private void OnResetDefaults(object sender, EventArgs eventArgs)
        {
            BallastSettings settings = new BallastSettings();
            BallastSettingsStore.Save(settings);
            _maximumComponentGap.Value = Clamp(settings.MaxComponentGap,
                _maximumComponentGap);
            if (_hasDetection)
                BuildRuleEditors(settings);
            SetStatus("已恢复默认参数。已识别的阵列仍然保留。",
                Color.FromArgb(232, 239, 249), Color.FromArgb(40, 77, 116));
        }

        private void SaveRules()
        {
            BallastSettings settings = BallastSettingsStore.Load();
            settings.MaxComponentGap = (double)_maximumComponentGap.Value;
            settings.LayerName = _ballastLayerName.Text;
            List<BallastRowRule> rules = new List<BallastRowRule>();
            for (int index = 0; index < _editors.Count; index++)
            {
                RowRuleEditor editor = _editors[index];
                rules.Add(new BallastRowRule
                {
                    RowsAlongLong = editor.Rows,
                    Shape = editor.Shape.SelectedIndex == 0
                        ? BallastShape.Circle : BallastShape.Rect,
                    Diameter = (double)editor.Diameter.Value,
                    RectLength = (double)editor.RectLength.Value,
                    RectWidth = (double)editor.RectWidth.Value,
                    Height = (double)editor.Height.Value,
                    CrossLineLength = (double)editor.CrossLine.Value,
                    CountMode = editor.CountMode.SelectedIndex == 0
                        ? BallastCountMode.AutoByRows : BallastCountMode.Manual,
                    ManualCount = (int)editor.Count.Value,
                    BallastCenterSpacing = (double)editor.BallastSpacing.Value,
                    FrameCenterSpacing = (double)editor.FrameSpacing.Value,
                    PlacementMode = editor.PlacementMode.SelectedIndex == 1
                        ? BallastPlacementMode.CustomFront
                        : BallastPlacementMode.Symmetric,
                    FrontCenterClearance = (double)editor.FrontClearance.Value,
                    ColorMode = editor.ColorMode,
                    ColorIndex = editor.ColorIndex,
                    ColorRed = editor.ColorRed,
                    ColorGreen = editor.ColorGreen,
                    ColorBlue = editor.ColorBlue
                });
            }
            settings.RowRules = rules;
            BallastSettingsStore.Save(settings);
        }

        private void OnChooseBallastColor(object sender, EventArgs eventArgs)
        {
            Button button = sender as Button;
            RowRuleEditor editor = button == null ? null : button.Tag as RowRuleEditor;
            if (editor == null)
                return;
            try
            {
                AcColorDialog dialog = new AcColorDialog();
                dialog.IncludeByBlockByLayer = false;
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;
                AcColor selected = dialog.Color;
                if (selected.ColorMethod == AcColorMethod.ByAci &&
                    selected.ColorIndex >= 1 && selected.ColorIndex <= 255)
                {
                    editor.ColorMode = BallastColorMode.Aci;
                    editor.ColorIndex = selected.ColorIndex;
                }
                else
                {
                    editor.ColorMode = BallastColorMode.TrueColor;
                }
                System.Drawing.Color rgb = selected.ColorValue;
                editor.ColorRed = rgb.R;
                editor.ColorGreen = rgb.G;
                editor.ColorBlue = rgb.B;
                UpdateBallastColorPresentation(editor);
            }
            catch (Exception exception)
            {
                MessageBox.Show("CAD 颜色选择器打开失败：" + exception.Message,
                    "自动布置配重", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static void UpdateBallastColorPresentation(RowRuleEditor editor)
        {
            if (editor == null)
                return;
            AcColor color;
            if (editor.ColorMode == BallastColorMode.TrueColor)
                color = AcColor.FromRgb((byte)editor.ColorRed,
                    (byte)editor.ColorGreen, (byte)editor.ColorBlue);
            else if (editor.ColorMode == BallastColorMode.Aci)
                color = AcColor.FromColorIndex(AcColorMethod.ByAci,
                    (short)Math.Max(1, Math.Min(255, editor.ColorIndex)));
            else
                color = AcColor.FromColorIndex(AcColorMethod.ByAci, 256);
            editor.ColorSwatch.BackColor = color.ColorValue;
            editor.ColorButton.Text = editor.ColorMode == BallastColorMode.ByLayer
                ? "默认颜色"
                : editor.ColorMode == BallastColorMode.Aci
                    ? "ACI " + editor.ColorIndex + "..."
                    : String.Format("#{0:X2}{1:X2}{2:X2}...",
                        editor.ColorRed, editor.ColorGreen, editor.ColorBlue);
        }

        private void SendCommand(string command)
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                ShowError("请先打开一张图纸。 ");
                return;
            }
            document.SendStringToExecute(command, true, false, false);
        }

        private void SetStatus(string text, Color background, Color foreground)
        {
            _status.Text = text;
            _status.BackColor = background;
            _status.ForeColor = foreground;
        }

        private static BallastRowRule FindRule(IList<BallastRowRule> rules,
            int rows)
        {
            if (rules == null)
                return null;
            for (int index = 0; index < rules.Count; index++)
                if (rules[index] != null && rules[index].RowsAlongLong == rows)
                    return rules[index];
            return null;
        }

        private static ComboBox CreateCombo(params string[] items)
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(4, 2, 0, 2);
            combo.Items.AddRange(items);
            return combo;
        }

        private static NumericUpDown CreateNumeric(decimal minimum,
            decimal maximum, int decimals, decimal increment)
        {
            NumericUpDown input = new NumericUpDown();
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.DecimalPlaces = decimals;
            input.Increment = increment;
            input.ThousandsSeparator = true;
            input.Dock = DockStyle.Fill;
            input.TextAlign = HorizontalAlignment.Right;
            input.Margin = new Padding(4, 2, 0, 2);
            return input;
        }

        private static Label CreateSettingLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 3, 4, 3);
            return label;
        }

        private static void AddSettingRow(TableLayoutPanel layout, int row,
            string labelText, Control control)
        {
            layout.Controls.Add(CreateSettingLabel(labelText), 0, row);
            layout.Controls.Add(control, 1, row);
        }

        private static void AddSectionHeader(TableLayoutPanel layout, int row,
            string text)
        {
            Label header = new Label();
            header.Text = text;
            header.AutoSize = true;
            header.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            header.ForeColor = Color.FromArgb(122, 0, 122);
            header.Margin = new Padding(0, row == 1 ? 2 : 8, 0, 3);
            layout.Controls.Add(header, 0, row);
            layout.SetColumnSpan(header, 2);
        }

        private static decimal Clamp(double value, NumericUpDown control)
        {
            decimal result = (decimal)value;
            return Math.Max(control.Minimum, Math.Min(control.Maximum, result));
        }
    }
}
