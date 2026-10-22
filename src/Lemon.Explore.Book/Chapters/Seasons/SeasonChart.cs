using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Seasons;

public enum SeasonChartKind
{
    /// <summary>Hours of daylight through the year at the latitude, and at the same latitude across the equator.</summary>
    DayLength,

    /// <summary>The distance to the Sun through the year, above the daily sunlight at the latitude north and south.</summary>
    Distance,
}

/// <summary>A year-long chart beside the diagram, with the equinoxes and solstices marked and the current date.</summary>
public sealed class SeasonChart : SeasonFigure
{
    public static readonly StyledProperty<SeasonChartKind> KindProperty =
        AvaloniaProperty.Register<SeasonChart, SeasonChartKind>(nameof(Kind));

    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<SeasonChart, double>(nameof(Day));

    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<SeasonChart, double>(nameof(Latitude), 40);

    public static readonly StyledProperty<double> TiltProperty =
        AvaloniaProperty.Register<SeasonChart, double>(nameof(Tilt), SeasonsGeometry.EarthTilt);

    static SeasonChart()
    {
        AffectsRender<SeasonChart>(KindProperty, DayProperty, LatitudeProperty, TiltProperty);
    }

    public SeasonChartKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public double Tilt
    {
        get => GetValue(TiltProperty);
        set => SetValue(TiltProperty, value);
    }

    private const int Samples = 146;
    private const double Left = 34;
    private const double Right = 10;

