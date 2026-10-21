using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Ballast.Core;
using StructuralToolbox.Ballast.Settings;

namespace StructuralToolbox.Ballast.UI
{
    public sealed class BallastRowRulesDialog : Form
    {
        private sealed class Editor
        {
            public int Rows;
            public ComboBox Shape;
            public NumericUpDown Diameter;
            public NumericUpDown RectLength;
            public NumericUpDown RectWidth;
            public NumericUpDown CrossLine;
            public ComboBox Mode;
            public NumericUpDown Count;
            public NumericUpDown BallastSpacing;
            public NumericUpDown FrameSpacing;
            public Panel DimensionHost;
        }

        private readonly BallastSettings _settings;
        private readonly List<Editor> _editors = new List<Editor>();

        public BallastRowRulesDialog(BallastSettings settings, IList<int> rowCounts,
            IDictionary<int, int> arrayCountsByRows)
        {
            _settings = settings;
            Text = "按组件排数设置配重";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 246, 248);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9.0f);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.AutoScroll = true;
            root.Padding = new Padding(12);
            root.ColumnCount = 9;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            Controls.Add(root);
            AddHeader(root, 0, "排数", "阵列数", "形状", "墩尺寸", "定位线",
                "数量模式", "单榀数量", "单榀中心距", "榀中心距");

            int rowIndex = 1;
            if (rowCounts != null)
                for (int index = 0; index < rowCounts.Count; index++)
                {
                    Editor editor = CreateEditor(rowCounts[index], settings);
                    _editors.Add(editor);
                    AddEditorRow(root, rowIndex++, editor, arrayCountsByRows);
                }

