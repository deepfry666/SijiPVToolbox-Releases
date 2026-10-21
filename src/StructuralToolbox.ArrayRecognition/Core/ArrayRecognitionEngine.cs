using System;
using System.Collections.Generic;
using System.Globalization;

namespace StructuralToolbox.ArrayRecognition.Core
{
    public static class ArrayRecognitionEngine
    {
        private const double NumericTolerance = 0.01;

        private sealed class SpecGroup
        {
            public readonly List<ArrayComponent> Components =
                new List<ArrayComponent>();
            public ArrayVec2 LongAxis;
            public ArrayVec2 ShortAxis;
            public double LongLength;
            public double ShortLength;
        }

        private sealed class Placement
        {
            public ArrayComponent Component;
            public double U;
            public double V;
        }

        private sealed class DisjointSet
        {
            private readonly int[] _parents;
            private readonly byte[] _ranks;

            public DisjointSet(int count)
            {
                _parents = new int[count];
                _ranks = new byte[count];
                for (int index = 0; index < count; index++)
                    _parents[index] = index;
            }

            public int Find(int value)
            {
                if (_parents[value] != value)
                    _parents[value] = Find(_parents[value]);
                return _parents[value];
            }

            public void Union(int first, int second)
            {
                int left = Find(first);
                int right = Find(second);
                if (left == right)
                    return;
                if (_ranks[left] < _ranks[right])
                    _parents[left] = right;
                else if (_ranks[left] > _ranks[right])
                    _parents[right] = left;
                else
                {
                    _parents[right] = left;
                    _ranks[left]++;
                }
            }
        }

        public static ArrayRecognitionResult Detect(
            IList<ArrayComponent> components, ArrayRecognitionOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            options.Normalize();
            ArrayRecognitionResult result = new ArrayRecognitionResult();
            if (components == null || components.Count == 0)
                return result;

            List<ArrayComponent> unique = FilterAndReport(components, result);
            List<SpecGroup> groups = BuildSpecGroups(unique, options);
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                BuildConnectedArrays(groups[groupIndex], options, result);
            result.Arrays.Sort(CompareArrays);
            for (int index = 0; index < result.Arrays.Count; index++)
                result.Arrays[index].Id = "A" + (index + 1).ToString(
                    CultureInfo.InvariantCulture);
            return result;
        }

        private static List<ArrayComponent> FilterAndReport(
            IList<ArrayComponent> components, ArrayRecognitionResult result)
        {
            List<ArrayComponent> unique = new List<ArrayComponent>();
            HashSet<string> sourceIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < components.Count; index++)
            {
                ArrayComponent component = components[index];
                if (!IsUsable(component))
                {
                    InvalidArrayGroup invalid = new InvalidArrayGroup
                    {
                        Reason = "组件几何无效：尺寸或长轴方向不可用"
                    };
                    invalid.SourceIds.Add(component == null ||
                        String.IsNullOrWhiteSpace(component.SourceId)
                        ? "(无句柄)" : component.SourceId);
                    result.InvalidGroups.Add(invalid);
                    continue;
                }
                string sourceId = component.SourceId == null
                    ? String.Empty : component.SourceId.Trim();
                if (sourceId.Length > 0 && !sourceIds.Add(sourceId))
                    continue;
                unique.Add(component);
            }
            return unique;
        }

