using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Rainbow;

/// <summary>A small chart of the rainbow side panel: redrawn when a property changes or the language does.</summary>
public abstract class RainbowChart : Control
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

    protected RainbowChart()
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

    /// <summary>A localized caption in a corner of the plot.</summary>
    private protected void Caption(PaintContext dc, string text, Point at, Color color, bool alignRight = false)
    {
        var formatted = Labels.Text(text, 10.5, color);
        dc.DrawText(formatted, new Point(alignRight ? at.X - formatted.Width : at.X, at.Y));
    }

    /// <summary>A value label beside a marked point, kept inside the plot.</summary>
    private protected void PointLabel(PaintContext dc, string value, Point point, Rect plot, Color color, bool below = false)
    {
        var text = Labels.Text(value, 11, color, true);
        var at = new Point(point.X + 8, below ? point.Y + 4 : point.Y - text.Height - 3);
        if (at.X + text.Width > plot.Right) at = at.WithX(point.X - text.Width - 8);
        at = at.WithY(Math.Clamp(at.Y, plot.Top - 4, plot.Bottom - text.Height));
        dc.DrawText(text, at);
    }

    private protected static void Marker(PaintContext dc, Point point, Color color, double radius = 4.5)
    {
        dc.DrawEllipse(Solid(70, color.R, color.G, color.B), null, point, radius * 2, radius * 2);
        dc.DrawEllipse(Solid(255, color.R, color.G, color.B), null, point, radius, radius);
    }

    private protected static StreamGeometry Curve(IEnumerable<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (double.IsNaN(point.X) || double.IsNaN(point.Y))
                {
                    first = true;
                    continue;
                }

                if (first) context.BeginFigure(point, false);
                else context.LineTo(point);
                first = false;
            }

            context.EndFigure(false);
        }
        return geometry;
    }

    /// <summary>Grid lines and labels up the left (or the right), every step from low to high.</summary>
    private protected void ValueAxis(PaintContext dc, Rect plot, double low, double high, double step, string unit, bool right = false, bool grid = true)
    {
        for (var value = Math.Ceiling(low / step) * step; value <= high + 1e-9; value += step)
        {
            var y = plot.Bottom - ((value - low) / (high - low) * plot.Height);
            if (grid) dc.DrawLine(ChartInk.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            AxisLabel(dc, $"{Number(value, step < 1 ? "0.#" : "0")}{unit}", new Point(right ? plot.Right + 5 : plot.Left - 5, y), alignRight: !right);
        }
    }

    /// <summary>Labels along the bottom, every step from low to high.</summary>
    private protected void BottomAxis(PaintContext dc, Rect plot, double low, double high, double step, string unit)
    {
        for (var value = Math.Ceiling(low / step) * step; value <= high + 1e-9; value += step)
        {
            var x = plot.Left + ((value - low) / (high - low) * plot.Width);
            dc.DrawLine(ChartInk.Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            AxisLabel(dc, $"{Number(value, step < 1 ? "0.0#" : "0")}{unit}", new Point(x, plot.Bottom + 9), center: true);
        }
    }

    private protected static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private protected static Color Spectral(double nm) => RainbowDiagram.LightColor(nm);

    private protected static class ChartInk
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Guide = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush Shade = Solid(26, 0xB3, 0x9D, 0xFF);
    }
}

/// <summary>
/// Surface: the bent angle and the share reflected against the beam's angle (from inside, the bent angle runs out at the
/// critical angle and everything is reflected past it). Prism: how far each colour is turned against the angle it meets
/// the first face at, lowest at the symmetric path.
/// </summary>
public sealed class RefractionChart : RainbowChart
{
    static RefractionChart()
    {
        AffectsRender<RefractionChart>(SceneProperty, MediumProperty, ColourProperty, FromInsideProperty, AngleProperty);
    }

    public static readonly StyledProperty<RefractionScene> SceneProperty =
        AvaloniaProperty.Register<RefractionChart, RefractionScene>(nameof(Scene), RefractionScene.Surface);
    public static readonly StyledProperty<OpticalMedium> MediumProperty =
        AvaloniaProperty.Register<RefractionChart, OpticalMedium>(nameof(Medium), OpticalMedium.Water);
    public static readonly StyledProperty<LightColour> ColourProperty =
        AvaloniaProperty.Register<RefractionChart, LightColour>(nameof(Colour), LightColour.White);
    public static readonly StyledProperty<bool> FromInsideProperty =
        AvaloniaProperty.Register<RefractionChart, bool>(nameof(FromInside), false);
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<RefractionChart, double>(nameof(Angle), 40.0);

    public RefractionScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public OpticalMedium Medium
    {
        get => GetValue(MediumProperty);
        set => SetValue(MediumProperty, value);
    }

    public LightColour Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public bool FromInside
    {
        get => GetValue(FromInsideProperty);
        set => SetValue(FromInsideProperty, value);
    }

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        if (Scene == RefractionScene.Prism) DrawPrism(dc, width, height);
        else DrawSurface(dc, width, height);
    }

    private void DrawSurface(PaintContext dc, double width, double height)
    {
        var plot = new Rect(36, 12, width - 78, height - 36);
        var nm = Colour == LightColour.White ? RainbowOptics.YellowNm : RainbowOptics.Wavelength(Colour);
        var n = RainbowOptics.Index(Medium, nm);
        var (n1, n2) = FromInside ? (n, 1.0) : (1.0, n);
        double X(double angle) => plot.Left + (angle / 90 * plot.Width);
        double Y(double value) => plot.Bottom - (value / 90 * plot.Height);

        var critical = RainbowOptics.CriticalAngle(n1, n2);
        if (!double.IsNaN(critical))
        {
            dc.DrawRectangle(ChartInk.Shade, null, new Rect(X(critical), plot.Top, plot.Right - X(critical), plot.Height));
            Caption(dc, Loc("Rain_Chart_AllReflected"), new Point(plot.Right - 4, plot.Top + 4), ChartInk.Violet, alignRight: true);
        }

        BottomAxis(dc, plot, 0, 90, 30, "°");
        ValueAxis(dc, plot, 0, 90, 30, "°");
        for (var p = 0; p <= 100; p += 50)
        {
            AxisLabel(dc, $"{p}%", new Point(plot.Right + 5, plot.Bottom - (p / 100.0 * plot.Height)));
        }

        dc.DrawLine(ChartInk.Guide, new Point(X(0), Y(0)), new Point(X(90), Y(90)));
        var bent = Curve(Enumerable.Range(0, 181).Select(i => i / 2.0).Select(a =>
        {
            var r = RainbowOptics.Refraction(n1, n2, a);
            return double.IsNaN(r) ? new Point(double.NaN, double.NaN) : new Point(X(a), Y(r));
        }));
        dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Cyan.R, ChartInk.Cyan.G, ChartInk.Cyan.B), 2), bent);
        var reflected = Curve(Enumerable.Range(0, 181).Select(i => i / 2.0)
            .Select(a => new Point(X(a), plot.Bottom - (RainbowOptics.Reflectance(n1, n2, a) * plot.Height))));
        dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Gold.R, ChartInk.Gold.G, ChartInk.Gold.B), 2), reflected);

        Caption(dc, Loc("Rain_Chart_Bent"), new Point(plot.Left + 4, plot.Top + 2), ChartInk.Cyan);
        Caption(dc, Loc("Rain_Chart_ReflectedShare"), new Point(plot.Left + 4, plot.Top + 16), ChartInk.Gold);

        var angle = Math.Clamp(Angle, 0, 89);
        var now = RainbowOptics.Refraction(n1, n2, angle);
        var share = RainbowOptics.Reflectance(n1, n2, angle);
        dc.DrawLine(ChartInk.Guide, new Point(X(angle), plot.Top), new Point(X(angle), plot.Bottom));
        if (!double.IsNaN(now))
        {
            var point = new Point(X(angle), Y(now));
            Marker(dc, point, ChartInk.Cyan);
            PointLabel(dc, $"{Number(now, "0.0")}°", point, plot, ChartInk.Cyan);
        }

        var at = new Point(X(angle), plot.Bottom - (share * plot.Height));
        Marker(dc, at, ChartInk.Gold, 3.5);
        PointLabel(dc, RainbowDiagram.Percent(share), at, plot, ChartInk.Gold, below: share > 0.5);
    }

    private void DrawPrism(PaintContext dc, double width, double height)
    {
        var plot = new Rect(36, 12, width - 50, height - 36);
        var colours = Colour == LightColour.White ? new[] { RainbowOptics.RedNm, RainbowOptics.VioletNm } : [RainbowOptics.Wavelength(Colour)];
        var apex = RainbowDiagram.PrismApex;
        var values = colours.SelectMany(nm => Enumerable.Range(0, 90).Select(a => RainbowOptics.PrismDeviation(RainbowOptics.Index(Medium, nm), apex, a)))
            .Where(d => !double.IsNaN(d)).ToArray();
        if (values.Length == 0) return;
        var low = Math.Floor((values.Min() - 2) / 10) * 10;
        var high = Math.Max(low + 20, Math.Min(90, Math.Ceiling((values.Max() + 2) / 10) * 10));
        double X(double angle) => plot.Left + (angle / 90 * plot.Width);
        double Y(double value) => plot.Bottom - ((value - low) / (high - low) * plot.Height);

        BottomAxis(dc, plot, 0, 90, 30, "°");
        ValueAxis(dc, plot, low, high, high - low > 40 ? 20 : 10, "°");

        foreach (var nm in colours)
        {
            var n = RainbowOptics.Index(Medium, nm);
            var color = Spectral(nm);
            var curve = Curve(Enumerable.Range(0, 179).Select(i => i / 2.0).Select(a =>
            {
                var d = RainbowOptics.PrismDeviation(n, apex, a);
                return double.IsNaN(d) || d > high ? new Point(double.NaN, double.NaN) : new Point(X(a), Y(d));
            }));
            dc.DrawGeometry(null, MakePen(new SolidColorBrush(color), 2), curve);

            var best = RainbowOptics.MinimumDeviationIncidence(n, apex);
            var least = new Point(X(best), Y(RainbowOptics.MinimumDeviation(n, apex)));
            dc.DrawEllipse(null, MakePen(new SolidColorBrush(color), 1.2), least, 4, 4);

            var angle = Math.Clamp(Angle, 0, 89);
            var now = RainbowOptics.PrismDeviation(n, apex, angle);
            if (!double.IsNaN(now) && now <= high)
            {
                var point = new Point(X(angle), Y(now));
                Marker(dc, point, color, 3.5);
                if (nm == colours[^1]) PointLabel(dc, $"{Number(now, "0.0")}°", point, plot, color, below: true);
            }
        }

        dc.DrawLine(ChartInk.Guide, new Point(X(Angle), plot.Top), new Point(X(Angle), plot.Bottom));
        Caption(dc, Loc("Rain_Chart_Turn"), new Point(plot.Right - 4, plot.Top + 2), ChartInk.Text, alignRight: true);
        Caption(dc, Loc("Rain_Chart_Least"), new Point(plot.Right - 4, plot.Top + 16), ChartInk.Dim, alignRight: true);
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);
}

