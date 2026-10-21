using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using StructuralToolbox.DrawingTranslation.Api;
using StructuralToolbox.DrawingTranslation.Commands;
using StructuralToolbox.DrawingTranslation.Core;
using StructuralToolbox.DrawingTranslation.Diagnostics;

namespace StructuralToolbox.DrawingTranslation.UI
{
    internal sealed class TranslationReviewDialog : Form
    {
        private readonly DataGridView _grid;
        private readonly Label _status;
        private readonly Button _confirm;
        private readonly Button _retry;
        private readonly Button _cancelTranslation;
        private readonly TranslationApiSettingsStore _settings;
        private CancellationTokenSource _translationCancellation;
        private TranslationProgress _lastProgress;

        internal TranslationReviewDialog(IList<TranslationTarget> targets,
            int skipped)
            : this(targets, skipped,
                TranslationApiSettingsStore.ForCurrentUser())
        {
        }

        // 自动化测试用：换成指定路径的设置存储，避免测试读写当前用户的真实配置。
        internal TranslationReviewDialog(IList<TranslationTarget> targets,
            int skipped,
            TranslationApiSettingsStore settingsStore)
        {
            _settings = settingsStore;
            Text = "图纸文字翻译（" + TranslationDiagnostics.ModuleStamp() + "）";
            Name = "TranslationReviewDialog";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(740, 450);
            ClientSize = new Size(1020, 650);
            ShowInTaskbar = false;

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(12),
                ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(layout);

            Label summary = new Label { Name = "TranslationSummary",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Text = String.Format(
                    "选中文字 {0} 项，跳过 {1} 项；本次确认后替换",
                    targets.Count, skipped) };
            layout.Controls.Add(summary, 0, 0);

            _status = new Label { Name = "TranslationStatus",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Text = "准备翻译..." };
            layout.Controls.Add(_status, 0, 1);

            _grid = new DataGridView { Name = "TranslationReviewGrid",
                Dock = DockStyle.Fill, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AllowUserToResizeRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false, BackgroundColor = SystemColors.Window };
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.DefaultCellStyle.Padding = new Padding(3);
            _grid.RowTemplate.Height = 56;
            _grid.RowTemplate.MinimumHeight = 56;
            _grid.Columns.Add(new DataGridViewTextBoxColumn {
                Name = "Position", HeaderText = "位置", ReadOnly = true,
                FillWeight = 13 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn {
                Name = "Original", HeaderText = "原文", ReadOnly = true,
                FillWeight = 39 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn {
                Name = "Translation", HeaderText = "译文", FillWeight = 48 });
            foreach (TranslationTarget target in targets)
            {
                int row = _grid.Rows.Add(
                    target.IsTableCell
                        ? String.Format("表格 {0}:{1}", target.Row + 1,
                            target.Column + 1)
                        : target.Item.Kind.ToString(),
                    target.Item.RawContent, String.Empty);
                _grid.Rows[row].Tag = target;
            }
            layout.Controls.Add(_grid, 0, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false };
            layout.Controls.Add(buttons, 0, 3);

            Button cancel = new Button { Name = "CancelReviewButton",
                Text = "取消", Size = new Size(88, 32),
                DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel);
            CancelButton = cancel;

            _confirm = new Button { Name = "ConfirmTranslationButton",
                Text = "确认替换",
                Size = new Size(104, 32), Enabled = false };
            _confirm.Click += Apply;
            buttons.Controls.Add(_confirm);
            AcceptButton = _confirm;

            _retry = new Button { Name = "RetryTranslationButton",
                Text = "重试", Size = new Size(88, 32), Visible = false };
            _retry.Click += TranslateAsync;
            buttons.Controls.Add(_retry);

            _cancelTranslation = new Button { Name = "CancelTranslationButton",
                Text = "取消翻译", Size = new Size(100, 32),
                Enabled = false, Visible = false };
            _cancelTranslation.Click += delegate {
                if (_translationCancellation != null)
                    _translationCancellation.Cancel();
            };
            buttons.Controls.Add(_cancelTranslation);

            Shown += delegate { TranslateAsync(this, EventArgs.Empty); };
            FormClosing += delegate {
                if (_translationCancellation != null)
                    _translationCancellation.Cancel();
            };
        }

        internal IList<TranslationEdit> Edits { get; private set; }

        private async void TranslateAsync(object sender, EventArgs args)
        {
            _grid.EndEdit();
            TranslationApiSettings settings;
            try
            {
                settings = _settings.Load();
                settings.Validate();
            }
            catch (Exception exception)
            {
                _status.Text = "API 设置未完成，请返回总设置修改";
                _retry.Visible = true;
                MessageBox.Show(this, exception.Message, "自动翻译",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<string> source = new List<string>();
            foreach (DataGridViewRow row in _grid.Rows)
                source.Add(((TranslationTarget)row.Tag).Item.TranslatableContent);
            _confirm.Enabled = false;
            _retry.Visible = false;
            _cancelTranslation.Enabled = true;
            _cancelTranslation.Visible = true;
            _status.Text = String.Format("正在翻译 0/{0} 项...", source.Count);
            _translationCancellation = new CancellationTokenSource();
            try
            {
                IList<string> results;
                using (OpenAiCompatibleTranslator translator =
                    OpenAiCompatibleTranslator.Create(settings,
                        TimeSpan.FromSeconds(120)))
                {
                    results = await translator.TranslateAsync(settings, source,
                        _translationCancellation.Token, ReportProgress);
                }
                List<string> restoredResults = new List<string>();
                for (int index = 0; index < results.Count; index++)
                {
                    TranslationTarget target =
                        (TranslationTarget)_grid.Rows[index].Tag;
                    string restored;
                    string error;
                    if (!target.Item.Protection.TryRestore(results[index],
                        out restored, out error))
                    {
                        throw new InvalidOperationException(String.Format(
                            "第 {0} 项译文无效：{1}", index + 1, error));
                    }
                    restoredResults.Add(restored);
                }
                for (int index = 0; index < restoredResults.Count; index++)
                    _grid.Rows[index].Cells[2].Value = restoredResults[index];
                int requested = _lastProgress == null ? results.Count :
                    _lastProgress.Requested;
                int cached = _lastProgress == null ? 0 : _lastProgress.Cached;
                _status.Text = String.Format(
                    "已翻译 {0} 项（请求 {1}，缓存命中 {2}），请检查后{3}",
                    results.Count, requested, cached, "确认替换");
                _confirm.Enabled = true;
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed)
                {
                    _status.Text = "翻译已取消，图纸未修改";
                    _retry.Visible = true;
                }
            }
            catch (Exception exception)
            {
                _status.Text = "自动翻译失败，图纸未修改";
                _retry.Visible = true;
                MessageBox.Show(this, exception is ArgumentException
                    ? "翻译服务返回格式无效。" : exception.Message,
                    "自动翻译", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                if (_translationCancellation != null)
                {
                    _translationCancellation.Dispose();
                    _translationCancellation = null;
                }
                if (!IsDisposed)
                {
                    _cancelTranslation.Enabled = false;
                    _cancelTranslation.Visible = false;
                }
            }
        }

        private void ReportProgress(TranslationProgress progress)
        {
            if (progress == null || IsDisposed || !IsHandleCreated)
                return;
            _lastProgress = progress;
            try
            {
                BeginInvoke(new Action(delegate {
                    if (!IsDisposed)
                        _status.Text = String.Format(
                            "正在翻译 {0}/{1} 项（请求 {2}，缓存命中 {3}）...",
                            progress.Completed, progress.Total,
                            progress.Requested, progress.Cached);
                }));
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void Apply(object sender, EventArgs args)
        {
            _grid.EndEdit();
            List<TranslationEdit> edits = new List<TranslationEdit>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                TranslationTarget target = (TranslationTarget)row.Tag;
                string value = Convert.ToString(row.Cells[2].Value) ??
                    String.Empty;
                string error;
                if (!ProtectedText.TryValidateFinal(value, out error))
                {
                    _grid.CurrentCell = row.Cells[2];
                    MessageBox.Show(this, error, "译文校验",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (String.Equals(value, target.Item.RawContent,
                    StringComparison.Ordinal))
                    continue;
                edits.Add(new TranslationEdit(target, value));
            }
            if (edits.Count == 0)
            {
                MessageBox.Show(this, "没有需要应用的译文。", "图纸文字翻译",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Edits = edits;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
