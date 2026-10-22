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

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// The side panel's chart, one per view. Shield: how far out the magnetopause sits against the solar wind's push, for a
/// northward, zero and southward field, with geostationary orbit marked. Particles: the field's strength along the
/// particle's line against latitude, the level where it turns back and the air at the line's foot. Sky: the light the
/// electrons give off by height: oxygen green, oxygen red and nitrogen violet.
/// </summary>
public sealed class AuroraChart : Control
{
    static AuroraChart()
    {
        AffectsRender<AuroraChart>(ModeProperty, WindSpeedProperty, WindDensityProperty, BzProperty, ShellProperty, PitchAngleProperty, EnergyKeVProperty);
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

    public static readonly StyledProperty<AuroraMode> ModeProperty =
        AvaloniaProperty.Register<AuroraChart, AuroraMode>(nameof(Mode), AuroraMode.Shield);

    public static readonly StyledProperty<double> WindSpeedProperty =
        AvaloniaProperty.Register<AuroraChart, double>(nameof(WindSpeed), 400.0);

    public static readonly StyledProperty<double> WindDensityProperty =
        AvaloniaProperty.Register<AuroraChart, double>(nameof(WindDensity), 5.0);

    public static readonly StyledProperty<double> BzProperty =
        AvaloniaProperty.Register<AuroraChart, double>(nameof(Bz), -5.0);

    public static readonly StyledProperty<double> ShellProperty =
        AvaloniaProperty.Register<AuroraChart, double>(nameof(Shell), 4.0);

    public static readonly StyledProperty<double> PitchAngleProperty =
        AvaloniaProperty.Register<AuroraChart, double>(nameof(PitchAngle), 30.0);

    public static readonly StyledProperty<double> EnergyKeVProperty =
        AvaloniaProperty.Register<AuroraChart, double>(nameof(EnergyKeV), 5.0);


    public AuroraMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double WindSpeed
    {
        get => GetValue(WindSpeedProperty);
        set => SetValue(WindSpeedProperty, value);
    }

    public double WindDensity
    {
        get => GetValue(WindDensityProperty);
        set => SetValue(WindDensityProperty, value);
    }

    public double Bz
    {
        get => GetValue(BzProperty);
        set => SetValue(BzProperty, value);
    }

    public double Shell
    {
        get => GetValue(ShellProperty);
        set => SetValue(ShellProperty, value);
    }

    public double PitchAngle
    {
        get => GetValue(PitchAngleProperty);
        set => SetValue(PitchAngleProperty, value);
    }

    public double EnergyKeV
    {
        get => GetValue(EnergyKeVProperty);
        set => SetValue(EnergyKeVProperty, value);
    }

    private static readonly Color TextColor = Color.FromRgb(0xEE, 0xF1, 0xF6);
    private static readonly Color DimColor = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
    private static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
    private static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);

    private readonly LabelCache _labels;

