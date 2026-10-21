using System;
using System.Globalization;

namespace StructuralToolbox.Ballast.Core
{
    public static class BallastPierIdentity
    {
        private const string CirclePrefix = "PZ_圆_D";
        private const string RectPrefix = "PZ_方_";

        public static bool TryParseToolBlockName(string blockName,
            out BallastStatisticItem item)
        {
            item = null;
            if (String.IsNullOrWhiteSpace(blockName))
                return false;

            string name = blockName.Trim();
            bool circle = name.StartsWith(CirclePrefix,
                StringComparison.OrdinalIgnoreCase);
            bool rectangle = name.StartsWith(RectPrefix,
                StringComparison.OrdinalIgnoreCase);
            if (!circle && !rectangle)
                return false;

            int valueStart = circle ? CirclePrefix.Length : RectPrefix.Length;
            int lengthMarker = name.IndexOf("_L", valueStart,
                StringComparison.OrdinalIgnoreCase);
            int heightMarker = lengthMarker < 0 ? -1 : name.IndexOf("_H",
                lengthMarker + 2, StringComparison.OrdinalIgnoreCase);
            int colorMarker = heightMarker < 0 ? -1 : name.IndexOf("_C",
                heightMarker + 2, StringComparison.OrdinalIgnoreCase);
            int layerMarker = colorMarker < 0 ? -1 : name.IndexOf("_Y",
                colorMarker + 2, StringComparison.OrdinalIgnoreCase);
            if (lengthMarker < 0 || heightMarker < 0 || colorMarker < 0 ||
                layerMarker < 0 || layerMarker + 2 >= name.Length)
                return false;

            double height;
            if (!TryParseNumber(name.Substring(heightMarker + 2,
                    colorMarker - heightMarker - 2), out height) || height <= 0.0)
                return false;

            string colorLabel;
            if (!TryParseColor(name.Substring(colorMarker + 2,
                    layerMarker - colorMarker - 2), out colorLabel))
                return false;

            BallastStatisticItem parsed = new BallastStatisticItem();
            parsed.Height = height;
            parsed.ColorLabel = colorLabel;
            parsed.Quantity = 1;
            if (circle)
            {
                double diameter;
                if (!TryParseNumber(name.Substring(CirclePrefix.Length,
                        lengthMarker - CirclePrefix.Length), out diameter) ||
                    diameter <= 0.0)
                    return false;
                parsed.Shape = BallastShape.Circle;
                parsed.Diameter = diameter;
            }
            else
            {
                string dimensions = name.Substring(RectPrefix.Length,
                    lengthMarker - RectPrefix.Length);
                int separator = dimensions.IndexOf('x');
                double first;
                double second;
                if (separator <= 0 || separator == dimensions.Length - 1 ||
                    !TryParseNumber(dimensions.Substring(0, separator), out first) ||
                    !TryParseNumber(dimensions.Substring(separator + 1), out second) ||
                    first <= 0.0 || second <= 0.0)
                    return false;
                parsed.Shape = BallastShape.Rect;
                parsed.RectLength = Math.Max(first, second);
                parsed.RectWidth = Math.Min(first, second);
            }
            item = parsed;
            return true;
        }

        private static bool TryParseNumber(string key, out double value)
        {
            value = 0.0;
            if (String.IsNullOrWhiteSpace(key))
                return false;
            string text = key.Replace("M", "-").Replace("m", "-")
                .Replace("P", ".").Replace("p", ".");
            return Double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) &&
                !Double.IsNaN(value) && !Double.IsInfinity(value);
        }

        private static bool TryParseColor(string key, out string label)
        {
            label = null;
            if (String.Equals(key, "BL", StringComparison.OrdinalIgnoreCase))
            {
                label = "默认";
                return true;
            }
            if (key.StartsWith("ACI", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!Int32.TryParse(key.Substring(3), NumberStyles.None,
                        CultureInfo.InvariantCulture, out index) ||
                    index < 1 || index > 255)
                    return false;
                label = "ACI " + index.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            if (!key.StartsWith("RGB", StringComparison.OrdinalIgnoreCase))
                return false;
            string[] channels = key.Substring(3).Split('_');
            if (channels.Length != 3)
                return false;
            int red;
            int green;
            int blue;
            if (!TryByte(channels[0], out red) ||
                !TryByte(channels[1], out green) ||
                !TryByte(channels[2], out blue))
                return false;
            label = String.Format(CultureInfo.InvariantCulture,
                "RGB {0},{1},{2}", red, green, blue);
            return true;
        }

        private static bool TryByte(string text, out int value)
        {
            return Int32.TryParse(text, NumberStyles.None,
                CultureInfo.InvariantCulture, out value) && value >= 0 && value <= 255;
        }
    }
}
