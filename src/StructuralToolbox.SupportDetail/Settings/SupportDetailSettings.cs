using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;

namespace StructuralToolbox.SupportDetail.Settings
{
    public enum SupportDetailPierShape
    {
        Circular,
        Rectangular
    }

    public enum SupportDetailPositionMode
    {
        Automatic,
        Manual
    }

    /// <summary>
    /// Parameters for one independent concrete-roof support detail drawing.
    /// All distances are millimetres and the angle is in degrees.
    /// </summary>
    [Serializable]
    public sealed class SupportDetailSettings
    {
        public int ComponentCount { get; set; }
        public double ComponentLength { get; set; }
        public double ComponentWidth { get; set; }
        public double ComponentGap { get; set; }
        public double SlopeDegrees { get; set; }

        public SupportDetailPositionMode RailPositionMode { get; set; }
        public double RailCenterSpacing { get; set; }
        public string RailPositions { get; set; }

        public SupportDetailPositionMode PierPositionMode { get; set; }
        public bool PierCountFollowsComponents { get; set; }
        public int PierCount { get; set; }
        public double PierSpacing { get; set; }
        public string PierStations { get; set; }

        public double ColumnHeight { get; set; }
        public double TriangleHeight { get; set; }
        public double TriangleBase { get; set; }
        public double BeamOverhang { get; set; }
        public double BeamWidth { get; set; }
        public double PanelThickness { get; set; }

        public SupportDetailPierShape PierShape { get; set; }
        public double PierDiameter { get; set; }
        public double PierLength { get; set; }
        public double PierWidth { get; set; }
        public double PierHeight { get; set; }

        public string OutputLayer { get; set; }
        public bool IncludeNodeDetails { get; set; }
        public bool IncludeMaterialTable { get; set; }
        public bool IncludeBackPull { get; set; }

        public SupportDetailSettings()
        {
            ComponentCount = 1;
            ComponentLength = 2382.0;
            ComponentWidth = 1134.0;
            ComponentGap = 20.0;
            SlopeDegrees = 10.0;

            RailPositionMode = SupportDetailPositionMode.Automatic;
            RailCenterSpacing = 1400.0;
            RailPositions = String.Empty;

            PierPositionMode = SupportDetailPositionMode.Automatic;
            PierCountFollowsComponents = true;
            PierCount = 2;
            PierSpacing = 1400.0;
            PierStations = String.Empty;

            ColumnHeight = 250.0;
            TriangleHeight = 90.0;
            TriangleBase = 180.0;
            BeamOverhang = 200.0;
            BeamWidth = 52.0;
            PanelThickness = 32.0;

            PierShape = SupportDetailPierShape.Circular;
            PierDiameter = 500.0;
            PierLength = 500.0;
            PierWidth = 500.0;
            PierHeight = 300.0;

            OutputLayer = "支架大样";
            IncludeNodeDetails = true;
            IncludeMaterialTable = true;
            IncludeBackPull = false;
        }

        public SupportDetailSettings Clone()
        {
            SupportDetailSettings clone = new SupportDetailSettings();
            clone.ComponentCount = ComponentCount;
            clone.ComponentLength = ComponentLength;
            clone.ComponentWidth = ComponentWidth;
            clone.ComponentGap = ComponentGap;
            clone.SlopeDegrees = SlopeDegrees;
            clone.RailPositionMode = RailPositionMode;
            clone.RailCenterSpacing = RailCenterSpacing;
            clone.RailPositions = RailPositions;
            clone.PierPositionMode = PierPositionMode;
            clone.PierCountFollowsComponents = PierCountFollowsComponents;
            clone.PierCount = PierCount;
            clone.PierSpacing = PierSpacing;
            clone.PierStations = PierStations;
            clone.ColumnHeight = ColumnHeight;
            clone.TriangleHeight = TriangleHeight;
            clone.TriangleBase = TriangleBase;
            clone.BeamOverhang = BeamOverhang;
            clone.BeamWidth = BeamWidth;
            clone.PanelThickness = PanelThickness;
            clone.PierShape = PierShape;
            clone.PierDiameter = PierDiameter;
            clone.PierLength = PierLength;
            clone.PierWidth = PierWidth;
            clone.PierHeight = PierHeight;
            clone.OutputLayer = OutputLayer;
            clone.IncludeNodeDetails = IncludeNodeDetails;
            clone.IncludeMaterialTable = IncludeMaterialTable;
            clone.IncludeBackPull = IncludeBackPull;
            return clone;
        }

        public void Normalize()
        {
            ComponentCount = Math.Max(1, Math.Min(200, ComponentCount));
            ComponentLength = Positive(ComponentLength, 2382.0);
            ComponentWidth = Positive(ComponentWidth, 1134.0);
            ComponentGap = NonNegative(ComponentGap, 20.0);
            SlopeDegrees = Clamp(SlopeDegrees, -89.0, 89.0);

            if (RailPositionMode != SupportDetailPositionMode.Manual)
                RailPositionMode = SupportDetailPositionMode.Automatic;
            RailCenterSpacing = Positive(RailCenterSpacing, 1400.0);
            RailPositions = (RailPositions ?? String.Empty).Trim();

            if (PierPositionMode != SupportDetailPositionMode.Manual)
                PierPositionMode = SupportDetailPositionMode.Automatic;
            PierCount = PierCountFollowsComponents
                ? Math.Min(201, ComponentCount + 1)
                : Math.Max(1, Math.Min(201, PierCount));
            PierSpacing = Positive(PierSpacing, 1400.0);
            PierStations = (PierStations ?? String.Empty).Trim();

            ColumnHeight = NonNegative(ColumnHeight, 250.0);
            TriangleHeight = Positive(TriangleHeight, 90.0);
            TriangleBase = Positive(TriangleBase, 180.0);
            BeamOverhang = NonNegative(BeamOverhang, 200.0);
            BeamWidth = Positive(BeamWidth, 52.0);
            PanelThickness = Positive(PanelThickness, 32.0);

            if (PierShape != SupportDetailPierShape.Rectangular)
                PierShape = SupportDetailPierShape.Circular;
            PierDiameter = Positive(PierDiameter, 500.0);
            PierLength = Positive(PierLength, 500.0);
            PierWidth = Positive(PierWidth, 500.0);
            PierHeight = Positive(PierHeight, 300.0);
            OutputLayer = String.IsNullOrWhiteSpace(OutputLayer)
                ? "支架大样" : OutputLayer.Trim();
        }

        public IList<double> ParseRailPositions()
        {
            return ParseList(RailPositions);
        }

        public IList<double> ParsePierStations()
        {
            return ParseList(PierStations);
        }

        public static IList<double> ParseList(string text)
        {
            List<double> values = new List<double>();
            if (String.IsNullOrWhiteSpace(text))
                return values;
            string[] tokens = text.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < tokens.Length; index++)
            {
                double value;
                if (Double.TryParse(tokens[index], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) ||
                    Double.TryParse(tokens[index], NumberStyles.Float,
                        CultureInfo.CurrentCulture, out value))
                {
                    values.Add(value);
                }
            }
            return values;
        }

        private static double Positive(double value, double fallback)
        {
            return value > 0.0001 ? value : fallback;
        }

        private static double NonNegative(double value, double fallback)
        {
            return value >= 0.0 ? value : fallback;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
