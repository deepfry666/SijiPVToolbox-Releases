using System;
using System.Collections.Generic;

namespace StructuralToolbox.PvComponent.Core
{
    public struct PvPoint2
    {
        public PvPoint2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X;
        public double Y;

        public static PvVector2 operator -(PvPoint2 left, PvPoint2 right)
        {
            return new PvVector2(left.X - right.X, left.Y - right.Y);
        }

        public static PvPoint2 operator +(PvPoint2 point, PvVector2 vector)
        {
            return new PvPoint2(point.X + vector.X, point.Y + vector.Y);
        }
    }

    public struct PvVector2
    {
        public PvVector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X;
        public double Y;
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }

        public PvVector2 Unit()
        {
            double length = Length;
            return length <= 0.0
                ? new PvVector2(0.0, 0.0)
                : new PvVector2(X / length, Y / length);
        }

        public double Dot(PvVector2 other)
        {
            return X * other.X + Y * other.Y;
        }

        public static PvVector2 operator *(PvVector2 vector, double scale)
        {
            return new PvVector2(vector.X * scale, vector.Y * scale);
        }
    }

    public sealed class PvComponentMeasurement
    {
        public PvPoint2 Anchor { get; set; }
        public PvVector2 Direction { get; set; }
        public double Length { get; set; }
        public double Thickness { get; set; }
        public double AngleRadians
        {
            get { return Math.Atan2(Direction.Y, Direction.X); }
        }
    }

    public static class PvComponentGeometry
    {
        private const double MinimumEdge = 0.01;

        public static bool TryMeasure(IList<PvPoint2> vertices, bool closed,
            out PvComponentMeasurement measurement, out string reason)
        {
            measurement = null;
            reason = String.Empty;
            if (!closed)
            {
                reason = "组件轮廓不是闭合多段线。";
                return false;
            }
            if (vertices == null || vertices.Count != 4)
            {
                reason = "组件轮廓必须恰好包含四个顶点。";
                return false;
            }

            PvVector2[] edges = new PvVector2[4];
            double[] lengths = new double[4];
            for (int index = 0; index < 4; index++)
            {
                edges[index] = vertices[(index + 1) % 4] - vertices[index];
                lengths[index] = edges[index].Length;
                if (lengths[index] < MinimumEdge)
                {
                    reason = "组件轮廓包含零长度边。";
                    return false;
                }
            }

            double scale = Math.Max(Math.Max(lengths[0], lengths[1]),
                Math.Max(lengths[2], lengths[3]));
            double lengthTolerance = Math.Max(0.1, scale * 0.00001);
            if (Math.Abs(lengths[0] - lengths[2]) > lengthTolerance ||
                Math.Abs(lengths[1] - lengths[3]) > lengthTolerance)
            {
                reason = "组件轮廓的对边长度不一致。";
                return false;
            }
            double angularTolerance = 0.0001;
            for (int index = 0; index < 4; index++)
            {
                PvVector2 first = edges[index].Unit();
                PvVector2 second = edges[(index + 1) % 4].Unit();
                if (Math.Abs(first.Dot(second)) > angularTolerance)
                {
                    reason = "组件轮廓不是矩形，不能可靠计量。";
                    return false;
                }
            }

            int lengthEdge = lengths[0] >= lengths[1] ? 0 : 1;
            int thicknessEdge = lengthEdge == 0 ? 1 : 0;
            measurement = new PvComponentMeasurement
            {
                Anchor = vertices[lengthEdge],
                Direction = edges[lengthEdge].Unit(),
                Length = lengths[lengthEdge],
                Thickness = lengths[thicknessEdge]
            };
            return true;
        }

        public static IList<PvPoint2> CreateRectangle(PvPoint2 anchor,
            double length, double thickness, double angleRadians)
        {
            if (length <= 0.0 || thickness <= 0.0)
                throw new ArgumentOutOfRangeException("length");
            PvVector2 direction = new PvVector2(Math.Cos(angleRadians),
                Math.Sin(angleRadians));
            PvVector2 normal = new PvVector2(-direction.Y, direction.X);
            return new[]
            {
                anchor,
                anchor + direction * length,
                anchor + direction * length + normal * thickness,
                anchor + normal * thickness
            };
        }
    }
}
