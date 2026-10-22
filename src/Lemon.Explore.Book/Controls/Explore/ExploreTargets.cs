using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Controls.Explore;

/// <summary>
/// A thing in an Explore diagram that can be pointed at, hovered and clicked: an id and a circle on screen, or a box
/// for a text label (<see cref="Box"/>), which is ringed with a rounded rectangle instead.
/// </summary>
internal readonly record struct ExploreTarget(string Id, Point Center, double Radius, Rect? Box = null);

/// <summary>
/// The clickable things of one frame of an Explore diagram. The diagram adds them while it renders (where it just drew
/// them), finds the one under the pointer for hover and click, and draws the lesson's highlight ring and the hover ring
/// over them.
/// </summary>
internal sealed class ExploreTargets
{
    private readonly List<ExploreTarget> _items = [];

    /// <summary>The target under the pointer, kept between frames; the diagram sets it on mouse move.</summary>
    public string? Hover { get; set; }

    public void Clear() => _items.Clear();

    public void Add(string id, Point center, double radius) => _items.Add(new ExploreTarget(id, center, Math.Max(radius, 6)));

    /// <summary>A text label (or anything box-shaped) as a target.</summary>
    public void AddBox(string id, Rect box) =>
        _items.Add(new ExploreTarget(id, new Point(box.X + (box.Width / 2), box.Y + (box.Height / 2)),
            Math.Max(6, Math.Max(box.Width, box.Height) / 2), box));

    public bool TryGet(string id, out ExploreTarget target)
    {
        // The last one added for an id wins: it was drawn on top.
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].Id != id) continue;
            target = _items[i];
            return true;
        }

        target = default;
        return false;
    }

    /// <summary>
    /// The target under <paramref name="point"/> (within <paramref name="slop"/> DIPs of its circle or box). Where they
    /// overlap, the one whose centre is relatively nearest wins, so a small marker on top of a big body stays clickable.
    /// </summary>
    public ExploreTarget? Find(Point point, double slop = 6)
    {
        ExploreTarget? best = null;
        var bestScore = double.PositiveInfinity;
        foreach (var item in _items)
        {
            double score;
            if (item.Box is { } box)
            {
                var grown = box.Inflate(slop);
                if (!grown.Contains(point)) continue;

                // 0 at the centre, 1 at the edge, on whichever axis is relatively further out.
                score = Math.Max(Math.Abs(point.X - item.Center.X) / (grown.Width / 2), Math.Abs(point.Y - item.Center.Y) / (grown.Height / 2));
            }
            else
            {
                var distance = ((Vector)(point - item.Center)).Length;
                if (distance > item.Radius + slop) continue;
                score = distance / (item.Radius + slop);
            }

            if (score >= bestScore) continue;
            best = item;
            bestScore = score;
        }

        return best;
    }

    /// <summary>
    /// Over the finished frame: a ring round the thing whose explanation is showing, the hover ring with the thing's
    /// name, and the walkthrough's pulsing ring (unless <paramref name="isLine"/> says the diagram glows it itself).
    /// </summary>
    public void DrawOverlays(DrawingContext dc, LabelCache labels, string? selected, string? highlight,
        Func<string, string?> titleKey, Func<string, bool> isLine, double now, double width)
    {
        if (selected is not null && TryGet(selected, out var chosen))
        {
            DrawHover(dc, chosen, selected: true);
        }

        if (Hover is { } hover && hover != selected && TryGet(hover, out var hovered))
        {
            DrawHover(dc, hovered, selected: false);

            // Over a box the box already shows the words, so only round things get their name.
            if (hovered.Box is null && titleKey(hover) is { } key)
            {
                var text = LocalizationService.Instance.GetString(key);
                var label = labels.Text(text, 11.5, Colors.White);
                var at = new Point(
                    Math.Clamp(hovered.Center.X - (label.Width / 2) - 6, 6, Math.Max(6, width - label.Width - 18)),
                    Math.Max(4, hovered.Center.Y - hovered.Radius - label.Height - 14));
                labels.DrawTextPill(dc, text, at, 11.5, Colors.White);
            }
        }

        if (highlight is not null && !isLine(highlight) && TryGet(highlight, out var target))
        {
            DrawHighlight(dc, target, now);
        }
    }

    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);

    /// <summary>The lesson's "look here" ring: a pulsing gold ring (or rounded box) with a ripple going out from it.</summary>
    public static void DrawHighlight(DrawingContext dc, ExploreTarget target, double now)
    {
        var pulse = 0.5 + (0.5 * Math.Sin(now * 4.2));
        var ripple = (now * 0.9) % 1;
        var ring = Pen(Color.FromArgb((byte)(150 + (100 * pulse)), Gold.R, Gold.G, Gold.B), 2.2);
        var wave = Pen(Color.FromArgb((byte)(150 * (1 - ripple)), Gold.R, Gold.G, Gold.B), 1.4);
        if (target.Box is { } box)
        {
            DrawRounded(dc, ring, box, 5 + (2 * pulse));
            DrawRounded(dc, wave, box, 7 + (14 * ripple));
            return;
        }

        var r = target.Radius + 6 + (2 * pulse);
        dc.DrawEllipse(null, ring, target.Center, r, r);
        var rr = r + (18 * ripple);
        dc.DrawEllipse(null, wave, target.Center, rr, rr);
    }

    /// <summary>A glow along a line or curve, for highlights that are not round (an orbit, an axis, a path).</summary>
    public static IPen GlowPen(double now, double thickness = 7)
    {
        var pulse = 0.5 + (0.5 * Math.Sin(now * 4.2));
        return Pen(Color.FromArgb((byte)(70 + (70 * pulse)), Gold.R, Gold.G, Gold.B), thickness);
    }

    /// <summary>The ring under the pointer, and around the thing whose explanation is showing.</summary>
    public static void DrawHover(DrawingContext dc, ExploreTarget target, bool selected)
    {
        var pen = selected ? SelectedPen : HoverPen;
        var fill = selected ? SelectedFill : null;
        if (target.Box is { } box)
        {
            box = box.Inflate(new Thickness(4, 3));
            var corner = Math.Min(box.Height / 2, 12);
            dc.DrawRectangle(fill, pen, box, corner, corner);
            return;
        }

        var r = target.Radius + 4;
        dc.DrawEllipse(fill, pen, target.Center, r, r);
    }

    private static void DrawRounded(DrawingContext dc, IPen pen, Rect box, double grow)
    {
        box = box.Inflate(grow);
        dc.DrawRectangle(null, pen, box, Math.Min(box.Height / 2, 14), Math.Min(box.Height / 2, 14));
    }

    private static readonly IPen HoverPen = Pen(Color.FromArgb(200, 255, 255, 255), 1.4);
    private static readonly IPen SelectedPen = Pen(Color.FromArgb(230, 0x8E, 0xC5, 0xFF), 1.8);
    private static readonly IBrush SelectedFill = SpaceDrawing.Solid(Color.FromArgb(28, 0x8E, 0xC5, 0xFF));

    private static IPen Pen(Color color, double thickness) => SpaceDrawing.MakePen(SpaceDrawing.Solid(color), thickness);
}
