using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Waves;

/// <summary>A small chart of the waves side panel: redrawn when a property changes or the language does.</summary>
public abstract class WaveChart : Control
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

    protected WaveChart()
    {
        Labels = new LabelCache(this);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.MediumQuality);
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

    private protected void CurveLabel(PaintContext dc, string value, Point at, Color color, bool alignRight = false, double size = 11)
    {
        var text = Labels.Text(value, size, color, true);
        dc.DrawText(text, new Point(alignRight ? at.X - text.Width : at.X, at.Y - (text.Height / 2)));
    }

    private protected static void Marker(PaintContext dc, Point point, Color color)
    {
        dc.DrawEllipse(Solid(70, color.R, color.G, color.B), null, point, 8, 8);
        dc.DrawEllipse(Solid(255, color.R, color.G, color.B), null, point, 4, 4);
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

    private protected static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private protected static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private protected static class ChartInk
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Pink = Color.FromRgb(0xFF, 0x8F, 0xB8);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);
        public static readonly IPen Guide = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
        public static readonly IPen CyanPen = MakePen(Solid(220, 0x6F, 0xD8, 0xFF), 1.5);
        public static readonly IPen PinkPen = MakePen(Solid(220, 0xFF, 0x8F, 0xB8), 1.5);
        public static readonly IPen GoldPen = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 2.4);
        public static readonly IPen GoldThin = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 1.8);
        public static readonly IPen OrangeGuide = MakePen(Solid(200, 0xFF, 0x9F, 0x5A), 1.2, dashed: true);
        public static readonly IBrush SupersonicShade = Solid(22, 0xFF, 0x9F, 0x5A);
        public static readonly IBrush SilenceShade = Solid(16, 255, 255, 255);
    }
}

/// <summary>
/// The water at the probe over the last three waves: how high A's wave lifts it, how high B's does, and the two together,
/// which is all the water does there. In step they add up; opposite they cancel.
/// </summary>
public sealed class RippleProbeChart : WaveChart
{
    static RippleProbeChart()
    {
        AffectsRender<RippleProbeChart>(TimeProperty, WavelengthProperty, SeparationProperty, OppositeProperty, SecondSourceProperty, ProbeProperty);
    }

    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<RippleProbeChart, double>(nameof(Time), 0.0);
    public static readonly StyledProperty<double> WavelengthProperty =
        AvaloniaProperty.Register<RippleProbeChart, double>(nameof(Wavelength), 2.5);
    public static readonly StyledProperty<double> SeparationProperty =
        AvaloniaProperty.Register<RippleProbeChart, double>(nameof(Separation), 6.0);
    public static readonly StyledProperty<bool> OppositeProperty =
        AvaloniaProperty.Register<RippleProbeChart, bool>(nameof(Opposite), false);
    public static readonly StyledProperty<bool> SecondSourceProperty =
        AvaloniaProperty.Register<RippleProbeChart, bool>(nameof(SecondSource), true);
    public static readonly StyledProperty<Point> ProbeProperty =
        AvaloniaProperty.Register<RippleProbeChart, Point>(nameof(Probe), new Point(5, 9));

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public double Wavelength
    {
        get => GetValue(WavelengthProperty);
        set => SetValue(WavelengthProperty, value);
    }

    public double Separation
    {
        get => GetValue(SeparationProperty);
        set => SetValue(SeparationProperty, value);
    }

    public bool Opposite
    {
        get => GetValue(OppositeProperty);
        set => SetValue(OppositeProperty, value);
    }

    public bool SecondSource
    {
        get => GetValue(SecondSourceProperty);
        set => SetValue(SecondSourceProperty, value);
    }

