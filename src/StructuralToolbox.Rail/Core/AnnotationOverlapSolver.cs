using System;
using System.Collections.Generic;

namespace StructuralToolbox.Core
{
    public enum AnnotationOverlapKind
    {
        Dimension,
        Text
    }

    public struct OverlapRect
    {
        public double MinimumX;
        public double MinimumY;
        public double MaximumX;
        public double MaximumY;

        public OverlapRect(double minimumX, double minimumY,
            double maximumX, double maximumY)
        {
            MinimumX = minimumX;
            MinimumY = minimumY;
            MaximumX = maximumX;
            MaximumY = maximumY;
        }

        public OverlapRect Translated(double deltaX, double deltaY)
        {
            return new OverlapRect(MinimumX + deltaX, MinimumY + deltaY,
                MaximumX + deltaX, MaximumY + deltaY);
        }

        public bool Intersects(OverlapRect other, double margin)
        {
            return MaximumX + margin >= other.MinimumX &&
                MinimumX - margin <= other.MaximumX &&
                MaximumY + margin >= other.MinimumY &&
                MinimumY - margin <= other.MaximumY;
        }

        public double Width
        {
            get { return MaximumX - MinimumX; }
        }

        public double Height
        {
            get { return MaximumY - MinimumY; }
        }

        public OverlapRect IntersectionWith(OverlapRect other)
        {
            double minX = Math.Max(MinimumX, other.MinimumX);
            double minY = Math.Max(MinimumY, other.MinimumY);
            double maxX = Math.Min(MaximumX, other.MaximumX);
            double maxY = Math.Min(MaximumY, other.MaximumY);
            if (maxX < minX || maxY < minY)
            {
                return new OverlapRect(0.0, 0.0, 0.0, 0.0);
            }
            return new OverlapRect(minX, minY, maxX, maxY);
        }
    }

    public sealed class AnnotationOverlapItem
    {
        public AnnotationOverlapItem()
        {
            TextBounds = new List<OverlapRect>();
            FullBounds = new List<OverlapRect>();
        }

        public AnnotationOverlapKind Kind { get; set; }
        public string DisplayName { get; set; }
        public string Layer { get; set; }
        public double TextHeight { get; set; }

        public List<OverlapRect> TextBounds { get; private set; }
        public List<OverlapRect> FullBounds { get; private set; }
    }

    public sealed class AnnotationOverlapPair
    {
        public int FirstIndex;
        public int SecondIndex;
        public OverlapRect Intersection;
    }

    public static class AnnotationOverlapSolver
    {
        public static List<AnnotationOverlapPair> FindOverlappingPairs(
            IList<AnnotationOverlapItem> items, double margin)
        {
            List<AnnotationOverlapPair> result =
                new List<AnnotationOverlapPair>();
            if (items == null || items.Count < 2)
                return result;

            for (int firstIndex = 0; firstIndex < items.Count; firstIndex++)
            {
                AnnotationOverlapItem first = items[firstIndex];
                for (int secondIndex = firstIndex + 1;
                    secondIndex < items.Count; secondIndex++)
                {
                    AnnotationOverlapItem second = items[secondIndex];
                    for (int firstBoundsIndex = 0;
                        firstBoundsIndex < first.TextBounds.Count;
                        firstBoundsIndex++)
                    {
                        for (int secondBoundsIndex = 0;
                            secondBoundsIndex < second.TextBounds.Count;
                            secondBoundsIndex++)
                        {
                            OverlapRect firstBounds =
                                first.TextBounds[firstBoundsIndex];
                            OverlapRect secondBounds =
                                second.TextBounds[secondBoundsIndex];
                            if (!firstBounds.Intersects(secondBounds, margin))
                                continue;

                            OverlapRect intersection =
                                firstBounds.IntersectionWith(secondBounds);
                            if (intersection.Width <= 0.0 ||
                                intersection.Height <= 0.0)
                            {
                                continue;
                            }

                            result.Add(new AnnotationOverlapPair
                            {
                                FirstIndex = firstIndex,
                                SecondIndex = secondIndex,
                                Intersection = intersection
                            });
                            break;
                        }
                    }
                }
            }

            result.Sort(ComparePairs);
            return result;
        }

        public static bool TryFindMoveOffset(
            IList<AnnotationOverlapItem> items, int movingIndex,
            double margin, out double deltaX, out double deltaY)
        {
            deltaX = 0.0;
            deltaY = 0.0;
            if (items == null || movingIndex < 0 ||
                movingIndex >= items.Count)
            {
                return false;
            }

            AnnotationOverlapItem moving = items[movingIndex];
            if (moving == null || moving.TextBounds.Count == 0)
                return false;

            double textHeight = ResolveTextHeight(moving);
            double step = Math.Max(0.5, textHeight);
            double[] directionX = new double[]
            {
                1.0, -1.0, 0.0, 0.0, 1.0, 1.0, -1.0, -1.0
            };
            double[] directionY = new double[]
            {
                0.0, 0.0, 1.0, -1.0, 1.0, -1.0, 1.0, -1.0
            };

            double currentStep = step;
            int loopLimit = 10;
            for (int distanceLoop = 1;
                distanceLoop <= loopLimit; distanceLoop++)
            {
                for (int directionIndex = 0;
                    directionIndex < directionX.Length; directionIndex++)
                {
                    double candidateX = directionX[directionIndex] * currentStep;
                    double candidateY = directionY[directionIndex] * currentStep;
                    if (CanPlaceAtOffset(items, movingIndex,
                        candidateX, candidateY, margin))
                    {
                        deltaX = candidateX;
                        deltaY = candidateY;
                        return true;
                    }
                }
                currentStep = currentStep * 1.6;
            }
            return false;
        }

        private static bool CanPlaceAtOffset(
            IList<AnnotationOverlapItem> items, int movingIndex,
            double deltaX, double deltaY, double margin)
        {
            AnnotationOverlapItem moving = items[movingIndex];
            for (int movingBoundsIndex = 0;
                movingBoundsIndex < moving.TextBounds.Count;
                movingBoundsIndex++)
            {
                OverlapRect translated = moving.TextBounds[movingBoundsIndex]
                    .Translated(deltaX, deltaY);
                for (int otherIndex = 0; otherIndex < items.Count;
                    otherIndex++)
                {
                    if (otherIndex == movingIndex)
                        continue;
                    AnnotationOverlapItem other = items[otherIndex];
                    for (int otherBoundsIndex = 0;
                        otherBoundsIndex < other.TextBounds.Count;
                        otherBoundsIndex++)
                    {
                        if (translated.Intersects(
                            other.TextBounds[otherBoundsIndex], margin))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static double ResolveTextHeight(AnnotationOverlapItem item)
        {
            if (item != null && item.TextHeight > 1e-9 &&
                !Double.IsNaN(item.TextHeight) &&
                !Double.IsInfinity(item.TextHeight))
            {
                return item.TextHeight;
            }
            return 1.0;
        }

        private static int ComparePairs(AnnotationOverlapPair left,
            AnnotationOverlapPair right)
        {
            int byY = left.Intersection.MinimumY.CompareTo(
                right.Intersection.MinimumY);
            if (byY != 0)
                return byY;
            int byX = left.Intersection.MinimumX.CompareTo(
                right.Intersection.MinimumX);
            return byX;
        }
    }
}
