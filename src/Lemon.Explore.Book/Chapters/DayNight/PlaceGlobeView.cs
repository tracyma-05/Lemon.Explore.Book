using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>
/// Side panel: Earth seen from space straight above the chosen place (north up) at the current instant, with the
/// line between day and night and the city lights.
/// </summary>
public sealed class PlaceGlobeView : Control
{
    static PlaceGlobeView()
    {
        AffectsRender<PlaceGlobeView>(InstantProperty, PlaceLatitudeProperty, PlaceLongitudeProperty);
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    public static readonly StyledProperty<DateTime> InstantProperty =
        AvaloniaProperty.Register<PlaceGlobeView, DateTime>(nameof(Instant), DateTime.UtcNow);

    public static readonly StyledProperty<double> PlaceLatitudeProperty =
        AvaloniaProperty.Register<PlaceGlobeView, double>(nameof(PlaceLatitude), 39.9);

    public static readonly StyledProperty<double> PlaceLongitudeProperty =
        AvaloniaProperty.Register<PlaceGlobeView, double>(nameof(PlaceLongitude), 116.4);

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

    private readonly GlobeRenderer _globe = new();

    public PlaceGlobeView()
    {
        ClipToBounds = true;
        WorldMap.WarmUp();
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 20 || height < 20) return;

        var radius = (Math.Min(width, height) / 2) - 10;
        var center = new Point(width / 2, height / 2);
        var sun = DayNightGeometry.Sun(Instant);

        // Look down from a little nearer the equator than the place, so both poles' regions stay in view.
        var camera = GlobeCamera.Above(Math.Clamp(PlaceLatitude, -55, 55), PlaceLongitude);
        var dpi = RenderScale();
        _globe.Render((int)Math.Ceiling(2 * radius * dpi), camera, sun);

        dc.DrawEllipse(Atmosphere, null, center, radius * 1.07, radius * 1.07);
        if (_globe.Bitmap is { } bitmap) dc.DrawImage(bitmap, new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
        dc.DrawEllipse(null, Rim, center, radius, radius);

        Point Screen((double X, double Y, bool Front) p) => new(center.X + (p.X * radius), center.Y - (p.Y * radius));

        // The day-night circle where it faces us.
        var line = DayNightGeometry.Circle(sun, 0, 180);
        for (var i = 0; i < line.Length; i++)
        {
            var a = camera.Project(line[i]);
            var b = camera.Project(line[(i + 1) % line.Length]);
            if (a.Front && b.Front) dc.DrawLine(DayNightPaint.DayNightLine, Screen(a), Screen(b));
        }

        foreach (var city in WorldCities.All)
        {
            var p = camera.Project(city.Location);
            if (!p.Front) continue;
            MapPainter.DrawCityLight(dc, Screen(p), DayNightPaint.Darkness(DayNightGeometry.Altitude(sun, city.Latitude, city.Longitude)), 0.6);
        }

        // The Sun's direction, when the point under it is on our side.
        var subsolar = camera.Project(sun.Subsolar);
        if (subsolar.Front) dc.DrawEllipse(DayNightPaint.SunDisc, DayNightPaint.SunRim, Screen(subsolar), 4.5, 4.5);

        var place = camera.Project(new GeoPoint(PlaceLatitude, PlaceLongitude));
        if (place.Front)
        {
            var at = Screen(place);
            dc.DrawEllipse(null, DayNightPaint.PlaceOutline, at, 6.5, 6.5);
            dc.DrawEllipse(null, DayNightPaint.PlaceRing, at, 5.5, 5.5);
            dc.DrawEllipse(DayNightPaint.PlaceFill, DayNightPaint.PlaceOutline, at, 2.5, 2.5);
        }
    }

    private static readonly IBrush Atmosphere = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0x8E, 0xC5, 0xFF), 0.9),
            (Color.FromArgb(100, 0x8E, 0xC5, 0xFF), 0.935),
            (Color.FromArgb(0, 0x8E, 0xC5, 0xFF), 1));

    private static readonly IPen Rim = MakePen(Solid(90, 0x8E, 0xC5, 0xFF), 1);
}
