using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Oscillations;

/// <summary>A small chart of the oscillations side panel: redrawn when a property changes or the language does.</summary>
public abstract class OscillationChart : Control
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

    protected OscillationChart()
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

    private protected void AxisLabel(PaintContext dc, string value, Point at, bool alignRight = false, bool center = false)
    {
        var text = Labels.Plain(value, 10, ChartInk.Dim);
        var x = alignRight ? at.X - text.Width : center ? at.X - (text.Width / 2) : at.X;
        dc.DrawText(text, new Point(x, at.Y - (text.Height / 2)));
    }

    /// <summary>A value label beside a marked point, kept inside the plot.</summary>
    private protected void PointLabel(PaintContext dc, string value, Point point, Rect plot, Color color)
    {
        var text = Labels.Text(value, 11.5, color, true);
        var at = new Point(point.X + 9, point.Y - text.Height - 3);
        if (at.X + text.Width > plot.Right)
        {
            // No room on the right: below and to the left, clear of the labels at the ends of curves.
            at = new Point(point.X - text.Width - 9, point.Y + 3);
        }

        at = at.WithY(Math.Clamp(at.Y, plot.Top - 4, plot.Bottom - text.Height));
        dc.DrawText(text, at);
    }

    private protected static void Marker(PaintContext dc, Point point, Color color)
    {
        dc.DrawEllipse(Solid(70, color.R, color.G, color.B), null, point, 9, 9);
        dc.DrawEllipse(Solid(255, color.R, color.G, color.B), null, point, 4.5, 4.5);
    }

    private protected static StreamGeometry Curve(IEnumerable<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, false);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(false);
        }
        return geometry;
    }

    private protected static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private protected static class ChartInk
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x7A, 0x6B);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Guide = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush Shade = Solid(18, 0x8B, 0xE0, 0x8F);

        public static Color Place(OscillatorPlace place) => place switch
        {
            OscillatorPlace.Moon => Color.FromRgb(0xC9, 0xD1, 0xDC),
            OscillatorPlace.Mars => Color.FromRgb(0xFF, 0x8A, 0x5C),
            OscillatorPlace.Jupiter => Color.FromRgb(0xE8, 0xB8, 0x6A),
            _ => Color.FromRgb(0x6F, 0xA8, 0xFF),
        };
    }
}

/// <summary>What the pendulum chart plots.</summary>
public enum PendulumChartKind
{
    /// <summary>The period against the length, a curve for each place.</summary>
    ByLength,

    /// <summary>How much longer than the formula a swing takes, against how far it goes.</summary>
    ByAmplitude,
}

/// <summary>
/// The pendulum's period: by length (the period grows as the square root of the length, and is longer where gravity is
/// weaker) or by amplitude (the formula holds for small swings; at 90° a swing takes 18% longer).
/// </summary>
public sealed class PendulumChart : OscillationChart
{
    static PendulumChart()
    {
        AffectsRender<PendulumChart>(KindProperty, PlaceProperty, EarthGravityProperty, LengthProperty, AmplitudeProperty);
    }

    public static readonly StyledProperty<PendulumChartKind> KindProperty =
        AvaloniaProperty.Register<PendulumChart, PendulumChartKind>(nameof(Kind), PendulumChartKind.ByLength);
    public static readonly StyledProperty<OscillatorPlace> PlaceProperty =
        AvaloniaProperty.Register<PendulumChart, OscillatorPlace>(nameof(Place), OscillatorPlace.Earth);
    /// <summary>Gravity on Earth here, m/s² (the shared observer location's).</summary>
    public static readonly StyledProperty<double> EarthGravityProperty =
        AvaloniaProperty.Register<PendulumChart, double>(nameof(EarthGravity), OscillatorPhysics.EarthGravity);
    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<PendulumChart, double>(nameof(Length), 1.0);

    /// <summary>Degrees.</summary>
    public static readonly StyledProperty<double> AmplitudeProperty =
        AvaloniaProperty.Register<PendulumChart, double>(nameof(Amplitude), 20.0);

    public double EarthGravity
    {
        get => GetValue(EarthGravityProperty);
        set => SetValue(EarthGravityProperty, value);
    }

    public PendulumChartKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public OscillatorPlace Place
    {
        get => GetValue(PlaceProperty);
        set => SetValue(PlaceProperty, value);
    }

