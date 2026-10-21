using System;
using System.Collections.Generic;
using System.Globalization;

namespace StructuralToolbox.Ballast.Core
{
    public struct BallastVec2
    {
        private readonly double _x;
        private readonly double _y;

        public BallastVec2(double x, double y)
        {
            _x = x;
            _y = y;
        }

        public double X { get { return _x; } }
        public double Y { get { return _y; } }

        public double Length
        {
            get { return Math.Sqrt((X * X) + (Y * Y)); }
        }

        public BallastVec2 Normalized()
        {
            double length = Length;
            if (Double.IsNaN(length) || Double.IsInfinity(length) || length <= 1e-12)
                throw new InvalidOperationException("Cannot normalize a zero-length vector.");
            return new BallastVec2(X / length, Y / length);
        }

        public static double Dot(BallastVec2 left, BallastVec2 right)
        {
            return (left.X * right.X) + (left.Y * right.Y);
        }

        public static double Cross(BallastVec2 left, BallastVec2 right)
        {
            return (left.X * right.Y) - (left.Y * right.X);
        }

        public static double Distance(BallastVec2 left, BallastVec2 right)
        {
            return (left - right).Length;
        }

        public static BallastVec2 operator +(BallastVec2 left, BallastVec2 right)
        {
            return new BallastVec2(left.X + right.X, left.Y + right.Y);
        }

        public static BallastVec2 operator -(BallastVec2 left, BallastVec2 right)
        {
            return new BallastVec2(left.X - right.X, left.Y - right.Y);
        }

        public static BallastVec2 operator *(BallastVec2 value, double scale)
        {
            return new BallastVec2(value.X * scale, value.Y * scale);
        }

        public static BallastVec2 operator /(BallastVec2 value, double scale)
        {
            return new BallastVec2(value.X / scale, value.Y / scale);
        }
    }

    public sealed class BallastPanel
    {
        public BallastPanel(BallastVec2 center, BallastVec2 longAxis, BallastVec2 shortAxis,
            double longLength, double shortLength, double elevation, string sourceId)
        {
            if (!IsFinite(center.X) || !IsFinite(center.Y) || !IsFinite(elevation))
                throw new ArgumentException("Panel coordinates must be finite.");
            if (!IsFinite(longLength) || !IsFinite(shortLength) ||
                longLength <= 0.0 || shortLength <= 0.0)
                throw new ArgumentException("Panel dimensions must be positive and finite.");

            Center = center;
            LongAxis = Canonical(longAxis.Normalized());
            BallastVec2 exactPerpendicular = new BallastVec2(-LongAxis.Y, LongAxis.X);
            if (BallastVec2.Dot(exactPerpendicular, shortAxis.Normalized()) < 0.0)
                exactPerpendicular = exactPerpendicular * -1.0;
            ShortAxis = Canonical(exactPerpendicular);
            LongLength = longLength;
            ShortLength = shortLength;
            Elevation = elevation;
            SourceId = sourceId ?? String.Empty;
        }

        public BallastVec2 Center { get; private set; }
        public BallastVec2 LongAxis { get; private set; }
        public BallastVec2 ShortAxis { get; private set; }
        public double LongLength { get; private set; }
        public double ShortLength { get; private set; }
        public double Elevation { get; private set; }
        public string SourceId { get; private set; }

        private static BallastVec2 Canonical(BallastVec2 axis)
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

    public enum BallastShape
    {
        Circle = 0,
        Rect = 1
    }

    public enum BallastCountMode
    {
        AutoByRows = 0,
        Manual = 1
    }

    public enum BallastPlacementMode
    {
        Symmetric = 0,
        CustomFront = 1
    }

    public enum BallastColorMode
    {
        ByLayer = 0,
        Aci = 1,
        TrueColor = 2
    }

    public sealed class BallastRowRule
    {
        public int RowsAlongLong { get; set; }
        public BallastShape Shape { get; set; }
        public double Diameter { get; set; }
        public double RectLength { get; set; }
        public double RectWidth { get; set; }
        public double Height { get; set; }
        public double CrossLineLength { get; set; }
        public BallastCountMode CountMode { get; set; }
        public int ManualCount { get; set; }
        public double BallastCenterSpacing { get; set; }
        public double FrameCenterSpacing { get; set; }
        public BallastPlacementMode PlacementMode { get; set; }
        public double FrontCenterClearance { get; set; }
        public BallastColorMode ColorMode { get; set; }
        public int ColorIndex { get; set; }
        public int ColorRed { get; set; }
        public int ColorGreen { get; set; }
        public int ColorBlue { get; set; }
    }

