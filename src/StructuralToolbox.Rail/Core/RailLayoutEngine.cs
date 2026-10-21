using System;
using System.Collections.Generic;

namespace StructuralToolbox.Core
{
    public sealed class RailLayoutOptions
    {
        public RailLayoutOptions()
        {
            RailCount = 2;
            TotalSpacing = 1400.0;
            MaximumContinuityGap = 20.0;
            EndOverhang = 100.0;
            MinimumBreakClearance = 1.0;
            LineAlignmentTolerance = 2.0;
            ElevationTolerance = 1.0;
            AngleToleranceDegrees = 0.05;
            NumericTolerance = 0.000001;
        }

        public int RailCount { get; set; }
        public double TotalSpacing { get; set; }
        public double MaximumContinuityGap { get; set; }
        public double EndOverhang { get; set; }
        public double MinimumBreakClearance { get; set; }
        public double LineAlignmentTolerance { get; set; }
        public double ElevationTolerance { get; set; }
        public double AngleToleranceDegrees { get; set; }
        public double NumericTolerance { get; set; }

        public void Validate()
        {
            if (RailCount < 1)
                throw new ArgumentOutOfRangeException("RailCount", "Rail count must be at least one.");
            if (!IsFinite(TotalSpacing) || TotalSpacing < 0.0)
                throw new ArgumentOutOfRangeException("TotalSpacing");
            if (RailCount > 1 && TotalSpacing <= NumericTolerance)
                throw new ArgumentOutOfRangeException("TotalSpacing", "Multiple rails require a positive spacing.");
            if (!IsFinite(MaximumContinuityGap) || MaximumContinuityGap < 0.0)
                throw new ArgumentOutOfRangeException("MaximumContinuityGap");
            if (!IsFinite(EndOverhang) || EndOverhang < 0.0)
                throw new ArgumentOutOfRangeException("EndOverhang");
            if (!IsFinite(MinimumBreakClearance) || MinimumBreakClearance < 0.0)
                throw new ArgumentOutOfRangeException("MinimumBreakClearance");
            if (!IsFinite(LineAlignmentTolerance) || LineAlignmentTolerance <= 0.0)
                throw new ArgumentOutOfRangeException("LineAlignmentTolerance");
            if (!IsFinite(ElevationTolerance) || ElevationTolerance <= 0.0)
                throw new ArgumentOutOfRangeException("ElevationTolerance");
            if (!IsFinite(AngleToleranceDegrees) ||
                AngleToleranceDegrees <= 0.0 || AngleToleranceDegrees >= 10.0)
                throw new ArgumentOutOfRangeException("AngleToleranceDegrees");
            if (!IsFinite(NumericTolerance) || NumericTolerance <= 0.0)
                throw new ArgumentOutOfRangeException("NumericTolerance");
        }

        private static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }

    public sealed class RailLayoutResult
    {
        internal RailLayoutResult(IList<RailSegment> segments, int panelGroupCount,
            IList<RailDirectionGroupInfo> directionGroups,
            IList<RailArrayIslandInfo> arrayIslands,
            IList<RailComponentRowInfo> componentRows,
            IList<RailSegmentMetadata> segmentMetadata,
            IList<RailBreakInfo> breaks)
        {
            Segments = segments;
            PanelGroupCount = panelGroupCount;
            DirectionGroups = directionGroups;
            ArrayIslands = arrayIslands;
            ComponentRows = componentRows;
            SegmentMetadata = segmentMetadata;
            Breaks = breaks;
        }

        public IList<RailSegment> Segments { get; private set; }
        public int PanelGroupCount { get; private set; }
        public IList<RailDirectionGroupInfo> DirectionGroups { get; private set; }
        public IList<RailArrayIslandInfo> ArrayIslands { get; private set; }
        public IList<RailComponentRowInfo> ComponentRows { get; private set; }
        public IList<RailSegmentMetadata> SegmentMetadata { get; private set; }
        public IList<RailBreakInfo> Breaks { get; private set; }
    }

    public sealed class RailDirectionGroupInfo
    {
        internal RailDirectionGroupInfo(string id, Vec2 panelLongAxis, Vec2 railDirection,
            double panelLongLength, double panelShortLength, double elevation,
            IList<string> sourcePanelIds, IList<string> islandIds)
        {
            Id = id;
            PanelLongAxis = panelLongAxis;
            RailDirection = railDirection;
            PanelLongLength = panelLongLength;
            PanelShortLength = panelShortLength;
            Elevation = elevation;
            SourcePanelIds = sourcePanelIds;
            IslandIds = islandIds;
        }

        public string Id { get; private set; }
        public Vec2 PanelLongAxis { get; private set; }
        public Vec2 RailDirection { get; private set; }
        public double PanelLongLength { get; private set; }
        public double PanelShortLength { get; private set; }
        public double Elevation { get; private set; }
        public IList<string> SourcePanelIds { get; private set; }
        public IList<string> IslandIds { get; private set; }
    }

    public sealed class RailArrayIslandInfo
    {
        internal RailArrayIslandInfo(string id, string directionGroupId,
            double minimumAlongRail, double maximumAlongRail,
            double minimumAcrossRail, double maximumAcrossRail,
            IList<string> sourcePanelIds, IList<string> componentRowIds)
        {
            Id = id;
            DirectionGroupId = directionGroupId;
            MinimumAlongRail = minimumAlongRail;
            MaximumAlongRail = maximumAlongRail;
            MinimumAcrossRail = minimumAcrossRail;
            MaximumAcrossRail = maximumAcrossRail;
            SourcePanelIds = sourcePanelIds;
            ComponentRowIds = componentRowIds;
        }

        public string Id { get; private set; }
        public string DirectionGroupId { get; private set; }
        public double MinimumAlongRail { get; private set; }
        public double MaximumAlongRail { get; private set; }
        public double MinimumAcrossRail { get; private set; }
        public double MaximumAcrossRail { get; private set; }
        public IList<string> SourcePanelIds { get; private set; }
        public IList<string> ComponentRowIds { get; private set; }
    }

    public sealed class RailComponentRowInfo
    {
        internal RailComponentRowInfo(string id, string directionGroupId, string islandId,
            double centerCoordinate, double minimumAlongRail, double maximumAlongRail,
            double minimumAcrossRail, double maximumAcrossRail,
            IList<string> sourcePanelIds)
        {
            Id = id;
            DirectionGroupId = directionGroupId;
            IslandId = islandId;
            CenterCoordinate = centerCoordinate;
            MinimumAlongRail = minimumAlongRail;
            MaximumAlongRail = maximumAlongRail;
            MinimumAcrossRail = minimumAcrossRail;
            MaximumAcrossRail = maximumAcrossRail;
            SourcePanelIds = sourcePanelIds;
        }

        public string Id { get; private set; }
        public string DirectionGroupId { get; private set; }
        public string IslandId { get; private set; }
        public double CenterCoordinate { get; private set; }
        public double MinimumAlongRail { get; private set; }
        public double MaximumAlongRail { get; private set; }
        public double MinimumAcrossRail { get; private set; }
        public double MaximumAcrossRail { get; private set; }
        public IList<string> SourcePanelIds { get; private set; }
    }

