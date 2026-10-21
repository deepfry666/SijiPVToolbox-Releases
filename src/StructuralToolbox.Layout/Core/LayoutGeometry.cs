using System;
using System.Collections.Generic;

namespace StructuralToolbox.Layout.Core
{
    public struct LayoutPoint
    {
        public LayoutPoint(double x, double y) { X = x; Y = y; }
        public double X;
        public double Y;
        public static LayoutPoint operator +(LayoutPoint a, LayoutVector b) { return new LayoutPoint(a.X + b.X, a.Y + b.Y); }
        public static LayoutVector operator -(LayoutPoint a, LayoutPoint b) { return new LayoutVector(a.X - b.X, a.Y - b.Y); }
    }

    public struct LayoutVector
    {
        public LayoutVector(double x, double y) { X = x; Y = y; }
        public double X;
        public double Y;
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }
        public LayoutVector Normalized()
        {
            double length = Length;
            if (length <= 1e-9) throw new ArgumentException("方向长度必须大于零。", "value");
            return new LayoutVector(X / length, Y / length);
        }
        public static LayoutVector operator *(LayoutVector a, double value) { return new LayoutVector(a.X * value, a.Y * value); }
        public static LayoutVector operator -(LayoutVector a) { return new LayoutVector(-a.X, -a.Y); }
    }

    public sealed class LayoutPolygon
    {
        private const double DuplicatePointTolerance = 1e-7;

        public LayoutPolygon(IEnumerable<LayoutPoint> points)
        {
            if (points == null) throw new ArgumentNullException("points");
            List<LayoutPoint> normalized = new List<LayoutPoint>();
            foreach (LayoutPoint point in points)
                if (normalized.Count == 0 ||
                    (point - normalized[normalized.Count - 1]).Length >
                        DuplicatePointTolerance)
                    normalized.Add(point);
            if (normalized.Count > 1 &&
                (normalized[0] - normalized[normalized.Count - 1]).Length <=
                    DuplicatePointTolerance)
                normalized.RemoveAt(normalized.Count - 1);
            Points = normalized;
            if (Points.Count < 3) throw new ArgumentException("轮廓至少需要三个点。", "points");
        }
        public IList<LayoutPoint> Points { get; private set; }
    }

    public static class LayoutBoundaryReader
    {
        public static bool TryCreate(IList<LayoutPoint> source,
            bool closedFlag, double closureTolerance,
            out LayoutPolygon polygon, out string reason)
        {
            polygon = null;
            reason = String.Empty;
            if (source == null)
            {
                reason = "屋面轮廓无法读取";
                return false;
            }
            if (closureTolerance < 0.0 || Double.IsNaN(closureTolerance) ||
                Double.IsInfinity(closureTolerance))
                throw new ArgumentOutOfRangeException("closureTolerance");

            List<LayoutPoint> points = new List<LayoutPoint>();
            for (int i = 0; i < source.Count; i++)
                if (points.Count == 0 ||
                    (source[i] - points[points.Count - 1]).Length >
                        closureTolerance)
                    points.Add(source[i]);
            if (points.Count < 2)
            {
                reason = "屋面轮廓顶点不足";
                return false;
            }
            double gap = (points[0] - points[points.Count - 1]).Length;
            if (!closedFlag && gap > closureTolerance)
            {
                reason = String.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "多段线未闭合，首尾端点相差 {0:0.###} mm", gap);
                return false;
            }
            if (gap <= closureTolerance)
                points.RemoveAt(points.Count - 1);
            try
            {
                polygon = new LayoutPolygon(points);
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }
    }

    public sealed class LayoutCircle
    {
        public LayoutCircle(LayoutPoint center, double radius)
        {
            if (radius <= 0.0) throw new ArgumentOutOfRangeException("radius");
            Center = center; Radius = radius;
        }
        public LayoutPoint Center { get; private set; }
        public double Radius { get; private set; }
    }

    public sealed class LayoutPath
    {
        public LayoutPath(IEnumerable<LayoutPoint> points)
        {
            if (points == null) throw new ArgumentNullException("points");
            Points = new List<LayoutPoint>(points);
            if (Points.Count < 2)
                throw new ArgumentException("线式障碍至少需要两个点。", "points");
            bool hasLength = false;
            for (int i = 0; i < Points.Count - 1; i++)
                if ((Points[i + 1] - Points[i]).Length > 1e-9)
                    hasLength = true;
            if (!hasLength)
                throw new ArgumentException("线式障碍长度必须大于零。", "points");
        }

        public IList<LayoutPoint> Points { get; private set; }
    }

    public sealed class LayoutRectangle
    {
        public LayoutRectangle(LayoutPoint center, LayoutVector along, LayoutVector across, double length, double width, bool conflict, string arrayId)
        {
            Center = center; Along = along.Normalized(); Across = across.Normalized(); Length = length; Width = width; Conflict = conflict; ArrayId = arrayId ?? String.Empty;
        }
        public LayoutPoint Center { get; private set; }
        public LayoutVector Along { get; private set; }
        public LayoutVector Across { get; private set; }
        public double Length { get; private set; }
        public double Width { get; private set; }
        public bool Conflict { get; private set; }
        public string ArrayId { get; private set; }
        public IList<LayoutPoint> Corners
        {
            get
            {
                LayoutVector a = Along * (Length / 2.0), b = Across * (Width / 2.0);
                return new[] { Center + -a + -b, Center + a + -b, Center + a + b, Center + -a + b };
            }
        }
    }
}
