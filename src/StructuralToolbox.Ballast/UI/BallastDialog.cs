using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Ballast.Core;
using StructuralToolbox.Ballast.Settings;

namespace StructuralToolbox.Ballast.UI
{
    public sealed class BallastDialog : Form
    {
        private readonly ComboBox _shape;
        private readonly Label _dimensionLabel;
        private readonly Panel _dimensionHost;
        private readonly NumericUpDown _diameter;
        private readonly NumericUpDown _rectLength;
        private readonly NumericUpDown _rectWidth;
        private readonly NumericUpDown _crossLineLength;
        private readonly TextBox _layerName;
        private readonly NumericUpDown _maxSideClearance;
        private readonly NumericUpDown _allowOverrun;
        private readonly BallastSettings _settings;

        public BallastDialog(BallastSettings settings, int arrayCount,
            IList<int> rowCounts)
        {
            _settings = settings;
            Text = "配重统一设置";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 246, 248);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9.0f);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48.0f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52.0f));
            Controls.Add(root);

            Label detected = new Label();
            detected.AutoSize = true;
            detected.Text = BuildDetectionText(arrayCount, rowCounts);
            detected.ForeColor = Color.FromArgb(55, 75, 92);
            detected.Margin = new Padding(0, 2, 0, 10);
            root.Controls.Add(detected, 0, 0);
            root.SetColumnSpan(detected, 2);

            _shape = new ComboBox();
            _shape.DropDownStyle = ComboBoxStyle.DropDownList;
            _shape.Items.Add("圆墩");
            _shape.Items.Add("方墩");
            _shape.Dock = DockStyle.Fill;

            _diameter = CreateNumeric(10.0M, 5000.0M, 1, 10.0M);
            _rectLength = CreateNumeric(10.0M, 5000.0M, 1, 10.0M);
            _rectWidth = CreateNumeric(10.0M, 5000.0M, 1, 10.0M);
            _crossLineLength = CreateNumeric(100.0M, 20000.0M, 1, 100.0M);
            _maxSideClearance = CreateNumeric(0.0M, 20000.0M, 1, 10.0M);
            _allowOverrun = CreateNumeric(0.0M, 20000.0M, 1, 10.0M);

            _layerName = new TextBox();
            _layerName.MaxLength = 255;
            _layerName.Dock = DockStyle.Fill;

            AddSection(root, 1, "配重规格");
            AddRow(root, 2, "形状", _shape);

            _dimensionLabel = CreateRowLabel("圆墩直径 (mm)");
            _dimensionHost = new Panel();
            _dimensionHost.Dock = DockStyle.Fill;
            _dimensionHost.Height = 25;
            root.Controls.Add(_dimensionLabel, 0, 3);
            root.Controls.Add(_dimensionHost, 1, 3);

            AddRow(root, 4, "定位线长度 (mm)", _crossLineLength);
            AddRow(root, 5, "输出图层", _layerName);

            AddSection(root, 6, "布置限制");
            AddRow(root, 7, "侧边最大净距 (mm)", _maxSideClearance);
            AddRow(root, 8, "允许超出范围 (mm)", _allowOverrun);

            Label nextStep = new Label();
            nextStep.AutoSize = true;
            nextStep.Text = "下一步将按识别到的组件排数分别设置。";
            nextStep.ForeColor = Color.FromArgb(90, 90, 90);
            nextStep.Margin = new Padding(0, 12, 0, 0);
            root.Controls.Add(nextStep, 0, 9);
            root.SetColumnSpan(nextStep, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.Anchor = AnchorStyles.Right;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Margin = new Padding(0, 14, 0, 0);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.AutoSize = true;
            cancel.MinimumSize = new Size(84, 30);
            cancel.Margin = new Padding(8, 0, 0, 0);
            cancel.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Button reset = new Button();
            reset.Text = "恢复默认";
            reset.AutoSize = true;
            reset.MinimumSize = new Size(84, 30);
            reset.Margin = new Padding(8, 0, 0, 0);
            reset.Click += delegate { Apply(new BallastSettings()); };

            Button ok = new Button();
            ok.Text = "下一步";
            ok.AutoSize = true;
            ok.MinimumSize = new Size(104, 30);
            ok.Click += OnOk;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(reset);
            root.Controls.Add(buttons, 0, 10);
            root.SetColumnSpan(buttons, 2);

            _shape.SelectedIndexChanged += delegate { UpdateShapeFields(); };
            Apply(settings);
            ClientSize = new Size(450, 430);
            MinimumSize = ClientSize;
        }

        private static string BuildDetectionText(int arrayCount,
            IList<int> rowCounts)
        {
            List<string> labels = new List<string>();
            if (rowCounts != null)
                for (int index = 0; index < rowCounts.Count; index++)
                    labels.Add(rowCounts[index] + "排");
            return String.Format("已识别 {0} 个有效阵列；组件排数：{1}。",
                arrayCount, labels.Count == 0 ? "无" : String.Join("、", labels));
        }

        private void UpdateShapeFields()
        {
            _dimensionHost.Controls.Clear();
            if (_shape.SelectedIndex == 1)
            {
                _dimensionLabel.Text = "方墩尺寸 (mm)";
                TableLayoutPanel dimensions = new TableLayoutPanel();
                dimensions.Dock = DockStyle.Fill;
                dimensions.ColumnCount = 3;
                dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
                dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22.0f));
                dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
                dimensions.Controls.Add(_rectLength, 0, 0);
                Label separator = new Label();
                separator.Text = "x";
                separator.TextAlign = ContentAlignment.MiddleCenter;
                separator.Dock = DockStyle.Fill;
                dimensions.Controls.Add(separator, 1, 0);
                dimensions.Controls.Add(_rectWidth, 2, 0);
                _dimensionHost.Controls.Add(dimensions);
            }
            else
            {
                _dimensionLabel.Text = "圆墩直径 (mm)";
                _dimensionHost.Controls.Add(_diameter);
            }
        }

        private static NumericUpDown CreateNumeric(decimal minimum, decimal maximum,
            int decimalPlaces, decimal increment)
        {
            NumericUpDown input = new NumericUpDown();
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.DecimalPlaces = decimalPlaces;
            input.Increment = increment;
            input.TextAlign = HorizontalAlignment.Right;
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(4, 2, 0, 2);
            return input;
        }

        private static void AddSection(TableLayoutPanel layout, int row, string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(122, 0, 122);
            label.Margin = new Padding(0, row == 0 ? 2 : 12, 0, 3);
            layout.Controls.Add(label, 0, row);
            layout.SetColumnSpan(label, 2);
        }

        private static Label CreateRowLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Margin = new Padding(0, 3, 8, 3);
            label.Anchor = AnchorStyles.Left;
            return label;
        }

        private static void AddRow(TableLayoutPanel layout, int row,
            string labelText, Control control)
        {
            layout.Controls.Add(CreateRowLabel(labelText), 0, row);
            layout.Controls.Add(control, 1, row);
        }

        private void Apply(BallastSettings settings)
        {
            settings.Normalize();
            _shape.SelectedIndex = settings.Shape == BallastShape.Circle ? 0 : 1;
            _diameter.Value = ClampDecimal(settings.Diameter, _diameter);
            _rectLength.Value = ClampDecimal(settings.RectLength, _rectLength);
            _rectWidth.Value = ClampDecimal(settings.RectWidth, _rectWidth);
            _crossLineLength.Value = ClampDecimal(settings.CrossLineLength,
                _crossLineLength);
            _layerName.Text = settings.LayerName;
            _maxSideClearance.Value = ClampDecimal(settings.MaxSideClearance,
                _maxSideClearance);
            _allowOverrun.Value = ClampDecimal(settings.AllowOverrun,
                _allowOverrun);
            UpdateShapeFields();
        }

        private void OnOk(object sender, EventArgs eventArgs)
        {
            if (String.IsNullOrWhiteSpace(_layerName.Text))
            {
                MessageBox.Show(this, "输出图层名称不能为空。", "自动布置配重",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_shape.SelectedIndex < 0)
                _shape.SelectedIndex = 0;

            _settings.Shape = _shape.SelectedIndex == 0
                ? BallastShape.Circle : BallastShape.Rect;
            _settings.Diameter = (double)_diameter.Value;
            _settings.RectLength = (double)_rectLength.Value;
            _settings.RectWidth = (double)_rectWidth.Value;
            _settings.CrossLineLength = (double)_crossLineLength.Value;
            _settings.LayerName = _layerName.Text.Trim();
            _settings.MaxSideClearance = (double)_maxSideClearance.Value;
            _settings.AllowOverrun = (double)_allowOverrun.Value;
            _settings.Normalize();
            DialogResult = DialogResult.OK;
            Close();
        }

        private static decimal ClampDecimal(double value, NumericUpDown control)
        {
            decimal result = (decimal)value;
            return Math.Max(control.Minimum, Math.Min(control.Maximum, result));
        }
    }
}