    public sealed class RailSupportIntervalInfo
    {
        internal RailSupportIntervalInfo(Vec2 start, Vec2 end, string sourcePanelId)
        {
            Start = start;
            End = end;
            SourcePanelId = sourcePanelId;
        }

        public Vec2 Start { get; private set; }
        public Vec2 End { get; private set; }
        public string SourcePanelId { get; private set; }
        public double Length { get { return Vec2.Distance(Start, End); } }
    }

    public sealed class RailSegmentMetadata
    {
        internal RailSegmentMetadata(string id, RailSegment segment,
            string directionGroupId, string islandId, string componentRowId,
            int railIndex, IList<string> sourcePanelIds,
            IList<RailSupportIntervalInfo> supportIntervals,
            Vec2 supportStart, Vec2 supportEnd,
            double startOverhang, double endOverhang)
        {
            Id = id;
            Segment = segment;
            DirectionGroupId = directionGroupId;
            IslandId = islandId;
            ComponentRowId = componentRowId;
            RailIndex = railIndex;
            SourcePanelIds = sourcePanelIds;
            SupportIntervals = supportIntervals;
            SupportStart = supportStart;
            SupportEnd = supportEnd;
            StartOverhang = startOverhang;
            EndOverhang = endOverhang;
        }

        public string Id { get; private set; }
        public RailSegment Segment { get; private set; }
        public string DirectionGroupId { get; private set; }
        public string IslandId { get; private set; }
        public string ComponentRowId { get; private set; }
        public int RailIndex { get; private set; }
        public IList<string> SourcePanelIds { get; private set; }
        public IList<RailSupportIntervalInfo> SupportIntervals { get; private set; }
        public Vec2 SupportStart { get; private set; }
        public Vec2 SupportEnd { get; private set; }
        public double StartOverhang { get; private set; }
        public double EndOverhang { get; private set; }
        public double SupportSpan { get { return Vec2.Distance(SupportStart, SupportEnd); } }
    }

    public sealed class RailBreakInfo
    {
        internal RailBreakInfo(string id, string directionGroupId, string islandId,
            string componentRowId, int railIndex,
            string firstSegmentId, string secondSegmentId,
            Vec2 firstSupportEnd, Vec2 secondSupportStart,
            Vec2 firstRailEnd, Vec2 secondRailStart)
        {
            Id = id;
            DirectionGroupId = directionGroupId;
            IslandId = islandId;
            ComponentRowId = componentRowId;
            RailIndex = railIndex;
            FirstSegmentId = firstSegmentId;
            SecondSegmentId = secondSegmentId;
            FirstSupportEnd = firstSupportEnd;
            SecondSupportStart = secondSupportStart;
            FirstRailEnd = firstRailEnd;
            SecondRailStart = secondRailStart;
        }

        public string Id { get; private set; }
        public string DirectionGroupId { get; private set; }
        public string IslandId { get; private set; }
        public string ComponentRowId { get; private set; }
        public int RailIndex { get; private set; }
        public string FirstSegmentId { get; private set; }
        public string SecondSegmentId { get; private set; }
        public Vec2 FirstSupportEnd { get; private set; }
        public Vec2 SecondSupportStart { get; private set; }
        public Vec2 FirstRailEnd { get; private set; }
        public Vec2 SecondRailStart { get; private set; }
        public double SourceGap { get { return Vec2.Distance(FirstSupportEnd, SecondSupportStart); } }
        public double ActualClearance { get { return Vec2.Distance(FirstRailEnd, SecondRailStart); } }
    }

    public static class RailLayoutEngine
    {
        private sealed class Seed
        {
            public Vec2 Start;
            public Vec2 End;
            public Vec2 Midpoint;
            public Vec2 Direction;
            public double Elevation;
            public int RailIndex;
        }

        private sealed class Interval
        {
            public double Start;
            public double End;
        }

        private sealed class LineBucket
        {
            public Vec2 Origin;
            public Vec2 Direction;
            public double Elevation;
            public int RailIndex;
            public readonly List<Interval> Intervals = new List<Interval>();
        }

        private sealed class AngularSeed
        {
            public Seed Seed;
            public double Angle;
            public double UnwrappedAngle;
            public double Offset;
        }

        private sealed class PanelGroup
        {
            public string Id;
            public double LongLength;
            public double ShortLength;
            public Vec2 LongAxis;
            public double Elevation;
            public readonly List<PanelRect> Panels = new List<PanelRect>();
        }

        private sealed class PanelPlacement
        {
            public PanelRect Panel;
            public string SourceId;
            public double UCenter;
            public double VCenter;
            public double MinimumU;
            public double MaximumU;
            public double MinimumV;
            public double MaximumV;
            public int RowBandIndex;
            public RowBuilder Row;
        }

        private sealed class RowBand
        {
            public double MinimumCenterCoordinate;
            public readonly List<PanelPlacement> Panels = new List<PanelPlacement>();
        }

        private sealed class IslandBuilder
        {
            public string Id;
            public double MinimumU = Double.PositiveInfinity;
            public double MaximumU = Double.NegativeInfinity;
            public double MinimumV = Double.PositiveInfinity;
            public double MaximumV = Double.NegativeInfinity;
            public readonly List<PanelPlacement> Panels = new List<PanelPlacement>();
            public readonly List<RowBuilder> Rows = new List<RowBuilder>();
        }

        private sealed class RowBuilder
        {
            public string Id;
            public IslandBuilder Island;
            public int RowBandIndex;
            public double CenterCoordinate;
            public double MinimumU = Double.PositiveInfinity;
            public double MaximumU = Double.NegativeInfinity;
            public double MinimumV = Double.PositiveInfinity;
            public double MaximumV = Double.NegativeInfinity;
            public readonly List<PanelPlacement> Panels = new List<PanelPlacement>();
        }

        private sealed class GroupTopology
        {
            public PanelGroup Group;
            public Vec2 RailDirection;
            public readonly List<PanelPlacement> Placements = new List<PanelPlacement>();
            public readonly List<IslandBuilder> Islands = new List<IslandBuilder>();
            public readonly List<RowBuilder> Rows = new List<RowBuilder>();
        }

        private sealed class DisjointSet
        {
            private readonly int[] _parents;

            public DisjointSet(int count)
            {
                _parents = new int[count];
                for (int index = 0; index < count; index++)
                    _parents[index] = index;
            }

            public int Find(int index)
            {
                int root = index;
                while (_parents[root] != root)
                    root = _parents[root];
                while (_parents[index] != index)
                {
                    int next = _parents[index];
                    _parents[index] = root;
                    index = next;
                }
                return root;
            }

            public void Union(int first, int second)
            {
                int firstRoot = Find(first);
                int secondRoot = Find(second);
                if (firstRoot == secondRoot)
                    return;
                if (firstRoot < secondRoot)
                    _parents[secondRoot] = firstRoot;
                else
                    _parents[firstRoot] = secondRoot;
            }
        }