    public Point Probe
    {
        get => GetValue(ProbeProperty);
        set => SetValue(ProbeProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(12, 12, width - 62, height - 24);
        var (a, b, together, _) = WavePhysics.Combined(Probe, Separation, Wavelength, Opposite, SecondSource);
        var reach = Math.Max(1e-6, (a + b) * 1.08);
        var period = 1 / WavePhysics.RippleFrequency(Wavelength);
        var end = Time;
        var start = end - (3 * period);

        double X(double t) => plot.Left + ((t - start) / (end - start) * plot.Width);
        double Y(double h) => plot.Top + (plot.Height / 2) - (h / reach * plot.Height / 2);

        dc.DrawLine(ChartInk.Axis, new Point(plot.Left, Y(0)), new Point(plot.Right, Y(0)));
        dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(together)), new Point(plot.Right, Y(together)));
        dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(-together)), new Point(plot.Right, Y(-together)));
        dc.DrawLine(ChartInk.Guide, new Point(plot.Right, plot.Top), new Point(plot.Right, plot.Bottom));

        const int samples = 180;
        var heights = Enumerable.Range(0, samples + 1)
            .Select(i => start + ((end - start) * i / samples))
            .Select(t => (T: t, H: WavePhysics.RippleHeights(Probe, Separation, Wavelength, Opposite, SecondSource, t)))
            .ToList();

        dc.DrawGeometry(null, ChartInk.CyanPen, Curve(heights.Select(p => new Point(X(p.T), Y(p.H.A)))));
        if (SecondSource) dc.DrawGeometry(null, ChartInk.PinkPen, Curve(heights.Select(p => new Point(X(p.T), Y(p.H.B)))));
        if (SecondSource) dc.DrawGeometry(null, ChartInk.GoldPen, Curve(heights.Select(p => new Point(X(p.T), Y(p.H.Sum)))));

        var now = heights[^1].H;
        var tags = new List<(double Y, string Text, Color Color)> { (Y(now.A), "A", ChartInk.Cyan) };
        if (SecondSource)
        {
            tags.Add((Y(now.B), "B", ChartInk.Pink));
            tags.Add((Y(now.Sum), "A+B", ChartInk.Gold));
            Marker(dc, new Point(plot.Right, Y(now.Sum)), ChartInk.Gold);
        }

        // Keep the tags at the right end from landing on each other.
        tags.Sort((p, q) => p.Y.CompareTo(q.Y));
        for (var i = 1; i < tags.Count; i++)
        {
            if (tags[i].Y - tags[i - 1].Y < 14) tags[i] = tags[i] with { Y = tags[i - 1].Y + 14 };
        }

        foreach (var tag in tags) CurveLabel(dc, tag.Text, new Point(plot.Right + 12, Math.Clamp(tag.Y, plot.Top, plot.Bottom)), tag.Color);
        AxisLabel(dc, Loc("Waves_Chart_Now"), new Point(plot.Right, plot.Bottom + 4), alignRight: true);
    }
}

/// <summary>
/// The real stripes on the screen: brightness across it (mm from the middle) with the colour it has, two slits' cos²
/// stripes under each slit's own broad glow; the spacing λL/d marked. White light adds up its colours, so only the middle
/// stripe is white and the others are rainbow-edged.
/// </summary>
public sealed class ScreenChart : WaveChart
{
    static ScreenChart()
    {
        AffectsRender<ScreenChart>(LightWavelengthProperty, SlitSpacingProperty, ScreenDistanceProperty, OneSlitProperty, WhiteLightProperty);
    }

    /// <summary>The screen shown, mm either side of the middle.</summary>
    public const double HalfWidth = 25;

    public static readonly StyledProperty<double> LightWavelengthProperty =
        AvaloniaProperty.Register<ScreenChart, double>(nameof(LightWavelength), 532.0);
    public static readonly StyledProperty<double> SlitSpacingProperty =
        AvaloniaProperty.Register<ScreenChart, double>(nameof(SlitSpacing), 0.25);
    public static readonly StyledProperty<double> ScreenDistanceProperty =
        AvaloniaProperty.Register<ScreenChart, double>(nameof(ScreenDistance), 2.0);
    public static readonly StyledProperty<bool> OneSlitProperty =
        AvaloniaProperty.Register<ScreenChart, bool>(nameof(OneSlit), false);
    public static readonly StyledProperty<bool> WhiteLightProperty =
        AvaloniaProperty.Register<ScreenChart, bool>(nameof(WhiteLight), false);

    public double LightWavelength
    {
        get => GetValue(LightWavelengthProperty);
        set => SetValue(LightWavelengthProperty, value);
    }

    public double SlitSpacing
    {
        get => GetValue(SlitSpacingProperty);
        set => SetValue(SlitSpacingProperty, value);
    }

