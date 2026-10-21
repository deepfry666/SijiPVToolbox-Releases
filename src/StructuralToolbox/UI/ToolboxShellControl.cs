using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Contracts;

namespace StructuralToolbox.UI
{
    internal sealed class ToolboxModulePageEventArgs : EventArgs
    {
        public ToolboxModulePageEventArgs(IToolboxModule module)
        {
            Module = module;
        }

        public IToolboxModule Module { get; private set; }
    }

    internal sealed class ToolboxShellControl : UserControl
    {
        public event EventHandler<ToolboxModulePageEventArgs> ModulePageShown;

        private readonly Panel _navigation;
        private readonly Button _backButton;
        private readonly Label _pageTitle;
        private readonly Panel _contentHost;
        private readonly ToolboxHomeControl _homePage;
        private readonly ToolTip _toolTip;
        private readonly Dictionary<string, IToolboxModule> _modules;
        private readonly Dictionary<string, Control> _pages;

        public ToolboxShellControl()
            : this(DiscoverModules())
        {
        }

        internal ToolboxShellControl(IList<IToolboxModule> modules)
            : this(modules, new List<string>())
        {
        }

        private ToolboxShellControl(ModuleDiscoveryResult discovery)
            : this(discovery.Modules, discovery.Errors)
        {
        }

        private ToolboxShellControl(IList<IToolboxModule> modules,
            IList<string> errors)
        {
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(245, 246, 248);
            _modules = new Dictionary<string, IToolboxModule>(
                StringComparer.OrdinalIgnoreCase);
            _pages = new Dictionary<string, Control>(
                StringComparer.OrdinalIgnoreCase);
            if (modules != null)
            {
                foreach (IToolboxModule module in modules)
                    _modules.Add(module.Id, module);
            }

            _contentHost = new Panel();
            _contentHost.Dock = DockStyle.Fill;
            _contentHost.BackColor = BackColor;
            Controls.Add(_contentHost);

            _navigation = new Panel();
            _navigation.Dock = DockStyle.Top;
            _navigation.Height = 40;
            _navigation.Padding = new Padding(6, 5, 8, 5);
            _navigation.BackColor = Color.FromArgb(232, 234, 238);
            Controls.Add(_navigation);

            TableLayoutPanel navigationLayout = new TableLayoutPanel();
            navigationLayout.Dock = DockStyle.Fill;
            navigationLayout.ColumnCount = 2;
            navigationLayout.RowCount = 1;
            navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34.0f));
            navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            navigationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            _navigation.Controls.Add(navigationLayout);

            _backButton = new Button();
            _backButton.Text = "←";
            _backButton.Dock = DockStyle.Fill;
            _backButton.FlatStyle = FlatStyle.Flat;
            _backButton.FlatAppearance.BorderSize = 0;
            _backButton.Font = new Font(Font.FontFamily, 12.0f, FontStyle.Regular);
            _backButton.Click += OnBack;
            navigationLayout.Controls.Add(_backButton, 0, 0);

            _pageTitle = new Label();
            _pageTitle.Dock = DockStyle.Fill;
            _pageTitle.TextAlign = ContentAlignment.MiddleLeft;
            _pageTitle.AutoEllipsis = true;
            _pageTitle.Padding = new Padding(8, 0, 0, 0);
            _pageTitle.Font = new Font(Font.FontFamily, 9.0f, FontStyle.Bold);
            navigationLayout.Controls.Add(_pageTitle, 1, 0);

            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_backButton, "返回首页");

            _homePage = new ToolboxHomeControl(modules, errors);
            _homePage.ModuleRequested += OnOpenModule;
            _contentHost.Controls.Add(_homePage);
            ShowHome();
        }

        public void ShowHome()
        {
            _navigation.Visible = false;
            ShowPage(_homePage);
        }

        public bool ShowModule(string moduleId)
        {
            IToolboxModule module;
            if (String.IsNullOrWhiteSpace(moduleId) ||
                !_modules.TryGetValue(moduleId, out module))
            {
                return false;
            }

            Control page;
            if (!_pages.TryGetValue(module.Id, out page))
            {
                page = module.CreateControl();
                if (page == null)
                    throw new InvalidOperationException(
                        "模块没有提供可显示页面：" + module.DisplayName);
                page.Dock = DockStyle.Fill;
                _pages.Add(module.Id, page);
                _contentHost.Controls.Add(page);
            }

            _pageTitle.Text = module.DisplayName;
            _navigation.Visible = true;
            ShowPage(page);
            EventHandler<ToolboxModulePageEventArgs> handler = ModulePageShown;
            if (handler != null)
                handler(this, new ToolboxModulePageEventArgs(module));
            return true;
        }

        private void ShowPage(Control page)
        {
            foreach (Control child in _contentHost.Controls)
                child.Visible = ReferenceEquals(child, page);
            page.BringToFront();
        }

        private void OnOpenModule(object sender,
            ToolboxModuleRequestedEventArgs eventArgs)
        {
            ShowModule(eventArgs.ModuleId);
        }

        private void OnBack(object sender, EventArgs eventArgs)
        {
            ShowHome();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _homePage.ModuleRequested -= OnOpenModule;
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private static ModuleDiscoveryResult DiscoverModules()
        {
            IList<string> errors;
            IList<IToolboxModule> modules = ToolboxModuleLoader.Discover(out errors);
            return new ModuleDiscoveryResult(modules, errors);
        }

        private sealed class ModuleDiscoveryResult
        {
            public ModuleDiscoveryResult(IList<IToolboxModule> modules,
                IList<string> errors)
            {
                Modules = modules;
                Errors = errors;
            }

            public IList<IToolboxModule> Modules { get; private set; }
            public IList<string> Errors { get; private set; }
        }
    }
}
