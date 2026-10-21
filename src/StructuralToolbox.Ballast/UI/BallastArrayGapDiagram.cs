using System.Drawing;
using System.Windows.Forms;

namespace StructuralToolbox.Ballast.UI
{
    internal sealed class BallastArrayGapDiagram : Control
    {
        public BallastArrayGapDiagram()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(58, 64, 72);
            MinimumSize = new Size(220, 96);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle left = new Rectangle(18, 30, 72, 48);
            Rectangle right = new Rectangle(150, 30, 72, 48);
            using (Pen outline = new Pen(Color.FromArgb(50, 112, 170), 2))
            using (Pen dimension = new Pen(Color.FromArgb(196, 76, 54), 1))
            {
                e.Graphics.DrawRectangle(outline, left);
                e.Graphics.DrawRectangle(outline, right);
                int y = 88;
                e.Graphics.DrawLine(dimension, left.Right, y, right.Left, y);
                e.Graphics.DrawLine(dimension, left.Right, y - 5, left.Right, y + 5);
                e.Graphics.DrawLine(dimension, right.Left, y - 5, right.Left, y + 5);
            }
            e.Graphics.DrawString("组件外轮廓", Font, Brushes.DimGray, 18, 9);
            e.Graphics.DrawString("组件外轮廓", Font, Brushes.DimGray, 150, 9);
            e.Graphics.DrawString("G：两外轮廓之间的断开净距", Font,
                Brushes.Firebrick, 30, 91);
        }
    }
}