    public override void Render(DrawingContext dc)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < Left + Right + 10 || height < 60)
        {
            return;
        }

        var plot = new Rect(Left, 10, width - Left - Right, height - 32);
        DrawMonths(dc, plot);

        if (Kind == SeasonChartKind.DayLength)
        {
            DrawDayLength(dc, plot);
        }
        else
        {
            var gap = 16;
            var top = new Rect(plot.X, plot.Y, plot.Width, (plot.Height - gap) * 0.42);
            var bottom = new Rect(plot.X, top.Bottom + gap, plot.Width, plot.Height - top.Height - gap);
            DrawDistance(dc, top);
            DrawSunlight(dc, bottom);
        }
    }

    private static double X(Rect plot, double day) => plot.X + (plot.Width * day / SeasonsGeometry.Year);

    private static double Y(Rect plot, double value, double min, double max) =>
        plot.Bottom - (plot.Height * (value - min) / (max - min));

    /// <summary>Month ticks along the bottom (every other one labelled) and the equinoxes and solstices.</summary>
    private void DrawMonths(DrawingContext dc, Rect plot)
    {
        var culture = LocalizationService.Instance.CurrentCulture;
        for (var month = 1; month <= 12; month++)
        {
            var day = new DateTime(2026, month, 1).DayOfYear - 1;
            var x = X(plot, day);
            dc.DrawLine(Paint.Tick, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            if (month % 2 == 0)
            {
                continue;
            }

            var name = culture.DateTimeFormat.GetAbbreviatedMonthName(month);
            var text = Labels.Text(name, 9.5, Paint.Axis);
            dc.DrawText(text, new Point(x + 2, plot.Bottom + 5));
        }

        foreach (var e in Enum.GetValues<SeasonEvent>())
        {
            var x = X(plot, SeasonsGeometry.EventDay(e));
            dc.DrawLine(Paint.EventLine, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }
    }

    private void DrawDayLength(DrawingContext dc, Rect plot)
    {
        foreach (var hours in new[] { 0, 6, 12, 18, 24 })
        {
            var y = Y(plot, hours, 0, 24);
            dc.DrawLine(hours == 12 ? Paint.Midline : Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = Labels.Text(hours.ToString(CultureInfo.InvariantCulture), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(plot.Left - text.Width - 6, y - (text.Height / 2)));
        }

        var latitude = Math.Clamp(Latitude, -90, 90);
        var tilt = Math.Clamp(Tilt, 0, 90);
        double Hours(double day, double lat) => SeasonsGeometry.DayLength(lat, SeasonsGeometry.Declination(day, tilt));

        dc.DrawGeometry(null, Paint.Opposite, Curve(plot, day => Y(plot, Hours(day, -latitude), 0, 24)));
        dc.DrawGeometry(null, Paint.Main, Curve(plot, day => Y(plot, Hours(day, latitude), 0, 24)));
        DrawToday(dc, plot, Y(plot, Hours(Day, latitude), 0, 24), Paint.MainDot);
    }

    private void DrawDistance(DrawingContext dc, Rect plot)
    {
        const double min = 146.5;
        const double max = 152.5;
        static double Km(double day) => SeasonsGeometry.SunDistance(day) * SeasonsGeometry.AstronomicalUnitKm / 1e6;

        foreach (var value in new[] { 147.0, 152.0 })
        {
            var y = Y(plot, value, min, max);
            dc.DrawLine(Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = Labels.Text(value.ToString("0", CultureInfo.InvariantCulture), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(plot.Left - text.Width - 6, y - (text.Height / 2)));
        }

        dc.DrawGeometry(null, Paint.Distance, Curve(plot, day => Y(plot, Km(day), min, max)));
        DrawToday(dc, plot, Y(plot, Km(Day), min, max), Paint.DistanceDot);
    }

    private void DrawSunlight(DrawingContext dc, Rect plot)
    {
        var latitude = Math.Abs(Math.Clamp(Latitude, -90, 90));
        var tilt = Math.Clamp(Tilt, 0, 90);
        double Sunlight(double day, double lat) => SeasonsGeometry.DailySunlight(lat, SeasonsGeometry.Declination(day, tilt), SeasonsGeometry.SunDistance(day));

        // Scale to the brightest day of either hemisphere, in steps of 50 %.
        var peak = 0.0;
        for (var i = 0; i < Samples; i++)
        {
            var day = SeasonsGeometry.Year * i / Samples;
            peak = Math.Max(peak, Math.Max(Sunlight(day, latitude), Sunlight(day, -latitude)));
        }

        var max = Math.Max(0.5, Math.Ceiling(peak * 1.05 / 0.5) * 0.5);
        for (var value = 0.0; value <= max + 1e-9; value += max > 1.5 ? 1 : 0.5)
        {
            var y = Y(plot, value, 0, max);
            dc.DrawLine(Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = Labels.Text(value.ToString("0%", CultureInfo.InvariantCulture), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(plot.Left - text.Width - 4, y - (text.Height / 2)));
        }

        dc.DrawGeometry(null, Paint.South, Curve(plot, day => Y(plot, Sunlight(day, -latitude), 0, max)));
        dc.DrawGeometry(null, Paint.Main, Curve(plot, day => Y(plot, Sunlight(day, latitude), 0, max)));
        DrawToday(dc, plot, Y(plot, Sunlight(Day, -latitude), 0, max), Paint.SouthDot, line: false);
        DrawToday(dc, plot, Y(plot, Sunlight(Day, latitude), 0, max), Paint.MainDot);
    }

    private static StreamGeometry Curve(Rect plot, Func<double, double> y)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i <= Samples; i++)
            {
                var day = SeasonsGeometry.Year * i / Samples;
                var point = new Point(X(plot, day), y(day));
                if (i == 0)
                {
                    ctx.BeginFigure(point, false);
                }
                else
                {
                    ctx.LineTo(point);
                }
            }

            ctx.EndFigure(false);
        }

        return geometry;
    }

    private void DrawToday(DrawingContext dc, Rect plot, double y, IBrush dot, bool line = true)
    {
        var x = X(plot, SeasonsGeometry.Wrap(Day));
        if (line)
        {
            dc.DrawLine(Paint.Today, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        dc.DrawEllipse(dot, Paint.DotOutline, new Point(x, y), 4, 4);
    }

    private static class Paint
    {
        public static readonly Color Axis = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);

        public static readonly IPen Tick = MakePen(Solid(90, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Midline = MakePen(Solid(70, 255, 255, 255), 1, dashed: true);
        public static readonly IPen EventLine = MakePen(Solid(34, 255, 211, 107), 1);
        public static readonly IPen Today = MakePen(Solid(150, 255, 255, 255), 1);
        public static readonly IPen DotOutline = MakePen(Solid(255, 10, 14, 24), 1.2);

        public static readonly IBrush MainDot = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IPen Main = MakePen(MainDot, 2);
        public static readonly IPen Opposite = MakePen(Solid(170, 0x8E, 0xC5, 0xFF), 1.3, dashed: true);
        public static readonly IBrush SouthDot = Solid(255, 0x8E, 0xC5, 0xFF);
        public static readonly IPen South = MakePen(SouthDot, 1.8);
        public static readonly IBrush DistanceDot = Solid(255, 0xE6, 0xEA, 0xF2);
        public static readonly IPen Distance = MakePen(DistanceDot, 1.8);
    }
}
