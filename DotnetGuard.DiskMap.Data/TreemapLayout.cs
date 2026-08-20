using System;
using System.Collections.Generic;
using System.Linq;
using DotnetGuard.DiskMap.Core.Models;

namespace DotnetGuard.DiskMap.Data
{
    public static class TreemapLayout
    {
        public static List<(LayoutRect Rect, DiskNode Node)> Compute(List<DiskNode> nodes, LayoutRect bounds)
        {
            List<(LayoutRect, DiskNode)> result = new List<(LayoutRect, DiskNode)>();

            if (nodes.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return result;
            }

            double total = nodes.Sum(n => (double)n.Size);

            if (total <= 0)
            {
                return result;
            }

            double areaScale = (bounds.Width * bounds.Height) / total;

            List<(DiskNode Node, double Area)> sorted = nodes
                .OrderByDescending(n => n.Size)
                .Select(n => (Node: n, Area: n.Size * areaScale))
                .Where(x => x.Area > 0.5)
                .ToList();

            Squarify(sorted, new List<(DiskNode, double)>(), bounds, result);
            return result;
        }

        private static void Squarify(List<(DiskNode Node, double Area)> remaining,
            List<(DiskNode Node, double Area)> row, LayoutRect bounds, List<(LayoutRect, DiskNode)> result)
        {
            if (remaining.Count == 0)
            {
                if (row.Count > 0)
                {
                    LayoutRow(row, bounds, result);
                }

                return;
            }

            double side = Math.Min(bounds.Width, bounds.Height);
            (DiskNode Node, double Area) next = remaining[0];
            List<(DiskNode, double)> rowWithNext = new List<(DiskNode, double)>(row) { next };

            if (row.Count == 0 || Worst(row, side) >= Worst(rowWithNext, side))
            {
                Squarify(remaining.Skip(1).ToList(), rowWithNext, bounds, result);
            }
            else
            {
                LayoutRect remainingBounds = LayoutRow(row, bounds, result);
                Squarify(remaining, new List<(DiskNode, double)>(), remainingBounds, result);
            }
        }

        private static double Worst(List<(DiskNode Node, double Area)> row, double side)
        {
            double sum = row.Sum(r => r.Area);
            double max = row.Max(r => r.Area);
            double min = row.Min(r => r.Area);
            double sideSquared = side * side;
            double sumSquared = sum * sum;

            return Math.Max((sideSquared * max) / sumSquared, sumSquared / (sideSquared * min));
        }

        private static LayoutRect LayoutRow(List<(DiskNode Node, double Area)> row, LayoutRect bounds, List<(LayoutRect, DiskNode)> result)
        {
            double rowSum = row.Sum(r => r.Area);
            bool horizontal = bounds.Width >= bounds.Height;

            if (horizontal)
            {
                double rowWidth = rowSum / bounds.Height;
                double offsetY = bounds.Y;

                foreach ((DiskNode Node, double Area) item in row)
                {
                    double itemHeight = item.Area / rowWidth;
                    result.Add((new LayoutRect(bounds.X, offsetY, rowWidth, itemHeight), item.Node));
                    offsetY += itemHeight;
                }

                return new LayoutRect(bounds.X + rowWidth, bounds.Y, Math.Max(0, bounds.Width - rowWidth), bounds.Height);
            }
            else
            {
                double rowHeight = rowSum / bounds.Width;
                double offsetX = bounds.X;

                foreach ((DiskNode Node, double Area) item in row)
                {
                    double itemWidth = item.Area / rowHeight;
                    result.Add((new LayoutRect(offsetX, bounds.Y, itemWidth, rowHeight), item.Node));
                    offsetX += itemWidth;
                }

                return new LayoutRect(bounds.X, bounds.Y + rowHeight, bounds.Width, Math.Max(0, bounds.Height - rowHeight));
            }
        }
    }
}
