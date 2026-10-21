using System;
using System.Collections.Generic;
using System.Globalization;
using StructuralToolbox.ArrayRecognition.Core;

namespace StructuralToolbox.Ballast.Core
{
    public static class BallastLayoutEngine
    {
        private const double AlignmentTolerance = 0.5;
        private const double SuspectAlignmentTolerance = 3.0;
        private const double NumericTolerance = 0.01;

        private sealed class SpecGroup
        {
            public readonly List<BallastPanel> Panels = new List<BallastPanel>();
            public BallastVec2 LongAxis;
            public BallastVec2 ShortAxis;
            public double LongLength;
            public double ShortLength;
            public double Elevation;
        }

        private sealed class Placement
        {
            public BallastPanel Panel;
            public double U;
            public double V;
        }

        private sealed class DisjointSet
        {
            private readonly int[] _parent;
            private readonly byte[] _rank;

            public DisjointSet(int count)
            {
                _parent = new int[count];
                _rank = new byte[count];
                for (int index = 0; index < count; index++)
                    _parent[index] = index;
            }

            public int Find(int value)
            {
                if (_parent[value] != value)
                    _parent[value] = Find(_parent[value]);
                return _parent[value];
            }

            public void Union(int first, int second)
            {
                int left = Find(first);
                int right = Find(second);
                if (left == right)
                    return;
                if (_rank[left] < _rank[right])
                    _parent[left] = right;
                else if (_rank[left] > _rank[right])
                    _parent[right] = left;
                else
                {
                    _parent[right] = left;
                    _rank[left]++;
                }
            }
        }

        public static BallastLayoutResult Create(IList<BallastPanel> panels,
            BallastLayoutOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            options.Normalize();

            BallastArrayDetectionResult detection = DetectArrays(panels, options);
            return Create(detection, options);
        }

        public static BallastLayoutResult Create(
            BallastArrayDetectionResult detection, BallastLayoutOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            options.Normalize();
            if (detection == null)
                detection = new BallastArrayDetectionResult
                {
                    Arrays = new List<BallastArrayInfo>(),
                    InvalidArrays = new List<BallastInvalidArray>()
                };
            BallastLayoutResult result = new BallastLayoutResult
            {
                Plans = new List<BallastArrayPlan>(),
                InvalidArrays = new List<BallastInvalidArray>(
                    detection.InvalidArrays ?? new List<BallastInvalidArray>())
            };

            for (int index = 0; index < detection.Arrays.Count; index++)
            {
                BallastArrayInfo array = detection.Arrays[index];
                string arrayId = "A" + (index + 1).ToString(
                    CultureInfo.InvariantCulture);
                array.Id = arrayId;
                BallastLayoutOptions arrayOptions = BuildArrayOptions(
                    options, arrayId, array.RowsAlongLong);
                BallastArrayPlan plan;
                string reason;
                if (!TryBuildPlan(array, arrayOptions, out plan, out reason))
                {
                    result.InvalidArrays.Add(new BallastInvalidArray
                    {
                        Reason = reason,
                        SourceIds = BuildSourceIds(array.Panels)
                    });
                    continue;
                }

                plan.ArrayId = arrayId;
                plan.Options = arrayOptions;
                result.Plans.Add(plan);
            }
            return result;
        }

