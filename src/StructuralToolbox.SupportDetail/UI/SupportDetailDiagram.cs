using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using StructuralToolbox.SupportDetail.Core;
using StructuralToolbox.SupportDetail.Settings;

namespace StructuralToolbox.SupportDetail.UI
{
    public sealed class SupportDetailDiagram : Control
    {
        private SupportDetailSettings _settings;

        public SupportDetailDiagram()
        {
            _settings = new SupportDetailSettings();
            Dock = DockStyle.Fill;
            MinimumSize = new Size(300, 260);
            BackColor = Color.White;
            AccessibleName = "混凝土屋面支架参数示意图";
            AccessibleDescription =
                "组件阵列低端为支架局部坐标原点，沿组件斜面向高端为正方向加s。" +
                "N为组件数量，L为组件斜长，G为组件净缝，alpha为倾角，" +
                "R为夹持中心距，r为组件低端至夹持中心线位置，" +
                "s为组件阵列低端至墩中心线位置，H为基准立柱高度，" +
                "E为斜梁端部至最外侧三角件中心线的伸出，" +
                "D或Bx乘By为混凝土支墩外轮廓，h为支墩高度。";
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public SupportDetailSettings Settings
        {
            get { return _settings; }
            set
            {
                _settings = value == null
                    ? new SupportDetailSettings() : value.Clone();
                _settings.Normalize();
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Pen border = new Pen(Color.FromArgb(202, 207, 212)))
                graphics.DrawRectangle(border, 0, 0,
                    Math.Max(0, ClientSize.Width - 1),
                    Math.Max(0, ClientSize.Height - 1));

            SupportDetailBuildResult result =
                SupportDetailGeometryBuilder.Build(_settings);
            if (!result.IsValid)
            {
                DrawCenteredText(graphics, "参数暂不能形成支架大样",
                    Color.FromArgb(170, 55, 45));
                return;
            }

            SupportDetailGeometry geometry = result.Geometry;
            RectangleF viewport = new RectangleF(34.0f, 34.0f,
                Math.Max(180.0f, ClientSize.Width - 58.0f),
                Math.Max(145.0f, ClientSize.Height - 78.0f));
            DiagramBounds bounds = CalculateBounds(geometry);
            double scale = Math.Min(viewport.Width / Math.Max(1.0, bounds.Width),
                viewport.Height / Math.Max(1.0, bounds.Height));
            scale = Math.Max(0.01, scale);

            Func<SupportDetailPoint, PointF> map = delegate(
                SupportDetailPoint point)
            {
                float x = viewport.Left + (float)((point.X - bounds.MinimumX) * scale);
                float y = viewport.Bottom - (float)((point.Y - bounds.MinimumY) * scale);
                return new PointF(x, y);
            };

            using (Font labelFont = new Font(Font.FontFamily,
                Math.Max(7.5f, Font.Size - 0.5f), FontStyle.Bold))
            using (Font smallFont = new Font(Font.FontFamily,
                Math.Max(7.0f, Font.Size - 1.0f), FontStyle.Regular))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(36, 41, 46)))
            using (Pen groundPen = new Pen(Color.FromArgb(100, 104, 108), 1.2f))
            using (Pen beamPen = new Pen(Color.FromArgb(38, 125, 91), 3.0f))
            using (Pen panelPen = new Pen(Color.FromArgb(27, 109, 171), 3.0f))
            using (Pen steelPen = new Pen(Color.FromArgb(181, 126, 20), 1.8f))
            using (Pen pierPen = new Pen(Color.FromArgb(170, 65, 55), 1.8f))
            using (Pen centerPen = new Pen(Color.FromArgb(120, 124, 128), 1.0f))
            using (Pen dimensionPen = new Pen(Color.FromArgb(50, 69, 62), 1.0f))
            {
                centerPen.DashStyle = DashStyle.Dash;
                DrawGround(graphics, groundPen, map, bounds, geometry);
                DrawPiers(graphics, pierPen, centerPen, steelPen, map,
                    geometry, scale);
                DrawBeam(graphics, beamPen, map, geometry);
                DrawPanels(graphics, panelPen, map, geometry);
                DrawRails(graphics, steelPen, map, geometry, scale);
                DrawDimensions(graphics, dimensionPen, textBrush, labelFont,
                    smallFont, map, geometry, viewport);
            }
        }

