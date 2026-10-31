using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>
/// The table view: 18 groups by 7 periods with the lanthanides and actinides in two rows under them, each cell coloured
/// by family, state or outer electrons; the group and period numbers; the picked element's tile and shells in the gap
/// over the middle of the table; and a legend for the colouring under it.
/// </summary>
public sealed partial class AtomDiagram
{
    /// <summary>Rows of cells from the top of period 1 to the bottom of the actinides: 7, half a row's gap, 2.</summary>
    private const double TableRows = 9.5;

    /// <summary>Where the table goes: the top left of its first cell, the cell size, and the legend's room under it.</summary>
    private readonly record struct TableLayout(Point Origin, double Cell, Rect Legend);

    private TableLayout LayOutTable()
    {
        var s = Stage();
        const double labelLeft = 18;
        const double labelTop = 16;
        var legendHeight = s.Width < 560 ? 64 : 46;
        var cell = Math.Max(8, Math.Min((s.Width - labelLeft) / 18, (s.Height - labelTop - legendHeight - 8) / TableRows));
        var width = cell * 18;
        var origin = new Point(s.Left + labelLeft + ((s.Width - labelLeft - width) / 2), s.Top + labelTop);
        var legend = new Rect(origin.X - labelLeft, origin.Y + (cell * TableRows) + 10, width + labelLeft, legendHeight);
        return new TableLayout(origin, cell, legend);
    }

    /// <summary>The square of column <paramref name="column"/> (1 to 18), row <paramref name="row"/> (1 to 7, or 9 and 10).</summary>
    private static Rect CellRect(TableLayout layout, int column, int row)
    {
        var y = layout.Origin.Y + ((row - 1) * layout.Cell) - (row >= 9 ? layout.Cell * 0.5 : 0);
        return new Rect(layout.Origin.X + ((column - 1) * layout.Cell), y, layout.Cell, layout.Cell);
    }

    private void DrawTableMode(PaintContext dc, double now)
    {
        var layout = LayOutTable();
        DrawTableNumbers(dc, layout, now);
        foreach (var element in ElementData.Elements)
        {
            var (column, row) = element.Cell;
            DrawCell(dc, CellRect(layout, column, row), element);
        }

        DrawFBlockMarks(dc, layout, now);
        var legendBottom = DrawLegend(dc, layout);

        // The picked element goes in the gap over the middle of the table, or, when the cells are small (a phone) and
        // there is room under the legend, in a bigger panel there.
        var stage = Stage();
        var below = stage.Bottom - legendBottom - 12;
        var area = layout.Cell < 30 && below >= 110
            ? new Rect(stage.Left, legendBottom + 12, stage.Width, Math.Min(150, below))
            : new Rect(CellRect(layout, 3, 1).TopLeft, CellRect(layout, 12, 3).BottomRight).Deflate(new Thickness(layout.Cell * 0.2, 2));
        DrawDetail(dc, area, now);
    }

    /// <summary>The group numbers over the columns and the period numbers beside the rows; each set glows as one.</summary>
    private void DrawTableNumbers(PaintContext dc, TableLayout layout, double now)
    {
        var size = Math.Clamp(layout.Cell * 0.3, 8, 11);
        var groupsBand = new Rect(layout.Origin.X, layout.Origin.Y - 15, layout.Cell * 18, 13);
        var periodsBand = new Rect(layout.Origin.X - 17, layout.Origin.Y, 14, layout.Cell * 7);
        if (Glows("groups")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(now, 5), groupsBand.Inflate(2), 6, 6);
        if (Glows("periods")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(now, 5), periodsBand.Inflate(2), 6, 6);

        for (var group = 1; group <= 18; group++)
        {
            var box = DrawText(dc, Number(group), new Point(layout.Origin.X + ((group - 0.5) * layout.Cell), groupsBand.Center.Y), Ink.Dim, size, TextAnchor.Center);
            AddBoxTarget("groups", box);
        }

        for (var period = 1; period <= 7; period++)
        {
            var box = DrawText(dc, Number(period), new Point(periodsBand.Center.X, layout.Origin.Y + ((period - 0.5) * layout.Cell)), Ink.Dim, size, TextAnchor.Center);
            AddBoxTarget("periods", box);
        }
    }