    public double Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    public double Amplitude
    {
        get => GetValue(AmplitudeProperty);
        set => SetValue(AmplitudeProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(36, 14, width - 50, height - 46);
        if (Kind == PendulumChartKind.ByAmplitude) DrawByAmplitude(dc, plot);
        else DrawByLength(dc, plot);
    }

    private void DrawByLength(PaintContext dc, Rect plot)
    {
        const double maxLength = OscillatorDiagram.MaxLength;
        const double maxPeriod = 8;
        Point P(double length, double period) =>
            new(plot.Left + (length / maxLength * plot.Width), plot.Bottom - (Math.Min(period, maxPeriod) / maxPeriod * plot.Height));

        for (var period = 0; period <= maxPeriod; period += 2)
        {
            dc.DrawLine(ChartInk.Grid, P(0, period), P(maxLength, period));
            AxisLabel(dc, $"{period} s", P(0, period) - new Vector(5, 0), alignRight: true);
        }

        for (var length = 0.0; length <= maxLength + 0.01; length += 0.5)
        {
            dc.DrawLine(ChartInk.Grid, P(length, 0), P(length, maxPeriod));
            AxisLabel(dc, $"{Number(length, "0.#")} m", P(length, 0) + new Vector(0, 10), center: true);
        }

        foreach (var place in Enum.GetValues<OscillatorPlace>().OrderBy(p => p == Place))
        {
            var gravity = OscillatorPhysics.Gravity(place, EarthGravity);
            var color = ChartInk.Place(place);
            var current = place == Place;
            var pen = MakePen(Solid((byte)(current ? 255 : 120), color.R, color.G, color.B), current ? 2.4 : 1.3);
            var points = Enumerable.Range(0, 81).Select(i => i / 80.0 * maxLength)
                .Select(l => P(l, OscillatorPhysics.SmallAnglePeriod(l, gravity)))
                .ToList();
            dc.DrawGeometry(null, pen, Curve(points));

            var name = Labels.Get($"Osc_Place_{place}", 10.5, Lerp(color, ChartInk.Dim, current ? 0 : 0.35), current);
            var end = points[^1];
            dc.DrawText(name, new Point(end.X - name.Width - 2, end.Y - name.Height - 1));
        }

        var length2 = Math.Clamp(Length, 0, maxLength);
        var periodNow = OscillatorPhysics.PendulumPeriod(length2, OscillatorPhysics.Gravity(Place, EarthGravity), Amplitude * Math.PI / 180);
        var point = P(length2, periodNow);
        Marker(dc, point, ChartInk.Gold);
        PointLabel(dc, $"{Number(periodNow, "0.00")} s", point, plot, ChartInk.Gold);
    }

    private void DrawByAmplitude(PaintContext dc, Rect plot)
    {
        const double maxStretch = 1.2;
        Point P(double degrees, double stretch) =>
            new(plot.Left + (degrees / 90 * plot.Width), plot.Bottom - ((stretch - 1) / (maxStretch - 1) * plot.Height));

        // Swings under about 15°: the formula is right to half a percent.
        dc.DrawRectangle(ChartInk.Shade, null, new Rect(P(0, maxStretch), P(15, 1)));
        var small = Labels.Get("Osc_Chart_SmallSwings", 10, Color.FromRgb(0x8B, 0xE0, 0x8F));
        dc.DrawText(small, new Point(plot.Left + 4, plot.Top + 2));

        for (var stretch = 1.0; stretch <= maxStretch + 0.001; stretch += 0.05)
        {
            dc.DrawLine(ChartInk.Grid, P(0, stretch), P(90, stretch));
            AxisLabel(dc, $"+{Number((stretch - 1) * 100, "0")}%", P(0, stretch) - new Vector(5, 0), alignRight: true);
        }

        for (var degrees = 0; degrees <= 90; degrees += 15)
        {
            dc.DrawLine(ChartInk.Grid, P(degrees, 1), P(degrees, maxStretch));
            AxisLabel(dc, $"{degrees}°", P(degrees, 1) + new Vector(0, 10), center: true);
        }

        var points = Enumerable.Range(0, 91).Select(d => P(d, OscillatorPhysics.PeriodStretch(d * Math.PI / 180))).ToList();
        dc.DrawGeometry(null, MakePen(Solid(255, 0xFF, 0x9F, 0x5A), 2.2), Curve(points));

        var amplitude = Math.Clamp(Amplitude, 0, 90);
        var stretchNow = OscillatorPhysics.PeriodStretch(amplitude * Math.PI / 180);
        var point = P(amplitude, stretchNow);
        Marker(dc, point, ChartInk.Gold);
        PointLabel(dc, $"+{Number((stretchNow - 1) * 100, stretchNow < 1.1 ? "0.0" : "0")}%", point, plot, ChartInk.Gold);
    }
}

/// <summary>
/// Where the energy of the swing is now: moving (kinetic), stored (in the height of the bob, or in the spring) and taken
/// by the air, as one bar the length of the energy it was let go with, and a row for each.
/// </summary>
public sealed class EnergyChart : OscillationChart
{
    static EnergyChart()
    {
        AffectsRender<EnergyChart>(EnergyProperty, IsSpringProperty);
    }

    public static readonly StyledProperty<EnergySplit> EnergyProperty =
        AvaloniaProperty.Register<EnergyChart, EnergySplit>(nameof(Energy), EnergySplit.None);
    public static readonly StyledProperty<bool> IsSpringProperty =
        AvaloniaProperty.Register<EnergyChart, bool>(nameof(IsSpring), false);

    public EnergySplit Energy
    {
        get => GetValue(EnergyProperty);
        set => SetValue(EnergyProperty, value);
    }

    public bool IsSpring
    {
        get => GetValue(IsSpringProperty);
        set => SetValue(IsSpringProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var energy = Energy;
        var bar = new Rect(14, 12, width - 28, 22);
        dc.DrawRoundedRectangle(Solid(20, 255, 255, 255), null, bar, 5, 5);
        dc.PushClip(new RectangleGeometry(bar, 5, 5));
        var x = bar.Left;
        foreach (var (share, color) in Parts(energy))
        {
            var w = Math.Clamp(share, 0, 1) * bar.Width;
            dc.DrawRectangle(Solid(230, color.R, color.G, color.B), null, new Rect(x, bar.Top, w, bar.Height));
            x += w;
        }

        dc.Pop();

        var y = bar.Bottom + 12;
        string[] keys = ["Osc_Energy_Kinetic", IsSpring ? "Osc_Energy_Spring" : "Osc_Energy_Height", "Osc_Energy_Lost"];
        var parts = Parts(energy).ToList();
        for (var i = 0; i < keys.Length; i++)
        {
            var (share, color) = parts[i];
            dc.DrawRoundedRectangle(Solid(255, color.R, color.G, color.B), null, new Rect(16, y + 4, 10, 10), 2, 2);
            var name = Labels.Get(keys[i], 12, ChartInk.Text);
            dc.DrawText(name, new Point(34, y));
            var value = Labels.Text($"{Number(Math.Clamp(share, 0, 1) * 100, "0")}%", 12, ChartInk.Text, true);
            dc.DrawText(value, new Point(width - 16 - value.Width, y));
            y += 22;
        }
    }

    private static IEnumerable<(double Share, Color Color)> Parts(EnergySplit energy)
    {
        yield return (energy.Kinetic, ChartInk.Cyan);
        yield return (energy.Stored, ChartInk.Gold);
        yield return (energy.Lost, ChartInk.Red);
    }
}

/// <summary>
/// How far the mass swings, against how often the top is shaken (log scale): hardly at all far from its own frequency,
/// many times the shake near it, the peak lower and wider with more damping.
/// </summary>
public sealed class ResonanceChart : OscillationChart
{
    static ResonanceChart()
    {
        AffectsRender<ResonanceChart>(NaturalFrequencyProperty, DriveFrequencyProperty, DampingProperty);
    }

    /// <summary>The spring's own frequency, hertz.</summary>
    public static readonly StyledProperty<double> NaturalFrequencyProperty =
        AvaloniaProperty.Register<ResonanceChart, double>(nameof(NaturalFrequency), 1.0);

    public static readonly StyledProperty<double> DriveFrequencyProperty =
        AvaloniaProperty.Register<ResonanceChart, double>(nameof(DriveFrequency), 1.0);

    /// <summary>Damping rate, per second.</summary>
    public static readonly StyledProperty<double> DampingProperty =
        AvaloniaProperty.Register<ResonanceChart, double>(nameof(Damping), 0.2);

    public double NaturalFrequency
    {
        get => GetValue(NaturalFrequencyProperty);
        set => SetValue(NaturalFrequencyProperty, value);
    }

    public double DriveFrequency
    {
        get => GetValue(DriveFrequencyProperty);
        set => SetValue(DriveFrequencyProperty, value);
    }

    public double Damping
    {
        get => GetValue(DampingProperty);
        set => SetValue(DampingProperty, value);
    }

    public const double MaxFrequency = 6;

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(36, 14, width - 50, height - 46);
        const double low = 0.01;
        const double high = 30;
        Point P(double hertz, double ratio) => new(plot.Left + (hertz / MaxFrequency * plot.Width),
            plot.Bottom - (Math.Log(Math.Clamp(ratio, low, high) / low) / Math.Log(high / low) * plot.Height));

        foreach (var ratio in new[] { 0.01, 0.1, 1, 10 })
        {
            dc.DrawLine(ChartInk.Grid, P(0, ratio), P(MaxFrequency, ratio));
            AxisLabel(dc, $"×{Number(ratio, "0.##")}", P(0, ratio) - new Vector(5, 0), alignRight: true);
        }

        for (var hertz = 0; hertz <= MaxFrequency; hertz++)
        {
            dc.DrawLine(ChartInk.Grid, P(hertz, low), P(hertz, high));
            AxisLabel(dc, $"{hertz} Hz", P(hertz, low) + new Vector(0, 10), center: true);
        }

        var omega = 2 * Math.PI * NaturalFrequency;
        var points = Enumerable.Range(1, 600).Select(i => i / 100.0)
            .Select(f => P(f, OscillatorPhysics.ShakenResponse(omega, Damping, 2 * Math.PI * f)))
            .ToList();
        dc.DrawGeometry(null, MakePen(Solid(255, 0xFF, 0x9F, 0x5A), 2.2), Curve(points));

        var natural = P(NaturalFrequency, high);
        dc.DrawLine(ChartInk.Guide, natural, P(NaturalFrequency, low));
        var own = Labels.Get("Osc_Chart_OwnFrequency", 10, ChartInk.Dim);
        dc.DrawText(own, new Point(natural.X + own.Width + 8 > plot.Right ? natural.X - own.Width - 4 : natural.X + 4,
            plot.Bottom - own.Height - 2));

        var response = OscillatorPhysics.ShakenResponse(omega, Damping, 2 * Math.PI * DriveFrequency);
        var point = P(DriveFrequency, response);
        Marker(dc, point, ChartInk.Gold);
        PointLabel(dc, $"×{Number(response, response < 10 ? "0.0" : "0")}", point, plot, ChartInk.Gold);
    }
}

/// <summary>
/// The fifteen pendulums of the wave to scale, hanging from the bar, each with how many swings it makes in one cycle,
/// and the periods of the longest and the shortest where they are.
/// </summary>
public sealed class WaveChart : OscillationChart
{
    static WaveChart()
    {
        AffectsRender<WaveChart>(PlaceProperty, EarthGravityProperty);
    }

    public static readonly StyledProperty<OscillatorPlace> PlaceProperty =
        AvaloniaProperty.Register<WaveChart, OscillatorPlace>(nameof(Place), OscillatorPlace.Earth);
    /// <summary>Gravity on Earth here, m/s² (the shared observer location's).</summary>
    public static readonly StyledProperty<double> EarthGravityProperty =
        AvaloniaProperty.Register<WaveChart, double>(nameof(EarthGravity), OscillatorPhysics.EarthGravity);

    public double EarthGravity
    {
        get => GetValue(EarthGravityProperty);
        set => SetValue(EarthGravityProperty, value);
    }

    public OscillatorPlace Place
    {
        get => GetValue(PlaceProperty);
        set => SetValue(PlaceProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var count = OscillatorPhysics.WaveCount;
        var bar = 20.0;
        var left = 26.0;
        var step = (width - 52) / (count - 1);
        var scale = (height - bar - 46) / OscillatorPhysics.WaveLength(0);
        dc.DrawLine(MakePen(Solid(255, 0x8A, 0x96, 0xA8), 4), new Point(left - 12, bar), new Point(width - 14, bar));

        var gravity = OscillatorPhysics.Gravity(Place, EarthGravity);
        for (var i = 0; i < count; i++)
        {
            var x = left + (i * step);
            var bob = new Point(x, bar + (OscillatorPhysics.WaveLength(i) * scale));
            dc.DrawLine(MakePen(Solid(170, 0xC9, 0xD1, 0xDC), 1), new Point(x, bar), bob);
            var color = OscillatorDiagram.WaveColour(i);
            dc.DrawEllipse(Solid(255, color.R, color.G, color.B), null, bob, 5, 5);
            if (i % 2 == 0) AxisLabel(dc, $"{OscillatorPhysics.WaveFirstSwings + i}", new Point(x, bob.Y + 14), center: true);
        }

        var first = Labels.Text($"{Number(OscillatorPhysics.WaveLength(0) * 100, "0")} cm · {Number(OscillatorPhysics.WavePeriod(0, gravity), "0.00")} s", 10.5, ChartInk.Text);
        var last = Labels.Text($"{Number(OscillatorPhysics.WaveLength(count - 1) * 100, "0")} cm · {Number(OscillatorPhysics.WavePeriod(count - 1, gravity), "0.00")} s", 10.5, ChartInk.Text);
        dc.DrawText(first, new Point(left - 10, 2));
        dc.DrawText(last, new Point(width - 14 - last.Width, 2));
        var swings = Labels.Get("Osc_Chart_SwingsPerCycle", 10, ChartInk.Dim);
        dc.DrawText(swings, new Point((width - swings.Width) / 2, height - swings.Height - 2));
    }
}
