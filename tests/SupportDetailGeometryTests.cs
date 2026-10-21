using System;
using StructuralToolbox.SupportDetail.Core;
using StructuralToolbox.SupportDetail.Settings;

internal static class SupportDetailGeometryTests
{
    private static int _passed;

    public static int Main()
    {
        try
        {
            DefaultSingleComponentMatchesDrawingRule();
            ComponentCountAndGapControlArrayLength();
            ArbitraryAngleKeepsStraightBeam();
            DefaultRailsAreCenteredAtFourteenHundred();
            ManualRailPositionsAreUsedPerComponent();
            AutomaticPierCountAndSpacingAreCentered();
            DefaultPierCountFollowsComponents();
            ManualPierPositionsControlColumnHeights();
            BeamOverhangIsMeasuredFromArrayEnds();
            InvalidManualListsAreRejected();
            Console.WriteLine("PASS {0} support detail geometry tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL support detail geometry tests: {0}",
                exception.Message);
            return 1;
        }
    }

    private static void DefaultSingleComponentMatchesDrawingRule()
    {
        SupportDetailBuildResult result = SupportDetailGeometryBuilder.Build(
            new SupportDetailSettings());
        Require(result.IsValid, "default geometry must be valid");
        Near(2382.0, result.Geometry.ArrayLength, 0.001,
            "default component length");
        Near(1800.0, result.Geometry.BeamLength, 0.001,
            "default beam length");
        Equal(2, result.Geometry.Piers.Count, "default pier count");
        Pass();
    }

    private static void ComponentCountAndGapControlArrayLength()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.ComponentCount = 4;
        settings.ComponentLength = 2300.0;
        settings.ComponentGap = 25.0;
        SupportDetailGeometry geometry = Build(settings);
        Near(9275.0, geometry.ArrayLength, 0.001,
            "four component array length");
        Equal(4, geometry.Panels.Count, "four component panel count");
        Near(25.0, geometry.Panels[1].StartStation -
            geometry.Panels[0].EndStation, 0.001, "component clear gap");
        Pass();
    }

    private static void ArbitraryAngleKeepsStraightBeam()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.SlopeDegrees = 12.345;
        settings.PierPositionMode = SupportDetailPositionMode.Manual;
        settings.PierStations = "350, 1475, 2430";
        settings.PierCount = 3;
        SupportDetailGeometry geometry = Build(settings);
        double expectedRise = geometry.BeamLength *
            Math.Sin(12.345 * Math.PI / 180.0);
        Near(expectedRise, geometry.BeamEnd.Y - geometry.BeamStart.Y,
            0.001, "arbitrary angle beam rise");
        for (int index = 0; index < geometry.Piers.Count; index++)
        {
            SupportDetailPierGeometry pier = geometry.Piers[index];
            double beamY = geometry.BeamStart.Y +
                (pier.Station - geometry.BeamStartStation) *
                geometry.BeamDirection.Y;
            Near(beamY - settings.BeamWidth / 2.0,
                pier.BeamElevation, 0.001,
                "pier reaches same straight beam");
        }
        Pass();
    }

    private static void DefaultRailsAreCenteredAtFourteenHundred()
    {
        SupportDetailGeometry geometry = Build(new SupportDetailSettings());
        Equal(2, geometry.Rails.Count, "default rail count");
        Near(491.0, geometry.Rails[0].Station -
            geometry.Panels[0].StartStation, 0.001, "front clamp offset");
        Near(1400.0, geometry.Rails[1].Station - geometry.Rails[0].Station,
            0.001, "default rail center spacing");
        Pass();
    }

    private static void ManualRailPositionsAreUsedPerComponent()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.ComponentCount = 2;
        settings.RailPositionMode = SupportDetailPositionMode.Manual;
        settings.RailPositions = "400; 1100; 1900";
        SupportDetailGeometry geometry = Build(settings);
        Equal(6, geometry.Rails.Count, "manual rails repeated per component");
        Near(1900.0, geometry.Rails[2].Station -
            geometry.Panels[0].StartStation, 0.001,
            "third manual rail position");
        Pass();
    }

    private static void AutomaticPierCountAndSpacingAreCentered()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.ComponentCount = 3;
        settings.PierCountFollowsComponents = false;
        settings.PierCount = 4;
        settings.PierSpacing = 1200.0;
        SupportDetailGeometry geometry = Build(settings);
        Equal(4, geometry.Piers.Count, "automatic pier count");
        for (int index = 1; index < geometry.Piers.Count; index++)
            Near(1200.0, geometry.Piers[index].Station -
                geometry.Piers[index - 1].Station, 0.001,
                "automatic pier spacing");
        double pierCenter = (geometry.Piers[0].Station +
            geometry.Piers[geometry.Piers.Count - 1].Station) / 2.0;
        Near(geometry.ArrayLength / 2.0,
            pierCenter, 0.001, "automatic piers centered below array");
        Pass();
    }

    private static void DefaultPierCountFollowsComponents()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.ComponentCount = 5;
        SupportDetailGeometry geometry = Build(settings);
        Equal(6, geometry.Piers.Count,
            "default pier count is component count plus one");
        Pass();
    }

    private static void ManualPierPositionsControlColumnHeights()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.SlopeDegrees = 15.0;
        settings.ColumnHeight = 300.0;
        settings.PierPositionMode = SupportDetailPositionMode.Manual;
        settings.PierStations = "200, 1200, 2200";
        settings.PierCount = 3;
        SupportDetailGeometry geometry = Build(settings);
        Near(300.0, geometry.Piers[0].ActualColumnHeight, 0.001,
            "first manual pier is height datum");
        Near(1000.0 * Math.Sin(15.0 * Math.PI / 180.0),
            geometry.Piers[1].ActualColumnHeight -
            geometry.Piers[0].ActualColumnHeight, 0.001,
            "second column follows slope");
        Pass();
    }

    private static void BeamOverhangIsMeasuredFromArrayEnds()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.BeamOverhang = 275.0;
        SupportDetailGeometry geometry = Build(settings);
        Near(275.0, geometry.Piers[0].Station -
            geometry.BeamStartStation, 0.001,
            "front beam overhang from triangle center");
        Near(275.0, geometry.BeamEndStation -
            geometry.Piers[geometry.Piers.Count - 1].Station, 0.001,
            "back beam overhang from triangle center");
        Pass();
    }

    private static void InvalidManualListsAreRejected()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.PierPositionMode = SupportDetailPositionMode.Manual;
        settings.PierStations = "abc";
        SupportDetailBuildResult result = SupportDetailGeometryBuilder.Build(settings);
        Require(!result.IsValid, "invalid manual pier list must fail");
        Require(result.Errors.Count > 0, "invalid manual pier list error");
        Pass();
    }

    private static SupportDetailGeometry Build(SupportDetailSettings settings)
    {
        SupportDetailBuildResult result = SupportDetailGeometryBuilder.Build(settings);
        if (!result.IsValid)
            throw new InvalidOperationException(String.Join("; ", result.Errors));
        return result.Geometry;
    }

    private static void Equal(int expected, int actual, string context)
    {
        if (expected != actual)
            throw new InvalidOperationException(context + ": expected " +
                expected + ", actual " + actual);
    }

    private static void Near(double expected, double actual, double tolerance,
        string context)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(context + ": expected " +
                expected.ToString("0.###") + ", actual " +
                actual.ToString("0.###"));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Pass()
    {
        _passed++;
    }
}
