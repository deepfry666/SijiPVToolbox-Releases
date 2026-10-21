using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using StructuralToolbox.Settings;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcColorDialog = Autodesk.AutoCAD.Windows.ColorDialog;
using AcColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;

namespace StructuralToolbox.UI
{
    internal sealed class ToolboxControl : UserControl
    {
        private readonly NumericUpDown _railCount;
        private readonly NumericUpDown _totalSpacing;
        private readonly NumericUpDown _maximumGap;
        private readonly NumericUpDown _overhang;
        private readonly TextBox _railLayerName;
        private readonly ComboBox _railColorApplication;
        private readonly Button _railColorButton;
        private readonly Panel _railColorSwatch;
        private readonly NumericUpDown _railGlobalWidth;
        private RailColorKind _railColorKind;
        private int _railColorIndex;
        private int _railColorRed;
        private int _railColorGreen;
        private int _railColorBlue;
        private bool _initializing;

        public ToolboxControl()
        {
            _initializing = true;
            Dock = DockStyle.Fill;
            AutoScroll = false;
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
            notice.Text = "样本图层：闭合矩形多段线";
            notice.AutoSize = true;
            notice.MaximumSize = new Size(290, 0);
            notice.BackColor = Color.FromArgb(255, 248, 218);
            notice.ForeColor = Color.FromArgb(128, 74, 0);
            notice.BorderStyle = BorderStyle.FixedSingle;
            notice.Padding = new Padding(6, 4, 6, 4);
            notice.Margin = new Padding(0, 0, 0, 6);
            layout.Controls.Add(notice, 0, 0);
            layout.SetColumnSpan(notice, 2);

            _railCount = CreateNumeric(1.0M, 20.0M, 0, 1.0M);
            _totalSpacing = CreateNumeric(0.0M, 100000.0M, 1, 10.0M);
            _maximumGap = CreateNumeric(0.0M, 100000.0M, 1, 1.0M);
            _overhang = CreateNumeric(0.0M, 100000.0M, 1, 10.0M);
            _railGlobalWidth = CreateNumeric(0.0M, 100000.0M, 2, 0.10M);
            _railLayerName = new TextBox();
            _railLayerName.Name = "RailLayerName";
            _railLayerName.Dock = DockStyle.Fill;
            _railLayerName.MaxLength = 255;
            _railLayerName.Margin = new Padding(4, 2, 0, 2);

            _railColorApplication = new ComboBox();
            _railColorApplication.Name = "RailColorApplication";
            _railColorApplication.DropDownStyle = ComboBoxStyle.DropDownList;
            _railColorApplication.Dock = DockStyle.Fill;
            _railColorApplication.Margin = new Padding(4, 2, 0, 2);
            _railColorApplication.Items.Add("随图层 (ByLayer)");
            _railColorApplication.Items.Add("实体指定颜色");

            FlowLayoutPanel colorValue = new FlowLayoutPanel();
            colorValue.AutoSize = true;
            colorValue.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            colorValue.WrapContents = false;
            colorValue.Anchor = AnchorStyles.Left;
            colorValue.Margin = new Padding(4, 1, 0, 1);

            _railColorSwatch = new Panel();
            _railColorSwatch.Size = new Size(22, 22);
            _railColorSwatch.Margin = new Padding(0, 2, 6, 0);
            _railColorSwatch.BackColor = Color.Magenta;
            _railColorSwatch.BorderStyle = BorderStyle.FixedSingle;

            _railColorButton = new Button();
            _railColorButton.Name = "RailColorButton";
            _railColorButton.AutoSize = true;
            _railColorButton.Text = "选择颜色...";
            _railColorButton.Click += OnChooseRailColor;
            colorValue.Controls.Add(_railColorSwatch);
            colorValue.Controls.Add(_railColorButton);

            AddSectionHeader(layout, 1, "导轨布置");
            AddSettingRow(layout, 2, "导轨数量", _railCount);
            AddSettingRow(layout, 3, "首末导轨总间距 (mm)", _totalSpacing);
            AddSettingRow(layout, 4, "最大贯通净距 (mm)", _maximumGap);
            AddSettingRow(layout, 5, "端部伸出 (mm)", _overhang);

            AddSectionHeader(layout, 6, "导轨输出");
            AddSettingRow(layout, 7, "导轨图层", _railLayerName);
            AddSettingRow(layout, 8, "颜色应用", _railColorApplication);
            AddSettingRow(layout, 9, "导轨颜色", colorValue);
            AddSettingRow(layout, 10, "全局宽度 (mm)", _railGlobalWidth);

            Label mode = new Label();
            mode.Text = "长边方案；导轨垂直组件长轴；按实际样式预览。";
            mode.AutoSize = true;
            mode.MaximumSize = new Size(290, 0);
            mode.ForeColor = Color.FromArgb(70, 70, 70);
            mode.Margin = new Padding(0, 6, 0, 6);
            layout.Controls.Add(mode, 0, 11);
            layout.SetColumnSpan(mode, 2);

            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 50;
            footer.Padding = new Padding(10, 4, 10, 4);
            footer.BackColor = BackColor;
            footer.ColumnCount = 3;
            footer.RowCount = 1;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94.0f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86.0f));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            Controls.Add(footer);

