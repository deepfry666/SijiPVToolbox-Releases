using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StructuralToolbox.Layout.Core
{
    public sealed class ObstacleSelectionSummary
    {
        private readonly SortedDictionary<string, int> _ignoredReasons =
            new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int PolygonCount { get; private set; }
        public int CircleCount { get; private set; }
        public int LineCount { get; private set; }
        public int OpenPolylineCount { get; private set; }
        public int IgnoredCount { get; private set; }
        public int RecognizedCount
        {
            get
            {
                return PolygonCount + CircleCount + LineCount +
                    OpenPolylineCount;
            }
        }
        public int SelectedCount { get { return RecognizedCount + IgnoredCount; } }
        public int LinearPathCount { get { return LineCount + OpenPolylineCount; } }
        public IDictionary<string, int> IgnoredReasons
        {
            get
            {
                return new SortedDictionary<string, int>(_ignoredReasons,
                    StringComparer.Ordinal);
            }
        }

        public void AddPolygon() { PolygonCount++; }
        public void AddCircle() { CircleCount++; }
        public void AddLine() { LineCount++; }
        public void AddOpenPolyline() { OpenPolylineCount++; }

        public void AddIgnored(string reason)
        {
            string key = String.IsNullOrWhiteSpace(reason)
                ? "未知原因" : reason.Trim();
            int count;
            _ignoredReasons[key] = _ignoredReasons.TryGetValue(key,
                out count) ? count + 1 : 1;
            IgnoredCount++;
        }

        public string ToMessage()
        {
            StringBuilder message = new StringBuilder();
            message.AppendFormat(CultureInfo.InvariantCulture,
                "障碍识别：共选择 {0} 个；已识别 {1} 个（闭合轮廓 {2}、圆 {3}、LINE 线障碍 {4}、开放多段线障碍 {5}）；忽略 {6} 个。",
                SelectedCount, RecognizedCount, PolygonCount, CircleCount,
                LineCount, OpenPolylineCount, IgnoredCount);
            if (_ignoredReasons.Count > 0)
            {
                message.Append(" 忽略原因：");
                bool first = true;
                foreach (KeyValuePair<string, int> item in _ignoredReasons)
                {
                    if (!first) message.Append("；");
                    message.AppendFormat(CultureInfo.InvariantCulture,
                        "{0} {1} 个", item.Key, item.Value);
                    first = false;
                }
                message.Append("。");
            }
            return message.ToString();
        }
    }
}
