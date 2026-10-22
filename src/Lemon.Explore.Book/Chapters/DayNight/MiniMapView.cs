using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>Side panel: the same instant on the flat map, to compare with the globe.</summary>
public sealed class MiniMapView : Control
{
    static MiniMapView()
    {
        AffectsRender<MiniMapView>(InstantProperty, PlaceLatitudeProperty, PlaceLongitudeProperty);
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    public static readonly StyledProperty<DateTime> InstantProperty =
        AvaloniaProperty.Register<MiniMapView, DateTime>(nameof(Instant), DateTime.UtcNow);

    public static readonly StyledProperty<double> PlaceLatitudeProperty =
        AvaloniaProperty.Register<MiniMapView, double>(nameof(PlaceLatitude), 39.9);

    public static readonly StyledProperty<double> PlaceLongitudeProperty =
        AvaloniaProperty.Register<MiniMapView, double>(nameof(PlaceLongitude), 116.4);

    /// <summary>UTC.</summary>
    public DateTime Instant
    {
        get => GetValue(InstantProperty);
        set => SetValue(InstantProperty, value);
    }

    public double PlaceLatitude
    {
        get => GetValue(PlaceLatitudeProperty);
        set => SetValue(PlaceLatitudeProperty, value);
    }

    public double PlaceLongitude
    {
        get => GetValue(PlaceLongitudeProperty);
        set => SetValue(PlaceLongitudeProperty, value);
    }

    private readonly MapPainter _painter = new();

    public MiniMapView()
    {
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 20 || height < 20) return;

        var mapWidth = Math.Min(width, height * 2);
        var map = new Rect((width - mapWidth) / 2, (height - (mapWidth / 2)) / 2, mapWidth, mapWidth / 2);
        var sun = DayNightGeometry.Sun(Instant);

        dc.PushClip(new RectangleGeometry(map));
        _painter.DrawSurface(dc, map);
        MapPainter.DrawGraticule(dc, map);
        _painter.DrawNight(dc, map, sun);
        MapPainter.DrawCircle(dc, map, sun, 0, DayNightPaint.DayNightLine, 360);
        MapPainter.DrawSun(dc, map, sun, 4);

        var place = MapPainter.ToScreen(map, PlaceLatitude, PlaceLongitude);
        dc.DrawEllipse(null, DayNightPaint.PlaceRing, place, 4.5, 4.5);
        dc.DrawEllipse(DayNightPaint.PlaceFill, DayNightPaint.PlaceOutline, place, 2, 2);
        dc.Pop();
    }
}
