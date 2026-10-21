using System;
using System.Collections.Generic;
using System.Diagnostics;
using StructuralToolbox.Core;

internal static class DeterministicLaneSolverTests
{
    private static int _passed;

    public static int Main()
    {
        try
        {
            SolvesFirstFitTrapExactly();
            RepeatsWithIdenticalAssignmentsAndStatistics();
            BacktracksWithoutAVisitBudget();
            SolvesTwentyFiveEntryCliqueInAscendingLanes();
            RejectsTwentySixEntryCliqueBeforeSearch();
            AppliesMatrixDomainsAndCrossLaneConflicts();
            AppliesAvailabilityCallback();
            TreatsConflictAsAnUnorderedPair();
            SymmetricSparseCallbackUsesOneConstraintCheck();
            ReturnsEmptyLanesAfterSearchFailure();
            HandlesZeroSizedAndSingleLaneProblems();
            PrefersHigherUnassignedDegreeForMrvTies();
            PrunesLocallyImpossiblePreferredSideBeforeGlobalSearch();
            SolvesEightHundredFortyTwoEntrySparsePathDeterministically();
            StopsDeterministicallyAtVisitedNodeBudget();
            StopsDeterministicallyAtConstraintCheckBudget();
            FindsEarliestInternalSourceClearBand();
            MovesPastOutermostBlockedBandWhenNoInternalGapFits();
            IgnoresSourceIntervalsBehindTheBaseLane();
            ConvertsInclusiveDistanceBoundariesToBlockedLanes();
            RepeatsSourceClearBandSelectionDeterministically();
            UsesOneSharedTranslationAcrossSourceBands();
            RejectsInvalidArguments();
            Console.WriteLine("PASS {0} deterministic lane solver tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static void SolvesFirstFitTrapExactly()
    {
        Func<int, int, int, int, bool> conflict =
            delegate(int leftEntry, int leftLane,
                int rightEntry, int rightLane)
            {
                if (leftLane != rightLane)
                    return false;

                return IsPair(leftEntry, rightEntry, 0, 2) ||
                    IsPair(leftEntry, rightEntry, 2, 3) ||
                    IsPair(leftEntry, rightEntry, 3, 1);
            };

        int[] oldFirstFitLanes;
        bool oldFirstFitSucceeded = TryOldFirstFit(
            4, 1, conflict, out oldFirstFitLanes);
        False(oldFirstFitSucceeded,
            "old first-fit unexpectedly solved the A,D,B,C path trap");

        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.Solve(4, 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                conflict);
        True(result.Success, "complete solver rejected the first-fit trap");
        SequenceEqual(new int[] { 0, 1, 1, 0 }, result.Lanes,
            "A,D,B,C lane assignment");
        Equal(4L, result.VisitedNodes,
            "MRV plus forward-check should visit one node per entry");
        Pass();
    }

    private static void RepeatsWithIdenticalAssignmentsAndStatistics()
    {
        Func<int, int, int, int, bool> conflict = PathTrapConflict();
        DeterministicLaneSolverResult baseline =
            DeterministicLaneSolver.Solve(4, 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                conflict);

        for (int run = 0; run < 20; run++)
        {
            DeterministicLaneSolverResult repeated =
                DeterministicLaneSolver.Solve(4, 1,
                    delegate(int entryIndex, int laneIndex) { return true; },
                    conflict);
            Equal(baseline.Success, repeated.Success,
                "success changed between deterministic runs");
            SequenceEqual(baseline.Lanes, repeated.Lanes,
                "lanes changed between deterministic runs");
            Equal(baseline.VisitedNodes, repeated.VisitedNodes,
                "visited node count changed between deterministic runs");
            Equal(baseline.Backtracks, repeated.Backtracks,
                "backtrack count changed between deterministic runs");
        }
        Pass();
    }

    private static void BacktracksWithoutAVisitBudget()
    {
        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.Solve(3, 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane)
                {
                    return IsPair(leftEntry, rightEntry, 0, 1) &&
                        ((leftEntry == 0 && leftLane == 0) ||
                        (rightEntry == 0 && rightLane == 0));
                });

        True(result.Success, "solver did not recover from the first bad lane");
        SequenceEqual(new int[] { 1, 0, 0 }, result.Lanes,
            "backtracking assignment");
        True(result.Backtracks > 0,
            "solver did not report its rejected search branch");
        Pass();
    }

    private static void SolvesTwentyFiveEntryCliqueInAscendingLanes()
    {
        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.Solve(25, 24,
                delegate(int entryIndex, int laneIndex) { return true; },
                SameLaneConflict());

        True(result.Success, "25-entry clique should fit lanes 0 through 24");
        int[] expected = new int[25];
        for (int index = 0; index < expected.Length; index++)
            expected[index] = index;
        SequenceEqual(expected, result.Lanes,
            "25-entry clique must use ascending lanes deterministically");
        Equal(25L, result.VisitedNodes,
            "25-entry clique should need one assignment per entry");
        Pass();
    }

    private static void RejectsTwentySixEntryCliqueBeforeSearch()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.Solve(26, 24,
                delegate(int entryIndex, int laneIndex) { return true; },
                SameLaneConflict());
        stopwatch.Stop();

        False(result.Success, "26-entry clique cannot fit 25 lanes");
        Equal(0, result.Lanes.Length,
            "clique failure must not expose a partial assignment");
        Equal(0L, result.VisitedNodes,
            "greedy clique lower bound should reject before search");
        Equal(0L, result.Backtracks,
            "clique lower-bound rejection should not backtrack");
        True(stopwatch.ElapsedMilliseconds < 2000L,
            "26-entry clique lower-bound check was not fast");
        Pass();
    }

    private static void AppliesMatrixDomainsAndCrossLaneConflicts()
    {
        bool[,] availability = new bool[3, 3];
        availability[0, 0] = true;
        availability[1, 1] = true;
        availability[1, 2] = true;
        availability[2, 1] = true;

        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.Solve(3, 2, availability,
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane)
                {
                    return IsCandidatePair(leftEntry, leftLane,
                        rightEntry, rightLane, 0, 0, 1, 1);
                });

        True(result.Success, "matrix-restricted problem should be solvable");
        SequenceEqual(new int[] { 0, 2, 1 }, result.Lanes,
            "matrix domains and cross-lane conflict");
        Pass();
    }

