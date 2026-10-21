using System;
using System.Collections.Generic;

namespace StructuralToolbox.Core
{
    internal struct BlockedLaneInterval
    {
        internal BlockedLaneInterval(int firstLane, int lastLane)
        {
            if (firstLane < 0)
                throw new ArgumentOutOfRangeException("firstLane");
            if (lastLane < firstLane)
                throw new ArgumentOutOfRangeException("lastLane");
            FirstLane = firstLane;
            LastLane = lastLane;
        }

        internal int FirstLane;
        internal int LastLane;
    }

    internal static class DeterministicLaneBandFinder
    {
        internal static bool TryCreateBlockedInterval(
            double lowerDistance, double upperDistance, double laneSpacing,
            out BlockedLaneInterval interval)
        {
            interval = new BlockedLaneInterval();
            if (Double.IsNaN(lowerDistance) ||
                Double.IsInfinity(lowerDistance))
            {
                throw new ArgumentOutOfRangeException("lowerDistance");
            }
            if (Double.IsNaN(upperDistance) ||
                Double.IsInfinity(upperDistance))
            {
                throw new ArgumentOutOfRangeException("upperDistance");
            }
            if (Double.IsNaN(laneSpacing) ||
                Double.IsInfinity(laneSpacing) || laneSpacing <= 0.0)
            {
                throw new ArgumentOutOfRangeException("laneSpacing");
            }
            if (upperDistance < lowerDistance)
                throw new ArgumentOutOfRangeException("upperDistance");
            if (upperDistance < 0.0)
                return false;

            double scaledLower = lowerDistance / laneSpacing;
            double scaledUpper = upperDistance / laneSpacing;
            if (scaledLower > Int32.MaxValue)
                return false;

            double tolerance = 1e-9 * Math.Max(1.0,
                Math.Max(Math.Abs(scaledLower), Math.Abs(scaledUpper)));
            double firstValue = Math.Ceiling(scaledLower - tolerance);
            double lastValue = Math.Floor(scaledUpper + tolerance);
            if (lastValue < 0.0 || firstValue > Int32.MaxValue)
                return false;

            int firstLane = firstValue <= 0.0 ? 0 : (int)firstValue;
            int lastLane = lastValue >= Int32.MaxValue
                ? Int32.MaxValue
                : (int)lastValue;
            if (lastLane < firstLane)
                return false;

            interval = new BlockedLaneInterval(firstLane, lastLane);
            return true;
        }

        internal static int FindEarliestClearBand(
            IList<BlockedLaneInterval> blockedIntervals,
            int maximumRelativeLane)
        {
            if (blockedIntervals == null)
                throw new ArgumentNullException("blockedIntervals");
            if (maximumRelativeLane < 0)
                throw new ArgumentOutOfRangeException("maximumRelativeLane");

            List<BlockedLaneInterval> ordered =
                new List<BlockedLaneInterval>(blockedIntervals);
            ordered.Sort(delegate(BlockedLaneInterval left,
                BlockedLaneInterval right)
            {
                int comparison = left.FirstLane.CompareTo(right.FirstLane);
                return comparison != 0
                    ? comparison
                    : left.LastLane.CompareTo(right.LastLane);
            });

            List<BlockedLaneInterval> merged =
                new List<BlockedLaneInterval>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                BlockedLaneInterval current = ordered[index];
                if (merged.Count == 0)
                {
                    merged.Add(current);
                    continue;
                }

                int lastIndex = merged.Count - 1;
                BlockedLaneInterval previous = merged[lastIndex];
                long nextLane = (long)previous.LastLane + 1L;
                if (current.FirstLane > nextLane)
                {
                    merged.Add(current);
                    continue;
                }
                if (current.LastLane > previous.LastLane)
                {
                    previous.LastLane = current.LastLane;
                    merged[lastIndex] = previous;
                }
            }

