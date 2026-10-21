using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.DrawingTranslation.UI
{
    internal sealed class DrawingTranslationToolboxControl : UserControl
    {
        internal DrawingTranslationToolboxControl()
        {
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(245, 246, 248);
            Padding = new Padding(14);
            AutoScroll = true;

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            Label title = new Label { Text = "图纸文字翻译",
                Font = new Font(Font.FontFamily, 13, FontStyle.Bold),
                AutoSize = true, Margin = new Padding(0, 4, 0, 6) };
            layout.Controls.Add(title);
            Label status = new Label { Text = "先设置接口，再选择图纸文字执行翻译。",
                AutoSize = true, MaximumSize = new Size(680, 0),
                ForeColor = Color.FromArgb(88, 92, 98),
                Margin = new Padding(0, 0, 0, 16) };
            layout.Controls.Add(status);

            AddButton(layout, "OpenDrawingTranslationSettings",
                "翻译设置", "STPVTRANSLATESETTINGS");
            AddButton(layout, "RunDrawingTranslationSelfTest",
                "接口自检（不改图纸）", "STPVTRANSLATETEST");
            AddButton(layout, "RunDrawingTranslation",
                "直接替换", "STPVTRANSLATE");
            AddButton(layout, "RunDrawingTranslationConfirm",
                "确认后替换", "STPVTRANSLATECONFIRM");
            AddButton(layout, "ShowDrawingTranslationInfo",
                "查看模块状态和日志", "STPVTRANSLATEINFO");
        }

        private static void AddButton(TableLayoutPanel layout, string name,
            string text, string command)
        {
            Button button = new Button { Name = name, Text = text,
                Height = 44, MinimumSize = new Size(0, 44), Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 8, 0), Margin = new Padding(0, 0, 0, 8),
                FlatStyle = FlatStyle.Flat, BackColor = Color.White,
                ForeColor = Color.FromArgb(32, 35, 40) };
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 194, 201);
            button.Click += delegate { SendCommand(command); };
            layout.Controls.Add(button);
        }

        private static void SendCommand(string command)
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            document.SendStringToExecute(command + " ", true, false, false);
        }
    }
}
