using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>
/// Side panel: day, twilight and night at the chosen place through the year, against the time on its clocks. The day
/// band widens and narrows with the seasons, and jumps an hour where daylight saving starts and ends.
/// </summary>
public sealed class SunClockChart : Control
{
    static SunClockChart()
    {
        AffectsRender<SunClockChart>(InstantProperty, PlaceLatitudeProperty, PlaceLongitudeProperty, PlaceCityProperty);
    }

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

    public static readonly StyledProperty<DateTime> InstantProperty =
        AvaloniaProperty.Register<SunClockChart, DateTime>(nameof(Instant), DateTime.UtcNow);

    public static readonly StyledProperty<double> PlaceLatitudeProperty =
        AvaloniaProperty.Register<SunClockChart, double>(nameof(PlaceLatitude), 39.9);

    public static readonly StyledProperty<double> PlaceLongitudeProperty =
        AvaloniaProperty.Register<SunClockChart, double>(nameof(PlaceLongitude), 116.4);

    public static readonly StyledProperty<string> PlaceCityProperty =
        AvaloniaProperty.Register<SunClockChart, string>(nameof(PlaceCity), "Beijing");

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

    public string PlaceCity
    {
        get => GetValue(PlaceCityProperty);
        set => SetValue(PlaceCityProperty, value);
    }

    /// <summary>Ten-minute rows.</summary>
    private const int Rows = 144;

    private const double Left = 30;
    private const double Right = 8;

    private readonly LabelCache _labels;
    private WriteableBitmap? _bitmap;
    private (double Lat, double Lon, string City, int Year) _key;

    public SunClockChart()
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
        if (width < Left + Right + 20 || height < 60) return;

        var plot = new Rect(Left, 8, width - Left - Right, height - 28);
        var zone = DayNightDiagram.ZoneOf(PlaceCity, PlaceLongitude);
        var local = TimeZoneInfo.ConvertTimeFromUtc(Instant, zone);
        var year = local.Year;
        var days = DateTime.IsLeapYear(year) ? 366 : 365;

        var key = (PlaceLatitude, PlaceLongitude, PlaceCity ?? string.Empty, year);
        if (_bitmap is null || key != _key)
        {
            _key = key;
            _bitmap = Build(year, days, zone);
        }

        dc.DrawImage(_bitmap, plot);
        DrawAxes(dc, plot, year, days);

        // Now: the date and the time on the clock.
        var x = plot.X + (plot.Width * (local.DayOfYear - 0.5) / days);
        var y = plot.Bottom - (plot.Height * local.TimeOfDay.TotalHours / 24);
        dc.DrawLine(Paint.Today, new Point(x, plot.Top), new Point(x, plot.Bottom));
        dc.DrawEllipse(Paint.NowDot, Paint.NowOutline, new Point(x, y), 4, 4);
    }

    private WriteableBitmap Build(int year, int days, TimeZoneInfo zone)
    {
        var pixels = new int[days * Rows];
        var (sinLat, cosLat) = Math.SinCos(DayNightGeometry.ToRadians(PlaceLatitude));
        for (var day = 0; day < days; day++)
        {
            // The Sun at local noon; through the day only the Earth's turn (15° an hour) changes much.
            var date = new DateTime(year, 1, 1).AddDays(day);
            var offset = zone.GetUtcOffset(date.AddHours(12));
            var noonUtc = DateTime.SpecifyKind(date.AddHours(12) - offset, DateTimeKind.Utc);
            var sun = DayNightGeometry.Sun(noonUtc);
            var (sinDec, cosDec) = Math.SinCos(DayNightGeometry.ToRadians(sun.Declination));
            var noonHourAngle = DayNightGeometry.HourAngle(sun, PlaceLongitude);

            for (var row = 0; row < Rows; row++)
            {
                var hours = 24 - ((row + 0.5) * 24 / Rows);
                var hourAngle = noonHourAngle + (15 * (hours - 12));
                var sine = (sinLat * sinDec) + (cosLat * cosDec * Math.Cos(DayNightGeometry.ToRadians(hourAngle)));
                pixels[(row * days) + day] = ColorFor(DayNightGeometry.ToDegrees(Math.Asin(Math.Clamp(sine, -1, 1))));
            }
        }

        var bitmap = new WriteableBitmap(new PixelSize(days, Rows), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        SpaceDrawing.WritePixels(bitmap, pixels, days, Rows);
        return bitmap;
    }

    /// <summary>Sky colours by the Sun's altitude: warm near the horizon, then the three twilights and night.</summary>
    private static int ColorFor(double altitude)
    {
        Color color = altitude switch
        {
            >= DayNightGeometry.SunriseAltitude => Lerp(Paint.LowSun, Paint.HighSun, Math.Clamp((altitude + 0.833) / 12, 0, 1)),
            >= DayNightGeometry.CivilTwilight => Paint.Civil,
            >= DayNightGeometry.NauticalTwilight => Paint.Nautical,
            >= DayNightGeometry.AstronomicalTwilight => Paint.Astronomical,
            _ => Paint.NightColor,
        };
        return (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
    }

    private void DrawAxes(PaintContext dc, Rect plot, int year, int days)
    {
        foreach (var hours in new[] { 0, 6, 12, 18, 24 })
        {
            var y = plot.Bottom - (plot.Height * hours / 24.0);
            dc.DrawLine(hours == 12 ? Paint.Midline : Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = _labels.Text($"{hours}:00", 9.5, Paint.Axis);
            dc.DrawText(text, new Point(plot.Left - text.Width - 4, y - (text.Height / 2)));
        }

        var culture = LocalizationService.Instance.CurrentCulture;
        for (var month = 1; month <= 12; month++)
        {
            var x = plot.X + (plot.Width * (new DateTime(year, month, 1).DayOfYear - 1) / days);
            dc.DrawLine(Paint.Tick, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            if (month % 2 == 0) continue;

            var text = _labels.Text(culture.DateTimeFormat.GetAbbreviatedMonthName(month), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(x + 2, plot.Bottom + 4));
        }

        dc.DrawRectangle(null, Paint.Frame, plot);
    }

    internal static class Paint
    {
        public static readonly Color Axis = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
        public static readonly Color LowSun = Color.FromRgb(0xD9, 0x96, 0x5A);
        public static readonly Color HighSun = Color.FromRgb(0x6C, 0xAE, 0xE6);
        public static readonly Color Civil = Color.FromRgb(0x33, 0x52, 0x8C);
        public static readonly Color Nautical = Color.FromRgb(0x21, 0x35, 0x68);
        public static readonly Color Astronomical = Color.FromRgb(0x15, 0x21, 0x46);
        public static readonly Color NightColor = Color.FromRgb(0x0A, 0x10, 0x24);

        public static readonly IPen Tick = MakePen(Solid(90, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(40, 255, 255, 255), 1);
        public static readonly IPen Midline = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Frame = MakePen(Solid(50, 255, 255, 255), 1);
        public static readonly IPen Today = MakePen(Solid(170, 255, 255, 255), 1);
        public static readonly IBrush NowDot = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IPen NowOutline = MakePen(Solid(255, 10, 14, 24), 1.2);
    }
}
