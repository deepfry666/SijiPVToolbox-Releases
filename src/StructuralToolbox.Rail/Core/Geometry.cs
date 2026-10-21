using System;

namespace StructuralToolbox.Core
{
    public struct Vec2
    {
        private readonly double _x;
        private readonly double _y;

        public Vec2(double x, double y)
        {
            _x = x;
            _y = y;
        }

        public double X
        {
            get { return _x; }
        }

        public double Y
        {
            get { return _y; }
        }

        public double Length
        {
            get { return Math.Sqrt((X * X) + (Y * Y)); }
        }

        public Vec2 Normalized()
        {
            double length = Length;
            if (Double.IsNaN(length) || Double.IsInfinity(length) || length <= 1e-12)
                throw new InvalidOperationException("Cannot normalize a zero-length vector.");

            return new Vec2(X / length, Y / length);
        }

        public static double Dot(Vec2 left, Vec2 right)
        {
            return (left.X * right.X) + (left.Y * right.Y);
        }

        public static double Cross(Vec2 left, Vec2 right)
        {
            return (left.X * right.Y) - (left.Y * right.X);
        }

        public static double Distance(Vec2 left, Vec2 right)
        {
            return (left - right).Length;
        }

        public static Vec2 operator +(Vec2 left, Vec2 right)
        {
            return new Vec2(left.X + right.X, left.Y + right.Y);
        }

        public static Vec2 operator -(Vec2 left, Vec2 right)
        {
            return new Vec2(left.X - right.X, left.Y - right.Y);
        }

        public static Vec2 operator *(Vec2 value, double scale)
        {
            return new Vec2(value.X * scale, value.Y * scale);
        }

        public static Vec2 operator /(Vec2 value, double scale)
        {
            return new Vec2(value.X / scale, value.Y / scale);
        }
    }

    public sealed class PanelRect
    {
        public PanelRect(Vec2 center, Vec2 longAxis, Vec2 shortAxis,
            double longLength, double shortLength, double elevation, string sourceId)
        {
            if (!IsFinite(center.X) || !IsFinite(center.Y) || !IsFinite(elevation))
                throw new ArgumentException("Panel coordinates must be finite.");
            if (!IsFinite(longLength) || !IsFinite(shortLength) ||
                longLength <= 0.0 || shortLength <= 0.0)
                throw new ArgumentException("Panel dimensions must be positive and finite.");

            Center = center;
            LongAxis = Canonical(longAxis.Normalized());
            Vec2 suppliedShortAxis = shortAxis.Normalized();
            Vec2 exactPerpendicular = new Vec2(-LongAxis.Y, LongAxis.X);
            if (Vec2.Dot(exactPerpendicular, suppliedShortAxis) < 0.0)
                exactPerpendicular = exactPerpendicular * -1.0;
            ShortAxis = Canonical(exactPerpendicular);
            LongLength = longLength;
            ShortLength = shortLength;
            Elevation = elevation;
            SourceId = sourceId ?? String.Empty;
        }

        public Vec2 Center { get; private set; }
        public Vec2 LongAxis { get; private set; }
        public Vec2 ShortAxis { get; private set; }
        public double LongLength { get; private set; }
        public double ShortLength { get; private set; }
        public double Elevation { get; private set; }
        public string SourceId { get; private set; }

        private static Vec2 Canonical(Vec2 axis)
        {
            if (axis.X < -1e-9 || (Math.Abs(axis.X) <= 1e-9 && axis.Y < 0.0))
                return axis * -1.0;

            return axis;
        }

        private static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }

    public sealed class RailSegment
    {
        public RailSegment(Vec2 start, Vec2 end, double elevation)
        {
            Start = start;
            End = end;
            Elevation = elevation;
        }

        public Vec2 Start { get; private set; }
        public Vec2 End { get; private set; }
        public double Elevation { get; private set; }

        public double Length
        {
            get { return Vec2.Distance(Start, End); }
        }
    }
}