    private static void AppliesAvailabilityCallback()
    {
        int callbackCalls = 0;
        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.Solve(2, 2,
                delegate(int entryIndex, int laneIndex)
                {
                    callbackCalls++;
                    return (entryIndex == 0 && laneIndex == 2) ||
                        (entryIndex == 1 && laneIndex == 0);
                },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane) { return false; });

        True(result.Success, "callback-restricted problem should be solvable");
        SequenceEqual(new int[] { 2, 0 }, result.Lanes,
            "availability callback assignment");
        Equal(6, callbackCalls,
            "availability callback should be evaluated once per candidate");
        Pass();
    }

    private static void ReturnsEmptyLanesAfterSearchFailure()
    {
        Func<int, int, int, int, bool> conflict =
            delegate(int leftEntry, int leftLane,
                int rightEntry, int rightLane) { return true; };
        DeterministicLaneSolverResult first =
            DeterministicLaneSolver.Solve(2, 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                conflict);
        DeterministicLaneSolverResult second =
            DeterministicLaneSolver.Solve(2, 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                conflict);

        False(first.Success, "all-pairs conflict should be unsatisfiable");
        Equal(0, first.Lanes.Length,
            "search failure must return an empty lane array");
        True(first.VisitedNodes > 0,
            "fixture should fail through search rather than the clique bound");
        Equal(first.VisitedNodes, second.VisitedNodes,
            "failure visited nodes changed between runs");
        Equal(first.Backtracks, second.Backtracks,
            "failure backtracks changed between runs");
        Equal(0, second.Lanes.Length,
            "repeated failure must return an empty lane array");
        Pass();
    }

    private static void TreatsConflictAsAnUnorderedPair()
    {
        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.Solve(2, 0,
                delegate(int entryIndex, int laneIndex) { return true; },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane)
                {
                    return leftEntry > rightEntry;
                });

        False(result.Success,
            "reverse-only conflict callback was treated as directional");
        Equal(0, result.Lanes.Length,
            "unordered-pair conflict failure lanes");
        Pass();
    }

    private static void SymmetricSparseCallbackUsesOneConstraintCheck()
    {
        IList<int>[] neighbors = CreateEmptyNeighbors(2);
        neighbors[0].Add(1);
        int callbackCalls = 0;
        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.SolveSparseSymmetric(2, 0,
                delegate(int entryIndex, int laneIndex) { return true; },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane)
                {
                    callbackCalls++;
                    return false;
                }, neighbors, 100L, 100L, false);

        True(result.Success, "symmetric sparse fixture should be solvable");
        Equal(1, callbackCalls,
            "symmetric conflict callback should run once per candidate pair");
        Equal(1L, result.ConstraintChecks,
            "symmetric constraint statistics should count one callback");
        Pass();
    }

    private static void HandlesZeroSizedAndSingleLaneProblems()
    {
        DeterministicLaneSolverResult empty =
            DeterministicLaneSolver.Solve(0, 0, new bool[0, 1],
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane) { return false; });
        True(empty.Success, "zero-entry problem should be solved");
        Equal(0, empty.Lanes.Length, "zero-entry lanes");

        DeterministicLaneSolverResult wideEmpty =
            DeterministicLaneSolver.Solve(0, Int32.MaxValue - 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane) { return false; });
        True(wideEmpty.Success,
            "zero-entry problem should not allocate an unused lane matrix");
        Equal(0, wideEmpty.Lanes.Length, "wide zero-entry lanes");

        bool[,] oneAvailable = new bool[1, 1];
        oneAvailable[0, 0] = true;
        DeterministicLaneSolverResult single =
            DeterministicLaneSolver.Solve(1, 0, oneAvailable,
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane) { return false; });
        True(single.Success, "inclusive maximumLane zero should expose lane zero");
        SequenceEqual(new int[] { 0 }, single.Lanes, "single lane assignment");

        DeterministicLaneSolverResult unavailable =
            DeterministicLaneSolver.Solve(1, 0, new bool[1, 1],
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane) { return false; });
        False(unavailable.Success, "empty domain should be unsatisfiable");
        Equal(0, unavailable.Lanes.Length, "empty-domain failure lanes");
        Pass();
    }

    private static void SolvesEightHundredFortyTwoEntrySparsePathDeterministically()
    {
        const int entryCount = 842;
        IList<int>[] neighbors = CreateEmptyNeighbors(entryCount);
        for (int entryIndex = 0; entryIndex < entryCount - 1; entryIndex++)
            neighbors[entryIndex].Add(entryIndex + 1);

        int callbackCalls = 0;
        Func<int, int, int, int, bool> conflict =
            delegate(int leftEntry, int leftLane,
                int rightEntry, int rightLane)
            {
                callbackCalls++;
                return leftLane == rightLane;
            };

        DeterministicLaneSolverResult first =
            DeterministicLaneSolver.SolveSparse(entryCount, 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                conflict, neighbors, 2000L, 10000L);

        True(first.Success, "842-entry sparse path should be solvable");
        Equal(entryCount, first.Lanes.Length,
            "842-entry sparse path assignment count");
        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            Equal(1 - (entryIndex % 2), first.Lanes[entryIndex],
                "842-entry sparse path alternating lane");
        }
        Equal((long)entryCount, first.VisitedNodes,
            "sparse path should visit one node per entry");
        Equal(first.ConstraintChecks, (long)callbackCalls,
            "constraint statistics should match callback calls");
        True(first.ConstraintChecks <= 4L * (entryCount - 1),
            "sparse path performed non-linear constraint checks");
        False(first.SearchBudgetExhausted,
            "solved sparse path incorrectly exhausted a budget");

        callbackCalls = 0;
        DeterministicLaneSolverResult second =
            DeterministicLaneSolver.SolveSparse(entryCount, 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                conflict, neighbors, 2000L, 10000L);
        True(second.Success,
            "repeated 842-entry sparse path should be solvable");
        SequenceEqual(first.Lanes, second.Lanes,
            "842-entry sparse path deterministic assignment");
        Equal(first.VisitedNodes, second.VisitedNodes,
            "842-entry sparse path deterministic visited nodes");
        Equal(first.Backtracks, second.Backtracks,
            "842-entry sparse path deterministic backtracks");
        Equal(first.ConstraintChecks, second.ConstraintChecks,
            "842-entry sparse path deterministic constraint checks");
        Equal(second.ConstraintChecks, (long)callbackCalls,
            "repeated constraint statistics should match callback calls");
        Pass();
    }

    private static void PrefersHigherUnassignedDegreeForMrvTies()
    {
        IList<int>[] neighbors = CreateEmptyNeighbors(3);
        neighbors[0].Add(2);
        neighbors[1].Add(2);

        DeterministicLaneSolverResult result =
            DeterministicLaneSolver.SolveSparse(3, 1,
                delegate(int entryIndex, int laneIndex) { return true; },
                SameLaneConflict(), neighbors, 100L, 100L);

        True(result.Success, "degree-tie fixture should be solvable");
        SequenceEqual(new int[] { 1, 1, 0 }, result.Lanes,
            "MRV ties should select the higher-degree center first");
        Equal(3L, result.VisitedNodes,
            "degree-tie fixture should not backtrack");
        Pass();
    }

    private static void PrunesLocallyImpossiblePreferredSideBeforeGlobalSearch()
    {
        const int entryCount = 26;
        const int lanesPerSide = 25;
        const int maximumSlot = (lanesPerSide * 2) - 1;
        IList<int>[] neighbors = CreateCompleteNeighbors(entryCount);
        Func<int, int, int, int, bool> conflict =
            delegate(int leftEntry, int leftSlot,
                int rightEntry, int rightSlot)
            {
                bool leftPreferred = leftSlot < lanesPerSide;
                bool rightPreferred = rightSlot < lanesPerSide;
                if (leftPreferred != rightPreferred)
                    return true;
                return leftPreferred && leftSlot == rightSlot;
            };

        DeterministicLaneSolverResult unpruned =
            DeterministicLaneSolver.SolveSparseSymmetric(entryCount,
                maximumSlot,
                delegate(int entryIndex, int slot) { return true; },
                conflict, neighbors, 1000L, 100000L, false);
        False(unpruned.Success,
            "unpruned preferred-side clique should hit the tight budget");
        True(unpruned.SearchBudgetExhausted,
            "unpruned preferred-side clique did not expose the search trap");

        DeterministicLaneSolverResult preferredSide =
            DeterministicLaneSolver.SolveSparseSymmetric(entryCount,
                lanesPerSide - 1,
                delegate(int entryIndex, int lane) { return true; },
                SameLaneConflict(), neighbors, 1000L, 100000L, true);
        False(preferredSide.Success,
            "26-entry preferred side cannot fit 25 exclusive lanes");
        False(preferredSide.SearchBudgetExhausted,
            "preferred-side clique must be proven impossible before search");
        Equal(0L, preferredSide.VisitedNodes,
            "preferred-side clique proof should not visit search nodes");

        DeterministicLaneSolverResult pruned =
            DeterministicLaneSolver.SolveSparseSymmetric(entryCount,
                maximumSlot,
                delegate(int entryIndex, int slot)
                {
                    return slot >= lanesPerSide;
                }, conflict, neighbors, 1000L, 100000L, false);
        True(pruned.Success,
            "locally impossible preferred side should be pruned globally");
        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            True(pruned.Lanes[entryIndex] >= lanesPerSide,
                "pruned fixture assigned a preferred-side slot");
        }
        Pass();
    }

    private static void StopsDeterministicallyAtVisitedNodeBudget()
    {
        IList<int>[] neighbors = CreateEmptyNeighbors(3);
        DeterministicLaneSolverResult first =
            DeterministicLaneSolver.SolveSparse(3, 0,
                delegate(int entryIndex, int laneIndex) { return true; },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane) { return false; },
                neighbors, 2L, 100L);
        DeterministicLaneSolverResult second =
            DeterministicLaneSolver.SolveSparse(3, 0,
                delegate(int entryIndex, int laneIndex) { return true; },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane) { return false; },
                neighbors, 2L, 100L);

        False(first.Success,
            "visited-node-limited search should stop before assignment completes");
        True(first.SearchBudgetExhausted,
            "visited-node-limited search did not report budget exhaustion");
        Equal(0, first.Lanes.Length,
            "visited-node exhaustion must not expose partial lanes");
        Equal(2L, first.VisitedNodes,
            "visited-node search exceeded or undershot its exact budget");
        Equal(0L, first.ConstraintChecks,
            "edgeless visited-node fixture should not check constraints");
        Equal(first.VisitedNodes, second.VisitedNodes,
            "visited-node exhaustion changed between runs");
        Equal(first.Backtracks, second.Backtracks,
            "visited-node exhaustion backtracks changed between runs");
        Equal(first.ConstraintChecks, second.ConstraintChecks,
            "visited-node exhaustion constraint checks changed between runs");
        Equal(first.SearchBudgetExhausted, second.SearchBudgetExhausted,
            "visited-node exhaustion flag changed between runs");
        Pass();
    }

    private static void StopsDeterministicallyAtConstraintCheckBudget()
    {
        IList<int>[] neighbors = CreateEmptyNeighbors(2);
        neighbors[0].Add(1);
        int firstCallbackCalls = 0;
        DeterministicLaneSolverResult first =
            DeterministicLaneSolver.SolveSparse(2, 0,
                delegate(int entryIndex, int laneIndex) { return true; },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane)
                {
                    firstCallbackCalls++;
                    return false;
                }, neighbors, 100L, 1L);

        int secondCallbackCalls = 0;
        DeterministicLaneSolverResult second =
            DeterministicLaneSolver.SolveSparse(2, 0,
                delegate(int entryIndex, int laneIndex) { return true; },
                delegate(int leftEntry, int leftLane,
                    int rightEntry, int rightLane)
                {
                    secondCallbackCalls++;
                    return false;
                }, neighbors, 100L, 1L);

        False(first.Success,
            "constraint-limited search should stop before assignment completes");
        True(first.SearchBudgetExhausted,
            "constraint-limited search did not report budget exhaustion");
        Equal(0, first.Lanes.Length,
            "constraint exhaustion must not expose partial lanes");
        Equal(1L, first.ConstraintChecks,
            "constraint search exceeded or undershot its exact budget");
        Equal(1, firstCallbackCalls,
            "constraint callback exceeded or undershot its exact budget");
        Equal(first.VisitedNodes, second.VisitedNodes,
            "constraint exhaustion visited nodes changed between runs");
        Equal(first.Backtracks, second.Backtracks,
            "constraint exhaustion backtracks changed between runs");
        Equal(first.ConstraintChecks, second.ConstraintChecks,
            "constraint exhaustion checks changed between runs");
        Equal(firstCallbackCalls, secondCallbackCalls,
            "constraint exhaustion callback calls changed between runs");
        Equal(first.SearchBudgetExhausted, second.SearchBudgetExhausted,
            "constraint exhaustion flag changed between runs");
        Pass();
    }

    private static void RejectsInvalidArguments()
    {
        Func<int, int, bool> available =
            delegate(int entryIndex, int laneIndex) { return true; };
        Func<int, int, int, int, bool> noConflict =
            delegate(int leftEntry, int leftLane,
                int rightEntry, int rightLane) { return false; };

        Throws<ArgumentOutOfRangeException>(delegate
        {
            DeterministicLaneSolver.Solve(-1, 0, available, noConflict);
        }, "negative entry count");
        Throws<ArgumentOutOfRangeException>(delegate
        {
            DeterministicLaneSolver.Solve(1, -1, available, noConflict);
        }, "negative maximum lane");
        Throws<ArgumentOutOfRangeException>(delegate
        {
            DeterministicLaneSolver.Solve(
                1, Int32.MaxValue, available, noConflict);
        }, "overflowing maximum lane");
        Throws<ArgumentException>(delegate
        {
            DeterministicLaneSolver.Solve(
                2, 1, new bool[2, 1], noConflict);
        }, "wrong availability matrix dimensions");

        bool[,] nullMatrix = null;
        Throws<ArgumentNullException>(delegate
        {
            DeterministicLaneSolver.Solve(1, 0, nullMatrix, noConflict);
        }, "null availability matrix");
        Func<int, int, bool> nullAvailability = null;
        Throws<ArgumentNullException>(delegate
        {
            DeterministicLaneSolver.Solve(
                1, 0, nullAvailability, noConflict);
        }, "null availability callback");
        Func<int, int, int, int, bool> nullConflict = null;
        Throws<ArgumentNullException>(delegate
        {
            DeterministicLaneSolver.Solve(
                1, 0, available, nullConflict);
        }, "null conflict callback");
        Pass();
    }

    private static void FindsEarliestInternalSourceClearBand()
    {
        List<BlockedLaneInterval> blocked =
            new List<BlockedLaneInterval>();
        blocked.Add(new BlockedLaneInterval(10, 12));
        blocked.Add(new BlockedLaneInterval(0, 4));

        int baseLane = DeterministicLaneBandFinder.FindEarliestClearBand(
            blocked, 2);

        Equal(5, baseLane,
            "earliest three-lane gap before a remote source interval");
        Pass();
    }

    private static void MovesPastOutermostBlockedBandWhenNoInternalGapFits()
    {
        List<BlockedLaneInterval> blocked =
            new List<BlockedLaneInterval>();
        blocked.Add(new BlockedLaneInterval(5, 12));
        blocked.Add(new BlockedLaneInterval(0, 4));

        int baseLane = DeterministicLaneBandFinder.FindEarliestClearBand(
            blocked, 2);

        Equal(13, baseLane,
            "clear band must move past the merged outer interval");
        Pass();
    }

    private static void IgnoresSourceIntervalsBehindTheBaseLane()
    {
        BlockedLaneInterval interval;
        bool created =
            DeterministicLaneBandFinder.TryCreateBlockedInterval(
                -8.0, -0.01, 2.0, out interval);
        False(created, "a source fully behind lane zero must not block a lane");

        int baseLane = DeterministicLaneBandFinder.FindEarliestClearBand(
            new List<BlockedLaneInterval>(), 24);
        Equal(0, baseLane, "a source behind the group must retain base lane zero");
        Pass();
    }

    private static void ConvertsInclusiveDistanceBoundariesToBlockedLanes()
    {
        BlockedLaneInterval exact;
        True(DeterministicLaneBandFinder.TryCreateBlockedInterval(
            4.0, 8.0, 2.0, out exact),
            "inclusive source interval should contain integer lanes");
        Equal(2, exact.FirstLane, "inclusive lower distance boundary");
        Equal(4, exact.LastLane, "inclusive upper distance boundary");

        BlockedLaneInterval between;
        True(DeterministicLaneBandFinder.TryCreateBlockedInterval(
            4.000001, 7.999999, 2.0, out between),
            "strictly interior distance interval should contain lane three");
        Equal(3, between.FirstLane, "ceiling of lower distance boundary");
        Equal(3, between.LastLane, "floor of upper distance boundary");

        BlockedLaneInterval zero;
        True(DeterministicLaneBandFinder.TryCreateBlockedInterval(
            -1.0, 0.0, 2.0, out zero),
            "touching at base lane zero must count as a collision");
        Equal(0, zero.FirstLane, "base-lane lower clamp");
        Equal(0, zero.LastLane, "base-lane inclusive upper boundary");
        Pass();
    }

        private static void RepeatsSourceClearBandSelectionDeterministically()
    {
        List<BlockedLaneInterval> blocked =
            new List<BlockedLaneInterval>();
        blocked.Add(new BlockedLaneInterval(20, 24));
        blocked.Add(new BlockedLaneInterval(0, 2));
        blocked.Add(new BlockedLaneInterval(2, 4));
        blocked.Add(new BlockedLaneInterval(9, 12));

        int baseline = DeterministicLaneBandFinder.FindEarliestClearBand(
            blocked, 3);
        for (int run = 0; run < 20; run++)
        {
            Equal(baseline,
                DeterministicLaneBandFinder.FindEarliestClearBand(
                    blocked, 3),
                "source-clear band changed between deterministic runs");
        }
        Equal(5, baseline, "first four-lane source-clear band");
            Pass();
        }

        private static void UsesOneSharedTranslationAcrossSourceBands()
        {
            List<BlockedLaneInterval> allGroups =
                new List<BlockedLaneInterval>();
            allGroups.Add(new BlockedLaneInterval(0, 4));
            allGroups.Add(new BlockedLaneInterval(50, 60));
            allGroups.Add(new BlockedLaneInterval(0, 39));

            int sharedBase =
                DeterministicLaneBandFinder.FindEarliestClearBand(
                    allGroups, 24);

            Equal(61, sharedBase,
                "shared translation must clear every group source band");
            Pass();
        }

    private static Func<int, int, int, int, bool> PathTrapConflict()
    {
        return delegate(int leftEntry, int leftLane,
            int rightEntry, int rightLane)
        {
            if (leftLane != rightLane)
                return false;
            return IsPair(leftEntry, rightEntry, 0, 2) ||
                IsPair(leftEntry, rightEntry, 2, 3) ||
                IsPair(leftEntry, rightEntry, 3, 1);
        };
    }

    private static Func<int, int, int, int, bool> SameLaneConflict()
    {
        return delegate(int leftEntry, int leftLane,
            int rightEntry, int rightLane)
        {
            return leftEntry != rightEntry && leftLane == rightLane;
        };
    }

    private static IList<int>[] CreateEmptyNeighbors(int entryCount)
    {
        IList<int>[] neighbors = new IList<int>[entryCount];
        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            neighbors[entryIndex] = new List<int>();
        return neighbors;
    }

    private static IList<int>[] CreateCompleteNeighbors(int entryCount)
    {
        IList<int>[] neighbors = CreateEmptyNeighbors(entryCount);
        for (int left = 0; left < entryCount; left++)
        {
            for (int right = left + 1; right < entryCount; right++)
                neighbors[left].Add(right);
        }
        return neighbors;
    }

    private static bool TryOldFirstFit(int entryCount, int maximumLane,
        Func<int, int, int, int, bool> hasConflict, out int[] lanes)
    {
        lanes = new int[entryCount];
        for (int index = 0; index < lanes.Length; index++)
            lanes[index] = -1;

        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            bool placed = false;
            for (int laneIndex = 0; laneIndex <= maximumLane; laneIndex++)
            {
                bool conflicts = false;
                for (int earlier = 0; earlier < entryIndex; earlier++)
                {
                    if (hasConflict(earlier, lanes[earlier],
                        entryIndex, laneIndex))
                    {
                        conflicts = true;
                        break;
                    }
                }

                if (!conflicts)
                {
                    lanes[entryIndex] = laneIndex;
                    placed = true;
                    break;
                }
            }

            if (!placed)
                return false;
        }

        return true;
    }

    private static bool IsPair(int left, int right,
        int expectedLeft, int expectedRight)
    {
        return (left == expectedLeft && right == expectedRight) ||
            (left == expectedRight && right == expectedLeft);
    }

    private static bool IsCandidatePair(
        int leftEntry, int leftLane, int rightEntry, int rightLane,
        int firstEntry, int firstLane, int secondEntry, int secondLane)
    {
        return (leftEntry == firstEntry && leftLane == firstLane &&
                rightEntry == secondEntry && rightLane == secondLane) ||
            (leftEntry == secondEntry && leftLane == secondLane &&
                rightEntry == firstEntry && rightLane == firstLane);
    }

    private static void SequenceEqual(int[] expected, int[] actual,
        string context)
    {
        if (actual == null)
            throw new InvalidOperationException(context + ": actual array is null");
        if (expected.Length != actual.Length)
        {
            throw new InvalidOperationException(String.Format(
                "{0}: expected length {1}, actual length {2}",
                context, expected.Length, actual.Length));
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
            {
                throw new InvalidOperationException(String.Format(
                    "{0}: index {1}, expected {2}, actual {3}",
                    context, index, expected[index], actual[index]));
            }
        }
    }

    private static void Throws<TException>(Action action, string context)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            context + ": expected " + typeof(TException).Name);
    }

    private static void True(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message)
    {
        True(!value, message);
    }

    private static void Equal(bool expected, bool actual, string context)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, actual {2}", context, expected, actual));
        }
    }

    private static void Equal(int expected, int actual, string context)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, actual {2}", context, expected, actual));
        }
    }

    private static void Equal(long expected, long actual, string context)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(String.Format(
                "{0}: expected {1}, actual {2}", context, expected, actual));
        }
    }

    private static void Pass()
    {
        _passed++;
    }
}
