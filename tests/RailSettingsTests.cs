using System;
using System.IO;
using System.Xml.Serialization;
using StructuralToolbox.Settings;

internal static class RailSettingsTests
{
    private static int _passed;

    public static int Main()
    {
        try
        {
            UsesDocumentedDefaults();
            DeserializesLegacyFourFieldXmlWithNewDefaults();
            RoundTripsExtendedRailSettings();
            NormalizesLegacyInvalidValues();
            NormalizesInvalidLayerNames();
            NormalizesInvalidWidths();
            NormalizesInvalidColorModes();
            ClampsColorComponents();
            Console.WriteLine("PASS {0} rail settings tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static void UsesDocumentedDefaults()
    {
        RailSettings settings = new RailSettings();
        Equal(2, settings.RailCount, "default rail count");
        Near(1400.0, settings.TotalSpacing, "default total spacing");
        Near(20.0, settings.MaximumContinuityGap, "default continuity gap");
        Near(100.0, settings.EndOverhang, "default end overhang");
        Equal("导轨", settings.RailLayerName, "default layer name");
        Near(0.0, settings.RailGlobalWidth, "default global width");
        Equal(RailColorApplication.ByLayer, settings.RailColorApplication,
            "default color application");
        Equal(RailColorKind.Aci, settings.RailColorKind,
            "default color kind");
        Equal(6, settings.RailColorIndex, "default ACI color");
        Equal(255, settings.RailColorRed, "default red component");
        Equal(0, settings.RailColorGreen, "default green component");
        Equal(255, settings.RailColorBlue, "default blue component");
        Pass();
    }

    private static void DeserializesLegacyFourFieldXmlWithNewDefaults()
    {
        const string legacyXml =
            "<?xml version=\"1.0\"?>" +
            "<RailSettings xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" " +
            "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            "<RailCount>3</RailCount>" +
            "<TotalSpacing>1500</TotalSpacing>" +
            "<MaximumContinuityGap>25</MaximumContinuityGap>" +
            "<EndOverhang>120</EndOverhang>" +
            "</RailSettings>";

        RailSettings settings = Deserialize(legacyXml);
        Equal(3, settings.RailCount, "legacy rail count");
        Near(1500.0, settings.TotalSpacing, "legacy total spacing");
        Near(25.0, settings.MaximumContinuityGap, "legacy continuity gap");
        Near(120.0, settings.EndOverhang, "legacy end overhang");
        Equal("导轨", settings.RailLayerName, "legacy default layer name");
        Near(0.0, settings.RailGlobalWidth, "legacy default global width");
        Pass();
    }

    private static void RoundTripsExtendedRailSettings()
    {
        RailSettings source = new RailSettings();
        source.RailCount = 4;
        source.TotalSpacing = 1800.0;
        source.MaximumContinuityGap = 35.0;
        source.EndOverhang = 150.0;
        source.RailLayerName = "项目导轨";
        source.RailGlobalWidth = 2.5;
        source.RailColorApplication = RailColorApplication.ByEntity;
        source.RailColorKind = RailColorKind.TrueColor;
        source.RailColorIndex = 31;
        source.RailColorRed = 12;
        source.RailColorGreen = 34;
        source.RailColorBlue = 56;

        XmlSerializer serializer = new XmlSerializer(typeof(RailSettings));
        string xml;
        using (StringWriter writer = new StringWriter())
        {
            serializer.Serialize(writer, source);
            xml = writer.ToString();
        }

        RailSettings result = Deserialize(xml);
        Equal(4, result.RailCount, "round-trip rail count");
        Near(1800.0, result.TotalSpacing, "round-trip total spacing");
        Near(35.0, result.MaximumContinuityGap,
            "round-trip continuity gap");
        Near(150.0, result.EndOverhang, "round-trip end overhang");
        Equal("项目导轨", result.RailLayerName, "round-trip layer name");
        Near(2.5, result.RailGlobalWidth, "round-trip global width");
        Equal(RailColorApplication.ByEntity, result.RailColorApplication,
            "round-trip color application");
        Equal(RailColorKind.TrueColor, result.RailColorKind,
            "round-trip color kind");
        Equal(31, result.RailColorIndex, "round-trip ACI color");
        Equal(12, result.RailColorRed, "round-trip red component");
        Equal(34, result.RailColorGreen, "round-trip green component");
        Equal(56, result.RailColorBlue, "round-trip blue component");
        Pass();
    }

    private static void NormalizesLegacyInvalidValues()
    {
        RailSettings settings = new RailSettings();
        settings.RailCount = 100;
        settings.TotalSpacing = Double.NaN;
        settings.MaximumContinuityGap = Double.PositiveInfinity;
        settings.EndOverhang = -1.0;
        settings.Normalize();

        Equal(20, settings.RailCount, "normalized rail count");
        Near(0.0, settings.TotalSpacing, "normalized total spacing");
        Near(0.0, settings.MaximumContinuityGap,
            "normalized continuity gap");
        Near(0.0, settings.EndOverhang, "normalized end overhang");
        Pass();
    }

    private static void NormalizesInvalidLayerNames()
    {
        RailSettings settings = new RailSettings();
        settings.RailLayerName = "   ";
        settings.Normalize();
        Equal("导轨", settings.RailLayerName, "blank layer name");

        settings.RailLayerName = "  自定义导轨  ";
        settings.Normalize();
        Equal("自定义导轨", settings.RailLayerName, "trimmed layer name");

        settings.RailLayerName = new String('x', 300);
        settings.Normalize();
        Equal(255, settings.RailLayerName.Length,
            "maximum layer name length");
        Pass();
    }

    private static void NormalizesInvalidWidths()
    {
        double[] invalidWidths = new double[]
        {
            Double.NaN,
            Double.PositiveInfinity,
            Double.NegativeInfinity,
            -0.01
        };
        for (int index = 0; index < invalidWidths.Length; index++)
        {
            RailSettings settings = new RailSettings();
            settings.RailGlobalWidth = invalidWidths[index];
            settings.Normalize();
            Near(0.0, settings.RailGlobalWidth,
                "invalid global width " + invalidWidths[index]);
        }

        RailSettings maximum = new RailSettings();
        maximum.RailGlobalWidth = 100000.01;
        maximum.Normalize();
        Near(100000.0, maximum.RailGlobalWidth,
            "maximum global width");
        Pass();
    }

    private static void NormalizesInvalidColorModes()
    {
        RailSettings settings = new RailSettings();
        settings.RailColorApplication = (RailColorApplication)999;
        settings.RailColorKind = (RailColorKind)999;
        settings.Normalize();

        Equal(RailColorApplication.ByLayer, settings.RailColorApplication,
            "invalid color application");
        Equal(RailColorKind.Aci, settings.RailColorKind,
            "invalid color kind");
        Pass();
    }

    private static void ClampsColorComponents()
    {
        RailSettings settings = new RailSettings();
        settings.RailColorIndex = -1;
        settings.RailColorRed = -1;
        settings.RailColorGreen = 256;
        settings.RailColorBlue = 1000;
        settings.Normalize();
        Equal(1, settings.RailColorIndex, "minimum ACI color");
        Equal(0, settings.RailColorRed, "minimum red component");
        Equal(255, settings.RailColorGreen, "maximum green component");
        Equal(255, settings.RailColorBlue, "maximum blue component");

        settings.RailColorIndex = 1000;
        settings.Normalize();
        Equal(255, settings.RailColorIndex, "maximum ACI color");
        Pass();
    }

    private static RailSettings Deserialize(string xml)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(RailSettings));
        using (StringReader reader = new StringReader(xml))
            return (RailSettings)serializer.Deserialize(reader);
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!Object.Equals(expected, actual))
        {
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, got {2}", label, expected, actual));
        }
    }

    private static void Near(double expected, double actual, string label)
    {
        if (Math.Abs(expected - actual) > 0.000001)
        {
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, got {2}", label, expected, actual));
        }
    }

    private static void Pass()
    {
        _passed++;
    }
}
