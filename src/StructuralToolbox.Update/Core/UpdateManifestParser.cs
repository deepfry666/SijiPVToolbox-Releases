using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StructuralToolbox.Update
{
    public static class UpdateManifestParser
    {
        private const int MaximumJsonDepth = 16;

        public static UpdateManifest Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
                throw new UpdateException("更新清单内容为空。");

            object root = new JsonParser(json).ParseRoot();
            Dictionary<string, object> map = root as
                Dictionary<string, object>;
            if (map == null)
                throw new UpdateException("更新清单根节点必须是 JSON 对象。");

            object schemaValue;
            if (!map.TryGetValue("schemaVersion", out schemaValue) ||
                !(schemaValue is long) || (long)schemaValue != 1L)
            {
                throw new UpdateException("更新清单 schemaVersion 必须是 1。");
            }

            UpdateManifest manifest = new UpdateManifest();

            string product = ReadRequiredText(map, "product");
            if (!String.Equals(product, UpdateManifest.ProductName,
                    StringComparison.Ordinal))
            {
                throw new UpdateException(
                    "更新清单 product 字段必须是 " +
                    UpdateManifest.ProductName + "，当前为：" + product);
            }

            string channel = ReadRequiredText(map, "channel");
            if (!String.Equals(channel, "stable", StringComparison.Ordinal))
                throw new UpdateException("当前客户端只接受 stable 更新通道。");
            manifest.Channel = channel;

            string versionText = ReadRequiredText(map, "version");
            Version parsedVersion;
            if (!Version.TryParse(versionText, out parsedVersion) ||
                parsedVersion.Major < 0 || parsedVersion.Minor < 0 ||
                parsedVersion.Build < 0)
            {
                throw new UpdateException(
                    "version 字段无法解析，需要形如 1.2.0 的数字版本号： " +
                    versionText);
            }
            manifest.Version = parsedVersion;

            string urlText = ReadRequiredText(map, "url");
            Uri packageUrl;
            if (!Uri.TryCreate(urlText, UriKind.Absolute, out packageUrl))
                throw new UpdateException("url 字段不是有效的绝对网址：" + urlText);
            string scheme = packageUrl.Scheme.ToLowerInvariant();
            if (scheme != "https" && scheme != "file")
                throw new UpdateException(
                    "在线安装包必须使用 HTTPS；离线测试可使用 file 地址：" + urlText);
            manifest.PackageUrl = packageUrl;
            manifest.PackageUrls.Add(packageUrl);
            object mirrorsValue;
            if (map.TryGetValue("mirrors", out mirrorsValue))
            {
                List<object> mirrorItems = mirrorsValue as List<object>;
                if (mirrorItems == null || mirrorItems.Count > 4)
                    throw new UpdateException("mirrors 字段必须是 HTTPS 网址数组。");
                foreach (object item in mirrorItems)
                {
                    string mirrorText = item as string;
                    Uri mirror;
                    if (String.IsNullOrWhiteSpace(mirrorText) ||
                        !Uri.TryCreate(mirrorText.Trim(), UriKind.Absolute, out mirror) ||
                        !String.Equals(mirror.Scheme, "https", StringComparison.OrdinalIgnoreCase) ||
                        !String.IsNullOrEmpty(mirror.UserInfo) || !String.IsNullOrEmpty(mirror.Fragment))
                    {
                        throw new UpdateException("mirrors 字段只能包含 HTTPS 网址。");
                    }
                    bool duplicate = false;
                    foreach (Uri existing in manifest.PackageUrls)
                        if (String.Equals(existing.AbsoluteUri, mirror.AbsoluteUri,
                            StringComparison.Ordinal)) duplicate = true;
                    if (!duplicate) manifest.PackageUrls.Add(mirror);
                }
            }

            string sha256 = ReadRequiredText(map, "sha256").Trim();
            if (sha256.Length != 64 || !IsHexadecimal(sha256))
                throw new UpdateException(
                    "sha256 字段必须是 64 位十六进制哈希值。");
            manifest.Sha256 = sha256;

            object sizeValue;
            if (map.TryGetValue("sizeBytes", out sizeValue))
            {
                long sizeBytes;
                if (!(sizeValue is long) ||
                    (sizeBytes = (long)sizeValue) < 0L)
                {
                    throw new UpdateException(
                        "sizeBytes 字段必须是不小于 0 的整数。");
                }
                manifest.SizeBytes = sizeBytes;
            }

            string dateText = ReadOptionalText(map, "date");
            if (dateText != null && dateText.Length > 40)
                throw new UpdateException("date 字段过长。");
            manifest.ReleaseDate = dateText;

            object notesValue;
            if (map.TryGetValue("notes", out notesValue))
            {
                List<object> noteItems = notesValue as List<object>;
                if (noteItems == null)
                    throw new UpdateException("notes 字段必须是字符串数组。");
                List<string> notes = new List<string>();
                foreach (object item in noteItems)
                {
                    string text = item as string;
                    if (text == null || text.Length > 200)
                        throw new UpdateException(
                            "notes 数组必须由不超过 200 字的字符串组成。");
                    notes.Add(text);
                }
                manifest.Notes = notes;
            }

            return manifest;
        }

        private static string ReadRequiredText(
            Dictionary<string, object> map,
            string key)
        {
            object value = ReadValue(map, key);
            string text = value as string;
            if (String.IsNullOrWhiteSpace(text))
                throw new UpdateException("更新清单缺少必填字段：" + key);
            return text.Trim();
        }

        private static string ReadOptionalText(
            Dictionary<string, object> map,
            string key)
        {
            object value = ReadValue(map, key);
            return value as string;
        }

        private static object ReadValue(
            Dictionary<string, object> map,
            string key)
        {
            object value;
            if (!map.TryGetValue(key, out value))
                return null;
            return value ?? String.Empty;
        }

        private static bool IsHexadecimal(string value)
        {
            foreach (char character in value)
            {
                bool isDigit = character >= '0' && character <= '9';
                bool isLowerHex =
                    character >= 'a' && character <= 'f';
                bool isUpperHex =
                    character >= 'A' && character <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                    return false;
            }
            return true;
        }

        private sealed class JsonParser
        {
            private readonly string _text;
            private int _index;

            public JsonParser(string text)
            {
                _text = text;
                _index = 0;
            }

            public object ParseRoot()
            {
                SkipWhitespace();
                object root = ParseValue(1);
                SkipWhitespace();
                if (_index < _text.Length)
                    throw CreateError("JSON 在结尾处有多余内容。");
                return root;
            }

            private object ParseValue(int depth)
            {
                if (depth > MaximumJsonDepth)
                    throw CreateError("JSON 嵌套过深。");
                SkipWhitespace();
                if (_index >= _text.Length)
                    throw CreateError("JSON 意外结束。");

                char current = _text[_index];
                switch (current)
                {
                    case '{':
                        return ParseObject(depth + 1);
                    case '[':
                        return ParseArray(depth + 1);
                    case '"':
                        return ParseString();
                    default:
                        if (MatchesLiteral("true"))
                        {
                            ConsumeLiteral(4);
                            return true;
                        }
                        if (MatchesLiteral("false"))
                        {
                            ConsumeLiteral(5);
                            return false;
                        }
                        if (MatchesLiteral("null"))
                        {
                            ConsumeLiteral(4);
                            return null;
                        }
                        return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject(int depth)
            {
                Expect('{');
                Dictionary<string, object> result =
                    new Dictionary<string, object>(
                        StringComparer.OrdinalIgnoreCase);
                SkipWhitespace();
                if (Peek('}'))
                {
                    Consume('}');
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    string name = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    object value = ParseValue(depth);
                    if (value != null)
                    {
                        if (result.ContainsKey(name))
                            throw CreateError("JSON 存在重复字段：" + name);
                        result.Add(name, value);
                    }
                    SkipWhitespace();
                    if (Peek(','))
                    {
                        Consume(',');
                        continue;
                    }
                    break;
                }

                Expect('}');
                return result;
            }

            private List<object> ParseArray(int depth)
            {
                Expect('[');
                List<object> result = new List<object>();
                SkipWhitespace();
                if (Peek(']'))
                {
                    Consume(']');
                    return result;
                }

                while (true)
                {
                    object value = ParseValue(depth);
                    if (value != null)
                        result.Add(value);
                    SkipWhitespace();
                    if (Peek(','))
                    {
                        Consume(',');
                        continue;
                    }
                    break;
                }

                Expect(']');
                return result;
            }

            private string ParseString()
            {
                SkipWhitespace();
                Expect('"');
                StringBuilder builder = new StringBuilder();
                while (_index < _text.Length)
                {
                    char current = _text[_index++];
                    if (current == '"')
                        return builder.ToString();
                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }
                    if (_index >= _text.Length)
                        throw CreateError("字符串转义不完整。");
                    char escape = _text[_index++];
                    switch (escape)
                    {
                        case '"':
                            builder.Append('"');
                            break;
                        case '\\':
                            builder.Append('\\');
                            break;
                        case '/':
                            builder.Append('/');
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeEscape());
                            break;
                        default:
                            throw CreateError("无效的转义字符：\\" + escape);
                    }
                }
                throw CreateError("字符串缺少结束引号。");
            }

            private string ParseUnicodeEscape()
            {
                if (_index + 4 > _text.Length)
                    throw CreateError("\\u 转义不完整。");
                string hex = _text.Substring(_index, 4);
                int codePoint;
                if (!Int32.TryParse(hex, NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out codePoint))
                {
                    throw CreateError("\\u 转义码无效：" + hex);
                }
                _index += 4;
                return Char.ConvertFromUtf32(codePoint);
            }

            private object ParseNumber()
            {
                int start = _index;
                bool sawDecimalPoint = false;
                while (_index < _text.Length)
                {
                    char current = _text[_index];
                    if (current >= '0' && current <= '9')
                    {
                        _index++;
                        continue;
                    }
                    if ((current == '-' || current == '+') &&
                        _index == start)
                    {
                        _index++;
                        continue;
                    }
                    if (current == '.' && !sawDecimalPoint)
                    {
                        sawDecimalPoint = true;
                        _index++;
                        continue;
                    }
                    break;
                }
                if (_index == start)
                    throw CreateError("JSON 值无法识别。");
                string token = _text.Substring(start, _index - start);
                if (sawDecimalPoint)
                {
                    double number;
                    if (!Double.TryParse(token, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out number))
                        throw CreateError("数字格式无效：" + token);
                    return number;
                }
                long integer;
                if (!Int64.TryParse(token, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out integer))
                    throw CreateError("整数超出支持范围：" + token);
                return integer;
            }

            private bool MatchesLiteral(string literal)
            {
                if (_index + literal.Length > _text.Length)
                    return false;
                return String.CompareOrdinal(_text, _index,
                    literal, 0, literal.Length) == 0;
            }

            private void ConsumeLiteral(int length)
            {
                _index += length;
            }

            private void SkipWhitespace()
            {
                while (_index < _text.Length &&
                    Char.IsWhiteSpace(_text[_index]))
                {
                    _index++;
                }
            }

            private bool Peek(char expected)
            {
                return _index < _text.Length && _text[_index] == expected;
            }

            private void Consume(char expected)
            {
                if (!Peek(expected))
                    Expect(expected);
                _index++;
            }

            private void Expect(char expected)
            {
                if (_index >= _text.Length || _text[_index] != expected)
                {
                    throw CreateError("JSON 需要字符 '" + expected +
                        "'，实际遇到 '" +
                        (_index < _text.Length
                            ? _text[_index].ToString()
                            : "结束") + "'。");
                }
                _index++;
            }

            private UpdateException CreateError(string message)
            {
                return new UpdateException(message + "（位置 " + _index + "）");
            }
        }
    }
}