/// <summary>
/// Where light comes out of a drop against where it went in: the primary rises to a top at 42° and turns back, the
/// secondary falls to a bottom at 51° and turns back; nothing comes out between them.
/// </summary>
public sealed class DeviationChart : RainbowChart
{
    static DeviationChart()
    {
        AffectsRender<DeviationChart>(OrderProperty, ColourProperty, ImpactProperty);
    }

    public static readonly StyledProperty<BowOrder> OrderProperty =
        AvaloniaProperty.Register<DeviationChart, BowOrder>(nameof(Order), BowOrder.Primary);
    public static readonly StyledProperty<LightColour> ColourProperty =
        AvaloniaProperty.Register<DeviationChart, LightColour>(nameof(Colour), LightColour.White);
    public static readonly StyledProperty<double> ImpactProperty =
        AvaloniaProperty.Register<DeviationChart, double>(nameof(Impact), 0.5);

    public BowOrder Order
    {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    public LightColour Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public double Impact
    {
        get => GetValue(ImpactProperty);
        set => SetValue(ImpactProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        const double top = 70;
        var plot = new Rect(36, 12, width - 48, height - 36);
        double X(double impact) => plot.Left + (impact * plot.Width);
        double Y(double angle) => plot.Bottom - (angle / top * plot.Height);

        var yellow = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm);
        var first = RainbowOptics.RainbowAngle(1, yellow);
        var second = RainbowOptics.RainbowAngle(2, yellow);
        dc.DrawRectangle(ChartInk.Shade, null, new Rect(plot.Left, Y(second), plot.Width, Y(first) - Y(second)));
        Caption(dc, LocalizationService.Instance.GetString("Rain_Chart_DarkBand"), new Point(plot.Left + 4, Y(second) + 1), ChartInk.Violet);

        BottomAxis(dc, plot, 0, 1, 0.25, string.Empty);
        ValueAxis(dc, plot, 0, top, 30, "°", grid: false);

        var colours = Colour == LightColour.White ? new[] { RainbowOptics.RedNm, RainbowOptics.VioletNm } : [RainbowOptics.Wavelength(Colour)];
        var orders = Order switch
        {
            BowOrder.Secondary => new[] { 2 },
            BowOrder.Both => [1, 2],
            _ => [1],
        };

        foreach (var order in orders)
        {
            foreach (var nm in colours)
            {
                var n = RainbowOptics.Index(OpticalMedium.Water, nm);
                var curve = Curve(Enumerable.Range(0, 201).Select(i => i / 200.0).Select(b =>
                {
                    var angle = RainbowOptics.ExitAngle(order, n, b);
                    return angle > top ? new Point(double.NaN, double.NaN) : new Point(X(b), Y(angle));
                }));
                dc.DrawGeometry(null, MakePen(new SolidColorBrush(Spectral(nm)), 1.8), curve);
            }

            var bow = RainbowOptics.RainbowAngle(order, yellow);
            dc.DrawLine(ChartInk.Guide, new Point(plot.Left, Y(bow)), new Point(plot.Right, Y(bow)));
            AxisLabel(dc, $"{Number(bow, "0")}°", new Point(plot.Left - 5, Y(bow)), alignRight: true);

            var impact = Math.Clamp(Impact, 0, 0.995);
            var now = RainbowOptics.ExitAngle(order, yellow, impact);
            if (now <= top)
            {
                var point = new Point(X(impact), Y(now));
                Marker(dc, point, ChartInk.Gold);
                PointLabel(dc, $"{Number(now, "0.0")}°", point, plot, ChartInk.Gold, below: order == 2);
            }
        }

        dc.DrawLine(ChartInk.Guide, new Point(X(Impact), plot.Top), new Point(X(Impact), plot.Bottom));
    }
}

/// <summary>
/// The light the rain sends back by angle from the point opposite the Sun, in its own colours: the sky lit inside the
/// primary bow, the bow at 42°, the dark band, the fainter secondary at 51° with its colours the other way round.
/// </summary>
public sealed class BowProfileChart : RainbowChart
{
    static BowProfileChart()
    {
        AffectsRender<BowProfileChart>(DropSizeProperty, SecondaryProperty);
    }

