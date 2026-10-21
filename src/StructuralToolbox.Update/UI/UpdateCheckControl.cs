using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace StructuralToolbox.Update.UI
{
    internal sealed class UpdateCheckControl : UserControl
    {
        private readonly IList<string> _sourceUrls;
        private readonly Button _checkButton;
        private readonly TextBox _statusTextBox;
        private readonly Label _description;
        private bool _busy;
        private CancellationTokenSource _cancellation;
        private readonly object _cancellationLock = new object();

        public UpdateCheckControl(string defaultSourceUrl)
            : this(new[] { defaultSourceUrl })
        {
        }

        public UpdateCheckControl(IList<string> sourceUrls)
        {
            _sourceUrls = sourceUrls ?? new List<string>();
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(245, 246, 248);
            Padding = new Padding(12);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Name = "UpdatePageLayout";
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(layout);

            Label title = new Label();
            title.Text = "在线更新";
            title.Font = new Font(Font.FontFamily, 15f, FontStyle.Bold);
            title.AutoSize = true;
            title.Margin = new Padding(0, 2, 0, 8);
            layout.Controls.Add(title);

            _description = new Label();
            _description.Name = "UpdateDescription";
            _description.Text =
                "检查新版本并下载安装包，网络不畅时自动切换备用线路。" +
                "下载完成后保存图纸、退出 AutoCAD，再运行安装包完成升级。";
            _description.ForeColor = Color.FromArgb(88, 92, 98);
            _description.AutoSize = true;
            _description.Dock = DockStyle.Top;
            _description.Margin = new Padding(0, 0, 0, 8);
            layout.Controls.Add(_description, 0, 1);

            FlowLayoutPanel actionRow = new FlowLayoutPanel();
            actionRow.Name = "UpdateActionRow";
            actionRow.Dock = DockStyle.Top;
            actionRow.AutoSize = true;
            actionRow.FlowDirection = FlowDirection.RightToLeft;
            actionRow.WrapContents = false;
            actionRow.Margin = new Padding(0, 6, 0, 8);

            _checkButton = new Button();
            _checkButton.Name = "CheckUpdateButton";
            _checkButton.Text = "检查更新";
            _checkButton.AutoSize = true;
            _checkButton.Click += OnCheckClick;
            actionRow.Controls.Add(_checkButton);
            layout.Controls.Add(actionRow, 0, 2);

            _statusTextBox = new TextBox();
            _statusTextBox.Name = "UpdateStatus";
            _statusTextBox.Multiline = true;
            _statusTextBox.ReadOnly = true;
            _statusTextBox.ScrollBars = ScrollBars.Vertical;
            _statusTextBox.Dock = DockStyle.Fill;
            _statusTextBox.MinimumSize = new Size(0, 80);
            _statusTextBox.Text = "尚未检查更新。";
            layout.Controls.Add(_statusTextBox, 0, 3);

            Resize += OnResponsiveResize;
            OnResponsiveResize(this, EventArgs.Empty);
        }

        private void OnResponsiveResize(object sender, EventArgs eventArgs)
        {
            int availableWidth = Math.Max(80, ClientSize.Width - Padding.Horizontal);
            _description.MaximumSize = new Size(availableWidth, 0);
        }

        private void OnCheckClick(object sender, EventArgs e)
        {
            if (_busy)
            {
                CancelOperation();
                ShowStatus("正在取消…");
                return;
            }

            List<Uri> sourceUris = new List<Uri>();
            foreach (string source in _sourceUrls)
            {
                Uri parsed;
                string sourceText = (source ?? "").Trim().Trim('"');
                if (Uri.TryCreate(sourceText, UriKind.Absolute, out parsed) &&
                    (parsed.Scheme == "https" || parsed.Scheme == "file"))
                    sourceUris.Add(parsed);
            }
            if (sourceUris.Count == 0)
            {
                ShowStatus("未配置更新服务，请联系软件维护人员。");
                return;
            }

            _busy = true;
            _cancellation = new CancellationTokenSource();
            _checkButton.Text = "取消检查";
            ShowStatus("正在连接更新服务…");

            Thread checkThread = new Thread(new ThreadStart(delegate
            {
                RunCheck(sourceUris);
            }));
            checkThread.IsBackground = true;
            checkThread.Name = "FourJTooLs update check";
            checkThread.Start();
        }

        private void RunCheck(IList<Uri> sourceUris)
        {
            UpdateManifest manifest = null;
            Version installedVersion = null;
            Exception error = null;
            try
            {
                manifest = new UpdateService().FetchManifest(sourceUris,
                    _cancellation.Token, delegate(string status)
                    { RunOnUiThread(delegate { ShowStatus(status); }); });
                installedVersion = UpdateService.DetectInstalledVersion();
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                lock (_cancellationLock)
                {
                    _cancellation.Dispose();
                    _cancellation = null;
                }
            }

            RunOnUiThread(delegate
            {
                try
                {
                    if (error != null)
                    {
                        ShowStatus(error is OperationCanceledException ? "已取消检查。" :
                            "检查更新失败：" + FormatCheckError(error));
                        return;
                    }
                    ShowCheckResult(manifest, installedVersion);
                }
                finally
                {
                    _busy = false;
                    _checkButton.Text = "检查更新";
                    _checkButton.Enabled = true;
                }
            });
        }

        private void ShowCheckResult(UpdateManifest manifest,
            Version installedVersion)
        {
            if (manifest.IsNewerThan(installedVersion))
            {
                ShowStatus("发现新版本 v" +
                    manifest.Version.ToString(3) + "（当前 v" +
                    installedVersion.ToString(3) + "）。");
                using (UpdateResultDialog dialog =
                    new UpdateResultDialog(manifest, installedVersion))
                {
                    dialog.ShowDialog(FindForm() ?? (IWin32Window)this);
                    if (!String.IsNullOrEmpty(dialog.DownloadedInstallerPath))
                    {
                        ShowStatus("已下载并通过校验：\r\n" +
                            dialog.DownloadedInstallerPath +
                            "\r\n关闭 AutoCAD 后运行它完成升级。");
                    }
                }
            }
            else if (VersionComparer.Compare(manifest.Version, installedVersion) == 0)
            {
                ShowStatus("当前已是最新版本 v" +
                    installedVersion.ToString(3) + "。");
            }
            else
            {
                ShowStatus("远端版本 v" +
                    manifest.Version.ToString(3) +
                    " 比当前安装的 v" +
                    installedVersion.ToString(3) +
                    " 更旧，未提供升级。若远端刚撤回新版本请留意服务器通知。");
            }
        }

        private static string FormatCheckError(Exception exception)
        {
            string message = exception == null ? "" : exception.Message;
            WebException webException = exception as WebException;
            if (webException != null && String.IsNullOrEmpty(message))
                return "网络请求失败。";
            return String.IsNullOrEmpty(message) ? "未知错误。" : message;
        }

        private void RunOnUiThread(Action action)
        {
            if (!IsHandleCreated || IsDisposed)
                return;
            try
            {
                BeginInvoke(new Action(delegate { if (!IsDisposed) action(); }));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ShowStatus(string text)
        {
            StringBuilder builder = new StringBuilder(_statusTextBox.Text);
            if (builder.Length > 4000)
                builder.Remove(0, builder.Length - 2000);
            builder.AppendLine(text.TrimEnd());
            _statusTextBox.Text = builder.ToString();
            _statusTextBox.SelectionStart =
                _statusTextBox.Text.Length;
            _statusTextBox.ScrollToCaret();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) CancelOperation();
            base.Dispose(disposing);
        }

        private void CancelOperation()
        {
            lock (_cancellationLock)
                if (_cancellation != null) _cancellation.Cancel();
        }
    }
}
