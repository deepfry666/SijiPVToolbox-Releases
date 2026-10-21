using System;
using System.Collections.Generic;

namespace StructuralToolbox.Update
{
    public static class VersionComparer
    {
        public static int Compare(Version left, Version right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int[] normalizedLeft = Normalize(left);
            int[] normalizedRight = Normalize(right);
            for (int index = 0; index < 4; index++)
            {
                if (normalizedLeft[index] != normalizedRight[index])
                    return normalizedLeft[index] < normalizedRight[index]
                        ? -1
                        : 1;
            }

            return 0;
        }

        public static bool IsNewerThan(Version candidate, Version current)
        {
            return Compare(candidate, current) > 0;
        }

        private static int[] Normalize(Version value)
        {
            return new[]
            {
                Math.Max(0, value.Major),
                Math.Max(0, value.Minor),
                Math.Max(0, value.Build),
                Math.Max(0, value.Revision)
            };
        }
    }
}