    public static readonly StyledProperty<DropSize> DropSizeProperty =
        AvaloniaProperty.Register<BowProfileChart, DropSize>(nameof(DropSize), DropSize.Rain);
    public static readonly StyledProperty<bool> SecondaryProperty =
        AvaloniaProperty.Register<BowProfileChart, bool>(nameof(Secondary), true);

    public DropSize DropSize
    {
        get => GetValue(DropSizeProperty);
        set => SetValue(DropSizeProperty, value);
    }

    public bool Secondary
    {
        get => GetValue(SecondaryProperty);
        set => SetValue(SecondaryProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        const double from = 25;
        const double to = 60;
        var plot = new Rect(14, 10, width - 28, height - 32);
        var colours = RainbowOptics.BowColours(DropSize, Secondary);

        // Brightness on a square-root scale, so the faint glow inside the bow shows next to the bow itself.
        var reference = RainbowOptics.BowColours(DropSize.Rain, false)
            .Skip((int)(from * RainbowOptics.ProfileSteps)).Take((int)((to - from) * RainbowOptics.ProfileSteps)).Max(c => c.Luminance);
        var columns = (int)Math.Ceiling(plot.Width);
        for (var i = 0; i < columns; i++)
        {
            var angle = from + ((i + 0.5) / columns * (to - from));
            var light = colours[(int)Math.Round(angle * RainbowOptics.ProfileSteps)];
            var level = Math.Sqrt(Math.Max(0, light.Luminance) / reference);
            var bar = Math.Min(plot.Height, level * plot.Height * 0.92);
            var color = SkyOptics.Hue(light);
            var shown = SkyOptics.Display(light, 3 / reference);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(255, (byte)((color.R + shown.R) / 2), (byte)((color.G + shown.G) / 2), (byte)((color.B + shown.B) / 2))),
                null, new Rect(plot.Left + i, plot.Bottom - bar, 1.2, bar));
        }

