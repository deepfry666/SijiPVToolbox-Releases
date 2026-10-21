using System;
using System.Collections.Generic;
using StructuralToolbox.Core;

internal static class AnnotationOverlapSolverTests
{
    private static int _passed;

    public static int Main()
    {
        try
        {
            DetectsOverlappingTextBounds();
            IgnoresSeparatedBounds();
            CanFindMoveOffsetThatClearsConflict();
            KeepsNonConflictingItemsUnchanged();
            Console.WriteLine("PASS {0} annotation overlap solver tests", _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static void DetectsOverlappingTextBounds()
    {
        List<AnnotationOverlapItem> items = new List<AnnotationOverlapItem>();
        items.Add(Item(AnnotationOverlapKind.Text, new[] {
            new OverlapRect(0.0, 0.0, 10.0, 3.0)
        }));
        items.Add(Item(AnnotationOverlapKind.Text, new[] {
            new OverlapRect(8.0, 0.0, 18.0, 3.0)
        }));

        List<AnnotationOverlapPair> pairs =
            AnnotationOverlapSolver.FindOverlappingPairs(items, 0.0);
        Equal(1, pairs.Count, "one overlapping pair");
        Equal(0, pairs[0].FirstIndex, "pair first index");
        Equal(1, pairs[0].SecondIndex, "pair second index");
        Pass();
    }

    private static void IgnoresSeparatedBounds()
    {
        List<AnnotationOverlapItem> items = new List<AnnotationOverlapItem>();
        items.Add(Item(AnnotationOverlapKind.Text, new[] {
            new OverlapRect(0.0, 0.0, 10.0, 3.0)
        }));
        items.Add(Item(AnnotationOverlapKind.Text, new[] {
            new OverlapRect(12.0, 0.0, 22.0, 3.0)
        }));

        List<AnnotationOverlapPair> pairs =
            AnnotationOverlapSolver.FindOverlappingPairs(items, 0.0);
        Equal(0, pairs.Count, "no overlap when separated");
        Pass();
    }

    private static void CanFindMoveOffsetThatClearsConflict()
    {
        List<AnnotationOverlapItem> items = new List<AnnotationOverlapItem>();
        items.Add(Item(AnnotationOverlapKind.Text, new[] {
            new OverlapRect(0.0, 0.0, 10.0, 3.0)
        }));
        items.Add(Item(AnnotationOverlapKind.Text, new[] {
            new OverlapRect(8.0, 0.0, 18.0, 3.0)
        }));
        items[0].TextHeight = 3.0;
        items[1].TextHeight = 3.0;

        double deltaX;
        double deltaY;
        bool moved = AnnotationOverlapSolver.TryFindMoveOffset(
            items, 1, 0.15, out deltaX, out deltaY);
        True(moved, "solver should find a clear offset");
        OverlapRect movedText = items[1].TextBounds[0]
            .Translated(deltaX, deltaY);
        False(movedText.Intersects(items[0].TextBounds[0], 0.15),
            "moved text should clear the conflicting item");
        Pass();
    }

    private static void KeepsNonConflictingItemsUnchanged()
    {
        List<AnnotationOverlapItem> items = new List<AnnotationOverlapItem>();
        items.Add(Item(AnnotationOverlapKind.Dimension, new[] {
            new OverlapRect(0.0, 0.0, 4.0, 1.0),
            new OverlapRect(10.0, 0.0, 14.0, 1.0)
        }));
        items.Add(Item(AnnotationOverlapKind.Dimension, new[] {
            new OverlapRect(20.0, 0.0, 24.0, 1.0)
        }));

        List<AnnotationOverlapPair> pairs =
            AnnotationOverlapSolver.FindOverlappingPairs(items, 0.0);
        Equal(0, pairs.Count, "unrelated text boxes must not pair");
        Pass();
    }

    private static AnnotationOverlapItem Item(AnnotationOverlapKind kind,
        OverlapRect[] bounds)
    {
        AnnotationOverlapItem item = new AnnotationOverlapItem();
        item.Kind = kind;
        item.DisplayName = "测试";
        item.Layer = "SJ";
        item.TextHeight = 1.0;
        item.TextBounds.AddRange(bounds);
        item.FullBounds.AddRange(bounds);
        return item;
    }

    private static void Equal(int expected, int actual, string message)
    {
        if (expected != actual)
            throw new Exception(message + " (expected " + expected +
                ", actual " + actual + ")");
    }

    private static void True(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void False(bool value, string message)
    {
        if (value)
            throw new Exception(message);
    }

    private static void Pass()
    {
        _passed++;
    }
}
