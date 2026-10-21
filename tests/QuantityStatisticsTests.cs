using System;
using System.Collections.Generic;
using StructuralToolbox.Ballast.Core;
using StructuralToolbox.Core;

internal static class QuantityStatisticsTests
{
    public static int Main()
    {
        try
        {
            BallastStatisticSummary ballast = BallastStatistics.Summarize(
                new BallastStatisticItem[]
                {
                    new BallastStatisticItem
                    {
                        Shape = BallastShape.Circle, Diameter = 500.0,
                        Height = 300.0, ColorLabel = "默认", Quantity = 4
                    },
                    new BallastStatisticItem
                    {
                        Shape = BallastShape.Circle, Diameter = 500.0,
                        Height = 300.0, ColorLabel = "默认", Quantity = 6
                    },
                    new BallastStatisticItem
                    {
                        Shape = BallastShape.Rect, RectLength = 500.0,
                        RectWidth = 300.0, Height = 250.0,
                        ColorLabel = "ACI 3", Quantity = 2
                    }
                });
            Equal(2, ballast.Entries.Count, "ballast grouped rows");
            Equal(12, ballast.TotalQuantity, "ballast total quantity");
            Equal(10, ballast.Entries[0].Quantity, "single ballast expansion");
            Equal("D500xH300", ballast.Entries[0].Specification,
                "circle specification includes height");

            double railMillimeters = RailStatistics.TotalLengthMillimeters(
                new double[] { 1000.0, 2500.0, -1.0, Double.NaN });
            Near(3500.0, railMillimeters, "rail millimeters");
            Near(3.5, RailStatistics.ToMeters(railMillimeters), "rail meters");
            Console.WriteLine("PASS quantity statistics tests");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!Object.Equals(expected, actual))
            throw new InvalidOperationException(label + ": expected " +
                expected + ", got " + actual);
    }

    private static void Near(double expected, double actual, string label)
    {
        if (Math.Abs(expected - actual) > 0.0001)
            throw new InvalidOperationException(label + ": expected " +
                expected + ", got " + actual);
    }
}
