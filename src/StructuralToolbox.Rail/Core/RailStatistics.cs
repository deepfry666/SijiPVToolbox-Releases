using System;
using System.Collections.Generic;

namespace StructuralToolbox.Core
{
    public static class RailStatistics
    {
        public static double TotalLengthMillimeters(IEnumerable<double> lengths)
        {
            double total = 0.0;
            if (lengths == null)
                return total;
            foreach (double length in lengths)
            {
                if (!Double.IsNaN(length) && !Double.IsInfinity(length) &&
                    length > 0.0)
                    total += length;
            }
            return total;
        }

        public static double ToMeters(double millimeters)
        {
            if (Double.IsNaN(millimeters) || Double.IsInfinity(millimeters) ||
                millimeters <= 0.0)
                return 0.0;
            return millimeters / 1000.0;
        }
    }
}
