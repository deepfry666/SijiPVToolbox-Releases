using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StructuralToolbox.SupportDetail.Core;
using StructuralToolbox.SupportDetail.Settings;

namespace StructuralToolbox.SupportDetail.UI
{
    public sealed class SupportDetailDialog : Form
    {
        private readonly NumericUpDown _componentCount;
        private readonly NumericUpDown _componentLength;
        private readonly NumericUpDown _componentWidth;
        private readonly NumericUpDown _componentGap;
        private readonly NumericUpDown _slope;
        private readonly ComboBox _railMode;
        private readonly NumericUpDown _railSpacing;
        private readonly TextBox _railPositions;
        private readonly ComboBox _pierMode;
        private readonly CheckBox _pierCountFollows;
        private readonly NumericUpDown _pierCount;
        private readonly NumericUpDown _pierSpacing;
        private readonly TextBox _pierStations;
        private readonly NumericUpDown _columnHeight;
        private readonly NumericUpDown _triangleHeight;
        private readonly NumericUpDown _triangleBase;
        private readonly NumericUpDown _beamOverhang;
        private readonly NumericUpDown _beamWidth;
        private readonly NumericUpDown _panelThickness;
        private readonly ComboBox _pierShape;
        private readonly NumericUpDown _pierDiameter;
        private readonly NumericUpDown _pierLength;
        private readonly NumericUpDown _pierWidth;
        private readonly NumericUpDown _pierHeight;
        private readonly TextBox _outputLayer;
        private readonly CheckBox _includeNodes;
        private readonly CheckBox _includeTable;
        private readonly CheckBox _includeBackPull;
        private readonly SupportDetailDiagram _diagram;
        private readonly Label _previewStatus;
        private bool _updating;

