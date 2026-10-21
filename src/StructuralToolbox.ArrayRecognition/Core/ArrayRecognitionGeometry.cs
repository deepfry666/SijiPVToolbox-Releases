using System;
using System.Collections.Generic;

namespace StructuralToolbox.ArrayRecognition.Core
{
    public struct ArrayVec2
    {
        private readonly double _x;
        private readonly double _y;

        public ArrayVec2(double x, double y)
        {
            _x = x;
            _y = y;
        }

        public double X { get { return _x; } }
        public double Y { get { return _y; } }
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }

        public ArrayVec2 Normalized()
        {
            double length = Length;
            if (length <= 1e-12)
                throw new InvalidOperationException("零长度向量不能归一化。");
            return new ArrayVec2(X / length, Y / length);
        }

        public static double Dot(ArrayVec2 left, ArrayVec2 right)
        {
            return left.X * right.X + left.Y * right.Y;
        }

        public static ArrayVec2 operator +(ArrayVec2 left, ArrayVec2 right)
        {
            return new ArrayVec2(left.X + right.X, left.Y + right.Y);
        }

        public static ArrayVec2 operator -(ArrayVec2 left, ArrayVec2 right)
        {
            return new ArrayVec2(left.X - right.X, left.Y - right.Y);
        }

        public static ArrayVec2 operator *(ArrayVec2 value, double scale)
        {
            return new ArrayVec2(value.X * scale, value.Y * scale);
        }

