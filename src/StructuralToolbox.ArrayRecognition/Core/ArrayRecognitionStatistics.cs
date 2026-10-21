using System;
using System.Collections.Generic;

namespace StructuralToolbox.ArrayRecognition.Core
{
    public static class ArrayRecognitionStatistics
    {
        public static ArrayRecognitionSummary Summarize(
            ArrayRecognitionResult result)
        {
            ArrayRecognitionSummary summary = new ArrayRecognitionSummary();
            if (result == null)
                return summary;
            Dictionary<string, ArraySummaryEntry> entries =
                new Dictionary<string, ArraySummaryEntry>(StringComparer.Ordinal);
            for (int index = 0; index < result.Arrays.Count; index++)
            {
                RecognizedArray array = result.Arrays[index];
                if (array == null || array.CountAlongLong <= 0 ||
                    array.CountAlongShort <= 0)
                    continue;
                string key = array.CountAlongLong + ":" + array.CountAlongShort;
                ArraySummaryEntry entry;
                if (!entries.TryGetValue(key, out entry))
                {
                    entry = new ArraySummaryEntry
                    {
                        CountAlongLong = array.CountAlongLong,
                        CountAlongShort = array.CountAlongShort
                    };
                    entries.Add(key, entry);
                    summary.Entries.Add(entry);
                }
                entry.ArrayCount++;
                summary.RecognizedArrayCount++;
                summary.RecognizedComponentCount += array.ComponentCount;
            }
            summary.InvalidArrayCount = result.InvalidGroups.Count;
            summary.InvalidComponentCount = result.InvalidComponentCount;
            summary.Entries.Sort(CompareEntries);
            return summary;
        }

        private static int CompareEntries(ArraySummaryEntry left,
            ArraySummaryEntry right)
        {
            int rows = left.CountAlongLong.CompareTo(right.CountAlongLong);
            return rows != 0 ? rows :
                left.CountAlongShort.CompareTo(right.CountAlongShort);
        }
    }
}