        private sealed class SupportProjection
        {
            public double Start;
            public double End;
            public string SourcePanelId;
        }

        private sealed class SegmentMetadataBuilder
        {
            public string Id;
            public RailSegment Segment;
            public string DirectionGroupId;
            public string IslandId;
            public string ComponentRowId;
            public int RailIndex;
            public Vec2 Direction;
            public readonly List<SupportProjection> Supports = new List<SupportProjection>();
        }

        public static RailLayoutResult Create(IList<PanelRect> panels, RailLayoutOptions options)
        {
            if (panels == null)
                throw new ArgumentNullException("panels");
            if (options == null)
                throw new ArgumentNullException("options");

            options.Validate();
            if (panels.Count == 0)
            {
                return new RailLayoutResult(new List<RailSegment>(), 0,
                    new List<RailDirectionGroupInfo>(), new List<RailArrayIslandInfo>(),
                    new List<RailComponentRowInfo>(), new List<RailSegmentMetadata>(),
                    new List<RailBreakInfo>());
            }

            List<PanelGroup> groups = BuildPanelGroups(panels, options);
            List<GroupTopology> topologies = new List<GroupTopology>(groups.Count);
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                UpdateRepresentativeAxis(groups[groupIndex]);
                groups[groupIndex].Id = String.Format("DG{0:D4}", groupIndex + 1);
                topologies.Add(BuildTopology(groups[groupIndex], options));
            }

            List<RailSegment> segments = new List<RailSegment>();
            List<SegmentMetadataBuilder> metadataBuilders = new List<SegmentMetadataBuilder>();
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                int segmentStart = segments.Count;
                List<Seed> seeds = CreateSeeds(groups[groupIndex], options);
                List<LineBucket> buckets = BuildBuckets(seeds, options);
                segments.AddRange(MergeBuckets(buckets, options));
                BuildSegmentMetadata(groups[groupIndex], topologies[groupIndex], segments,
                    segmentStart, segments.Count, options, metadataBuilders);
            }

            AssignSegmentIds(metadataBuilders, topologies);
            List<RailDirectionGroupInfo> directionGroups = BuildDirectionGroupInfo(topologies);
            List<RailArrayIslandInfo> arrayIslands = BuildArrayIslandInfo(topologies);
            List<RailComponentRowInfo> componentRows = BuildComponentRowInfo(topologies);
            List<RailSegmentMetadata> segmentMetadata = FinalizeSegmentMetadata(metadataBuilders);
            List<RailBreakInfo> breaks = BuildBreakInfo(metadataBuilders, topologies, options);

