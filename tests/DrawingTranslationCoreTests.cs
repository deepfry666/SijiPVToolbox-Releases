using System;
using System.Collections.Generic;
using StructuralToolbox.DrawingTranslation.Core;

internal static class DrawingTranslationCoreTests
{
    private static int _passed;

    private static int Main()
    {
        try
        {
            VerifySupportedKinds();
            VerifyMTextFormattingAndEngineeringTokens();
            VerifyUnsupportedReasons();
            VerifyBatchCountsAndFailures();
            VerifyDamagedTokensAreRejected();
            VerifyAdditionalCadFormatting();
            Console.WriteLine("PASS {0} drawing translation core tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL drawing translation core tests: " +
                exception.Message);
            return 1;
        }
    }

    private static void VerifySupportedKinds()
    {
        DrawingTextItem dbText = DrawingTextItemFactory.Create(
            "1A", DrawingTextKind.DbText, "导轨", false, false);
        DrawingTextItem mText = DrawingTextItemFactory.Create(
            "1B", DrawingTextKind.MText, "技术要求", false, false);
        DrawingTextItem table = DrawingTextItemFactory.Create(
            "1C", DrawingTextKind.TableCell, "材料表", false, false);
        DrawingTextItem attribute = DrawingTextItemFactory.Create(
            "1D", DrawingTextKind.AttributeReference, "编号", false, false);
        Require(dbText.CanWrite && mText.CanWrite && table.CanWrite &&
            attribute.CanWrite, "supported text kinds are not writable");
        _passed++;
    }

    private static void VerifyMTextFormattingAndEngineeringTokens()
    {
        string raw = @"\\fSimSun|b0|i0;钢檩条\\P间距 1500mm H200×100×4.5×6";
        DrawingTextItem item = DrawingTextItemFactory.Create(
            "2A", DrawingTextKind.MText, raw, false, false);
        Require(item.Protection.Tokens.Count >= 4,
            "format and engineering tokens were not protected");
        Require(item.TranslatableContent.Contains("钢檩条") &&
            item.TranslatableContent.Contains("间距"),
            "translatable Chinese text was removed");

        string translated = item.TranslatableContent.Replace("钢檩条", "Steel purlin")
            .Replace("间距", "spacing");
        string restored = TextProtection.Restore(translated, item.Protection);
        Require(restored.Contains(@"\\fSimSun|b0|i0;") &&
            restored.Contains(@"\\P") && restored.Contains("1500mm") &&
            restored.Contains("H200×100×4.5×6"),
            "protected MText content was not restored");
        _passed++;
    }

    private static void VerifyUnsupportedReasons()
    {
        DrawingTextItem field = DrawingTextItemFactory.Create(
            "3A", DrawingTextKind.DbText, "%<\\AcVar.3.40 DATE>%", true, false);
        DrawingTextItem dimension = DrawingTextItemFactory.Create(
            "3B", DrawingTextKind.Dimension, "<>", false, false);
        DrawingTextItem xref = DrawingTextItemFactory.Create(
            "3C", DrawingTextKind.MText, "外部说明", false, true);
        Require(!field.CanWrite && field.SkipReason.Contains("字段") &&
            !dimension.CanWrite && dimension.SkipReason.Contains("尺寸") &&
            !xref.CanWrite && xref.SkipReason.Contains("外部参照"),
            "unsupported entity reasons are unstable or missing");
        _passed++;
    }

    private static void VerifyBatchCountsAndFailures()
    {
        List<DrawingTextItem> items = new List<DrawingTextItem>
        {
            DrawingTextItemFactory.Create("4A", DrawingTextKind.DbText,
                "导轨", false, false),
            DrawingTextItemFactory.Create("4B", DrawingTextKind.Dimension,
                "<>", false, false)
        };
        TranslationBatchResult result = TranslationBatch.Apply(items,
            new List<string> { "rail" });
        Require(result.SkippedCount == 1 && result.ReadyCount == 1 &&
            items[0].Translation == "rail", "batch status accounting failed");

        List<DrawingTextItem> failed = new List<DrawingTextItem>
        {
            DrawingTextItemFactory.Create("4C", DrawingTextKind.DbText,
                "说明", false, false)
        };
        TranslationBatchResult failedResult = TranslationBatch.Apply(failed,
            new List<string> { "" });
        Require(failedResult.FailedCount == 1 &&
            failed[0].FailureReason.Contains("为空"),
            "empty translation was not rejected");
        _passed++;
    }

    private static void VerifyDamagedTokensAreRejected()
    {
        DrawingTextItem item = DrawingTextItemFactory.Create(
            "5A", DrawingTextKind.MText, @"说明\P间距 1500mm",
            false, false);
        Require(item.Protection.Tokens.Count >= 2,
            "expected protected format and distance tokens");
        string restored;
        string error;
        Require(!item.Protection.TryRestore("说明", out restored, out error),
            "missing tokens were accepted");
        string first = ProtectedText.TokenName(0);
        string second = ProtectedText.TokenName(1);
        Require(!item.Protection.TryRestore(second + first, out restored,
            out error), "reordered tokens were accepted");
        Require(!item.Protection.TryRestore(first + first + second,
            out restored, out error), "duplicate tokens were accepted");
        Require(item.Protection.TryRestore("Description" + first +
            "spacing " + second, out restored, out error) &&
            restored.Contains("1500mm"), "valid tokens did not restore");
        Require(!item.Protection.TryRestore("Description _STPV_TOKEN_0000_",
            out restored, out error),
            "damaged internal marker was accepted");
        Require(!ProtectedText.TryValidateFinal(
            "Thickness 3~__STPV_TOKEN_0000__", out error) &&
            error.Contains("STPV_TOKEN"),
            "internal marker was accepted as final user-visible text");
        Require(ProtectedText.TryValidateFinal("Thickness 3~5mm", out error),
            "clean restored translation was rejected");
        _passed++;
    }

    private static void VerifyAdditionalCadFormatting()
    {
        string raw = @"{坡度 \U+00B0}\~%%c 20mm";
        ProtectedText protection = TextProtection.Protect(raw);
        Require(protection.Tokens.Count >= 6,
            "CAD formatting tokens were not protected");
        string restored;
        string error;
        Require(protection.TryRestore(protection.Template.Replace("坡度",
            "Slope"), out restored, out error) &&
            restored.Contains(@"\U+00B0") &&
            restored.Contains("%%c") && restored.Contains("20mm"),
            "CAD formatting tokens did not survive translation");
        _passed++;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