        public static BallastArrayDetectionResult DetectArrays(
            IList<BallastPanel> panels, BallastLayoutOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            options.Normalize();

            BallastArrayDetectionResult result = new BallastArrayDetectionResult
            {
                Arrays = new List<BallastArrayInfo>(),
                InvalidArrays = new List<BallastInvalidArray>()
            };
            if (panels == null || panels.Count == 0)
                return result;

            List<ArrayComponent> components = new List<ArrayComponent>();
            for (int index = 0; index < panels.Count; index++)
            {
                BallastPanel panel = panels[index];
                if (panel == null)
                    continue;
                components.Add(new ArrayComponent
                {
                    SourceId = panel.SourceId,
                    Kind = ArrayComponentKind.LightweightPolyline,
                    Center = new ArrayVec2(panel.Center.X, panel.Center.Y),
                    LongAxis = new ArrayVec2(panel.LongAxis.X, panel.LongAxis.Y),
                    ShortAxis = new ArrayVec2(panel.ShortAxis.X, panel.ShortAxis.Y),
                    LongLength = panel.LongLength, ShortLength = panel.ShortLength,
                    Elevation = panel.Elevation
                });
            }
            ArrayRecognitionResult recognized = ArrayRecognitionEngine.Detect(
                components, new ArrayRecognitionOptions
                {
                    MaximumOutlineGap = options.MaxComponentGap
                });
            Dictionary<string, BallastPanel> byId =
                new Dictionary<string, BallastPanel>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < panels.Count; index++)
                if (panels[index] != null)
                    byId[panels[index].SourceId] = panels[index];
            for (int index = 0; index < recognized.Arrays.Count; index++)
            {
                RecognizedArray source = recognized.Arrays[index];
                BallastArrayInfo target = new BallastArrayInfo
                {
                    Id = source.Id, Panels = new List<BallastPanel>(),
                    Center = new BallastVec2(source.Center.X, source.Center.Y),
                    LongAxis = new BallastVec2(source.LongAxis.X, source.LongAxis.Y),
                    ShortAxis = new BallastVec2(source.ShortAxis.X, source.ShortAxis.Y),
                    LongSpan = source.LongSpan, ShortSpan = source.ShortSpan,
                    RowsAlongLong = source.CountAlongLong,
                    ColumnsAlongShort = source.CountAlongShort
                };
                for (int componentIndex = 0; componentIndex < source.Components.Count;
                    componentIndex++)
                {
                    BallastPanel panel;
                    if (byId.TryGetValue(source.Components[componentIndex].SourceId,
                        out panel))
                        target.Panels.Add(panel);
                }
                result.Arrays.Add(target);
            }
            for (int index = 0; index < recognized.InvalidGroups.Count; index++)
                result.InvalidArrays.Add(new BallastInvalidArray
                {
                    Reason = recognized.InvalidGroups[index].Reason,
                    SourceIds = String.Join(",",
                        recognized.InvalidGroups[index].SourceIds.ToArray())
                });
            return result;
        }

