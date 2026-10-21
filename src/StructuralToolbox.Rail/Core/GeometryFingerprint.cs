using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StructuralToolbox.Core
{
    public static class GeometryFingerprint
    {
        private const string FormatVersion = "GFP1";
        private const double LinearQuantum = 0.001;
        private const double UnitAxisQuantum = 0.000000000001;

        private struct QuantizedPoint
        {
            public long X;
            public long Y;
        }

        public static string CreatePanels(IList<PanelRect> panels)
        {
            if (panels == null)
                throw new ArgumentNullException("panels");
            List<string> records = new List<string>();
            for (int index = 0; index < panels.Count; index++)
            {
                PanelRect panel = panels[index];
                if (panel == null)
                    throw new ArgumentException("Panel list contains null.", "panels");
                Vec2 longAxis = CanonicalDirection(panel.LongAxis);
                Vec2 shortAxis = CanonicalDirection(panel.ShortAxis);
                records.Add(String.Format(CultureInfo.InvariantCulture,
                    "P|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}",
                    QuantizeLinear(panel.Center.X),
                    QuantizeLinear(panel.Center.Y),
                    QuantizeLinear(panel.Elevation),
                    QuantizeAxis(longAxis.X),
                    QuantizeAxis(longAxis.Y),
                    QuantizeAxis(shortAxis.X),
                    QuantizeAxis(shortAxis.Y),
                    QuantizeLinear(panel.LongLength),
                    QuantizeLinear(panel.ShortLength)));
            }
            return CreateHash("PANELS", records);
        }

        public static string CreateSegments(IList<RailSegment> segments)
        {
            if (segments == null)
                throw new ArgumentNullException("segments");
            List<string> records = new List<string>();
            for (int index = 0; index < segments.Count; index++)
            {
                RailSegment segment = segments[index];
                if (segment == null)
                    throw new ArgumentException("Segment list contains null.", "segments");
                QuantizedPoint first = QuantizePoint(segment.Start);
                QuantizedPoint second = QuantizePoint(segment.End);
                if (Compare(first, second) > 0)
                {
                    QuantizedPoint swap = first;
                    first = second;
                    second = swap;
                }
                records.Add(String.Format(CultureInfo.InvariantCulture,
                    "R|{0}|{1}|{2}|{3}|{4}",
                    first.X, first.Y, second.X, second.Y,
                    QuantizeLinear(segment.Elevation)));
            }
            return CreateHash("SEGMENTS", records);
        }

        private static Vec2 CanonicalDirection(Vec2 direction)
        {
            Vec2 normalized = direction.Normalized();
            if (normalized.X < -1e-12 ||
                (Math.Abs(normalized.X) <= 1e-12 && normalized.Y < 0.0))
            {
                normalized = normalized * -1.0;
            }
            return normalized;
        }

        private static QuantizedPoint QuantizePoint(Vec2 point)
        {
            return new QuantizedPoint
            {
                X = QuantizeLinear(point.X),
                Y = QuantizeLinear(point.Y)
            };
        }

        private static long QuantizeLinear(double value)
        {
            return Quantize(value, LinearQuantum);
        }

        private static long QuantizeAxis(double value)
        {
            return Quantize(value, UnitAxisQuantum);
        }

        private static long Quantize(double value, double quantum)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
                throw new ArgumentException("Fingerprint values must be finite.");

            double rounded = Math.Round(value / quantum,
                MidpointRounding.AwayFromZero);
            if (rounded == 0.0)
                return 0L;
            try
            {
                return checked((long)rounded);
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException("value", value,
                    "Fingerprint value exceeds the supported quantized range.");
            }
        }

        private static int Compare(QuantizedPoint first, QuantizedPoint second)
        {
            int x = first.X.CompareTo(second.X);
            return x != 0 ? x : first.Y.CompareTo(second.Y);
        }

        private static string CreateHash(string recordKind, IList<string> records)
        {
            List<string> sorted = new List<string>(records);
            sorted.Sort(StringComparer.Ordinal);
            string source = FormatVersion + "|" + recordKind + "\n" +
                String.Join("\n", sorted.ToArray());
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(source));
                return BitConverter.ToString(digest).Replace("-", String.Empty);
            }
        }
    }
}
