using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>
/// The Moon's latitude above or below the ecliptic over the last two months and the next few weeks, with every new
/// and full moon marked. A new moon inside the gold band is a solar eclipse, a full moon inside the copper band a
/// lunar one; the wave drifts against the moons, so they line up only in the eclipse seasons.
/// </summary>
public sealed class MoonLatitudeChart : Control
{
    static MoonLatitudeChart()
    {
        AffectsRender<MoonLatitudeChart>(DayProperty);
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    private const double DaysBefore = 70;
    private const double DaysAfter = 20;
    private const double MaxLatitude = 6;

    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<MoonLatitudeChart, double>(nameof(Day), 0.0);

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    private readonly LabelCache _labels;

    public MoonLatitudeChart()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 40 || ActualHeight < 40) return;

        dc.DrawRectangle(Paint.Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var plot = new Rect(34, 10, ActualWidth - 44, ActualHeight - 20);
        var day = Day;
        var start = day - DaysBefore;
        var end = day + DaysAfter;

        double X(double d) => plot.Left + ((d - start) / (end - start) * plot.Width);
        double Y(double latitude) => plot.Top + ((MaxLatitude - latitude) / (2 * MaxLatitude) * plot.Height);

        // Eclipse zones around the ecliptic.
        dc.DrawRectangle(Paint.SolarZone, null, new Rect(plot.Left, Y(EclipseSeasons.SolarLimit), plot.Width, Y(-EclipseSeasons.SolarLimit) - Y(EclipseSeasons.SolarLimit)));
        dc.DrawRectangle(Paint.LunarZone, null, new Rect(plot.Left, Y(EclipseSeasons.LunarLimit), plot.Width, Y(-EclipseSeasons.LunarLimit) - Y(EclipseSeasons.LunarLimit)));
        dc.DrawLine(Paint.Ecliptic, new Point(plot.Left, Y(0)), new Point(plot.Right, Y(0)));

        foreach (var latitude in new[] { 5.0, 0.0, -5.0 })
        {
            var text = _labels.Plain(latitude.ToString("+0°;−0°;0°", CultureInfo.InvariantCulture), 10, Paint.AxisText);
            dc.DrawText(text, new Point(plot.Left - text.Width - 5, Y(latitude) - (text.Height / 2)));
            if (latitude != 0) dc.DrawLine(Paint.Grid, new Point(plot.Left, Y(latitude)), new Point(plot.Right, Y(latitude)));
        }

        // The Moon's latitude: the past solid, the weeks ahead faint.
        DrawCurve(dc, start, day, X, Y, Paint.Curve);
        DrawCurve(dc, day, end, X, Y, Paint.FutureCurve);

        foreach (var e in EclipseSeasons.Events(start, end))
        {
            var at = new Point(X(e.Day), Y(e.Latitude));
            dc.PushOpacity(e.Day > day ? 0.45 : 1);
            if (e.Kind != SeasonEventKind.None)
            {
                dc.DrawEllipse(Paint.Glow(e.Kind), null, at, 9, 9);
            }

            dc.DrawEllipse(e.IsNewMoon ? Paint.NewMoon : Paint.FullMoon, e.IsNewMoon ? Paint.NewMoonEdge : null, at, 4, 4);
            dc.Pop();
        }

        dc.DrawLine(Paint.Now, new Point(X(day), plot.Top), new Point(X(day), plot.Bottom));
        dc.DrawEllipse(Paint.NowDot, null, new Point(X(day), Y(EclipseSeasons.MoonLatitude(day))), 3, 3);
    }

    private static void DrawCurve(PaintContext dc, double from, double to, Func<double, double> x, Func<double, double> y, IPen pen)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var steps = Math.Max(2, (int)((to - from) * 2));
            for (var i = 0; i <= steps; i++)
            {
                var d = from + ((to - from) * i / steps);
                var point = new Point(x(d), y(EclipseSeasons.MoonLatitude(d)));
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private static class Paint
    {
        public static readonly Color AxisText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
        public static readonly IBrush Background = Solid(255, 0x06, 0x0A, 0x15);
        public static readonly IBrush SolarZone = Solid(46, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush LunarZone = Solid(60, 0xE0, 0x70, 0x3C);
        public static readonly IPen Ecliptic = MakePen(Solid(150, 255, 214, 130), 1, dashed: true);
        public static readonly IPen Grid = MakePen(Solid(30, 255, 255, 255), 1);
        public static readonly IPen Curve = MakePen(Solid(255, 0x8E, 0xC5, 0xFF), 1.6);
        public static readonly IPen FutureCurve = MakePen(Solid(110, 0x8E, 0xC5, 0xFF), 1.2, dashed: true);
        public static readonly IBrush NewMoon = Solid(255, 0x12, 0x16, 0x22);
        public static readonly IPen NewMoonEdge = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 1.5);
        public static readonly IBrush FullMoon = Solid(255, 0xF2, 0xF0, 0xE8);
        public static readonly IPen Now = MakePen(Solid(150, 255, 255, 255), 1);
        public static readonly IBrush NowDot = Solid(255, 0x8E, 0xC5, 0xFF);

        private static readonly IBrush SolarGlow = Glow(Color.FromRgb(0xFF, 0xD3, 0x6B));
        private static readonly IBrush LunarGlow = Glow(Color.FromRgb(0xE0, 0x70, 0x3C));
        private static readonly IBrush PenumbralGlow = Glow(Color.FromRgb(0xB9, 0xC2, 0xD0));

        public static IBrush Glow(SeasonEventKind kind) => kind switch
        {
            SeasonEventKind.SolarEclipse => SolarGlow,
            SeasonEventKind.LunarEclipse => LunarGlow,
            _ => PenumbralGlow,
        };

        private static IBrush Glow(Color color) => SpaceDrawing.RadialGradient(
            (Color.FromArgb(220, color.R, color.G, color.B), 0.35),
            (Color.FromArgb(0, color.R, color.G, color.B), 1));
    }
}
