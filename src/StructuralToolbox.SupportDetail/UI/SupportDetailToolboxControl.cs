using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using StructuralToolbox.SupportDetail.Core;
using StructuralToolbox.SupportDetail.Settings;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.SupportDetail.UI
{
    internal sealed class SupportDetailToolboxControl : UserControl
    {
        private readonly SupportDetailDiagram _diagram;
        private readonly Label _parameterSummary;
        private readonly Label _status;
        private readonly ToolTip _toolTip;

        public SupportDetailToolboxControl()
        {
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(245, 246, 248);

            Panel scrollHost = new Panel();
            scrollHost.Name = "SupportDetailScrollHost";
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.Padding = new Padding(12, 10, 12, 10);
            Controls.Add(scrollHost);

            TableLayoutPanel content = new TableLayoutPanel();
            content.Name = "SupportDetailPageContent";
            content.Dock = DockStyle.Top;
            content.AutoSize = true;
            content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            content.ColumnCount = 1;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            scrollHost.Controls.Add(content);

            Label section = new Label();
            section.Text = "当前大样参数";
            section.AutoSize = true;
            section.Font = new Font(SystemFonts.MessageBoxFont,
                FontStyle.Bold);
            section.ForeColor = Color.FromArgb(122, 0, 122);
            section.Margin = new Padding(0, 0, 0, 6);
            content.Controls.Add(section);

            _parameterSummary = new Label();
            _parameterSummary.Name = "SupportDetailParameterSummary";
            _parameterSummary.AutoSize = true;
            _parameterSummary.MaximumSize = new Size(410, 0);
            _parameterSummary.ForeColor = Color.FromArgb(48, 52, 58);
            _parameterSummary.Margin = new Padding(0, 0, 0, 10);
            content.Controls.Add(_parameterSummary);

            _diagram = new SupportDetailDiagram();
            _diagram.Name = "SupportDetailToolboxDiagram";
            _diagram.Dock = DockStyle.Top;
            _diagram.Height = 280;
            _diagram.Margin = new Padding(0, 0, 0, 8);
            content.Controls.Add(_diagram);

            Label dimensionLegend = new Label();
            dimensionLegend.Name = "SupportDetailToolboxLegend";
            dimensionLegend.Text =
                "基准：组件阵列低端为 0，沿斜面向高端为 +s。" +
                "r、s、R、S 为中心线尺寸；D 或 Bx×By 为支墩外轮廓。";
            dimensionLegend.AutoSize = true;
            dimensionLegend.MaximumSize = new Size(410, 0);
            dimensionLegend.ForeColor = Color.FromArgb(80, 84, 90);
            dimensionLegend.Margin = new Padding(0, 0, 0, 8);
            content.Controls.Add(dimensionLegend);

            _status = new Label();
            _status.Name = "SupportDetailToolboxStatus";
            _status.AutoSize = true;
            _status.MaximumSize = new Size(410, 0);
            _status.Padding = new Padding(7, 5, 7, 5);
            _status.Margin = new Padding(0, 0, 0, 2);
            content.Controls.Add(_status);

            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Name = "SupportDetailActionFooter";
            footer.Dock = DockStyle.Bottom;
            footer.Height = 116;
            footer.Padding = new Padding(12, 8, 12, 10);
            footer.ColumnCount = 2;
            footer.RowCount = 2;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50.0f));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50.0f));
            footer.BackColor = Color.FromArgb(232, 234, 238);
            Controls.Add(footer);

            Button create = CreateActionButton("CreateSupportDetail",
                "新建支架大样");
            create.BackColor = Color.FromArgb(122, 0, 122);
            create.ForeColor = Color.White;
            create.FlatAppearance.BorderColor = Color.FromArgb(122, 0, 122);
            create.Click += delegate
            {
                SendCommand("STPVSUPPORTDETAIL", "正在打开支架大样参数。 ");
            };
            footer.Controls.Add(create, 0, 0);

            Button edit = CreateActionButton("EditSupportDetail",
                "编辑选中实例");
            edit.Click += delegate
            {
                SendCommand("STPVSUPPORTDETAILEDIT", "请在图面选择支架构件。 ");
            };
            footer.Controls.Add(edit, 1, 0);

            Button inspect = CreateActionButton("InspectSupportDetail",
                "查看实例信息");
            inspect.Click += delegate
            {
                SendCommand("STPVSUPPORTDETAILINFO", "请在图面选择支架构件。 ");
            };
            footer.Controls.Add(inspect, 0, 1);

            Button delete = CreateActionButton("DeleteSupportDetail",
                "删除选中实例");
            delete.ForeColor = Color.FromArgb(155, 42, 42);
            delete.Click += delegate
            {
                SendCommand("STPVSUPPORTDETAILDELETE", "请在图面选择支架构件。 ");
            };
            footer.Controls.Add(delete, 1, 1);

            _toolTip = new ToolTip();
            _toolTip.SetToolTip(create, "输入参数并在图面指定大样基点");
            _toolTip.SetToolTip(edit, "选择任意工具生成构件并回读整组参数");
            _toolTip.SetToolTip(inspect, "读取所选实例的参数与关联实体数量");
            _toolTip.SetToolTip(delete, "确认后删除所选实例的全部工具实体");

            VisibleChanged += OnVisibleChanged;
            RefreshPreview();
        }

        private void OnVisibleChanged(object sender, EventArgs eventArgs)
        {
            if (Visible)
                RefreshPreview();
        }

        private void RefreshPreview()
        {
            SupportDetailSettings settings = SupportDetailSettingsStore.Load();
            settings.Normalize();
            _diagram.Settings = settings;
            string rail = settings.RailPositionMode ==
                SupportDetailPositionMode.Automatic
                ? "R=" + settings.RailCenterSpacing.ToString("0.#",
                    CultureInfo.CurrentCulture)
                : "r=手动";
            string pier = settings.PierShape == SupportDetailPierShape.Circular
                ? "D=" + settings.PierDiameter.ToString("0.#",
                    CultureInfo.CurrentCulture)
                : "Bx×By=" + settings.PierLength.ToString("0.#",
                    CultureInfo.CurrentCulture) + "×" +
                    settings.PierWidth.ToString("0.#",
                        CultureInfo.CurrentCulture);
            _parameterSummary.Text = String.Format(CultureInfo.CurrentCulture,
                "N={0}  α={1:0.###}°  G={2:0.#} mm  {3}\n" +
                "P={4}  H={5:0.#} mm  E={6:0.#} mm  {7} mm  h={8:0.#} mm",
                settings.ComponentCount, settings.SlopeDegrees,
                settings.ComponentGap, rail, settings.PierCount,
                settings.ColumnHeight, settings.BeamOverhang, pier,
                settings.PierHeight);

            SupportDetailBuildResult result =
                SupportDetailGeometryBuilder.Build(settings);
            if (!result.IsValid)
            {
                SetStatus(String.Join("；", result.Errors), true);
                return;
            }
            string message = result.Warnings.Count == 0
                ? String.Format(CultureInfo.CurrentCulture,
                    "参数可生成：斜梁 {0:0.#} mm，立柱 {1} 根。",
                    result.Geometry.BeamLength, result.Geometry.Piers.Count)
                : String.Join("；", result.Warnings);
            SetStatus(message, result.Warnings.Count > 0);
        }

        private void SendCommand(string command, string pendingText)
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                SetStatus("请先打开一张图纸。", true);
                return;
            }
            SetStatus(pendingText, false);
            document.SendStringToExecute(command + " ", true, false, false);
        }

        private void SetStatus(string text, bool warning)
        {
            _status.Text = text;
            _status.BackColor = warning
                ? Color.FromArgb(255, 244, 224)
                : Color.FromArgb(231, 243, 236);
            _status.ForeColor = warning
                ? Color.FromArgb(145, 78, 17)
                : Color.FromArgb(39, 95, 63);
        }

        private static Button CreateActionButton(string name, string text)
        {
            Button button = new Button();
            button.Name = name;
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(180, 184, 190);
            button.BackColor = Color.White;
            button.Font = new Font(SystemFonts.MessageBoxFont,
                FontStyle.Regular);
            return button;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                VisibleChanged -= OnVisibleChanged;
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
