using System;
using System.Collections.Generic;
using StructuralToolbox.Layout.Core;

internal static class LayoutEngineTests
{
    private static int _passed;

    private static int Main()
    {
        try
        {
            FillsSimpleRoofWithoutExceedingCapacity();
            KeepsTwoRowArraysRectangular();
            UsesDirectionForRowsAndStringSizeForColumns();
            AvoidsConcaveRoofNotch();
            AppliesCircularObstacleClearance();
            RejectsConflictPlanWhenItOnlyAddsOneComponent();
            AcceptsConflictPlanWhenItAddsTwoComponents();
            RebalancesOneRowCapacityGlobally();
            RebalancesTwoRowCapacityGlobally();
            MaximumRowsAllowsSingleRowWinner();
            LeavesOnlyUnusableCapacityRemainderForTwoRows();
            KeepsUnlimitedCapacityBehavior();
            BuffersSingleLinePipeWithClearance();
            BuffersOpenPolylineTurnsAndEnds();
            SupportsMultiplePipeDirections();
            TreatsZeroWidthLineAsObstacleWithoutClearance();
            NormalizesDuplicatePolygonVertices();
            AcceptsNearClosedBoundaryWithoutClosedFlag();
            RejectsTrulyOpenBoundaryWithGap();
            SummarizesRecognizedAndIgnoredObstacles();
            ProducesDeterministicResults();
            RejectsSelfIntersectingBoundary();
            Console.WriteLine("PASS {0} layout engine tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL layout engine tests: {0}", exception.Message);
            return 1;
        }
    }

    private static void FillsSimpleRoofWithoutExceedingCapacity()
    {
        LayoutOptions options = BasicOptions();
        options.MaximumCapacityKwp = 6.3;
        LayoutResult result = LayoutEngine.Solve(Input(Rectangle(0, 0, 30, 10)), options);
        Equal(10, result.ComponentCount, "capacity component count");
        Near(6.3, result.CapacityKwp, "capacity kWp");
        _passed++;
    }

