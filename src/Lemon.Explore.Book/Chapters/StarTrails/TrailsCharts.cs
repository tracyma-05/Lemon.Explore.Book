using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>A small chart of the star-trails side panel: redrawn when a property changes or the language does.</summary>
public abstract class TrailsChart : Control
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

    private protected readonly LabelCache Labels;

    protected TrailsChart()
    {
        Labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        Labels.Clear();
        InvalidateVisual();
    }


    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 80 || ActualHeight < 60) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        Draw(dc, ActualWidth, ActualHeight);
    }

    protected abstract void Draw(PaintContext dc, double width, double height);

    private protected void Label(PaintContext dc, string value, Point at, Color color, bool alignRight = false, double size = 10.5, bool bold = false, bool center = false)
    {
        var text = Labels.Text(value, size, color, bold);
        var x = alignRight ? at.X - text.Width : center ? at.X - (text.Width / 2) : at.X;
        dc.DrawText(text, new Point(x, at.Y - (text.Height / 2)));
    }

    private protected static StreamGeometry Curve(IEnumerable<Point> points, bool closed = false)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, closed);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(closed);
        }
        return geometry;
    }

    private protected static IPen LinePen(Color color, double thickness, bool dashed = false, byte alpha = 255) =>
        MakePen(Solid(alpha, color.R, color.G, color.B), thickness, dashed);

    private protected static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private protected static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private protected static class ChartInk
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(110, 255, 255, 255), 1);
    }
}

/// <summary>
/// How many hours of each day a star is above the horizon, by its declination, at the latitude: a whole day near the
/// visible pole (gold), none near the other (blue), half on the celestial equator; the sphere's stars marked.
/// </summary>
public sealed class VisibleHoursChart : TrailsChart
{
    static VisibleHoursChart()
    {
        AffectsRender<VisibleHoursChart>(LatitudeProperty);
    }

    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<VisibleHoursChart, double>(nameof(Latitude), 28.2);

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(30, 10, width - 40, height - 32);
        double X(double dec) => plot.Left + ((dec + 90) / 180 * plot.Width);
        double Y(double hours) => plot.Bottom - (hours / 24 * plot.Height);
        var limit = 90 - Math.Abs(Latitude);
        var north = Latitude >= 0;

        // The two caps as bands of declination.
        var (always, never) = north ? ((90 - limit, 90.0), (-90.0, -(90 - limit))) : ((-90.0, -(90 - limit)), (90 - limit, 90.0));
        if (limit < 89.5)
        {
            dc.DrawRectangle(Solid(40, ChartInk.Gold.R, ChartInk.Gold.G, ChartInk.Gold.B), null, new Rect(new Point(X(always.Item1), plot.Top), new Point(X(always.Item2), plot.Bottom)));
            dc.DrawRectangle(Solid(40, ChartInk.Blue.R, ChartInk.Blue.G, ChartInk.Blue.B), null, new Rect(new Point(X(never.Item1), plot.Top), new Point(X(never.Item2), plot.Bottom)));
        }

        foreach (var hours in new[] { 0.0, 6, 12, 18, 24 })
        {
            dc.DrawLine(hours is 0 or 24 ? ChartInk.Axis : ChartInk.Grid, new Point(plot.Left, Y(hours)), new Point(plot.Right, Y(hours)));
            Label(dc, Number(hours, "0"), new Point(plot.Left - 4, Y(hours)), ChartInk.Dim, alignRight: true, size: 9.5);
        }

        foreach (var dec in new[] { -90.0, -45, 0, 45, 90 })
        {
            Label(dc, $"{Number(dec, "0")}°", new Point(X(dec), plot.Bottom + 10), ChartInk.Dim, center: true, size: 9.5);
        }

        var curve = Enumerable.Range(0, 181).Select(i => new Point(X(i - 90.0), Y(Math.Min(24, SkyModel.HoursAbove(i - 90.0, Latitude) * SkyModel.SiderealRate))));
        dc.DrawGeometry(null, LinePen(ChartInk.Cyan, 2), Curve(curve));

        foreach (var id in TrailsDiagram.SphereStars)
        {
            var star = StarCatalog.Get(id);
            var p = new Point(X(star.Dec), Y(Math.Min(24, SkyModel.HoursAbove(star.Dec, Latitude) * SkyModel.SiderealRate)));
            var colour = TrailsDiagram.StarColor(star.ColorIndex);
            dc.DrawEllipse(Solid(255, colour.R, colour.G, colour.B), null, p, 3.2, 3.2);
        }

