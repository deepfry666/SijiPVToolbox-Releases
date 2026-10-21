using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StructuralToolbox.Layout.UI;
using LayoutSettingsModel = StructuralToolbox.Layout.Settings.LayoutSettings;

internal static class LayoutDialogTests
{
    private static int _passed;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            VerifyScale(1.00f, false, null);
            VerifyScale(1.25f, false, null);
            VerifyScale(1.50f, false, null);
            VerifyScale(2.00f, true, args.Length > 0 ? args[0] : null);
            Console.WriteLine("PASS {0} layout dialog scale tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL layout dialog tests: {0}",
                exception.Message);
            return 1;
        }
    }

    private static void VerifyScale(float scale, bool useLongestValues,
        string screenshotPath)
    {
        LayoutSettingsModel settings = new LayoutSettingsModel();
        if (useLongestValues)
        {
            settings.OutputLayer = new string('长', 255);
            settings.MaximumCapacityKwp = "999999999.999";
            settings.StringSize = 500;
            settings.Rows = 8;
        }
        using (LayoutDialog dialog = new LayoutDialog(settings))
        {
            dialog.Scale(new SizeF(scale, scale));
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(-4000, -4000);
            dialog.ShowInTaskbar = false;
            dialog.Show();
            Application.DoEvents();
            dialog.PerformLayout();

            Require(dialog.ClientSize.Width > 0 && dialog.ClientSize.Height > 0,
                "dialog has no client area at scale " + scale);
            Control accept = Find(dialog, "AcceptLayout");
            Control cancel = Find(dialog, "CancelLayout");
            Control diagram = Find(dialog, "LayoutDiagram");
            Require(accept != null && cancel != null && diagram != null,
                "required controls are missing at scale " + scale);
            RequireVisibleInside(dialog, accept, "accept button", scale);
            RequireVisibleInside(dialog, cancel, "cancel button", scale);
            RequireVisibleInside(dialog, diagram, "engineering diagram", scale);
            Require(accept.Width >= accept.PreferredSize.Width &&
                accept.Height >= accept.PreferredSize.Height,
                "accept button text is clipped at scale " + scale);
            Require(diagram.AccessibleDescription.Contains("L/W") &&
                diagram.AccessibleDescription.Contains("Gx/Gy") &&
                diagram.AccessibleDescription.Contains("Ax/Ay") &&
                diagram.AccessibleDescription.Contains("开放线障碍") &&
                diagram.AccessibleDescription.Contains("净距 O") &&
                diagram.AccessibleDescription.Contains("alpha"),
                "diagram symbols do not match the input fields");

            settings.Rows = 3;
            StructuralToolbox.Layout.Core.LayoutOptions options =
                settings.ToOptions(
                    new StructuralToolbox.Layout.Core.LayoutVector(1, 0));
            Require(options.AllowedRows.Length == 3 &&
                options.AllowedRows[0] == 1 && options.AllowedRows[1] == 2 &&
                options.AllowedRows[2] == 3,
                "maximum row count did not expand to 1..M");

            if (!String.IsNullOrWhiteSpace(screenshotPath))
            {
                string directory = Path.GetDirectoryName(screenshotPath);
                if (!String.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                using (Bitmap bitmap = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(bitmap,
                        new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(screenshotPath);
                }
            }
        }
        _passed++;
    }

    private static Control Find(Control parent, string name)
    {
        if (String.Equals(parent.Name, name, StringComparison.Ordinal))
            return parent;
        foreach (Control child in parent.Controls)
        {
            Control found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void RequireVisibleInside(Form dialog, Control control,
        string context, float scale)
    {
        Rectangle bounds = dialog.RectangleToClient(
            control.Parent.RectangleToScreen(control.Bounds));
        Require(bounds.Left >= 0 && bounds.Top >= 0 &&
            bounds.Right <= dialog.ClientSize.Width &&
            bounds.Bottom <= dialog.ClientSize.Height,
            context + " is outside the dialog at scale " + scale);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
