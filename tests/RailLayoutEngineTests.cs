using System;
using System.Collections.Generic;
using StructuralToolbox.Core;

internal static class RailLayoutEngineTests
{
    private static int _passed;

    public static int Main()
    {
        try
        {
            MergesTwentyMillimeterGap();
            SplitsGapAboveThreshold();
            DistributesCustomRailCountAcrossTotalSpacing();
            SupportsRotatedPanels();
            KeepsDifferentDirectionsSeparate();
            ForcesRailsPerpendicularToLongAxis();
            IsIndependentOfSelectionOrder();
            UsesStrictContinuityThreshold();
            RejectsZeroSpacingForMultipleRails();
            KeepsDifferentSizesInSeparateGroups();
            KeepsDifferentRotationsSeparateAndOrderIndependent();
            PreservesVisibleClearanceAtInternalBreaks();
            TrimsBothSidesAcrossMultipleBreaks();
            MergesBeforeApplyingInternalBreakOverhang();
            SeparatesMeaningfullyDifferentRotations();
            MergesMinorRotationNoise();
            TracksDirectionIslandRowAndRailMetadata();
            TracksActualSupportsOverhangsAndBreaks();
            KeepsRemoteArraysInSeparateIslands();
            KeepsMetadataStableAcrossSelectionOrder();
            Console.WriteLine("PASS {0} rail layout tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static void MergesTwentyMillimeterGap()
    {
        List<PanelRect> panels = HorizontalPair(20.0);
        RailLayoutResult result = RailLayoutEngine.Create(panels, new RailLayoutOptions());
        Equal(2, result.Segments.Count, "20 mm gap should create two continuous rails");
        Near(2488.0, result.Segments[0].Length, 0.001, "merged rail length");
        Pass();
    }

    private static void SplitsGapAboveThreshold()
    {
        List<PanelRect> panels = HorizontalPair(20.1);
        RailLayoutResult result = RailLayoutEngine.Create(panels, new RailLayoutOptions());
        Equal(4, result.Segments.Count, "gap above threshold should split both rails");
        Near(1.0, GapBetween(result.Segments[0], result.Segments[1]), 0.000001,
            "split rails must retain a visible gap");
        Pass();
    }

    private static void DistributesCustomRailCountAcrossTotalSpacing()
    {
        RailLayoutOptions options = new RailLayoutOptions();
        options.RailCount = 3;
        options.TotalSpacing = 1400.0;
        List<PanelRect> panels = new List<PanelRect>();
        panels.Add(CreatePanel(new Vec2(0.0, 0.0), new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
        RailLayoutResult result = RailLayoutEngine.Create(panels, options);
        Equal(3, result.Segments.Count, "three rail layout");
        double[] y = new double[]
        {
            result.Segments[0].Start.Y,
            result.Segments[1].Start.Y,
            result.Segments[2].Start.Y
        };
        Array.Sort(y);
        Near(-700.0, y[0], 0.001, "first rail offset");
        Near(0.0, y[1], 0.001, "middle rail offset");
        Near(700.0, y[2], 0.001, "last rail offset");
        Pass();
    }

    private static void SupportsRotatedPanels()
    {
        double radians = 30.0 * Math.PI / 180.0;
        Vec2 longAxis = new Vec2(Math.Cos(radians), Math.Sin(radians));
        Vec2 shortAxis = new Vec2(-Math.Sin(radians), Math.Cos(radians));
        List<PanelRect> panels = new List<PanelRect>();
        panels.Add(CreatePanel(new Vec2(100.0, 200.0), longAxis, shortAxis));
        RailLayoutResult result = RailLayoutEngine.Create(panels, new RailLayoutOptions());
        Equal(2, result.Segments.Count, "rotated panel rail count");
        Vec2 direction = (result.Segments[0].End - result.Segments[0].Start).Normalized();
        Near(0.0, Vec2.Dot(direction, longAxis), 0.0001, "rail must be perpendicular to long axis");
        Pass();
    }

        private static void KeepsDifferentDirectionsSeparate()
    {
        List<PanelRect> panels = new List<PanelRect>();
        panels.Add(CreatePanel(new Vec2(0.0, 0.0), new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
        panels.Add(CreatePanel(new Vec2(0.0, 0.0), new Vec2(1.0, 0.0), new Vec2(0.0, 1.0)));
        RailLayoutResult result = RailLayoutEngine.Create(panels, new RailLayoutOptions());
        Equal(4, result.Segments.Count, "different directions should not merge");
        Equal(2, result.PanelGroupCount, "direction group count");
            Pass();
        }

        private static void ForcesRailsPerpendicularToLongAxis()
        {
            double skew = Math.PI / 180.0;
            PanelRect panel = new PanelRect(
                new Vec2(0.0, 0.0),
                new Vec2(0.0, 1.0),
                new Vec2(Math.Cos(skew), Math.Sin(skew)),
                2300.835318,
                1134.0,
                0.0,
                "skew");
            RailLayoutResult result = RailLayoutEngine.Create(
                new List<PanelRect> { panel }, new RailLayoutOptions());
            Vec2 direction = (result.Segments[0].End - result.Segments[0].Start).Normalized();
            Near(0.0, Vec2.Dot(direction, panel.LongAxis), 0.0000001,
                "rail must be exactly perpendicular to long axis");
            Pass();
        }

        private static void IsIndependentOfSelectionOrder()
        {
            List<PanelRect> forward = MisalignedTriplet();
            List<PanelRect> reverse = MisalignedTriplet();
            reverse.Reverse();
            RailLayoutResult forwardResult = RailLayoutEngine.Create(forward, new RailLayoutOptions());
            RailLayoutResult reverseResult = RailLayoutEngine.Create(reverse, new RailLayoutOptions());
            string forwardSignature = SegmentSignature(forwardResult.Segments);
            string reverseSignature = SegmentSignature(reverseResult.Segments);
            if (!String.Equals(forwardSignature, reverseSignature, StringComparison.Ordinal))
                throw new InvalidOperationException("selection order changed the rail geometry");
            Pass();
        }

        private static void UsesStrictContinuityThreshold()
        {
            RailLayoutResult result = RailLayoutEngine.Create(
                HorizontalPair(20.0001), new RailLayoutOptions());
            Equal(4, result.Segments.Count, "gap above 20 mm must split");
            Pass();
        }

        private static void RejectsZeroSpacingForMultipleRails()
        {
            RailLayoutOptions options = new RailLayoutOptions();
            options.RailCount = 2;
            options.TotalSpacing = 0.0;
            bool rejected = false;
            try
            {
                RailLayoutEngine.Create(HorizontalPair(20.0), options);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejected = true;
            }
            if (!rejected)
                throw new InvalidOperationException("zero spacing was accepted for multiple rails");
            Pass();
        }

        private static void KeepsDifferentSizesInSeparateGroups()
        {
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(new PanelRect(new Vec2(0.0, 0.0), new Vec2(0.0, 1.0),
                new Vec2(1.0, 0.0), 2300.835318, 1134.0, 0.0, "standard"));
            panels.Add(new PanelRect(new Vec2(1154.0, 0.0), new Vec2(0.0, 1.0),
                new Vec2(1.0, 0.0), 2400.0, 1134.0, 0.0, "different"));
            RailLayoutResult result = RailLayoutEngine.Create(panels, new RailLayoutOptions());
            Equal(2, result.PanelGroupCount, "different size group count");
            Equal(4, result.Segments.Count, "different sizes must not merge across groups");
            Pass();
        }

        private static void KeepsDifferentRotationsSeparateAndOrderIndependent()
        {
            double radians = 0.4 * Math.PI / 180.0;
            List<PanelRect> forward = new List<PanelRect>();
            forward.Add(CreatePanel(new Vec2(0.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            forward.Add(CreatePanel(new Vec2(1154.0, 0.0),
                new Vec2(-Math.Sin(radians), Math.Cos(radians)),
                new Vec2(Math.Cos(radians), Math.Sin(radians))));
            List<PanelRect> reverse = new List<PanelRect>(forward);
            reverse.Reverse();

            RailLayoutResult forwardResult = RailLayoutEngine.Create(forward, new RailLayoutOptions());
            RailLayoutResult reverseResult = RailLayoutEngine.Create(reverse, new RailLayoutOptions());
            Equal(2, forwardResult.PanelGroupCount, "different rotation group count");
            Equal(4, forwardResult.Segments.Count, "different rotations must not merge");
            if (!String.Equals(SegmentSignature(forwardResult.Segments),
                SegmentSignature(reverseResult.Segments), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("rotation grouping depends on selection order");
            }
            Pass();
        }

        private static void PreservesVisibleClearanceAtInternalBreaks()
        {
            double[] sourceGaps = new double[] { 20.1, 30.0, 199.9, 200.0, 201.0, 250.0 };
            double[] expectedRailGaps = new double[] { 1.0, 1.0, 1.0, 1.0, 1.0, 50.0 };
            for (int index = 0; index < sourceGaps.Length; index++)
            {
                RailLayoutOptions options = SingleRailOptions();
                RailLayoutResult result = RailLayoutEngine.Create(
                    HorizontalPair(sourceGaps[index]), options);
                Equal(2, result.Segments.Count,
                    "source gap above threshold must create two rail segments");
                Near(expectedRailGaps[index], GapBetween(result.Segments[0], result.Segments[1]),
                    0.000001, "visible rail gap for source gap " + sourceGaps[index]);

                double originalBreakMidpoint = 567.0 + (sourceGaps[index] / 2.0);
                Near(originalBreakMidpoint - (expectedRailGaps[index] / 2.0),
                    SegmentMaximumX(result.Segments[0]), 0.000001,
                    "left internal endpoint for source gap " + sourceGaps[index]);
                Near(originalBreakMidpoint + (expectedRailGaps[index] / 2.0),
                    SegmentMinimumX(result.Segments[1]), 0.000001,
                    "right internal endpoint for source gap " + sourceGaps[index]);

                double rightPanelEdge = 1134.0 + sourceGaps[index] + 567.0;
                Near(-667.0, MinimumX(result.Segments), 0.000001,
                    "outer start overhang for source gap " + sourceGaps[index]);
                Near(rightPanelEdge + 100.0, MaximumX(result.Segments), 0.000001,
                    "outer end overhang for source gap " + sourceGaps[index]);
            }
            Pass();
        }

        private static void MergesBeforeApplyingInternalBreakOverhang()
        {
            RailLayoutOptions options = SingleRailOptions();
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(CreatePanel(new Vec2(0.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            panels.Add(CreatePanel(new Vec2(1154.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            panels.Add(CreatePanel(new Vec2(2318.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));

            RailLayoutResult result = RailLayoutEngine.Create(panels, options);
            Equal(2, result.Segments.Count,
                "20 mm pair must merge before the following 30 mm break is extended");
            Near(1.0, GapBetween(result.Segments[0], result.Segments[1]), 0.000001,
                "merged run to third panel break clearance");
            Near(-667.0, MinimumX(result.Segments), 0.000001,
                "merged run outer start overhang");
            Near(2985.0, MaximumX(result.Segments), 0.000001,
                "third panel outer end overhang");
            Pass();
        }

        private static void TrimsBothSidesAcrossMultipleBreaks()
        {
            RailLayoutOptions options = SingleRailOptions();
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(CreatePanel(new Vec2(0.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            panels.Add(CreatePanel(new Vec2(1164.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            panels.Add(CreatePanel(new Vec2(2548.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));

            RailLayoutResult result = RailLayoutEngine.Create(panels, options);
            Equal(3, result.Segments.Count, "three separated panel runs");
            Near(1.0, GapBetween(result.Segments[0], result.Segments[1]), 0.000001,
                "first internal break clearance");
            Near(50.0, GapBetween(result.Segments[1], result.Segments[2]), 0.000001,
                "second internal break clearance");
            Near(-667.0, MinimumX(result.Segments), 0.000001,
                "chain outer start overhang");
            Near(3215.0, MaximumX(result.Segments), 0.000001,
                "chain outer end overhang");
            Pass();
        }

        private static void SeparatesMeaningfullyDifferentRotations()
        {
            double radians = 0.10 * Math.PI / 180.0;
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(CreatePanel(new Vec2(0.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            panels.Add(CreatePanel(new Vec2(1154.0, 0.0),
                new Vec2(-Math.Sin(radians), Math.Cos(radians)),
                new Vec2(Math.Cos(radians), Math.Sin(radians))));

            RailLayoutResult result = RailLayoutEngine.Create(panels, new RailLayoutOptions());
            Equal(2, result.PanelGroupCount, "different actual rotations must form separate groups");
            Equal(4, result.Segments.Count, "different actual rotations must not share rails");
            Pass();
        }

        private static void MergesMinorRotationNoise()
        {
            double radians = 0.02 * Math.PI / 180.0;
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(CreatePanel(new Vec2(0.0, 0.0),
                new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            panels.Add(CreatePanel(new Vec2(1154.0, 0.0),
                new Vec2(-Math.Sin(radians), Math.Cos(radians)),
                new Vec2(Math.Cos(radians), Math.Sin(radians))));

            RailLayoutResult result = RailLayoutEngine.Create(panels,
                new RailLayoutOptions());
            Equal(1, result.PanelGroupCount,
                "minor CAD angle noise should remain in one direction group");
            Equal(2, result.Segments.Count,
                "minor CAD angle noise should keep the continuous rail pair");
            Pass();
        }

        private static void TracksDirectionIslandRowAndRailMetadata()
        {
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(CreateNamedPanel(new Vec2(0.0, 0.0), "A"));
            panels.Add(CreateNamedPanel(new Vec2(1154.0, 0.0), "B"));
            RailLayoutResult result = RailLayoutEngine.Create(panels,
                new RailLayoutOptions());

            Equal(1, result.DirectionGroups.Count, "direction metadata count");
            Equal(1, result.ArrayIslands.Count, "island metadata count");
            Equal(1, result.ComponentRows.Count, "row metadata count");
            Equal(2, result.SegmentMetadata.Count, "segment metadata count");
            Equal(0, result.Breaks.Count, "continuous row break count");
            Equal(0, result.SegmentMetadata[0].RailIndex, "first rail index");
            Equal(1, result.SegmentMetadata[1].RailIndex, "second rail index");
            Equal(2, result.SegmentMetadata[0].SourcePanelIds.Count,
                "rail source panel count");
            Equal(2, result.SegmentMetadata[0].SupportIntervals.Count,
                "actual support interval count");
            Near(100.0, result.SegmentMetadata[0].StartOverhang, 0.001,
                "metadata start overhang");
            Near(100.0, result.SegmentMetadata[0].EndOverhang, 0.001,
                "metadata end overhang");
            Near(2288.0, result.SegmentMetadata[0].SupportSpan, 0.001,
                "metadata array support span");
            if (!String.Equals(result.SegmentMetadata[0].ComponentRowId,
                result.ComponentRows[0].Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("segment row ID is not traceable");
            }
            Pass();
        }

        private static void TracksActualSupportsOverhangsAndBreaks()
        {
            RailLayoutOptions options = SingleRailOptions();
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(CreateNamedPanel(new Vec2(0.0, 0.0), "LEFT"));
            panels.Add(CreateNamedPanel(new Vec2(1634.0, 0.0), "RIGHT"));
            RailLayoutResult result = RailLayoutEngine.Create(panels, options);

            Equal(2, result.SegmentMetadata.Count, "split segment metadata count");
            Equal(1, result.ArrayIslands.Count,
                "nearby split runs should remain one annotation island");
            Equal(1, result.ComponentRows.Count,
                "nearby split runs should remain one component row");
            Equal(1, result.Breaks.Count, "break metadata count");
            Near(500.0, result.Breaks[0].SourceGap, 0.001,
                "source component gap");
            Near(300.0, result.Breaks[0].ActualClearance, 0.001,
                "actual final rail clearance");
            Near(100.0, result.SegmentMetadata[0].EndOverhang, 0.001,
                "first internal end overhang");
            Near(100.0, result.SegmentMetadata[1].StartOverhang, 0.001,
                "second internal start overhang");
            Pass();
        }

        private static void KeepsRemoteArraysInSeparateIslands()
        {
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(CreateNamedPanel(new Vec2(0.0, 0.0), "NEAR"));
            panels.Add(CreateNamedPanel(new Vec2(4134.0, 0.0), "REMOTE"));
            RailLayoutResult result = RailLayoutEngine.Create(panels,
                new RailLayoutOptions());

            Equal(1, result.DirectionGroups.Count, "remote direction group count");
            Equal(2, result.ArrayIslands.Count, "remote array island count");
            Equal(2, result.ComponentRows.Count, "remote row metadata count");
            for (int index = 0; index < result.SegmentMetadata.Count; index++)
            {
                if (String.IsNullOrEmpty(result.SegmentMetadata[index].IslandId))
                    throw new InvalidOperationException("remote segment has no island ID");
            }
            Pass();
        }

        private static void KeepsMetadataStableAcrossSelectionOrder()
        {
            List<PanelRect> forward = new List<PanelRect>();
            forward.Add(CreateNamedPanel(new Vec2(0.0, 0.0), "A"));
            forward.Add(CreateNamedPanel(new Vec2(1154.0, 0.0), "B"));
            forward.Add(CreateNamedPanel(new Vec2(0.0, 3500.0), "C"));
            List<PanelRect> reverse = new List<PanelRect>(forward);
            reverse.Reverse();

            RailLayoutResult forwardResult = RailLayoutEngine.Create(forward,
                new RailLayoutOptions());
            RailLayoutResult reverseResult = RailLayoutEngine.Create(reverse,
                new RailLayoutOptions());
            if (!String.Equals(MetadataSignature(forwardResult),
                MetadataSignature(reverseResult), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "selection order changed stable layout metadata");
            }
            Pass();
        }

        private static List<PanelRect> HorizontalPair(double gap)
    {
        double centerDistance = 1134.0 + gap;
        List<PanelRect> panels = new List<PanelRect>();
        panels.Add(CreatePanel(new Vec2(0.0, 0.0), new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
        panels.Add(CreatePanel(new Vec2(centerDistance, 0.0), new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            return panels;
        }

        private static List<PanelRect> MisalignedTriplet()
        {
            List<PanelRect> panels = new List<PanelRect>();
            panels.Add(CreatePanel(new Vec2(0.0, 0.0), new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            panels.Add(CreatePanel(new Vec2(1154.0, 1.5), new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            panels.Add(CreatePanel(new Vec2(2308.0, 3.0), new Vec2(0.0, 1.0), new Vec2(1.0, 0.0)));
            return panels;
        }

        private static RailLayoutOptions SingleRailOptions()
        {
            RailLayoutOptions options = new RailLayoutOptions();
            options.RailCount = 1;
            return options;
        }

        private static double GapBetween(RailSegment first, RailSegment second)
        {
            double firstMinimum = Math.Min(first.Start.X, first.End.X);
            double firstMaximum = Math.Max(first.Start.X, first.End.X);
            double secondMinimum = Math.Min(second.Start.X, second.End.X);
            double secondMaximum = Math.Max(second.Start.X, second.End.X);
            if (secondMinimum < firstMinimum)
            {
                double temporaryMinimum = firstMinimum;
                double temporaryMaximum = firstMaximum;
                firstMinimum = secondMinimum;
                firstMaximum = secondMaximum;
                secondMinimum = temporaryMinimum;
                secondMaximum = temporaryMaximum;
            }
            return secondMinimum - firstMaximum;
        }

        private static double MinimumX(IList<RailSegment> segments)
        {
            double minimum = Double.PositiveInfinity;
            for (int index = 0; index < segments.Count; index++)
            {
                minimum = Math.Min(minimum,
                    Math.Min(segments[index].Start.X, segments[index].End.X));
            }
            return minimum;
        }

        private static double SegmentMinimumX(RailSegment segment)
        {
            return Math.Min(segment.Start.X, segment.End.X);
        }

        private static double SegmentMaximumX(RailSegment segment)
        {
            return Math.Max(segment.Start.X, segment.End.X);
        }

        private static double MaximumX(IList<RailSegment> segments)
        {
            double maximum = Double.NegativeInfinity;
            for (int index = 0; index < segments.Count; index++)
            {
                maximum = Math.Max(maximum,
                    Math.Max(segments[index].Start.X, segments[index].End.X));
            }
            return maximum;
        }

        private static string SegmentSignature(IList<RailSegment> segments)
        {
            List<string> values = new List<string>();
            for (int index = 0; index < segments.Count; index++)
            {
                RailSegment segment = segments[index];
                Vec2 start = segment.Start;
                Vec2 end = segment.End;
                if (start.X > end.X || (Math.Abs(start.X - end.X) < 0.000001 && start.Y > end.Y))
                {
                    Vec2 temporary = start;
                    start = end;
                    end = temporary;
                }
                values.Add(String.Format("{0:F6},{1:F6},{2:F6},{3:F6},{4:F6}",
                    start.X, start.Y, end.X, end.Y, segment.Elevation));
            }
            values.Sort(StringComparer.Ordinal);
            return String.Join("|", values.ToArray());
        }

    private static PanelRect CreatePanel(Vec2 center, Vec2 longAxis, Vec2 shortAxis)
    {
        return new PanelRect(center, longAxis, shortAxis, 2300.835318, 1134.0, 0.0, "test");
    }

    private static PanelRect CreateNamedPanel(Vec2 center, string sourceId)
    {
        return new PanelRect(center, new Vec2(0.0, 1.0), new Vec2(1.0, 0.0),
            2300.835318, 1134.0, 0.0, sourceId);
    }

    private static string MetadataSignature(RailLayoutResult result)
    {
        List<string> values = new List<string>();
        for (int index = 0; index < result.SegmentMetadata.Count; index++)
        {
            RailSegmentMetadata metadata = result.SegmentMetadata[index];
            values.Add(String.Format("{0}:{1}:{2}:{3}:{4}:{5:F3}:{6:F3}",
                metadata.Id, metadata.DirectionGroupId, metadata.IslandId,
                metadata.ComponentRowId, metadata.RailIndex,
                metadata.StartOverhang, metadata.EndOverhang));
        }
        values.Sort(StringComparer.Ordinal);
        return String.Join("|", values.ToArray());
    }

    private static void Equal(int expected, int actual, string label)
    {
        if (expected != actual)
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, got {2}", label, expected, actual));
    }

    private static void Near(double expected, double actual, double tolerance, string label)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, got {2}", label, expected, actual));
    }

    private static void Pass()
    {
        _passed++;
    }
}
