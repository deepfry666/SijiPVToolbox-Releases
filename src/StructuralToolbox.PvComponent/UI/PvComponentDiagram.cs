using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using StructuralToolbox.PvComponent.Settings;

namespace StructuralToolbox.PvComponent.UI
{
    public sealed class PvComponentDiagram : Control
    {
        private PvComponentSettings _settings;

        public PvComponentDiagram()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(248, 249, 250);
            ForeColor = Color.FromArgb(33, 37, 41);
            MinimumSize = new Size(320, 280);
            Dock = DockStyle.Fill;
            AccessibleName = "组件尺寸与插入基准示意图";
            AccessibleDescription =
                "P0为组件侧视轮廓左下角，+X为长度L方向；" +
                "侧视轮廓显示L和T，平面尺寸显示L和W。";
        }

        public PvComponentSettings Settings
        {
            get
            {
                return _settings == null
                    ? new PvComponentSettings() : _settings.Clone();
            }
            set
            {
                _settings = value == null
                    ? new PvComponentSettings() : value.Clone();
                _settings.Normalize();
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PvComponentSettings settings = Settings;
            Rectangle bounds = ClientRectangle;
            if (bounds.Width < 260 || bounds.Height < 220)
                return;

            using (Font title = new Font(Font.FontFamily, 10.0f,
                FontStyle.Bold))
            using (Font label = new Font(Font.FontFamily, 9.0f,
                FontStyle.Regular))
            using (Pen outline = new Pen(Color.FromArgb(0, 139, 156), 2.0f))
            using (Pen dimension = new Pen(Color.FromArgb(72, 78, 84), 1.2f))
            using (Pen datum = new Pen(Color.FromArgb(190, 45, 45), 1.4f))
            using (Brush text = new SolidBrush(ForeColor))
            using (Brush fill = new SolidBrush(Color.FromArgb(218, 244, 247)))
            {
                graphics.DrawString("组件侧视与清单尺寸", title, text,
                    new PointF(16.0f, 14.0f));

                float left = 54.0f;
                float right = bounds.Width - 32.0f;
                float sideTop = 76.0f;
                float sideHeight = Math.Max(12.0f, Math.Min(34.0f,
                    (float)(settings.Thickness / Math.Max(1.0,
                        settings.Length) * (right - left) * 8.0)));
                RectangleF side = new RectangleF(left, sideTop,
                    Math.Max(100.0f, right - left), sideHeight);
                graphics.FillRectangle(fill, side);
                graphics.DrawRectangle(outline, side.X, side.Y,
                    side.Width, side.Height);
                graphics.FillEllipse(Brushes.White, side.Left - 4.0f,
                    side.Bottom - 4.0f, 8.0f, 8.0f);
                graphics.DrawEllipse(datum, side.Left - 4.0f,
                    side.Bottom - 4.0f, 8.0f, 8.0f);
                graphics.DrawString("P0", label, text,
                    side.Left - 26.0f, side.Bottom + 5.0f);

                DrawHorizontalDimension(graphics, dimension, label, text,
                    side.Left, side.Right, side.Top - 20.0f,
                    "L=" + settings.Length.ToString("0.###") + " mm");
                DrawVerticalDimension(graphics, dimension, label, text,
                    side.Right + 12.0f, side.Top, side.Bottom,
                    "T=" + settings.Thickness.ToString("0.###") + " mm");
                DrawArrow(graphics, datum,
                    new PointF(side.Left, side.Bottom + 32.0f),
                    new PointF(side.Left + 78.0f, side.Bottom + 32.0f));
                graphics.DrawString("+X", label, text,
                    side.Left + 84.0f, side.Bottom + 23.0f);

                float planTop = Math.Max(side.Bottom + 76.0f,
                    bounds.Height * 0.54f);
                float availableHeight = Math.Max(46.0f,
                    bounds.Height - planTop - 36.0f);
                float ratio = (float)Math.Min(0.45,
                    settings.Width / Math.Max(settings.Length, 1.0));
                float planHeight = Math.Max(42.0f,
                    Math.Min(availableHeight, side.Width * ratio));
                RectangleF plan = new RectangleF(side.Left, planTop,
                    side.Width, planHeight);
                graphics.FillRectangle(fill, plan);
                graphics.DrawRectangle(outline, plan.X, plan.Y,
                    plan.Width, plan.Height);
                graphics.DrawString("平面", label, text,
                    plan.Left, plan.Top - 24.0f);
                DrawVerticalDimension(graphics, dimension, label, text,
                    plan.Right + 12.0f, plan.Top, plan.Bottom,
                    "W=" + settings.Width.ToString("0.###") + " mm");
            }
        }

        private static void DrawHorizontalDimension(Graphics graphics,
            Pen pen, Font font, Brush brush, float left, float right, float y,
            string label)
        {
            graphics.DrawLine(pen, left, y, right, y);
            graphics.DrawLine(pen, left, y - 5.0f, left, y + 5.0f);
            graphics.DrawLine(pen, right, y - 5.0f, right, y + 5.0f);
            DrawArrowHead(graphics, pen, new PointF(left, y), 1.0f);
            DrawArrowHead(graphics, pen, new PointF(right, y), -1.0f);
            SizeF size = graphics.MeasureString(label, font);
            graphics.FillRectangle(new SolidBrush(Color.FromArgb(248, 249, 250)),
                left + (right - left - size.Width) / 2.0f - 3.0f,
                y - size.Height / 2.0f, size.Width + 6.0f, size.Height);
            graphics.DrawString(label, font, brush,
                left + (right - left - size.Width) / 2.0f,
                y - size.Height / 2.0f);
        }

        private static void DrawVerticalDimension(Graphics graphics, Pen pen,
            Font font, Brush brush, float x, float top, float bottom,
            string label)
        {
            graphics.DrawLine(pen, x, top, x, bottom);
            graphics.DrawLine(pen, x - 5.0f, top, x + 5.0f, top);
            graphics.DrawLine(pen, x - 5.0f, bottom, x + 5.0f, bottom);
            DrawVerticalArrowHead(graphics, pen, new PointF(x, top), 1.0f);
            DrawVerticalArrowHead(graphics, pen, new PointF(x, bottom), -1.0f);
            graphics.DrawString(label, font, brush, x + 8.0f,
                top + Math.Max(0.0f, (bottom - top) / 2.0f - font.Height / 2.0f));
        }

        private static void DrawArrow(Graphics graphics, Pen pen,
            PointF start, PointF end)
        {
            graphics.DrawLine(pen, start, end);
            DrawArrowHead(graphics, pen, end, -1.0f);
        }

        private static void DrawArrowHead(Graphics graphics, Pen pen,
            PointF point, float direction)
        {
            graphics.DrawLine(pen, point,
                new PointF(point.X + direction * 7.0f, point.Y - 4.0f));
            graphics.DrawLine(pen, point,
                new PointF(point.X + direction * 7.0f, point.Y + 4.0f));
        }

        private static void DrawVerticalArrowHead(Graphics graphics, Pen pen,
            PointF point, float direction)
        {
            graphics.DrawLine(pen, point,
                new PointF(point.X - 4.0f, point.Y + direction * 7.0f));
            graphics.DrawLine(pen, point,
                new PointF(point.X + 4.0f, point.Y + direction * 7.0f));
        }
    }
}
