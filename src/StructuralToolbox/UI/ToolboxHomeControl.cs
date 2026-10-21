using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Contracts;

namespace StructuralToolbox.UI
{
    internal sealed class ToolboxModuleRequestedEventArgs : EventArgs
    {
        public ToolboxModuleRequestedEventArgs(string moduleId)
        {
            ModuleId = moduleId;
        }

        public string ModuleId { get; private set; }
    }

    internal sealed class ToolboxHomeControl : UserControl
    {
        public event EventHandler<ToolboxModuleRequestedEventArgs> ModuleRequested;

        public ToolboxHomeControl(IList<IToolboxModule> modules,
            IList<string> loadErrors)
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(245, 246, 248);
            Padding = new Padding(14);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            Controls.Add(layout);

            Label brand = new Label();
            brand.Text = "四季光伏工具箱";
            brand.Font = new Font(Font.FontFamily, 15.0f, FontStyle.Bold);
            brand.AutoSize = true;
            brand.Margin = new Padding(0, 4, 0, 4);
            layout.Controls.Add(brand);

            Label description = new Label();
            description.Text = "FourJTooLs";
            description.AutoSize = true;
            description.ForeColor = Color.FromArgb(88, 92, 98);
            description.Margin = new Padding(0, 0, 0, 22);
            layout.Controls.Add(description);

            Label version = new Label();
            version.Name = "ToolboxVersion";
            version.Text = "版本 1.0.8";
            version.AutoSize = true;
            version.ForeColor = Color.FromArgb(105, 108, 114);
            version.Margin = new Padding(0, 0, 0, 18);
            layout.Controls.Add(version);

            Label section = new Label();
            section.Text = "光伏结构";
            section.Font = new Font(Font.FontFamily, 10.0f, FontStyle.Bold);
            section.AutoSize = true;
            section.Margin = new Padding(0, 0, 0, 8);
            layout.Controls.Add(section);

            if (modules != null)
            {
                foreach (IToolboxModule module in modules)
                    layout.Controls.Add(CreateToolButton(module));
            }

            if (loadErrors != null && loadErrors.Count > 0)
            {
                Label error = new Label();
                error.Name = "ModuleLoadStatus";
                error.Text = "部分功能模块未加载，请重新安装工具箱。";
                error.AutoSize = true;
                error.MaximumSize = new Size(300, 0);
                error.ForeColor = Color.FromArgb(170, 45, 45);
                error.Margin = new Padding(0, 8, 0, 0);
                layout.Controls.Add(error);
            }
        }

        private Button CreateToolButton(IToolboxModule module)
        {
            Button button = new Button();
            button.Name = module.HomeButtonName;
            button.Text = module.DisplayName;
            button.Tag = module.Id;
            button.Height = 52;
            button.MinimumSize = new Size(0, 52);
            button.Dock = DockStyle.Top;
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 0, 8, 0);
            button.Margin = new Padding(0, 0, 0, 8);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 194, 201);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 239, 242);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(32, 35, 40);
            button.Font = new Font(Font.FontFamily, 10.0f, FontStyle.Regular);
            button.Click += OnOpenModule;
            return button;
        }

        private void OnOpenModule(object sender, EventArgs eventArgs)
        {
            Button button = sender as Button;
            string moduleId = button == null ? null : button.Tag as string;
            EventHandler<ToolboxModuleRequestedEventArgs> handler = ModuleRequested;
            if (handler != null && !String.IsNullOrWhiteSpace(moduleId))
                handler(this, new ToolboxModuleRequestedEventArgs(moduleId));
        }
    }
}
