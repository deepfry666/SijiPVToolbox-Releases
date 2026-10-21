using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using LayoutSettingsModel = StructuralToolbox.Layout.Settings.LayoutSettings;

namespace StructuralToolbox.Layout.UI
{
    internal sealed class LayoutDialog : Form
    {
        private readonly Dictionary<string, TextBox> _fields = new Dictionary<string, TextBox>(StringComparer.Ordinal);
        private readonly ComboBox _orientation;
        private readonly NumericUpDown _rows;
        private readonly NumericUpDown _strings;
        public LayoutSettingsModel Settings { get; private set; }

        public LayoutDialog(LayoutSettingsModel settings)
        {
            Settings = settings ?? new LayoutSettingsModel(); Settings.Normalize();
            Text = "组件自动排布 · 独立测试版 v0.1.2"; Name = "LayoutDialog"; StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(720, 520); ClientSize = new Size(780, 610); AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.Sizable; ShowInTaskbar = false;

            TableLayoutPanel root = new TableLayoutPanel(); root.Dock = DockStyle.Fill; root.Padding = new Padding(10); root.ColumnCount = 2; root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            Panel left = new Panel(); left.Dock = DockStyle.Fill; left.AutoScroll = true; root.Controls.Add(left, 0, 0);
            TableLayoutPanel fields = new TableLayoutPanel(); fields.Dock = DockStyle.Top; fields.AutoSize = true; fields.ColumnCount = 2; fields.Padding = new Padding(0, 0, 8, 0);
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); left.Controls.Add(fields);
            AddHeader(fields, "屋面与组件参数");
            AddText(fields, "L (mm)", "Length", Settings.Length.ToString("0.###", CultureInfo.InvariantCulture));
            AddText(fields, "W (mm)", "Width", Settings.Width.ToString("0.###", CultureInfo.InvariantCulture));
            AddText(fields, "P (Wp)", "PowerWp", Settings.PowerWp.ToString("0.###", CultureInfo.InvariantCulture));
            AddText(fields, "Gx (mm)", "ComponentGapAcross", Settings.ComponentGapAcross.ToString("0.###", CultureInfo.InvariantCulture));
            AddText(fields, "Gy (mm)", "ComponentGapAlong", Settings.ComponentGapAlong.ToString("0.###", CultureInfo.InvariantCulture));
            AddText(fields, "Ax (mm)", "ArrayGapAcross", Settings.ArrayGapAcross.ToString("0.###", CultureInfo.InvariantCulture));
            AddText(fields, "Ay (mm)", "ArrayGapAlong", Settings.ArrayGapAlong.ToString("0.###", CultureInfo.InvariantCulture));
            AddText(fields, "α (°)", "SlopeDegrees", Settings.SlopeDegrees.ToString("0.###", CultureInfo.InvariantCulture));
            AddText(fields, "O (mm)", "ObstacleClearance", Settings.ObstacleClearance.ToString("0.###", CultureInfo.InvariantCulture));
            AddHeader(fields, "排布规则");
            Label direction = new Label(); direction.Text = "D（排间/坡向）"; direction.Dock = DockStyle.Fill; direction.TextAlign = ContentAlignment.MiddleLeft; fields.Controls.Add(direction);
            Label directionValue = new Label(); directionValue.Text = "命令中点选第一点 → 第二点"; directionValue.Dock = DockStyle.Fill; directionValue.TextAlign = ContentAlignment.MiddleLeft; fields.Controls.Add(directionValue);
            AddCombo(fields, "组件朝向", out _orientation, new[] { "自动比较", "竖排", "横排" }, Settings.Orientation);
            AddNumeric(fields, "最大阵列排数 M", out _rows, 1, 8, Settings.Rows);
            AddNumeric(fields, "组串数量", out _strings, 1, 500, Settings.StringSize);
            AddText(fields, "最大容量 (kWp)", "MaximumCapacityKwp", Settings.MaximumCapacityKwp);
            AddText(fields, "组件输出图层", "OutputLayer", Settings.OutputLayer, 255);
            Label note = new Label(); note.Text = "M 是允许的最大排数：填 2 会比较 1 排和 2 排方案。容量是硬上限；同容量时优先无冲突、完整矩形。LINE 和开放直线多段线会直接作为障碍，按 O 退距避让。"; note.AutoSize = true; note.MaximumSize = new Size(390, 0); note.Margin = new Padding(0, 8, 0, 8); fields.Controls.Add(note); fields.SetColumnSpan(note, 2);

