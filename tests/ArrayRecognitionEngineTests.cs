using System;
using System.Collections.Generic;
using StructuralToolbox.ArrayRecognition.Core;

internal static class ArrayRecognitionEngineTests
{
    private static int _passed;

    public static int Main()
    {
        Run(RecognizesCompleteGridInMultipleDirections);
        Run(SplitsArraysAtOutlineGapG);
        Run(ReportsMissingComponent);
        Run(ReportsMisalignedComponent);
        Run(ReportsDuplicatePosition);
        Run(ReportsMixedElevation);
        Run(KeepsOneByOneAsValidArray);
        Run(DeduplicatesRepeatedSourceObjects);
        Run(FingerprintChangesWithGeometry);
        Run(FingerprintIgnoresEquivalentAxisDirection);
        Run(AcceptsOpenFlagWithRepeatedStartPoint);
        Run(AcceptsOpenFlagWithinClosureTolerance);
        Run(RejectsOpenOutlineBeyondClosureTolerance);
        Run(NormalizesRepeatedClosureVertex);
        Run(SummarizesFixed272Array1854ComponentExample);
        Console.WriteLine("ARRAY_RECOGNITION_CORE_TEST=PASS; CASES=" + _passed);
        return 0;
    }

    private static void RecognizesCompleteGridInMultipleDirections()
    {
        List<ArrayComponent> items = Grid("A", 2, 3, 0, 0, 0);
        items.AddRange(Grid("B", 3, 2, 20000, 10000, Math.PI / 6));
        ArrayRecognitionResult result = Detect(items, 20);
        Require(result.Arrays.Count == 2, "multi-direction arrays were not separated");
        Require(result.RecognizedComponentCount == 12, "multi-direction component total");
        Require(result.InvalidComponentCount == 0, "valid multi-direction arrays marked invalid");
        Require(HasSpec(result, 2, 3) && HasSpec(result, 3, 2),
            "M/N must follow component long/short axes");
    }

    private static void SplitsArraysAtOutlineGapG()
    {
        List<ArrayComponent> items = Grid("G1", 1, 2, 0, 0, 0);
        items.AddRange(Grid("G2", 1, 2, 0, 2300, 0));
        ArrayRecognitionResult result = Detect(items, 20);
        Require(result.Arrays.Count == 2, "groups beyond G were joined");
        Require(result.Arrays[0].ComponentCount == 2 && result.Arrays[1].ComponentCount == 2,
            "gap split changed component ownership");
    }

    private static void ReportsMissingComponent()
    {
        List<ArrayComponent> items = Grid("MISS", 2, 3, 0, 0, 0);
        items.RemoveAt(2);
        AssertInvalidAll(items, "缺块");
    }

    private static void ReportsMisalignedComponent()
    {
        List<ArrayComponent> items = Grid("SHIFT", 2, 2, 0, 0, 0);
        items[3].Center = items[3].Center + new ArrayVec2(2, 0);
        AssertInvalidAll(items, "错位");
    }

    private static void ReportsDuplicatePosition()
    {
        List<ArrayComponent> items = Grid("DUP", 1, 2, 0, 0, 0);
        ArrayComponent duplicate = Clone(items[0], "DUP-X");
        items.Add(duplicate);
        AssertInvalidAll(items, "重复");
    }

    private static void ReportsMixedElevation()
    {
        List<ArrayComponent> items = Grid("Z", 2, 2, 0, 0, 0);
        items[3].Elevation = 12;
        AssertInvalidAll(items, "标高");
    }

    private static void KeepsOneByOneAsValidArray()
    {
        ArrayRecognitionResult result = Detect(Grid("ONE", 1, 1, 0, 0, 0), 20);
        Require(result.Arrays.Count == 1 && result.Arrays[0].CountAlongLong == 1 &&
            result.Arrays[0].CountAlongShort == 1, "1x1 was not counted as a valid array");
    }

