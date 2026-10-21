using System;
using System.Collections.Generic;
using System.Globalization;

namespace StructuralToolbox.PvComponent.Core
{
    public sealed class PvComponentStatisticItem
    {
        public string Name { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Thickness { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class PvComponentStatisticEntry
    {
        public string Name { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Thickness { get; set; }
        public int Quantity { get; set; }

        public string Specification
        {
            get
            {
                return Format(Length) + "x" + Format(Width) + "x" +
                    Format(Thickness);
            }
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    public sealed class PvComponentStatisticSummary
    {
        public PvComponentStatisticSummary()
        {
            Entries = new List<PvComponentStatisticEntry>();
        }

        public IList<PvComponentStatisticEntry> Entries { get; private set; }
        public int TotalQuantity { get; set; }
    }

    public static class PvComponentStatistics
    {
        public static PvComponentStatisticSummary Summarize(
            IEnumerable<PvComponentStatisticItem> items)
        {
            PvComponentStatisticSummary summary =
                new PvComponentStatisticSummary();
            Dictionary<string, PvComponentStatisticEntry> grouped =
                new Dictionary<string, PvComponentStatisticEntry>(
                    StringComparer.OrdinalIgnoreCase);
            if (items == null)
                return summary;

            foreach (PvComponentStatisticItem item in items)
            {
                if (item == null || item.Quantity <= 0 || item.Length <= 0.0 ||
                    item.Width <= 0.0 || item.Thickness <= 0.0)
                    continue;
                string name = String.IsNullOrWhiteSpace(item.Name)
                    ? "光伏组件" : item.Name.Trim();
                double length = RoundDimension(item.Length);
                double width = RoundDimension(item.Width);
                double thickness = RoundDimension(item.Thickness);
                string key = name + "|" + FormatKey(length) + "|" +
                    FormatKey(width) + "|" + FormatKey(thickness);
                PvComponentStatisticEntry entry;
                if (!grouped.TryGetValue(key, out entry))
                {
                    entry = new PvComponentStatisticEntry
                    {
                        Name = name,
                        Length = length,
                        Width = width,
                        Thickness = thickness
                    };
                    grouped.Add(key, entry);
                    summary.Entries.Add(entry);
                }
                entry.Quantity += item.Quantity;
                summary.TotalQuantity += item.Quantity;
            }
            List<PvComponentStatisticEntry> sorted =
                (List<PvComponentStatisticEntry>)summary.Entries;
            sorted.Sort(CompareEntries);
            return summary;
        }

        private static double RoundDimension(double value)
        {
            return Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }

        private static string FormatKey(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static int CompareEntries(PvComponentStatisticEntry left,
            PvComponentStatisticEntry right)
        {
            int name = StringComparer.OrdinalIgnoreCase.Compare(left.Name,
                right.Name);
            if (name != 0)
                return name;
            int length = left.Length.CompareTo(right.Length);
            if (length != 0)
                return length;
            int width = left.Width.CompareTo(right.Width);
            if (width != 0)
                return width;
            return left.Thickness.CompareTo(right.Thickness);
        }
    }
}
