using System;
using System.Collections.Generic;
using StructuralToolbox.Ballast.Core;

namespace StructuralToolbox.Ballast.Settings
{
    [Serializable]
    public sealed class BallastSettings
    {
        public BallastSettings()
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
            NormalizeColorFields();
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
                if (rule.Diameter <= 10.0 && rule.RectLength <= 10.0 &&
                    rule.RectWidth <= 10.0 && rule.CrossLineLength <= 100.0)
                {
                    rule.Diameter = Diameter;
                    rule.RectLength = RectLength;
                    rule.RectWidth = RectWidth;
                    rule.CrossLineLength = CrossLineLength;
                }
                if (!Enum.IsDefined(typeof(BallastCountMode), rule.CountMode))
                    rule.CountMode = BallastCountMode.AutoByRows;
                rule.ManualCount = Math.Max(1, Math.Min(50, rule.ManualCount));
                rule.BallastCenterSpacing = Clamp(
                    rule.BallastCenterSpacing, 1.0, 20000.0);
                rule.FrameCenterSpacing = Clamp(
                    rule.FrameCenterSpacing <= 0.0
                        ? FrameCenterSpacing : rule.FrameCenterSpacing,
                    1.0, 20000.0);
                NormalizeRule(rule);
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
                rule.BallastCenterSpacing = Clamp(rule.BallastCenterSpacing,
                    1.0, 20000.0);
                rule.FrameCenterSpacing = Clamp(rule.FrameCenterSpacing,
                    1.0, 20000.0);
                NormalizeRule(rule);
            }
        }

        private static void NormalizeRule(BallastRowRule rule)
        {
            if (!Enum.IsDefined(typeof(BallastPlacementMode), rule.PlacementMode))
                rule.PlacementMode = BallastPlacementMode.Symmetric;
            rule.FrontCenterClearance = Clamp(rule.FrontCenterClearance,
                0.0, 20000.0);
            if (!Enum.IsDefined(typeof(BallastColorMode), rule.ColorMode))
                rule.ColorMode = BallastColorMode.ByLayer;
            rule.ColorIndex = Math.Max(1, Math.Min(255,
                rule.ColorIndex == 0 ? 7 : rule.ColorIndex));
            rule.ColorRed = Math.Max(0, Math.Min(255, rule.ColorRed));
            rule.ColorGreen = Math.Max(0, Math.Min(255, rule.ColorGreen));
            rule.ColorBlue = Math.Max(0, Math.Min(255, rule.ColorBlue));
        }

        private static void NormalizeRule(BallastArrayRule rule)
        {
            if (!Enum.IsDefined(typeof(BallastPlacementMode), rule.PlacementMode))
                rule.PlacementMode = BallastPlacementMode.Symmetric;
            rule.FrontCenterClearance = Clamp(rule.FrontCenterClearance,
                0.0, 20000.0);
            if (!Enum.IsDefined(typeof(BallastColorMode), rule.ColorMode))
                rule.ColorMode = BallastColorMode.ByLayer;
            rule.ColorIndex = Math.Max(1, Math.Min(255,
                rule.ColorIndex == 0 ? 7 : rule.ColorIndex));
            rule.ColorRed = Math.Max(0, Math.Min(255, rule.ColorRed));
            rule.ColorGreen = Math.Max(0, Math.Min(255, rule.ColorGreen));
            rule.ColorBlue = Math.Max(0, Math.Min(255, rule.ColorBlue));
        }

        private void NormalizeColorFields()
        {
            if (!Enum.IsDefined(typeof(BallastColorMode), ColorMode))
                ColorMode = BallastColorMode.ByLayer;
            ColorIndex = Math.Max(1, Math.Min(255, ColorIndex == 0 ? 7 : ColorIndex));
            ColorRed = Math.Max(0, Math.Min(255, ColorRed));
            ColorGreen = Math.Max(0, Math.Min(255, ColorGreen));
            ColorBlue = Math.Max(0, Math.Min(255, ColorBlue));
        }

        public BallastLayoutOptions ToOptions()
        {
            BallastLayoutOptions options = new BallastLayoutOptions();
            options.Shape = Shape;
            options.Diameter = Diameter;
            options.RectLength = RectLength;
            options.RectWidth = RectWidth;
            options.Height = Height;
            options.CrossLineLength = CrossLineLength;
            options.FrameCenterSpacing = FrameCenterSpacing;
            options.BallastCenterSpacing = BallastCenterSpacing;
            options.MaxComponentGap = MaxComponentGap;
            options.MaxSideClearance = MaxSideClearance;
            options.AllowOverrun = AllowOverrun;
            options.PlacementMode = PlacementMode;
            options.FrontCenterClearance = FrontCenterClearance;
            options.ColorMode = ColorMode;
            options.ColorIndex = ColorIndex;
            options.ColorRed = ColorRed;
            options.ColorGreen = ColorGreen;
            options.ColorBlue = ColorBlue;
            options.CountMode = CountMode;
            options.ManualCount = ManualCount;
            options.LayerName = LayerName;
            options.RowRules = CloneRules(RowRules);
            options.ArrayRules = CloneArrayRules(ArrayRules);
            options.Normalize();
            return options;
        }

        public static void ApplyOptions(BallastSettings settings, BallastLayoutOptions options)
        {
            settings.Shape = options.Shape;
            settings.Diameter = options.Diameter;
            settings.RectLength = options.RectLength;
            settings.RectWidth = options.RectWidth;
            settings.Height = options.Height;
            settings.CrossLineLength = options.CrossLineLength;
            settings.FrameCenterSpacing = options.FrameCenterSpacing;
            settings.BallastCenterSpacing = options.BallastCenterSpacing;
            settings.MaxComponentGap = options.MaxComponentGap;
            settings.MaxSideClearance = options.MaxSideClearance;
            settings.AllowOverrun = options.AllowOverrun;
            settings.PlacementMode = options.PlacementMode;
            settings.FrontCenterClearance = options.FrontCenterClearance;
            settings.ColorMode = options.ColorMode;
            settings.ColorIndex = options.ColorIndex;
            settings.ColorRed = options.ColorRed;
            settings.ColorGreen = options.ColorGreen;
            settings.ColorBlue = options.ColorBlue;
            settings.CountMode = options.CountMode;
            settings.ManualCount = options.ManualCount;
            settings.LayerName = options.LayerName;
            settings.RowRules = CloneRules(options.RowRules);
            settings.ArrayRules = CloneArrayRules(options.ArrayRules);
            settings.Normalize();
        }

        private static List<BallastRowRule> CloneRules(
            IEnumerable<BallastRowRule> rules)
        {
            List<BallastRowRule> result = new List<BallastRowRule>();
            if (rules == null)
                return result;
            foreach (BallastRowRule rule in rules)
            {
                if (rule == null)
                    continue;
                result.Add(new BallastRowRule
                {
                    RowsAlongLong = rule.RowsAlongLong,
                    Shape = rule.Shape,
                    Diameter = rule.Diameter,
                    RectLength = rule.RectLength,
                    RectWidth = rule.RectWidth,
                    Height = rule.Height,
                    CrossLineLength = rule.CrossLineLength,
                    CountMode = rule.CountMode,
                    ManualCount = rule.ManualCount,
                    BallastCenterSpacing = rule.BallastCenterSpacing,
                    FrameCenterSpacing = rule.FrameCenterSpacing,
                    PlacementMode = rule.PlacementMode,
                    FrontCenterClearance = rule.FrontCenterClearance,
                    ColorMode = rule.ColorMode,
                    ColorIndex = rule.ColorIndex,
                    ColorRed = rule.ColorRed,
                    ColorGreen = rule.ColorGreen,
                    ColorBlue = rule.ColorBlue
                });
            }
            return result;
        }

        private static List<BallastArrayRule> CloneArrayRules(
            IEnumerable<BallastArrayRule> rules)
        {
            List<BallastArrayRule> result = new List<BallastArrayRule>();
            if (rules == null)
                return result;
            foreach (BallastArrayRule rule in rules)
            {
                if (rule == null)
                    continue;
                result.Add(new BallastArrayRule
                {
                    ArrayId = rule.ArrayId,
                    Shape = rule.Shape,
                    Diameter = rule.Diameter,
                    RectLength = rule.RectLength,
                    RectWidth = rule.RectWidth,
                    Height = rule.Height,
                    CrossLineLength = rule.CrossLineLength,
                    CountMode = rule.CountMode,
                    ManualCount = rule.ManualCount,
                    BallastCenterSpacing = rule.BallastCenterSpacing,
                    FrameCenterSpacing = rule.FrameCenterSpacing,
                    PlacementMode = rule.PlacementMode,
                    FrontCenterClearance = rule.FrontCenterClearance,
                    ColorMode = rule.ColorMode,
                    ColorIndex = rule.ColorIndex,
                    ColorRed = rule.ColorRed,
                    ColorGreen = rule.ColorGreen,
                    ColorBlue = rule.ColorBlue
                });
            }
            return result;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
                return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
