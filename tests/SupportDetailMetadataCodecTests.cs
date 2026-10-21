using System;
using System.Collections.Generic;
using StructuralToolbox.SupportDetail.Core;
using StructuralToolbox.SupportDetail.Settings;

internal static class SupportDetailMetadataCodecTests
{
    private static int _passed;

    public static int Main()
    {
        try
        {
            SettingsRoundTripPreservesEditableParameters();
            LongMetadataIsSplitWithoutLoss();
            InvalidMetadataIsRejected();
            Console.WriteLine("PASS {0} support detail metadata tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL support detail metadata tests: {0}",
                exception.Message);
            return 1;
        }
    }

    private static void SettingsRoundTripPreservesEditableParameters()
    {
        SupportDetailSettings settings = new SupportDetailSettings();
        settings.ComponentCount = 7;
        settings.ComponentLength = 2278.5;
        settings.ComponentGap = 23.0;
        settings.SlopeDegrees = 13.728;
        settings.RailPositionMode = SupportDetailPositionMode.Manual;
        settings.RailPositions = "410, 1880";
        settings.PierPositionMode = SupportDetailPositionMode.Manual;
        settings.PierStations = "150, 1450, 2890, 4230";
        settings.PierCountFollowsComponents = false;
        settings.PierCount = 4;
        settings.ColumnHeight = 376.5;
        settings.BeamOverhang = 245.0;
        settings.PierShape = SupportDetailPierShape.Rectangular;
        settings.PierLength = 650.0;
        settings.PierWidth = 480.0;
        settings.PierHeight = 420.0;
        settings.OutputLayer = "支架大样-可编辑";
        settings.IncludeBackPull = true;

        string encoded = SupportDetailMetadataCodec.SerializeSettings(settings);
        SupportDetailSettings decoded =
            SupportDetailMetadataCodec.DeserializeSettings(encoded);
        Equal(7, decoded.ComponentCount, "component count");
        Near(13.728, decoded.SlopeDegrees, "slope");
        Equal("410, 1880", decoded.RailPositions, "rail positions");
        Equal("150, 1450, 2890, 4230", decoded.PierStations,
            "pier stations");
        Equal(SupportDetailPierShape.Rectangular, decoded.PierShape,
            "pier shape");
        Equal("支架大样-可编辑", decoded.OutputLayer, "unicode layer");
        Require(decoded.IncludeBackPull, "back pull option");
        Pass();
    }

    private static void LongMetadataIsSplitWithoutLoss()
    {
        string value = new string('A', 395) + "支架参数" + new string('B', 77);
        IList<string> chunks = SupportDetailMetadataCodec.SplitIntoChunks(value);
        Require(chunks.Count > 3, "long metadata did not split");
        for (int index = 0; index < chunks.Count; index++)
            Require(chunks[index].Length <=
                SupportDetailMetadataCodec.MaximumChunkCharacters,
                "metadata chunk exceeds the storage limit");
        Equal(value, SupportDetailMetadataCodec.JoinChunks(chunks),
            "chunk round trip");
        Pass();
    }

    private static void InvalidMetadataIsRejected()
    {
        bool failed = false;
        try
        {
            SupportDetailMetadataCodec.DeserializeSettings("not xml");
        }
        catch (InvalidOperationException)
        {
            failed = true;
        }
        Require(failed, "invalid metadata was accepted");
        Pass();
    }

    private static void Equal<T>(T expected, T actual, string context)
    {
        if (!Object.Equals(expected, actual))
            throw new InvalidOperationException(context + ": expected " +
                expected + ", actual " + actual);
    }

    private static void Near(double expected, double actual, string context)
    {
        if (Math.Abs(expected - actual) > 0.000001)
            throw new InvalidOperationException(context + ": expected " +
                expected + ", actual " + actual);
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
