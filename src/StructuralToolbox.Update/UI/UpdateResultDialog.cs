using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using StructuralToolbox.Update;

namespace StructuralToolbox.Update.UI
{
    internal sealed class UpdateResultDialog : Form
    {
        private readonly UpdateManifest _manifest;
        private readonly Version _currentVersion;
        private readonly Button _downloadButton;
        private readonly Button _copyLinkButton;
        private readonly Label _statusLabel;
        private readonly ProgressBar _progressBar;
        private bool _busy;
        private CancellationTokenSource _cancellation;
        private readonly object _cancellationLock = new object();

        public UpdateResultDialog(
            UpdateManifest manifest,
            Version currentVersion)
        {
            if (manifest == null)
                throw new ArgumentNullException("manifest");
            _manifest = manifest;
            _currentVersion = currentVersion;

            Text = "四季光伏工具箱 更新";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(540, 400);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(14);
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowCount = 6;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            Label heading = new Label();
            heading.Text = "发现新版本 v" + manifest.Version.ToString(3) +
                "（当前 v" + currentVersion.ToString(3) + "）";
            heading.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);
            heading.AutoSize = true;
            layout.Controls.Add(heading);

            Label meta = new Label();
            meta.Text = (String.IsNullOrEmpty(manifest.ReleaseDate)
                    ? ""
                    : "发布日期：" + manifest.ReleaseDate + "\r\n") +
                "更新说明：";
            meta.ForeColor = Color.FromArgb(88, 92, 98);
            meta.AutoSize = true;
            layout.Controls.Add(meta);

            ListBox notes = new ListBox();
            notes.Dock = DockStyle.Fill;
            notes.HorizontalScrollbar = true;
            foreach (string note in manifest.Notes)
                notes.Items.Add(note);
            if (manifest.Notes.Count == 0)
                notes.Items.Add("（本次未提供更新说明。）");
            layout.Controls.Add(notes);
            layout.SetRowSpan(notes, 1);

            _statusLabel = new Label();
            _statusLabel.Text = " ";
            _statusLabel.ForeColor = Color.FromArgb(105, 108, 114);
            _statusLabel.AutoSize = true;
            layout.Controls.Add(_statusLabel);

            _progressBar = new ProgressBar();
            _progressBar.Height = 18;
            _progressBar.Dock = DockStyle.Fill;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 100;
            _progressBar.Value = 0;
            layout.Controls.Add(_progressBar);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            buttons.WrapContents = false;

            Button closeButton = new Button();
            closeButton.Text = "关闭";
            closeButton.DialogResult = DialogResult.Cancel;
            closeButton.AutoSize = true;
            buttons.Controls.Add(closeButton);

            _copyLinkButton = new Button();
            _copyLinkButton.Text = "复制下载链接";
            _copyLinkButton.AutoSize = true;
            _copyLinkButton.Click += OnCopyLinkClick;
            buttons.Controls.Add(_copyLinkButton);

            _downloadButton = new Button();
            _downloadButton.Text = "下载并校验";
            _downloadButton.AutoSize = true;
            _downloadButton.Click += OnDownloadClick;
            buttons.Controls.Add(_downloadButton);

            layout.Controls.Add(buttons);
            CancelButton = closeButton;
        }

        public string DownloadedInstallerPath { get; private set; }

        private void OnCopyLinkClick(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(_manifest.PackageUrl.ToString());
                _statusLabel.Text = "下载链接已复制到剪贴板。";
            }
            catch (Exception)
            {
                _statusLabel.Text = "复制失败，请手动记录链接。";
            }
        }

        private void OnDownloadClick(object sender, EventArgs e)
        {
            if (_busy)
            {
                CancelOperation();
                _statusLabel.Text = "正在取消下载…";
                return;
            }
            _busy = true;
            _cancellation = new CancellationTokenSource();
            _downloadButton.Text = "取消下载";
            _copyLinkButton.Enabled = false;
            _statusLabel.Text = "正在下载安装包…";
            _progressBar.Visible = true;

            System.Threading.Thread downloadThread =
                new System.Threading.Thread(RunDownload);
            downloadThread.IsBackground = true;
            downloadThread.SetApartmentState(ApartmentState.STA);
            downloadThread.Start();
        }

        private void RunDownload()
        {
            UpdateService service = new UpdateService();
            long lastReportedBytes = -1L;
            string path = null;
            Exception error = null;
            try
            {
                path = service.DownloadInstaller(_manifest,
                    delegate(long copied, long total)
                    {
                        if (copied == lastReportedBytes)
                            return;
                        lastReportedBytes = copied;
                        ReportProgress(copied, total);
                    },
                    null, _cancellation.Token, delegate(string status)
                    { RunOnUiThread(delegate { _statusLabel.Text = status; }); });
                DownloadedInstallerPath = path;
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
            FinishDownload(error, path);
        }

        private void ReportProgress(long copied, long total)
        {
            RunOnUiThread(delegate
            {
                int percent = total > 0L
                    ? (int)Math.Min(100L, copied * 100L / total)
                    : 50;
                _progressBar.Value = percent;
                _statusLabel.Text = "正在下载安装包…" +
                    FormatMegabytes(copied) +
                    (total > 0L ? " / " + FormatMegabytes(total) : "");
            });
        }

        private void FinishDownload(Exception error, string path)
        {
            RunOnUiThread(delegate
            {
                _busy = false;
                _downloadButton.Text = "下载并校验";
                _progressBar.Visible = false;
                _downloadButton.Enabled = true;
                _copyLinkButton.Enabled = true;
                if (error != null)
                {
                    _statusLabel.ForeColor = Color.FromArgb(180, 60, 56);
                    _statusLabel.Text = error is OperationCanceledException
                        ? "下载已取消，可以重新下载。" : "下载未完成：" + error.Message;
                    return;
                }

                _statusLabel.ForeColor = Color.FromArgb(46, 125, 78);
                _statusLabel.Text = "校验通过。";
                MessageBox.Show(this,
                    "安装包已下载并通过 SHA-256 校验：\r\n" + path +
                    "\r\n\r\n请完全退出 AutoCAD 和探索者后，运行该安装包完成升级。",
                    "下载完成", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                OpenContainingFolder(path);
            });
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

        private static void OpenContainingFolder(string path)
        {
            try
            {
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception)
            {
            }
        }

        private static string FormatMegabytes(long bytes)
        {
            double megabytes = bytes / (1024.0 * 1024.0);
            return megabytes.ToString("F1", null) + " MB";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CancelOperation();
            base.OnFormClosing(e);
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
