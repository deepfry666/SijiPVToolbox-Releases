using System;
using System.Collections.Generic;
using StructuralToolbox.Core;

internal static class GeometryFingerprintTests
{
    private static int _passed;

    private static int Main()
    {
        try
        {
            PanelSelectionOrderDoesNotMatter();
            PanelAxisSignDoesNotMatter();
            RailSelectionAndDirectionDoNotMatter();
            GeometryChangesFingerprint();
            PanelGoldenVectorMatchesGfp1();
            RailGoldenVectorMatchesGfp1();
            PositiveAndNegativeZeroMatch();
            LinearPerturbationsRespectQuantum();
            AxisPerturbationsRespectQuantum();
            HalfQuantumRoundsAwayFromZero();
            Console.WriteLine("PASS {0} geometry fingerprint tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL geometry fingerprint tests: {0}",
                exception.Message);
            return 1;
        }
    }

    private static void PanelSelectionOrderDoesNotMatter()
    {
        PanelRect first = Panel(0.0, 0.0, new Vec2(0.0, 1.0), "A");
        PanelRect second = Panel(1200.0, 0.0, new Vec2(0.0, 1.0), "B");
        Equal(GeometryFingerprint.CreatePanels(new List<PanelRect> { first, second }),
            GeometryFingerprint.CreatePanels(new List<PanelRect> { second, first }),
            "panel order");
        _passed++;
    }

    private static void PanelAxisSignDoesNotMatter()
    {
        PanelRect positive = Panel(0.0, 0.0, new Vec2(0.0, 1.0), "A");
        PanelRect negative = Panel(0.0, 0.0, new Vec2(0.0, -1.0), "B");
        Equal(GeometryFingerprint.CreatePanels(new List<PanelRect> { positive }),
            GeometryFingerprint.CreatePanels(new List<PanelRect> { negative }),
            "panel axis sign");
        _passed++;
    }

    private static void RailSelectionAndDirectionDoNotMatter()
    {
        RailSegment first = new RailSegment(
            new Vec2(0.0, 0.0), new Vec2(100.0, 0.0), 0.0);
        RailSegment second = new RailSegment(
            new Vec2(0.0, 50.0), new Vec2(100.0, 50.0), 0.0);
        RailSegment reverseFirst = new RailSegment(
            new Vec2(100.0, 0.0), new Vec2(0.0, 0.0), 0.0);
        RailSegment reverseSecond = new RailSegment(
            new Vec2(100.0, 50.0), new Vec2(0.0, 50.0), 0.0);
        Equal(GeometryFingerprint.CreateSegments(
                new List<RailSegment> { first, second }),
            GeometryFingerprint.CreateSegments(
                new List<RailSegment> { reverseSecond, reverseFirst }),
            "rail order and direction");
        _passed++;
    }

    private static void GeometryChangesFingerprint()
    {
        string original = GeometryFingerprint.CreateSegments(
            new List<RailSegment> { new RailSegment(
                new Vec2(0.0, 0.0), new Vec2(100.0, 0.0), 0.0) });
        string changed = GeometryFingerprint.CreateSegments(
            new List<RailSegment> { new RailSegment(
                new Vec2(0.0, 0.0), new Vec2(101.0, 0.0), 0.0) });
        if (String.Equals(original, changed, StringComparison.Ordinal))
            throw new InvalidOperationException("changed rail geometry reused the hash");
        _passed++;
    }

    private static void PanelGoldenVectorMatchesGfp1()
    {
        string actual = GeometryFingerprint.CreatePanels(
            new List<PanelRect> {
                Panel(0.0, 0.0, new Vec2(0.0, 1.0), "GOLDEN") });
        Equal("DF5DB82C5A06DB7A0AA077E3F43FC6DC06B82EF71C536152E0938D799DE0BA61",
            actual, "GFP1 panel golden vector");
        _passed++;
    }

    private static void RailGoldenVectorMatchesGfp1()
    {
        List<RailSegment> segments = new List<RailSegment>();
        segments.Add(new RailSegment(
            new Vec2(0.0, 0.0), new Vec2(100.0, 0.0), 0.0));
        segments.Add(new RailSegment(
            new Vec2(0.0, 50.0), new Vec2(100.0, 50.0), 0.0));
        Equal("6752F99BF33B7FD5CB3104826B3355BCCEEA87F26C49B485BFBED12E2F07C2A0",
            GeometryFingerprint.CreateSegments(segments),
            "GFP1 rail golden vector");
        _passed++;
    }