    public sealed class BallastArrayRule
    {
        public string ArrayId { get; set; }
        public BallastShape Shape { get; set; }
        public double Diameter { get; set; }
        public double RectLength { get; set; }
        public double RectWidth { get; set; }
        public double Height { get; set; }
        public double CrossLineLength { get; set; }
        public BallastCountMode CountMode { get; set; }
        public int ManualCount { get; set; }
        public double BallastCenterSpacing { get; set; }
        public double FrameCenterSpacing { get; set; }
        public BallastPlacementMode PlacementMode { get; set; }
        public double FrontCenterClearance { get; set; }
        public BallastColorMode ColorMode { get; set; }
        public int ColorIndex { get; set; }
        public int ColorRed { get; set; }
        public int ColorGreen { get; set; }
        public int ColorBlue { get; set; }
    }

    public sealed class BallastLayoutOptions
    {
        public BallastLayoutOptions()
        {
            Shape = BallastShape.Circle;
            Diameter = 500.0;
            RectLength = 500.0;
            RectWidth = 300.0;
            Height = 300.0;
            CrossLineLength = 3000.0;
            FrameCenterSpacing = 1800.0;
            BallastCenterSpacing = 1400.0;
            MaxComponentGap = 20.0;
            MaxSideClearance = 800.0;
            AllowOverrun = 0.0;
            PlacementMode = BallastPlacementMode.Symmetric;
            FrontCenterClearance = 450.0;
            ColorMode = BallastColorMode.ByLayer;
            ColorIndex = 7;
            ColorRed = 255;
            ColorGreen = 255;
            ColorBlue = 255;
            CountMode = BallastCountMode.AutoByRows;
            ManualCount = 2;
            LayerName = "配重块";
            RowRules = new List<BallastRowRule>();
            ArrayRules = new List<BallastArrayRule>();
        }

        public BallastShape Shape { get; set; }
        public double Diameter { get; set; }
        public double RectLength { get; set; }
        public double RectWidth { get; set; }
        public double Height { get; set; }
        public double CrossLineLength { get; set; }
        public double FrameCenterSpacing { get; set; }
        public double BallastCenterSpacing { get; set; }
        public double MaxComponentGap { get; set; }
        public double MaxSideClearance { get; set; }
        public double AllowOverrun { get; set; }
        public BallastPlacementMode PlacementMode { get; set; }
        public double FrontCenterClearance { get; set; }
        public BallastColorMode ColorMode { get; set; }
        public int ColorIndex { get; set; }
        public int ColorRed { get; set; }
        public int ColorGreen { get; set; }
        public int ColorBlue { get; set; }
        public BallastCountMode CountMode { get; set; }
        public int ManualCount { get; set; }
        public string LayerName { get; set; }
        public List<BallastRowRule> RowRules { get; set; }
        public List<BallastArrayRule> ArrayRules { get; set; }