        public static ArrayVec2 operator /(ArrayVec2 value, double scale)
        {
            return new ArrayVec2(value.X / scale, value.Y / scale);
        }
    }

    public static class ArrayComponentBoundaryReader
    {
        public static bool TryNormalize(IList<ArrayVec2> source,
            bool closedFlag, double duplicatePointTolerance,
            double closureTolerance, out List<ArrayVec2> vertices,
            out string reason)
        {
            vertices = new List<ArrayVec2>();
            reason = String.Empty;
            if (source == null)
            {
                reason = "组件轮廓无法读取";
                return false;
            }
            ValidateTolerance(duplicatePointTolerance,
                "duplicatePointTolerance");
            ValidateTolerance(closureTolerance, "closureTolerance");
            if (closureTolerance < duplicatePointTolerance)
                throw new ArgumentOutOfRangeException("closureTolerance");

            for (int index = 0; index < source.Count; index++)
                if (vertices.Count == 0 ||
                    (source[index] - vertices[vertices.Count - 1]).Length >
                        duplicatePointTolerance)
                    vertices.Add(source[index]);
            if (vertices.Count < 2)
            {
                reason = "组件轮廓顶点不足";
                return false;
            }

            double gap = (vertices[0] -
                vertices[vertices.Count - 1]).Length;
            if (!closedFlag && gap > closureTolerance)
            {
                reason = String.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "多段线未闭合，首尾端点相差 {0:0.###} mm", gap);
                return false;
            }
            if (gap <= closureTolerance)
                vertices.RemoveAt(vertices.Count - 1);
            return true;
        }

        private static void ValidateTolerance(double value, string parameterName)
        {
            if (value < 0.0 || Double.IsNaN(value) || Double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public enum ArrayComponentKind
    {
        LightweightPolyline = 0,
        TwoDimensionalPolyline = 1,
        BlockReference = 2
    }

    public sealed class ArrayComponent
    {
        public string SourceId { get; set; }
        public ArrayComponentKind Kind { get; set; }
        public string LayerName { get; set; }
        public ArrayVec2 Center { get; set; }
        public ArrayVec2 LongAxis { get; set; }
        public ArrayVec2 ShortAxis { get; set; }
        public double LongLength { get; set; }
        public double ShortLength { get; set; }
        public double Elevation { get; set; }
    }

    public sealed class ArrayRecognitionOptions
    {
        public ArrayRecognitionOptions()
        {
            MaximumOutlineGap = 20.0;
            AlignmentTolerance = 0.5;
            SuspectAlignmentTolerance = 3.0;
            DimensionTolerance = 0.5;
            ElevationTolerance = 0.01;
            DirectionToleranceDegrees = 0.05;
        }

        public double MaximumOutlineGap { get; set; }
        public double AlignmentTolerance { get; set; }
        public double SuspectAlignmentTolerance { get; set; }
        public double DimensionTolerance { get; set; }
        public double ElevationTolerance { get; set; }
        public double DirectionToleranceDegrees { get; set; }

        public void Normalize()
        {
            MaximumOutlineGap = Clamp(MaximumOutlineGap, 0.0, 5000.0, 20.0);
            AlignmentTolerance = Clamp(AlignmentTolerance, 0.01, 100.0, 0.5);
            SuspectAlignmentTolerance = Clamp(SuspectAlignmentTolerance,
                AlignmentTolerance, 500.0, 3.0);
            DimensionTolerance = Clamp(DimensionTolerance, 0.01, 100.0, 0.5);
            ElevationTolerance = Clamp(ElevationTolerance, 0.0001, 100.0, 0.01);
            DirectionToleranceDegrees = Clamp(DirectionToleranceDegrees,
                0.001, 5.0, 0.05);
        }

        private static double Clamp(double value, double minimum, double maximum,
            double fallback)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
                return fallback;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    public sealed class RecognizedArray
    {
        public RecognizedArray()
        {
            Components = new List<ArrayComponent>();
        }

        public string Id { get; set; }
        public List<ArrayComponent> Components { get; private set; }
        public ArrayVec2 Center { get; set; }
        public ArrayVec2 LongAxis { get; set; }
        public ArrayVec2 ShortAxis { get; set; }
        public double LongSpan { get; set; }
        public double ShortSpan { get; set; }
        public int CountAlongLong { get; set; }
        public int CountAlongShort { get; set; }
        public int ComponentCount { get { return Components.Count; } }
        public string Specification
        {
            get { return CountAlongLong + "×" + CountAlongShort; }
        }
    }

    public sealed class InvalidArrayGroup
    {
        public InvalidArrayGroup()
        {
            SourceIds = new List<string>();
        }

        public string Reason { get; set; }
        public List<string> SourceIds { get; private set; }
        public int ComponentCount { get { return SourceIds.Count; } }
    }

    public sealed class ArrayRecognitionResult
    {
        public ArrayRecognitionResult()
        {
            Arrays = new List<RecognizedArray>();
            InvalidGroups = new List<InvalidArrayGroup>();
        }

        public List<RecognizedArray> Arrays { get; private set; }
        public List<InvalidArrayGroup> InvalidGroups { get; private set; }

        public int RecognizedComponentCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < Arrays.Count; index++)
                    count += Arrays[index].ComponentCount;
                return count;
            }
        }

        public int InvalidComponentCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < InvalidGroups.Count; index++)
                    count += InvalidGroups[index].ComponentCount;
                return count;
            }
        }
    }

    public sealed class ArraySummaryEntry
    {
        public int CountAlongLong { get; set; }
        public int CountAlongShort { get; set; }
        public int ArrayCount { get; set; }
        public int ComponentsPerArray
        {
            get { return CountAlongLong * CountAlongShort; }
        }
        public int TotalComponentCount
        {
            get { return ComponentsPerArray * ArrayCount; }
        }
        public string Specification
        {
            get { return CountAlongLong + "×" + CountAlongShort + "阵列"; }
        }
    }

    public sealed class ArrayRecognitionSummary
    {
        public ArrayRecognitionSummary()
        {
            Entries = new List<ArraySummaryEntry>();
        }

        public List<ArraySummaryEntry> Entries { get; private set; }
        public int RecognizedArrayCount { get; set; }
        public int RecognizedComponentCount { get; set; }
        public int InvalidArrayCount { get; set; }
        public int InvalidComponentCount { get; set; }
    }
}
