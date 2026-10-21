using System;
using System.Collections.Generic;
using System.Globalization;
using StructuralToolbox.SupportDetail.Settings;

namespace StructuralToolbox.SupportDetail.Core
{
    public struct SupportDetailPoint
    {
        public double X;
        public double Y;

        public SupportDetailPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static SupportDetailPoint operator +(
            SupportDetailPoint point, SupportDetailVector vector)
        {
            return new SupportDetailPoint(point.X + vector.X,
                point.Y + vector.Y);
        }

        public static SupportDetailVector operator -(
            SupportDetailPoint left, SupportDetailPoint right)
        {
            return new SupportDetailVector(left.X - right.X,
                left.Y - right.Y);
        }
    }

    public struct SupportDetailVector
    {
        public double X;
        public double Y;

        public SupportDetailVector(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y); }
        }

        public SupportDetailVector Unit()
        {
            double length = Length;
            return length < 0.0000001
                ? new SupportDetailVector(1.0, 0.0)
                : new SupportDetailVector(X / length, Y / length);
        }

        public SupportDetailVector Perpendicular()
        {
            return new SupportDetailVector(-Y, X);
        }

        public static SupportDetailVector operator *(
            SupportDetailVector vector, double scalar)
        {
            return new SupportDetailVector(vector.X * scalar,
                vector.Y * scalar);
        }
    }

    public sealed class SupportDetailPanelGeometry
    {
        public int Index { get; internal set; }
        public double StartStation { get; internal set; }
        public double EndStation { get; internal set; }
        public SupportDetailPoint Start { get; internal set; }
        public SupportDetailPoint End { get; internal set; }
    }

    public sealed class SupportDetailRailGeometry
    {
        public int PanelIndex { get; internal set; }
        public int RailIndex { get; internal set; }
        public double Station { get; internal set; }
        public SupportDetailPoint Point { get; internal set; }
    }

    public sealed class SupportDetailPierGeometry
    {
        public int Index { get; internal set; }
        public double Station { get; internal set; }
        public SupportDetailPoint Center { get; internal set; }
        public double TopElevation { get; internal set; }
        public double BeamElevation { get; internal set; }
        public double ActualColumnHeight { get; internal set; }
    }

    public sealed class SupportDetailTriangleGeometry
    {
        public int Index { get; internal set; }
        public double Station { get; internal set; }
        public SupportDetailPoint BeamPoint { get; internal set; }
        public SupportDetailPoint LeftBase { get; internal set; }
        public SupportDetailPoint RightBase { get; internal set; }
        public SupportDetailPoint Apex { get; internal set; }
    }

    public sealed class SupportDetailGeometry
    {
        public SupportDetailSettings Settings { get; internal set; }
        public SupportDetailPoint BeamStart { get; internal set; }
        public SupportDetailPoint BeamEnd { get; internal set; }
        public SupportDetailVector BeamDirection { get; internal set; }
        public SupportDetailVector BeamNormal { get; internal set; }
        public double ArrayLength { get; internal set; }
        public double BeamLength { get; internal set; }
        public double BeamStartStation { get; internal set; }
        public double BeamEndStation { get; internal set; }
        public IList<SupportDetailPanelGeometry> Panels { get; internal set; }
        public IList<SupportDetailRailGeometry> Rails { get; internal set; }
        public IList<SupportDetailPierGeometry> Piers { get; internal set; }
        public IList<SupportDetailTriangleGeometry> Triangles { get; internal set; }
        public IList<string> Warnings { get; internal set; }
    }

    public sealed class SupportDetailBuildResult
    {
        public SupportDetailGeometry Geometry { get; internal set; }
        public IList<string> Errors { get; internal set; }
        public IList<string> Warnings { get; internal set; }
        public bool IsValid
        {
            get { return Geometry != null && Errors.Count == 0; }
        }
    }

    public static class SupportDetailGeometryBuilder
    {
        public static SupportDetailBuildResult Build(
            SupportDetailSettings sourceSettings)
        {
            SupportDetailSettings settings = sourceSettings == null
                ? new SupportDetailSettings() : sourceSettings.Clone();
            settings.Normalize();

            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            Validate(settings, errors);
            if (errors.Count > 0)
            {
                return new SupportDetailBuildResult
                {
                    Errors = errors,
                    Warnings = warnings,
                    Geometry = null
                };
            }

            double arrayLength = settings.ComponentCount * settings.ComponentLength +
                Math.Max(0, settings.ComponentCount - 1) * settings.ComponentGap;
            double angle = settings.SlopeDegrees * Math.PI / 180.0;
            SupportDetailVector direction = new SupportDetailVector(
                Math.Cos(angle), Math.Sin(angle)).Unit();
            SupportDetailVector normal = direction.Perpendicular().Unit();

            IList<double> pierStations = ResolvePierStations(settings,
                arrayLength, warnings);
            IList<double> railOffsets = ResolveRailOffsets(settings, warnings);

            double minimumPierStation = Minimum(pierStations);
            double maximumPierStation = Maximum(pierStations);
            double beamStartStation = minimumPierStation -
                settings.BeamOverhang;
            double beamEndStation = maximumPierStation +
                settings.BeamOverhang;
            double beamLength = beamEndStation - beamStartStation;
            double referenceStation = direction.Y >= 0.0
                ? minimumPierStation : maximumPierStation;
            double referencePierTop = settings.PierHeight;
            double stationZeroY = referencePierTop + settings.ColumnHeight +
                settings.TriangleHeight + settings.BeamWidth / 2.0 -
                referenceStation * direction.Y;
            SupportDetailPoint stationZero = new SupportDetailPoint(0.0,
                stationZeroY);
            SupportDetailPoint beamStart = stationZero +
                direction * beamStartStation;
            SupportDetailPoint beamEnd = stationZero +
                direction * beamEndStation;

            List<SupportDetailPanelGeometry> panels =
                new List<SupportDetailPanelGeometry>();
            for (int index = 0; index < settings.ComponentCount; index++)
            {
                double start = index *
                    (settings.ComponentLength + settings.ComponentGap);
                double end = start + settings.ComponentLength;
                panels.Add(new SupportDetailPanelGeometry
                {
                    Index = index,
                    StartStation = start,
                    EndStation = end,
                    Start = stationZero + direction * start +
                        normal * (settings.BeamWidth / 2.0 +
                            settings.PanelThickness / 2.0 + 8.0),
                    End = stationZero + direction * end +
                        normal * (settings.BeamWidth / 2.0 +
                            settings.PanelThickness / 2.0 + 8.0)
                });
            }

            List<SupportDetailRailGeometry> rails =
                new List<SupportDetailRailGeometry>();
            for (int panelIndex = 0; panelIndex < panels.Count; panelIndex++)
            {
                SupportDetailPanelGeometry panel = panels[panelIndex];
                for (int railIndex = 0; railIndex < railOffsets.Count; railIndex++)
                {
                    double station = panel.StartStation + railOffsets[railIndex];
                    rails.Add(new SupportDetailRailGeometry
                    {
                        PanelIndex = panelIndex,
                        RailIndex = railIndex,
                        Station = station,
                        Point = stationZero + direction * station
                    });
                }
            }

            List<SupportDetailPierGeometry> piers =
                new List<SupportDetailPierGeometry>();
            List<SupportDetailTriangleGeometry> triangles =
                new List<SupportDetailTriangleGeometry>();
            for (int index = 0; index < pierStations.Count; index++)
            {
                double station = pierStations[index];
                SupportDetailPoint beamPoint = stationZero + direction * station;
                double beamElevation = beamPoint.Y - settings.BeamWidth / 2.0;
                double columnTop = beamElevation - settings.TriangleHeight;
                double actualColumnHeight = columnTop - settings.PierHeight;
                if (actualColumnHeight < 0.0)
                {
                    warnings.Add("第 " + (index + 1).ToString(
                        CultureInfo.InvariantCulture) +
                        " 个立柱顶面低于支墩顶面，请检查墩位或立柱高度。");
                }
                piers.Add(new SupportDetailPierGeometry
                {
                    Index = index,
                    Station = station,
                    Center = new SupportDetailPoint(beamPoint.X, 0.0),
                    TopElevation = settings.PierHeight,
                    BeamElevation = beamElevation,
                    ActualColumnHeight = actualColumnHeight
                });

                SupportDetailPoint leftBase = new SupportDetailPoint(
                    beamPoint.X - settings.TriangleBase / 2.0,
                    columnTop);
                SupportDetailPoint rightBase = new SupportDetailPoint(
                    beamPoint.X + settings.TriangleBase / 2.0,
                    columnTop);
                SupportDetailPoint apex = new SupportDetailPoint(
                    beamPoint.X, beamElevation);
                triangles.Add(new SupportDetailTriangleGeometry
                {
                    Index = index,
                    Station = station,
                    BeamPoint = beamPoint,
                    LeftBase = leftBase,
                    RightBase = rightBase,
                    Apex = apex
                });
            }

            SupportDetailGeometry geometry = new SupportDetailGeometry
            {
                Settings = settings,
                BeamStart = beamStart,
                BeamEnd = beamEnd,
                BeamDirection = direction,
                BeamNormal = normal,
                ArrayLength = arrayLength,
                BeamLength = beamLength,
                BeamStartStation = beamStartStation,
                BeamEndStation = beamEndStation,
                Panels = panels,
                Rails = rails,
                Piers = piers,
                Triangles = triangles,
                Warnings = warnings
            };
            return new SupportDetailBuildResult
            {
                Geometry = geometry,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static void Validate(SupportDetailSettings settings,
            IList<string> errors)
        {
            if (settings.ComponentCount < 1)
                errors.Add("组件数量 N 必须至少为 1。");
            if (settings.ComponentLength <= 0.0)
                errors.Add("组件斜长 L 必须大于 0。");
            if (settings.ComponentGap < 0.0)
                errors.Add("组件间缝隙 G 不能为负数。");
            if (settings.RailCenterSpacing <= 0.0)
                errors.Add("导轨中心距 R 必须大于 0。");
            if (settings.ColumnHeight < 0.0)
                errors.Add("立柱高度 H 不能为负数。");
            if (settings.BeamOverhang < 0.0)
                errors.Add("斜梁端部伸出 E 不能为负数。");
            if (settings.PierHeight <= 0.0)
                errors.Add("支墩高度 h 必须大于 0。");
            if (settings.PierPositionMode == SupportDetailPositionMode.Manual &&
                settings.ParsePierStations().Count == 0)
                errors.Add("启用手动墩位时，墩子位置 s 至少需要一个数值。");
            if (settings.RailPositionMode == SupportDetailPositionMode.Manual &&
                settings.ParseRailPositions().Count == 0)
                errors.Add("启用手动夹持位置时，导轨位置至少需要一个数值。");
        }

        private static IList<double> ResolveRailOffsets(
            SupportDetailSettings settings, IList<string> warnings)
        {
            List<double> values = new List<double>();
            if (settings.RailPositionMode == SupportDetailPositionMode.Manual)
            {
                IList<double> manual = settings.ParseRailPositions();
                for (int index = 0; index < manual.Count; index++)
                    values.Add(ClampOffset(manual[index], settings.ComponentLength,
                        warnings, "导轨位置"));
            }
            if (values.Count == 0)
            {
                double first = (settings.ComponentLength -
                    settings.RailCenterSpacing) / 2.0;
                double second = first + settings.RailCenterSpacing;
                values.Add(ClampOffset(first, settings.ComponentLength, warnings,
                    "默认导轨位置"));
                values.Add(ClampOffset(second, settings.ComponentLength, warnings,
                    "默认导轨位置"));
            }
            return values;
        }

        private static double ClampOffset(double value, double maximum,
            IList<string> warnings, string label)
        {
            double clamped = Math.Max(0.0, Math.Min(maximum, value));
            if (Math.Abs(clamped - value) > 0.0001)
                warnings.Add(label + "超出组件范围，已按组件端部修正。");
            return clamped;
        }

        private static IList<double> ResolvePierStations(
            SupportDetailSettings settings, double arrayLength,
            IList<string> warnings)
        {
            List<double> values = new List<double>();
            if (settings.PierPositionMode == SupportDetailPositionMode.Manual)
            {
                IList<double> manual = settings.ParsePierStations();
                for (int index = 0; index < manual.Count; index++)
                    values.Add(manual[index]);
                if (settings.PierCount != values.Count)
                {
                    warnings.Add("手动墩位数量与墩子数量 P 不一致，按手动墩位数量生成。");
                }
            }
            if (values.Count > 0)
            {
                values.Sort();
                return values;
            }

            int count = Math.Max(1, settings.PierCount);
            double center = arrayLength / 2.0;
            double first = center - settings.PierSpacing * (count - 1) / 2.0;
            double last = center + settings.PierSpacing * (count - 1) / 2.0;
            if (first < 0.0 || last > arrayLength)
            {
                warnings.Add(
                    "按当前 P 和 S 计算的外侧墩中心超出组件阵列范围；" +
                    "仍按输入生成，请复核支架受力路径。");
            }
            for (int index = 0; index < count; index++)
                values.Add(first + settings.PierSpacing * index);
            return values;
        }

        private static double Minimum(IList<double> values)
        {
            double result = values[0];
            for (int index = 1; index < values.Count; index++)
                result = Math.Min(result, values[index]);
            return result;
        }

        private static double Maximum(IList<double> values)
        {
            double result = values[0];
            for (int index = 1; index < values.Count; index++)
                result = Math.Max(result, values[index]);
            return result;
        }
    }
}