        dc.DrawLine(ChartInk.Grid, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        for (var a = 30; a <= 60; a += 10)
        {
            var x = plot.Left + ((a - from) / (to - from) * plot.Width);
            AxisLabel(dc, $"{a}°", new Point(x, plot.Bottom + 9), center: true);
        }

        var yellow = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm);
        var gap = plot.Left + ((((RainbowOptics.RainbowAngle(1, yellow) + RainbowOptics.RainbowAngle(2, yellow)) / 2) - from) / (to - from) * plot.Width);
        var text = Labels.Text(LocalizationService.Instance.GetString("Rain_Chart_DarkBandShort"), 10.5, ChartInk.Dim);
        dc.DrawText(text, new Point(gap - (text.Width / 2), plot.Top));
    }
}

/// <summary>
/// The sky from the side: sunlight from behind you, the line to the point opposite the Sun, and the drops 42° (and 51°)
/// off it that send you each colour: red from higher drops in the primary bow, from lower ones in the secondary.
/// </summary>
public sealed class SkySideView : RainbowChart
{
    static SkySideView()
    {
        AffectsRender<SkySideView>(SunAltitudeProperty, SecondaryProperty);
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    public static readonly StyledProperty<double> SunAltitudeProperty =
        AvaloniaProperty.Register<SkySideView, double>(nameof(SunAltitude), 20.0);
    public static readonly StyledProperty<bool> SecondaryProperty =
        AvaloniaProperty.Register<SkySideView, bool>(nameof(Secondary), true);

    public double SunAltitude
    {
        get => GetValue(SunAltitudeProperty);
        set => SetValue(SunAltitudeProperty, value);
    }

    public bool Secondary
    {
        get => GetValue(SecondaryProperty);
        set => SetValue(SecondaryProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        const double deg = Math.PI / 180;
        var ground = height - 30;
        var eye = new Point(46, ground - 14);
        var sun = Math.Clamp(SunAltitude, 0, 60);
        var reach = width - 70;

        // Sunlight from behind, coming down at the Sun's height: parallel lines across the whole view, then the ground over them.
        var sunward = new Vector(Math.Cos(sun * deg), Math.Sin(sun * deg));
        var across = new Vector(-sunward.Y, sunward.X);
        var middle = new Point(width / 2, ground / 2);
        for (var k = -12; k <= 12; k++)
        {
            var through = middle + (across * (k * 24));
            dc.DrawLine(Sunbeam, through - (sunward * 800), through + (sunward * 800));
        }

        dc.DrawRectangle(GroundBrush, null, new Rect(0, ground, width, height - ground));
        dc.DrawLine(GroundLine, new Point(0, ground), new Point(width, ground));

        // The person, and the line to the point opposite the Sun.
        dc.DrawLine(PersonPen, new Point(eye.X, ground), eye + new Vector(0, 4));
        dc.DrawEllipse(PersonBrush, null, eye, 4.5, 4.5);
        dc.DrawLine(ChartInk.Guide, eye, eye + (sunward * reach));
        Caption(dc, LocalizationService.Instance.GetString("Rain_Chart_ToAntisolar"), eye + (sunward * Math.Min(reach * 0.55, (ground - eye.Y) / Math.Max(0.05, sunward.Y) * 0.8)) + new Vector(4, 2), ChartInk.Dim);

        var red = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.RedNm);
        var violet = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.VioletNm);
        DrawBow(dc, eye, sun, ground, reach * 0.9, 1, red, violet, "Rain_Chart_Primary");
        if (Secondary) DrawBow(dc, eye, sun, ground, reach * 0.5, 2, red, violet, "Rain_Chart_Secondary");
    }