    private static void PositiveAndNegativeZeroMatch()
    {
        RailSegment positive = new RailSegment(
            new Vec2(0.0, 0.0), new Vec2(100.0, 0.0), 0.0);
        RailSegment negative = new RailSegment(
            new Vec2(-0.0, -0.0), new Vec2(100.0, -0.0), -0.0);
        Equal(GeometryFingerprint.CreateSegments(
                new List<RailSegment> { positive }),
            GeometryFingerprint.CreateSegments(
                new List<RailSegment> { negative }),
            "positive and negative zero");
        PanelRect positivePanel = new PanelRect(
            new Vec2(0.0, 0.0), new Vec2(0.0, 1.0),
            new Vec2(1.0, 0.0), 2300.0, 1134.0, 0.0, "POSITIVE");
        PanelRect negativePanel = new PanelRect(
            new Vec2(-0.0, -0.0), new Vec2(-0.0, 1.0),
            new Vec2(1.0, -0.0), 2300.0, 1134.0, -0.0, "NEGATIVE");
        Equal(GeometryFingerprint.CreatePanels(
                new List<PanelRect> { positivePanel }),
            GeometryFingerprint.CreatePanels(
                new List<PanelRect> { negativePanel }),
            "panel positive and negative zero");
        _passed++;
    }

    private static void LinearPerturbationsRespectQuantum()
    {
        RailSegment baseline = new RailSegment(
            new Vec2(0.0, 0.0), new Vec2(100.0, 0.0), 0.0);
        RailSegment insideQuantum = new RailSegment(
            new Vec2(0.0004, 0.0), new Vec2(100.0004, 0.0), 0.0004);
        RailSegment outsideQuantum = new RailSegment(
            new Vec2(0.0006, 0.0), new Vec2(100.0006, 0.0), 0.0006);
        string baselineHash = GeometryFingerprint.CreateSegments(
            new List<RailSegment> { baseline });
        Equal(baselineHash, GeometryFingerprint.CreateSegments(
                new List<RailSegment> { insideQuantum }),
            "linear perturbation inside 0.001 quantum");
        NotEqual(baselineHash, GeometryFingerprint.CreateSegments(
                new List<RailSegment> { outsideQuantum }),
            "linear perturbation outside 0.001 quantum");
        _passed++;
    }

    private static void AxisPerturbationsRespectQuantum()
    {
        PanelRect baseline = Panel(0.0, 0.0,
            new Vec2(0.0, 1.0), "BASE");
        PanelRect insideQuantum = Panel(0.0, 0.0,
            new Vec2(0.0000000000004, 1.0), "INSIDE");
        PanelRect outsideQuantum = Panel(0.0, 0.0,
            new Vec2(0.0000000000006, 1.0), "OUTSIDE");
        string baselineHash = GeometryFingerprint.CreatePanels(
            new List<PanelRect> { baseline });
        Equal(baselineHash, GeometryFingerprint.CreatePanels(
                new List<PanelRect> { insideQuantum }),
            "axis perturbation inside 1e-12 quantum");
        NotEqual(baselineHash, GeometryFingerprint.CreatePanels(
                new List<PanelRect> { outsideQuantum }),
            "axis perturbation outside 1e-12 quantum");
        _passed++;
    }

    private static void HalfQuantumRoundsAwayFromZero()
    {
        RailSegment positiveHalf = new RailSegment(
            new Vec2(0.0005, 0.0), new Vec2(100.0, 0.0), 0.0);
        RailSegment positiveSameCell = new RailSegment(
            new Vec2(0.0006, 0.0), new Vec2(100.0, 0.0), 0.0);
        RailSegment negativeHalf = new RailSegment(
            new Vec2(-0.0005, 0.0), new Vec2(100.0, 0.0), 0.0);
        RailSegment negativeSameCell = new RailSegment(
            new Vec2(-0.0006, 0.0), new Vec2(100.0, 0.0), 0.0);
        Equal(GeometryFingerprint.CreateSegments(
                new List<RailSegment> { positiveSameCell }),
            GeometryFingerprint.CreateSegments(
                new List<RailSegment> { positiveHalf }),
            "positive linear midpoint rounds away from zero");
        Equal(GeometryFingerprint.CreateSegments(
                new List<RailSegment> { negativeSameCell }),
            GeometryFingerprint.CreateSegments(
                new List<RailSegment> { negativeHalf }),
            "negative linear midpoint rounds away from zero");

        PanelRect positiveAxisHalf = Panel(0.0, 0.0,
            new Vec2(0.0000000000005, 1.0), "AXIS-HALF");
        PanelRect positiveAxisSameCell = Panel(0.0, 0.0,
            new Vec2(0.0000000000006, 1.0), "AXIS-CELL");
        Equal(GeometryFingerprint.CreatePanels(
                new List<PanelRect> { positiveAxisSameCell }),
            GeometryFingerprint.CreatePanels(
                new List<PanelRect> { positiveAxisHalf }),
            "axis midpoint rounds away from zero");
        _passed++;
    }

    private static PanelRect Panel(double x, double y, Vec2 longAxis,
        string sourceId)
    {
        Vec2 shortAxis = new Vec2(longAxis.Y, -longAxis.X);
        return new PanelRect(new Vec2(x, y), longAxis, shortAxis,
            2300.0, 1134.0, 0.0, sourceId);
    }

    private static void Equal(string expected, string actual, string context)
    {
        if (!String.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException(context + " fingerprint mismatch");
    }

    private static void NotEqual(string first, string second, string context)
    {
        if (String.Equals(first, second, StringComparison.Ordinal))
            throw new InvalidOperationException(context + " reused the fingerprint");
    }
}
