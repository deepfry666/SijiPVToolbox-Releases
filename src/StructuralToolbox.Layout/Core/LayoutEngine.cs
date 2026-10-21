using System;
using System.Collections.Generic;
using System.Globalization;

namespace StructuralToolbox.Layout.Core
{
    public enum LayoutOrientation { Vertical, Horizontal }

    public sealed class LayoutOptions
    {
        public double Length = 2382.0, Width = 1134.0, PowerWp = 630.0;
        public double ComponentGapAlong = 20.0, ComponentGapAcross = 20.0;
        public double ArrayGapAlong = 500.0, ArrayGapAcross = 500.0;
        public double SlopeDegrees;
        public double ObstacleClearance;
        public double? MaximumCapacityKwp;
        public LayoutOrientation? Orientation;
        public int[] AllowedRows = new[] { 1, 2 };
        public int StringSize = 18;
        public LayoutVector Direction = new LayoutVector(1, 0);
        public LayoutPoint? StartPoint;
        public double ConflictRatioLimit = 0.05;
        public int ConflictPerArrayLimit = 1;
        public int MinimumConflictGain = 2;

        public void Validate()
        {
            RequirePositive(Length, "组件长度"); RequirePositive(Width, "组件宽度"); RequirePositive(PowerWp, "单块功率");
            RequireNonNegative(ComponentGapAlong, "组件纵向净距"); RequireNonNegative(ComponentGapAcross, "组件横向净距");
            RequireNonNegative(ArrayGapAlong, "阵列纵向净距"); RequireNonNegative(ArrayGapAcross, "阵列横向净距");
            RequireNonNegative(ObstacleClearance, "障碍退距"); if (SlopeDegrees < -89.9 || SlopeDegrees > 89.9) throw new ArgumentOutOfRangeException("SlopeDegrees");
            Direction = Direction.Normalized(); if (StringSize < 1) throw new ArgumentOutOfRangeException("StringSize");
            if (MaximumCapacityKwp.HasValue && MaximumCapacityKwp.Value <= 0.0) throw new ArgumentOutOfRangeException("MaximumCapacityKwp");
            if (AllowedRows == null || AllowedRows.Length == 0) throw new ArgumentException("至少启用一种阵列排数。", "AllowedRows");
            for (int i = 0; i < AllowedRows.Length; i++)
            {
                if (AllowedRows[i] < 1 || AllowedRows[i] > 8)
                    throw new ArgumentOutOfRangeException("AllowedRows");
                if (AllowedRows[i] > StringSize)
                    throw new ArgumentException(
                        "阵列排数不能大于每组串组件数。", "AllowedRows");
            }
        }
        private static void RequirePositive(double value, string name) { if (value <= 0.0 || Double.IsNaN(value) || Double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        private static void RequireNonNegative(double value, string name) { if (value < 0.0 || Double.IsNaN(value) || Double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
    }

    public sealed class LayoutInput
    {
        public LayoutPolygon Boundary;
        public IList<LayoutPolygon> Obstacles = new List<LayoutPolygon>();
        public IList<LayoutCircle> Circles = new List<LayoutCircle>();
        public IList<LayoutPath> LinearObstacles = new List<LayoutPath>();
        public IList<LayoutPolygon> SplitLines = new List<LayoutPolygon>();
    }

    public sealed class LayoutResult
    {
        public LayoutResult() { Rectangles = new List<LayoutRectangle>(); Messages = new List<string>(); }
        public IList<LayoutRectangle> Rectangles { get; private set; }
        public IList<string> Messages { get; private set; }
        public int ComponentCount { get { return Rectangles.Count; } }
        public double CapacityKwp { get; internal set; }
        public int ConflictCount { get; internal set; }
        public string OrientationName { get; internal set; }
        public int Rows { get; internal set; }
        public int ArrayCount { get; internal set; }
    }

    public static class LayoutEngine
    {
        private const double Epsilon = 1e-7;

        public static LayoutResult Solve(LayoutInput input, LayoutOptions options)
        {
            if (input == null || input.Boundary == null) throw new ArgumentNullException("input");
            if (options == null) throw new ArgumentNullException("options"); options.Validate();
            ValidateSimplePolygon(input.Boundary);
            int maximum = options.MaximumCapacityKwp.HasValue ? (int)Math.Floor(options.MaximumCapacityKwp.Value * 1000.0 / options.PowerWp + Epsilon) : Int32.MaxValue;
            LayoutResult bestClean = null;
            LayoutResult bestConflict = null;
            LayoutOrientation[] orientations = options.Orientation.HasValue ? new[] { options.Orientation.Value } : new[] { LayoutOrientation.Vertical, LayoutOrientation.Horizontal };
            for (int oi = 0; oi < orientations.Length; oi++)
                for (int ri = 0; ri < options.AllowedRows.Length; ri++)
                {
                    int rows = options.AllowedRows[ri];
                    LayoutResult clean = SolveGrid(input, options, orientations[oi], rows, maximum, false);
                    LayoutResult conflict = SolveGrid(input, options, orientations[oi], rows, maximum, true);
                    if (IsBetter(clean, bestClean, options)) bestClean = clean;
                    if (IsBetter(conflict, bestConflict, options)) bestConflict = conflict;
                }
            LayoutResult best = bestClean;
            if (bestConflict != null && bestConflict.ConflictCount > 0 &&
                (bestClean == null || bestConflict.ComponentCount - bestClean.ComponentCount >= options.MinimumConflictGain) &&
                IsBetter(bestConflict, best, options))
                best = bestConflict;
            if (best == null) best = new LayoutResult();
            best.CapacityKwp = best.ComponentCount * options.PowerWp / 1000.0;
            best.ConflictCount = CountConflicts(best.Rectangles);
            best.Messages.Add(String.Format(CultureInfo.InvariantCulture, "首版规则：容量硬上限；{0} 块冲突组件，整齐度仅在数量相同时参与排序。", best.ConflictCount));
            return best;
        }

        private static LayoutResult SolveGrid(LayoutInput input, LayoutOptions o,
            LayoutOrientation orientation, int rows, int maximum,
            bool allowConflicts)
        {
            LayoutVector direction = o.Direction.Normalized();
            LayoutVector acrossDirection =
                new LayoutVector(-direction.Y, direction.X);
            double alongSize = (orientation == LayoutOrientation.Vertical
                ? o.Length : o.Width) *
                Math.Cos(o.SlopeDegrees * Math.PI / 180.0);
            double acrossSize = orientation == LayoutOrientation.Vertical
                ? o.Width : o.Length;
            double rowPitch = alongSize + o.ComponentGapAlong;
            double columnPitch = acrossSize + o.ComponentGapAcross;
            double rowExtent = (rows - 1) * rowPitch + alongSize;
            double minU, maxU, minV, maxV;
            Project(input.Boundary, direction, acrossDirection, out minU,
                out maxU, out minV, out maxV);

            IList<double> originsU = o.StartPoint.HasValue
                ? new[] { Dot(o.StartPoint.Value, direction) }
                : BuildOrigins(minU, maxU, rowExtent, rowPitch);
            IList<double> originsV = o.StartPoint.HasValue
                ? new[] { Dot(o.StartPoint.Value, acrossDirection) }
                : BuildOrigins(minV, maxV, acrossSize, columnPitch);
            LayoutResult best = null;
            for (int ui = 0; ui < originsU.Count; ui++)
                for (int vi = 0; vi < originsV.Count; vi++)
                {
                    LayoutResult candidate = SolveGridAtOrigin(input, o,
                        orientation, rows, maximum, allowConflicts, direction,
                        acrossDirection, alongSize, acrossSize, rowPitch,
                        columnPitch, rowExtent, originsU[ui], originsV[vi],
                        maxU, maxV);
                    if (IsBetter(candidate, best, o))
                        best = candidate;
                }
            return best ?? NewResult(orientation, rows);
        }

        private static LayoutResult SolveGridAtOrigin(LayoutInput input,
            LayoutOptions o, LayoutOrientation orientation, int rows,
            int maximum, bool allowConflicts, LayoutVector direction,
            LayoutVector acrossDirection, double alongSize, double acrossSize,
            double rowPitch, double columnPitch, double rowExtent,
            double originU, double originV, double maxU, double maxV)
        {
            List<ArrayCandidate> candidates = new List<ArrayCandidate>();
            int targetColumns = Math.Max(1, o.StringSize / rows);
            double uStart = originU;
            while (uStart + rowExtent <= maxU + Epsilon)
            {
                double vStart = originV;
                while (vStart + acrossSize <= maxV + Epsilon)
                {
                    List<LayoutRectangle> block =
                        new List<LayoutRectangle>();
                    int conflicts = 0;
                    int columns = 0;
                    while (columns < targetColumns &&
                        vStart + columns * columnPitch + acrossSize <=
                            maxV + Epsilon)
                    {
                        List<LayoutRectangle> next =
                            new List<LayoutRectangle>();
                        int nextConflicts = conflicts;
                        bool validColumn = true;
                        for (int row = 0; row < rows; row++)
                        {
                            double u = uStart + row * rowPitch +
                                alongSize / 2.0;
                            double v = vStart + columns * columnPitch +
                                acrossSize / 2.0;
                            LayoutRectangle panel = MakeRectangle(u, v,
                                direction, acrossDirection, alongSize,
                                acrossSize, false, String.Empty);
                            if (!IsInside(input.Boundary, panel.Corners))
                            {
                                validColumn = false;
                                break;
                            }
                            if (IntersectsObstacle(input, panel,
                                o.ObstacleClearance))
                            {
                                if (!allowConflicts)
                                {
                                    validColumn = false;
                                    break;
                                }
                                nextConflicts++;
                                panel = MakeRectangle(u, v, direction,
                                    acrossDirection, alongSize, acrossSize,
                                    true, String.Empty);
                            }
                            next.Add(panel);
                        }
                        if (!validColumn ||
                            nextConflicts > o.ConflictPerArrayLimit)
                            break;
                        block.AddRange(next);
                        conflicts = nextConflicts;
                        columns++;
                    }
                    if (columns == 0)
                    {
                        vStart += columnPitch;
                        continue;
                    }
                    candidates.Add(new ArrayCandidate(block, columns,
                        conflicts));
                    vStart += (columns - 1) * columnPitch + acrossSize +
                        o.ArrayGapAcross;
                }
                uStart += rowExtent + o.ArrayGapAlong;
            }
            LayoutResult result = SelectCandidates(candidates, orientation,
                rows, maximum, o);
            result.ConflictCount = CountConflicts(result.Rectangles);
            result.ArrayCount = CountArrays(result.Rectangles);
            return result;
        }

        private sealed class ArrayCandidate
        {
            public ArrayCandidate(IList<LayoutRectangle> rectangles,
                int columns, int conflicts)
            {
                Rectangles = rectangles;
                Columns = columns;
                Conflicts = conflicts;
            }

            public IList<LayoutRectangle> Rectangles;
            public int Columns;
            public int Conflicts;
        }

        private sealed class SelectionNode
        {
            public SelectionNode Previous;
            public int CandidateIndex;
            public int StartColumn;
            public int SelectedColumns;
            public int TotalColumns;
            public int Conflicts;
            public int ArrayCount;
            public bool TailUsed;
            public long Compactness;
        }

        private static LayoutResult SelectCandidates(
            IList<ArrayCandidate> candidates, LayoutOrientation orientation,
            int rows, int maximum, LayoutOptions options)
        {
            LayoutResult result = NewResult(orientation, rows);
            if (candidates.Count == 0 || maximum < rows)
                return result;

            int totalAvailableColumns = 0;
            for (int i = 0; i < candidates.Count; i++)
                totalAvailableColumns += candidates[i].Columns;
            int capacityColumns = maximum == Int32.MaxValue
                ? totalAvailableColumns
                : Math.Min(totalAvailableColumns, maximum / rows);
            if (capacityColumns <= 0)
                return result;

            SelectionNode[,] states = new SelectionNode[capacityColumns + 1, 2];
            states[0, 0] = new SelectionNode();
            for (int candidateIndex = 0; candidateIndex < candidates.Count;
                candidateIndex++)
            {
                ArrayCandidate candidate = candidates[candidateIndex];
                SelectionNode[,] next = new SelectionNode[capacityColumns + 1, 2];
                for (int total = 0; total <= capacityColumns; total++)
                    for (int tail = 0; tail < 2; tail++)
                    {
                        SelectionNode state = states[total, tail];
                        if (state == null) continue;
                        KeepBetter(next, state);
                        if (total + candidate.Columns <= capacityColumns)
                            KeepBetter(next, ExtendSelection(state,
                                candidateIndex, 0, candidate.Columns,
                                candidate.Conflicts, false));
                        if (!state.TailUsed)
                            for (int selected = 1;
                                selected < candidate.Columns; selected++)
                            {
                                int start;
                                int conflicts = BestWindow(candidate, rows,
                                    selected, out start);
                                if (total + selected <= capacityColumns)
                                    KeepBetter(next, ExtendSelection(state,
                                        candidateIndex, start, selected,
                                        conflicts, true));
                            }
                    }
                states = next;
            }

            SelectionNode best = null;
            for (int total = capacityColumns; total >= 0 && best == null;
                total--)
                for (int tail = 0; tail < 2; tail++)
                {
                    SelectionNode state = states[total, tail];
                    if (state == null) continue;
                    int componentCount = state.TotalColumns * rows;
                    int allowedConflicts = Math.Max(1, (int)Math.Floor(
                        componentCount * options.ConflictRatioLimit +
                        Epsilon));
                    if (state.Conflicts > 0 &&
                        state.Conflicts > allowedConflicts)
                        continue;
                    if (IsBetterSelection(state, best))
                        best = state;
                }
            if (best == null)
                return result;

            List<SelectionNode> chosen = new List<SelectionNode>();
            for (SelectionNode node = best; node != null &&
                node.SelectedColumns > 0; node = node.Previous)
                chosen.Add(node);
            chosen.Reverse();
            for (int i = 0; i < chosen.Count; i++)
            {
                SelectionNode choice = chosen[i];
                ArrayCandidate candidate = candidates[choice.CandidateIndex];
                string arrayId = "A" + (i + 1).ToString(
                    CultureInfo.InvariantCulture);
                int first = choice.StartColumn * rows;
                int count = choice.SelectedColumns * rows;
                for (int j = first; j < first + count; j++)
                {
                    LayoutRectangle source = candidate.Rectangles[j];
                    result.Rectangles.Add(new LayoutRectangle(source.Center,
                        source.Along, source.Across, source.Length,
                        source.Width, source.Conflict, arrayId));
                }
            }
            return result;
        }

        private static SelectionNode ExtendSelection(SelectionNode previous,
            int candidateIndex, int startColumn, int selectedColumns,
            int conflicts, bool tailUsed)
        {
            return new SelectionNode
            {
                Previous = previous,
                CandidateIndex = candidateIndex,
                StartColumn = startColumn,
                SelectedColumns = selectedColumns,
                TotalColumns = previous.TotalColumns + selectedColumns,
                Conflicts = previous.Conflicts + conflicts,
                ArrayCount = previous.ArrayCount + 1,
                TailUsed = previous.TailUsed || tailUsed,
                Compactness = previous.Compactness +
                    (long)selectedColumns * selectedColumns
            };
        }

        private static int BestWindow(ArrayCandidate candidate, int rows,
            int selectedColumns, out int bestStart)
        {
            int bestConflicts = Int32.MaxValue;
            bestStart = 0;
            for (int start = 0;
                start + selectedColumns <= candidate.Columns; start++)
            {
                int conflicts = 0;
                int first = start * rows;
                int count = selectedColumns * rows;
                for (int i = first; i < first + count; i++)
                    if (candidate.Rectangles[i].Conflict) conflicts++;
                if (conflicts < bestConflicts)
                {
                    bestConflicts = conflicts;
                    bestStart = start;
                }
            }
            return bestConflicts;
        }

        private static void KeepBetter(SelectionNode[,] states,
            SelectionNode candidate)
        {
            int tail = candidate.TailUsed ? 1 : 0;
            SelectionNode current = states[candidate.TotalColumns, tail];
            if (IsBetterSelection(candidate, current))
                states[candidate.TotalColumns, tail] = candidate;
        }

        private static bool IsBetterSelection(SelectionNode candidate,
            SelectionNode current)
        {
            if (current == null) return true;
            if (candidate.TotalColumns != current.TotalColumns)
                return candidate.TotalColumns > current.TotalColumns;
            if (candidate.Conflicts != current.Conflicts)
                return candidate.Conflicts < current.Conflicts;
            if (candidate.ArrayCount != current.ArrayCount)
                return candidate.ArrayCount < current.ArrayCount;
            if (candidate.TailUsed != current.TailUsed)
                return !candidate.TailUsed;
            if (candidate.Compactness != current.Compactness)
                return candidate.Compactness > current.Compactness;
            return false;
        }

        private static LayoutResult NewResult(LayoutOrientation orientation,
            int rows)
        {
            LayoutResult result = new LayoutResult();
            result.OrientationName = orientation == LayoutOrientation.Vertical
                ? "竖排" : "横排";
            result.Rows = rows;
            return result;
        }

        private static IList<double> BuildOrigins(double minimum,
            double maximum, double extent, double pitch)
        {
            List<double> result = new List<double>();
            AddOrigin(result, minimum, minimum, maximum, extent);
            AddOrigin(result, maximum - extent, minimum, maximum, extent);
            AddOrigin(result, minimum + pitch / 2.0, minimum, maximum, extent);
            AddOrigin(result, maximum - extent - pitch / 2.0, minimum,
                maximum, extent);
            return result;
        }

        private static void AddOrigin(ICollection<double> values,
            double candidate, double minimum, double maximum, double extent)
        {
            if (candidate < minimum - Epsilon ||
                candidate + extent > maximum + Epsilon)
                return;
            foreach (double value in values)
                if (Math.Abs(value - candidate) <= Epsilon)
                    return;
            values.Add(candidate);
        }

        private static LayoutRectangle MakeRectangle(double u, double v,
            LayoutVector direction, LayoutVector acrossDirection,
            double length, double width, bool conflict, string id)
        {
            return new LayoutRectangle(new LayoutPoint(
                direction.X * u + acrossDirection.X * v,
                direction.Y * u + acrossDirection.Y * v), direction,
                acrossDirection, length, width, conflict, id);
        }

        private static bool IsBetter(LayoutResult a, LayoutResult b,
            LayoutOptions options)
        {
            if (b == null) return true;
            if (a.ComponentCount != b.ComponentCount)
                return a.ComponentCount > b.ComponentCount;
            if (a.ConflictCount != b.ConflictCount)
                return a.ConflictCount < b.ConflictCount;
            if (a.ArrayCount != b.ArrayCount)
                return a.ArrayCount < b.ArrayCount;
            if (a.Rows != b.Rows)
                return a.Rows < b.Rows;
            if (a.Rectangles.Count == 0) return false;
            if (!String.Equals(a.OrientationName, b.OrientationName,
                StringComparison.Ordinal))
                return String.Equals(a.OrientationName, "竖排",
                    StringComparison.Ordinal);
            return false;
        }

        private static int CountConflicts(
            IList<LayoutRectangle> rectangles)
        {
            int count = 0;
            for (int i = 0; i < rectangles.Count; i++)
                if (rectangles[i].Conflict) count++;
            return count;
        }

        private static int CountArrays(IList<LayoutRectangle> rectangles)
        {
            HashSet<string> ids =
                new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rectangles.Count; i++)
                ids.Add(rectangles[i].ArrayId);
            return ids.Count;
        }
        private static double Dot(LayoutPoint point, LayoutVector vector) { return point.X * vector.X + point.Y * vector.Y; }
        private static double Dot(LayoutVector first, LayoutVector second) { return first.X * second.X + first.Y * second.Y; }
        private static void Project(LayoutPolygon polygon, LayoutVector d, LayoutVector p, out double minU, out double maxU, out double minV, out double maxV)
        { minU = minV = Double.MaxValue; maxU = maxV = Double.MinValue; for (int i = 0; i < polygon.Points.Count; i++) { double u = Dot(polygon.Points[i], d), v = Dot(polygon.Points[i], p); minU = Math.Min(minU, u); maxU = Math.Max(maxU, u); minV = Math.Min(minV, v); maxV = Math.Max(maxV, v); } }
        private static bool IsInside(LayoutPolygon polygon, IList<LayoutPoint> corners)
        {
            for (int i = 0; i < corners.Count; i++) if (!Contains(polygon, corners[i])) return false;
            LayoutPoint center = new LayoutPoint(
                (corners[0].X + corners[2].X) / 2.0,
                (corners[0].Y + corners[2].Y) / 2.0);
            if (!Contains(polygon, center)) return false;
            for (int i = 0; i < corners.Count; i++)
            {
                LayoutPoint a = corners[i], b = corners[(i + 1) % corners.Count];
                if (!Contains(polygon, new LayoutPoint(
                    (a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0)))
                    return false;
                for (int j = 0; j < polygon.Points.Count; j++)
                    if (SegmentsProperlyCross(a, b, polygon.Points[j], polygon.Points[(j + 1) % polygon.Points.Count])) return false;
            }
            for (int j = 0; j < polygon.Points.Count; j++) if (PointStrictlyInRectangle(polygon.Points[j], corners)) return false;
            return true;
        }
        private static bool Contains(LayoutPolygon polygon, LayoutPoint point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Points.Count - 1;
                i < polygon.Points.Count; j = i++)
            {
                LayoutPoint a = polygon.Points[i];
                LayoutPoint b = polygon.Points[j];
                bool crosses = ((a.Y > point.Y) != (b.Y > point.Y)) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) /
                        (b.Y - a.Y) + a.X;
                if (crosses) inside = !inside;
            }
            return inside || OnBoundary(polygon, point);
        }

        private static bool OnBoundary(LayoutPolygon polygon, LayoutPoint point)
        {
            for (int i = 0; i < polygon.Points.Count; i++)
                if (PointOnSegment(point, polygon.Points[i],
                    polygon.Points[(i + 1) % polygon.Points.Count]))
                    return true;
            return false;
        }
        private static bool IntersectsObstacle(LayoutInput input,
            LayoutRectangle rectangle, double clearance)
        {
            IList<LayoutPoint> corners = rectangle.Corners;
            for (int i = 0; i < input.Obstacles.Count; i++)
            {
                LayoutPolygon obstacle = input.Obstacles[i];
                for (int j = 0; j < corners.Count; j++)
                    if (Contains(obstacle, corners[j])) return true;
                for (int j = 0; j < obstacle.Points.Count; j++)
                    if (PointInRectangle(obstacle.Points[j], rectangle, 0.0))
                        return true;
                for (int j = 0; j < corners.Count; j++)
                    for (int k = 0; k < obstacle.Points.Count; k++)
                    {
                        LayoutPoint firstStart = corners[j];
                        LayoutPoint firstEnd = corners[(j + 1) % corners.Count];
                        LayoutPoint secondStart = obstacle.Points[k];
                        LayoutPoint secondEnd = obstacle.Points[
                            (k + 1) % obstacle.Points.Count];
                        if (SegmentsCross(firstStart, firstEnd, secondStart,
                            secondEnd)) return true;
                        if (clearance > 0.0 && SegmentDistance(firstStart,
                            firstEnd, secondStart, secondEnd) <=
                                clearance + Epsilon) return true;
                    }
            }
            for (int i = 0; i < input.Circles.Count; i++)
            {
                LayoutCircle circle = input.Circles[i]; double du = Dot(circle.Center - rectangle.Center, rectangle.Along), dv = Dot(circle.Center - rectangle.Center, rectangle.Across);
                double hu = rectangle.Length / 2.0;
                double hv = rectangle.Width / 2.0;
                double cu = Math.Max(-hu, Math.Min(hu, du)), cv = Math.Max(-hv, Math.Min(hv, dv));
                double protectedRadius = circle.Radius + clearance;
                if ((du - cu) * (du - cu) + (dv - cv) * (dv - cv) <=
                    protectedRadius * protectedRadius + Epsilon) return true;
            }
            double pipeProtectedRadius = clearance;
            for (int i = 0; i < input.LinearObstacles.Count; i++)
            {
                LayoutPath path = input.LinearObstacles[i];
                for (int j = 0; j < path.Points.Count - 1; j++)
                {
                    LayoutPoint start = path.Points[j];
                    LayoutPoint end = path.Points[j + 1];
                    if (PointInRectangle(start, rectangle,
                        pipeProtectedRadius) || PointInRectangle(end,
                        rectangle, pipeProtectedRadius)) return true;
                    for (int k = 0; k < corners.Count; k++)
                        if (SegmentDistance(start, end, corners[k],
                            corners[(k + 1) % corners.Count]) <=
                                pipeProtectedRadius + Epsilon) return true;
                }
            }
            return false;
        }
        private static bool PointInRectangle(LayoutPoint point, LayoutRectangle rectangle, double clearance) { double u = Math.Abs(Dot(point - rectangle.Center, rectangle.Along)), v = Math.Abs(Dot(point - rectangle.Center, rectangle.Across)); return u <= rectangle.Length / 2.0 + clearance && v <= rectangle.Width / 2.0 + clearance; }
        private static bool PointStrictlyInRectangle(LayoutPoint point, IList<LayoutPoint> corners)
        {
            LayoutVector axis = corners[1] - corners[0], across = corners[3] - corners[0];
            double length = axis.Length, width = across.Length; if (length <= Epsilon || width <= Epsilon) return false;
            LayoutPoint center = new LayoutPoint((corners[0].X + corners[2].X) / 2.0, (corners[0].Y + corners[2].Y) / 2.0);
            LayoutVector delta = point - center; return Math.Abs(delta.X * axis.X + delta.Y * axis.Y) < length * length / 2.0 - Epsilon && Math.Abs(delta.X * across.X + delta.Y * across.Y) < width * width / 2.0 - Epsilon;
        }
        private static bool SegmentsCross(LayoutPoint a, LayoutPoint b, LayoutPoint c, LayoutPoint d)
        {
            double ab1 = Cross(b - a, c - a), ab2 = Cross(b - a, d - a), cd1 = Cross(d - c, a - c), cd2 = Cross(d - c, b - c);
            if (((ab1 > Epsilon && ab2 < -Epsilon) ||
                 (ab1 < -Epsilon && ab2 > Epsilon)) &&
                ((cd1 > Epsilon && cd2 < -Epsilon) ||
                 (cd1 < -Epsilon && cd2 > Epsilon))) return true;
            return (Math.Abs(ab1) <= Epsilon && PointOnSegment(c, a, b)) ||
                (Math.Abs(ab2) <= Epsilon && PointOnSegment(d, a, b)) ||
                (Math.Abs(cd1) <= Epsilon && PointOnSegment(a, c, d)) ||
                (Math.Abs(cd2) <= Epsilon && PointOnSegment(b, c, d));
        }
        private static double Cross(LayoutVector a, LayoutVector b) { return a.X * b.Y - a.Y * b.X; }
        private static bool SegmentsProperlyCross(LayoutPoint a, LayoutPoint b, LayoutPoint c, LayoutPoint d)
        {
            double first = Cross(b - a, c - a), second = Cross(b - a, d - a), third = Cross(d - c, a - c), fourth = Cross(d - c, b - c);
            return ((first > Epsilon && second < -Epsilon) || (first < -Epsilon && second > Epsilon)) && ((third > Epsilon && fourth < -Epsilon) || (third < -Epsilon && fourth > Epsilon));
        }
        private static bool PointOnSegment(LayoutPoint point, LayoutPoint start,
            LayoutPoint end)
        {
            LayoutVector edge = end - start;
            LayoutVector offset = point - start;
            return Math.Abs(Cross(edge, offset)) <= Epsilon &&
                point.X >= Math.Min(start.X, end.X) - Epsilon &&
                point.X <= Math.Max(start.X, end.X) + Epsilon &&
                point.Y >= Math.Min(start.Y, end.Y) - Epsilon &&
                point.Y <= Math.Max(start.Y, end.Y) + Epsilon;
        }

        private static double SegmentDistance(LayoutPoint firstStart,
            LayoutPoint firstEnd, LayoutPoint secondStart,
            LayoutPoint secondEnd)
        {
            if (SegmentsCross(firstStart, firstEnd, secondStart, secondEnd))
                return 0.0;
            return Math.Min(Math.Min(
                PointSegmentDistance(firstStart, secondStart, secondEnd),
                PointSegmentDistance(firstEnd, secondStart, secondEnd)),
                Math.Min(PointSegmentDistance(secondStart, firstStart,
                    firstEnd), PointSegmentDistance(secondEnd, firstStart,
                    firstEnd)));
        }

        private static double PointSegmentDistance(LayoutPoint point,
            LayoutPoint start, LayoutPoint end)
        {
            LayoutVector edge = end - start;
            double squaredLength = edge.X * edge.X + edge.Y * edge.Y;
            if (squaredLength <= Epsilon)
                return (point - start).Length;
            LayoutVector offset = point - start;
            double ratio = Math.Max(0.0, Math.Min(1.0,
                (offset.X * edge.X + offset.Y * edge.Y) / squaredLength));
            LayoutPoint closest = start + edge * ratio;
            return (point - closest).Length;
        }
        private static void ValidateSimplePolygon(LayoutPolygon polygon)
        {
            double area = 0.0; for (int i = 0; i < polygon.Points.Count; i++) { LayoutPoint a = polygon.Points[i], b = polygon.Points[(i + 1) % polygon.Points.Count]; area += a.X * b.Y - b.X * a.Y; }
            if (Math.Abs(area) <= Epsilon) throw new ArgumentException("屋面边界零面积。", "Boundary");
            for (int i = 0; i < polygon.Points.Count; i++) for (int j = i + 1; j < polygon.Points.Count; j++)
                if (j != i + 1 && !(i == 0 && j == polygon.Points.Count - 1) && SegmentsCross(polygon.Points[i], polygon.Points[(i + 1) % polygon.Points.Count], polygon.Points[j], polygon.Points[(j + 1) % polygon.Points.Count]))
                    throw new ArgumentException("屋面边界存在自交。", "Boundary");
        }
    }
}