    public AuroraChart()
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
        if (width < 80 || height < 60) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        var plot = new Rect(40, 12, width - 52, height - 40);
        switch (Mode)
        {
            case AuroraMode.Particles:
                DrawField(dc, plot);
                break;

            case AuroraMode.Sky:
                DrawLight(dc, plot);
                break;

            default:
                DrawStandoff(dc, plot);
                break;
        }
    }

    /// <summary>Magnetopause distance (4–16 Earth radii) against the wind's push (0.3–30 nPa, logarithmic).</summary>
    private void DrawStandoff(PaintContext dc, Rect plot)
    {
        double X(double pressure) => plot.Left + (plot.Width * (Math.Log10(pressure) + 0.5) / 2);
        double Y(double radius) => plot.Bottom - (plot.Height * (radius - 4) / 12);

        foreach (var p in new[] { 0.5, 1, 2, 5, 10, 20 })
        {
            dc.DrawLine(Grid, new Point(X(p), plot.Top), new Point(X(p), plot.Bottom));
            Plain(dc, p.ToString(CultureInfo.InvariantCulture), new Point(X(p), plot.Bottom + 4), center: true);
        }

        foreach (var r in new[] { 4.0, 8, 12, 16 })
        {
            dc.DrawLine(Grid, new Point(plot.Left, Y(r)), new Point(plot.Right, Y(r)));
            Plain(dc, r.ToString(CultureInfo.InvariantCulture), new Point(plot.Left - 6, Y(r) - 7), right: true);
        }

        // Geostationary orbit: inside it the satellites there meet the solar wind.
        var geo = Y(AuroraModel.Geostationary);
        dc.DrawLine(MakePen(Solid(170, Gold.R, Gold.G, Gold.B), 1.2, dashed: true), new Point(plot.Left, geo), new Point(plot.Right, geo));
        Text(dc, "Aurora_Chart_Geo", new Point(plot.Left + 4, geo - 15), Gold);

        foreach (var (bz, color) in new[] { (5.0, Cyan), (0.0, DimColor), (-10.0, Red) })
        {
            var points = Enumerable.Range(0, 61).Select(i =>
            {
                var p = Math.Pow(10, -0.5 + (2.0 * i / 60));
                return new Point(X(p), Y(Math.Clamp(AuroraModel.Standoff(p, bz), 4, 16)));
            });
            dc.DrawGeometry(null, MakePen(Solid(220, color.R, color.G, color.B), 1.6), Line(points));
        }

        var pressure = Math.Clamp(AuroraModel.DynamicPressure(WindDensity, WindSpeed), 0.32, 31);
        var nose = AuroraModel.Standoff(pressure, Bz);
        var at = new Point(X(pressure), Y(Math.Clamp(nose, 4, 16)));
        dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2), at, 4.5, 4.5);

        Legend(dc, plot, [("Aurora_Chart_North", Cyan), ("Aurora_Chart_Zero", DimColor), ("Aurora_Chart_South", Red)]);
        Text(dc, "Aurora_Chart_Pressure", new Point(plot.Right, plot.Bottom + 15), DimColor, right: true);
    }

    /// <summary>The field's strength along the line (× its equatorial value, logarithmic) against magnetic latitude.</summary>
    private void DrawField(PaintContext dc, Rect plot)
    {
        var shell = Math.Clamp(Shell, 1.5, 8);
        var foot = AuroraModel.FootLatitude(shell, AuroraModel.AtmosphereKm);
        var top = Math.Max(2, Math.Ceiling(Math.Log10(AuroraModel.FieldRatio(foot) * 1.3)));
        double X(double latitude) => plot.Left + (plot.Width * latitude / 80);
        double Y(double ratio) => plot.Bottom - (plot.Height * Math.Log10(Math.Max(1, ratio)) / top);

        foreach (var latitude in new[] { 0.0, 20, 40, 60, 80 })
        {
            dc.DrawLine(Grid, new Point(X(latitude), plot.Top), new Point(X(latitude), plot.Bottom));
            Plain(dc, latitude.ToString(CultureInfo.InvariantCulture) + "°", new Point(X(latitude), plot.Bottom + 4), center: true);
        }

        for (var k = 0; k <= top; k++)
        {
            var value = Math.Pow(10, k);
            dc.DrawLine(Grid, new Point(plot.Left, Y(value)), new Point(plot.Right, Y(value)));
            Plain(dc, "×" + value.ToString(CultureInfo.InvariantCulture), new Point(plot.Left - 6, Y(value) - 7), right: true);
        }

        // The air at the line's foot: a particle that would turn back only past here is lost.
        var air = new Rect(X(foot), plot.Top, Math.Max(0, plot.Right - X(foot)), plot.Height);
        dc.DrawRectangle(Solid(40, Green.R, Green.G, Green.B), null, air);
        Text(dc, "Aurora_Chart_Air", new Point(Math.Min(X(foot) + 4, plot.Right - 30), plot.Top + 2), Green);

        var curve = Enumerable.Range(0, 81).Select(i => foot * i / 80).Select(l => new Point(X(l), Y(AuroraModel.FieldRatio(l))));
        dc.DrawGeometry(null, MakePen(Solid(230, Cyan.R, Cyan.G, Cyan.B), 1.8), Line(curve));

        // The level where it turns back: 1 / sin² of the pitch angle.
        var s = Math.Sin(Math.Clamp(PitchAngle, 0.5, 90) * Math.PI / 180);
        var level = 1 / (s * s);
        var mirror = AuroraModel.MirrorLatitude(PitchAngle);
        var lost = mirror >= foot;
        var y = Y(Math.Min(level, Math.Pow(10, top)));
        dc.DrawLine(MakePen(Solid(200, Gold.R, Gold.G, Gold.B), 1.2, dashed: true), new Point(plot.Left, y), new Point(plot.Right, y));
        if (!lost)
        {
            var at = new Point(X(mirror), Y(level));
            dc.DrawEllipse(Solid(255, Gold.R, Gold.G, Gold.B), null, at, 4.5, 4.5);
            Text(dc, "Aurora_Chart_Mirror", new Point(at.X + 6, at.Y - 18), Gold);
        }
        else
        {
            Text(dc, "Aurora_Chart_Lost", new Point(plot.Left + 4, y - 17), Green);
        }

        Text(dc, "Aurora_Chart_Latitude", new Point(plot.Right, plot.Bottom + 15), DimColor, right: true);
    }

    /// <summary>The light by height (80–450 km) for the electrons' energy: green, red and violet, each relative to the most.</summary>
    private void DrawLight(PaintContext dc, Rect plot)
    {
        const double bottom = 80, topHeight = 450;
        double Y(double height) => plot.Bottom - (plot.Height * (height - bottom) / (topHeight - bottom));
        foreach (var height in new[] { 100.0, 200, 300, 400 })
        {
            dc.DrawLine(Grid, new Point(plot.Left, Y(height)), new Point(plot.Right, Y(height)));
            Plain(dc, height.ToString(CultureInfo.InvariantCulture), new Point(plot.Left - 6, Y(height) - 7), right: true);
        }

        var energy = Math.Clamp(EnergyKeV, 0.2, 100);
        var samples = Enumerable.Range(0, 186).Select(i => bottom + (2.0 * i)).Select(h => (Height: h, Light: AuroraModel.Light(h, energy))).ToList();
        var most = Math.Max(1e-9, samples.Max(s => Math.Max(s.Light.Green, Math.Max(s.Light.Red, s.Light.Violet))));
        double X(double value) => plot.Left + (plot.Width * Math.Clamp(value / most, 0, 1));

        foreach (var (pick, color) in new (Func<AuroraLight, double>, Color)[] { (l => l.Violet, Violet), (l => l.Red, Red), (l => l.Green, Green) })
        {
            var points = samples.Select(s => new Point(X(pick(s.Light)), Y(s.Height)));
            dc.DrawGeometry(null, MakePen(Solid(235, color.R, color.G, color.B), 1.8), Line(points));
        }

        var peak = AuroraModel.PeakHeight(energy);
        dc.DrawLine(MakePen(Solid(150, Gold.R, Gold.G, Gold.B), 1, dashed: true), new Point(plot.Left, Y(peak)), new Point(plot.Right, Y(peak)));
        Legend(dc, plot, [("Aurora_Chart_Green", Green), ("Aurora_Chart_Red", Red), ("Aurora_Chart_Violet", Violet)]);
        Text(dc, "Aurora_Chart_Brightness", new Point(plot.Right, plot.Bottom + 15), DimColor, right: true);
        Text(dc, "Aurora_Chart_Km", new Point(plot.Left - 36, plot.Top - 12), DimColor);
    }

    private void Legend(PaintContext dc, Rect plot, (string Key, Color Color)[] items)
    {
        var y = plot.Top + 4;
        foreach (var (key, color) in items)
        {
            var text = _labels.Get(key, 10.5, color);
            dc.DrawText(text, new Point(plot.Right - text.Width - 4, y));
            y += text.Height + 1;
        }
    }

    private void Text(PaintContext dc, string key, Point at, Color color, bool right = false)
    {
        var text = _labels.Get(key, 10.5, color);
        dc.DrawText(text, right ? new Point(at.X - text.Width, at.Y) : at);
    }

    private void Plain(PaintContext dc, string value, Point at, bool center = false, bool right = false)
    {
        var text = _labels.Text(value, 10, DimColor);
        var x = center ? at.X - (text.Width / 2) : right ? at.X - text.Width : at.X;
        dc.DrawText(text, new Point(x, at.Y));
    }

    private static StreamGeometry Line(IEnumerable<Point> points)
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
}