    private static void DeduplicatesRepeatedSourceObjects()
    {
        List<ArrayComponent> items = Grid("MW", 1, 2, 0, 0, 0);
        items.Add(Clone(items[0], items[0].SourceId));
        ArrayRecognitionResult result = Detect(items, 20);
        Require(result.Arrays.Count == 1 && result.RecognizedComponentCount == 2,
            "repeated window selection was not deduplicated by source identity");
    }

    private static void FingerprintChangesWithGeometry()
    {
        List<ArrayComponent> items = Grid("FP", 1, 2, 0, 0, 0);
        string before = ArrayRecognitionFingerprint.Create(items);
        items[1].Center = items[1].Center + new ArrayVec2(1, 0);
        string after = ArrayRecognitionFingerprint.Create(items);
        Require(!String.Equals(before, after, StringComparison.Ordinal),
            "geometry mutation did not invalidate recognition fingerprint");
    }

    private static void FingerprintIgnoresEquivalentAxisDirection()
    {
        List<ArrayComponent> items = Grid("AXIS", 1, 1, 0, 0, Math.PI);
        string first = ArrayRecognitionFingerprint.Create(items);
        items[0].LongAxis = items[0].LongAxis * -1.0;
        string second = ArrayRecognitionFingerprint.Create(items);
        Require(String.Equals(first, second, StringComparison.Ordinal),
            "equivalent reversed long axis changed fingerprint");
    }

    private static void AcceptsOpenFlagWithRepeatedStartPoint()
    {
        List<ArrayVec2> normalized;
        string reason;
        bool accepted = ArrayComponentBoundaryReader.TryNormalize(
            RectangleOutline(new ArrayVec2(0, 0)), false, 0.01, 0.1,
            out normalized, out reason);
        Require(accepted, "exactly closed geometry was rejected: " + reason);
        Require(normalized.Count == 4,
            "repeated start point was not removed from open-flag outline");
    }

    private static void AcceptsOpenFlagWithinClosureTolerance()
    {
        List<ArrayVec2> normalized;
        string reason;
        bool accepted = ArrayComponentBoundaryReader.TryNormalize(
            RectangleOutline(new ArrayVec2(0.05, 0)), false, 0.01, 0.1,
            out normalized, out reason);
        Require(accepted, "near-closed component was rejected: " + reason);
        Require(normalized.Count == 4,
            "near-closed endpoint was not normalized");
    }

    private static void RejectsOpenOutlineBeyondClosureTolerance()
    {
        List<ArrayVec2> normalized;
        string reason;
        bool accepted = ArrayComponentBoundaryReader.TryNormalize(
            RectangleOutline(new ArrayVec2(0.2, 0)), false, 0.01, 0.1,
            out normalized, out reason);
        Require(!accepted && reason.IndexOf("未闭合",
            StringComparison.Ordinal) >= 0 && reason.IndexOf("0.2 mm",
                StringComparison.Ordinal) >= 0,
            "open component did not report its endpoint gap");
    }

    private static void NormalizesRepeatedClosureVertex()
    {
        List<ArrayVec2> normalized;
        string reason;
        bool accepted = ArrayComponentBoundaryReader.TryNormalize(
            RectangleOutline(new ArrayVec2(0, 0)), true, 0.01, 0.1,
            out normalized, out reason);
        Require(accepted && normalized.Count == 4,
            "closed outline retained its repeated closure vertex");
    }

    private static List<ArrayVec2> RectangleOutline(ArrayVec2 endpoint)
    {
        return new List<ArrayVec2>
        {
            new ArrayVec2(0, 0), new ArrayVec2(2382, 0),
            new ArrayVec2(2382, 1134), new ArrayVec2(0, 1134), endpoint
        };
    }