    /// <summary>The two drops of one bow on the line of sight above the antisolar line: red and violet, each with its ray to the eye.</summary>
    private void DrawBow(PaintContext dc, Point eye, double sun, double ground, double distance, int order, double red, double violet, string key)
    {
        const double deg = Math.PI / 180;
        Point? labelAt = null;
        foreach (var (n, nm) in new[] { (red, RainbowOptics.RedNm), (violet, RainbowOptics.VioletNm) })
        {
            // Up from the antisolar line (which runs down at the Sun's height) by the bow's angle.
            var angle = RainbowOptics.RainbowAngle(order, n) - sun;
            var direction = new Vector(Math.Cos(angle * deg), -Math.Sin(angle * deg));

            // Kept inside the view: nearer for a high bow.
            var drop = eye + (direction * Math.Min(distance, (eye.Y - 16) / Math.Max(0.05, -direction.Y)));
            var color = Spectral(nm);
            var belowGround = drop.Y > ground;
            dc.DrawLine(MakePen(new SolidColorBrush(WithAlpha(color, belowGround ? 0.35 : 0.9)), 1.4, dashed: belowGround), drop, eye);
            dc.DrawEllipse(new SolidColorBrush(WithAlpha(color, belowGround ? 0.4 : 1)), DropEdge, drop, 4.2, 4.2);
            labelAt ??= drop;
        }

        if (labelAt is { } at)
        {
            var bow = RainbowOptics.RainbowAngle(order, RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm));
            // The primary's name under its drops, the secondary's above, so the two never meet.
            var text = Labels.Text(LocalizationService.Instance.Format(key, Number(bow, "0")), 10.5, order == 1 ? ChartInk.Gold : ChartInk.Violet);
            var x = Math.Min(at.X - (text.Width / 2), ActualWidth - text.Width - 4);
            dc.DrawText(text, new Point(x, order == 1 ? at.Y + 12 : at.Y - text.Height - 8));
        }
    }

    private static readonly IBrush GroundBrush = Solid(255, 0x14, 0x22, 0x16);
    private static readonly IPen GroundLine = MakePen(Solid(120, 0xC9, 0xD1, 0xDC), 1);
    private static readonly IPen Sunbeam = MakePen(Solid(40, 0xFF, 0xE6, 0xA8), 1);
    private static readonly IPen PersonPen = MakePen(Solid(255, 0xC9, 0xD1, 0xDC), 3);
    private static readonly IBrush PersonBrush = Solid(255, 0xC9, 0xD1, 0xDC);
    private static readonly IPen DropEdge = MakePen(Solid(160, 255, 255, 255), 0.8);
}