        private static void DrawGround(Graphics graphics, Pen pen,
            Func<SupportDetailPoint, PointF> map, DiagramBounds bounds,
            SupportDetailGeometry geometry)
        {
            PointF left = map(new SupportDetailPoint(bounds.MinimumX, 0.0));
            PointF right = map(new SupportDetailPoint(bounds.MaximumX, 0.0));
            graphics.DrawLine(pen, left, right);
        }

        private static void DrawPiers(Graphics graphics, Pen pierPen,
            Pen centerPen, Pen steelPen, Func<SupportDetailPoint, PointF> map,
            SupportDetailGeometry geometry, double scale)
        {
            SupportDetailSettings settings = geometry.Settings;
            double pierWidth = settings.PierShape ==
                SupportDetailPierShape.Circular
                ? settings.PierDiameter : settings.PierLength;
            for (int index = 0; index < geometry.Piers.Count; index++)
            {
                SupportDetailPierGeometry pier = geometry.Piers[index];
                SupportDetailPoint lowerLeft = new SupportDetailPoint(
                    pier.Center.X - pierWidth / 2.0, 0.0);
                SupportDetailPoint upperRight = new SupportDetailPoint(
                    pier.Center.X + pierWidth / 2.0, settings.PierHeight);
                PointF first = map(lowerLeft);
                PointF second = map(upperRight);
                RectangleF rectangle = Normalize(first, second);
                graphics.DrawRectangle(pierPen, rectangle.X, rectangle.Y,
                    rectangle.Width, rectangle.Height);

                PointF centerBottom = map(new SupportDetailPoint(
                    pier.Center.X, 0.0));
                PointF centerTop = map(new SupportDetailPoint(
                    pier.Center.X, pier.BeamElevation));
                graphics.DrawLine(centerPen, centerBottom, centerTop);

                double halfColumn = Math.Max(8.0, 41.0 / 2.0);
                PointF columnLeftBottom = map(new SupportDetailPoint(
                    pier.Center.X - halfColumn, settings.PierHeight));
                PointF columnLeftTop = map(new SupportDetailPoint(
                    pier.Center.X - halfColumn,
                    settings.PierHeight + pier.ActualColumnHeight));
                PointF columnRightBottom = map(new SupportDetailPoint(
                    pier.Center.X + halfColumn, settings.PierHeight));
                PointF columnRightTop = map(new SupportDetailPoint(
                    pier.Center.X + halfColumn,
                    settings.PierHeight + pier.ActualColumnHeight));
                graphics.DrawLine(steelPen, columnLeftBottom, columnLeftTop);
                graphics.DrawLine(steelPen, columnRightBottom, columnRightTop);

                SupportDetailTriangleGeometry triangle =
                    geometry.Triangles[index];
                graphics.DrawLine(steelPen, map(triangle.LeftBase),
                    map(triangle.RightBase));
                graphics.DrawLine(steelPen, map(triangle.LeftBase),
                    map(triangle.Apex));
                graphics.DrawLine(steelPen, map(triangle.RightBase),
                    map(triangle.Apex));
            }
        }

        private static void DrawBeam(Graphics graphics, Pen pen,
            Func<SupportDetailPoint, PointF> map,
            SupportDetailGeometry geometry)
        {
            SupportDetailVector normal = geometry.BeamNormal *
                (geometry.Settings.BeamWidth / 2.0);
            graphics.DrawLine(pen, map(geometry.BeamStart + normal),
                map(geometry.BeamEnd + normal));
            graphics.DrawLine(pen, map(geometry.BeamStart + normal * -1.0),
                map(geometry.BeamEnd + normal * -1.0));
        }

        private static void DrawPanels(Graphics graphics, Pen pen,
            Func<SupportDetailPoint, PointF> map,
            SupportDetailGeometry geometry)
        {
            for (int index = 0; index < geometry.Panels.Count; index++)
                graphics.DrawLine(pen, map(geometry.Panels[index].Start),
                    map(geometry.Panels[index].End));
        }

