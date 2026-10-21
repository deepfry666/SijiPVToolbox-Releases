using System;
using System.Text;

namespace StructuralToolbox.Core
{
    internal static class ToolMetadataCodec
    {
        internal const int MaximumXDataStringUtf8Bytes = 255;

        internal static string RequireXDataString(string value, string fieldName)
        {
            string safeValue = value ?? String.Empty;
            int byteCount = Encoding.UTF8.GetByteCount(safeValue);
            if (byteCount > MaximumXDataStringUtf8Bytes)
            {
                string safeFieldName = String.IsNullOrWhiteSpace(fieldName)
                    ? "未命名字段"
                    : fieldName;
                throw new InvalidOperationException(String.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "XData 字段“{0}”的 UTF-8 长度为 {1} 字节，超过 AutoCAD 单个字符串 {2} 字节的限制。为避免数据被截断，本次操作已取消。",
                    safeFieldName, byteCount, MaximumXDataStringUtf8Bytes));
            }
            return safeValue;
        }

    }
}
