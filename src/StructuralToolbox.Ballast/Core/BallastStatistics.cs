using System;
using System.Collections.Generic;
using System.Globalization;

namespace StructuralToolbox.Ballast.Core
{
    public sealed class BallastStatisticItem
    {
        public BallastShape Shape { get; set; }
        public double Diameter { get; set; }
        public double RectLength { get; set; }
        public double RectWidth { get; set; }
        public double Height { get; set; }
        public string ColorLabel { get; set; }
        public int Quantity { get; set; }

        public string Specification
        {
            get
            {
                if (Shape == BallastShape.Circle)
                    return "D" + Format(Diameter) + "xH" + Format(Height);
                return Format(RectLength) + "x" + Format(RectWidth) +
                    "xH" + Format(Height);
            }
        }

        private static string Format(double value)
        {
            return value > 0.0
                ? value.ToString("0.##", CultureInfo.InvariantCulture)
                : "未定义";
        }
    }

    public sealed class BallastStatisticEntry
    {
        public BallastShape Shape { get; set; }
        public string Specification { get; set; }
        public string ColorLabel { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class BallastStatisticSummary
    {
        public BallastStatisticSummary()
        {
            Entries = new List<BallastStatisticEntry>();
        }

        public List<BallastStatisticEntry> Entries { get; private set; }
        public int TotalQuantity { get; set; }
    }

    public static class BallastStatistics
    {
        public static BallastStatisticSummary Summarize(
            IEnumerable<BallastStatisticItem> items)
        {
            BallastStatisticSummary summary = new BallastStatisticSummary();
            Dictionary<string, BallastStatisticEntry> grouped =
                new Dictionary<string, BallastStatisticEntry>(
                    StringComparer.OrdinalIgnoreCase);
            if (items == null)
                return summary;

            foreach (BallastStatisticItem item in items)
            {
                if (item == null || item.Quantity <= 0)
                    continue;
                string color = String.IsNullOrWhiteSpace(item.ColorLabel)
                    ? "默认" : item.ColorLabel.Trim();
                string key = ((int)item.Shape).ToString(
                    CultureInfo.InvariantCulture) + "|" +
                    item.Specification + "|" + color;
                BallastStatisticEntry entry;
                if (!grouped.TryGetValue(key, out entry))
                {
                    entry = new BallastStatisticEntry
                    {
                        Shape = item.Shape,
                        Specification = item.Specification,
                        ColorLabel = color
                    };
                    grouped.Add(key, entry);
                    summary.Entries.Add(entry);
                }
                entry.Quantity += item.Quantity;
                summary.TotalQuantity += item.Quantity;
            }
            summary.Entries.Sort(CompareEntries);
            return summary;
        }

        private static int CompareEntries(BallastStatisticEntry left,
            BallastStatisticEntry right)
        {
            int shape = left.Shape.CompareTo(right.Shape);
            if (shape != 0)
                return shape;
            int specification = StringComparer.OrdinalIgnoreCase.Compare(
                left.Specification, right.Specification);
            if (specification != 0)
                return specification;
            return StringComparer.OrdinalIgnoreCase.Compare(
                left.ColorLabel, right.ColorLabel);
        }
    }
}
