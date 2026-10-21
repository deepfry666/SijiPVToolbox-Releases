using System;
using System.Collections.Generic;
using StructuralToolbox.Ballast.Core;

internal static class BallastLayoutEngineTests
{
    private const double LongLength = 2300.0;
    private const double ShortLength = 1134.0;
    private static int _passed;

    public static int Main()
    {
        try
        {
            SinglePanelAutoCreatesTwoBallasts();
            GapAtLimitStaysConnected();
            GapAboveLimitSplitsArrays();
            RegularTwoByThreeGridIsRecognized();
            MissingPanelRejectsWholeConnectedArray();
            MisalignedPanelIsReportedInsteadOfSilentlySplit();
            RowRuleControlsBothFrameAndBallastSpacing();
            SeparateArraysUseIndependentCenters();
            FrameLayoutKeepsInteriorSpacingAndShrinksEnds();
            BallastOutlinesStayInsideArray();
            LongEdgeCheckIncludesBallastOutline();
            ManualCountOverridesAutomaticCount();
            CustomFrontPlacementUsesArrayDirection();
            CustomPlacementRejectsContradictoryValues();
            FrontBackClearanceIsNotLimitedBySideMaximum();
            RowRuleOverridesShapeAndCount();
            Console.WriteLine("PASS {0} ballast layout tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static void SinglePanelAutoCreatesTwoBallasts()
    {
        BallastLayoutResult result = BallastLayoutEngine.Create(
            new List<BallastPanel> { Panel(0.0, 0.0, "P1") },
            DefaultOptions());
        Equal(1, result.Plans.Count, "single panel plan count");
        Equal(1, result.FrameCount, "single panel frame count");
        Equal(2, result.BallastCount, "single panel ballast count");
        Pass();
    }

    private static void GapAtLimitStaysConnected()
    {
        BallastLayoutOptions options = DefaultOptions();
        BallastArrayDetectionResult acrossShort = BallastLayoutEngine.DetectArrays(
            new List<BallastPanel>
            {
                Panel(0.0, 0.0, "P1"),
                Panel(0.0, ShortLength + 20.0, "P2")
            }, options);
        Equal(1, acrossShort.Arrays.Count, "20 mm short gap array count");
        Equal(2, acrossShort.Arrays[0].ColumnsAlongShort,
            "20 mm short gap column count");

        BallastArrayDetectionResult alongLong = BallastLayoutEngine.DetectArrays(
            new List<BallastPanel>
            {
                Panel(0.0, 0.0, "P1"),
                Panel(LongLength + 20.0, 0.0, "P2")
            }, options);
        Equal(1, alongLong.Arrays.Count, "20 mm long gap array count");
        Equal(2, alongLong.Arrays[0].RowsAlongLong,
            "20 mm long gap row count");
        Pass();
    }

    private static void GapAboveLimitSplitsArrays()
    {
        BallastArrayDetectionResult result = BallastLayoutEngine.DetectArrays(
            new List<BallastPanel>
            {
                Panel(0.0, 0.0, "P1"),
                Panel(0.0, ShortLength + 20.1, "P2")
            }, DefaultOptions());
        Equal(2, result.Arrays.Count, "20.1 mm gap must split");
        Pass();
    }

    private static void RegularTwoByThreeGridIsRecognized()
    {
        BallastArrayDetectionResult result = BallastLayoutEngine.DetectArrays(
            Grid(2, 3), DefaultOptions());
        Equal(1, result.Arrays.Count, "2x3 array count");
        Equal(2, result.Arrays[0].RowsAlongLong, "2x3 row count");
        Equal(3, result.Arrays[0].ColumnsAlongShort, "2x3 column count");
        Equal(0, result.InvalidArrays.Count, "2x3 invalid count");
        Pass();
    }

    private static void MissingPanelRejectsWholeConnectedArray()
    {
        List<BallastPanel> panels = Grid(2, 2);
        panels.RemoveAt(panels.Count - 1);
        BallastArrayDetectionResult result = BallastLayoutEngine.DetectArrays(
            panels, DefaultOptions());
        Equal(0, result.Arrays.Count, "missing panel valid array count");
        Equal(1, result.InvalidArrays.Count, "missing panel invalid array count");
        Pass();
    }

    private static void MisalignedPanelIsReportedInsteadOfSilentlySplit()
    {
        BallastArrayDetectionResult result = BallastLayoutEngine.DetectArrays(
            new List<BallastPanel>
            {
                Panel(0.0, 0.0, "P1"),
                Panel(1.0, ShortLength + 20.0, "P2")
            }, DefaultOptions());
        Equal(0, result.Arrays.Count, "misaligned valid array count");
        Equal(1, result.InvalidArrays.Count, "misaligned invalid array count");
        Pass();
    }

    private static void RowRuleControlsBothFrameAndBallastSpacing()
    {
        BallastLayoutOptions options = DefaultOptions();
        options.RowRules = new List<BallastRowRule>
        {
            new BallastRowRule
            {
                RowsAlongLong = 2,
                CountMode = BallastCountMode.Manual,
                ManualCount = 5,
                BallastCenterSpacing = 900.0,
                FrameCenterSpacing = 1700.0
            }
        };
        BallastLayoutResult result = BallastLayoutEngine.Create(Grid(2, 3), options);
        Equal(1, result.Plans.Count, "row rule plan count");
        Equal(5, result.Plans[0].Frames[0].Ballasts.Count,
            "row rule ballast count");
        Near(900.0, Distance(result.Plans[0].Frames[0].Ballasts[0].Center,
            result.Plans[0].Frames[0].Ballasts[1].Center), 0.001,
            "row rule ballast spacing");
        if (result.Plans[0].Frames.Count > 1)
            Near(1471.0, Distance(result.Plans[0].Frames[0].Center,
                result.Plans[0].Frames[1].Center), 0.001,
                "row rule frame spacing");
        Pass();
    }

    private static void SeparateArraysUseIndependentCenters()
    {
        List<BallastPanel> panels = new List<BallastPanel>
        {
            Panel(0.0, 0.0, "A1"),
            Panel(0.0, ShortLength + 20.0, "A2"),
            Panel(0.0, 5000.0, "B1"),
            Panel(0.0, 5000.0 + ShortLength + 20.0, "B2")
        };
        BallastArrayDetectionResult result = BallastLayoutEngine.DetectArrays(
            panels, DefaultOptions());
        Equal(2, result.Arrays.Count, "independent array count");
        Near((ShortLength + 20.0) / 2.0, result.Arrays[0].Center.X,
            0.001, "first array center");
        Near(5000.0 + (ShortLength + 20.0) / 2.0,
            result.Arrays[1].Center.X, 0.001, "second array center");
        Pass();
    }

    private static void FrameLayoutKeepsInteriorSpacingAndShrinksEnds()
    {
        BallastLayoutResult result = BallastLayoutEngine.Create(
            Grid(1, 5), DefaultOptions());
        Equal(1, result.Plans.Count, "five column plan count");
        Equal(4, result.Plans[0].Frames.Count, "five column frame count");
        List<BallastFramePlan> frames = result.Plans[0].Frames;
        Near(1725.0, Distance(frames[0].Center, frames[1].Center),
            0.001, "first reduced gap");
        Near(1800.0, Distance(frames[1].Center, frames[2].Center),
            0.001, "interior standard gap");
        Near(1725.0, Distance(frames[2].Center, frames[3].Center),
            0.001, "last reduced gap");
        Pass();
    }

    private static void BallastOutlinesStayInsideArray()
    {
        BallastLayoutResult result = BallastLayoutEngine.Create(
            Grid(1, 5), DefaultOptions());
        BallastArrayPlan plan = result.Plans[0];
        double halfSpan = plan.Array.ShortSpan / 2.0;
        for (int index = 0; index < plan.Frames.Count; index++)
        {
            double offset = BallastVec2.Dot(
                plan.Frames[index].Center - plan.Array.Center,
                plan.Array.ShortAxis);
            if (Math.Abs(offset) + 250.0 > halfSpan + 0.001)
                throw new InvalidOperationException("ballast outline exceeds array");
        }
        Pass();
    }

    private static void LongEdgeCheckIncludesBallastOutline()
    {
        BallastLayoutOptions options = DefaultOptions();
        options.CountMode = BallastCountMode.Manual;
        options.ManualCount = 2;
        options.BallastCenterSpacing = 2050.0;
        BallastLayoutResult result = BallastLayoutEngine.Create(
            new List<BallastPanel> { Panel(0.0, 0.0, "P1") }, options);
        Equal(0, result.Plans.Count, "long outline invalid plan count");
        Equal(1, result.InvalidArrays.Count, "long outline invalid count");
        Pass();
    }

    private static void ManualCountOverridesAutomaticCount()
    {
        BallastLayoutOptions options = DefaultOptions();
        options.CountMode = BallastCountMode.Manual;
        options.ManualCount = 3;
        options.BallastCenterSpacing = 800.0;
        BallastLayoutResult result = BallastLayoutEngine.Create(
            new List<BallastPanel> { Panel(0.0, 0.0, "P1") }, options);
        Equal(3, result.BallastCount, "manual ballast count");
        Pass();
    }

    private static void CustomFrontPlacementUsesArrayDirection()
    {
        BallastLayoutOptions options = DefaultOptions();
        options.PlacementMode = BallastPlacementMode.CustomFront;
        options.FrontCenterClearance = 300.0;
        options.BallastCenterSpacing = 1700.0;
        BallastLayoutResult result = BallastLayoutEngine.Create(
            new List<BallastPanel> { Panel(0.0, 0.0, "P1") }, options);
        Equal(1, result.Plans.Count, "custom placement plan count");
        BallastArrayPlan plan = result.Plans[0];
        double firstOffset = BallastVec2.Dot(
            plan.Frames[0].Ballasts[0].Center - plan.Array.Center,
            plan.Array.LongAxis);
        Near(-LongLength / 2.0 + 300.0, firstOffset, 0.001,
            "custom front center clearance");
        Pass();
    }

    private static void CustomPlacementRejectsContradictoryValues()
    {
        BallastLayoutOptions options = DefaultOptions();
        options.PlacementMode = BallastPlacementMode.CustomFront;
        options.FrontCenterClearance = 200.0;
        BallastLayoutResult result = BallastLayoutEngine.Create(
            new List<BallastPanel> { Panel(0.0, 0.0, "P1") }, options);
        Equal(0, result.Plans.Count, "custom outline contradiction plan count");
        Equal(1, result.InvalidArrays.Count,
            "custom outline contradiction invalid count");
        Pass();
    }

    private static void FrontBackClearanceIsNotLimitedBySideMaximum()
    {
        BallastLayoutOptions options = DefaultOptions();
        options.CountMode = BallastCountMode.Manual;
        options.ManualCount = 2;
        options.BallastCenterSpacing = 650.0;
        BallastLayoutResult result = BallastLayoutEngine.Create(
            new List<BallastPanel> { Panel(0.0, 0.0, "P1") }, options);
        Equal(1, result.Plans.Count,
            "front/back center clearance may exceed side maximum");
        Pass();
    }

    private static void RowRuleOverridesShapeAndCount()
    {
        BallastLayoutOptions options = DefaultOptions();
        options.MaxComponentGap = 20.0;
        List<BallastPanel> panels = new List<BallastPanel>
        {
            Panel(0.0, 0.0, "A1P1"),
            Panel(0.0, ShortLength + 10.0, "A1P2"),
            Panel(10000.0, 0.0, "A2P1"),
            Panel(10000.0, ShortLength + 10.0, "A2P2")
        };
        options.RowRules = new List<BallastRowRule>
        {
            new BallastRowRule
            {
                RowsAlongLong = 1, Shape = BallastShape.Circle, Diameter = 400.0,
                Height = 300.0, CrossLineLength = 3000.0,
                CountMode = BallastCountMode.Manual,
                ManualCount = 2, BallastCenterSpacing = 1400.0,
                FrameCenterSpacing = 1800.0
            },
            new BallastRowRule
            {
                RowsAlongLong = 1, Shape = BallastShape.Rect, RectLength = 300.0,
                RectWidth = 300.0, Height = 250.0, CrossLineLength = 3000.0,
                CountMode = BallastCountMode.Manual, ManualCount = 3,
                BallastCenterSpacing = 800.0, FrameCenterSpacing = 1800.0
            }
        };
        options.ArrayRules = new List<BallastArrayRule>
        {
            new BallastArrayRule
            {
                ArrayId = "A2", Shape = BallastShape.Rect,
                RectLength = 300.0, RectWidth = 300.0, Height = 250.0,
                CrossLineLength = 3000.0, CountMode = BallastCountMode.Manual,
                ManualCount = 3, BallastCenterSpacing = 800.0,
                FrameCenterSpacing = 1800.0
            }
        };
        BallastLayoutResult result = BallastLayoutEngine.Create(panels, options);
        Equal(2, result.Plans.Count, "array override plan count");
        Equal(2, result.Plans[0].Frames[0].Ballasts.Count,
            "first array override count");
        Equal(3, result.Plans[1].Frames[0].Ballasts.Count,
            "second array override count");
        Equal(BallastShape.Circle, result.Plans[0].Options.Shape,
            "first array override shape");
        Equal(BallastShape.Rect, result.Plans[1].Options.Shape,
            "second array override shape");
        Pass();
    }

    private static BallastLayoutOptions DefaultOptions()
    {
        return new BallastLayoutOptions
        {
            Shape = BallastShape.Circle,
            Diameter = 500.0,
            CrossLineLength = 3000.0,
            FrameCenterSpacing = 1800.0,
            BallastCenterSpacing = 1400.0,
            MaxComponentGap = 20.0,
            MaxSideClearance = 800.0,
            AllowOverrun = 0.0,
            CountMode = BallastCountMode.AutoByRows,
            ManualCount = 2
        };
    }

    private static List<BallastPanel> Grid(int rows, int columns)
    {
        List<BallastPanel> panels = new List<BallastPanel>();
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
                panels.Add(Panel(row * (LongLength + 20.0),
                    column * (ShortLength + 20.0),
                    "P" + row.ToString() + "_" + column.ToString()));
        return panels;
    }

    private static BallastPanel Panel(double u, double v, string id)
    {
        return new BallastPanel(new BallastVec2(v, u),
            new BallastVec2(0.0, 1.0), new BallastVec2(1.0, 0.0),
            LongLength, ShortLength, 0.0, id);
    }

    private static double Distance(BallastVec2 first, BallastVec2 second)
    {
        return BallastVec2.Distance(first, second);
    }

    private static void Near(double expected, double actual,
        double tolerance, string label)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, got {2}", label, expected, actual));
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!Object.Equals(expected, actual))
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, got {2}", label, expected, actual));
    }

    private static void Pass()
    {
        _passed++;
    }
}