        private static List<SpecGroup> BuildSpecGroups(
            IList<ArrayComponent> components, ArrayRecognitionOptions options)
        {
            List<SpecGroup> groups = new List<SpecGroup>();
            double minimumDirectionDot = Math.Cos(options.DirectionToleranceDegrees *
                Math.PI / 180.0);
            for (int componentIndex = 0; componentIndex < components.Count;
                componentIndex++)
            {
                ArrayComponent component = components[componentIndex];
                if (!IsUsable(component))
                    continue;
                ArrayVec2 longAxis;
                ArrayVec2 shortAxis;
                NormalizeAxes(component.LongAxis, out longAxis, out shortAxis);
                component.LongAxis = longAxis;
                component.ShortAxis = shortAxis;

                SpecGroup matched = null;
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    SpecGroup candidate = groups[groupIndex];
                    if (Math.Abs(candidate.LongLength - component.LongLength) >
                            options.DimensionTolerance ||
                        Math.Abs(candidate.ShortLength - component.ShortLength) >
                            options.DimensionTolerance ||
                        Math.Abs(ArrayVec2.Dot(candidate.LongAxis, longAxis)) <
                            minimumDirectionDot)
                    {
                        continue;
                    }
                    matched = candidate;
                    break;
                }

                if (matched == null)
                {
                    matched = new SpecGroup
                    {
                        LongAxis = longAxis,
                        ShortAxis = shortAxis,
                        LongLength = component.LongLength,
                        ShortLength = component.ShortLength
                    };
                    groups.Add(matched);
                }
                matched.Components.Add(component);
            }
            return groups;
        }

        private static bool IsUsable(ArrayComponent component)
        {
            return component != null && component.LongLength > NumericTolerance &&
                component.ShortLength > NumericTolerance &&
                component.LongAxis.Length > NumericTolerance;
        }

        private static void NormalizeAxes(ArrayVec2 source,
            out ArrayVec2 longAxis, out ArrayVec2 shortAxis)
        {
            ArrayVec2 normalized = source.Normalized();
            double angle = Math.Atan2(normalized.Y, normalized.X);
            while (angle < 0.0)
                angle += Math.PI;
            while (angle >= Math.PI)
                angle -= Math.PI;
            longAxis = new ArrayVec2(Math.Cos(angle), Math.Sin(angle));
            shortAxis = new ArrayVec2(-longAxis.Y, longAxis.X);
        }

        private static void BuildConnectedArrays(SpecGroup group,
            ArrayRecognitionOptions options, ArrayRecognitionResult result)
        {
            List<Placement> placements = new List<Placement>();
            for (int index = 0; index < group.Components.Count; index++)
            {
                ArrayComponent component = group.Components[index];
                placements.Add(new Placement
                {
                    Component = component,
                    U = ArrayVec2.Dot(component.Center, group.LongAxis),
                    V = ArrayVec2.Dot(component.Center, group.ShortAxis)
                });
            }

            DisjointSet sets = new DisjointSet(placements.Count);
            for (int firstIndex = 0; firstIndex < placements.Count; firstIndex++)
                for (int secondIndex = firstIndex + 1;
                    secondIndex < placements.Count; secondIndex++)
                    if (AreAdjacent(placements[firstIndex], placements[secondIndex],
                        group, options))
                        sets.Union(firstIndex, secondIndex);

            Dictionary<int, List<Placement>> connected =
                new Dictionary<int, List<Placement>>();
            for (int index = 0; index < placements.Count; index++)
            {
                int root = sets.Find(index);
                List<Placement> groupPlacements;
                if (!connected.TryGetValue(root, out groupPlacements))
                {
                    groupPlacements = new List<Placement>();
                    connected.Add(root, groupPlacements);
                }
                groupPlacements.Add(placements[index]);
            }

            foreach (KeyValuePair<int, List<Placement>> entry in connected)
            {
                RecognizedArray array;
                string reason;
                if (TryBuildArray(entry.Value, group, options, out array, out reason))
                    result.Arrays.Add(array);
                else
                {
                    InvalidArrayGroup invalid = new InvalidArrayGroup();
                    invalid.Reason = reason;
                    for (int index = 0; index < entry.Value.Count; index++)
                    {
                        string id = entry.Value[index].Component.SourceId;
                        invalid.SourceIds.Add(String.IsNullOrWhiteSpace(id)
                            ? "(无句柄)" : id);
                    }
                    invalid.SourceIds.Sort(StringComparer.Ordinal);
                    result.InvalidGroups.Add(invalid);
                }
            }
        }

        private static bool AreAdjacent(Placement first, Placement second,
            SpecGroup group, ArrayRecognitionOptions options)
        {
            double deltaU = Math.Abs(first.U - second.U);
            double deltaV = Math.Abs(first.V - second.V);
            if (deltaU <= options.SuspectAlignmentTolerance + NumericTolerance &&
                deltaV <= options.SuspectAlignmentTolerance + NumericTolerance)
                return true;
            if (deltaV <= options.SuspectAlignmentTolerance + NumericTolerance)
                return deltaU - group.LongLength <=
                    options.MaximumOutlineGap + NumericTolerance;
            if (deltaU <= options.SuspectAlignmentTolerance + NumericTolerance)
                return deltaV - group.ShortLength <=
                    options.MaximumOutlineGap + NumericTolerance;
            return false;
        }

        private static bool TryBuildArray(IList<Placement> placements,
            SpecGroup group, ArrayRecognitionOptions options,
            out RecognizedArray array, out string reason)
        {
            array = null;
            reason = String.Empty;
            double minimumElevation = placements[0].Component.Elevation;
            double maximumElevation = minimumElevation;
            for (int index = 1; index < placements.Count; index++)
            {
                minimumElevation = Math.Min(minimumElevation,
                    placements[index].Component.Elevation);
                maximumElevation = Math.Max(maximumElevation,
                    placements[index].Component.Elevation);
            }
            if (maximumElevation - minimumElevation >
                options.ElevationTolerance + NumericTolerance)
            {
                reason = "同一阵列包含混合标高，阵列已跳过";
                return false;
            }
            List<double> uBands = BuildBands(placements, true,
                options.AlignmentTolerance);
            List<double> vBands = BuildBands(placements, false,
                options.AlignmentTolerance);
            bool[,] occupied = new bool[uBands.Count, vBands.Count];
            for (int index = 0; index < placements.Count; index++)
            {
                Placement placement = placements[index];
                int uIndex = FindBand(uBands, placement.U,
                    options.AlignmentTolerance);
                int vIndex = FindBand(vBands, placement.V,
                    options.AlignmentTolerance);
                if (uIndex < 0 || vIndex < 0)
                {
                    reason = "组件中心未能对齐到规则行列，阵列已跳过";
                    return false;
                }
                if (occupied[uIndex, vIndex])
                {
                    reason = "同一阵列位置存在重叠或重复组件，阵列已跳过";
                    return false;
                }
                occupied[uIndex, vIndex] = true;
            }

            if (uBands.Count * vBands.Count != placements.Count)
            {
                reason = "阵列存在缺块、错位或各排组件数量不一致，阵列已跳过";
                return false;
            }
            for (int u = 0; u < uBands.Count; u++)
                for (int v = 0; v < vBands.Count; v++)
                    if (!occupied[u, v])
                    {
                        reason = "阵列存在缺块，阵列已跳过";
                        return false;
                    }

            if (!ValidateBandGaps(uBands, group.LongLength, options, out reason) ||
                !ValidateBandGaps(vBands, group.ShortLength, options, out reason))
                return false;

            double minimumU = uBands[0] - group.LongLength / 2.0;
            double maximumU = uBands[uBands.Count - 1] + group.LongLength / 2.0;
            double minimumV = vBands[0] - group.ShortLength / 2.0;
            double maximumV = vBands[vBands.Count - 1] + group.ShortLength / 2.0;
            array = new RecognizedArray
            {
                Id = "pending",
                LongAxis = group.LongAxis,
                ShortAxis = group.ShortAxis,
                Center = group.LongAxis * ((minimumU + maximumU) / 2.0) +
                    group.ShortAxis * ((minimumV + maximumV) / 2.0),
                LongSpan = maximumU - minimumU,
                ShortSpan = maximumV - minimumV,
                CountAlongLong = uBands.Count,
                CountAlongShort = vBands.Count
            };
            for (int index = 0; index < placements.Count; index++)
                array.Components.Add(placements[index].Component);
            return true;
        }

        private static List<double> BuildBands(IList<Placement> placements,
            bool useU, double tolerance)
        {
            List<double> values = new List<double>();
            for (int index = 0; index < placements.Count; index++)
                values.Add(useU ? placements[index].U : placements[index].V);
            values.Sort();
            List<double> bands = new List<double>();
            int start = 0;
            while (start < values.Count)
            {
                double minimum = values[start];
                double sum = 0.0;
                int end = start;
                while (end < values.Count &&
                    values[end] - minimum <= tolerance + NumericTolerance)
                {
                    sum += values[end];
                    end++;
                }
                bands.Add(sum / (end - start));
                start = end;
            }
            return bands;
        }

        private static int FindBand(IList<double> bands, double value,
            double tolerance)
        {
            for (int index = 0; index < bands.Count; index++)
                if (Math.Abs(bands[index] - value) <=
                    tolerance + NumericTolerance)
                    return index;
            return -1;
        }

        private static bool ValidateBandGaps(IList<double> bands,
            double componentLength, ArrayRecognitionOptions options,
            out string reason)
        {
            reason = String.Empty;
            for (int index = 1; index < bands.Count; index++)
            {
                double gap = bands[index] - bands[index - 1] - componentLength;
                if (gap < -options.AlignmentTolerance)
                {
                    reason = "阵列内组件发生重叠，阵列已跳过";
                    return false;
                }
                if (gap > options.MaximumOutlineGap + NumericTolerance)
                {
                    reason = String.Format(CultureInfo.InvariantCulture,
                        "阵列内组件净距 {0:0.##} mm 超过断开值 G={1:0.##} mm，阵列已拆分",
                        gap, options.MaximumOutlineGap);
                    return false;
                }
            }
            return true;
        }

        private static int CompareArrays(RecognizedArray left,
            RecognizedArray right)
        {
            int x = left.Center.X.CompareTo(right.Center.X);
            if (x != 0)
                return x;
            int y = left.Center.Y.CompareTo(right.Center.Y);
            if (y != 0)
                return y;
            int rows = left.CountAlongLong.CompareTo(right.CountAlongLong);
            return rows != 0 ? rows :
                left.CountAlongShort.CompareTo(right.CountAlongShort);
        }
    }
}