    /// <summary>A cell: its colour, its symbol, and its number when there is room; the picked one outlined in white.</summary>
    private void DrawCell(PaintContext dc, Rect cell, Element element)
    {
        var color = AtomColours.Of(element, Colouring);
        var box = cell.Deflate(Math.Max(1, cell.Width * 0.05));
        var picked = element.Number == ElementZ;
        var corner = Math.Max(2, box.Width * 0.12);
        dc.DrawRoundedRectangle(FadedBrush(color, picked ? 0.6 : 0.3), picked ? Ink.Selected : FadedPen(color, 0.75, 1), box, corner, corner);
        var symbolSize = box.Height * (box.Height >= 26 ? 0.4 : 0.5);
        if (box.Height >= 26)
        {
            DrawText(dc, Number(element.Number), new Point(box.Left + (box.Width * 0.1), box.Top + (box.Height * 0.2)), Ink.Text, Math.Max(6.5, box.Height * 0.2));
            DrawText(dc, element.Symbol, new Point(box.Center.X, box.Top + (box.Height * 0.62)), Colors.White, symbolSize, TextAnchor.Center, bold: true);
        }
        else
        {
            DrawText(dc, element.Symbol, box.Center, Colors.White, Math.Max(6, symbolSize), TextAnchor.Center, bold: true);
        }

        _targets.AddBox(CellId(element.Number), box);
    }

    /// <summary>The two placeholders in group 3 that say where the rows under the table belong, and those rows' glow.</summary>
    private void DrawFBlockMarks(PaintContext dc, TableLayout layout, double now)
    {
        foreach (var (row, text, category) in new[] { (6, "57–71", ElementCategory.Lanthanide), (7, "89–103", ElementCategory.Actinide) })
        {
            var box = CellRect(layout, 3, row).Deflate(Math.Max(1, layout.Cell * 0.05));
            var color = Colouring == TableColouring.Category ? AtomColours.Category(category) : Ink.Dim;
            dc.DrawRoundedRectangle(FadedBrush(color, 0.12), FadedPen(color, 0.6, 1, dashed: true), box, 3, 3);
            DrawWrapped(dc, text, box.Center, Ink.Text, Math.Max(6, box.Height * 0.22), box.Width - 2);
            AddBoxTarget("fblock", box);
        }

        var rows = new Rect(CellRect(layout, 3, 9).TopLeft, CellRect(layout, 17, 10).BottomRight);
        if (Glows("fblock")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(now, 5), rows.Inflate(3), 6, 6);
        _targets.AddBox("fblock", new Rect(rows.Left - 2, rows.Top, 4, rows.Height));
    }

    /// <summary>
    /// The picked element in the gap over groups 3 to 12: its tile, its shells turning, and its name, family, mass and
    /// electrons per shell.
    /// </summary>
    private void DrawDetail(PaintContext dc, Rect area, double now)
    {
        var element = ElementData.Of(ElementZ);
        var side = Math.Min(area.Height, area.Width * 0.3);
        var color = AtomColours.Of(element, Colouring);
        var tile = new Rect(area.Left, area.Top, side, side);
        DrawTile(dc, tile, element, color, glow: Glows("detail"), now: now);
        _targets.AddBox("detail", tile);

        var shellsCenter = new Point(tile.Right + (side * 0.62), area.Center.Y);
        var shellsRadius = side * 0.48;
        DrawMiniShells(dc, shellsCenter, shellsRadius, element.Shells, now);
        _targets.Add("minishells", shellsCenter, shellsRadius);

        // The words, if there is room right of the shells.
        var left = shellsCenter.X + shellsRadius + 10;
        var room = area.Right - left;
        if (room < 50 || !ShowLabels) return;
        var size = Math.Clamp(side * 0.13, 9, 14);
        var y = area.Top + (size * 0.9);
        var name = _labels.Text(Loc(element.NameKey), size + 2, Colors.White, bold: true);
        dc.DrawText(name, new Point(left, y - (name.Height / 2)));
        var lines = new[]
        {
            (Loc($"Atom_Cat_{element.Category}"), AtomColours.Category(element.Category), size),
            (LocFormat("Atom_Label_Mass", AtomViewModel.MassText(element)), Ink.Text, size * 0.9),
            (LocFormat("Atom_Label_Shells", AtomViewModel.ShellsText(element.Shells)), Ink.Electron, size * 0.9),
        };

        // As many lines as fit in the area's height.
        y += size * 1.6;
        foreach (var (text, lineColor, lineSize) in lines)
        {
            if (y + (lineSize * 0.6) > area.Bottom) break;
            DrawFitted(dc, text, new Point(left, y), lineColor, lineSize, room);
            y += size * 1.45;
        }
    }

