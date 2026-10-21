using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StructuralToolbox.ArrayRecognition.Core
{
    public static class ArrayRecognitionFingerprint
    {
        public static string Create(IEnumerable<ArrayComponent> components)
        {
            List<string> records = new List<string>();
            if (components != null)
                foreach (ArrayComponent component in components)
                    if (component != null)
                        records.Add(Record(component));
            records.Sort(StringComparer.Ordinal);
            string content = String.Join("\n", records.ToArray());
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(content));
                StringBuilder result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                    result.Append(hash[index].ToString("X2",
                        CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static string Record(ArrayComponent component)
        {
            ArrayVec2 axis = CanonicalAxis(component.LongAxis);
            return String.Join("|", new string[]
            {
                component.SourceId ?? String.Empty,
                ((int)component.Kind).ToString(CultureInfo.InvariantCulture),
                component.LayerName ?? String.Empty,
                Format(component.Center.X), Format(component.Center.Y),
                Format(axis.X), Format(axis.Y),
                Format(component.LongLength), Format(component.ShortLength),
                Format(component.Elevation)
            });
        }

        private static ArrayVec2 CanonicalAxis(ArrayVec2 source)
        {
            if (source.Length <= 1e-12)
                return source;
            ArrayVec2 axis = source.Normalized();
            if (axis.X < -1e-9 ||
                (Math.Abs(axis.X) <= 1e-9 && axis.Y < 0.0))
                axis = axis * -1.0;
            return axis;
        }

        private static string Format(double value)
        {
            return Math.Round(value, 4).ToString("0.####",
                CultureInfo.InvariantCulture);
        }
    }
}
