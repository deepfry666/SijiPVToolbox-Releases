using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.Windows;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.UI
{
    public static class ToolboxPalette
    {
        private const string PaletteTitle = "四季光伏工具箱";
        private static readonly Size MinimumPaletteSize = new Size(340, 520);
        private static readonly Size HomePaletteSize = new Size(360, 580);
        private const int PaletteLayoutVersion = 6;
        private const int LayoutCorrectionPasses = 5;
        private static readonly TimeSpan LayoutCorrectionInterval =
            TimeSpan.FromMilliseconds(120.0);
        private static readonly Guid PaletteId =
            new Guid("23745D36-A798-4120-BB89-BD64492DE53D");
        private static PaletteSet _palette;
        private static ToolboxShellControl _shell;
        private static bool _layoutMigrationRequired = NeedsLayoutMigration();
        private static bool _layoutCorrectionAttached;
        private static PaletteSet _scheduledPalette;
        private static ToolboxShellControl _scheduledShell;
        private static Size _pendingPaletteSize;
        private static int _remainingLayoutCorrectionPasses;
        private static bool _completeMigrationAfterCorrection;
        private static DateTime _nextLayoutCorrectionUtc = DateTime.MinValue;

        public static void Show()
        {
            ShowHome();
        }

        public static void ShowHome()
        {
            EnsureCreated();
            _shell.ShowHome();
            ApplyTitle(_palette);
            _palette.Visible = true;
            ApplyLayoutMigration();
        }

        public static void ShowModule(string moduleId)
        {
            EnsureCreated();
            if (!_shell.ShowModule(moduleId))
            {
                AcApplication.ShowAlertDialog(
                    "功能模块未加载，请重新安装四季光伏工具箱。 ");
                return;
            }
            ApplyTitle(_palette);
            _palette.Visible = true;
            ApplyLayoutMigration();
        }

        private static void EnsureCreated()
        {
            if (_palette != null && !_palette.IsDisposed &&
                _shell != null && !_shell.IsDisposed)
            {
                ApplyTitle(_palette);
                return;
            }

            DisposeIncompleteState();

            PaletteSet palette = null;
            ToolboxShellControl shell = null;
            try
            {
                palette = new PaletteSet(PaletteTitle, PaletteId);
                palette.Style = PaletteSetStyles.ShowAutoHideButton |
                    PaletteSetStyles.ShowCloseButton |
                    PaletteSetStyles.ShowPropertiesMenu;
                ApplyMinimumSize(palette);
                palette.DockEnabled = DockSides.Left | DockSides.Right;
                shell = new ToolboxShellControl();
                palette.Add("工具箱", shell);
                palette.DeviceIndependentSize = new System.Windows.Size(
                    HomePaletteSize.Width, HomePaletteSize.Height);
                ApplyTitle(palette);

                _palette = palette;
                _shell = shell;
            }
            catch
            {
                DisposeSafely(palette, shell);
                throw;
            }
        }

        private static void DisposeIncompleteState()
        {
            CancelLayoutCorrection();
            PaletteSet palette = _palette;
            ToolboxShellControl shell = _shell;
            _palette = null;
            _shell = null;
            DisposeSafely(palette, shell);
        }

        private static void ApplyLayoutMigration()
        {
            if (!_layoutMigrationRequired || _palette == null ||
                _palette.IsDisposed)
                return;

            ApplyMinimumSize(_palette);
            // AutoCAD may restore the v0.4.x page-expanded size after the palette is shown.
            ScheduleLayoutCorrection();
        }

        private static void ScheduleLayoutCorrection()
        {
            PaletteSet palette = _palette;
            ToolboxShellControl shell = _shell;
            if (palette == null || palette.IsDisposed ||
                shell == null || shell.IsDisposed)
            {
                return;
            }

            _scheduledPalette = palette;
            _scheduledShell = shell;
            _pendingPaletteSize = HomePaletteSize;
            _remainingLayoutCorrectionPasses = LayoutCorrectionPasses;
            _completeMigrationAfterCorrection = true;
            _nextLayoutCorrectionUtc = DateTime.MinValue;

            TryApplyMigrationSize(palette, _pendingPaletteSize);

            if (_layoutCorrectionAttached)
                return;

            try
            {
                AcApplication.Idle += OnLayoutCorrectionIdle;
                _layoutCorrectionAttached = true;
            }
            catch
            {
                // A later page opening will retry if AutoCAD rejects event hookup.
            }
        }

        private static void OnLayoutCorrectionIdle(object sender, EventArgs eventArgs)
        {
            if (!_layoutCorrectionAttached)
                return;

            if (!ReferenceEquals(_palette, _scheduledPalette) ||
                !ReferenceEquals(_shell, _scheduledShell) ||
                _scheduledPalette == null || _scheduledPalette.IsDisposed ||
                _scheduledShell == null || _scheduledShell.IsDisposed)
            {
                CancelLayoutCorrection();
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (now < _nextLayoutCorrectionUtc)
                return;
            _nextLayoutCorrectionUtc = now.Add(LayoutCorrectionInterval);

            try
            {
                bool sizeSatisfied = TryApplyMigrationSize(
                    _scheduledPalette, _pendingPaletteSize);
                _scheduledShell.PerformLayout();
                _scheduledShell.Refresh();

                _remainingLayoutCorrectionPasses--;
                if (_remainingLayoutCorrectionPasses > 0)
                    return;

                bool completeMigration =
                    _completeMigrationAfterCorrection && sizeSatisfied;
                CancelLayoutCorrection();
                if (completeMigration)
                    CompleteLayoutMigration();
            }
            catch
            {
                // AutoCAD can rebuild Palette handles while switching workspaces.
                CancelLayoutCorrection();
            }
        }

        private static bool TryApplyMigrationSize(PaletteSet palette,
            Size requestedSize)
        {
            if (palette == null || palette.IsDisposed)
                return false;

            try
            {
                palette.DeviceIndependentSize = new System.Windows.Size(
                    requestedSize.Width, requestedSize.Height);
                System.Windows.Size applied = palette.DeviceIndependentSize;
                return applied.Width >= requestedSize.Width &&
                    applied.Height >= requestedSize.Height;
            }
            catch
            {
                return false;
            }
        }

        private static void CancelLayoutCorrection()
        {
            if (_layoutCorrectionAttached)
            {
                try
                {
                    AcApplication.Idle -= OnLayoutCorrectionIdle;
                    _layoutCorrectionAttached = false;
                }
                catch
                {
                    // Keep the flag true so a later callback or shutdown retries detach.
                }
            }

            _scheduledPalette = null;
            _scheduledShell = null;
            _pendingPaletteSize = Size.Empty;
            _remainingLayoutCorrectionPasses = 0;
            _completeMigrationAfterCorrection = false;
            _nextLayoutCorrectionUtc = DateTime.MinValue;
        }

        public static void Shutdown()
        {
            CancelLayoutCorrection();
        }

        private static void ApplyMinimumSize(PaletteSet palette)
        {
            palette.DeviceIndependentMinimumSize = MinimumPaletteSize;
        }

        private static bool NeedsLayoutMigration()
        {
            try
            {
                if (!File.Exists(LayoutVersionPath))
                    return true;
                string value = File.ReadAllText(LayoutVersionPath).Trim();
                int version;
                return !Int32.TryParse(value, out version) ||
                    version < PaletteLayoutVersion;
            }
            catch
            {
                return true;
            }
        }

        private static void CompleteLayoutMigration()
        {
            if (!_layoutMigrationRequired)
                return;

            try
            {
                string directory = Path.GetDirectoryName(LayoutVersionPath);
                Directory.CreateDirectory(directory);
                File.WriteAllText(LayoutVersionPath,
                    PaletteLayoutVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _layoutMigrationRequired = false;
            }
            catch
            {
                // A failed marker write only means the migration may retry next launch.
            }
        }

        private static string LayoutVersionPath
        {
            get
            {
                string root = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, "StructuralToolbox",
                    "palette-layout." + CurrentProfileKey + ".version");
            }
        }

        private static string CurrentProfileKey
        {
            get
            {
                string profile = "default";
                try
                {
                    object value = AcApplication.GetSystemVariable("CPROFILE");
                    if (value != null && !String.IsNullOrWhiteSpace(value.ToString()))
                        profile = value.ToString().Trim();
                }
                catch
                {
                }

                using (SHA256 algorithm = SHA256.Create())
                {
                    byte[] digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(profile));
                    return BitConverter.ToString(digest, 0, 8).Replace("-", String.Empty);
                }
            }
        }

        private static void DisposeSafely(PaletteSet palette, ToolboxShellControl shell)
        {
            try
            {
                if (palette != null && !palette.IsDisposed)
                    palette.Dispose();
            }
            catch
            {
            }

            try
            {
                if (shell != null && !shell.IsDisposed)
                    shell.Dispose();
            }
            catch
            {
            }
        }

        private static void ApplyTitle(PaletteSet palette)
        {
            palette.Name = PaletteTitle;
            palette.Text = PaletteTitle;
        }
    }
}
