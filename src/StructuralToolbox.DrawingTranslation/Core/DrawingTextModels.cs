using System;
using System.Collections.Generic;

namespace StructuralToolbox.DrawingTranslation.Core
{
    public enum DrawingTextKind
    {
        DbText,
        MText,
        TableCell,
        AttributeReference,
        MLeader,
        Dimension,
        Field,
        BlockReference,
        Xref,
        Unsupported
    }

    public enum TranslationItemStatus
    {
        Ready,
        Skipped,
        Failed,
        Stale
    }

    public sealed class ProtectedText
    {
        private static readonly System.Text.RegularExpressions.Regex
            InternalMarkerPattern = new System.Text.RegularExpressions.Regex(
                @"_*STPV[\s_-]*TOKEN[\s_-]*\d{1,8}_*",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        public ProtectedText(string template, IList<string> tokens)
        {
            Template = template ?? String.Empty;
            Tokens = tokens ?? new List<string>();
        }

        public string Template { get; private set; }
        public IList<string> Tokens { get; private set; }

        public string Restore(string translated)
        {
            string value = translated ?? String.Empty;
            for (int index = 0; index < Tokens.Count; index++)
                value = value.Replace(TokenName(index), Tokens[index]);
            return value;
        }

        public bool TryRestore(string translated, out string restored,
            out string error)
        {
            restored = String.Empty;
            error = String.Empty;
            if (String.IsNullOrWhiteSpace(translated))
            {
                error = "译文为空。";
                return false;
            }

            int previous = -1;
            for (int index = 0; index < Tokens.Count; index++)
            {
                string marker = TokenName(index);
                int position = translated.IndexOf(marker,
                    StringComparison.Ordinal);
                if (position <= previous || position < 0 ||
                    translated.IndexOf(marker, position + marker.Length,
                        StringComparison.Ordinal) >= 0)
                {
                    error = "格式或工程参数占位符被删除、重复或调换。";
                    return false;
                }
                previous = position;
            }
            string candidate = Restore(translated);
            if (ContainsInternalToken(candidate))
            {
                error = "译文包含未还原的内部占位符，已阻止写入。";
                return false;
            }
            restored = candidate;
            return true;
        }

        public static bool TryValidateFinal(string value, out string error)
        {
            error = String.Empty;
            if (String.IsNullOrWhiteSpace(value))
            {
                error = "译文为空。";
                return false;
            }
            if (ContainsInternalToken(value))
            {
                error = "译文包含内部占位符 STPV_TOKEN，已阻止写入。请重新翻译。";
                return false;
            }
            return true;
        }

        public static bool ContainsInternalToken(string value)
        {
            return !String.IsNullOrEmpty(value) &&
                InternalMarkerPattern.IsMatch(value);
        }

        public static string TokenName(int index)
        {
            return "__STPV_TOKEN_" + index.ToString("D4") + "__";
        }
    }

    public sealed class DrawingTextItem
    {
        public DrawingTextItem(string handle, DrawingTextKind kind,
            string rawContent, string translatableContent,
            ProtectedText protection, bool canWrite, string skipReason)
        {
            Handle = handle ?? String.Empty;
            Kind = kind;
            RawContent = rawContent ?? String.Empty;
            TranslatableContent = translatableContent ?? String.Empty;
            Protection = protection;
            CanWrite = canWrite;
            SkipReason = skipReason ?? String.Empty;
            Status = canWrite ? TranslationItemStatus.Ready :
                TranslationItemStatus.Skipped;
        }

        public string Handle { get; private set; }
        public DrawingTextKind Kind { get; private set; }
        public string RawContent { get; private set; }
        public string TranslatableContent { get; private set; }
        public ProtectedText Protection { get; private set; }
        public bool CanWrite { get; private set; }
        public string SkipReason { get; private set; }
        public TranslationItemStatus Status { get; set; }
        public string Translation { get; set; }
        public string FailureReason { get; set; }
    }

    public static class DrawingTextItemFactory
    {
        public static DrawingTextItem Create(string handle,
            DrawingTextKind kind, string rawContent, bool hasField,
            bool isFromXref)
        {
            string raw = rawContent ?? String.Empty;
            if (isFromXref)
                return Skipped(handle, DrawingTextKind.Xref, raw,
                    "外部参照文字首版不处理。");
            if (hasField || kind == DrawingTextKind.Field)
                return Skipped(handle, kind, raw,
                    "包含字段或动态值，首版跳过。");
            if (kind != DrawingTextKind.DbText &&
                kind != DrawingTextKind.MText &&
                kind != DrawingTextKind.TableCell &&
                kind != DrawingTextKind.AttributeReference)
            {
                string reason = kind == DrawingTextKind.Dimension
                    ? "尺寸标注可能包含真实测量值，首版只允许人工处理。"
                    : kind == DrawingTextKind.MLeader
                        ? "多重引线文字的关联内容需单独确认，首版跳过。"
                        : "该对象类型首版不支持翻译插入。";
                return Skipped(handle, kind, raw, reason);
            }

            ProtectedText protection = TextProtection.Protect(raw);
            return new DrawingTextItem(handle, kind, raw,
                protection.Template, protection, true, String.Empty);
        }

        private static DrawingTextItem Skipped(string handle,
            DrawingTextKind kind, string raw, string reason)
        {
            return new DrawingTextItem(handle, kind, raw, String.Empty,
                new ProtectedText(String.Empty, new List<string>()), false,
                reason);
        }
    }
}