        private static void DrawRails(Graphics graphics, Pen pen,
            Func<SupportDetailPoint, PointF> map,
            SupportDetailGeometry geometry, double scale)
        {
            double markerHalf = Math.Max(20.0, 5.0 / scale);
            for (int index = 0; index < geometry.Rails.Count; index++)
            {
                SupportDetailPoint point = geometry.Rails[index].Point;
                SupportDetailVector normal = geometry.BeamNormal * markerHalf;
                graphics.DrawLine(pen, map(point + normal),
                    map(point + normal * -1.0));
            }
        }

        private static void DrawDimensions(Graphics graphics, Pen dimensionPen,
            Brush brush, Font labelFont, Font smallFont,
            Func<SupportDetailPoint, PointF> map,
            SupportDetailGeometry geometry, RectangleF viewport)
        {
            PointF beamStart = map(geometry.BeamStart);
            PointF beamEnd = map(geometry.BeamEnd);
            DrawArrow(graphics, dimensionPen, beamStart, beamEnd);
            DrawLabel(graphics, brush, labelFont, "+s",
                new PointF(beamEnd.X - 16.0f, beamEnd.Y - 24.0f));
            DrawLabel(graphics, brush, smallFont, "0",
                new PointF(map(geometry.Panels[0].Start).X - 10.0f,
                    map(geometry.Panels[0].Start).Y + 5.0f));

            DrawLabel(graphics, brush, labelFont,
                "N=" + geometry.Settings.ComponentCount +
                "  L  G", new PointF(viewport.Left + 4.0f,
                    viewport.Top - 26.0f));
            DrawLabel(graphics, brush, labelFont, "α",
                new PointF(beamStart.X + 20.0f, beamStart.Y + 5.0f));

            if (geometry.Rails.Count >= 2)
            {
                PointF first = map(geometry.Rails[0].Point);
                PointF second = map(geometry.Rails[1].Point);
                DrawDoubleArrow(graphics, dimensionPen,
                    Offset(first, 0.0f, -18.0f),
                    Offset(second, 0.0f, -18.0f));
                DrawLabel(graphics, brush, labelFont, "R / r",
                    Midpoint(first, second, 0.0f, -34.0f));
            }

            if (geometry.Piers.Count > 0)
            {
                SupportDetailPierGeometry firstPier = geometry.Piers[0];
                PointF pierCenter = map(new SupportDetailPoint(
                    firstPier.Center.X, 0.0));
                PointF pierTop = map(new SupportDetailPoint(
                    firstPier.Center.X, geometry.Settings.PierHeight));
                PointF columnTop = map(new SupportDetailPoint(
                    firstPier.Center.X,
                    geometry.Settings.PierHeight +
                    firstPier.ActualColumnHeight));
                DrawDoubleArrow(graphics, dimensionPen,
                    Offset(pierTop, -22.0f, 0.0f),
                    Offset(columnTop, -22.0f, 0.0f));
                DrawLabel(graphics, brush, labelFont, "H",
                    Midpoint(pierTop, columnTop, -37.0f, -6.0f));
                DrawLabel(graphics, brush, labelFont, "s1",
                    new PointF(pierCenter.X + 4.0f, pierCenter.Y + 2.0f));
            }

            DrawLabel(graphics, brush, labelFont, "E",
                Midpoint(map(geometry.BeamStart),
                    map(geometry.Triangles[0].BeamPoint), 0.0f, -28.0f));
            DrawLabel(graphics, brush, smallFont,
                geometry.Settings.PierShape == SupportDetailPierShape.Circular
                    ? "支墩 D / h（外轮廓）" : "支墩 Bx×By / h（外轮廓）",
                new PointF(viewport.Left + 4.0f, viewport.Bottom + 8.0f));
        }