    private static void SummarizesFixed272Array1854ComponentExample()
    {
        ArrayRecognitionResult result = new ArrayRecognitionResult();
        AddSyntheticArrays(result, 7, 1, 222);
        AddSyntheticArrays(result, 3, 2, 50);
        ArrayRecognitionSummary summary = ArrayRecognitionStatistics.Summarize(result);
        Require(summary.RecognizedArrayCount == 272, "fixed example array total");
        Require(summary.RecognizedComponentCount == 1854, "fixed example component total");
        Require(summary.Entries.Count == 2, "fixed example row count");
        Require(summary.Entries[0].CountAlongLong == 3 &&
            summary.Entries[0].CountAlongShort == 2 &&
            summary.Entries[0].ArrayCount == 50 &&
            summary.Entries[0].TotalComponentCount == 300, "3x2 summary row");
        Require(summary.Entries[1].CountAlongLong == 7 &&
            summary.Entries[1].CountAlongShort == 1 &&
            summary.Entries[1].ArrayCount == 222 &&
            summary.Entries[1].TotalComponentCount == 1554, "7x1 summary row");
    }

    private static ArrayRecognitionResult Detect(IList<ArrayComponent> items, double gap)
    {
        return ArrayRecognitionEngine.Detect(items, new ArrayRecognitionOptions
        {
            MaximumOutlineGap = gap,
            AlignmentTolerance = 0.5,
            SuspectAlignmentTolerance = 3.0,
            ElevationTolerance = 0.01
        });
    }

    private static void AssertInvalidAll(List<ArrayComponent> items, string reasonPart)
    {
        ArrayRecognitionResult result = Detect(items, 20);
        Require(result.Arrays.Count == 0, reasonPart + ": invalid group leaked into valid arrays");
        Require(result.InvalidComponentCount == items.Count, reasonPart + ": invalid count");
        Require(result.InvalidGroups.Count == 1 &&
            result.InvalidGroups[0].Reason.IndexOf(reasonPart, StringComparison.Ordinal) >= 0,
            reasonPart + ": explicit reason missing");
    }

    private static List<ArrayComponent> Grid(string prefix, int alongLong,
        int alongShort, double originX, double originY, double angle)
    {
        const double longLength = 2382;
        const double shortLength = 1134;
        const double gap = 10;
        ArrayVec2 u = new ArrayVec2(Math.Cos(angle), Math.Sin(angle));
        ArrayVec2 v = new ArrayVec2(-u.Y, u.X);
        List<ArrayComponent> result = new List<ArrayComponent>();
        for (int m = 0; m < alongLong; m++)
            for (int n = 0; n < alongShort; n++)
            {
                ArrayVec2 center = new ArrayVec2(originX, originY) +
                    u * (m * (longLength + gap)) +
                    v * (n * (shortLength + gap));
                result.Add(new ArrayComponent
                {
                    SourceId = prefix + "-" + m + "-" + n,
                    Kind = ArrayComponentKind.LightweightPolyline,
                    LayerName = "PV", Center = center, LongAxis = u, ShortAxis = v,
                    LongLength = longLength, ShortLength = shortLength, Elevation = 0
                });
            }
        return result;
    }

    private static ArrayComponent Clone(ArrayComponent source, string sourceId)
    {
        return new ArrayComponent
        {
            SourceId = sourceId, Kind = source.Kind, LayerName = source.LayerName,
            Center = source.Center, LongAxis = source.LongAxis, ShortAxis = source.ShortAxis,
            LongLength = source.LongLength, ShortLength = source.ShortLength,
            Elevation = source.Elevation
        };
    }

    private static bool HasSpec(ArrayRecognitionResult result, int m, int n)
    {
        for (int index = 0; index < result.Arrays.Count; index++)
            if (result.Arrays[index].CountAlongLong == m &&
                result.Arrays[index].CountAlongShort == n)
                return true;
        return false;
    }

    private static void AddSyntheticArrays(ArrayRecognitionResult result,
        int m, int n, int count)
    {
        for (int index = 0; index < count; index++)
        {
            RecognizedArray array = new RecognizedArray
            {
                CountAlongLong = m, CountAlongShort = n
            };
            for (int component = 0; component < m * n; component++)
                array.Components.Add(new ArrayComponent());
            result.Arrays.Add(array);
        }
    }

    private static void Run(Action test)
    {
        try
        {
            test();
            _passed++;
        }
        catch
        {
            Console.Error.WriteLine("FAILED_CASE=" + test.Method.Name);
            throw;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
