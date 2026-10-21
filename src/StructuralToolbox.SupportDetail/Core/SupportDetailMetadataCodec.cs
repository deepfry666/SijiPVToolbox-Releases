using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using StructuralToolbox.SupportDetail.Settings;

namespace StructuralToolbox.SupportDetail.Core
{
    public static class SupportDetailMetadataCodec
    {
        public const int MaximumChunkCharacters = 120;

        public static string SerializeSettings(SupportDetailSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            SupportDetailSettings copy = settings.Clone();
            copy.Normalize();
            XmlSerializer serializer = new XmlSerializer(
                typeof(SupportDetailSettings));
            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.OmitXmlDeclaration = true;
            writerSettings.Indent = false;
            writerSettings.Encoding = new UTF8Encoding(false);
            XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
            namespaces.Add(String.Empty, String.Empty);
            StringBuilder output = new StringBuilder();
            using (StringWriter textWriter = new InvariantStringWriter(output))
            using (XmlWriter xmlWriter = XmlWriter.Create(textWriter,
                writerSettings))
            {
                serializer.Serialize(xmlWriter, copy, namespaces);
            }
            return output.ToString();
        }

        public static SupportDetailSettings DeserializeSettings(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("支架大样参数记录为空。");
            try
            {
                XmlSerializer serializer = new XmlSerializer(
                    typeof(SupportDetailSettings));
                using (StringReader reader = new StringReader(value))
                {
                    SupportDetailSettings settings = serializer.Deserialize(reader)
                        as SupportDetailSettings;
                    if (settings == null)
                        throw new InvalidOperationException("参数记录类型不正确。");
                    settings.Normalize();
                    return settings;
                }
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(
                    "无法读取支架大样参数记录：" + exception.Message,
                    exception);
            }
        }

        public static IList<string> SplitIntoChunks(string value)
        {
            List<string> chunks = new List<string>();
            string text = value ?? String.Empty;
            if (text.Length == 0)
            {
                chunks.Add(String.Empty);
                return chunks;
            }
            for (int offset = 0; offset < text.Length;
                offset += MaximumChunkCharacters)
            {
                chunks.Add(text.Substring(offset,
                    Math.Min(MaximumChunkCharacters, text.Length - offset)));
            }
            return chunks;
        }

        public static string JoinChunks(IEnumerable<string> chunks)
        {
            if (chunks == null)
                throw new ArgumentNullException("chunks");
            StringBuilder result = new StringBuilder();
            foreach (string chunk in chunks)
                result.Append(chunk ?? String.Empty);
            return result.ToString();
        }

        public static string FormatDouble(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        public static double ParseDouble(string value, string fieldName)
        {
            double result;
            if (!Double.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out result))
            {
                throw new InvalidOperationException(
                    "参数记录中的 " + fieldName + " 不是有效数字。");
            }
            return result;
        }

        private sealed class InvariantStringWriter : StringWriter
        {
            public InvariantStringWriter(StringBuilder builder)
                : base(builder, CultureInfo.InvariantCulture)
            {
            }

            public override Encoding Encoding
            {
                get { return new UTF8Encoding(false); }
            }
        }
    }
}