        Label(dc, Loc("Trails_Chart_Hours_Axis"), new Point(plot.Left + 4, plot.Top + 8), ChartInk.Dim, size: 9.5);
    }
}

/// <summary>
/// When some constellations stand highest (cross the meridian) through the year, between 18:00 and 06:00: every line
/// runs two hours earlier a month. The dark of night at the latitude shaded, today marked.
/// </summary>
public sealed class TransitChart : TrailsChart
{
    static TransitChart()
    {
        AffectsRender<TransitChart>(DayProperty, LatitudeProperty);
    }

    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<TransitChart, double>(nameof(Day), 1.0);
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<TransitChart, double>(nameof(Latitude), 28.2);

    /// <summary>The constellations followed, with their colours.</summary>
    internal static readonly (string Id, Color Colour)[] Followed =
    [
        ("Orion", Color.FromRgb(0x8F, 0xB4, 0xE8)),
        ("Leo", Color.FromRgb(0xFF, 0xD3, 0x6B)),
        ("Scorpius", Color.FromRgb(0xFF, 0x8A, 0x6A)),
        ("Pegasus", Color.FromRgb(0x8B, 0xE0, 0x8F)),
    ];

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

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(40, 10, width - 50, height - 32);
        double X(double day) => plot.Left + ((day - 1) / 365 * plot.Width);
        double Y(double hour) => plot.Top + ((hour - 18) / 12 * plot.Height);

        // Night: from sunset to sunrise (no twilight), shaded darker.
        var dark = new List<Point>();
        var light = new List<Point>();
        for (var day = 1; day <= 365; day += 4)
        {
            var (set, rise) = SunsetSunrise(day, Latitude);
            dark.Add(new Point(X(day), Y(Math.Clamp(set, 18, 30))));
            light.Add(new Point(X(day), Y(Math.Clamp(rise, 18, 30))));
        }

        light.Reverse();
        dc.DrawGeometry(Solid(60, 0x30, 0x48, 0x78), null, Curve(dark.Concat(light), closed: true));

        foreach (var hour in new[] { 18.0, 21, 24, 27, 30 })
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(hour)), new Point(plot.Right, Y(hour)));
            Label(dc, $"{Number(hour % 24, "00")}:00", new Point(plot.Left - 4, Y(hour)), ChartInk.Dim, alignRight: true, size: 9.5);
        }

        for (var month = 1; month <= 12; month += 2)
        {
            Label(dc, Loc($"Trails_Month_{month}"), new Point(X(new DateTime(2025, month, 15).DayOfYear), plot.Bottom + 10), ChartInk.Dim, center: true, size: 9.5);
        }

        // Each constellation's crossing time, split where it wraps out of the night window.
        foreach (var (id, colour) in Followed)
        {
            var (ra, _) = StarCatalog.Center(StarCatalog.Figures.First(f => f.Id == id));
            var run = new List<Point>();
            Point? last = null;
            for (var day = 1; day <= 365; day++)
            {
                var t = SkyModel.TransitTime(ra, day);
                if (t < 12) t += 24;
                if (t < 18 || t > 30)
                {
                    Flush();
                    continue;
                }

                var p = new Point(X(day), Y(t));
                if (last is { } previous && Math.Abs(previous.Y - p.Y) > plot.Height / 2) Flush();
                run.Add(p);
                last = p;
            }

            Flush();

            void Flush()
            {
                if (run.Count > 1)
                {
                    dc.DrawGeometry(null, LinePen(colour, 2), Curve(run));
                    Label(dc, Loc(TrailsDiagram.ObjectTitleKey(TrailsDiagram.FigureId(id))!), run[run.Count / 2] + new Vector(4, -8), colour, size: 9.5);
                }

                run = [];
                last = null;
            }
        }

        var x = X(TrailsDiagram.WrapDay(Day));
        dc.DrawLine(LinePen(ChartInk.Text, 1.2, alpha: 200), new Point(x, plot.Top), new Point(x, plot.Bottom));
    }

    /// <summary>Sunset and the next sunrise, local solar hours (sunrise past midnight as 24 + h); polar day or night clamp.</summary>
    internal static (double Set, double Rise) SunsetSunrise(double day, double latitude)
    {
        var product = -Math.Tan(latitude * Math.PI / 180) * Math.Tan(SkyModel.SunDec(day) * Math.PI / 180);
        if (product >= 1) return (12, 36);
        if (product <= -1) return (24, 24);
        var half = Math.Acos(product) * 180 / Math.PI / 15;
        return (12 + half, 36 - half);
    }
}