        public void Normalize()
        {
            if (!Enum.IsDefined(typeof(BallastShape), Shape))
                Shape = BallastShape.Circle;
            if (!Enum.IsDefined(typeof(BallastCountMode), CountMode))
                CountMode = BallastCountMode.AutoByRows;
            Diameter = Clamp(Diameter, 1.0, 5000.0);
            RectLength = Clamp(RectLength, 1.0, 5000.0);
            RectWidth = Clamp(RectWidth, 1.0, 5000.0);
            Height = Clamp(Height, 1.0, 5000.0);
            CrossLineLength = Clamp(CrossLineLength, 1.0, 20000.0);
            FrameCenterSpacing = Clamp(FrameCenterSpacing, 1.0, 20000.0);
            BallastCenterSpacing = Clamp(BallastCenterSpacing, 1.0, 20000.0);
            MaxComponentGap = Clamp(MaxComponentGap, 0.0, 5000.0);
            MaxSideClearance = Clamp(MaxSideClearance, 0.0, 20000.0);
            AllowOverrun = 0.0;
            if (!Enum.IsDefined(typeof(BallastPlacementMode), PlacementMode))
                PlacementMode = BallastPlacementMode.Symmetric;
            FrontCenterClearance = Clamp(FrontCenterClearance, 0.0, 20000.0);
            ColorMode = NormalizeColorMode(ColorMode);
            ColorIndex = NormalizeColorIndex(ColorIndex);
            ColorRed = NormalizeByte(ColorRed);
            ColorGreen = NormalizeByte(ColorGreen);
            ColorBlue = NormalizeByte(ColorBlue);
            ManualCount = Math.Max(1, Math.Min(50, ManualCount));
            LayerName = String.IsNullOrWhiteSpace(LayerName) ? "配重块" : LayerName.Trim();
            if (LayerName.Length > 255)
                LayerName = LayerName.Substring(0, 255);
            if (RowRules == null)
                RowRules = new List<BallastRowRule>();
            for (int index = 0; index < RowRules.Count; index++)
            {
                BallastRowRule rule = RowRules[index];
                rule.RowsAlongLong = Math.Max(1, rule.RowsAlongLong);
                if (!Enum.IsDefined(typeof(BallastShape), rule.Shape))
                    rule.Shape = Shape;
                rule.Diameter = Clamp(rule.Diameter <= 0.0
                    ? Diameter : rule.Diameter, 1.0, 5000.0);
                rule.RectLength = Clamp(rule.RectLength <= 0.0
                    ? RectLength : rule.RectLength, 1.0, 5000.0);
                rule.RectWidth = Clamp(rule.RectWidth <= 0.0
                    ? RectWidth : rule.RectWidth, 1.0, 5000.0);
                rule.Height = Clamp(rule.Height <= 0.0
                    ? Height : rule.Height, 1.0, 5000.0);
                rule.CrossLineLength = Clamp(rule.CrossLineLength <= 0.0
                    ? CrossLineLength : rule.CrossLineLength, 1.0, 20000.0);
                if (!Enum.IsDefined(typeof(BallastCountMode), rule.CountMode))
                    rule.CountMode = BallastCountMode.AutoByRows;
                rule.ManualCount = Math.Max(1, Math.Min(50, rule.ManualCount));
                rule.BallastCenterSpacing = Clamp(
                    rule.BallastCenterSpacing, 1.0, 20000.0);
                rule.FrameCenterSpacing = Clamp(
                    rule.FrameCenterSpacing <= 0.0
                        ? FrameCenterSpacing : rule.FrameCenterSpacing,
                    1.0, 20000.0);
                if (!Enum.IsDefined(typeof(BallastPlacementMode),
                    rule.PlacementMode))
                    rule.PlacementMode = BallastPlacementMode.Symmetric;
                rule.FrontCenterClearance = Clamp(rule.FrontCenterClearance,
                    0.0, 20000.0);
                rule.ColorMode = NormalizeColorMode(rule.ColorMode);
                rule.ColorIndex = NormalizeColorIndex(rule.ColorIndex);
                rule.ColorRed = NormalizeByte(rule.ColorRed);
                rule.ColorGreen = NormalizeByte(rule.ColorGreen);
                rule.ColorBlue = NormalizeByte(rule.ColorBlue);
            }
            if (ArrayRules == null)
                ArrayRules = new List<BallastArrayRule>();
            for (int index = 0; index < ArrayRules.Count; index++)
            {
                BallastArrayRule rule = ArrayRules[index];
                if (rule == null)
                    continue;
                rule.ArrayId = String.IsNullOrWhiteSpace(rule.ArrayId)
                    ? String.Empty : rule.ArrayId.Trim();
                if (!Enum.IsDefined(typeof(BallastShape), rule.Shape))
                    rule.Shape = BallastShape.Circle;
                if (!Enum.IsDefined(typeof(BallastCountMode), rule.CountMode))
                    rule.CountMode = BallastCountMode.AutoByRows;
                rule.Diameter = Clamp(rule.Diameter, 1.0, 5000.0);
                rule.RectLength = Clamp(rule.RectLength, 1.0, 5000.0);
                rule.RectWidth = Clamp(rule.RectWidth, 1.0, 5000.0);
                rule.Height = Clamp(rule.Height, 1.0, 5000.0);
                rule.CrossLineLength = Clamp(rule.CrossLineLength, 1.0, 20000.0);
                rule.ManualCount = Math.Max(1, Math.Min(50, rule.ManualCount));
                rule.BallastCenterSpacing = Clamp(
                    rule.BallastCenterSpacing, 1.0, 20000.0);
                rule.FrameCenterSpacing = Clamp(
                    rule.FrameCenterSpacing, 1.0, 20000.0);
                if (!Enum.IsDefined(typeof(BallastPlacementMode),
                    rule.PlacementMode))
                    rule.PlacementMode = BallastPlacementMode.Symmetric;
                rule.FrontCenterClearance = Clamp(rule.FrontCenterClearance,
                    0.0, 20000.0);
                rule.ColorMode = NormalizeColorMode(rule.ColorMode);
                rule.ColorIndex = NormalizeColorIndex(rule.ColorIndex);
                rule.ColorRed = NormalizeByte(rule.ColorRed);
                rule.ColorGreen = NormalizeByte(rule.ColorGreen);
                rule.ColorBlue = NormalizeByte(rule.ColorBlue);
            }
        }

        private static BallastColorMode NormalizeColorMode(BallastColorMode mode)
        {
            if (!Enum.IsDefined(typeof(BallastColorMode), mode))
                return BallastColorMode.ByLayer;
            return mode;
        }

        private static int NormalizeColorIndex(int value)
        {
            return Math.Max(1, Math.Min(255, value == 0 ? 7 : value));
        }

        private static int NormalizeByte(int value)
        {
            return Math.Max(0, Math.Min(255, value));
        }

        public double ExtentAlongShort()
        {
            return Shape == BallastShape.Circle ? Diameter : RectWidth;
        }

