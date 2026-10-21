using System;
using StructuralToolbox.Layout.Core;

namespace StructuralToolbox.Layout.Settings
{
    public sealed class LayoutSettings
    {
        public double Length = 2382.0;
        public double Width = 1134.0;
        public double PowerWp = 630.0;
        public double ComponentGapAlong = 20.0;
        public double ComponentGapAcross = 20.0;
        public double ArrayGapAlong = 500.0;
        public double ArrayGapAcross = 500.0;
        public double SlopeDegrees;
        public double ObstacleClearance;
        public string MaximumCapacityKwp = String.Empty;
        public string Orientation = "自动比较";
        public int Rows = 1;
        public int StringSize = 18;
        public string OutputLayer = "组件";

        public LayoutOptions ToOptions(LayoutVector direction)
        {
            LayoutOptions options = new LayoutOptions();
            options.Length = Length; options.Width = Width; options.PowerWp = PowerWp;
            options.ComponentGapAlong = ComponentGapAlong; options.ComponentGapAcross = ComponentGapAcross;
            options.ArrayGapAlong = ArrayGapAlong; options.ArrayGapAcross = ArrayGapAcross;
            options.SlopeDegrees = SlopeDegrees; options.ObstacleClearance = ObstacleClearance;
            options.Direction = direction; options.StringSize = StringSize;
            options.AllowedRows = new int[Rows];
            for (int i = 0; i < Rows; i++) options.AllowedRows[i] = i + 1;
            if (!String.IsNullOrWhiteSpace(MaximumCapacityKwp))
            {
                double capacity;
                if (!Double.TryParse(MaximumCapacityKwp, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.CurrentCulture, out capacity))
                    throw new ArgumentException("最大容量必须是数字。", "MaximumCapacityKwp");
                options.MaximumCapacityKwp = capacity;
            }
            if (String.Equals(Orientation, "竖排", StringComparison.Ordinal)) options.Orientation = LayoutOrientation.Vertical;
            else if (String.Equals(Orientation, "横排", StringComparison.Ordinal)) options.Orientation = LayoutOrientation.Horizontal;
            else options.Orientation = null;
            options.Validate();
            if (String.IsNullOrWhiteSpace(OutputLayer)) throw new ArgumentException("组件输出图层不能为空。", "OutputLayer");
            return options;
        }

        public void Normalize()
        {
            Length = Clamp(Length, 1.0, 100000.0); Width = Clamp(Width, 1.0, 100000.0); PowerWp = Clamp(PowerWp, 0.1, 100000.0);
            ComponentGapAlong = Clamp(ComponentGapAlong, 0.0, 100000.0); ComponentGapAcross = Clamp(ComponentGapAcross, 0.0, 100000.0);
            ArrayGapAlong = Clamp(ArrayGapAlong, 0.0, 100000.0); ArrayGapAcross = Clamp(ArrayGapAcross, 0.0, 100000.0);
            SlopeDegrees = Clamp(SlopeDegrees, -89.9, 89.9); ObstacleClearance = Clamp(ObstacleClearance, 0.0, 100000.0);
            Rows = Math.Max(1, Math.Min(8, Rows)); StringSize = Math.Max(1, Math.Min(500, StringSize));
            if (String.IsNullOrWhiteSpace(Orientation)) Orientation = "自动比较";
            if (String.IsNullOrWhiteSpace(OutputLayer)) OutputLayer = "组件";
        }
        private static double Clamp(double value, double min, double max) { return Double.IsNaN(value) || Double.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value)); }
    }
}
