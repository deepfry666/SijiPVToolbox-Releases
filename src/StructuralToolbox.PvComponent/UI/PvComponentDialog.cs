using System;
using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.PvComponent.Settings;

namespace StructuralToolbox.PvComponent.UI
{
    public sealed class PvComponentDialog : Form
    {
        private readonly TextBox _name;
        private readonly NumericUpDown _length;
        private readonly NumericUpDown _width;
        private readonly NumericUpDown _thickness;
        private readonly TextBox _layer;
        private readonly PvComponentDiagram _diagram;

        public PvComponentDialog(PvComponentSettings settings)
        {
            ResultSettings = settings == null
                ? new PvComponentSettings() : settings.Clone();
            ResultSettings.Normalize();

            Text = "光伏组件构件 · 独立验证版 v0.1.0";
            Name = "PvComponentDialog";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 500);
            ClientSize = new Size(880, 560);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowInTaskbar = false;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320.0f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48.0f));
            Controls.Add(root);

            TableLayoutPanel fields = new TableLayoutPanel();
            fields.Name = "PvComponentFields";
            fields.Dock = DockStyle.Top;
            fields.AutoSize = true;
            fields.ColumnCount = 2;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45.0f));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55.0f));
            root.Controls.Add(fields, 0, 0);

            _name = AddText(fields, "名称 / 型号", "ComponentName",
                ResultSettings.Name);
            _length = AddNumeric(fields, "长度 L (mm)", "ComponentLength",
                100, 20000, ResultSettings.Length);
            _width = AddNumeric(fields, "宽度 W (mm)", "ComponentWidth",
                100, 10000, ResultSettings.Width);
            _thickness = AddNumeric(fields, "厚度 T (mm)",
                "ComponentThickness", 1, 500, ResultSettings.Thickness);
            _layer = AddText(fields, "输出图层", "ComponentLayer",
                ResultSettings.OutputLayer);

            Label unit = new Label();
            unit.Name = "ComponentQuantityUnit";
            unit.Text = "清单单位：块";
            unit.AutoSize = true;
            unit.ForeColor = Color.FromArgb(65, 70, 75);
            unit.Margin = new Padding(4, 14, 4, 4);
            fields.Controls.Add(unit, 0, fields.RowCount);
            fields.SetColumnSpan(unit, 2);

            _diagram = new PvComponentDiagram();
            _diagram.Name = "PvComponentDiagram";
            _diagram.Settings = ResultSettings;
            _diagram.Margin = new Padding(12, 0, 0, 0);
            root.Controls.Add(_diagram, 1, 0);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = false;
            root.Controls.Add(buttons, 0, 1);
            root.SetColumnSpan(buttons, 2);

            Button accept = new Button();
            accept.Name = "AcceptPvComponent";
            accept.Text = "插入 / 更新组件";
            accept.AutoSize = true;
            accept.Padding = new Padding(12, 3, 12, 3);
            accept.Click += OnAccept;
            buttons.Controls.Add(accept);

            Button cancel = new Button();
            cancel.Name = "CancelPvComponent";
            cancel.Text = "取消";
            cancel.AutoSize = true;
            cancel.Padding = new Padding(10, 3, 10, 3);
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);
            AcceptButton = accept;
            CancelButton = cancel;

            _length.ValueChanged += OnPreviewChanged;
            _width.ValueChanged += OnPreviewChanged;
            _thickness.ValueChanged += OnPreviewChanged;
        }

        public PvComponentSettings ResultSettings { get; private set; }

        private void OnPreviewChanged(object sender, EventArgs eventArgs)
        {
            _diagram.Settings = ReadSettings();
        }

        private void OnAccept(object sender, EventArgs eventArgs)
        {
            try
            {
                PvComponentSettings settings = ReadSettings();
                if (String.IsNullOrWhiteSpace(settings.Name))
                    throw new InvalidOperationException("组件名称不能为空。");
                if (String.IsNullOrWhiteSpace(settings.OutputLayer))
                    throw new InvalidOperationException("输出图层不能为空。");
                ResultSettings = settings;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "组件参数错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private PvComponentSettings ReadSettings()
        {
            PvComponentSettings settings = new PvComponentSettings
            {
                Name = _name.Text.Trim(),
                Length = (double)_length.Value,
                Width = (double)_width.Value,
                Thickness = (double)_thickness.Value,
                OutputLayer = _layer.Text.Trim()
            };
            settings.Normalize();
            return settings;
        }

        private static TextBox AddText(TableLayoutPanel fields, string label,
            string name, string value)
        {
            int row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label caption = CreateLabel(label);
            TextBox input = new TextBox();
            input.Name = name;
            input.Text = value ?? String.Empty;
            input.Dock = DockStyle.Top;
            input.Margin = new Padding(4, 7, 4, 7);
            fields.Controls.Add(caption, 0, row);
            fields.Controls.Add(input, 1, row);
            return input;
        }

        private static NumericUpDown AddNumeric(TableLayoutPanel fields,
            string label, string name, decimal minimum, decimal maximum,
            double value)
        {
            int row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label caption = CreateLabel(label);
            NumericUpDown input = new NumericUpDown();
            input.Name = name;
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.DecimalPlaces = 1;
            input.Increment = 1;
            input.ThousandsSeparator = true;
            input.Value = Math.Max(minimum, Math.Min(maximum,
                Convert.ToDecimal(value)));
            input.Dock = DockStyle.Top;
            input.Margin = new Padding(4, 7, 4, 7);
            fields.Controls.Add(caption, 0, row);
            fields.Controls.Add(input, 1, row);
            return input;
        }

        private static Label CreateLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(4, 10, 4, 4);
            return label;
        }
    }
}
