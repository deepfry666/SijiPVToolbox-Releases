using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StructuralToolbox.Layout.UI
{
    internal sealed class LayoutDiagram : Control
    {
        public LayoutDiagram()
        {
            Name = "LayoutDiagram";
            Height = 235;
            MinimumSize = new Size(270, 220);
            Dock = DockStyle.Top;
            Margin = new Padding(0, 4, 0, 6);
            BackColor = Color.FromArgb(248, 250, 249);
            AccessibleName = "组件自动排布工程几何示意图";
            AccessibleDescription =
                "示意屋面边界和局部方向 D；组件真实尺寸 L/W；" +
                "组件外轮廓净距 Gx/Gy；阵列外轮廓净距 Ax/Ay；" +
                "开放线障碍及组件外轮廓到障碍线的净距 O；" +
                "完成面角度 alpha。";
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen border = new Pen(Color.FromArgb(205, 211, 208)))
                g.DrawRectangle(border, 0, 0, Math.Max(0, ClientSize.Width - 1),
                    Math.Max(0, ClientSize.Height - 1));

            float left = 30.0f;
            float top = 28.0f;
            float right = Math.Max(left + 180.0f, ClientSize.Width - 16.0f);
            float bottom = Math.Max(top + 115.0f, ClientSize.Height - 24.0f);
            float roofWidth = right - left;
            float roofHeight = bottom - top;

            using (Pen roof = new Pen(Color.FromArgb(47, 111, 78), 2.0f))
                g.DrawRectangle(roof, left, top, roofWidth, roofHeight);

            float panelWidth = Clamp(roofWidth * 0.09f, 16.0f, 44.0f);
            float panelHeight = Clamp(roofHeight * 0.20f, 24.0f, 54.0f);
            float componentGapX = Clamp(roofWidth * 0.025f, 5.0f, 12.0f);
            float componentGapY = Clamp(roofHeight * 0.03f, 5.0f, 12.0f);
            float arrayGapX = Clamp(roofWidth * 0.08f, 15.0f, 40.0f);
            float arrayGapY = Clamp(roofHeight * 0.08f, 12.0f, 30.0f);
            float firstX = left + 14.0f;
            float firstY = top + 24.0f;
            float firstWidth = panelWidth * 2.0f + componentGapX;
            float firstHeight = panelHeight * 2.0f + componentGapY;
            float secondX = firstX + firstWidth + arrayGapX;
            float thirdY = firstY + firstHeight + arrayGapY;

            using (Brush fill = new SolidBrush(Color.FromArgb(208, 226, 215)))
            using (Pen panel = new Pen(Color.FromArgb(77, 122, 92)))
            {
                for (int row = 0; row < 2; row++)
                    for (int column = 0; column < 2; column++)
                        DrawPanel(g, fill, panel,
                            firstX + column * (panelWidth + componentGapX),
                            firstY + row * (panelHeight + componentGapY),
                            panelWidth, panelHeight);
                DrawPanel(g, fill, panel, secondX, firstY,
                    panelWidth, panelHeight);
                DrawPanel(g, fill, panel, firstX, thirdY,
                    panelWidth, panelHeight);
            }

            float obstacleWidth = Clamp(roofWidth * 0.13f, 28.0f, 70.0f);
            float obstacleHeight = Clamp(roofHeight * 0.30f, 36.0f, 76.0f);
            float obstacleX = right - obstacleWidth - 34.0f;
            float obstacleY = top + 32.0f;
            float clearance = Clamp(Math.Min(obstacleWidth, obstacleHeight) *
                0.12f, 4.0f, 8.0f);
            float obstacleLineX = obstacleX + obstacleWidth / 2.0f;
            using (Pen obstacle = new Pen(Color.FromArgb(184, 66, 42), 2.0f))
            {
                g.DrawLine(obstacle, obstacleLineX, obstacleY,
                    obstacleLineX, obstacleY + obstacleHeight * 0.55f);
                g.DrawLine(obstacle, obstacleLineX,
                    obstacleY + obstacleHeight * 0.55f,
                    obstacleLineX + obstacleWidth * 0.42f,
                    obstacleY + obstacleHeight * 0.82f);
            }
            using (Pen clearancePen = new Pen(Color.FromArgb(184, 66, 42), 1.2f))
            {
                clearancePen.DashStyle = DashStyle.Dash;
                g.DrawLine(clearancePen, obstacleLineX - clearance,
                    obstacleY - clearance, obstacleLineX - clearance,
                    obstacleY + obstacleHeight * 0.55f);
            }

            using (Font labelFont = new Font(Font.FontFamily,
                Math.Max(7.5f, Font.Size - 1.0f), FontStyle.Bold))
            using (Font dimensionFont = new Font(Font.FontFamily,
                Math.Max(7.0f, Font.Size - 1.5f), FontStyle.Bold))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(43, 47, 51)))
            using (Pen dimensionPen = new Pen(Color.FromArgb(35, 68, 50), 1.2f))
            {
                g.DrawString("屋面边界", labelFont, labelBrush, left, 7.0f);
                g.DrawString("开放线障碍", labelFont, labelBrush,
                    obstacleX - 8.0f, obstacleY - 19.0f);

                DrawDimension(g, dimensionPen, labelBrush, dimensionFont,
                    new PointF(firstX, firstY - 5.0f),
                    new PointF(firstX + panelWidth, firstY - 5.0f), "W",
                    new PointF(0.0f, -13.0f));
                DrawDimension(g, dimensionPen, labelBrush, dimensionFont,
                    new PointF(firstX - 6.0f, firstY),
                    new PointF(firstX - 6.0f, firstY + panelHeight), "L",
                    new PointF(-12.0f, -4.0f));
                DrawDimension(g, dimensionPen, labelBrush, dimensionFont,
                    new PointF(firstX + panelWidth,
                        firstY + panelHeight * 0.55f),
                    new PointF(firstX + panelWidth + componentGapX,
                        firstY + panelHeight * 0.55f), "Gx",
                    new PointF(-5.0f, -14.0f));
                DrawDimension(g, dimensionPen, labelBrush, dimensionFont,
                    new PointF(firstX + panelWidth * 0.55f,
                        firstY + panelHeight),
                    new PointF(firstX + panelWidth * 0.55f,
                        firstY + panelHeight + componentGapY), "Gy",
                    new PointF(4.0f, -4.0f));
                DrawDimension(g, dimensionPen, labelBrush, dimensionFont,
                    new PointF(firstX + firstWidth,
                        firstY + panelHeight * 0.55f),
                    new PointF(secondX, firstY + panelHeight * 0.55f), "Ax",
                    new PointF(0.0f, -14.0f));
                DrawDimension(g, dimensionPen, labelBrush, dimensionFont,
                    new PointF(firstX + panelWidth * 0.55f,
                        firstY + firstHeight),
                    new PointF(firstX + panelWidth * 0.55f, thirdY), "Ay",
                    new PointF(4.0f, -4.0f));
                DrawDimension(g, dimensionPen, labelBrush, dimensionFont,
                    new PointF(obstacleLineX - clearance,
                        obstacleY + obstacleHeight * 0.35f),
                    new PointF(obstacleLineX,
                        obstacleY + obstacleHeight * 0.35f), "O",
                    new PointF(-5.0f, -14.0f));

                float directionX = right - 12.0f;
                DrawSingleArrow(g, dimensionPen,
                    new PointF(directionX, top + 55.0f),
                    new PointF(directionX, top + 18.0f));
                g.DrawString("D", labelFont, labelBrush,
                    directionX - 7.0f, top + 2.0f);

                float angleX = right - 76.0f;
                float angleY = bottom - 12.0f;
                g.DrawLine(dimensionPen, angleX, angleY, angleX + 45.0f,
                    angleY);
                g.DrawLine(dimensionPen, angleX, angleY, angleX + 35.0f,
                    angleY - 18.0f);
                g.DrawArc(dimensionPen, angleX + 5.0f, angleY - 13.0f,
                    24.0f, 18.0f, 205.0f, 28.0f);
                g.DrawString("α", labelFont, labelBrush,
                    angleX + 19.0f, angleY - 19.0f);
            }
        }

        private static void DrawPanel(Graphics graphics, Brush fill, Pen line,
            float x, float y, float width, float height)
        {
            graphics.FillRectangle(fill, x, y, width, height);
            graphics.DrawRectangle(line, x, y, width, height);
        }

        private static void DrawDimension(Graphics graphics, Pen pen,
            Brush brush, Font font, PointF start, PointF end, string label,
            PointF labelOffset)
        {
            DrawDoubleArrow(graphics, pen, start, end);
            float centerX = (start.X + end.X) / 2.0f;
            float centerY = (start.Y + end.Y) / 2.0f;
            graphics.DrawString(label, font, brush,
                centerX + labelOffset.X, centerY + labelOffset.Y);
        }

        private static void DrawDoubleArrow(Graphics graphics, Pen pen,
            PointF start, PointF end)
        {
            graphics.DrawLine(pen, start, end);
            DrawArrowHead(graphics, pen, end, start);
            DrawArrowHead(graphics, pen, start, end);
        }

        private static void DrawSingleArrow(Graphics graphics, Pen pen,
            PointF start, PointF end)
        {
            graphics.DrawLine(pen, start, end);
            DrawArrowHead(graphics, pen, end, start);
        }

        private static void DrawArrowHead(Graphics graphics, Pen pen,
            PointF tip, PointF tail)
        {
            float dx = tail.X - tip.X;
            float dy = tail.Y - tip.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.01f) return;
            dx /= length;
            dy /= length;
            const float arrowLength = 4.0f;
            const float arrowWidth = 2.5f;
            PointF basePoint = new PointF(tip.X + dx * arrowLength,
                tip.Y + dy * arrowLength);
            PointF first = new PointF(basePoint.X - dy * arrowWidth,
                basePoint.Y + dx * arrowWidth);
            PointF second = new PointF(basePoint.X + dy * arrowWidth,
                basePoint.Y - dx * arrowWidth);
            graphics.DrawLine(pen, tip, first);
            graphics.DrawLine(pen, tip, second);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