    private static void KeepsTwoRowArraysRectangular()
    {
        LayoutOptions options = BasicOptions();
        options.AllowedRows = new[] { 2 };
        options.MaximumCapacityKwp = 6.3;
        LayoutResult result = LayoutEngine.Solve(Input(Rectangle(0, 0, 30, 10)), options);
        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < result.Rectangles.Count; i++)
        {
            string id = result.Rectangles[i].ArrayId;
            counts[id] = counts.ContainsKey(id) ? counts[id] + 1 : 1;
        }
        foreach (KeyValuePair<string, int> item in counts)
            True(item.Value % 2 == 0, "two-row array is not a complete rectangle");
        Equal(10, result.ComponentCount, "two-row capacity count");
        _passed++;
    }

    private static void UsesDirectionForRowsAndStringSizeForColumns()
    {
        LayoutOptions options = BasicOptions();
        options.AllowedRows = new[] { 2 };
        options.StringSize = 4;
        LayoutResult result = LayoutEngine.Solve(
            Input(Rectangle(0, 0, 20, 20)), options);
        Dictionary<string, List<LayoutRectangle>> arrays =
            new Dictionary<string, List<LayoutRectangle>>();
        for (int i = 0; i < result.Rectangles.Count; i++)
        {
            LayoutRectangle item = result.Rectangles[i];
            if (!arrays.ContainsKey(item.ArrayId))
                arrays[item.ArrayId] = new List<LayoutRectangle>();
            arrays[item.ArrayId].Add(item);
        }
        foreach (KeyValuePair<string, List<LayoutRectangle>> array in arrays)
        {
            True(array.Value.Count <= 4,
                "array exceeds the configured string size");
            HashSet<double> rowsAlongDirection = new HashSet<double>();
            HashSet<double> columnsAcrossDirection = new HashSet<double>();
            for (int i = 0; i < array.Value.Count; i++)
            {
                rowsAlongDirection.Add(array.Value[i].Center.X);
                columnsAcrossDirection.Add(array.Value[i].Center.Y);
            }
            Equal(2, rowsAlongDirection.Count,
                "M rows are not aligned with D");
            True(columnsAcrossDirection.Count <= 2,
                "string column target is not 2x2");
        }
        _passed++;
    }

    private static void AvoidsConcaveRoofNotch()
    {
        LayoutPolygon roof = new LayoutPolygon(new[]
        {
            new LayoutPoint(0, 0), new LayoutPoint(8, 0), new LayoutPoint(8, 6),
            new LayoutPoint(5, 6), new LayoutPoint(5, 2), new LayoutPoint(3, 2),
            new LayoutPoint(3, 6), new LayoutPoint(0, 6)
        });
        LayoutResult result = LayoutEngine.Solve(Input(roof), BasicOptions());
        for (int i = 0; i < result.Rectangles.Count; i++)
        {
            LayoutPoint center = result.Rectangles[i].Center;
            True(!(center.X > 3 && center.X < 5 && center.Y > 2),
                "component crossed the concave roof notch at " +
                center.X + "," + center.Y);
        }
        _passed++;
    }

    private static void AppliesCircularObstacleClearance()
    {
        LayoutInput input = Input(Rectangle(0, 0, 4, 2));
        input.Circles.Add(new LayoutCircle(new LayoutPoint(4.5, 1), 0.1));
        LayoutOptions withoutClearance = BasicOptions();
        LayoutOptions withClearance = BasicOptions();
        withClearance.ObstacleClearance = 0.5;
        LayoutResult clear = LayoutEngine.Solve(input, withoutClearance);
        LayoutResult protectedResult = LayoutEngine.Solve(input, withClearance);
        Equal(4, clear.ComponentCount,
            "outside circle should not block without clearance");
        True(protectedResult.ComponentCount < clear.ComponentCount,
            "circle clearance did not exclude nearby components");
        _passed++;
    }

    private static void RejectsConflictPlanWhenItOnlyAddsOneComponent()
    {
        LayoutInput input = Input(Rectangle(0, 0, 2, 5));
        input.Circles.Add(new LayoutCircle(new LayoutPoint(1, 2.5), 0.1));
        LayoutOptions options = BasicOptions();
        options.StringSize = 5;
        options.ConflictPerArrayLimit = 1;

        LayoutResult result = LayoutEngine.Solve(input, options);

        Equal(4, result.ComponentCount,
            "one-component conflict gain must be rejected");
        Equal(0, result.ConflictCount,
            "rejected conflict plan left red components");
        _passed++;
    }

    private static void AcceptsConflictPlanWhenItAddsTwoComponents()
    {
        LayoutInput input = Input(Rectangle(0, 0, 2, 5));
        input.Circles.Add(new LayoutCircle(new LayoutPoint(1, 2.5), 0.1));
        LayoutOptions options = BasicOptions();
        options.StringSize = 5;
        options.ArrayGapAcross = 2;
        options.ConflictPerArrayLimit = 1;

        LayoutResult result = LayoutEngine.Solve(input, options);

        Equal(5, result.ComponentCount,
            "two-component conflict gain should be accepted");
        Equal(1, result.ConflictCount,
            "accepted conflict plan should mark only the true conflict");
        _passed++;
    }

    private static void ProducesDeterministicResults()
    {
        LayoutOptions options = BasicOptions();
        LayoutInput input = Input(Rectangle(0, 0, 20, 8));
        input.Circles.Add(new LayoutCircle(new LayoutPoint(8, 3), 0.4));
        string first = Signature(LayoutEngine.Solve(input, options));
        string second = Signature(LayoutEngine.Solve(input, options));
        Equal(first, second, "deterministic signature");
        _passed++;
    }

    private static void RebalancesOneRowCapacityGlobally()
    {
        LayoutOptions options = CapacityOptions(1, 4, 16);
        LayoutPolygon roof = new LayoutPolygon(new[]
        {
            new LayoutPoint(0, 0), new LayoutPoint(10, 0),
            new LayoutPoint(10, 4), new LayoutPoint(6, 4),
            new LayoutPoint(6, 5), new LayoutPoint(0, 5)
        });
        LayoutResult result = LayoutEngine.Solve(Input(roof), options);
        Equal(16, result.ComponentCount, "one-row global capacity count");
        AssertArraySizes(result, new[] { 4, 4, 4, 4 },
            "one-row global capacity arrays");
        _passed++;
    }

    private static void RebalancesTwoRowCapacityGlobally()
    {
        LayoutOptions options = CapacityOptions(2, 8, 32);
        LayoutPolygon roof = new LayoutPolygon(new[]
        {
            new LayoutPoint(0, 0), new LayoutPoint(16, 0),
            new LayoutPoint(16, 4), new LayoutPoint(12, 4),
            new LayoutPoint(12, 5), new LayoutPoint(0, 5)
        });
        LayoutResult result = LayoutEngine.Solve(Input(roof), options);
        Equal(32, result.ComponentCount, "two-row global capacity count");
        AssertArraySizes(result, new[] { 8, 8, 8, 8 },
            "two-row global capacity arrays");
        _passed++;
    }

    private static void MaximumRowsAllowsSingleRowWinner()
    {
        LayoutOptions options = BasicOptions();
        options.AllowedRows = new[] { 1, 2 };
        options.StringSize = 4;
        LayoutResult result = LayoutEngine.Solve(
            Input(Rectangle(0, 0, 2, 8)), options);
        Equal(1, result.Rows,
            "maximum row count 2 incorrectly forced a two-row solution");
        True(result.ComponentCount > 0,
            "maximum row comparison failed to keep the one-row solution");
        _passed++;
    }

    private static void LeavesOnlyUnusableCapacityRemainderForTwoRows()
    {
        LayoutOptions options = CapacityOptions(2, 8, 31);
        LayoutResult result = LayoutEngine.Solve(
            Input(Rectangle(0, 0, 20, 20)), options);
        Equal(30, result.ComponentCount,
            "two-row non-divisible capacity should leave one component slot");
        True(result.ComponentCount <= 31, "capacity hard limit was exceeded");
        for (int i = 0; i < result.Rectangles.Count; i++)
            True(CountArray(result, result.Rectangles[i].ArrayId) % 2 == 0,
                "capacity remainder created a non-rectangular array");
        _passed++;
    }

    private static void KeepsUnlimitedCapacityBehavior()
    {
        LayoutOptions options = CapacityOptions(1, 4, 16);
        LayoutPolygon roof = new LayoutPolygon(new[]
        {
            new LayoutPoint(0, 0), new LayoutPoint(10, 0),
            new LayoutPoint(10, 4), new LayoutPoint(6, 4),
            new LayoutPoint(6, 5), new LayoutPoint(0, 5)
        });
        options.MaximumCapacityKwp = null;
        LayoutResult first = LayoutEngine.Solve(Input(roof), options);
        LayoutResult second = LayoutEngine.Solve(Input(roof), options);
        True(first.ComponentCount > 16,
            "unlimited capacity was accidentally capped");
        Equal(Signature(first), Signature(second),
            "unlimited capacity deterministic signature");
        _passed++;
    }

    private static void BuffersSingleLinePipeWithClearance()
    {
        LayoutInput input = Input(Rectangle(0, 0, 4, 2));
        input.LinearObstacles.Add(new LayoutPath(new[]
        {
            new LayoutPoint(4.4, 0), new LayoutPoint(4.4, 2)
        }));
        LayoutOptions narrow = BasicOptions();
        LayoutOptions protectedOptions = BasicOptions();
        protectedOptions.ObstacleClearance = 0.5;
        LayoutResult withoutClearance = LayoutEngine.Solve(input, narrow);
        LayoutResult withClearance = LayoutEngine.Solve(input,
            protectedOptions);
        Equal(4, withoutClearance.ComponentCount,
            "distant pipe blocked components without clearance");
        True(withClearance.ComponentCount < withoutClearance.ComponentCount,
            "line obstacle O clearance was not applied");
        _passed++;
    }

    private static void BuffersOpenPolylineTurnsAndEnds()
    {
        LayoutInput input = Input(Rectangle(0, 0, 6, 3));
        input.LinearObstacles.Add(new LayoutPath(new[]
        {
            new LayoutPoint(1, 0.5), new LayoutPoint(3, 0.5),
            new LayoutPoint(3, 2.5)
        }));
        LayoutResult result = LayoutEngine.Solve(input, BasicOptions());
        True(result.ComponentCount < 9,
            "open polyline turn and round end did not block components");
        _passed++;
    }

    private static void SupportsMultiplePipeDirections()
    {
        LayoutInput input = Input(Rectangle(0, 0, 6, 3));
        input.LinearObstacles.Add(new LayoutPath(new[]
        {
            new LayoutPoint(0, 1.5), new LayoutPoint(6, 1.5)
        }));
        input.LinearObstacles.Add(new LayoutPath(new[]
        {
            new LayoutPoint(5, 0), new LayoutPoint(5, 3)
        }));
        LayoutResult result = LayoutEngine.Solve(input, BasicOptions());
        True(result.ComponentCount < 9,
            "multiple horizontal and vertical pipes were not combined");
        _passed++;
    }

    private static void TreatsZeroWidthLineAsObstacleWithoutClearance()
    {
        LayoutInput input = Input(Rectangle(0, 0, 4, 2));
        input.LinearObstacles.Add(new LayoutPath(new[]
        {
            new LayoutPoint(1, 0), new LayoutPoint(1, 2)
        }));
        LayoutResult result = LayoutEngine.Solve(input, BasicOptions());
        True(result.ComponentCount < 4,
            "zero-width line crossing components was not treated as obstacle");
        _passed++;
    }

    private static void NormalizesDuplicatePolygonVertices()
    {
        LayoutPolygon polygon = new LayoutPolygon(new[]
        {
            new LayoutPoint(0, 0), new LayoutPoint(4, 0),
            new LayoutPoint(4, 0), new LayoutPoint(4, 2),
            new LayoutPoint(0, 2), new LayoutPoint(0, 0)
        });
        Equal(4, polygon.Points.Count,
            "duplicate and repeated closure vertices were not normalized");
        LayoutResult result = LayoutEngine.Solve(Input(polygon),
            BasicOptions());
        True(result.ComponentCount > 0,
            "normalized valid roof was rejected as self-intersecting");
        _passed++;
    }

    private static void AcceptsNearClosedBoundaryWithoutClosedFlag()
    {
        LayoutPolygon polygon;
        string reason;
        bool accepted = LayoutBoundaryReader.TryCreate(new[]
        {
            new LayoutPoint(0, 0), new LayoutPoint(4, 0),
            new LayoutPoint(4, 2), new LayoutPoint(0, 2),
            new LayoutPoint(0.05, 0)
        }, false, 0.1, out polygon, out reason);
        True(accepted, "near-closed boundary was rejected: " + reason);
        Equal(4, polygon.Points.Count,
            "near-closed endpoint was not normalized");
        _passed++;
    }

    private static void RejectsTrulyOpenBoundaryWithGap()
    {
        LayoutPolygon polygon;
        string reason;
        bool accepted = LayoutBoundaryReader.TryCreate(new[]
        {
            new LayoutPoint(0, 0), new LayoutPoint(4, 0),
            new LayoutPoint(4, 2), new LayoutPoint(0, 2)
        }, false, 0.1, out polygon, out reason);
        True(!accepted && reason.IndexOf("未闭合",
            StringComparison.Ordinal) >= 0 && reason.IndexOf("mm",
                StringComparison.Ordinal) >= 0,
            "truly open boundary did not report its endpoint gap");
        _passed++;
    }

    private static void SummarizesRecognizedAndIgnoredObstacles()
    {
        ObstacleSelectionSummary summary = new ObstacleSelectionSummary();
        summary.AddPolygon();
        summary.AddCircle();
        summary.AddLine();
        summary.AddOpenPolyline();
        summary.AddIgnored("块参照未展开");
        summary.AddIgnored("块参照未展开");
        Equal(6, summary.SelectedCount, "obstacle selected count");
        Equal(4, summary.RecognizedCount, "obstacle recognized count");
        Equal(2, summary.IgnoredCount, "obstacle ignored count");
        True(summary.ToMessage().IndexOf("块参照未展开 2 个",
            StringComparison.Ordinal) >= 0,
            "stable ignored reason was not reported");
        _passed++;
    }

    private static void RejectsSelfIntersectingBoundary()
    {
        LayoutPolygon crossed = new LayoutPolygon(new[]
        {
            new LayoutPoint(0, 0), new LayoutPoint(5, 5),
            new LayoutPoint(0, 5), new LayoutPoint(5, 0)
        });
        bool threw = false;
        try { LayoutEngine.Solve(Input(crossed), BasicOptions()); }
        catch (ArgumentException) { threw = true; }
        True(threw, "self-intersecting boundary was accepted");
        _passed++;
    }

    private static LayoutOptions BasicOptions()
    {
        return new LayoutOptions
        {
            Length = 2.0, Width = 1.0, PowerWp = 630,
            ComponentGapAlong = 0, ComponentGapAcross = 0,
            ArrayGapAlong = 0, ArrayGapAcross = 0,
            Orientation = LayoutOrientation.Vertical,
            AllowedRows = new[] { 1 }, Direction = new LayoutVector(1, 0),
            ConflictPerArrayLimit = 0
        };
    }

    private static LayoutOptions CapacityOptions(int rows, int stringSize,
        int maximumComponents)
    {
        LayoutOptions options = BasicOptions();
        options.AllowedRows = new[] { rows };
        options.StringSize = stringSize;
        options.MaximumCapacityKwp = maximumComponents *
            options.PowerWp / 1000.0;
        return options;
    }

    private static int CountArray(LayoutResult result, string arrayId)
    {
        int count = 0;
        for (int i = 0; i < result.Rectangles.Count; i++)
            if (String.Equals(arrayId, result.Rectangles[i].ArrayId,
                StringComparison.Ordinal)) count++;
        return count;
    }

    private static void AssertArraySizes(LayoutResult result, int[] expected,
        string context)
    {
        SortedDictionary<string, int> arrays =
            new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < result.Rectangles.Count; i++)
        {
            string id = result.Rectangles[i].ArrayId;
            arrays[id] = arrays.ContainsKey(id) ? arrays[id] + 1 : 1;
        }
        Equal(expected.Length, arrays.Count, context + " count");
        int index = 0;
        foreach (KeyValuePair<string, int> item in arrays)
            Equal(expected[index++], item.Value, context + " size");
    }

    private static LayoutInput Input(LayoutPolygon boundary)
    {
        return new LayoutInput { Boundary = boundary };
    }

    private static LayoutPolygon Rectangle(double x, double y, double width, double height)
    {
        return new LayoutPolygon(new[] { new LayoutPoint(x, y), new LayoutPoint(x + width, y),
            new LayoutPoint(x + width, y + height), new LayoutPoint(x, y + height) });
    }

    private static string Signature(LayoutResult result)
    {
        List<string> values = new List<string>();
        for (int i = 0; i < result.Rectangles.Count; i++)
        {
            LayoutRectangle item = result.Rectangles[i];
            values.Add(String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.###},{1:0.###},{2},{3}", item.Center.X, item.Center.Y,
                item.ArrayId, item.Conflict));
        }
        return String.Join("|", values.ToArray());
    }

    private static void Equal(int expected, int actual, string context)
    { if (expected != actual) throw new InvalidOperationException(context + ": expected " + expected + ", actual " + actual); }
    private static void Equal(string expected, string actual, string context)
    { if (!String.Equals(expected, actual, StringComparison.Ordinal)) throw new InvalidOperationException(context + " changed"); }
    private static void Near(double expected, double actual, string context)
    { if (Math.Abs(expected - actual) > 1e-9) throw new InvalidOperationException(context + ": expected " + expected + ", actual " + actual); }
    private static void True(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
