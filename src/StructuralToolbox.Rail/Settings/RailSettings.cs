using System;

namespace StructuralToolbox.Settings
{
    public enum RailColorApplication
    {
        ByLayer,
        ByEntity
    }

    public enum RailColorKind
    {
        Aci,
        TrueColor
    }

    [Serializable]
    public sealed class RailSettings
    {
        public RailSettings()
        {
            RailCount = 2;
            TotalSpacing = 1400.0;
            MaximumContinuityGap = 20.0;
            EndOverhang = 100.0;
            RailLayerName = "导轨";
            RailGlobalWidth = 0.0;
            RailColorApplication = RailColorApplication.ByLayer;
            RailColorKind = RailColorKind.Aci;
            RailColorIndex = 6;
            RailColorRed = 255;
            RailColorGreen = 0;
            RailColorBlue = 255;
        }

        public int RailCount { get; set; }
        public double TotalSpacing { get; set; }
        public double MaximumContinuityGap { get; set; }
        public double EndOverhang { get; set; }
        public string RailLayerName { get; set; }
        public double RailGlobalWidth { get; set; }
        public RailColorApplication RailColorApplication { get; set; }
        public RailColorKind RailColorKind { get; set; }
        public int RailColorIndex { get; set; }
        public int RailColorRed { get; set; }
        public int RailColorGreen { get; set; }
        public int RailColorBlue { get; set; }
        public void Normalize()
        {
            RailCount = Math.Max(1, Math.Min(20, RailCount));
            TotalSpacing = Clamp(TotalSpacing, 0.0, 100000.0);
            MaximumContinuityGap = Clamp(MaximumContinuityGap, 0.0, 100000.0);
            EndOverhang = Clamp(EndOverhang, 0.0, 100000.0);
            RailLayerName = String.IsNullOrWhiteSpace(RailLayerName)
                ? "导轨"
                : RailLayerName.Trim();
            if (RailLayerName.Length > 255)
                RailLayerName = RailLayerName.Substring(0, 255);

            RailGlobalWidth = Clamp(RailGlobalWidth, 0.0, 100000.0);
            if (!Enum.IsDefined(typeof(RailColorApplication), RailColorApplication))
                RailColorApplication = RailColorApplication.ByLayer;
            if (!Enum.IsDefined(typeof(RailColorKind), RailColorKind))
                RailColorKind = RailColorKind.Aci;
            RailColorIndex = Math.Max(1, Math.Min(255, RailColorIndex));
            RailColorRed = Math.Max(0, Math.Min(255, RailColorRed));
            RailColorGreen = Math.Max(0, Math.Min(255, RailColorGreen));
            RailColorBlue = Math.Max(0, Math.Min(255, RailColorBlue));

        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
                return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