            Panel right = new Panel(); right.Dock = DockStyle.Fill; root.Controls.Add(right, 1, 0);
            LayoutDiagram diagram = new LayoutDiagram(); right.Controls.Add(diagram);
            Label legend = new Label(); legend.Text = "本页符号与输入一致：L/W、D、Gx/Gy、Ax/Ay、O、α。\n开放线本身即为障碍；O 为组件外轮廓与障碍线之间的净距。屋面边界和障碍在命令行中点选。"; legend.Dock = DockStyle.Top; legend.AutoSize = true; legend.Padding = new Padding(4, 8, 4, 8); right.Controls.Add(legend);

            FlowLayoutPanel buttons = new FlowLayoutPanel(); buttons.FlowDirection = FlowDirection.RightToLeft; buttons.Dock = DockStyle.Fill;
            Button ok = new Button(); ok.Text = "确定并计算"; ok.Name = "AcceptLayout"; ok.AutoSize = true; ok.Click += OnAccept;
            Button cancel = new Button(); cancel.Text = "取消"; cancel.Name = "CancelLayout"; cancel.DialogResult = DialogResult.Cancel; cancel.AutoSize = true;
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel); root.Controls.Add(buttons, 0, 1); root.SetColumnSpan(buttons, 2); AcceptButton = ok; CancelButton = cancel;
        }

        private void AddHeader(TableLayoutPanel table, string text) { Label header = new Label(); header.Text = text; header.Font = new Font(Font, FontStyle.Bold); header.AutoSize = true; header.Padding = new Padding(0, 8, 0, 4); table.Controls.Add(header); table.SetColumnSpan(header, 2); }
        private void AddText(TableLayoutPanel table, string labelText, string key, string value, int maxLength = 40)
        { Label label = new Label(); label.Text = labelText; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; TextBox box = new TextBox(); box.Name = key; box.Text = value ?? String.Empty; box.MaxLength = maxLength; box.Dock = DockStyle.Fill; box.Margin = new Padding(4, 2, 0, 2); table.Controls.Add(label); table.Controls.Add(box); _fields[key] = box; }
        private void AddCombo(TableLayoutPanel table, string labelText, out ComboBox combo, string[] values, string selected)
        { Label label = new Label(); label.Text = labelText; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; combo = new ComboBox(); combo.Name = "Orientation"; combo.DropDownStyle = ComboBoxStyle.DropDownList; combo.Items.AddRange(values); combo.SelectedItem = selected; combo.Dock = DockStyle.Fill; combo.Margin = new Padding(4, 2, 0, 2); table.Controls.Add(label); table.Controls.Add(combo); }
        private void AddNumeric(TableLayoutPanel table, string labelText, out NumericUpDown numeric, int min, int max, int value)
        { Label label = new Label(); label.Text = labelText; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; numeric = new NumericUpDown(); numeric.Name = labelText; numeric.Minimum = min; numeric.Maximum = max; numeric.Value = Math.Max(min, Math.Min(max, value)); numeric.Dock = DockStyle.Left; numeric.Width = 150; numeric.Margin = new Padding(4, 2, 0, 2); table.Controls.Add(label); table.Controls.Add(numeric); }
        private void OnAccept(object sender, EventArgs e)
        {
            try
            {
                Settings.Length = Read("Length"); Settings.Width = Read("Width"); Settings.PowerWp = Read("PowerWp"); Settings.ComponentGapAlong = Read("ComponentGapAlong"); Settings.ComponentGapAcross = Read("ComponentGapAcross"); Settings.ArrayGapAlong = Read("ArrayGapAlong"); Settings.ArrayGapAcross = Read("ArrayGapAcross"); Settings.SlopeDegrees = Read("SlopeDegrees"); Settings.ObstacleClearance = Read("ObstacleClearance"); Settings.MaximumCapacityKwp = _fields["MaximumCapacityKwp"].Text.Trim(); Settings.OutputLayer = _fields["OutputLayer"].Text.Trim(); Settings.Orientation = _orientation.SelectedItem as string ?? "自动比较"; Settings.Rows = (int)_rows.Value; Settings.StringSize = (int)_strings.Value; Settings.Normalize();
                if (String.IsNullOrWhiteSpace(Settings.OutputLayer)) throw new InvalidOperationException("组件输出图层不能为空。");
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        private double Read(string key)
        { double value; string text = _fields[key].Text.Trim(); if (Double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) || Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value; throw new InvalidOperationException("参数 " + key + " 必须是数字。"); }
    }
}
