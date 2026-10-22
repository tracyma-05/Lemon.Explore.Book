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

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>
/// The planet's ecliptic longitude through the loop being shown, east upwards: it climbs, levels off at the first
/// station, dips back (orange) and climbs again after the second. A line marks the date.
/// </summary>
public sealed class LongitudeChart : Control
{
    static LongitudeChart()
    {
        AffectsRender<LongitudeChart>(PlanetProperty, DayProperty, WindowStartProperty, WindowEndProperty);
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

    public static readonly StyledProperty<RetroPlanet> PlanetProperty =
        AvaloniaProperty.Register<LongitudeChart, RetroPlanet>(nameof(Planet), RetroPlanet.Mars);

    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<LongitudeChart, double>(nameof(Day), 9000.0);

    public static readonly StyledProperty<double> WindowStartProperty =
        AvaloniaProperty.Register<LongitudeChart, double>(nameof(WindowStart), 8800.0);

    public static readonly StyledProperty<double> WindowEndProperty =
        AvaloniaProperty.Register<LongitudeChart, double>(nameof(WindowEnd), 9200.0);


    public RetroPlanet Planet
    {
        get => GetValue(PlanetProperty);
        set => SetValue(PlanetProperty, value);
    }

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    public double WindowStart
    {
        get => GetValue(WindowStartProperty);
        set => SetValue(WindowStartProperty, value);
    }

    public double WindowEnd
    {
        get => GetValue(WindowEndProperty);
        set => SetValue(WindowEndProperty, value);
    }

    private readonly LabelCache _labels;

    /// <summary>The curve for the planet and window last drawn: days and longitudes (unwrapped round the loop's middle).</summary>
    private (RetroPlanet Planet, double From, double To, RetroPeriod Period, double Center, IReadOnlyList<(double Day, double Lon)> Points)? _curve;

    public LongitudeChart()
    {
        _labels = new LabelCache(this);
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
        if (width < 80 || height < 60 || WindowEnd <= WindowStart) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        if (_curve is not { } curve || curve.Planet != Planet || curve.From != WindowStart || curve.To != WindowEnd)
        {
            var loop = RetroModel.Apparition(Planet, (WindowStart + WindowEnd) / 2);
            var center = RetroModel.Geo(Planet, loop.Middle).Longitude;
            var points = Enumerable.Range(0, 241)
                .Select(i => WindowStart + ((WindowEnd - WindowStart) * i / 240))
                .Select(d => (d, RetroModel.Wrap180(RetroModel.Geo(Planet, d).Longitude - center)))
                .ToList();
            curve = (Planet, WindowStart, WindowEnd, loop, center, points);
            _curve = curve;
        }

        var (min, max) = (curve.Points.Min(p => p.Lon), curve.Points.Max(p => p.Lon));
        var pad = Math.Max(1, (max - min) * 0.08);
        var plot = new Rect(40, 12, width - 52, height - 36);
        double X(double day) => plot.Left + ((day - curve.From) / (curve.To - curve.From) * plot.Width);
        double Y(double lon) => plot.Bottom - ((lon - (min - pad)) / ((max + pad) - (min - pad)) * plot.Height);

        // The retrograde stretch shaded, a few longitudes across, and the start, middle and end dates under it.
        var period = curve.Period;
        dc.DrawRectangle(Solid(36, 0xFF, 0x7A, 0x4A), null, new Rect(new Point(X(period.Start), plot.Top), new Point(X(period.End), plot.Bottom)));
        var step = NiceStep((max - min) / 4);
        for (var lon = Math.Ceiling((min - pad) / step) * step; lon <= max + pad; lon += step)
        {
            dc.DrawLine(Ink.Grid, new Point(plot.Left, Y(lon)), new Point(plot.Right, Y(lon)));
            Label(dc, $"{Number(RetroModel.Wrap360(lon + curve.Center), "0")}°", new Point(plot.Left - 4, Y(lon)), Ink.Dim, right: true);
        }

        dc.DrawLine(Ink.Axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        Label(dc, Short(curve.From), new Point(plot.Left, plot.Bottom + 11), Ink.Dim);
        Label(dc, Short(curve.To), new Point(plot.Right, plot.Bottom + 11), Ink.Dim, right: true);
        Label(dc, Short(period.Middle), new Point(X(period.Middle), plot.Bottom + 11), Ink.Orange, center: true);
        Label(dc, Loc("Retro_Chart_East"), new Point(plot.Left + 4, plot.Top + 4), Ink.Gold);

        // The curve: gold forward, orange back.
        foreach (var retro in new[] { false, true })
        {
            var geometry = new StreamGeometry();
            using (var figures = geometry.Open())
            {
                var open = false;
                (double Day, double Lon)? previous = null;
                foreach (var point in curve.Points)
                {
                    var inside = period.Contains(point.Day);
                    if (inside == retro)
                    {
                        if (!open) figures.BeginFigure(new Point(X((previous ?? point).Day), Y((previous ?? point).Lon)), false);
                        figures.LineTo(new Point(X(point.Day), Y(point.Lon)));
                        open = true;
                    }
                    else if (open)
                    {
                        figures.LineTo(new Point(X(point.Day), Y(point.Lon)));
                        figures.EndFigure(false);
                        open = false;
                    }

                    previous = point;
                }

                if (open) figures.EndFigure(false);
            }
            dc.DrawGeometry(null, MakePen(retro ? Ink.OrangeBrush : Ink.GoldBrush, 2.2), geometry);
        }

        foreach (var day in new[] { period.Start, period.End })
        {
            dc.DrawEllipse(null, MakePen(Ink.CyanBrush, 1.6), new Point(X(day), Y(RetroModel.Wrap180(RetroModel.Geo(Planet, day).Longitude - curve.Center))), 4, 4);
        }

        // Today in the loop.
        if (Day >= curve.From && Day <= curve.To)
        {
            var x = X(Day);
            dc.DrawLine(Ink.Now, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var lon = RetroModel.Wrap180(RetroModel.Geo(Planet, Day).Longitude - curve.Center);
            dc.DrawEllipse(Brushes.White, null, new Point(x, Y(lon)), 3.5, 3.5);
        }
    }

    /// <summary>A round step (1, 2 or 5 times a power of ten) near <paramref name="rough"/>.</summary>
    private static double NiceStep(double rough)
    {
        if (rough <= 0) return 1;
        var power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var unit = rough / power;
        return power * (unit < 1.5 ? 1 : unit < 3.5 ? 2 : unit < 7.5 ? 5 : 10);
    }

    private void Label(PaintContext dc, string value, Point at, Color color, bool right = false, bool center = false)
    {
        var text = _labels.Text(value, 9.5, color);
        var x = right ? at.X - text.Width : center ? at.X - (text.Width / 2) : at.X;
        dc.DrawText(text, new Point(x, at.Y - (text.Height / 2)));
    }

    private static string Short(double day) => LocalizationService.Instance.Format("Retro_ShortDate", RetroModel.DateOf(day));

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static class Ink
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x7A, 0x4A);
        public static readonly IBrush GoldBrush = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush OrangeBrush = Solid(255, 0xFF, 0x7A, 0x4A);
        public static readonly IBrush CyanBrush = Solid(255, 0x6F, 0xD8, 0xFF);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(110, 255, 255, 255), 1);
        public static readonly IPen Now = MakePen(Solid(160, 255, 255, 255), 1, dashed: true);
    }
}