            long candidateBase = 0L;
            for (int index = 0; index < merged.Count; index++)
            {
                BlockedLaneInterval blocked = merged[index];
                if (blocked.LastLane < candidateBase)
                    continue;
                long candidateEnd = candidateBase + maximumRelativeLane;
                if (candidateEnd < blocked.FirstLane)
                    return (int)candidateBase;
                candidateBase = (long)blocked.LastLane + 1L;
                if (candidateBase + maximumRelativeLane > Int32.MaxValue)
                {
                    throw new InvalidOperationException(
                        "No representable physical lane can contain the clear band.");
                }
            }
            return (int)candidateBase;
        }
    }

    public sealed class DeterministicLaneSolverResult
    {
        private readonly int[] _lanes;

        internal DeterministicLaneSolverResult(bool success, int[] lanes,
            long visitedNodes, long backtracks, long constraintChecks,
            bool searchBudgetExhausted)
        {
            Success = success;
            _lanes = lanes == null ? new int[0] : (int[])lanes.Clone();
            VisitedNodes = visitedNodes;
            Backtracks = backtracks;
            ConstraintChecks = constraintChecks;
            SearchBudgetExhausted = searchBudgetExhausted;
        }

        public bool Success { get; private set; }

        public int[] Lanes
        {
            get { return (int[])_lanes.Clone(); }
        }

        public long VisitedNodes { get; private set; }

        public long Backtracks { get; private set; }

        public long ConstraintChecks { get; private set; }

        public bool SearchBudgetExhausted { get; private set; }
    }

    public static class DeterministicLaneSolver
    {
        public static DeterministicLaneSolverResult Solve(
            int entryCount, int maximumLane, bool[,] availability,
            Func<int, int, int, int, bool> hasConflict)
        {
            ValidateCounts(entryCount, maximumLane);
            if (availability == null)
                throw new ArgumentNullException("availability");
            if (hasConflict == null)
                throw new ArgumentNullException("hasConflict");

            int laneCount = maximumLane + 1;
            if (availability.GetLength(0) != entryCount ||
                availability.GetLength(1) != laneCount)
            {
                throw new ArgumentException(
                    "Availability dimensions must be entryCount by maximumLane + 1.",
                    "availability");
            }

            return SolveValidated(entryCount, laneCount,
                delegate(int entryIndex, int laneIndex)
                {
                    return availability[entryIndex, laneIndex];
                }, hasConflict, null, Int64.MaxValue, Int64.MaxValue, true,
                false, false);
        }

        public static DeterministicLaneSolverResult Solve(
            int entryCount, int maximumLane,
            Func<int, int, bool> isLaneAvailable,
            Func<int, int, int, int, bool> hasConflict)
        {
            ValidateCounts(entryCount, maximumLane);
            if (isLaneAvailable == null)
                throw new ArgumentNullException("isLaneAvailable");
            if (hasConflict == null)
                throw new ArgumentNullException("hasConflict");

            return SolveValidated(entryCount, maximumLane + 1,
                isLaneAvailable, hasConflict, null, Int64.MaxValue,
                Int64.MaxValue, true, false, false);
        }

        public static DeterministicLaneSolverResult SolveSparse(
            int entryCount, int maximumLane,
            Func<int, int, bool> isLaneAvailable,
            Func<int, int, int, int, bool> hasConflict,
            IList<int>[] neighbors, long maximumVisitedNodes,
            long maximumConstraintChecks)
        {
            ValidateCounts(entryCount, maximumLane);
            if (isLaneAvailable == null)
                throw new ArgumentNullException("isLaneAvailable");
            if (hasConflict == null)
                throw new ArgumentNullException("hasConflict");
            if (maximumVisitedNodes <= 0)
                throw new ArgumentOutOfRangeException("maximumVisitedNodes");
            if (maximumConstraintChecks <= 0)
                throw new ArgumentOutOfRangeException("maximumConstraintChecks");

            IList<int>[] normalizedNeighbors = NormalizeNeighbors(
                entryCount, neighbors);
            return SolveValidated(entryCount, maximumLane + 1,
                isLaneAvailable, hasConflict, normalizedNeighbors,
                maximumVisitedNodes, maximumConstraintChecks, false, false,
                false);
        }

        internal static DeterministicLaneSolverResult SolveSparseSymmetric(
            int entryCount, int maximumLane,
            Func<int, int, bool> isLaneAvailable,
            Func<int, int, int, int, bool> hasConflict,
            IList<int>[] neighbors, long maximumVisitedNodes,
            long maximumConstraintChecks, bool useCliqueLowerBound)
        {
            ValidateCounts(entryCount, maximumLane);
            if (isLaneAvailable == null)
                throw new ArgumentNullException("isLaneAvailable");
            if (hasConflict == null)
                throw new ArgumentNullException("hasConflict");
            if (maximumVisitedNodes <= 0)
                throw new ArgumentOutOfRangeException("maximumVisitedNodes");
            if (maximumConstraintChecks <= 0)
                throw new ArgumentOutOfRangeException("maximumConstraintChecks");

            IList<int>[] normalizedNeighbors = NormalizeNeighbors(
                entryCount, neighbors);
            return SolveValidated(entryCount, maximumLane + 1,
                isLaneAvailable, hasConflict, normalizedNeighbors,
                maximumVisitedNodes, maximumConstraintChecks,
                useCliqueLowerBound, true, true);
        }

        private static DeterministicLaneSolverResult SolveValidated(
            int entryCount, int laneCount,
            Func<int, int, bool> isLaneAvailable,
            Func<int, int, int, int, bool> hasConflict,
            IList<int>[] neighbors, long maximumVisitedNodes,
            long maximumConstraintChecks, bool useCliqueLowerBound,
            bool conflictCallbackIsSymmetric,
            bool useLeastConstrainingValue)
        {
            if (entryCount == 0)
                return new DeterministicLaneSolverResult(
                    true, new int[0], 0L, 0L, 0L, false);

            bool[,] domains = new bool[entryCount, laneCount];
            int[] domainCounts = new int[entryCount];
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
                {
                    bool available = isLaneAvailable(entryIndex, laneIndex);
                    domains[entryIndex, laneIndex] = available;
                    if (available)
                        domainCounts[entryIndex]++;
                }

                if (domainCounts[entryIndex] == 0)
                    return Failure(0L, 0L, 0L, false);
            }

            SolverState state = new SolverState(
                entryCount, laneCount, domains, domainCounts, hasConflict,
                neighbors, maximumVisitedNodes, maximumConstraintChecks,
                conflictCallbackIsSymmetric, useLeastConstrainingValue);
            if (useCliqueLowerBound &&
                state.FindGreedyCliqueLowerBound() > laneCount)
            {
                return Failure(0L, 0L, state.ConstraintChecks,
                    state.SearchBudgetExhausted);
            }

            if (!state.Search(0))
                return Failure(state.VisitedNodes, state.Backtracks,
                    state.ConstraintChecks, state.SearchBudgetExhausted);

            return new DeterministicLaneSolverResult(true,
                state.Assignments, state.VisitedNodes, state.Backtracks,
                state.ConstraintChecks, false);
        }

        private static DeterministicLaneSolverResult Failure(
            long visitedNodes, long backtracks, long constraintChecks,
            bool searchBudgetExhausted)
        {
            return new DeterministicLaneSolverResult(
                false, new int[0], visitedNodes, backtracks,
                constraintChecks, searchBudgetExhausted);
        }

        private static IList<int>[] NormalizeNeighbors(int entryCount,
            IList<int>[] neighbors)
        {
            if (neighbors == null)
                throw new ArgumentNullException("neighbors");
            if (neighbors.Length != entryCount)
            {
                throw new ArgumentException(
                    "Neighbor list length must equal entryCount.",
                    "neighbors");
            }

            HashSet<int>[] normalized = new HashSet<int>[entryCount];
            for (int index = 0; index < entryCount; index++)
                normalized[index] = new HashSet<int>();
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                IList<int> entryNeighbors = neighbors[entryIndex];
                if (entryNeighbors == null)
                    throw new ArgumentException("Neighbor lists cannot be null.",
                        "neighbors");
                for (int neighborIndex = 0;
                    neighborIndex < entryNeighbors.Count; neighborIndex++)
                {
                    int neighbor = entryNeighbors[neighborIndex];
                    if (neighbor < 0 || neighbor >= entryCount ||
                        neighbor == entryIndex)
                    {
                        throw new ArgumentException(
                            "Neighbor indices must identify another entry.",
                            "neighbors");
                    }
                    normalized[entryIndex].Add(neighbor);
                    normalized[neighbor].Add(entryIndex);
                }
            }
            IList<int>[] result = new IList<int>[entryCount];
            for (int index = 0; index < entryCount; index++)
            {
                List<int> sorted = new List<int>(normalized[index]);
                sorted.Sort();
                result[index] = sorted.AsReadOnly();
            }
            return result;
        }

        private static void ValidateCounts(int entryCount, int maximumLane)
        {
            if (entryCount < 0)
                throw new ArgumentOutOfRangeException("entryCount");
            if (maximumLane < 0 || maximumLane == Int32.MaxValue)
                throw new ArgumentOutOfRangeException("maximumLane");
        }

        private sealed class SolverState
        {
            private readonly int _entryCount;
            private readonly int _laneCount;
            private readonly bool[,] _domains;
            private readonly int[] _domainCounts;
            private readonly Func<int, int, int, int, bool> _hasConflict;
            private readonly Dictionary<ConflictKey, bool> _conflictCache;
            private readonly IList<int>[] _neighbors;
            private readonly int[] _unassignedNeighborCounts;
            private readonly long _maximumVisitedNodes;
            private readonly long _maximumConstraintChecks;
            private readonly bool _conflictCallbackIsSymmetric;
            private readonly bool _useLeastConstrainingValue;
            private const int MaximumCachedConflicts = 250000;
            private const int MaximumLeastConstrainingDomainSize = 8;

            internal SolverState(int entryCount, int laneCount,
                bool[,] domains, int[] domainCounts,
                Func<int, int, int, int, bool> hasConflict,
                IList<int>[] neighbors, long maximumVisitedNodes,
                long maximumConstraintChecks,
                bool conflictCallbackIsSymmetric,
                bool useLeastConstrainingValue)
            {
                _entryCount = entryCount;
                _laneCount = laneCount;
                _domains = domains;
                _domainCounts = domainCounts;
                _hasConflict = hasConflict;
                _conflictCache = new Dictionary<ConflictKey, bool>();
                _neighbors = neighbors;
                if (_neighbors != null)
                {
                    _unassignedNeighborCounts = new int[entryCount];
                    for (int index = 0; index < entryCount; index++)
                    {
                        _unassignedNeighborCounts[index] =
                            _neighbors[index].Count;
                    }
                }
                _maximumVisitedNodes = maximumVisitedNodes;
                _maximumConstraintChecks = maximumConstraintChecks;
                _conflictCallbackIsSymmetric =
                    conflictCallbackIsSymmetric;
                _useLeastConstrainingValue = useLeastConstrainingValue;
                Assignments = new int[entryCount];
                for (int index = 0; index < Assignments.Length; index++)
                    Assignments[index] = -1;
            }

            internal int[] Assignments { get; private set; }

            internal long VisitedNodes { get; private set; }

            internal long Backtracks { get; private set; }

            internal long ConstraintChecks { get; private set; }

            internal bool SearchBudgetExhausted { get; private set; }

            internal bool Search(int assignedCount)
            {
                if (assignedCount == _entryCount)
                    return true;
                if (SearchBudgetExhausted)
                    return false;

                int entryIndex = SelectEntryByMinimumRemainingValues();
                if (entryIndex < 0 || _domainCounts[entryIndex] == 0)
                    return false;

                int[] laneOrder = GetLaneOrder(entryIndex);
                if (SearchBudgetExhausted)
                    return false;
                for (int orderIndex = 0;
                    orderIndex < laneOrder.Length; orderIndex++)
                {
                    int laneIndex = laneOrder[orderIndex];

                    if (VisitedNodes >= _maximumVisitedNodes)
                    {
                        SearchBudgetExhausted = true;
                        return false;
                    }
                    VisitedNodes++;
                    Assignments[entryIndex] = laneIndex;
                    UpdateUnassignedNeighborCounts(entryIndex, -1);
                    List<DomainRemoval> removals =
                        new List<DomainRemoval>();
                    bool forwardCheckPassed = ForwardCheck(
                        entryIndex, laneIndex, removals);
                    if (forwardCheckPassed && Search(assignedCount + 1))
                        return true;

                    Restore(removals);
                    UpdateUnassignedNeighborCounts(entryIndex, 1);
                    Assignments[entryIndex] = -1;
                    Backtracks++;
                    if (SearchBudgetExhausted)
                        return false;
                }

                return false;
            }

            private int[] GetLaneOrder(int entryIndex)
            {
                List<LanePreference> preferences =
                    new List<LanePreference>(_domainCounts[entryIndex]);
                for (int laneIndex = 0;
                    laneIndex < _laneCount; laneIndex++)
                {
                    if (!_domains[entryIndex, laneIndex])
                        continue;

                    int removals = 0;
                    int emptiedDomains = 0;
                    if (_useLeastConstrainingValue &&
                        _domainCounts[entryIndex] <=
                            MaximumLeastConstrainingDomainSize)
                    {
                        IList<int> candidates = _neighbors == null
                            ? null
                            : _neighbors[entryIndex];
                        int candidateCount = candidates == null
                            ? _entryCount
                            : candidates.Count;
                        for (int candidateIndex = 0;
                            candidateIndex < candidateCount; candidateIndex++)
                        {
                            int neighbor = candidates == null
                                ? candidateIndex
                                : candidates[candidateIndex];
                            if (Assignments[neighbor] >= 0)
                                continue;

                            int neighborRemovals = 0;
                            for (int neighborLane = 0;
                                neighborLane < _laneCount; neighborLane++)
                            {
                                if (_domains[neighbor, neighborLane] &&
                                    HasConflict(entryIndex, laneIndex,
                                        neighbor, neighborLane))
                                {
                                    neighborRemovals++;
                                }
                                if (SearchBudgetExhausted)
                                    return new int[0];
                            }
                            removals += neighborRemovals;
                            if (neighborRemovals == _domainCounts[neighbor])
                                emptiedDomains++;
                        }
                    }
                    preferences.Add(new LanePreference(laneIndex,
                        emptiedDomains, removals));
                }

                preferences.Sort(delegate(LanePreference left,
                    LanePreference right)
                {
                    int comparison = left.EmptiedDomains.CompareTo(
                        right.EmptiedDomains);
                    if (comparison != 0)
                        return comparison;
                    comparison = left.Removals.CompareTo(right.Removals);
                    return comparison != 0
                        ? comparison
                        : left.Lane.CompareTo(right.Lane);
                });
                int[] result = new int[preferences.Count];
                for (int index = 0; index < preferences.Count; index++)
                    result[index] = preferences[index].Lane;
                return result;
            }

            internal int FindGreedyCliqueLowerBound()
            {
                bool[,] cannotShareLane =
                    new bool[_entryCount, _entryCount];
                int[] degrees = new int[_entryCount];
                for (int left = 0; left < _entryCount; left++)
                {
                    for (int right = left + 1;
                        right < _entryCount; right++)
                    {
                        if (!CanShareAnyLane(left, right))
                        {
                            cannotShareLane[left, right] = true;
                            cannotShareLane[right, left] = true;
                            degrees[left]++;
                            degrees[right]++;
                        }
                    }
                }

                List<int> order = new List<int>(_entryCount);
                for (int index = 0; index < _entryCount; index++)
                    order.Add(index);
                order.Sort(delegate(int left, int right)
                {
                    int comparison = degrees[right].CompareTo(degrees[left]);
                    if (comparison != 0)
                        return comparison;
                    return left.CompareTo(right);
                });

                List<int> clique = new List<int>();
                for (int orderIndex = 0;
                    orderIndex < order.Count; orderIndex++)
                {
                    int candidate = order[orderIndex];
                    bool adjacentToClique = true;
                    for (int cliqueIndex = 0;
                        cliqueIndex < clique.Count; cliqueIndex++)
                    {
                        if (!cannotShareLane[candidate, clique[cliqueIndex]])
                        {
                            adjacentToClique = false;
                            break;
                        }
                    }

                    if (adjacentToClique)
                        clique.Add(candidate);
                }

                return clique.Count;
            }

            private int SelectEntryByMinimumRemainingValues()
            {
                int selected = -1;
                int smallestDomain = Int32.MaxValue;
                int greatestUnassignedDegree = -1;
                for (int entryIndex = 0;
                    entryIndex < _entryCount; entryIndex++)
                {
                    if (Assignments[entryIndex] >= 0)
                        continue;

                    int count = _domainCounts[entryIndex];
                    int degree = _unassignedNeighborCounts == null
                        ? 0
                        : _unassignedNeighborCounts[entryIndex];
                    if (count < smallestDomain ||
                        (count == smallestDomain &&
                        degree > greatestUnassignedDegree))
                    {
                        smallestDomain = count;
                        greatestUnassignedDegree = degree;
                        selected = entryIndex;
                    }
                }

                return selected;
            }

            private void UpdateUnassignedNeighborCounts(int entryIndex,
                int adjustment)
            {
                if (_unassignedNeighborCounts == null)
                    return;

                IList<int> neighbors = _neighbors[entryIndex];
                for (int index = 0; index < neighbors.Count; index++)
                {
                    int neighbor = neighbors[index];
                    if (Assignments[neighbor] < 0)
                        _unassignedNeighborCounts[neighbor] += adjustment;
                }
            }

            private bool ForwardCheck(int assignedEntry, int assignedLane,
                ICollection<DomainRemoval> removals)
            {
                IList<int> candidates = _neighbors == null
                    ? null
                    : _neighbors[assignedEntry];
                int candidateCount = candidates == null
                    ? _entryCount
                    : candidates.Count;
                for (int candidateIndex = 0;
                    candidateIndex < candidateCount; candidateIndex++)
                {
                    int entryIndex = candidates == null
                        ? candidateIndex
                        : candidates[candidateIndex];
                    if (Assignments[entryIndex] >= 0)
                        continue;

                    for (int laneIndex = 0;
                        laneIndex < _laneCount; laneIndex++)
                    {
                        if (!_domains[entryIndex, laneIndex])
                            continue;
                        if (!HasConflict(assignedEntry, assignedLane,
                            entryIndex, laneIndex))
                        {
                            continue;
                        }

                        _domains[entryIndex, laneIndex] = false;
                        _domainCounts[entryIndex]--;
                        removals.Add(new DomainRemoval(
                            entryIndex, laneIndex));
                    }

                    if (SearchBudgetExhausted)
                        return false;

                    if (_domainCounts[entryIndex] == 0)
                        return false;
                }

                return true;
            }

            private void Restore(IList<DomainRemoval> removals)
            {
                for (int index = removals.Count - 1; index >= 0; index--)
                {
                    DomainRemoval removal = removals[index];
                    _domains[removal.EntryIndex, removal.LaneIndex] = true;
                    _domainCounts[removal.EntryIndex]++;
                }
            }

            private bool CanShareAnyLane(int leftEntry, int rightEntry)
            {
                for (int laneIndex = 0;
                    laneIndex < _laneCount; laneIndex++)
                {
                    if (!_domains[leftEntry, laneIndex] ||
                        !_domains[rightEntry, laneIndex])
                    {
                        continue;
                    }

                    if (!HasConflict(leftEntry, laneIndex,
                        rightEntry, laneIndex))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool HasConflict(int firstEntry, int firstLane,
                int secondEntry, int secondLane)
            {
                if (firstEntry == secondEntry)
                    return false;

                if (firstEntry > secondEntry)
                {
                    int temporaryEntry = firstEntry;
                    firstEntry = secondEntry;
                    secondEntry = temporaryEntry;

                    int temporaryLane = firstLane;
                    firstLane = secondLane;
                    secondLane = temporaryLane;
                }

                ConflictKey key = new ConflictKey(firstEntry, firstLane,
                    secondEntry, secondLane);
                bool conflict;
                if (_conflictCache.TryGetValue(key, out conflict))
                    return conflict;

                if (ConstraintChecks >= _maximumConstraintChecks)
                {
                    SearchBudgetExhausted = true;
                    return true;
                }

                // A conflict is an unordered candidate pair. Evaluate both
                // directions in a fixed order so callback argument order
                // cannot change the constraint represented by the solver.
                ConstraintChecks++;
                conflict = _hasConflict(firstEntry, firstLane,
                    secondEntry, secondLane);
                if (!conflict && !_conflictCallbackIsSymmetric)
                {
                    if (ConstraintChecks >= _maximumConstraintChecks)
                    {
                        SearchBudgetExhausted = true;
                        return true;
                    }
                    ConstraintChecks++;
                    conflict = _hasConflict(secondEntry, secondLane,
                        firstEntry, firstLane);
                }
                if (_conflictCache.Count < MaximumCachedConflicts)
                    _conflictCache.Add(key, conflict);
                return conflict;
            }
        }

        private struct DomainRemoval
        {
            internal DomainRemoval(int entryIndex, int laneIndex)
            {
                EntryIndex = entryIndex;
                LaneIndex = laneIndex;
            }

            internal int EntryIndex;
            internal int LaneIndex;
        }

        private struct LanePreference
        {
            internal LanePreference(int lane, int emptiedDomains,
                int removals)
            {
                Lane = lane;
                EmptiedDomains = emptiedDomains;
                Removals = removals;
            }

            internal int Lane;
            internal int EmptiedDomains;
            internal int Removals;
        }

        private struct ConflictKey : IEquatable<ConflictKey>
        {
            private readonly int _firstEntry;
            private readonly int _firstLane;
            private readonly int _secondEntry;
            private readonly int _secondLane;

            internal ConflictKey(int firstEntry, int firstLane,
                int secondEntry, int secondLane)
            {
                _firstEntry = firstEntry;
                _firstLane = firstLane;
                _secondEntry = secondEntry;
                _secondLane = secondLane;
            }

            public bool Equals(ConflictKey other)
            {
                return _firstEntry == other._firstEntry &&
                    _firstLane == other._firstLane &&
                    _secondEntry == other._secondEntry &&
                    _secondLane == other._secondLane;
            }

            public override bool Equals(object value)
            {
                if (!(value is ConflictKey))
                    return false;
                return Equals((ConflictKey)value);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _firstEntry;
                    hash = (hash * 397) ^ _firstLane;
                    hash = (hash * 397) ^ _secondEntry;
                    hash = (hash * 397) ^ _secondLane;
                    return hash;
                }
            }
        }
    }
}
