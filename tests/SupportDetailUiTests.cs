using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StructuralToolbox.SupportDetail.Settings;
using StructuralToolbox.SupportDetail.UI;

internal static class SupportDetailUiTests
{
    private static int _passed;

    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyDiagramRendersAndNamesEveryGeometrySymbol();
            VerifyCustomInputsShareTheDiagramContext();
            VerifyDialogLayoutsAtSupportedSizes();
            Console.WriteLine("PASS {0} support detail UI tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL support detail UI tests: {0}",
                exception.Message);
            return 1;
        }
    }

    private static void VerifyDiagramRendersAndNamesEveryGeometrySymbol()
    {
        using (Form host = new Form())
        using (SupportDetailDiagram diagram = new SupportDetailDiagram())
        using (Bitmap bitmap = new Bitmap(520, 360))
        {
            SupportDetailSettings settings = new SupportDetailSettings();
            settings.ComponentCount = 3;
            settings.SlopeDegrees = 17.35;
            settings.PierPositionMode = SupportDetailPositionMode.Manual;
            settings.PierStations = "250, 1500, 3100, 5200";
            settings.PierCount = 4;
            diagram.Settings = settings;
            host.ClientSize = bitmap.Size;
            host.StartPosition = FormStartPosition.Manual;
            host.Location = new Point(-3000, -3000);
            host.ShowInTaskbar = false;
            diagram.Dock = DockStyle.Fill;
            host.Controls.Add(diagram);
            host.Show();
            Application.DoEvents();
            diagram.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));

            int colored = 0;
            for (int y = 0; y < bitmap.Height; y += 2)
                for (int x = 0; x < bitmap.Width; x += 2)
                    if (Distance(bitmap.GetPixel(x, y), diagram.BackColor) > 24)
                        colored++;
            Require(colored > 900, "diagram did not render enough geometry");
            string description = diagram.AccessibleDescription;
            foreach (string symbol in new[]
                { "N", "L", "G", "alpha", "R", "r", "s", "H", "E", "D", "Bx", "h" })
            {
                Require(description.Contains(symbol),
                    "diagram accessibility text omits " + symbol);
            }
            string output = Path.Combine("work", "support-detail-tests",
                "support-detail-diagram.png");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            bitmap.Save(output);
        }
        Pass();
    }

    private static void VerifyCustomInputsShareTheDiagramContext()
    {
        using (SupportDetailDialog dialog = new SupportDetailDialog(
            new SupportDetailSettings()))
        {
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(-4000, -4000);
            dialog.Show();
            Application.DoEvents();
            ComboBox railMode = Find<ComboBox>(dialog, "RailPositionMode");
            TextBox railPositions = Find<TextBox>(dialog, "RailPositions");
            NumericUpDown railSpacing = Find<NumericUpDown>(dialog,
                "RailCenterSpacing");
            ComboBox pierMode = Find<ComboBox>(dialog, "PierPositionMode");
            TextBox pierStations = Find<TextBox>(dialog, "PierStations");
            SupportDetailDiagram diagram = Find<SupportDetailDiagram>(dialog,
                "SupportDetailDiagram");

            railMode.SelectedIndex = 1;
            pierMode.SelectedIndex = 1;
            Application.DoEvents();
            Require(railPositions.Enabled,
                "manual rail position input is not active");
            Require(!railSpacing.Enabled,
                "default rail spacing stays active in manual mode");
            Require(pierStations.Enabled,
                "manual pier station input is not active");
            Require(diagram.Visible,
                "engineering diagram is hidden in custom mode");
            Require(diagram.AccessibleDescription.Contains("中心线") &&
                diagram.AccessibleDescription.Contains("外轮廓") &&
                diagram.AccessibleDescription.Contains("加s") &&
                diagram.AccessibleDescription.Contains("组件阵列低端"),
                "diagram does not distinguish datum, direction, and outline");
        }
        Pass();
    }

    private static void VerifyDialogLayoutsAtSupportedSizes()
    {
        Size[] sizes =
        {
            new Size(900, 620),
            new Size(1080, 760),
            new Size(1360, 900)
        };
        for (int index = 0; index < sizes.Length; index++)
        {
            using (SupportDetailDialog dialog = new SupportDetailDialog(
                new SupportDetailSettings()))
            using (Bitmap bitmap = new Bitmap(sizes[index].Width,
                sizes[index].Height))
            {
                dialog.ClientSize = sizes[index];
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-4000, -4000);
                dialog.Show();
                Application.DoEvents();
                dialog.PerformLayout();
                SupportDetailDiagram diagram = Find<SupportDetailDiagram>(dialog,
                    "SupportDetailDiagram");
                Button generate = Find<Button>(dialog, "GenerateSupportDetail");
                Require(diagram.Width >= 300 && diagram.Height >= 260,
                    "diagram is clipped at " + sizes[index]);
                Require(generate.Width > 90 && generate.Height > 24,
                    "generate button text is clipped at " + sizes[index]);
                dialog.DrawToBitmap(bitmap,
                    new Rectangle(Point.Empty, bitmap.Size));
                if (index == 1)
                {
                    string output = Path.Combine("work",
                        "support-detail-tests", "support-detail-dialog.png");
                    bitmap.Save(output);
                }
                dialog.Hide();
            }
        }
        Pass();
    }

    private static T Find<T>(Control root, string name) where T : Control
    {
        Control[] controls = root.Controls.Find(name, true);
        if (controls.Length != 1 || !(controls[0] is T))
            throw new InvalidOperationException("control not found: " + name);
        return (T)controls[0];
    }

    private static int Distance(Color first, Color second)
    {
        return Math.Abs(first.R - second.R) +
            Math.Abs(first.G - second.G) + Math.Abs(first.B - second.B);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Pass()
    {
        _passed++;
    }
}