        public SupportDetailDialog(SupportDetailSettings settings)
        {
            ResultSettings = settings == null
                ? new SupportDetailSettings() : settings.Clone();
            ResultSettings.Normalize();

            Text = "混凝土屋面支架大样 · 四季光伏工具箱 v0.4.2";
            Name = "SupportDetailDialog";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 620);
            ClientSize = new Size(1080, 760);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowInTaskbar = false;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51.0f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 49.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46.0f));
            Controls.Add(root);

            Panel scrollHost = new Panel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.Padding = new Padding(0, 0, 8, 0);
            root.Controls.Add(scrollHost, 0, 0);

            TableLayoutPanel fields = new TableLayoutPanel();
            fields.Name = "SupportDetailFields";
            fields.Dock = DockStyle.Top;
            fields.AutoSize = true;
            fields.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fields.ColumnCount = 2;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230.0f));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            scrollHost.Controls.Add(fields);

            AddSection(fields, "组件与斜梁");
            _componentCount = AddNumeric(fields, "组件数量 N", "ComponentCount",
                1, 200, 0, 1, ResultSettings.ComponentCount);
            _componentLength = AddNumeric(fields, "组件斜长 L (mm)",
                "ComponentLength", 100, 20000, 1, 10,
                ResultSettings.ComponentLength);
            _componentWidth = AddNumeric(fields, "组件宽度 W (mm)",
                "ComponentWidth", 100, 10000, 1, 10,
                ResultSettings.ComponentWidth);
            _componentGap = AddNumeric(fields, "组件净缝 G (mm)",
                "ComponentGap", 0, 1000, 1, 1, ResultSettings.ComponentGap);
            _slope = AddNumeric(fields, "支架倾角 α (°)", "SlopeDegrees",
                -89, 89, 3, 0.1m, ResultSettings.SlopeDegrees);
            _beamOverhang = AddNumeric(fields, "斜梁超外侧三角件中心 E (mm)",
                "BeamOverhang", 0, 10000, 1, 10,
                ResultSettings.BeamOverhang);
            _beamWidth = AddNumeric(fields, "斜梁截面高度 B (mm)",
                "BeamWidth", 1, 1000, 1, 1, ResultSettings.BeamWidth);
            _panelThickness = AddNumeric(fields, "组件厚度 t (mm)",
                "PanelThickness", 1, 300, 1, 1,
                ResultSettings.PanelThickness);

            AddSection(fields, "夹持 / 导轨位置");
            _railMode = AddCombo(fields, "夹持位置模式", "RailPositionMode",
                new[] { "默认居中", "手动输入" },
                ResultSettings.RailPositionMode ==
                    SupportDetailPositionMode.Manual ? 1 : 0);
            _railSpacing = AddNumeric(fields, "导轨中心距 R (mm)",
                "RailCenterSpacing", 1, 20000, 1, 10,
                ResultSettings.RailCenterSpacing);
            _railPositions = AddText(fields,
                "夹持中心线 r (自每块组件低端)", "RailPositions",
                ResultSettings.RailPositions,
                "示例：491, 1891；沿每块组件的 +s 方向量到夹持中心线。");

            AddSection(fields, "支墩、立柱和三角件");
            _pierMode = AddCombo(fields, "墩位模式", "PierPositionMode",
                new[] { "自动居中", "手动输入" },
                ResultSettings.PierPositionMode ==
                    SupportDetailPositionMode.Manual ? 1 : 0);
            _pierCountFollows = AddCheckBox(fields,
                "墩子数量按 P=N+1", "PierCountFollowsComponents",
                ResultSettings.PierCountFollowsComponents);
            _pierCount = AddNumeric(fields, "墩子数量 P", "PierCount",
                1, 201, 0, 1, ResultSettings.PierCount);
            _pierSpacing = AddNumeric(fields, "自动墩中心距 S (mm)",
                "PierSpacing", 1, 50000, 1, 10,
                ResultSettings.PierSpacing);
            _pierStations = AddText(fields,
                "墩中心线位置 s (自组件阵列低端)", "PierStations",
                ResultSettings.PierStations,
                "示例：200, 1600, 3000；均为组件阵列低端至墩中心线的中心线尺寸。");
            _columnHeight = AddNumeric(fields,
                "基准立柱高度 H (mm)", "ColumnHeight",
                0, 20000, 1, 10, ResultSettings.ColumnHeight);
            _triangleHeight = AddNumeric(fields,
                "三角件高度 T (mm)", "TriangleHeight",
                1, 2000, 1, 1, ResultSettings.TriangleHeight);
            _triangleBase = AddNumeric(fields,
                "三角件底宽 A (mm)", "TriangleBase",
                1, 5000, 1, 1, ResultSettings.TriangleBase);

            AddSection(fields, "混凝土支墩 / 配重块");
            _pierShape = AddCombo(fields, "外轮廓形式", "PierShape",
                new[] { "圆形", "矩形" },
                ResultSettings.PierShape ==
                    SupportDetailPierShape.Rectangular ? 1 : 0);
            _pierDiameter = AddNumeric(fields, "圆形直径 D (mm)",
                "PierDiameter", 1, 10000, 1, 10,
                ResultSettings.PierDiameter);
            _pierLength = AddNumeric(fields, "矩形长度 Bx (mm)",
                "PierLength", 1, 10000, 1, 10,
                ResultSettings.PierLength);
            _pierWidth = AddNumeric(fields, "矩形宽度 By (mm)",
                "PierWidth", 1, 10000, 1, 10,
                ResultSettings.PierWidth);
            _pierHeight = AddNumeric(fields, "支墩高度 h (mm)",
                "PierHeight", 1, 10000, 1, 10,
                ResultSettings.PierHeight);

            AddSection(fields, "出图内容");
            _outputLayer = AddText(fields, "输出图层", "OutputLayer",
                ResultSettings.OutputLayer, null);
            _includeNodes = AddCheckBox(fields, "附固定节点 A-D",
                "IncludeNodeDetails", ResultSettings.IncludeNodeDetails);
            _includeTable = AddCheckBox(fields, "附材料表",
                "IncludeMaterialTable", ResultSettings.IncludeMaterialTable);
            _includeBackPull = AddCheckBox(fields, "主图显示背拉（后拉）",
                "IncludeBackPull", ResultSettings.IncludeBackPull);

            Label sourceNote = new Label();
            sourceNote.Name = "PierSourceNote";
            sourceNote.Text =
                "默认采用材料表 D500×300。原总体大样另有 Φ500×400 标注，" +
                "因此 D、Bx、By、h 均保留为本实例参数。";
            sourceNote.AutoSize = true;
            sourceNote.MaximumSize = new Size(480, 0);
            sourceNote.ForeColor = Color.FromArgb(115, 76, 18);
            sourceNote.BackColor = Color.FromArgb(255, 248, 220);
            sourceNote.Padding = new Padding(6, 4, 6, 4);
            sourceNote.Margin = new Padding(0, 7, 0, 8);
            fields.Controls.Add(sourceNote, 0, fields.RowCount);
            fields.SetColumnSpan(sourceNote, 2);

            TableLayoutPanel visualHost = new TableLayoutPanel();
            visualHost.Dock = DockStyle.Fill;
            visualHost.Padding = new Padding(8, 0, 0, 0);
            visualHost.ColumnCount = 1;
            visualHost.RowCount = 3;
            visualHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            visualHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            visualHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            visualHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(visualHost, 1, 0);

            _diagram = new SupportDetailDiagram();
            _diagram.Name = "SupportDetailDiagram";
            visualHost.Controls.Add(_diagram, 0, 0);

            Label legend = new Label();
            legend.Name = "SupportDetailDiagramLegend";
            legend.Text =
                "基准：组件阵列低端为 0，沿组件斜面向高端为 +s。" +
                "r、s、S、R 都是中心线尺寸；D 或 Bx×By 是支墩外轮廓。\n" +
                "E 是斜梁端部至最外侧三角件中心线的距离。" +
                "H 是第一个/最低基准墩位的立柱高度，其余高度随 α 和墩位求出，" +
                "保证斜梁为整根直梁。";
            legend.AutoSize = true;
            legend.MaximumSize = new Size(500, 0);
            legend.Padding = new Padding(4, 8, 4, 4);
            visualHost.Controls.Add(legend, 0, 1);

            _previewStatus = new Label();
            _previewStatus.Name = "SupportDetailPreviewStatus";
            _previewStatus.AutoSize = true;
            _previewStatus.MaximumSize = new Size(500, 0);
            _previewStatus.Padding = new Padding(4, 3, 4, 6);
            visualHost.Controls.Add(_previewStatus, 0, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = false;
            root.Controls.Add(buttons, 0, 1);
            root.SetColumnSpan(buttons, 2);

            Button generate = new Button();
            generate.Name = "GenerateSupportDetail";
            generate.Text = "生成 / 更新大样";
            generate.AutoSize = true;
            generate.Padding = new Padding(10, 2, 10, 2);
            generate.Click += OnAccept;
            buttons.Controls.Add(generate);

            Button cancel = new Button();
            cancel.Name = "CancelSupportDetail";
            cancel.Text = "取消";
            cancel.AutoSize = true;
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Padding = new Padding(8, 2, 8, 2);
            buttons.Controls.Add(cancel);
            AcceptButton = generate;
            CancelButton = cancel;

            HookPreviewEvents(this);
            UpdateFieldAvailability();
            RefreshPreview();
        }

        public SupportDetailSettings ResultSettings { get; private set; }

        private void OnAccept(object sender, EventArgs eventArgs)
        {
            try
            {
                SupportDetailSettings settings = ReadSettings();
                if (String.IsNullOrWhiteSpace(settings.OutputLayer))
                    throw new InvalidOperationException("输出图层名称不能为空。");
                SupportDetailBuildResult result =
                    SupportDetailGeometryBuilder.Build(settings);
                if (!result.IsValid)
                    throw new InvalidOperationException(
                        String.Join("\n", result.Errors));
                ResultSettings = settings;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "支架大样参数错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnPreviewChanged(object sender, EventArgs eventArgs)
        {
            if (_updating)
                return;
            if (ReferenceEquals(sender, _componentCount) &&
                _pierCountFollows.Checked)
            {
                _updating = true;
                _pierCount.Value = Math.Min(_pierCount.Maximum,
                    _componentCount.Value + 1);
                _updating = false;
            }
            UpdateFieldAvailability();
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            try
            {
                SupportDetailSettings settings = ReadSettings();
                _diagram.Settings = settings;
                SupportDetailBuildResult result =
                    SupportDetailGeometryBuilder.Build(settings);
                if (!result.IsValid)
                {
                    _previewStatus.ForeColor = Color.FromArgb(170, 45, 45);
                    _previewStatus.Text = String.Join("；", result.Errors);
                    return;
                }
                _previewStatus.ForeColor = result.Warnings.Count == 0
                    ? Color.FromArgb(45, 105, 70)
                    : Color.FromArgb(150, 88, 20);
                _previewStatus.Text = result.Warnings.Count == 0
                    ? "当前参数可生成。斜梁长 " +
                        result.Geometry.BeamLength.ToString("0.#",
                            CultureInfo.CurrentCulture) + " mm；立柱 " +
                        result.Geometry.Piers.Count.ToString(
                            CultureInfo.CurrentCulture) + " 根。"
                    : String.Join("；", result.Warnings);
            }
            catch (Exception exception)
            {
                _previewStatus.ForeColor = Color.FromArgb(170, 45, 45);
                _previewStatus.Text = exception.Message;
            }
        }

        private SupportDetailSettings ReadSettings()
        {
            SupportDetailSettings settings = new SupportDetailSettings();
            settings.ComponentCount = (int)_componentCount.Value;
            settings.ComponentLength = (double)_componentLength.Value;
            settings.ComponentWidth = (double)_componentWidth.Value;
            settings.ComponentGap = (double)_componentGap.Value;
            settings.SlopeDegrees = (double)_slope.Value;
            settings.RailPositionMode = _railMode.SelectedIndex == 1
                ? SupportDetailPositionMode.Manual
                : SupportDetailPositionMode.Automatic;
            settings.RailCenterSpacing = (double)_railSpacing.Value;
            settings.RailPositions = _railPositions.Text.Trim();
            settings.PierPositionMode = _pierMode.SelectedIndex == 1
                ? SupportDetailPositionMode.Manual
                : SupportDetailPositionMode.Automatic;
            settings.PierCountFollowsComponents = _pierCountFollows.Checked;
            settings.PierCount = (int)_pierCount.Value;
            settings.PierSpacing = (double)_pierSpacing.Value;
            settings.PierStations = _pierStations.Text.Trim();
            settings.ColumnHeight = (double)_columnHeight.Value;
            settings.TriangleHeight = (double)_triangleHeight.Value;
            settings.TriangleBase = (double)_triangleBase.Value;
            settings.BeamOverhang = (double)_beamOverhang.Value;
            settings.BeamWidth = (double)_beamWidth.Value;
            settings.PanelThickness = (double)_panelThickness.Value;
            settings.PierShape = _pierShape.SelectedIndex == 1
                ? SupportDetailPierShape.Rectangular
                : SupportDetailPierShape.Circular;
            settings.PierDiameter = (double)_pierDiameter.Value;
            settings.PierLength = (double)_pierLength.Value;
            settings.PierWidth = (double)_pierWidth.Value;
            settings.PierHeight = (double)_pierHeight.Value;
            settings.OutputLayer = _outputLayer.Text.Trim();
            settings.IncludeNodeDetails = _includeNodes.Checked;
            settings.IncludeMaterialTable = _includeTable.Checked;
            settings.IncludeBackPull = _includeBackPull.Checked;
            settings.Normalize();
            return settings;
        }

        private void UpdateFieldAvailability()
        {
            bool manualRails = _railMode.SelectedIndex == 1;
            _railSpacing.Enabled = !manualRails;
            _railPositions.Enabled = manualRails;
            bool manualPiers = _pierMode.SelectedIndex == 1;
            _pierStations.Enabled = manualPiers;
            _pierSpacing.Enabled = !manualPiers;
            _pierCountFollows.Enabled = !manualPiers;
            _pierCount.Enabled = !manualPiers && !_pierCountFollows.Checked;
            bool circular = _pierShape.SelectedIndex != 1;
            _pierDiameter.Enabled = circular;
            _pierLength.Enabled = !circular;
            _pierWidth.Enabled = !circular;
        }

        private void HookPreviewEvents(Control root)
        {
            foreach (Control child in root.Controls)
            {
                NumericUpDown numeric = child as NumericUpDown;
                if (numeric != null)
                    numeric.ValueChanged += OnPreviewChanged;
                TextBox text = child as TextBox;
                if (text != null)
                    text.TextChanged += OnPreviewChanged;
                ComboBox combo = child as ComboBox;
                if (combo != null)
                    combo.SelectedIndexChanged += OnPreviewChanged;
                CheckBox checkBox = child as CheckBox;
                if (checkBox != null)
                    checkBox.CheckedChanged += OnPreviewChanged;
                if (child.HasChildren)
                    HookPreviewEvents(child);
            }
        }

        private static void AddSection(TableLayoutPanel table, string text)
        {
            int row = table.RowCount++;
            Label header = new Label();
            header.Text = text;
            header.AutoSize = true;
            header.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            header.ForeColor = Color.FromArgb(46, 96, 76);
            header.Margin = new Padding(0, row == 0 ? 2 : 12, 0, 4);
            table.Controls.Add(header, 0, row);
            table.SetColumnSpan(header, 2);
        }

        private static NumericUpDown AddNumeric(TableLayoutPanel table,
            string labelText, string name, decimal minimum, decimal maximum,
            int decimalPlaces, decimal increment, double value)
        {
            NumericUpDown input = new NumericUpDown();
            input.Name = name;
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.DecimalPlaces = decimalPlaces;
            input.Increment = increment;
            input.ThousandsSeparator = true;
            input.TextAlign = HorizontalAlignment.Right;
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(4, 2, 0, 2);
            input.Value = Math.Max(minimum, Math.Min(maximum, (decimal)value));
            AddRow(table, labelText, input);
            return input;
        }

        private static ComboBox AddCombo(TableLayoutPanel table,
            string labelText, string name, string[] values, int selectedIndex)
        {
            ComboBox combo = new ComboBox();
            combo.Name = name;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.AddRange(values);
            combo.SelectedIndex = Math.Max(0,
                Math.Min(values.Length - 1, selectedIndex));
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(4, 2, 0, 2);
            AddRow(table, labelText, combo);
            return combo;
        }

        private static TextBox AddText(TableLayoutPanel table,
            string labelText, string name, string value, string toolTipText)
        {
            TextBox input = new TextBox();
            input.Name = name;
            input.Text = value ?? String.Empty;
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(4, 2, 0, 2);
            if (!String.IsNullOrWhiteSpace(toolTipText))
            {
                ToolTip toolTip = new ToolTip();
                toolTip.SetToolTip(input, toolTipText);
                input.Tag = toolTip;
            }
            AddRow(table, labelText, input);
            return input;
        }

        private static CheckBox AddCheckBox(TableLayoutPanel table,
            string text, string name, bool isChecked)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Name = name;
            checkBox.Text = text;
            checkBox.Checked = isChecked;
            checkBox.AutoSize = true;
            checkBox.Margin = new Padding(4, 3, 0, 3);
            int row = table.RowCount++;
            table.Controls.Add(checkBox, 0, row);
            table.SetColumnSpan(checkBox, 2);
            return checkBox;
        }

        private static void AddRow(TableLayoutPanel table,
            string labelText, Control input)
        {
            int row = table.RowCount++;
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 4, 6, 3);
            table.Controls.Add(label, 0, row);
            table.Controls.Add(input, 1, row);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Control control in FindControls(this))
                {
                    ToolTip toolTip = control.Tag as ToolTip;
                    if (toolTip != null)
                        toolTip.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private static IEnumerable<Control> FindControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in FindControls(child))
                    yield return descendant;
            }
        }
    }
}
