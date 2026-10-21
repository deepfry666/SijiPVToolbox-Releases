using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Ballast.Core;
using StructuralToolbox.Ballast.Settings;

namespace StructuralToolbox.Ballast.UI
{
    public sealed class BallastArrayRulesDialog : Form
    {
        private readonly BallastSettings _settings;
        private readonly IList<BallastArrayInfo> _arrays;
        private readonly ListBox _arrayList;
        private readonly ComboBox _shape;
        private readonly NumericUpDown _diameter;
        private readonly NumericUpDown _rectLength;
        private readonly NumericUpDown _rectWidth;
        private readonly NumericUpDown _height;
        private readonly NumericUpDown _crossLineLength;
        private readonly ComboBox _countMode;
        private readonly NumericUpDown _manualCount;
        private readonly NumericUpDown _ballastSpacing;
        private readonly NumericUpDown _frameSpacing;
        private readonly ComboBox _placementMode;
        private readonly NumericUpDown _frontClearance;
        private readonly BallastPlacementDiagram _placementDiagram;
        private readonly Label _shapeLabel;
        private readonly Panel _shapeHost;
        private bool _loading;
        private readonly Dictionary<string, BallastArrayRule> _rules =
            new Dictionary<string, BallastArrayRule>(StringComparer.OrdinalIgnoreCase);

        public BallastArrayRulesDialog(BallastSettings settings,
            IList<BallastArrayInfo> arrays)
        {
            _settings = settings;
            _arrays = arrays ?? new List<BallastArrayInfo>();
            Text = "按阵列设置配重";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 246, 248);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9.0f);