    public double ScreenDistance
    {
        get => GetValue(ScreenDistanceProperty);
        set => SetValue(ScreenDistanceProperty, value);
    }

    public bool OneSlit
    {
        get => GetValue(OneSlitProperty);
        set => SetValue(OneSlitProperty, value);
    }

    public bool WhiteLight
    {
        get => GetValue(WhiteLightProperty);
        set => SetValue(WhiteLightProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var strip = new Rect(12, 10, width - 24, 26);
        var plot = new Rect(12, strip.Bottom + 22, width - 24, height - strip.Bottom - 44);
        double X(double mm) => plot.Left + ((mm + HalfWidth) / (2 * HalfWidth) * plot.Width);
        double Y(double brightness) => plot.Bottom - (Math.Clamp(brightness, 0, 1.05) * plot.Height);

        var columns = Math.Max(2, (int)Math.Round(strip.Width));
        var brightness = new double[columns];
        var pixels = new uint[columns];
        var colour = SkyOptics.SpectralColour(LightWavelength);
        var linear = new LinearRgb(Math.Pow(colour.R / 255.0, 2.2), Math.Pow(colour.G / 255.0, 2.2), Math.Pow(colour.B / 255.0, 2.2));
        for (var i = 0; i < columns; i++)
        {
            var y = -HalfWidth + ((i + 0.5) / columns * 2 * HalfWidth);
            LinearRgb light;
            if (WhiteLight)
            {
                light = default;
                for (var band = 0; band < SkyOptics.Bands; band++)
                {
                    light += SkyOptics.BandColours[band] *
                        WavePhysics.ScreenIntensity(y, SkyOptics.Wavelengths[band], ScreenDistance, SlitSpacing, !OneSlit);
                }

                brightness[i] = light.Luminance;
            }
            else
            {
                brightness[i] = WavePhysics.ScreenIntensity(y, LightWavelength, ScreenDistance, SlitSpacing, !OneSlit);
                light = linear * brightness[i];
            }

            pixels[i] = WaveField.Pack(Color.FromRgb(SkyOptics.Encode(light.R), SkyOptics.Encode(light.G), SkyOptics.Encode(light.B)));
        }

        var bitmap = new WriteableBitmap(new PixelSize(columns, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        SpaceDrawing.WritePixels(bitmap, pixels, columns, 1);
        dc.DrawRectangle(Brushes.Black, null, strip);
        dc.DrawImage(bitmap, strip);
        dc.DrawRectangle(null, ChartInk.Axis, strip);

        // Brightness across the screen.
        foreach (var mm in new[] { -20.0, -10, 0, 10, 20 })
        {
            dc.DrawLine(ChartInk.Grid, new Point(X(mm), plot.Top), new Point(X(mm), plot.Bottom));
            AxisLabel(dc, Number(mm, "0"), new Point(X(mm), plot.Bottom + 9), center: true);
        }

        AxisLabel(dc, Loc("Waves_Chart_Millimetres"), new Point(plot.Right, plot.Bottom + 9), alignRight: true);
        dc.DrawLine(ChartInk.Axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        var curveColour = WhiteLight ? ChartInk.Text : colour;
        var pen = MakePen(Solid(255, curveColour.R, curveColour.G, curveColour.B), 1.6);
        dc.DrawGeometry(null, pen, Curve(brightness.Select((b, i) => new Point(strip.Left + i + 0.5, Y(b)))));

        // The spacing between the middle stripe and the next.
        var spacing = WavePhysics.FringeSpacing(LightWavelength, ScreenDistance, SlitSpacing);
        if (OneSlit || WhiteLight || spacing > HalfWidth * 0.9) return;
        var y0 = strip.Bottom + 8;
        dc.DrawLine(ChartInk.Guide, new Point(X(0), y0), new Point(X(spacing), y0));
        dc.DrawLine(ChartInk.Axis, new Point(X(0), y0 - 4), new Point(X(0), y0 + 4));
        dc.DrawLine(ChartInk.Axis, new Point(X(spacing), y0 - 4), new Point(X(spacing), y0 + 4));
        CurveLabel(dc, LocFormat("Waves_Chart_Spacing", Number(spacing, spacing < 10 ? "0.0" : "0")), new Point(X(spacing) + 6, y0), ChartInk.Text,
            size: 10.5);
    }
}

/// <summary>
/// The pitch the listener hears through the pass: high while the source comes, its own pitch as it goes past, low as it
/// goes away. Faster than sound: nothing, then the bang, then two sounds at once.
/// </summary>
public sealed class PitchChart : WaveChart
{
    static PitchChart()
    {
        AffectsRender<PitchChart>(MachProperty, ListenerProperty, TimeProperty);
    }

    public static readonly StyledProperty<double> MachProperty =
        AvaloniaProperty.Register<PitchChart, double>(nameof(Mach), 0.5);
    public static readonly StyledProperty<Point> ListenerProperty =
        AvaloniaProperty.Register<PitchChart, Point>(nameof(Listener), new Point(60, 45));
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<PitchChart, double>(nameof(Time), 0.0);

    private (double Mach, Point Listener, List<(double T, IReadOnlyList<HeardSound> Sounds)> Samples)? _cache;

    public double Mach
    {
        get => GetValue(MachProperty);
        set => SetValue(MachProperty, value);
    }

    public Point Listener
    {
        get => GetValue(ListenerProperty);
        set => SetValue(ListenerProperty, value);
    }

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(40, 24, width - 52, height - 46);
        var mach = Mach;
        var loop = WavePhysics.LoopDuration(mach, Listener);
        if (_cache is not { } cache || cache.Mach != mach || cache.Listener != Listener)
        {
            var samples = Enumerable.Range(0, 241)
                .Select(i => i * loop / 240)
                .Select(t => (t, WavePhysics.Heard(Listener, t, mach)))
                .ToList();
            cache = (mach, Listener, samples);
            _cache = cache;
        }

        var highest = cache.Samples.SelectMany(s => s.Sounds).Select(s => s.Frequency).DefaultIfEmpty(880).Max();
        var top = Math.Clamp(Math.Ceiling(highest * 1.1 / 220) * 220, 660, 1760);
        double X(double t) => plot.Left + (t / loop * plot.Width);
        double Y(double hz) => plot.Bottom - (Math.Min(hz, top * 1.05) / top * plot.Height);

        for (var hz = 0.0; hz <= top + 1; hz += top > 1000 ? 440 : 220)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(hz)), new Point(plot.Right, Y(hz)));
            AxisLabel(dc, Number(hz, "0"), new Point(plot.Left - 5, Y(hz)), alignRight: true);
        }

        dc.DrawLine(ChartInk.Guide, new Point(plot.Left, Y(WavePhysics.SourcePitch)), new Point(plot.Right, Y(WavePhysics.SourcePitch)));
        AxisLabel(dc, Loc("Waves_Chart_Hz"), new Point(plot.Left - 5, 10), alignRight: true);
        AxisLabel(dc, LocFormat("Waves_Chart_Seconds", Number(loop, "0.0")), new Point(plot.Right, plot.Bottom + 11), alignRight: true);
        AxisLabel(dc, "0", new Point(plot.Left, plot.Bottom + 11), center: true);

        // Faster than sound: silence until the bang.
        var boom = WavePhysics.ConeArrival(Listener, mach);
        if (!double.IsNaN(boom) && boom > 0)
        {
            var edge = Math.Min(X(boom), plot.Right);
            dc.DrawRectangle(ChartInk.SilenceShade, null, new Rect(plot.Left, plot.Top, edge - plot.Left, plot.Height));
            CurveLabel(dc, Loc("Waves_Chart_Silence"), new Point(plot.Left + 6, plot.Top + 10), ChartInk.Dim, size: 10.5);
            if (boom < loop)
            {
                dc.DrawLine(ChartInk.OrangeGuide, new Point(edge, plot.Top), new Point(edge, plot.Bottom));
                CurveLabel(dc, Loc("Waves_Chart_Bang"), new Point(edge + 4, plot.Top + 24), ChartInk.Orange, size: 10.5);
            }
        }

        // Each sound as a line; faster than sound, the one made later is the second.
        for (var branch = 0; branch < 2; branch++)
        {
            var pen = branch == 0 ? ChartInk.GoldThin : ChartInk.CyanPen;
            var run = new List<Point>();
            foreach (var (t, sounds) in cache.Samples)
            {
                if (sounds.Count > branch) run.Add(new Point(X(t), Y(sounds[branch].Frequency)));
                else Flush();
            }

            Flush();

            void Flush()
            {
                if (run.Count > 1) dc.DrawGeometry(null, pen, Curve(run));
                run.Clear();
            }
        }

        // Now.
        var now = Math.Clamp(Time, 0, loop);
        dc.DrawLine(ChartInk.Guide, new Point(X(now), plot.Top), new Point(X(now), plot.Bottom));
        var heard = WavePhysics.Heard(Listener, now, mach);
        for (var i = 0; i < heard.Count; i++) Marker(dc, new Point(X(now), Y(heard[i].Frequency)), i == 0 ? ChartInk.Gold : ChartInk.Cyan);
    }
}

