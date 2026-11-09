using PurgeWin.Utilities;
using Windows.Foundation;

namespace PurgeWin.Models;

public sealed record TreemapItem(string Id, double Weight);

public sealed record TreemapTile(string Id, Rect Rect, double Weight)
{
    public Rect Frame => Rect;
}

/// Purge for Windows: squarified treemap (Bruls, Huizing and van Wijk).
public static class TreemapLayout
{
    public const double MinimumTileSide = 4.0;

    public static List<TreemapTile> Squarify(IReadOnlyList<TreemapItem> rawItems, Rect rect, double spacing = 3.0)
    {
        var items = rawItems.Where(i => i.Weight > 0).ToList();
        var total = items.Sum(i => i.Weight);
        if (total <= 0 || rect.Width <= 0 || rect.Height <= 0) return [];

        var scale = (rect.Width * rect.Height) / total;
        var tiles = new List<TreemapTile>();
        var remaining = rect;
        var row = new List<TreemapItem>();
        var index = 0;

        while (index < items.Count)
        {
            var item = items[index];
            var side = Math.Min(remaining.Width, remaining.Height);
            var current = WorstAspect(row, side, scale);
            var nextRow = new List<TreemapItem>(row) { item };
            var extended = WorstAspect(nextRow, side, scale);

            if (row.Count == 0 || extended <= current)
            {
                row.Add(item);
                index++;
            }
            else
            {
                remaining = LayoutRow(row, remaining, scale, spacing, tiles);
                row.Clear();
            }
        }
        if (row.Count > 0) LayoutRow(row, remaining, scale, spacing, tiles);

        return tiles.Where(t => t.Rect.Width >= MinimumTileSide && t.Rect.Height >= MinimumTileSide).ToList();
    }

    private static double WorstAspect(List<TreemapItem> row, double side, double scale)
    {
        if (row.Count == 0 || side <= 0) return double.PositiveInfinity;
        var sum = 0.0;
        var largest = 0.0;
        var smallest = double.PositiveInfinity;
        foreach (var item in row)
        {
            var area = item.Weight * scale;
            sum += area;
            if (area > largest) largest = area;
            if (area < smallest) smallest = area;
        }
        if (sum <= 0 || smallest <= 0) return double.PositiveInfinity;
        var side2 = side * side;
        return Math.Max((side2 * largest) / (sum * sum), (sum * sum) / (side2 * smallest));
    }

    private static Rect LayoutRow(List<TreemapItem> row, Rect rect, double scale, double spacing, List<TreemapTile> tiles)
    {
        var rowArea = row.Sum(i => i.Weight * scale);
        var horizontal = rect.Width >= rect.Height;
        var thickness = horizontal ? rowArea / Math.Max(1, rect.Height) : rowArea / Math.Max(1, rect.Width);
        var offset = 0.0;
        var half = spacing / 2.0;

        foreach (var item in row)
        {
            var area = item.Weight * scale;
            var length = thickness > 0 ? area / thickness : 0;
            var rawRect = horizontal
                ? new Rect(rect.X, rect.Y + offset, Math.Max(0, thickness), Math.Max(0, length))
                : new Rect(rect.X + offset, rect.Y, Math.Max(0, length), Math.Max(0, thickness));
            var insetRect = new Rect(
                rawRect.X + half,
                rawRect.Y + half,
                Math.Max(0, rawRect.Width - spacing),
                Math.Max(0, rawRect.Height - spacing));
            tiles.Add(new TreemapTile(item.Id, insetRect, item.Weight));
            offset += length;
        }

        return horizontal
            ? new Rect(rect.X + thickness, rect.Y, Math.Max(0, rect.Width - thickness), rect.Height)
            : new Rect(rect.X, rect.Y + thickness, rect.Width, Math.Max(0, rect.Height - thickness));
    }
}
