using System;

namespace StructuralToolbox.PvComponent.Settings
{
    [Serializable]
    public sealed class PvComponentSettings
    {
        public PvComponentSettings()
        {
            Name = "光伏组件";
            Length = 2382.0;
            Width = 1134.0;
            Thickness = 30.0;
            OutputLayer = "组件";
        }

        public string Name { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Thickness { get; set; }
        public string OutputLayer { get; set; }

        public PvComponentSettings Clone()
        {
            return new PvComponentSettings
            {
                Name = Name,
                Length = Length,
                Width = Width,
                Thickness = Thickness,
                OutputLayer = OutputLayer
            };
        }

        public void Normalize()
        {
            Name = String.IsNullOrWhiteSpace(Name) ? "光伏组件" : Name.Trim();
            Length = Positive(Length, 2382.0);
            Width = Positive(Width, 1134.0);
            Thickness = Positive(Thickness, 30.0);
            OutputLayer = String.IsNullOrWhiteSpace(OutputLayer)
                ? "组件" : OutputLayer.Trim();
        }

        private static double Positive(double value, double fallback)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value) &&
                value > 0.0 ? value : fallback;
        }
    }
}
