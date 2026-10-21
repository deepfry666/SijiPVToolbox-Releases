using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StructuralToolbox.Ballast.UI
{
    internal sealed class BallastPlacementDiagram : Control
    {
        public BallastPlacementDiagram()
        {
            Name = "BallastPlacementDiagram";
            Height = 148;
            MinimumSize = new Size(260, 148);
            Dock = DockStyle.Top;
            Margin = new Padding(0, 4, 0, 6);
            BackColor = Color.FromArgb(248, 250, 249);
            AccessibleName = "配重自定义布置示意图";
            AccessibleDescription =
                "按组件阵列自身方向标出前侧中心边距 F、配重中心间距 S 和自动计算的后侧边距。";
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle border = new Rectangle(0, 0,
                Math.Max(0, ClientSize.Width - 1),
                Math.Max(0, ClientSize.Height - 1));
            using (Pen borderPen = new Pen(Color.FromArgb(205, 211, 208)))
                graphics.DrawRectangle(borderPen, border);

            float left = 46.0f;
            float right = Math.Max(left + 150.0f, ClientSize.Width - 22.0f);
            float top = 30.0f;
            float bottom = 84.0f;
            float centerY = (top + bottom) / 2.0f;
            float span = right - left;
            float first = left + span * 0.20f;
            float second = left + span * 0.50f;
            float third = left + span * 0.80f;

            using (Pen arrayPen = new Pen(Color.FromArgb(47, 111, 78), 2.0f))
            using (Pen modulePen = new Pen(Color.FromArgb(144, 174, 155), 1.0f))
            {
                graphics.DrawRectangle(arrayPen, left, top,
                    Math.Max(1.0f, span), bottom - top);
                for (int index = 1; index < 5; index++)
                {
                    float x = left + span * index / 5.0f;
                    graphics.DrawLine(modulePen, x, top, x, bottom);
                }
            }

            DrawFrontDirection(graphics, left, centerY);
            DrawBallast(graphics, first, centerY);
            DrawBallast(graphics, second, centerY);
            DrawBallast(graphics, third, centerY);

            DrawDimension(graphics, left, first, 102.0f, "F");
            DrawDimension(graphics, first, second, 124.0f, "S");
            DrawDimension(graphics, third, right, 102.0f, "后侧自动");
        }

        private void DrawFrontDirection(Graphics graphics, float edge,
            float centerY)
        {
            using (Brush textBrush = new SolidBrush(Color.FromArgb(35, 68, 50)))
            using (Font labelFont = new Font(Font.FontFamily,
                Math.Max(8.0f, Font.Size - 0.5f), FontStyle.Bold))
            {
                graphics.DrawString("前侧", labelFont, textBrush, 6.0f,
                    centerY - labelFont.Height / 2.0f);
            }
            using (Pen arrowPen = new Pen(Color.FromArgb(35, 68, 50), 1.5f))
                graphics.DrawLine(arrowPen, edge - 4.0f, centerY,
                    edge - 17.0f, centerY);
            using (Brush arrowBrush = new SolidBrush(Color.FromArgb(35, 68, 50)))
            {
                graphics.FillPolygon(arrowBrush, new PointF[]
                {
                    new PointF(edge - 17.0f, centerY),
                    new PointF(edge - 11.0f, centerY - 4.0f),
                    new PointF(edge - 11.0f, centerY + 4.0f)
                });
            }
        }

        private static void DrawBallast(Graphics graphics, float centerX,
            float centerY)
        {
            RectangleF ballast = new RectangleF(centerX - 8.0f,
                centerY - 12.0f, 16.0f, 24.0f);
            using (Brush fill = new SolidBrush(Color.FromArgb(221, 118, 40)))
            using (Pen outline = new Pen(Color.FromArgb(139, 66, 15), 1.5f))
            {
                graphics.FillRectangle(fill, ballast);
                graphics.DrawRectangle(outline, ballast.X, ballast.Y,
                    ballast.Width, ballast.Height);
                graphics.DrawLine(outline, centerX - 3.0f, centerY,
                    centerX + 3.0f, centerY);
                graphics.DrawLine(outline, centerX, centerY - 3.0f,
                    centerX, centerY + 3.0f);
            }
        }

        private void DrawDimension(Graphics graphics, float startX,
            float endX, float y, string label)
        {
            using (Pen pen = new Pen(Color.FromArgb(58, 62, 66), 1.0f))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(43, 47, 51)))
            using (Brush backgroundBrush = new SolidBrush(BackColor))
            {
                graphics.DrawLine(pen, startX, y, endX, y);
                graphics.DrawLine(pen, startX, y - 4.0f, startX, y + 4.0f);
                graphics.DrawLine(pen, endX, y - 4.0f, endX, y + 4.0f);
                SizeF size = graphics.MeasureString(label, Font);
                float textX = (startX + endX - size.Width) / 2.0f;
                graphics.FillRectangle(backgroundBrush, textX - 2.0f,
                    y - size.Height / 2.0f, size.Width + 4.0f, size.Height);
                graphics.DrawString(label, Font, textBrush, textX,
                    y - size.Height / 2.0f);
            }
        }
    }
}
