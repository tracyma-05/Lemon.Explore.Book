using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>
/// Why there is a bulge on the far side too, in two steps. Above: the Moon pulls Earth's near side hardest, its centre
/// less and its far side least. Below: take away the pull on the centre, which moves Earth as a whole, and what is
/// left pulls the near side towards the Moon and the far side away from it. Exaggerated: the Moon is drawn three Earth
/// radii away instead of sixty.
/// </summary>
public sealed class TidalForceView : Control
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    private const double EarthRadius = 40;
    private const double ArrowUnit = 24;

    /// <summary>Distances from the Moon in Earth radii, far side, centre, near side, as drawn.</summary>
    private static readonly double[] Distances = [4, 3, 2];

    private readonly LabelCache _labels;

    public TidalForceView()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 200 || height < 160) return;

        var centerX = width * 0.34;
        var moonX = width - 24;
        var rowHeight = height / 2;

        // Pull relative to the pull on Earth's centre: (3/d)².
        var pulls = Distances.Select(d => Math.Pow(Distances[1] / d, 2)).ToArray();

        for (var row = 0; row < 2; row++)
        {
            var top = row * rowHeight;
            var y = top + (rowHeight * 0.56);
            var caption = _labels.Get(row == 0 ? "Tides_Force_Pull" : "Tides_Force_Tidal", 11.5, TidesDrawing.Text, true);
            dc.DrawText(caption, new Point(12, top + 8));
            if (row == 1) dc.DrawLine(Paint.Divider, new Point(12, top), new Point(width - 12, top));

            // Earth, stretched into its two bulges in the second row; the Moon off to the right.
            if (row == 1) dc.DrawEllipse(null, Paint.Stretched, new Point(centerX, y), EarthRadius + 9, EarthRadius - 3);
            dc.DrawEllipse(Paint.Earth, Paint.EarthEdge, new Point(centerX, y), EarthRadius, EarthRadius);
            dc.DrawEllipse(Paint.Moon, null, new Point(moonX, y), 10, 10);
            _labels.DrawCentered(dc, "Tides_Label_Moon", new Point(moonX, y + 12), 10, TidesDrawing.DimText);

            for (var i = 0; i < 3; i++)
            {
                var x = centerX + ((i - 1) * EarthRadius);
                var at = new Point(x, y);
                var length = (row == 0 ? pulls[i] : pulls[i] - pulls[1]) * ArrowUnit;
                var pen = row == 0 ? Paint.PullPen : TidesDrawing.ForcePen;
                var brush = row == 0 ? Paint.PullBrush : TidesDrawing.ForceBrush;
                dc.DrawEllipse(Brushes.White, null, at, 2.5, 2.5);
                if (Math.Abs(length) > 1)
                {
                    var end = new Point(x + length, y);
                    dc.DrawLine(pen, at, end);
                    DrawArrowHead(dc, brush, end, new Vector(Math.Sign(length), 0), 7);
                }

                if (row == 0)
                {
                    var key = i switch { 0 => "Tides_Force_Far", 1 => "Tides_Force_Centre", _ => "Tides_Force_Near" };
                    var text = _labels.Get(key, 9.5, TidesDrawing.DimText);
                    dc.DrawText(text, new Point(x - (text.Width / 2), y + EarthRadius + 4));
                }
            }
        }
    }

    private static class Paint
    {
        public static readonly IBrush Earth = Solid(255, 0x1D, 0x4E, 0x89);
        public static readonly IPen EarthEdge = MakePen(Solid(120, 0x8F, 0xD3, 0xFF), 1);
        public static readonly IPen Stretched = MakePen(Solid(220, 0x6F, 0xC3, 0xFF), 1.4, dashed: true);
        public static readonly IBrush Moon = Solid(255, 0xD9, 0xDC, 0xE3);
        public static readonly IPen PullPen = MakePen(SpaceDrawing.Solid(TidesDrawing.MoonBlue), 2);
        public static readonly IBrush PullBrush = SpaceDrawing.Solid(TidesDrawing.MoonBlue);
        public static readonly IPen Divider = MakePen(Solid(30, 255, 255, 255), 1);
    }
}