        public double ExtentAlongLong()
        {
            return Shape == BallastShape.Circle ? Diameter : RectLength;
        }

        public BallastRowRule FindRule(int rowsAlongLong)
        {
            if (RowRules == null)
                return null;
            for (int index = 0; index < RowRules.Count; index++)
            {
                if (RowRules[index] != null &&
                    RowRules[index].RowsAlongLong == rowsAlongLong)
                    return RowRules[index];
            }
            return null;
        }

        public BallastArrayRule FindArrayRule(string arrayId)
        {
            if (ArrayRules == null || String.IsNullOrWhiteSpace(arrayId))
                return null;
            for (int index = 0; index < ArrayRules.Count; index++)
            {
                BallastArrayRule rule = ArrayRules[index];
                if (rule != null && String.Equals(rule.ArrayId, arrayId,
                    StringComparison.OrdinalIgnoreCase))
                    return rule;
            }
            return null;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
                return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    public sealed class BallastArrayInfo
    {
        public string Id { get; set; }
        public List<BallastPanel> Panels { get; set; }
        public BallastVec2 LongAxis { get; set; }
        public BallastVec2 ShortAxis { get; set; }
        public BallastVec2 Center { get; set; }
        public double LongSpan { get; set; }
        public double ShortSpan { get; set; }
        public int RowsAlongLong { get; set; }
        public int ColumnsAlongShort { get; set; }
    }

    public sealed class BallastPointPlan
    {
        public BallastVec2 Center { get; set; }
        public bool Conflict { get; set; }
        public string ConflictReason { get; set; }
    }

    public sealed class BallastFramePlan
    {
        public BallastVec2 Center { get; set; }
        public List<BallastPointPlan> Ballasts { get; set; }
        public bool Conflict { get; set; }

        public bool HasConflicts()
        {
            if (Conflict)
                return true;
            if (Ballasts == null)
                return false;
            for (int index = 0; index < Ballasts.Count; index++)
            {
                if (Ballasts[index] != null && Ballasts[index].Conflict)
                    return true;
            }
            return false;
        }
    }

    public sealed class BallastArrayPlan
    {
        public string ArrayId { get; set; }
        public BallastArrayInfo Array { get; set; }
        public BallastLayoutOptions Options { get; set; }
        public List<BallastFramePlan> Frames { get; set; }
        public bool HasConflicts()
        {
            if (Frames == null)
                return false;
            for (int index = 0; index < Frames.Count; index++)
            {
                if (Frames[index] != null && Frames[index].HasConflicts())
                    return true;
            }
            return false;
        }
    }

    public sealed class BallastArrayDetectionResult
    {
        public List<BallastArrayInfo> Arrays { get; set; }
        public List<BallastInvalidArray> InvalidArrays { get; set; }
    }

    public sealed class BallastInvalidArray
    {
        public string Reason { get; set; }
        public string SourceIds { get; set; }
    }

    public sealed class BallastLayoutResult
    {
        public List<BallastArrayPlan> Plans { get; set; }
        public List<BallastInvalidArray> InvalidArrays { get; set; }

        public int FrameCount
        {
            get
            {
                int count = 0;
                if (Plans != null)
                {
                    for (int index = 0; index < Plans.Count; index++)
                        if (Plans[index].Frames != null)
                            count += Plans[index].Frames.Count;
                }
                return count;
            }
        }

        public int BallastCount
        {
            get
            {
                int count = 0;
                if (Plans != null)
                {
                    for (int planIndex = 0; planIndex < Plans.Count; planIndex++)
                    {
                        if (Plans[planIndex].Frames == null)
                            continue;
                        for (int frameIndex = 0; frameIndex < Plans[planIndex].Frames.Count; frameIndex++)
                        {
                            BallastFramePlan frame = Plans[planIndex].Frames[frameIndex];
                            if (frame != null && frame.Ballasts != null)
                                count += frame.Ballasts.Count;
                        }
                    }
                }
                return count;
            }
        }

        public int ConflictCount
        {
            get
            {
                int count = 0;
                if (Plans != null)
                {
                    for (int planIndex = 0; planIndex < Plans.Count; planIndex++)
                    {
                        if (Plans[planIndex].Frames == null)
                            continue;
                        for (int frameIndex = 0; frameIndex < Plans[planIndex].Frames.Count; frameIndex++)
                        {
                            BallastFramePlan frame = Plans[planIndex].Frames[frameIndex];
                            if (frame == null || frame.Ballasts == null)
                                continue;
                            for (int pointIndex = 0; pointIndex < frame.Ballasts.Count; pointIndex++)
                            {
                                BallastPointPlan point = frame.Ballasts[pointIndex];
                                if (point != null && point.Conflict)
                                    count++;
                            }
                        }
                    }
                }
                return count;
            }
        }
    }
}