        private static DiagramBounds CalculateBounds(
            SupportDetailGeometry geometry)
        {
            double pierWidth = geometry.Settings.PierShape ==
                SupportDetailPierShape.Circular
                ? geometry.Settings.PierDiameter : geometry.Settings.PierLength;
            double minimumX = Math.Min(geometry.BeamStart.X,
                geometry.BeamEnd.X);
            double maximumX = Math.Max(geometry.BeamStart.X,
                geometry.BeamEnd.X);
            for (int index = 0; index < geometry.Piers.Count; index++)
            {
                minimumX = Math.Min(minimumX,
                    geometry.Piers[index].Center.X - pierWidth / 2.0);
                maximumX = Math.Max(maximumX,
                    geometry.Piers[index].Center.X + pierWidth / 2.0);
            }
            double maximumY = Math.Max(geometry.BeamStart.Y,
                geometry.BeamEnd.Y) + geometry.Settings.PanelThickness + 80.0;
            double marginX = Math.Max(100.0, (maximumX - minimumX) * 0.04);
            return new DiagramBounds(minimumX - marginX, -40.0,
                maximumX + marginX, maximumY);
        }

        private void DrawCenteredText(Graphics graphics, string text, Color color)
        {
            using (Brush brush = new SolidBrush(color))
            using (Font font = new Font(Font.FontFamily, Font.Size,
                FontStyle.Bold))
            {
                SizeF size = graphics.MeasureString(text, font);
                graphics.DrawString(text, font, brush,
                    (ClientSize.Width - size.Width) / 2.0f,
                    (ClientSize.Height - size.Height) / 2.0f);
            }
        }

        private static RectangleF Normalize(PointF first, PointF second)
        {
            return new RectangleF(Math.Min(first.X, second.X),
                Math.Min(first.Y, second.Y), Math.Abs(first.X - second.X),
                Math.Abs(first.Y - second.Y));
        }

        private static void DrawArrow(Graphics graphics, Pen pen,
            PointF start, PointF end)
        {
            graphics.DrawLine(pen, start, end);
            DrawArrowHead(graphics, pen, end, start);
        }

        private static void DrawDoubleArrow(Graphics graphics, Pen pen,
            PointF start, PointF end)
        {
            graphics.DrawLine(pen, start, end);
            DrawArrowHead(graphics, pen, start, end);
            DrawArrowHead(graphics, pen, end, start);
        }

        private static void DrawArrowHead(Graphics graphics, Pen pen,
            PointF tip, PointF tail)
        {
            float dx = tail.X - tip.X;
            float dy = tail.Y - tip.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.01f)
                return;
            dx /= length;
            dy /= length;
            const float arrowLength = 5.0f;
            const float arrowWidth = 3.0f;
            PointF basePoint = new PointF(tip.X + dx * arrowLength,
                tip.Y + dy * arrowLength);
            graphics.DrawLine(pen, tip,
                new PointF(basePoint.X - dy * arrowWidth,
                    basePoint.Y + dx * arrowWidth));
            graphics.DrawLine(pen, tip,
                new PointF(basePoint.X + dy * arrowWidth,
                    basePoint.Y - dx * arrowWidth));
        }

        private static PointF Offset(PointF point, float x, float y)
        {
            return new PointF(point.X + x, point.Y + y);
        }

        private static PointF Midpoint(PointF first, PointF second,
            float xOffset, float yOffset)
        {
            return new PointF((first.X + second.X) / 2.0f + xOffset,
                (first.Y + second.Y) / 2.0f + yOffset);
        }

        private static void DrawLabel(Graphics graphics, Brush brush,
            Font font, string text, PointF point)
        {
            graphics.DrawString(text, font, brush, point);
        }

        private sealed class DiagramBounds
        {
            public DiagramBounds(double minimumX, double minimumY,
                double maximumX, double maximumY)
            {
                MinimumX = minimumX;
                MinimumY = minimumY;
                MaximumX = maximumX;
                MaximumY = maximumY;
            }

            public double MinimumX { get; private set; }
            public double MinimumY { get; private set; }
            public double MaximumX { get; private set; }
            public double MaximumY { get; private set; }
            public double Width { get { return MaximumX - MinimumX; } }
            public double Height { get { return MaximumY - MinimumY; } }
        }
    }
}
