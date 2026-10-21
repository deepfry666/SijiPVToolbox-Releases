using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using StructuralToolbox.DrawingTranslation.Api;

namespace StructuralToolbox.DrawingTranslation.UI
{
    internal sealed class TranslationApiSettingsDialog : Form
    {
        private readonly TranslationApiSettingsStore _store;
        private readonly TextBox _endpoint;
        private readonly ComboBox _model;
        private readonly Button _models;
        private readonly TextBox _key;
        private readonly ComboBox _language;
        private readonly ComboBox _proxyMode;
        private readonly TextBox _proxyAddress;
        private readonly TextBox _glossary;
        private readonly CheckBox _cache;
        private readonly NumericUpDown _cacheDays;
        private readonly Button _clearCache;
        private readonly Label _status;
        private readonly Button _test;

        internal TranslationApiSettingsDialog(TranslationApiSettingsStore store)
        {
            _store = store;
            Text = "翻译 API 设置";
            Name = "TranslationApiSettingsDialog";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(660, 590);
            ClientSize = new Size(760, 650);
            ShowInTaskbar = false;

            TableLayoutPanel root = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(12),
                ColumnCount = 1, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            Panel scrollHost = new Panel { Name = "ApiSettingsScrollHost",
                Dock = DockStyle.Fill, AutoScroll = true };
            root.Controls.Add(scrollHost, 0, 0);
            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 7,
                Padding = new Padding(0, 0, 6, 0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 6; row++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 166));
            scrollHost.Controls.Add(layout);

            _endpoint = AddTextField(layout, 0, "接口地址", "ApiEndpoint");