/// <summary>
/// The pitch straight ahead of the source and straight behind it, by its speed: ahead it climbs without end towards the
/// speed of sound; behind it only halves even at twice the speed of sound.
/// </summary>
public sealed class MachChart : WaveChart
{
    static MachChart()
    {
        AffectsRender<MachChart>(MachProperty);
    }

    public static readonly StyledProperty<double> MachProperty =
        AvaloniaProperty.Register<MachChart, double>(nameof(Mach), 0.5);

    private const double Top = 1760;

    public double Mach
    {
        get => GetValue(MachProperty);
        set => SetValue(MachProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(40, 24, width - 52, height - 46);
        double X(double mach) => plot.Left + (mach / 2 * plot.Width);
        double Y(double hz) => plot.Bottom - (Math.Min(hz, Top * 1.05) / Top * plot.Height);

        dc.DrawRectangle(ChartInk.SupersonicShade, null, new Rect(X(1), plot.Top, plot.Right - X(1), plot.Height));
        CurveLabel(dc, Loc("Waves_Chart_Supersonic"), new Point(X(1) + 6, plot.Top + 10), ChartInk.Orange, size: 10.5);

        foreach (var hz in new[] { 0.0, 440, 880, 1320, 1760 })
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(hz)), new Point(plot.Right, Y(hz)));
            AxisLabel(dc, Number(hz, "0"), new Point(plot.Left - 5, Y(hz)), alignRight: true);
        }

        foreach (var mach in new[] { 0.0, 0.5, 1, 1.5 })
        {
            AxisLabel(dc, Number(mach, "0.#"), new Point(X(mach), plot.Bottom + 11), center: true);
        }

        AxisLabel(dc, Loc("Waves_Chart_Hz"), new Point(plot.Left - 5, 10), alignRight: true);
        AxisLabel(dc, Loc("Waves_Chart_Mach"), new Point(plot.Right, plot.Bottom + 11), alignRight: true);

        var ahead = Enumerable.Range(0, 200).Select(i => i / 200.0 * 0.995).Where(m => WavePhysics.Ahead(m) < Top * 1.05)
            .Select(m => new Point(X(m), Y(WavePhysics.Ahead(m))));
        dc.DrawGeometry(null, ChartInk.GoldThin, Curve(ahead));
        dc.DrawGeometry(null, ChartInk.CyanPen, Curve(Enumerable.Range(0, 201).Select(i => i / 100.0)
            .Select(m => new Point(X(m), Y(WavePhysics.Behind(m))))));
        CurveLabel(dc, Loc("Waves_Chart_Ahead"), new Point(X(0.62), Y(WavePhysics.Ahead(0.62)) - 2), ChartInk.Gold, alignRight: true, size: 10.5);
        CurveLabel(dc, Loc("Waves_Chart_Behind"), new Point(X(1.98), Y(WavePhysics.Behind(1.98)) - 12), ChartInk.Cyan, alignRight: true, size: 10.5);

        var now = Math.Clamp(Mach, 0, 2);
        dc.DrawLine(ChartInk.Guide, new Point(X(now), plot.Top), new Point(X(now), plot.Bottom));
        if (now < 1 && WavePhysics.Ahead(now) < Top) Marker(dc, new Point(X(now), Y(WavePhysics.Ahead(now))), ChartInk.Gold);
        Marker(dc, new Point(X(now), Y(WavePhysics.Behind(now))), ChartInk.Cyan);
    }
}