            Button drawButton = new Button();
            drawButton.Text = "自动画导轨";
            drawButton.Dock = DockStyle.Fill;
            drawButton.FlatStyle = FlatStyle.Flat;
            drawButton.BackColor = Color.FromArgb(176, 0, 176);
            drawButton.ForeColor = Color.White;
            drawButton.Font = new Font(Font.FontFamily, 10.0f, FontStyle.Bold);
            drawButton.Margin = new Padding(0, 0, 8, 0);
            drawButton.Click += OnDrawRails;
            footer.Controls.Add(drawButton, 0, 0);

            Button resetButton = new Button();
            resetButton.Text = "恢复默认参数";
            resetButton.Dock = DockStyle.Fill;
            resetButton.Margin = Padding.Empty;
            resetButton.Click += OnResetDefaults;
            footer.Controls.Add(resetButton, 1, 0);

            Button statButton = new Button();
            statButton.Name = "RailStatistics";
            statButton.Text = "框选统计";
            statButton.Dock = DockStyle.Fill;
            statButton.Margin = Padding.Empty;
            statButton.Click += delegate
            {
                Document document = AcApplication.DocumentManager.MdiActiveDocument;
                if (document != null)
                    document.SendStringToExecute("STPVRAILSTAT ", true, false, true);
            };
            footer.Controls.Add(statButton, 2, 0);