    /// <summary>Left-aligned text, smaller until it fits.</summary>
    private void DrawFitted(PaintContext dc, string text, Point at, Color color, double size, double maxWidth)
    {
        var fitted = size;
        while (fitted > 7 && _labels.Text(text, fitted, color).Width > maxWidth) fitted -= 0.5;
        DrawText(dc, text, at, color, fitted);
    }

    /// <summary>The colouring's key under the table: the families, the four states, or one to eight outer electrons; returns its bottom.</summary>
    private double DrawLegend(PaintContext dc, TableLayout layout)
    {
        var area = layout.Legend;
        if (Colouring == TableColouring.Outer)
        {
            DrawOuterLegend(dc, area);
            return area.Top + 18;
        }

        var items = Colouring == TableColouring.State
            ? Enum.GetValues<ElementState>().Select(s => ("state_" + s, Loc($"Atom_State_{s}"), AtomColours.State(s))).ToList()
            : Enum.GetValues<ElementCategory>().Select(c => ("cat_" + c, Loc($"Atom_Cat_{c}"), AtomColours.Category(c))).ToList();

        var size = 10.5;
        var x = area.Left;
        var y = area.Top + 8;
        foreach (var (id, text, color) in items)
        {
            var label = _labels.Text(text, size, Ink.Text);
            var width = 16 + label.Width + 12;
            if (x + width > area.Right && x > area.Left)
            {
                x = area.Left;
                y += 19;
            }

            dc.DrawRoundedRectangle(FadedBrush(color, 0.5), FadedPen(color, 0.9, 1), new Rect(x, y - 5, 10, 10), 2, 2);
            dc.DrawText(label, new Point(x + 15, y - (label.Height / 2)));
            AddBoxTarget(id, new Rect(x - 2, y - 8, width - 8, 16));
            x += width;
        }

        return y + 9;
    }

    /// <summary>"Outer electrons:" and eight swatches numbered 1 to 8, all one target.</summary>
    private void DrawOuterLegend(PaintContext dc, Rect area)
    {
        var y = area.Top + 8;
        var title = DrawText(dc, Loc("Atom_Label_OuterLegend"), new Point(area.Left, y), Ink.Text, 10.5);
        var x = title.Right + 10;
        for (var outer = 1; outer <= 8; outer++)
        {
            var swatch = new Rect(x, y - 8, 20, 16);
            var color = AtomColours.Outer(outer);
            dc.DrawRoundedRectangle(FadedBrush(color, 0.5), FadedPen(color, 0.9, 1), swatch, 3, 3);
            DrawText(dc, Number(outer), swatch.Center, Colors.White, 10, TextAnchor.Center, bold: true);
            x += 24;
        }

        AddBoxTarget("outer", new Rect(area.Left, y - 9, x - area.Left, 18));
    }

    /// <summary>The name of the element under the pointer, over its cell (cells are too small to carry it).</summary>
    private void DrawCellHover(PaintContext dc)
    {
        if (CellNumber(_targets.Hover) is not { } number || !_targets.TryGet(CellId(number), out var cell) || cell.Box is not { } box) return;
        var element = ElementData.Of(number);
        var text = $"{element.Number} {element.Symbol} · {Loc(element.NameKey)}";
        DrawPill(dc, text, new Point(box.Center.X, box.Top - 14), Colors.White, 11.5);
    }
}