            AddLabel(layout, 1, "模型");
            TableLayoutPanel modelRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));
            modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            _model = new ComboBox { Name = "ApiModel", Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList };
            modelRow.Controls.Add(_model, 0, 0);
            _models = new Button { Name = "FetchModelsButton",
                Text = "获取模型", Dock = DockStyle.Fill };
            _models.Click += FetchModels;
            modelRow.Controls.Add(_models, 2, 0);
            layout.Controls.Add(modelRow, 1, 1);

            _key = AddTextField(layout, 2, "API 密钥", "ApiKey");
            _key.UseSystemPasswordChar = true;

            AddLabel(layout, 3, "目标语言");
            _language = new ComboBox { Name = "ApiTargetLanguage",
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            _language.Items.AddRange(new object[] {
                "English", "简体中文", "日本語" });
            layout.Controls.Add(_language, 1, 3);

            AddLabel(layout, 4, "网络连接");
            _proxyMode = new ComboBox { Name = "ApiProxyModeCombo",
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList };
            _proxyMode.Items.AddRange(new object[] {
                "直连（不使用系统代理）", "跟随系统代理", "自定义代理地址" });
            _proxyMode.SelectedIndexChanged += delegate {
                _proxyAddress.Enabled = _proxyMode.SelectedIndex == 2;
            };
            layout.Controls.Add(_proxyMode, 1, 4);

            _proxyAddress = AddTextField(layout, 5, "代理地址",
                "ApiProxyAddress");

            GroupBox advanced = new GroupBox {
                Name = "TranslationAdvancedGroup", Text = "术语表与缓存（可选）",
                Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
            TableLayoutPanel advancedLayout = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(8),
                ColumnCount = 4, RowCount = 3 };
            advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
            advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            advancedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            advancedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            advancedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            advanced.Controls.Add(advancedLayout);
            advancedLayout.Controls.Add(new Label { Text = "术语表",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft },
                0, 0);
            _glossary = new TextBox { Name = "ApiGlossary",
                Dock = DockStyle.Fill, Multiline = true,
                ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
            advancedLayout.Controls.Add(_glossary, 1, 0);
            advancedLayout.SetColumnSpan(_glossary, 3);
            _cache = new CheckBox { Name = "TranslationCacheEnabled",
                Text = "启用译文缓存", Dock = DockStyle.Fill,
                AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            advancedLayout.Controls.Add(_cache, 0, 1);
            advancedLayout.SetColumnSpan(_cache, 2);
            advancedLayout.Controls.Add(new Label { Text = "有效期（天）",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight },
                2, 1);
            _cacheDays = new NumericUpDown { Name = "TranslationCacheDays",
                Minimum = 1, Maximum = 365, Value = 30, Dock = DockStyle.Fill };
            advancedLayout.Controls.Add(_cacheDays, 3, 1);
            _clearCache = new Button { Name = "ClearTranslationCacheButton",
                Text = "清空缓存", Dock = DockStyle.Right,
                Size = new Size(92, 30) };
            _clearCache.Click += delegate {
                TranslationCache.ForCurrentUser().Clear();
                _status.Text = "译文缓存已清空。";
            };
            advancedLayout.Controls.Add(_clearCache, 3, 2);
            layout.Controls.Add(advanced, 0, 6);
            layout.SetColumnSpan(advanced, 2);

            _status = new Label { Name = "ApiTestStatusLabel",
                Dock = DockStyle.Fill, AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                Text = "连接状态会显示在这里。" };
            root.Controls.Add(_status, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false };
            root.Controls.Add(buttons, 0, 2);
            Button cancel = new Button { Name = "CancelApiSettingsButton",
                Text = "取消", Size = new Size(88, 32),
                DialogResult = DialogResult.Cancel };
            Button save = new Button { Name = "SaveApiSettingsButton",
                Text = "保存", Size = new Size(88, 32) };
            save.Click += SaveSettings;
            _test = new Button { Name = "TestApiButton",
                Text = "测试连接", Size = new Size(100, 32) };
            _test.Click += TestConnection;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            buttons.Controls.Add(_test);
            CancelButton = cancel;
            AcceptButton = save;

            TranslationApiSettings current = _store.Load();
            _endpoint.Text = current.Endpoint;
            if (!String.IsNullOrWhiteSpace(current.Model))
                _model.Items.Add(current.Model.Trim());
            if (_model.Items.Count > 0)
                _model.SelectedIndex = 0;
            _key.Text = current.ApiKey;
            _language.Text = current.TargetLanguage;
            _proxyMode.SelectedIndex = current.ProxyMode ==
                TranslationProxyMode.System ? 1 :
                current.ProxyMode == TranslationProxyMode.Custom ? 2 : 0;
            _proxyAddress.Text = current.ProxyAddress;
            _proxyAddress.Enabled = _proxyMode.SelectedIndex == 2;
            _glossary.Text = current.Glossary ?? String.Empty;
            _cache.Checked = current.EnableCache;
            _cacheDays.Value = Math.Max(1, Math.Min(365, current.CacheDays));
        }

        private static void AddLabel(TableLayoutPanel layout, int row,
            string text)
        {
            layout.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        }

        private static TextBox AddTextField(TableLayoutPanel layout, int row,
            string label, string name)
        {
            AddLabel(layout, row, label);
            TextBox input = new TextBox { Name = name, Dock = DockStyle.Fill };
            layout.Controls.Add(input, 1, row);
            return input;
        }

        private TranslationApiSettings ReadSettings()
        {
            return new TranslationApiSettings {
                Endpoint = _endpoint.Text,
                Model = _model.Text,
                ApiKey = _key.Text,
                TargetLanguage = _language.Text,
                Workflow = TranslationWorkflow.ConfirmBeforeReplace,
                ProxyMode = _proxyMode.SelectedIndex == 1
                    ? TranslationProxyMode.System
                    : _proxyMode.SelectedIndex == 2
                        ? TranslationProxyMode.Custom
                        : TranslationProxyMode.Direct,
                ProxyAddress = _proxyAddress.Text,
                Glossary = _glossary.Text,
                EnableCache = _cache.Checked,
                CacheDays = Decimal.ToInt32(_cacheDays.Value)
            };
        }

        private void SaveSettings(object sender, EventArgs args)
        {
            try
            {
                _store.Save(ReadSettings());
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "API 设置",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void FetchModels(object sender, EventArgs args)
        {
            _models.Enabled = false;
            _models.Text = "获取中...";
            _status.Text = "正在获取可用模型...";
            try
            {
                TranslationApiSettings settings = ReadSettings();
                settings.ValidateConnection();
                IList<string> models;
                using (OpenAiCompatibleTranslator translator =
                    OpenAiCompatibleTranslator.Create(settings,
                        TimeSpan.FromSeconds(30)))
                {
                    models = await translator.ListModelsAsync(settings,
                        CancellationToken.None);
                }
                string previous = _model.Text;
                _model.BeginUpdate();
                try
                {
                    _model.Items.Clear();
                    foreach (string model in models)
                        if (!_model.Items.Contains(model))
                            _model.Items.Add(model);
                    int previousIndex = _model.FindStringExact(previous);
                    _model.SelectedIndex = previousIndex >= 0
                        ? previousIndex : (_model.Items.Count > 0 ? 0 : -1);
                }
                finally { _model.EndUpdate(); }
                _status.Text = String.Format(
                    "已获取 {0} 个模型，请从下拉列表选择。", models.Count);
            }
            catch (Exception exception)
            {
                _status.Text = "获取模型失败：" + exception.Message;
                MessageBox.Show(this, exception.Message, "获取模型",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed)
                {
                    _models.Enabled = true;
                    _models.Text = "获取模型";
                }
            }
        }

        private async void TestConnection(object sender, EventArgs args)
        {
            _test.Enabled = false;
            _test.Text = "测试中...";
            _status.Text = "正在测试连接...";
            string failure = null;
            try
            {
                TranslationApiSettings settings = ReadSettings();
                settings.Validate();
                using (OpenAiCompatibleTranslator translator =
                    OpenAiCompatibleTranslator.Create(settings,
                        TimeSpan.FromSeconds(60)))
                {
                    IList<string> translated = await translator.TranslateAsync(
                        settings, new List<string> { "结构工具箱连接测试" },
                        CancellationToken.None);
                    _status.Text = "连接成功，示例译文：" + translated[0];
                }
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                _status.Text = "连接失败：" + failure;
            }
            finally
            {
                if (!IsDisposed)
                {
                    _test.Enabled = true;
                    _test.Text = "测试连接";
                }
            }
            if (failure != null)
                MessageBox.Show(this, failure, "测试连接",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