            return new RailLayoutResult(segments, groups.Count, directionGroups,
                arrayIslands, componentRows, segmentMetadata, breaks);
        }

        private static GroupTopology BuildTopology(PanelGroup group, RailLayoutOptions options)
        {
            GroupTopology topology = new GroupTopology();
            topology.Group = group;
            topology.RailDirection = new Vec2(-group.LongAxis.Y, group.LongAxis.X).Normalized();
            CanonicalizeDirection(ref topology.RailDirection);

            List<PanelRect> orderedPanels = new List<PanelRect>(group.Panels);
            orderedPanels.Sort(ComparePanels);
            for (int panelIndex = 0; panelIndex < orderedPanels.Count; panelIndex++)
            {
                PanelRect panel = orderedPanels[panelIndex];
                PanelPlacement placement = new PanelPlacement();
                placement.Panel = panel;
                placement.SourceId = CreateSourcePanelId(panel, panelIndex);
                placement.UCenter = Vec2.Dot(panel.Center, topology.RailDirection);
                placement.VCenter = Vec2.Dot(panel.Center, group.LongAxis);
                CalculatePanelBounds(panel, topology.RailDirection, group.LongAxis,
                    out placement.MinimumU, out placement.MaximumU,
                    out placement.MinimumV, out placement.MaximumV);
                topology.Placements.Add(placement);
            }

            List<PanelPlacement> byRow = new List<PanelPlacement>(topology.Placements);
            byRow.Sort(ComparePlacementsByRow);
            List<RowBand> rowBands = new List<RowBand>();
            int rowStart = 0;
            while (rowStart < byRow.Count)
            {
                RowBand rowBand = new RowBand();
                rowBand.MinimumCenterCoordinate = byRow[rowStart].VCenter;
                int rowEnd = rowStart;
                while (rowEnd < byRow.Count &&
                    byRow[rowEnd].VCenter - rowBand.MinimumCenterCoordinate <=
                        options.LineAlignmentTolerance + options.NumericTolerance)
                {
                    byRow[rowEnd].RowBandIndex = rowBands.Count;
                    rowBand.Panels.Add(byRow[rowEnd]);
                    rowEnd++;
                }
                rowBands.Add(rowBand);
                rowStart = rowEnd;
            }

            DisjointSet islands = new DisjointSet(topology.Placements.Count);
            double islandMaximumGap = Math.Max(group.LongLength, group.ShortLength);
            for (int firstIndex = 0; firstIndex < topology.Placements.Count; firstIndex++)
            {
                PanelPlacement first = topology.Placements[firstIndex];
                for (int secondIndex = firstIndex + 1;
                    secondIndex < topology.Placements.Count; secondIndex++)
                {
                    PanelPlacement second = topology.Placements[secondIndex];
                    double alongGap = IntervalGap(first.MinimumU, first.MaximumU,
                        second.MinimumU, second.MaximumU);
                    double acrossGap = IntervalGap(first.MinimumV, first.MaximumV,
                        second.MinimumV, second.MaximumV);
                    bool sameRow = first.RowBandIndex == second.RowBandIndex;
                    bool connectedAlongRow = sameRow &&
                        alongGap <= islandMaximumGap + options.NumericTolerance;
                    bool connectedAcrossRows = !sameRow &&
                        acrossGap <= islandMaximumGap + options.NumericTolerance &&
                        alongGap <= options.MaximumContinuityGap +
                            options.LineAlignmentTolerance + options.NumericTolerance;
                    if (connectedAlongRow || connectedAcrossRows)
                        islands.Union(firstIndex, secondIndex);
                }
            }

            Dictionary<int, IslandBuilder> byRoot = new Dictionary<int, IslandBuilder>();
            for (int index = 0; index < topology.Placements.Count; index++)
            {
                int root = islands.Find(index);
                IslandBuilder island;
                if (!byRoot.TryGetValue(root, out island))
                {
                    island = new IslandBuilder();
                    byRoot[root] = island;
                }
                AddPlacementToIsland(island, topology.Placements[index]);
            }
            foreach (KeyValuePair<int, IslandBuilder> pair in byRoot)
                topology.Islands.Add(pair.Value);
            topology.Islands.Sort(CompareIslands);

            for (int islandIndex = 0; islandIndex < topology.Islands.Count; islandIndex++)
            {
                IslandBuilder island = topology.Islands[islandIndex];
                island.Id = group.Id + String.Format("-I{0:D4}", islandIndex + 1);
                Dictionary<int, RowBuilder> rowsByBand = new Dictionary<int, RowBuilder>();
                for (int panelIndex = 0; panelIndex < island.Panels.Count; panelIndex++)
                {
                    PanelPlacement placement = island.Panels[panelIndex];
                    RowBuilder row;
                    if (!rowsByBand.TryGetValue(placement.RowBandIndex, out row))
                    {
                        row = new RowBuilder();
                        row.Island = island;
                        row.RowBandIndex = placement.RowBandIndex;
                        rowsByBand[placement.RowBandIndex] = row;
                    }
                    AddPlacementToRow(row, placement);
                }
                foreach (KeyValuePair<int, RowBuilder> pair in rowsByBand)
                    island.Rows.Add(pair.Value);
                island.Rows.Sort(CompareRows);

                for (int rowIndex = 0; rowIndex < island.Rows.Count; rowIndex++)
                {
                    RowBuilder row = island.Rows[rowIndex];
                    row.Id = island.Id + String.Format("-R{0:D4}", rowIndex + 1);
                    double centerSum = 0.0;
                    for (int panelIndex = 0; panelIndex < row.Panels.Count; panelIndex++)
                    {
                        centerSum += row.Panels[panelIndex].VCenter;
                        row.Panels[panelIndex].Row = row;
                    }
                    row.CenterCoordinate = centerSum / row.Panels.Count;
                    topology.Rows.Add(row);
                }
            }

            return topology;
        }

        private static string CreateSourcePanelId(PanelRect panel, int stableIndex)
        {
            if (!String.IsNullOrWhiteSpace(panel.SourceId))
                return panel.SourceId;
            return String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "PANEL-{0:D4}@{1:R},{2:R},{3:R}", stableIndex + 1,
                panel.Center.X, panel.Center.Y, panel.Elevation);
        }

        private static void CalculatePanelBounds(PanelRect panel, Vec2 railDirection,
            Vec2 panelLongAxis, out double minimumU, out double maximumU,
            out double minimumV, out double maximumV)
        {
            Vec2 halfLong = panel.LongAxis * (panel.LongLength / 2.0);
            Vec2 halfShort = panel.ShortAxis * (panel.ShortLength / 2.0);
            Vec2[] corners = new Vec2[]
            {
                panel.Center - halfLong - halfShort,
                panel.Center - halfLong + halfShort,
                panel.Center + halfLong - halfShort,
                panel.Center + halfLong + halfShort
            };
            minimumU = Double.PositiveInfinity;
            maximumU = Double.NegativeInfinity;
            minimumV = Double.PositiveInfinity;
            maximumV = Double.NegativeInfinity;
            for (int index = 0; index < corners.Length; index++)
            {
                double u = Vec2.Dot(corners[index], railDirection);
                double v = Vec2.Dot(corners[index], panelLongAxis);
                minimumU = Math.Min(minimumU, u);
                maximumU = Math.Max(maximumU, u);
                minimumV = Math.Min(minimumV, v);
                maximumV = Math.Max(maximumV, v);
            }
        }

        private static int ComparePlacementsByRow(PanelPlacement left, PanelPlacement right)
        {
            int comparison = left.VCenter.CompareTo(right.VCenter);
            if (comparison != 0)
                return comparison;
            comparison = left.UCenter.CompareTo(right.UCenter);
            if (comparison != 0)
                return comparison;
            return StringComparer.Ordinal.Compare(left.SourceId, right.SourceId);
        }

        private static double IntervalGap(double firstStart, double firstEnd,
            double secondStart, double secondEnd)
        {
            if (firstEnd < secondStart)
                return secondStart - firstEnd;
            if (secondEnd < firstStart)
                return firstStart - secondEnd;
            return 0.0;
        }

        private static void AddPlacementToIsland(IslandBuilder island,
            PanelPlacement placement)
        {
            island.Panels.Add(placement);
            island.MinimumU = Math.Min(island.MinimumU, placement.MinimumU);
            island.MaximumU = Math.Max(island.MaximumU, placement.MaximumU);
            island.MinimumV = Math.Min(island.MinimumV, placement.MinimumV);
            island.MaximumV = Math.Max(island.MaximumV, placement.MaximumV);
        }

        private static void AddPlacementToRow(RowBuilder row, PanelPlacement placement)
        {
            row.Panels.Add(placement);
            row.MinimumU = Math.Min(row.MinimumU, placement.MinimumU);
            row.MaximumU = Math.Max(row.MaximumU, placement.MaximumU);
            row.MinimumV = Math.Min(row.MinimumV, placement.MinimumV);
            row.MaximumV = Math.Max(row.MaximumV, placement.MaximumV);
        }

        private static int CompareIslands(IslandBuilder left, IslandBuilder right)
        {
            int comparison = left.MinimumV.CompareTo(right.MinimumV);
            if (comparison != 0)
                return comparison;
            comparison = left.MinimumU.CompareTo(right.MinimumU);
            if (comparison != 0)
                return comparison;
            comparison = left.MaximumV.CompareTo(right.MaximumV);
            if (comparison != 0)
                return comparison;
            return left.MaximumU.CompareTo(right.MaximumU);
        }

        private static int CompareRows(RowBuilder left, RowBuilder right)
        {
            int comparison = left.CenterCoordinate.CompareTo(right.CenterCoordinate);
            if (comparison != 0)
                return comparison;
            comparison = left.MinimumU.CompareTo(right.MinimumU);
            if (comparison != 0)
                return comparison;
            return left.MaximumU.CompareTo(right.MaximumU);
        }

        private static void BuildSegmentMetadata(PanelGroup group, GroupTopology topology,
            IList<RailSegment> segments, int startIndex, int endIndex,
            RailLayoutOptions options, ICollection<SegmentMetadataBuilder> output)
        {
            for (int segmentIndex = startIndex; segmentIndex < endIndex; segmentIndex++)
            {
                RailSegment segment = segments[segmentIndex];
                Vec2 direction = (segment.End - segment.Start).Normalized();
                RowBuilder bestRow = null;
                int bestRailIndex = -1;
                List<SupportProjection> bestSupports = null;
                int bestSupportCount = -1;
                double bestOverlap = Double.NegativeInfinity;
                double bestLineError = Double.PositiveInfinity;

                for (int rowIndex = 0; rowIndex < topology.Rows.Count; rowIndex++)
                {
                    RowBuilder row = topology.Rows[rowIndex];
                    for (int railIndex = 0; railIndex < options.RailCount; railIndex++)
                    {
                        double overlap;
                        double lineError;
                        List<SupportProjection> supports = CollectSupports(segment, direction,
                            group, row, railIndex, options, out overlap, out lineError);
                        if (supports.Count > bestSupportCount ||
                            (supports.Count == bestSupportCount && overlap > bestOverlap) ||
                            (supports.Count == bestSupportCount &&
                                Math.Abs(overlap - bestOverlap) <= options.NumericTolerance &&
                                lineError < bestLineError))
                        {
                            bestRow = row;
                            bestRailIndex = railIndex;
                            bestSupports = supports;
                            bestSupportCount = supports.Count;
                            bestOverlap = overlap;
                            bestLineError = lineError;
                        }
                    }
                }

                if (bestRow == null || bestSupports == null || bestSupports.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Generated rail segment could not be matched to its source panels.");
                }

                bestSupports.Sort(CompareSupports);
                SegmentMetadataBuilder builder = new SegmentMetadataBuilder();
                builder.Segment = segment;
                builder.DirectionGroupId = group.Id;
                builder.IslandId = bestRow.Island.Id;
                builder.ComponentRowId = bestRow.Id;
                builder.RailIndex = bestRailIndex;
                builder.Direction = direction;
                builder.Supports.AddRange(bestSupports);
                output.Add(builder);
            }
        }

        private static List<SupportProjection> CollectSupports(RailSegment segment,
            Vec2 segmentDirection, PanelGroup group, RowBuilder row, int railIndex,
            RailLayoutOptions options, out double totalOverlap, out double maximumLineError)
        {
            List<SupportProjection> supports = new List<SupportProjection>();
            totalOverlap = 0.0;
            maximumLineError = 0.0;
            double segmentLength = segment.Length;
            double offset = CalculateRailOffset(railIndex, options);
            for (int panelIndex = 0; panelIndex < row.Panels.Count; panelIndex++)
            {
                PanelPlacement placement = row.Panels[panelIndex];
                PanelRect panel = placement.Panel;
                Vec2 midpoint = panel.Center + (group.LongAxis * offset);
                double lineError = Math.Abs(Vec2.Cross(segmentDirection,
                    midpoint - segment.Start));
                if (lineError > options.LineAlignmentTolerance + options.NumericTolerance)
                    continue;

                Vec2 halfRail = segmentDirection * (panel.ShortLength / 2.0);
                double first = Vec2.Dot((midpoint - halfRail) - segment.Start,
                    segmentDirection);
                double second = Vec2.Dot((midpoint + halfRail) - segment.Start,
                    segmentDirection);
                double supportStart = Math.Min(first, second);
                double supportEnd = Math.Max(first, second);
                if (supportEnd < -options.NumericTolerance ||
                    supportStart > segmentLength + options.NumericTolerance)
                {
                    continue;
                }

                if (supportStart < 0.0 && supportStart >= -options.NumericTolerance)
                    supportStart = 0.0;
                if (supportEnd > segmentLength &&
                    supportEnd <= segmentLength + options.NumericTolerance)
                {
                    supportEnd = segmentLength;
                }
                SupportProjection support = new SupportProjection();
                support.Start = supportStart;
                support.End = supportEnd;
                support.SourcePanelId = placement.SourceId;
                supports.Add(support);
                totalOverlap += Math.Max(0.0,
                    Math.Min(segmentLength, supportEnd) - Math.Max(0.0, supportStart));
                maximumLineError = Math.Max(maximumLineError, lineError);
            }
            return supports;
        }

        private static double CalculateRailOffset(int railIndex, RailLayoutOptions options)
        {
            if (options.RailCount <= 1)
                return 0.0;
            return (-options.TotalSpacing / 2.0) +
                (options.TotalSpacing * railIndex / (options.RailCount - 1));
        }

        private static int CompareSupports(SupportProjection left, SupportProjection right)
        {
            int comparison = left.Start.CompareTo(right.Start);
            if (comparison != 0)
                return comparison;
            comparison = left.End.CompareTo(right.End);
            if (comparison != 0)
                return comparison;
            return StringComparer.Ordinal.Compare(left.SourcePanelId, right.SourcePanelId);
        }

        private static void AssignSegmentIds(IList<SegmentMetadataBuilder> builders,
            IList<GroupTopology> topologies)
        {
            for (int topologyIndex = 0; topologyIndex < topologies.Count; topologyIndex++)
            {
                GroupTopology topology = topologies[topologyIndex];
                for (int rowIndex = 0; rowIndex < topology.Rows.Count; rowIndex++)
                {
                    RowBuilder row = topology.Rows[rowIndex];
                    for (int railIndex = 0; ; railIndex++)
                    {
                        List<SegmentMetadataBuilder> matching = new List<SegmentMetadataBuilder>();
                        for (int builderIndex = 0; builderIndex < builders.Count; builderIndex++)
                        {
                            SegmentMetadataBuilder builder = builders[builderIndex];
                            if (String.Equals(builder.ComponentRowId, row.Id,
                                    StringComparison.Ordinal) &&
                                builder.RailIndex == railIndex)
                            {
                                matching.Add(builder);
                            }
                        }
                        if (matching.Count == 0)
                        {
                            if (railIndex > 0)
                                break;
                            continue;
                        }
                        matching.Sort(delegate(SegmentMetadataBuilder left,
                            SegmentMetadataBuilder right)
                        {
                            double leftCoordinate = Vec2.Dot(
                                (left.Segment.Start + left.Segment.End) / 2.0,
                                topology.RailDirection);
                            double rightCoordinate = Vec2.Dot(
                                (right.Segment.Start + right.Segment.End) / 2.0,
                                topology.RailDirection);
                            return leftCoordinate.CompareTo(rightCoordinate);
                        });
                        for (int segmentIndex = 0; segmentIndex < matching.Count; segmentIndex++)
                        {
                            matching[segmentIndex].Id = row.Id + String.Format(
                                "-L{0:D2}-S{1:D3}", railIndex + 1, segmentIndex + 1);
                        }
                    }
                }
            }

            for (int index = 0; index < builders.Count; index++)
            {
                if (String.IsNullOrEmpty(builders[index].Id))
                    throw new InvalidOperationException("Rail segment metadata ID was not assigned.");
            }
        }

        private static List<RailDirectionGroupInfo> BuildDirectionGroupInfo(
            IList<GroupTopology> topologies)
        {
            List<RailDirectionGroupInfo> result = new List<RailDirectionGroupInfo>();
            for (int index = 0; index < topologies.Count; index++)
            {
                GroupTopology topology = topologies[index];
                List<string> sourceIds = SourceIds(topology.Placements);
                List<string> islandIds = new List<string>();
                for (int islandIndex = 0; islandIndex < topology.Islands.Count; islandIndex++)
                    islandIds.Add(topology.Islands[islandIndex].Id);
                result.Add(new RailDirectionGroupInfo(topology.Group.Id,
                    topology.Group.LongAxis, topology.RailDirection,
                    topology.Group.LongLength, topology.Group.ShortLength,
                    topology.Group.Elevation, sourceIds.AsReadOnly(),
                    islandIds.AsReadOnly()));
            }
            return result;
        }

        private static List<RailArrayIslandInfo> BuildArrayIslandInfo(
            IList<GroupTopology> topologies)
        {
            List<RailArrayIslandInfo> result = new List<RailArrayIslandInfo>();
            for (int topologyIndex = 0; topologyIndex < topologies.Count; topologyIndex++)
            {
                GroupTopology topology = topologies[topologyIndex];
                for (int islandIndex = 0; islandIndex < topology.Islands.Count; islandIndex++)
                {
                    IslandBuilder island = topology.Islands[islandIndex];
                    List<string> sourceIds = SourceIds(island.Panels);
                    List<string> rowIds = new List<string>();
                    for (int rowIndex = 0; rowIndex < island.Rows.Count; rowIndex++)
                        rowIds.Add(island.Rows[rowIndex].Id);
                    result.Add(new RailArrayIslandInfo(island.Id, topology.Group.Id,
                        island.MinimumU, island.MaximumU, island.MinimumV, island.MaximumV,
                        sourceIds.AsReadOnly(), rowIds.AsReadOnly()));
                }
            }
            return result;
        }

        private static List<RailComponentRowInfo> BuildComponentRowInfo(
            IList<GroupTopology> topologies)
        {
            List<RailComponentRowInfo> result = new List<RailComponentRowInfo>();
            for (int topologyIndex = 0; topologyIndex < topologies.Count; topologyIndex++)
            {
                GroupTopology topology = topologies[topologyIndex];
                for (int rowIndex = 0; rowIndex < topology.Rows.Count; rowIndex++)
                {
                    RowBuilder row = topology.Rows[rowIndex];
                    List<string> sourceIds = SourceIds(row.Panels);
                    result.Add(new RailComponentRowInfo(row.Id, topology.Group.Id,
                        row.Island.Id, row.CenterCoordinate, row.MinimumU, row.MaximumU,
                        row.MinimumV, row.MaximumV, sourceIds.AsReadOnly()));
                }
            }
            return result;
        }

        private static List<string> SourceIds(IList<PanelPlacement> placements)
        {
            List<string> result = new List<string>();
            for (int index = 0; index < placements.Count; index++)
            {
                if (!result.Contains(placements[index].SourceId))
                    result.Add(placements[index].SourceId);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static List<RailSegmentMetadata> FinalizeSegmentMetadata(
            IList<SegmentMetadataBuilder> builders)
        {
            List<RailSegmentMetadata> result = new List<RailSegmentMetadata>(builders.Count);
            for (int index = 0; index < builders.Count; index++)
            {
                SegmentMetadataBuilder builder = builders[index];
                builder.Supports.Sort(CompareSupports);
                double supportStart = builder.Supports[0].Start;
                double supportEnd = builder.Supports[0].End;
                List<RailSupportIntervalInfo> supportIntervals =
                    new List<RailSupportIntervalInfo>(builder.Supports.Count);
                List<string> sourceIds = new List<string>();
                for (int supportIndex = 0;
                    supportIndex < builder.Supports.Count; supportIndex++)
                {
                    SupportProjection support = builder.Supports[supportIndex];
                    supportStart = Math.Min(supportStart, support.Start);
                    supportEnd = Math.Max(supportEnd, support.End);
                    supportIntervals.Add(new RailSupportIntervalInfo(
                        builder.Segment.Start + (builder.Direction * support.Start),
                        builder.Segment.Start + (builder.Direction * support.End),
                        support.SourcePanelId));
                    if (!sourceIds.Contains(support.SourcePanelId))
                        sourceIds.Add(support.SourcePanelId);
                }
                sourceIds.Sort(StringComparer.Ordinal);

                Vec2 supportStartPoint = builder.Segment.Start +
                    (builder.Direction * supportStart);
                Vec2 supportEndPoint = builder.Segment.Start +
                    (builder.Direction * supportEnd);
                double startOverhang = Math.Max(0.0, supportStart);
                double endOverhang = Math.Max(0.0, builder.Segment.Length - supportEnd);
                result.Add(new RailSegmentMetadata(builder.Id, builder.Segment,
                    builder.DirectionGroupId, builder.IslandId, builder.ComponentRowId,
                    builder.RailIndex, sourceIds.AsReadOnly(),
                    supportIntervals.AsReadOnly(), supportStartPoint, supportEndPoint,
                    startOverhang, endOverhang));
            }
            return result;
        }

        private static List<RailBreakInfo> BuildBreakInfo(
            IList<SegmentMetadataBuilder> builders, IList<GroupTopology> topologies,
            RailLayoutOptions options)
        {
            List<RailBreakInfo> result = new List<RailBreakInfo>();
            for (int topologyIndex = 0; topologyIndex < topologies.Count; topologyIndex++)
            {
                GroupTopology topology = topologies[topologyIndex];
                for (int rowIndex = 0; rowIndex < topology.Rows.Count; rowIndex++)
                {
                    RowBuilder row = topology.Rows[rowIndex];
                    for (int railIndex = 0; railIndex < options.RailCount; railIndex++)
                    {
                        List<SegmentMetadataBuilder> matching =
                            new List<SegmentMetadataBuilder>();
                        for (int builderIndex = 0; builderIndex < builders.Count; builderIndex++)
                        {
                            SegmentMetadataBuilder builder = builders[builderIndex];
                            if (String.Equals(builder.ComponentRowId, row.Id,
                                    StringComparison.Ordinal) &&
                                builder.RailIndex == railIndex)
                            {
                                matching.Add(builder);
                            }
                        }
                        matching.Sort(delegate(SegmentMetadataBuilder left,
                            SegmentMetadataBuilder right)
                        {
                            return Vec2.Dot(left.Segment.Start, topology.RailDirection)
                                .CompareTo(Vec2.Dot(right.Segment.Start,
                                    topology.RailDirection));
                        });
                        for (int breakIndex = 0; breakIndex + 1 < matching.Count; breakIndex++)
                        {
                            SegmentMetadataBuilder first = matching[breakIndex];
                            SegmentMetadataBuilder second = matching[breakIndex + 1];
                            SupportProjection firstSupport =
                                first.Supports[first.Supports.Count - 1];
                            SupportProjection secondSupport = second.Supports[0];
                            Vec2 firstSupportEnd = first.Segment.Start +
                                (first.Direction * firstSupport.End);
                            Vec2 secondSupportStart = second.Segment.Start +
                                (second.Direction * secondSupport.Start);
                            string id = row.Id + String.Format("-L{0:D2}-B{1:D3}",
                                railIndex + 1, breakIndex + 1);
                            result.Add(new RailBreakInfo(id, topology.Group.Id,
                                row.Island.Id, row.Id, railIndex, first.Id, second.Id,
                                firstSupportEnd, secondSupportStart,
                                first.Segment.End, second.Segment.Start));
                        }
                    }
                }
            }
            return result;
        }

        private static List<Seed> CreateSeeds(PanelGroup group, RailLayoutOptions options)
        {
            IList<PanelRect> panels = group.Panels;
            List<Seed> seeds = new List<Seed>(panels.Count * options.RailCount);
            Vec2 railDirection = new Vec2(-group.LongAxis.Y, group.LongAxis.X).Normalized();
            CanonicalizeDirection(ref railDirection);
            for (int panelIndex = 0; panelIndex < panels.Count; panelIndex++)
            {
                PanelRect panel = panels[panelIndex];
                for (int railIndex = 0; railIndex < options.RailCount; railIndex++)
                {
                    double offset = 0.0;
                    if (options.RailCount > 1)
                    {
                        offset = (-options.TotalSpacing / 2.0) +
                            (options.TotalSpacing * railIndex / (options.RailCount - 1));
                    }

                    Vec2 midpoint = panel.Center + (group.LongAxis * offset);
                    Vec2 halfRail = railDirection * (panel.ShortLength / 2.0);
                    Vec2 start = midpoint - halfRail;
                    Vec2 end = midpoint + halfRail;
                    Vec2 direction = (end - start).Normalized();
                    Canonicalize(ref start, ref end, ref direction);

                    seeds.Add(new Seed
                    {
                        Start = start,
                        End = end,
                        Midpoint = midpoint,
                        Direction = direction,
                        Elevation = panel.Elevation,
                        RailIndex = railIndex
                    });
                }
            }

            return seeds;
        }

        private static List<LineBucket> BuildBuckets(IList<Seed> seeds, RailLayoutOptions options)
        {
            List<LineBucket> buckets = new List<LineBucket>();
            for (int railIndex = 0; railIndex < options.RailCount; railIndex++)
            {
                List<Seed> railSeeds = new List<Seed>();
                for (int seedIndex = 0; seedIndex < seeds.Count; seedIndex++)
                {
                    if (seeds[seedIndex].RailIndex == railIndex)
                        railSeeds.Add(seeds[seedIndex]);
                }

                railSeeds.Sort(CompareSeedsByElevationAndPosition);
                int elevationStart = 0;
                while (elevationStart < railSeeds.Count)
                {
                    int elevationEnd = elevationStart + 1;
                    double minimumElevation = railSeeds[elevationStart].Elevation;
                    while (elevationEnd < railSeeds.Count &&
                        railSeeds[elevationEnd].Elevation - minimumElevation <=
                            options.ElevationTolerance + options.NumericTolerance)
                    {
                        elevationEnd++;
                    }

                    BuildOrientationBuckets(railSeeds, elevationStart, elevationEnd,
                        railIndex, options, buckets);
                    elevationStart = elevationEnd;
                }
            }

            return buckets;
        }

        private static void BuildOrientationBuckets(IList<Seed> seeds, int start, int end,
            int railIndex, RailLayoutOptions options, ICollection<LineBucket> output)
        {
            List<AngularSeed> angularSeeds = new List<AngularSeed>(end - start);
            for (int index = start; index < end; index++)
            {
                angularSeeds.Add(new AngularSeed
                {
                    Seed = seeds[index],
                    Angle = LineAngle(seeds[index].Direction)
                });
            }

            angularSeeds.Sort(delegate(AngularSeed left, AngularSeed right)
            {
                int comparison = left.Angle.CompareTo(right.Angle);
                if (comparison != 0)
                    return comparison;
                comparison = left.Seed.Midpoint.X.CompareTo(right.Seed.Midpoint.X);
                if (comparison != 0)
                    return comparison;
                return left.Seed.Midpoint.Y.CompareTo(right.Seed.Midpoint.Y);
            });

            int cutIndex = FindCircularCut(angularSeeds);
            List<AngularSeed> unwrapped = new List<AngularSeed>(angularSeeds.Count);
            double previousAngle = Double.NegativeInfinity;
            for (int offset = 0; offset < angularSeeds.Count; offset++)
            {
                AngularSeed item = angularSeeds[(cutIndex + offset) % angularSeeds.Count];
                double angle = item.Angle;
                while (angle < previousAngle)
                    angle += Math.PI;
                item.UnwrappedAngle = angle;
                unwrapped.Add(item);
                previousAngle = angle;
            }

            double angleTolerance = options.AngleToleranceDegrees * Math.PI / 180.0;
            int orientationStart = 0;
            while (orientationStart < unwrapped.Count)
            {
                int orientationEnd = orientationStart + 1;
                double minimumAngle = unwrapped[orientationStart].UnwrappedAngle;
                while (orientationEnd < unwrapped.Count &&
                    unwrapped[orientationEnd].UnwrappedAngle - minimumAngle <=
                        angleTolerance + 1e-12)
                {
                    orientationEnd++;
                }

                BuildOffsetBuckets(unwrapped, orientationStart, orientationEnd,
                    railIndex, options, output);
                orientationStart = orientationEnd;
            }
        }

        private static void BuildOffsetBuckets(IList<AngularSeed> seeds, int start, int end,
            int railIndex, RailLayoutOptions options, ICollection<LineBucket> output)
        {
            double angleSum = 0.0;
            for (int index = start; index < end; index++)
            {
                angleSum += seeds[index].UnwrappedAngle;
            }

            double meanAngle = angleSum / (end - start);
            Vec2 direction = new Vec2(Math.Cos(meanAngle), Math.Sin(meanAngle)).Normalized();
            CanonicalizeDirection(ref direction);
            Vec2 normal = new Vec2(-direction.Y, direction.X);

            List<AngularSeed> sortedByOffset = new List<AngularSeed>(end - start);
            for (int index = start; index < end; index++)
            {
                AngularSeed item = seeds[index];
                item.Offset = Vec2.Dot(item.Seed.Midpoint, normal);
                sortedByOffset.Add(item);
            }
            sortedByOffset.Sort(delegate(AngularSeed left, AngularSeed right)
            {
                int comparison = left.Offset.CompareTo(right.Offset);
                if (comparison != 0)
                    return comparison;
                comparison = left.Seed.Midpoint.X.CompareTo(right.Seed.Midpoint.X);
                if (comparison != 0)
                    return comparison;
                return left.Seed.Midpoint.Y.CompareTo(right.Seed.Midpoint.Y);
            });

            int offsetStart = 0;
            while (offsetStart < sortedByOffset.Count)
            {
                int offsetEnd = offsetStart + 1;
                double minimumOffset = sortedByOffset[offsetStart].Offset;
                while (offsetEnd < sortedByOffset.Count &&
                    sortedByOffset[offsetEnd].Offset - minimumOffset <=
                        options.LineAlignmentTolerance + options.NumericTolerance)
                {
                    offsetEnd++;
                }

                double offsetSum = 0.0;
                double bucketElevationSum = 0.0;
                for (int index = offsetStart; index < offsetEnd; index++)
                {
                    offsetSum += sortedByOffset[index].Offset;
                    bucketElevationSum += sortedByOffset[index].Seed.Elevation;
                }

                LineBucket bucket = new LineBucket();
                bucket.Direction = direction;
                bucket.Origin = normal * (offsetSum / (offsetEnd - offsetStart));
                bucket.Elevation = bucketElevationSum / (offsetEnd - offsetStart);
                bucket.RailIndex = railIndex;

                for (int index = offsetStart; index < offsetEnd; index++)
                {
                    Seed seed = sortedByOffset[index].Seed;
                    double startProjection = Vec2.Dot(seed.Start - bucket.Origin, bucket.Direction);
                    double endProjection = Vec2.Dot(seed.End - bucket.Origin, bucket.Direction);
                    bucket.Intervals.Add(new Interval
                    {
                        Start = Math.Min(startProjection, endProjection),
                        End = Math.Max(startProjection, endProjection)
                    });
                }

                output.Add(bucket);
                offsetStart = offsetEnd;
            }
        }

        private static int FindCircularCut(IList<AngularSeed> seeds)
        {
            if (seeds.Count <= 1)
                return 0;

            double largestGap = Double.NegativeInfinity;
            int cutIndex = 0;
            for (int index = 0; index < seeds.Count; index++)
            {
                double current = seeds[index].Angle;
                double next = index + 1 < seeds.Count
                    ? seeds[index + 1].Angle
                    : seeds[0].Angle + Math.PI;
                double gap = next - current;
                if (gap > largestGap)
                {
                    largestGap = gap;
                    cutIndex = (index + 1) % seeds.Count;
                }
            }

            return cutIndex;
        }

        private static int CompareSeedsByElevationAndPosition(Seed left, Seed right)
        {
            int comparison = left.Elevation.CompareTo(right.Elevation);
            if (comparison != 0)
                return comparison;
            comparison = left.Midpoint.X.CompareTo(right.Midpoint.X);
            if (comparison != 0)
                return comparison;
            return left.Midpoint.Y.CompareTo(right.Midpoint.Y);
        }

        private static double LineAngle(Vec2 direction)
        {
            double angle = Math.Atan2(direction.Y, direction.X);
            while (angle < 0.0)
                angle += Math.PI;
            while (angle >= Math.PI)
                angle -= Math.PI;
            return angle;
        }

        private static void CanonicalizeDirection(ref Vec2 direction)
        {
            if (direction.X < -1e-9 ||
                (Math.Abs(direction.X) <= 1e-9 && direction.Y < 0.0))
            {
                direction = direction * -1.0;
            }
        }

        private static List<RailSegment> MergeBuckets(IList<LineBucket> buckets, RailLayoutOptions options)
        {
            List<RailSegment> result = new List<RailSegment>();
            for (int bucketIndex = 0; bucketIndex < buckets.Count; bucketIndex++)
            {
                LineBucket bucket = buckets[bucketIndex];
                bucket.Intervals.Sort(delegate(Interval left, Interval right)
                {
                    return left.Start.CompareTo(right.Start);
                });

                double currentStart = bucket.Intervals[0].Start;
                double currentEnd = bucket.Intervals[0].End;
                double currentStartOverhang = options.EndOverhang;
                for (int index = 1; index < bucket.Intervals.Count; index++)
                {
                    Interval next = bucket.Intervals[index];
                    double gap = next.Start - currentEnd;
                    if (gap <= options.MaximumContinuityGap + options.NumericTolerance)
                    {
                        currentEnd = Math.Max(currentEnd, next.End);
                    }
                    else
                    {
                        double internalOverhang = CalculateInternalOverhang(gap, options);
                        AddSegment(result, bucket, currentStart, currentEnd,
                            currentStartOverhang, internalOverhang);
                        currentStart = next.Start;
                        currentEnd = next.End;
                        currentStartOverhang = internalOverhang;
                    }
                }

                AddSegment(result, bucket, currentStart, currentEnd,
                    currentStartOverhang, options.EndOverhang);
            }

            return result;
        }

        private static double CalculateInternalOverhang(double gap, RailLayoutOptions options)
        {
            double targetClearance = Math.Min(options.MinimumBreakClearance, gap);
            double availablePerSide = Math.Max(0.0, (gap - targetClearance) / 2.0);
            return Math.Min(options.EndOverhang, availablePerSide);
        }

        private static void UpdateRepresentativeAxis(PanelGroup group)
        {
            Vec2 reference = group.Panels[0].LongAxis;
            Vec2 sum = new Vec2(0.0, 0.0);
            double elevationSum = 0.0;
            for (int index = 0; index < group.Panels.Count; index++)
            {
                Vec2 axis = group.Panels[index].LongAxis;
                if (Vec2.Dot(axis, reference) < 0.0)
                    axis = axis * -1.0;
                sum = sum + axis;
                elevationSum += group.Panels[index].Elevation;
            }
            group.LongAxis = sum.Normalized();
            CanonicalizeDirection(ref group.LongAxis);
            group.Elevation = elevationSum / group.Panels.Count;
        }

        private static void AddSegment(ICollection<RailSegment> output, LineBucket bucket,
            double start, double end, double startOverhang, double endOverhang)
        {
            Vec2 startPoint = bucket.Origin + (bucket.Direction * (start - startOverhang));
            Vec2 endPoint = bucket.Origin + (bucket.Direction * (end + endOverhang));
            output.Add(new RailSegment(startPoint, endPoint, bucket.Elevation));
        }

        private static List<PanelGroup> BuildPanelGroups(
            IList<PanelRect> panels, RailLayoutOptions options)
        {
            List<PanelRect> orderedPanels = new List<PanelRect>(panels);
            orderedPanels.Sort(ComparePanels);
            List<PanelGroup> groups = new List<PanelGroup>();
            double sineTolerance = Math.Sin(options.AngleToleranceDegrees * Math.PI / 180.0);

            for (int panelIndex = 0; panelIndex < orderedPanels.Count; panelIndex++)
            {
                PanelRect panel = orderedPanels[panelIndex];
                PanelGroup matchedGroup = null;
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    PanelGroup group = groups[groupIndex];
                    if (Math.Abs(group.LongLength - panel.LongLength) > options.LineAlignmentTolerance)
                        continue;
                    if (Math.Abs(group.ShortLength - panel.ShortLength) > options.LineAlignmentTolerance)
                        continue;
                    if (Math.Abs(group.Elevation - panel.Elevation) > options.ElevationTolerance)
                        continue;
                    if (Math.Abs(Vec2.Cross(group.LongAxis, panel.LongAxis)) > sineTolerance)
                        continue;

                    matchedGroup = group;
                    break;
                }

                if (matchedGroup == null)
                {
                    matchedGroup = new PanelGroup
                    {
                        LongLength = panel.LongLength,
                        ShortLength = panel.ShortLength,
                        LongAxis = panel.LongAxis,
                        Elevation = panel.Elevation
                    };
                    groups.Add(matchedGroup);
                }
                matchedGroup.Panels.Add(panel);
            }

            return groups;
        }

        private static int ComparePanels(PanelRect left, PanelRect right)
        {
            int comparison = left.LongLength.CompareTo(right.LongLength);
            if (comparison != 0)
                return comparison;
            comparison = left.ShortLength.CompareTo(right.ShortLength);
            if (comparison != 0)
                return comparison;
            comparison = left.Elevation.CompareTo(right.Elevation);
            if (comparison != 0)
                return comparison;
            comparison = LineAngle(left.LongAxis).CompareTo(LineAngle(right.LongAxis));
            if (comparison != 0)
                return comparison;
            comparison = left.Center.X.CompareTo(right.Center.X);
            if (comparison != 0)
                return comparison;
            return left.Center.Y.CompareTo(right.Center.Y);
        }

        private static void Canonicalize(ref Vec2 start, ref Vec2 end, ref Vec2 direction)
        {
            if (direction.X < -1e-9 || (Math.Abs(direction.X) <= 1e-9 && direction.Y < 0.0))
            {
                Vec2 temporary = start;
                start = end;
                end = temporary;
                direction = direction * -1.0;
            }
        }
    }
}