            RailSettings settings = RailSettingsStore.Load();
            ApplySettings(settings);
            _railCount.ValueChanged += OnSettingsChanged;
            _totalSpacing.ValueChanged += OnSettingsChanged;
            _maximumGap.ValueChanged += OnSettingsChanged;
            _overhang.ValueChanged += OnSettingsChanged;
            _railLayerName.TextChanged += OnSettingsChanged;
            _railColorApplication.SelectedIndexChanged += OnSettingsChanged;
            _railGlobalWidth.ValueChanged += OnSettingsChanged;
            _initializing = false;
        }

        private static NumericUpDown CreateNumeric(decimal minimum, decimal maximum,
            int decimalPlaces, decimal increment)
        {
            NumericUpDown input = new NumericUpDown();
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.DecimalPlaces = decimalPlaces;
            input.Increment = increment;
            input.ThousandsSeparator = true;
            input.Dock = DockStyle.Fill;
            input.TextAlign = HorizontalAlignment.Right;
            input.Margin = new Padding(4, 2, 0, 2);
            return input;
        }

        private static void AddSettingRow(TableLayoutPanel layout, int row,
            string labelText, Control control)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 3, 4, 3);
            layout.Controls.Add(label, 0, row);
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

        private void OnSettingsChanged(object sender, EventArgs eventArgs)
        {
            if (_initializing)
                return;

            SaveSettings();
        }

        private void OnDrawRails(object sender, EventArgs eventArgs)
        {
            if (String.IsNullOrWhiteSpace(_railLayerName.Text))
            {
                MessageBox.Show("输出图层名称不能为空。", "四季光伏工具箱",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _railLayerName.Focus();
                return;
            }
            _railLayerName.Text = _railLayerName.Text.Trim();
            SaveSettings();
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                MessageBox.Show("请先打开一张图纸。", "四季光伏工具箱",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            document.SendStringToExecute("STPVRAIL ", true, false, true);
        }

        private void OnChooseRailColor(object sender, EventArgs eventArgs)
        {
            try
            {
                AcColorDialog dialog = new AcColorDialog();
                dialog.IncludeByBlockByLayer = false;
                dialog.Color = CreateAutoCadColor();
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                AcColor selectedColor = dialog.Color;
                if (selectedColor.ColorMethod == AcColorMethod.ByAci &&
                    selectedColor.ColorIndex >= 1 && selectedColor.ColorIndex <= 255)
                {
                    _railColorKind = RailColorKind.Aci;
                    _railColorIndex = selectedColor.ColorIndex;
                }
                else
                {
                    _railColorKind = RailColorKind.TrueColor;
                }

                System.Drawing.Color selectedRgb = selectedColor.ColorValue;
                _railColorRed = selectedRgb.R;
                _railColorGreen = selectedRgb.G;
                _railColorBlue = selectedRgb.B;
                UpdateColorPresentation();
                SaveSettings();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "CAD 颜色选择器打开失败，请确认当前在完整 AutoCAD 中运行。\n" +
                    exception.Message,
                    "四季光伏工具箱", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnResetDefaults(object sender, EventArgs eventArgs)
        {
            RailSettings settings = RailSettingsStore.Load();
            RailSettings defaults = new RailSettings();
            settings.RailCount = defaults.RailCount;
            settings.TotalSpacing = defaults.TotalSpacing;
            settings.MaximumContinuityGap = defaults.MaximumContinuityGap;
            settings.EndOverhang = defaults.EndOverhang;
            settings.RailLayerName = defaults.RailLayerName;
            settings.RailGlobalWidth = defaults.RailGlobalWidth;
            settings.RailColorApplication = defaults.RailColorApplication;
            settings.RailColorKind = defaults.RailColorKind;
            settings.RailColorIndex = defaults.RailColorIndex;
            settings.RailColorRed = defaults.RailColorRed;
            settings.RailColorGreen = defaults.RailColorGreen;
            settings.RailColorBlue = defaults.RailColorBlue;
            ApplySettings(settings);
            SaveSettings();
        }

        private void ApplySettings(RailSettings settings)
        {
            bool wasInitializing = _initializing;
            _initializing = true;
            try
            {
                settings.Normalize();
                _railCount.Value = settings.RailCount;
                _totalSpacing.Value = (decimal)settings.TotalSpacing;
                _maximumGap.Value = (decimal)settings.MaximumContinuityGap;
                _overhang.Value = (decimal)settings.EndOverhang;
                _railLayerName.Text = settings.RailLayerName;
                _railGlobalWidth.Value = (decimal)settings.RailGlobalWidth;
                _railColorApplication.SelectedIndex =
                    settings.RailColorApplication == RailColorApplication.ByEntity ? 1 : 0;
                _railColorKind = settings.RailColorKind;
                _railColorIndex = settings.RailColorIndex;
                _railColorRed = settings.RailColorRed;
                _railColorGreen = settings.RailColorGreen;
                _railColorBlue = settings.RailColorBlue;
                SyncStoredRgbFromColor();
                UpdateColorPresentation();
            }
            finally
            {
                _initializing = wasInitializing;
            }
        }

        private void SaveSettings()
        {
            RailSettings settings = RailSettingsStore.Load();
            settings.RailCount = (int)_railCount.Value;
            settings.TotalSpacing = (double)_totalSpacing.Value;
            settings.MaximumContinuityGap = (double)_maximumGap.Value;
            settings.EndOverhang = (double)_overhang.Value;
            settings.RailLayerName = _railLayerName.Text;
            settings.RailGlobalWidth = (double)_railGlobalWidth.Value;
            settings.RailColorApplication = _railColorApplication.SelectedIndex == 1
                ? RailColorApplication.ByEntity
                : RailColorApplication.ByLayer;
            settings.RailColorKind = _railColorKind;
            settings.RailColorIndex = _railColorIndex;
            SyncStoredRgbFromColor();
            settings.RailColorRed = _railColorRed;
            settings.RailColorGreen = _railColorGreen;
            settings.RailColorBlue = _railColorBlue;
            try
            {
                RailSettingsStore.Save(settings);
            }
            catch (Exception exception)
            {
                MessageBox.Show("参数保存失败：" + exception.Message, "四季光伏工具箱",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private AcColor CreateAutoCadColor()
        {
            if (_railColorKind == RailColorKind.TrueColor)
            {
                return AcColor.FromRgb(
                    (byte)_railColorRed,
                    (byte)_railColorGreen,
                    (byte)_railColorBlue);
            }

            return AcColor.FromColorIndex(AcColorMethod.ByAci, (short)_railColorIndex);
        }

        private void UpdateColorPresentation()
        {
            AcColor color = CreateAutoCadColor();
            System.Drawing.Color displayColor = color.ColorValue;
            _railColorSwatch.BackColor = displayColor;
            _railColorButton.Text = _railColorKind == RailColorKind.Aci
                ? "ACI " + _railColorIndex + "..."
                : String.Format("#{0:X2}{1:X2}{2:X2}...",
                    _railColorRed, _railColorGreen, _railColorBlue);
        }

        private void SyncStoredRgbFromColor()
        {
            AcColor color = CreateAutoCadColor();
            System.Drawing.Color displayColor = color.ColorValue;
            _railColorRed = displayColor.R;
            _railColorGreen = displayColor.G;
            _railColorBlue = displayColor.B;
        }

    }
}
