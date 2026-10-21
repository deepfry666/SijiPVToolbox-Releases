using System;
using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Ballast.UI;

internal static class BallastPlacementDiagramTests
{
    private static int _passed;

    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyDiagram(new Size(260, 148), "compact diagram");
            VerifyDiagram(new Size(520, 148), "wide diagram");
            Console.WriteLine("PASS {0} ballast placement diagram tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL ballast placement diagram tests: {0}",
                exception.Message);
            return 1;
        }
    }

    private static void VerifyDiagram(Size size, string context)
    {
        using (Form host = new Form())
        using (BallastPlacementDiagram diagram = new BallastPlacementDiagram())
        using (Bitmap bitmap = new Bitmap(size.Width, size.Height))
        {
            host.ClientSize = size;
            diagram.Dock = DockStyle.Fill;
            host.Controls.Add(diagram);
            host.ShowInTaskbar = false;
            host.StartPosition = FormStartPosition.Manual;
            host.Location = new Point(-3000, -3000);
            host.Show();
            Application.DoEvents();
            diagram.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));

            int colored = 0;
            Color background = diagram.BackColor;
            for (int y = 0; y < bitmap.Height; y += 2)
                for (int x = 0; x < bitmap.Width; x += 2)
                    if (ColorDistance(bitmap.GetPixel(x, y), background) > 18)
                        colored++;

            Require(colored > 180, context + " did not render enough diagram content.");
            Require(diagram.AccessibleDescription.Contains("F") &&
                diagram.AccessibleDescription.Contains("S"),
                context + " accessibility description omits the input symbols.");
        }
        _passed++;
    }

    private static int ColorDistance(Color first, Color second)
    {
        return Math.Abs(first.R - second.R) + Math.Abs(first.G - second.G) +
            Math.Abs(first.B - second.B);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