        private static List<SpecGroup> BuildSpecGroups(IList<BallastPanel> panels)
        {
            List<SpecGroup> groups = new List<SpecGroup>();
            for (int panelIndex = 0; panelIndex < panels.Count; panelIndex++)
            {
                BallastPanel panel = panels[panelIndex];
                if (panel == null)
                    continue;
                SpecGroup matched = null;
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    SpecGroup candidate = groups[groupIndex];
                    if (Math.Abs(candidate.LongLength - panel.LongLength) > 0.5 ||
                        Math.Abs(candidate.ShortLength - panel.ShortLength) > 0.5 ||
                        Math.Abs(candidate.Elevation - panel.Elevation) > 0.01 ||
                        Math.Abs(BallastVec2.Dot(candidate.LongAxis,
                            panel.LongAxis)) < Math.Cos(0.05 * Math.PI / 180.0))
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
                        LongAxis = panel.LongAxis,
                        ShortAxis = panel.ShortAxis,
                        LongLength = panel.LongLength,
                        ShortLength = panel.ShortLength,
                        Elevation = panel.Elevation
                    };
                    groups.Add(matched);
                }
                matched.Panels.Add(panel);
            }
            return groups;
        }

        private static void BuildConnectedArrays(SpecGroup group,
            BallastLayoutOptions options, ICollection<BallastArrayInfo> arrays,
            ICollection<BallastInvalidArray> invalidArrays)
        {
            List<Placement> placements = new List<Placement>();
            for (int index = 0; index < group.Panels.Count; index++)
            {
                BallastPanel panel = group.Panels[index];
                placements.Add(new Placement
                {
                    Panel = panel,
                    U = BallastVec2.Dot(panel.Center, group.LongAxis),
                    V = BallastVec2.Dot(panel.Center, group.ShortAxis)
                });
            }

            DisjointSet sets = new DisjointSet(placements.Count);
            for (int firstIndex = 0; firstIndex < placements.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1;
                    secondIndex < placements.Count; secondIndex++)
                {
                    if (AreAdjacent(placements[firstIndex], placements[secondIndex],
                        group, options.MaxComponentGap))
                    {
                        sets.Union(firstIndex, secondIndex);
                    }
                }
            }

            Dictionary<int, List<Placement>> components =
                new Dictionary<int, List<Placement>>();
            for (int index = 0; index < placements.Count; index++)
            {
                int root = sets.Find(index);
                List<Placement> component;
                if (!components.TryGetValue(root, out component))
                {
                    component = new List<Placement>();
                    components[root] = component;
                }
                component.Add(placements[index]);
            }

            foreach (KeyValuePair<int, List<Placement>> entry in components)
            {
                BallastArrayInfo array;
                string reason;
                if (TryBuildArray(entry.Value, group, options, out array, out reason))
                    arrays.Add(array);
                else
                    invalidArrays.Add(new BallastInvalidArray
                    {
                        Reason = reason,
                        SourceIds = BuildSourceIds(entry.Value)
                    });
            }
        }

        private static bool AreAdjacent(Placement first, Placement second,
            SpecGroup group, double maximumGap)
        {
            double deltaU = Math.Abs(first.U - second.U);
            double deltaV = Math.Abs(first.V - second.V);
            if (deltaU <= SuspectAlignmentTolerance &&
                deltaV <= SuspectAlignmentTolerance)
                return true;

            if (deltaV <= SuspectAlignmentTolerance)
                return deltaU - group.LongLength <= maximumGap + NumericTolerance;
            if (deltaU <= SuspectAlignmentTolerance)
                return deltaV - group.ShortLength <= maximumGap + NumericTolerance;
            return false;
        }

        private static bool TryBuildArray(IList<Placement> placements,
            SpecGroup group, BallastLayoutOptions options,
            out BallastArrayInfo array, out string reason)
        {
            array = null;
            reason = String.Empty;
            List<double> uBands = BuildBands(placements, true);
            List<double> vBands = BuildBands(placements, false);
            bool[,] occupied = new bool[uBands.Count, vBands.Count];

            for (int index = 0; index < placements.Count; index++)
            {
                Placement placement = placements[index];
                int uIndex = FindBand(uBands, placement.U);
                int vIndex = FindBand(vBands, placement.V);
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

            if (!ValidateBandGaps(uBands, group.LongLength,
                    options.MaxComponentGap, out reason) ||
                !ValidateBandGaps(vBands, group.ShortLength,
                    options.MaxComponentGap, out reason))
            {
                return false;
            }

            double minimumU = uBands[0] - group.LongLength / 2.0;
            double maximumU = uBands[uBands.Count - 1] + group.LongLength / 2.0;
            double minimumV = vBands[0] - group.ShortLength / 2.0;
            double maximumV = vBands[vBands.Count - 1] + group.ShortLength / 2.0;
            array = new BallastArrayInfo
            {
                Id = "pending",
                Panels = new List<BallastPanel>(),
                LongAxis = group.LongAxis,
                ShortAxis = group.ShortAxis,
                Center = group.LongAxis * ((minimumU + maximumU) / 2.0) +
                    group.ShortAxis * ((minimumV + maximumV) / 2.0),
                LongSpan = maximumU - minimumU,
                ShortSpan = maximumV - minimumV,
                RowsAlongLong = uBands.Count,
                ColumnsAlongShort = vBands.Count
            };
            for (int index = 0; index < placements.Count; index++)
                array.Panels.Add(placements[index].Panel);
            return true;
        }

        private static List<double> BuildBands(IList<Placement> placements,
            bool useU)
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
                    values[end] - minimum <= AlignmentTolerance + NumericTolerance)
                {
                    sum += values[end];
                    end++;
                }
                bands.Add(sum / (end - start));
                start = end;
            }
            return bands;
        }

        private static int FindBand(IList<double> bands, double value)
        {
            for (int index = 0; index < bands.Count; index++)
                if (Math.Abs(bands[index] - value) <=
                    AlignmentTolerance + NumericTolerance)
                    return index;
            return -1;
        }

        private static bool ValidateBandGaps(IList<double> bands,
            double panelLength, double maximumGap, out string reason)
        {
            reason = String.Empty;
            for (int index = 1; index < bands.Count; index++)
            {
                double gap = bands[index] - bands[index - 1] - panelLength;
                if (gap < -AlignmentTolerance)
                {
                    reason = "阵列内组件发生重叠，阵列已跳过";
                    return false;
                }
                if (gap > maximumGap + NumericTolerance)
                {
                    reason = String.Format(CultureInfo.InvariantCulture,
                        "阵列内组件净距 {0:0.##} mm 超过断开值 {1:0.##} mm，阵列已拆分",
                        gap, maximumGap);
                    return false;
                }
            }
            return true;
        }

        private static bool TryBuildPlan(BallastArrayInfo array,
            BallastLayoutOptions options, out BallastArrayPlan plan,
            out string reason)
        {
            plan = null;
            reason = String.Empty;
            int ballastCount = options.CountMode == BallastCountMode.Manual
                ? options.ManualCount
                : array.RowsAlongLong + 1;
            double ballastSpacing = options.BallastCenterSpacing;
            double frameSpacing = options.FrameCenterSpacing;
            double extentLong = options.ExtentAlongLong();
            double extentShort = options.ExtentAlongShort();

            if (ballastCount > 1 && ballastSpacing < extentLong - NumericTolerance)
            {
                reason = "单榀配重中心距小于配重外轮廓尺寸，阵列已跳过";
                return false;
            }
            double ballastSpan = (ballastCount - 1.0) * ballastSpacing;
            double frontCenterClearance = options.PlacementMode ==
                BallastPlacementMode.CustomFront
                    ? options.FrontCenterClearance
                    : (array.LongSpan - ballastSpan) / 2.0;
            double rearCenterClearance = array.LongSpan -
                frontCenterClearance - ballastSpan;
            double minimumCenterClearance = extentLong / 2.0;
            if (frontCenterClearance < minimumCenterClearance - NumericTolerance ||
                rearCenterClearance < minimumCenterClearance - NumericTolerance)
            {
                reason = "前后边距与配重间距互相矛盾，配重外轮廓将超出组件边缘";
                return false;
            }
            List<double> frameOffsets;
            double sideClearance;
            if (!TryCreateFrameOffsets(array.ShortSpan, extentShort,
                    frameSpacing, options.MaxSideClearance, out frameOffsets,
                    out sideClearance, out reason))
            {
                return false;
            }

            plan = new BallastArrayPlan
            {
                Array = array,
                Frames = new List<BallastFramePlan>()
            };
            for (int frameIndex = 0; frameIndex < frameOffsets.Count; frameIndex++)
            {
                BallastVec2 frameCenter = array.Center +
                    array.ShortAxis * frameOffsets[frameIndex];
                BallastFramePlan frame = new BallastFramePlan
                {
                    Center = frameCenter,
                    Ballasts = new List<BallastPointPlan>(),
                    Conflict = false
                };
                double firstBallast = -array.LongSpan / 2.0 +
                    frontCenterClearance;
                for (int ballastIndex = 0; ballastIndex < ballastCount;
                    ballastIndex++)
                {
                    BallastVec2 point = frameCenter + array.LongAxis *
                        (firstBallast + ballastIndex * ballastSpacing);
                    frame.Ballasts.Add(new BallastPointPlan
                    {
                        Center = point,
                        Conflict = false,
                        ConflictReason = String.Empty
                    });
                }
                plan.Frames.Add(frame);
            }
            return true;
        }

        private static BallastLayoutOptions BuildArrayOptions(
            BallastLayoutOptions source, string arrayId, int rowsAlongLong)
        {
            BallastLayoutOptions result = new BallastLayoutOptions();
            result.Shape = source.Shape;
            result.Diameter = source.Diameter;
            result.RectLength = source.RectLength;
            result.RectWidth = source.RectWidth;
            result.Height = source.Height;
            result.CrossLineLength = source.CrossLineLength;
            result.FrameCenterSpacing = source.FrameCenterSpacing;
            result.BallastCenterSpacing = source.BallastCenterSpacing;
            result.MaxComponentGap = source.MaxComponentGap;
            result.MaxSideClearance = source.MaxSideClearance;
            result.AllowOverrun = 0.0;
            result.PlacementMode = source.PlacementMode;
            result.FrontCenterClearance = source.FrontCenterClearance;
            result.ColorMode = source.ColorMode;
            result.ColorIndex = source.ColorIndex;
            result.ColorRed = source.ColorRed;
            result.ColorGreen = source.ColorGreen;
            result.ColorBlue = source.ColorBlue;
            result.CountMode = source.CountMode;
            result.ManualCount = source.ManualCount;
            result.LayerName = source.LayerName;
            result.RowRules = new List<BallastRowRule>();
            BallastRowRule rowRule = source.FindRule(rowsAlongLong);
            if (rowRule != null)
            {
                result.Shape = rowRule.Shape;
                result.Diameter = rowRule.Diameter;
                result.RectLength = rowRule.RectLength;
                result.RectWidth = rowRule.RectWidth;
                result.Height = rowRule.Height;
                result.CrossLineLength = rowRule.CrossLineLength;
                result.CountMode = rowRule.CountMode;
                result.ManualCount = rowRule.ManualCount;
                result.BallastCenterSpacing = rowRule.BallastCenterSpacing;
                result.FrameCenterSpacing = rowRule.FrameCenterSpacing;
                result.PlacementMode = rowRule.PlacementMode;
                result.FrontCenterClearance = rowRule.FrontCenterClearance;
                result.ColorMode = rowRule.ColorMode;
                result.ColorIndex = rowRule.ColorIndex;
                result.ColorRed = rowRule.ColorRed;
                result.ColorGreen = rowRule.ColorGreen;
                result.ColorBlue = rowRule.ColorBlue;
            }
            BallastArrayRule arrayRule = source.FindArrayRule(arrayId);
            if (arrayRule != null)
            {
                result.Shape = arrayRule.Shape;
                result.Diameter = arrayRule.Diameter;
                result.RectLength = arrayRule.RectLength;
                result.RectWidth = arrayRule.RectWidth;
                result.Height = arrayRule.Height;
                result.CrossLineLength = arrayRule.CrossLineLength;
                result.CountMode = arrayRule.CountMode;
                result.ManualCount = arrayRule.ManualCount;
                result.BallastCenterSpacing = arrayRule.BallastCenterSpacing;
                result.FrameCenterSpacing = arrayRule.FrameCenterSpacing;
                result.PlacementMode = arrayRule.PlacementMode;
                result.FrontCenterClearance = arrayRule.FrontCenterClearance;
                result.ColorMode = arrayRule.ColorMode;
                result.ColorIndex = arrayRule.ColorIndex;
                result.ColorRed = arrayRule.ColorRed;
                result.ColorGreen = arrayRule.ColorGreen;
                result.ColorBlue = arrayRule.ColorBlue;
            }
            result.ArrayRules = new List<BallastArrayRule>();
            result.Normalize();
            return result;
        }

        private static bool TryCreateFrameOffsets(double arraySpan,
            double ballastExtent, double standardSpacing,
            double maximumSideClearance,
            out List<double> offsets, out double sideClearance,
            out string reason)
        {
            offsets = null;
            sideClearance = Double.NaN;
            reason = String.Empty;
            if (arraySpan < ballastExtent - NumericTolerance)
            {
                reason = "配重外轮廓大于组件阵列宽度，无法布置";
                return false;
            }

            double insideCenterSpan = Math.Max(0.0, arraySpan - ballastExtent);
            if (TryCreateOffsetsWithinLimit(arraySpan, ballastExtent,
                    standardSpacing, maximumSideClearance,
                    insideCenterSpan, out offsets, out sideClearance))
            {
                return true;
            }

            reason = "无法同时满足榀最大中心距、两侧中心限距和配重不越界要求";
            return false;
        }

        private static bool TryCreateOffsetsWithinLimit(double arraySpan,
            double ballastExtent, double spacing, double maximumSideClearance,
            double centerSpanLimit, out List<double> offsets,
            out double sideClearance)
        {
            offsets = null;
            sideClearance = Double.NaN;
            if (centerSpanLimit < -NumericTolerance)
                return false;

            for (int count = 1; count <= 500; count++)
            {
                List<double> candidate = new List<double>();
                double usedSpan;
                if (count == 1)
                {
                    usedSpan = 0.0;
                    candidate.Add(0.0);
                }
                else
                {
                    double desiredSpan = (count - 1.0) * spacing;
                    if (desiredSpan <= centerSpanLimit + NumericTolerance)
                    {
                        usedSpan = desiredSpan;
                        double first = -usedSpan / 2.0;
                        for (int index = 0; index < count; index++)
                            candidate.Add(first + index * spacing);
                    }
                    else
                    {
                        usedSpan = centerSpanLimit;
                        if (count == 2)
                        {
                            if (usedSpan < ballastExtent - NumericTolerance ||
                                usedSpan > spacing + NumericTolerance)
                                continue;
                            candidate.Add(-usedSpan / 2.0);
                            candidate.Add(usedSpan / 2.0);
                        }
                        else
                        {
                            double middleSpan = (count - 3.0) * spacing;
                            double edgeGap = (usedSpan - middleSpan) / 2.0;
                            if (edgeGap < ballastExtent - NumericTolerance ||
                                edgeGap > spacing + NumericTolerance)
                                continue;
                            double position = -usedSpan / 2.0;
                            candidate.Add(position);
                            position += edgeGap;
                            candidate.Add(position);
                            for (int index = 2; index < count - 1; index++)
                            {
                                position += spacing;
                                candidate.Add(position);
                            }
                            candidate.Add(usedSpan / 2.0);
                        }
                    }
                }

                double clearance = (arraySpan - usedSpan) / 2.0;
                if (clearance <= maximumSideClearance + NumericTolerance)
                {
                    offsets = candidate;
                    sideClearance = clearance;
                    return true;
                }
            }
            return false;
        }

        private static int CompareArrays(BallastArrayInfo left,
            BallastArrayInfo right)
        {
            int comparison = left.Center.X.CompareTo(right.Center.X);
            if (comparison != 0)
                return comparison;
            return left.Center.Y.CompareTo(right.Center.Y);
        }

        private static string BuildSourceIds(IEnumerable<Placement> placements)
        {
            List<BallastPanel> panels = new List<BallastPanel>();
            foreach (Placement placement in placements)
                panels.Add(placement.Panel);
            return BuildSourceIds(panels);
        }

        private static string BuildSourceIds(IEnumerable<BallastPanel> panels)
        {
            List<string> values = new List<string>();
            if (panels != null)
                foreach (BallastPanel panel in panels)
                    if (panel != null && !String.IsNullOrWhiteSpace(panel.SourceId))
                        values.Add(panel.SourceId);
            values.Sort(StringComparer.Ordinal);
            return String.Join(",", values.ToArray());
        }
    }
}
