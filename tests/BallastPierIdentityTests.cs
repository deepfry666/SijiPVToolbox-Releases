using System;
using StructuralToolbox.Ballast.Core;

internal static class BallastPierIdentityTests
{
    private static int _passed;

    public static int Main()
    {
        Run(ReadsCircleByLayer);
        Run(ReadsRectangleAci);
        Run(ReadsDecimalTrueColor);
        Run(RejectsUnrelatedAndMalformedBlocks);
        Console.WriteLine("BALLAST_PIER_IDENTITY_TEST=PASS; CASES=" + _passed);
        return 0;
    }

    private static void ReadsCircleByLayer()
    {
        BallastStatisticItem item = Parse(
            "PZ_圆_D500_L3000_H300_CBL_YABCDEF12");
        Require(item.Shape == BallastShape.Circle &&
            Near(item.Diameter, 500.0) && Near(item.Height, 300.0) &&
            item.ColorLabel == "默认" && item.Quantity == 1,
            "circle identity was not restored");
    }

    private static void ReadsRectangleAci()
    {
        BallastStatisticItem item = Parse(
            "PZ_方_300x500_L3000_H250_CACI3_Y01234567");
        Require(item.Shape == BallastShape.Rect &&
            Near(item.RectLength, 500.0) && Near(item.RectWidth, 300.0) &&
            Near(item.Height, 250.0) && item.ColorLabel == "ACI 3",
            "rectangle identity was not normalized");
    }

    private static void ReadsDecimalTrueColor()
    {
        BallastStatisticItem item = Parse(
            "PZ_圆_D500P5_L2999P25_H300P5_CRGB12_34_56_Y89ABCDEF");
        Require(Near(item.Diameter, 500.5) && Near(item.Height, 300.5) &&
            item.ColorLabel == "RGB 12,34,56",
            "decimal or true-color identity was not restored");
    }

    private static void RejectsUnrelatedAndMalformedBlocks()
    {
        BallastStatisticItem item;
        Require(!BallastPierIdentity.TryParseToolBlockName(
                "PV_COMPONENT", out item),
            "unrelated block was accepted");
        Require(!BallastPierIdentity.TryParseToolBlockName(
                "PZ_圆_D500_L3000_H0_CBL_YABC", out item),
            "zero-height block was accepted");
        Require(!BallastPierIdentity.TryParseToolBlockName(
                "PZ_方_500x300_L3000_H250_CRGB999_0_0_YABC", out item),
            "invalid color was accepted");
    }

    private static BallastStatisticItem Parse(string name)
    {
        BallastStatisticItem item;
        if (!BallastPierIdentity.TryParseToolBlockName(name, out item))
            throw new InvalidOperationException("failed to parse " + name);
        return item;
    }

    private static bool Near(double actual, double expected)
    {
        return Math.Abs(actual - expected) <= 0.0001;
    }

    private static void Run(Action test)
    {
        test();
        _passed++;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