            root.RowCount = rowIndex + 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            for (int styleIndex = 1; styleIndex < rowIndex; styleIndex++)
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            FlowLayoutPanel buttons = new FlowLayoutPanel { AutoSize = true,
                Anchor = AnchorStyles.Right, FlowDirection = FlowDirection.RightToLeft };
            Button cancel = MakeButton("取消", 84);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Button ok = MakeButton("确定生成", 100);
            ok.Click += OnOk;
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, rowIndex); root.SetColumnSpan(buttons, 9);
            ClientSize = new Size(1180, 115 + Math.Max(1, _editors.Count) * 62);
            MinimumSize = ClientSize;
        }

        private Editor CreateEditor(int rows, BallastSettings settings)
        {
            BallastRowRule saved = FindRule(settings.RowRules, rows);
            BallastRowRule rule = saved ?? new BallastRowRule
            {
                RowsAlongLong = rows, Shape = settings.Shape, Diameter = settings.Diameter,
                RectLength = settings.RectLength, RectWidth = settings.RectWidth,
                CrossLineLength = settings.CrossLineLength, CountMode = BallastCountMode.AutoByRows,
                ManualCount = rows + 1, BallastCenterSpacing = settings.BallastCenterSpacing,
                FrameCenterSpacing = settings.FrameCenterSpacing
            };
            Editor editor = new Editor { Rows = rows };
            editor.Shape = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            editor.Shape.Items.Add("圆墩"); editor.Shape.Items.Add("方墩");
            editor.Diameter = Numeric(10, 5000, 1, 10);
            editor.RectLength = Numeric(10, 5000, 1, 10);
            editor.RectWidth = Numeric(10, 5000, 1, 10);
            editor.CrossLine = Numeric(100, 20000, 1, 100);
            editor.DimensionHost = new Panel { Dock = DockStyle.Fill, MinimumSize = new Size(0, 28) };
            editor.Mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            editor.Mode.Items.Add("自动"); editor.Mode.Items.Add("手动");
            editor.Count = Numeric(1, 50, 0, 1);
            editor.BallastSpacing = Numeric(100, 20000, 1, 10);
            editor.FrameSpacing = Numeric(100, 20000, 1, 10);
            editor.Shape.SelectedIndexChanged += delegate { UpdateDimension(editor); };
            editor.Mode.SelectedIndexChanged += delegate { editor.Count.Enabled = editor.Mode.SelectedIndex == 1; };
            editor.Shape.SelectedIndex = rule.Shape == BallastShape.Circle ? 0 : 1;
            editor.Diameter.Value = Clamp(rule.Diameter, editor.Diameter);
            editor.RectLength.Value = Clamp(rule.RectLength, editor.RectLength);
            editor.RectWidth.Value = Clamp(rule.RectWidth, editor.RectWidth);
            editor.CrossLine.Value = Clamp(rule.CrossLineLength, editor.CrossLine);
            editor.Mode.SelectedIndex = rule.CountMode == BallastCountMode.AutoByRows ? 0 : 1;
            editor.Count.Value = Clamp(rule.ManualCount, editor.Count);
            editor.BallastSpacing.Value = Clamp(rule.BallastCenterSpacing, editor.BallastSpacing);
            editor.FrameSpacing.Value = Clamp(rule.FrameCenterSpacing, editor.FrameSpacing);
            editor.Count.Enabled = editor.Mode.SelectedIndex == 1;
            UpdateDimension(editor);
            return editor;
        }

        private static void AddEditorRow(TableLayoutPanel root, int row, Editor editor,
            IDictionary<int, int> counts)
        {
            int count;
            root.Controls.Add(new Label { Text = editor.Rows + " 排", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            root.Controls.Add(new Label { Text = counts != null && counts.TryGetValue(editor.Rows, out count) ? count + " 个" : "-", AutoSize = true, Anchor = AnchorStyles.Left }, 1, row);
            root.Controls.Add(editor.Shape, 2, row); root.Controls.Add(editor.DimensionHost, 3, row);
            root.Controls.Add(editor.CrossLine, 4, row); root.Controls.Add(editor.Mode, 5, row);
            root.Controls.Add(editor.Count, 6, row); root.Controls.Add(editor.BallastSpacing, 7, row);
            root.Controls.Add(editor.FrameSpacing, 8, row);
        }

        private static void UpdateDimension(Editor editor)
        {
            editor.DimensionHost.Controls.Clear();
            if (editor.Shape.SelectedIndex == 1)
            {
                TableLayoutPanel panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                panel.Controls.Add(editor.RectLength, 0, 0);
                panel.Controls.Add(new Label { Text = "x", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 1, 0);
                panel.Controls.Add(editor.RectWidth, 2, 0); editor.DimensionHost.Controls.Add(panel);
            }
            else editor.DimensionHost.Controls.Add(editor.Diameter);
        }

        private void OnOk(object sender, EventArgs e)
        {
            List<BallastRowRule> rules = new List<BallastRowRule>();
            for (int index = 0; index < _editors.Count; index++)
            {
                Editor editor = _editors[index];
                rules.Add(new BallastRowRule
                {
                    RowsAlongLong = editor.Rows,
                    Shape = editor.Shape.SelectedIndex == 0 ? BallastShape.Circle : BallastShape.Rect,
                    Diameter = (double)editor.Diameter.Value, RectLength = (double)editor.RectLength.Value,
                    RectWidth = (double)editor.RectWidth.Value, CrossLineLength = (double)editor.CrossLine.Value,
                    CountMode = editor.Mode.SelectedIndex == 0 ? BallastCountMode.AutoByRows : BallastCountMode.Manual,
                    ManualCount = (int)editor.Count.Value, BallastCenterSpacing = (double)editor.BallastSpacing.Value,
                    FrameCenterSpacing = (double)editor.FrameSpacing.Value
                });
            }
            _settings.RowRules = rules;
            _settings.Normalize(); DialogResult = DialogResult.OK; Close();
        }

        private static BallastRowRule FindRule(IList<BallastRowRule> rules, int rows)
        {
            if (rules == null) return null;
            for (int index = 0; index < rules.Count; index++)
                if (rules[index] != null && rules[index].RowsAlongLong == rows) return rules[index];
            return null;
        }

        private static NumericUpDown Numeric(decimal min, decimal max, int decimals, decimal increment)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals,
                Increment = increment, TextAlign = HorizontalAlignment.Right, Dock = DockStyle.Fill };
        }
        private static Button MakeButton(string text, int width)
        {
            Button button = new Button {
                Text = text, Width = width, Height = 30,
                Margin = new Padding(5, 4, 0, 0), FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 194, 201);
            if (String.Equals(text, "确定生成", StringComparison.Ordinal))
            {
                button.BackColor = Color.FromArgb(176, 0, 176);
                button.ForeColor = Color.White;
                button.Font = new Font(button.Font, FontStyle.Bold);
            }
            return button;
        }
        private static decimal Clamp(double value, NumericUpDown control)
        { decimal result = (decimal)value; return Math.Max(control.Minimum, Math.Min(control.Maximum, result)); }
        private static void AddHeader(TableLayoutPanel root, int row, params string[] texts)
        {
            for (int index = 0; index < texts.Length; index++)
                root.Controls.Add(new Label { Text = texts[index], AutoSize = true,
                    Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold), Margin = new Padding(0, 0, 5, 4) }, index, row);
        }
    }
}
