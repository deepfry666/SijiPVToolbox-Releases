using System;
using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Layout.UI;

internal static class LayoutDiagramTests
{
    [STAThread]
    private static int Main()
    {
        try { Verify(new Size(270, 220)); Verify(new Size(540, 440)); Console.WriteLine("PASS layout diagram tests"); return 0; }
        catch (Exception exception) { Console.Error.WriteLine("FAIL layout diagram tests: {0}", exception.Message); return 1; }
    }
    private static void Verify(Size size)
    {
        using (Form host = new Form()) using (LayoutDiagram diagram = new LayoutDiagram()) using (Bitmap bitmap = new Bitmap(size.Width, size.Height))
        {
            host.ClientSize = size; diagram.Dock = DockStyle.Fill; host.Controls.Add(diagram); host.ShowInTaskbar = false; host.StartPosition = FormStartPosition.Manual; host.Location = new Point(-3000, -3000); host.Show(); Application.DoEvents(); diagram.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
            int colored = 0; Color background = diagram.BackColor;
            for (int y = 0; y < bitmap.Height; y += 3) for (int x = 0; x < bitmap.Width; x += 3) if (Distance(bitmap.GetPixel(x, y), background) > 18) colored++;
            if (colored < 250) throw new InvalidOperationException("diagram rendered too little content");
            string description = diagram.AccessibleDescription; if (!description.Contains("L/W") || !description.Contains("Gx/Gy") || !description.Contains("Ax/Ay") || !description.Contains("开放线障碍") || !description.Contains("净距 O") || !description.Contains("alpha")) throw new InvalidOperationException("diagram accessibility labels are incomplete");
        }
    }
    private static int Distance(Color a, Color b) { return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B); }
}