            LoadRules();
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245.0f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42.0f));
            Controls.Add(root);

            _arrayList = new ListBox();
            _arrayList.Dock = DockStyle.Fill;
            _arrayList.IntegralHeight = false;
            for (int index = 0; index < _arrays.Count; index++)
            {
                BallastArrayInfo array = _arrays[index];
                _arrayList.Items.Add(String.Format("{0}   {1}排 x {2}列   ({3:0},{4:0})",
                    array.Id, array.RowsAlongLong, array.ColumnsAlongShort,
                    array.Center.X, array.Center.Y));
            }
            _arrayList.SelectedIndexChanged += delegate { LoadSelected(); };
            root.Controls.Add(_arrayList, 0, 0);

            TableLayoutPanel editor = new TableLayoutPanel();
            editor.Dock = DockStyle.Fill;
            editor.Padding = new Padding(12, 0, 0, 0);
            editor.ColumnCount = 2;
            editor.RowCount = 12;
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44.0f));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56.0f));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34.0f));
            for (int row = 1; row <= 9; row++)
                editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 42.0f));
            editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 42.0f));
            root.Controls.Add(editor, 1, 0);

            Label title = new Label();
            title.Name = "SelectedArrayTitle";
            title.AutoSize = true;
            title.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(122, 0, 122);
            editor.Controls.Add(title, 0, 0);
            editor.SetColumnSpan(title, 2);

            _shape = new ComboBox();
            _shape.DropDownStyle = ComboBoxStyle.DropDownList;
            _shape.Items.Add("圆墩");
            _shape.Items.Add("方墩");
            _shape.Dock = DockStyle.Fill;
            _shape.SelectedIndexChanged += delegate { UpdateShapeFields(); };
            AddRow(editor, 1, "形状", _shape);

            _diameter = CreateNumeric(10, 5000, 1, 10);
            _rectLength = CreateNumeric(10, 5000, 1, 10);
            _rectWidth = CreateNumeric(10, 5000, 1, 10);
            _height = CreateNumeric(1, 5000, 1, 10);
            _shapeLabel = CreateLabel("圆墩直径 (mm)");
            _shapeHost = new Panel();
            _shapeHost.Dock = DockStyle.Fill;
            _shapeHost.MinimumSize = new Size(0, 28);
            editor.Controls.Add(_shapeLabel, 0, 2);
            editor.Controls.Add(_shapeHost, 1, 2);

            _crossLineLength = CreateNumeric(100, 20000, 1, 100);
            AddRow(editor, 3, "定位线长度 (mm)", _crossLineLength);
            _countMode = new ComboBox();
            _countMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _countMode.Items.Add("自动：组件排数 + 1");
            _countMode.Items.Add("手动指定");
            _countMode.Dock = DockStyle.Fill;
            AddRow(editor, 4, "配重数量模式", _countMode);
            _manualCount = CreateNumeric(1, 50, 0, 1);
            AddRow(editor, 5, "手动配重数量", _manualCount);
            _ballastSpacing = CreateNumeric(100, 20000, 1, 10);
            AddRow(editor, 6, "配重中心间距 S (mm)", _ballastSpacing);
            _frameSpacing = CreateNumeric(100, 20000, 1, 10);
            AddRow(editor, 7, "榀中心距 (mm)", _frameSpacing);
            _placementMode = new ComboBox();
            _placementMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _placementMode.Items.Add("默认前后对称");
            _placementMode.Items.Add("自定义前侧");
            _placementMode.Dock = DockStyle.Fill;
            AddRow(editor, 8, "前后布置", _placementMode);
            _frontClearance = CreateNumeric(0, 20000, 1, 10);
            AddRow(editor, 9, "前侧中心边距 F (mm)", _frontClearance);
            _placementDiagram = new BallastPlacementDiagram();
            editor.Controls.Add(_placementDiagram, 0, 10);
            editor.SetColumnSpan(_placementDiagram, 2);
            AddRow(editor, 11, "配重高度 (mm)", _height);
            _placementMode.SelectedIndexChanged += delegate
            {
                _frontClearance.Enabled = _placementMode.SelectedIndex == 1;
                _placementDiagram.Visible = _placementMode.SelectedIndex == 1;
            };
            _countMode.SelectedIndexChanged += delegate
            {
                _manualCount.Enabled = _countMode.SelectedIndex == 1;
            };

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            Button applyRows = CreateButton("套用同排数", 110);
            applyRows.Click += delegate { ApplyToSameRows(); };
            Button applyAll = CreateButton("套用全部", 100);
            applyAll.Click += delegate { ApplyToAll(); };
            Button cancel = CreateButton("取消", 84);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Button ok = CreateButton("确定生成", 100);
            ok.Click += OnOk;
            actions.Controls.Add(applyRows);
            actions.Controls.Add(applyAll);
            actions.Controls.Add(cancel);
            actions.Controls.Add(ok);
            root.Controls.Add(actions, 0, 1);
            root.SetColumnSpan(actions, 2);

            ClientSize = new Size(850, 760);
            MinimumSize = ClientSize;
            if (_arrays.Count > 0)
                _arrayList.SelectedIndex = 0;
        }

        private void LoadRules()
        {
            for (int index = 0; index < _arrays.Count; index++)
            {
                BallastArrayInfo array = _arrays[index];
                BallastArrayRule saved = FindArrayRule(array.Id);
                BallastRowRule rowRule = FindRowRule(array.RowsAlongLong);
                _rules[array.Id] = saved ?? new BallastArrayRule
                {
                    ArrayId = array.Id,
                    Shape = _settings.Shape,
                    Diameter = _settings.Diameter,
                    RectLength = _settings.RectLength,
                    RectWidth = _settings.RectWidth,
                    Height = _settings.Height,
                    CrossLineLength = _settings.CrossLineLength,
                    CountMode = rowRule == null ? _settings.CountMode : rowRule.CountMode,
                    ManualCount = rowRule == null ? _settings.ManualCount : rowRule.ManualCount,
                    BallastCenterSpacing = rowRule == null
                        ? _settings.BallastCenterSpacing : rowRule.BallastCenterSpacing,
                    FrameCenterSpacing = rowRule == null
                        ? _settings.FrameCenterSpacing : rowRule.FrameCenterSpacing,
                    PlacementMode = rowRule == null
                        ? _settings.PlacementMode : rowRule.PlacementMode,
                    FrontCenterClearance = rowRule == null
                        ? _settings.FrontCenterClearance : rowRule.FrontCenterClearance,
                    ColorMode = rowRule == null ? _settings.ColorMode : rowRule.ColorMode,
                    ColorIndex = rowRule == null ? _settings.ColorIndex : rowRule.ColorIndex,
                    ColorRed = rowRule == null ? _settings.ColorRed : rowRule.ColorRed,
                    ColorGreen = rowRule == null ? _settings.ColorGreen : rowRule.ColorGreen,
                    ColorBlue = rowRule == null ? _settings.ColorBlue : rowRule.ColorBlue
                };
            }
        }

        private void LoadSelected()
        {
            if (_arrayList.SelectedIndex < 0 || _arrayList.SelectedIndex >= _arrays.Count)
                return;
            BallastArrayInfo array = _arrays[_arrayList.SelectedIndex];
            BallastArrayRule rule = _rules[array.Id];
            _loading = true;
            Control title = FindControl("SelectedArrayTitle");
            if (title != null)
                title.Text = String.Format("{0}：{1}排 x {2}列，中心 ({3:0.##}, {4:0.##})",
                    array.Id, array.RowsAlongLong, array.ColumnsAlongShort,
                    array.Center.X, array.Center.Y);
            _shape.SelectedIndex = rule.Shape == BallastShape.Circle ? 0 : 1;
            _diameter.Value = Clamp(rule.Diameter, _diameter);
            _rectLength.Value = Clamp(rule.RectLength, _rectLength);
            _rectWidth.Value = Clamp(rule.RectWidth, _rectWidth);
            _height.Value = Clamp(rule.Height <= 0.0 ? _settings.Height : rule.Height,
                _height);
            _crossLineLength.Value = Clamp(rule.CrossLineLength, _crossLineLength);
            _countMode.SelectedIndex = rule.CountMode == BallastCountMode.AutoByRows ? 0 : 1;
            _manualCount.Value = Clamp(rule.ManualCount, _manualCount);
            _ballastSpacing.Value = Clamp(rule.BallastCenterSpacing, _ballastSpacing);
            _frameSpacing.Value = Clamp(rule.FrameCenterSpacing, _frameSpacing);
            _placementMode.SelectedIndex = rule.PlacementMode ==
                BallastPlacementMode.CustomFront ? 1 : 0;
            _frontClearance.Value = Clamp(rule.FrontCenterClearance,
                _frontClearance);
            _frontClearance.Enabled = _placementMode.SelectedIndex == 1;
            _placementDiagram.Visible = _placementMode.SelectedIndex == 1;
            _manualCount.Enabled = _countMode.SelectedIndex == 1;
            UpdateShapeFields();
            _loading = false;
        }

        private BallastArrayRule FindArrayRule(string id)
        {
            if (_settings.ArrayRules == null) return null;
            for (int index = 0; index < _settings.ArrayRules.Count; index++)
            {
                BallastArrayRule rule = _settings.ArrayRules[index];
                if (rule != null && String.Equals(rule.ArrayId, id,
                    StringComparison.OrdinalIgnoreCase)) return rule;
            }
            return null;
        }

        private BallastRowRule FindRowRule(int rows)
        {
            if (_settings.RowRules == null) return null;
            for (int index = 0; index < _settings.RowRules.Count; index++)
            {
                BallastRowRule rule = _settings.RowRules[index];
                if (rule != null && rule.RowsAlongLong == rows) return rule;
            }
            return null;
        }

        private void SaveSelected()
        {
            if (_loading || _arrayList.SelectedIndex < 0)
                return;
            BallastArrayInfo array = _arrays[_arrayList.SelectedIndex];
            BallastArrayRule rule = _rules[array.Id];
            rule.Shape = _shape.SelectedIndex == 0 ? BallastShape.Circle : BallastShape.Rect;
            rule.Diameter = (double)_diameter.Value;
            rule.RectLength = (double)_rectLength.Value;
            rule.RectWidth = (double)_rectWidth.Value;
            rule.Height = (double)_height.Value;
            rule.CrossLineLength = (double)_crossLineLength.Value;
            rule.CountMode = _countMode.SelectedIndex == 0
                ? BallastCountMode.AutoByRows : BallastCountMode.Manual;
            rule.ManualCount = (int)_manualCount.Value;
            rule.BallastCenterSpacing = (double)_ballastSpacing.Value;
            rule.FrameCenterSpacing = (double)_frameSpacing.Value;
            rule.PlacementMode = _placementMode.SelectedIndex == 1
                ? BallastPlacementMode.CustomFront : BallastPlacementMode.Symmetric;
            rule.FrontCenterClearance = (double)_frontClearance.Value;
        }

        private void ApplyToSameRows()
        {
            SaveSelected();
            if (_arrayList.SelectedIndex < 0) return;
            BallastArrayInfo selected = _arrays[_arrayList.SelectedIndex];
            BallastArrayRule source = _rules[selected.Id];
            for (int index = 0; index < _arrays.Count; index++)
                if (_arrays[index].RowsAlongLong == selected.RowsAlongLong)
                    _rules[_arrays[index].Id] = Copy(source, _arrays[index].Id);
            LoadSelected();
        }

        private void ApplyToAll()
        {
            SaveSelected();
            if (_arrayList.SelectedIndex < 0) return;
            BallastArrayRule source = _rules[_arrays[_arrayList.SelectedIndex].Id];
            for (int index = 0; index < _arrays.Count; index++)
                _rules[_arrays[index].Id] = Copy(source, _arrays[index].Id);
            LoadSelected();
        }

        private void OnOk(object sender, EventArgs eventArgs)
        {
            SaveSelected();
            List<BallastArrayRule> result = new List<BallastArrayRule>();
            for (int index = 0; index < _arrays.Count; index++)
                result.Add(Copy(_rules[_arrays[index].Id], _arrays[index].Id));
            _settings.ArrayRules = result;
            _settings.Normalize();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateShapeFields()
        {
            _shapeHost.Controls.Clear();
            if (_shape.SelectedIndex == 1)
            {
                _shapeLabel.Text = "方墩尺寸 (mm)";
                TableLayoutPanel panel = new TableLayoutPanel();
                panel.Dock = DockStyle.Fill;
                panel.ColumnCount = 3;
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                panel.Controls.Add(_rectLength, 0, 0);
                Label x = new Label(); x.Text = "x"; x.TextAlign = ContentAlignment.MiddleCenter; x.Dock = DockStyle.Fill;
                panel.Controls.Add(x, 1, 0);
                panel.Controls.Add(_rectWidth, 2, 0);
                _shapeHost.Controls.Add(panel);
            }
            else
            {
                _shapeLabel.Text = "圆墩直径 (mm)";
                _shapeHost.Controls.Add(_diameter);
            }
        }

        private Control FindControl(string name)
        {
            foreach (Control control in Controls)
            {
                Control found = FindControl(control, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Control FindControl(Control parent, string name)
        {
            if (parent.Name == name) return parent;
            foreach (Control child in parent.Controls)
            {
                Control found = FindControl(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static BallastArrayRule Copy(BallastArrayRule source, string id)
        {
            return new BallastArrayRule
            {
                ArrayId = id, Shape = source.Shape, Diameter = source.Diameter,
                RectLength = source.RectLength, RectWidth = source.RectWidth,
                Height = source.Height,
                CrossLineLength = source.CrossLineLength, CountMode = source.CountMode,
                ManualCount = source.ManualCount, BallastCenterSpacing = source.BallastCenterSpacing,
                FrameCenterSpacing = source.FrameCenterSpacing,
                PlacementMode = source.PlacementMode,
                FrontCenterClearance = source.FrontCenterClearance,
                ColorMode = source.ColorMode,
                ColorIndex = source.ColorIndex,
                ColorRed = source.ColorRed,
                ColorGreen = source.ColorGreen,
                ColorBlue = source.ColorBlue
            };
        }

        private static Label CreateLabel(string text)
        {
            Label label = new Label(); label.Text = text; label.AutoSize = false;
            label.Dock = DockStyle.Fill; label.Margin = new Padding(0, 3, 8, 3);
            label.TextAlign = ContentAlignment.MiddleLeft; return label;
        }

        private static void AddRow(TableLayoutPanel panel, int row, string text, Control control)
        {
            panel.Controls.Add(CreateLabel(text), 0, row); panel.Controls.Add(control, 1, row);
        }

        private static NumericUpDown CreateNumeric(decimal min, decimal max, int decimals, decimal increment)
        {
            NumericUpDown value = new NumericUpDown { Minimum = min, Maximum = max,
                DecimalPlaces = decimals, Increment = increment, TextAlign = HorizontalAlignment.Right,
                Dock = DockStyle.Fill, Margin = new Padding(4, 2, 0, 2) }; return value;
        }

        private static Button CreateButton(string text, int width)
        {
            Button button = new Button { Text = text, Width = width, Height = 30,
                Margin = new Padding(4, 5, 0, 0) }; return button;
        }

        private static decimal Clamp(double value, NumericUpDown control)
        {
            decimal result = (decimal)value; return Math.Max(control.Minimum, Math.Min(control.Maximum, result));
        }
    }
}
