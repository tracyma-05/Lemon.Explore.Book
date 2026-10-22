using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>
/// What the eclipse looks like from Earth. Solar mode: the observer's sky, the Moon sliding over the Sun, the sky
/// darkening, the corona at totality and the ring of an annular eclipse. Lunar mode: the Moon crossing Earth's shadow,
/// with the (normally invisible) outlines of the penumbra and umbra drawn in.
/// </summary>
/// <remarks>Follows the same inputs as <see cref="EclipseDiagram"/> and redraws whenever one of them changes.</remarks>
public sealed class EclipseSkyView : Control
{
    static EclipseSkyView()
    {
        AffectsRender<EclipseSkyView>(ModeProperty, MoonAngleProperty, IsMoonNearProperty, ObserverOffsetProperty, LunarHeightProperty);
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

    public static readonly StyledProperty<EclipseMode> ModeProperty =
        AvaloniaProperty.Register<EclipseSkyView, EclipseMode>(nameof(Mode), EclipseMode.Solar);

    public static readonly StyledProperty<double> MoonAngleProperty =
        AvaloniaProperty.Register<EclipseSkyView, double>(nameof(MoonAngle), 0.0);

    public static readonly StyledProperty<bool> IsMoonNearProperty =
        AvaloniaProperty.Register<EclipseSkyView, bool>(nameof(IsMoonNear), true);

    public static readonly StyledProperty<double> ObserverOffsetProperty =
        AvaloniaProperty.Register<EclipseSkyView, double>(nameof(ObserverOffset), 0.0);

    public static readonly StyledProperty<double> LunarHeightProperty =
        AvaloniaProperty.Register<EclipseSkyView, double>(nameof(LunarHeight), 0.0);

    public EclipseMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double MoonAngle
    {
        get => GetValue(MoonAngleProperty);
        set => SetValue(MoonAngleProperty, value);
    }

    public bool IsMoonNear
    {
        get => GetValue(IsMoonNearProperty);
        set => SetValue(IsMoonNearProperty, value);
    }

    public double ObserverOffset
    {
        get => GetValue(ObserverOffsetProperty);
        set => SetValue(ObserverOffsetProperty, value);
    }

    public double LunarHeight
    {
        get => GetValue(LunarHeightProperty);
        set => SetValue(LunarHeightProperty, value);
    }

    /// <summary>A fixed scatter of stars (x, y in 0…1, radius) that shows in a dark sky.</summary>
    private static readonly (double X, double Y, double R)[] Stars = CreateStars();

    private readonly PlanetRenderer _moon = new(Body("Moon"));
    private readonly LabelCache _labels;

    public EclipseSkyView()
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
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (bounds.Width < 1 || bounds.Height < 1) return;

        if (Mode == EclipseMode.Lunar)
        {
            DrawLunar(dc, bounds);
        }
        else
        {
            DrawSolar(dc, bounds);
        }
    }

    private void DrawSolar(PaintContext dc, Rect bounds)
    {
        var sky = EclipseGeometry.Solar(MoonAngle * Math.PI / 180, IsMoonNear, ObserverOffset);
        var total = sky.Stage == EclipseStage.SolarTotal;

        // Daylight hardly changes until the Sun is almost covered; at totality it is night-dark within seconds.
        var darkness = total ? 1 : Noise.SmoothStep(0.7, 1, sky.Coverage) * (sky.MoonRadius >= sky.SunRadius ? 0.7 : 0.3);
        var skyBrush = SpaceDrawing.LinearGradient(Lerp(Paint.DayTop, Paint.NightTop, darkness), Lerp(Paint.DayBottom, Paint.NightBottom, darkness), 90);
        dc.DrawRectangle(skyBrush, null, bounds);
        DrawStars(dc, bounds, Noise.SmoothStep(0.55, 1, darkness));

        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        var scale = Math.Min(bounds.Width, bounds.Height) * 0.24 / sky.SunRadius;
        var sunRadius = sky.SunRadius * scale;

        // The corona only shows once the bright disc is (nearly) gone.
        var corona = total ? 1 : sky.MoonRadius >= sky.SunRadius ? Noise.SmoothStep(0.985, 1, sky.Coverage) * 0.6 : 0;
        if (corona > 0) DrawCorona(dc, center, sunRadius, corona);

        var glow = Math.Sqrt(1 - sky.Coverage);
        if (glow > 0.01)
        {
            dc.PushOpacity(glow);
            dc.DrawEllipse(Paint.SunGlow, null, center, sunRadius * 3, sunRadius * 3);
            dc.Pop();
        }

        dc.DrawEllipse(Paint.SunDisc, null, center, sunRadius, sunRadius);

        if (!sky.MoonInFront) return;

        var moon = new Point(center.X + (sky.MoonX * scale), center.Y - (sky.MoonY * scale));
        var moonRadius = sky.MoonRadius * scale;
        dc.DrawEllipse(total ? Paint.MoonSilhouetteDark : Paint.MoonSilhouette, Paint.MoonOutline, moon, moonRadius, moonRadius);

        // The diamond ring: the last (and first) bead of sunlight beside the Moon's edge.
        if (!total && sky.MoonRadius >= sky.SunRadius && sky.Coverage > 0.95)
        {
            Vector away = center - moon;
            if (away.Length > 1e-6)
            {
                var bead = center + (away / away.Length * sunRadius);
                var strength = Noise.SmoothStep(0.95, 0.995, sky.Coverage);
                dc.PushOpacity(strength);
                dc.DrawEllipse(Paint.Diamond, null, bead, sunRadius * 0.5, sunRadius * 0.5);
                dc.DrawLine(Paint.Flare, new Point(bead.X - (sunRadius * 0.7), bead.Y), new Point(bead.X + (sunRadius * 0.7), bead.Y));
                dc.DrawLine(Paint.Flare, new Point(bead.X, bead.Y - (sunRadius * 0.7)), new Point(bead.X, bead.Y + (sunRadius * 0.7)));
                dc.Pop();
            }
        }
    }

    private static void DrawCorona(PaintContext dc, Point center, double sunRadius, double opacity)
    {
        dc.PushOpacity(opacity);
        dc.DrawEllipse(Paint.CoronaHalo, null, center, sunRadius * 2.6, sunRadius * 2.6);

        // Streamers: long soft rays, a few along the solar equator and shorter plumes near the poles.
        for (var i = 0; i < Streamers.Length; i++)
        {
            var (angle, length, width) = Streamers[i];
            dc.PushTransform(SpaceDrawing.Rotation(angle, center.X, center.Y));
            dc.DrawEllipse(Paint.Streamer, null, new Point(center.X + (sunRadius * (0.55 + (length / 2))), center.Y),
                sunRadius * (length / 2 + 0.45), sunRadius * width);
            dc.Pop();
        }

        dc.Pop();
    }

    private static readonly (double Angle, double Length, double Width)[] Streamers =
    [
        (-8, 2.6, 0.30), (14, 1.9, 0.22), (172, 2.8, 0.32), (196, 2.0, 0.24),
        (62, 1.2, 0.16), (118, 1.1, 0.14), (-62, 1.15, 0.15), (-116, 1.3, 0.16), (35, 1.5, 0.18), (215, 1.6, 0.2),
    ];

    private void DrawLunar(PaintContext dc, Rect bounds)
    {
        var lunar = EclipseGeometry.Lunar(MoonAngle * Math.PI / 180, LunarHeight);
        dc.DrawRectangle(Paint.NightSky, null, bounds);
        DrawStars(dc, bounds, 0.9);

        // A fixed scale: the shadow as it is at full moon fills most of the height.
        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        var fullMoonPenumbra = EclipseGeometry.EarthShadow.Penumbra(EclipseGeometry.MoonOrbitNear);
        var scale = Math.Min(bounds.Width, bounds.Height) * 0.46 / fullMoonPenumbra;

        var penumbra = lunar.PenumbraRadius * scale;
        var umbra = lunar.UmbraRadius * scale;
        dc.DrawEllipse(Paint.PenumbraFill(umbra / penumbra), Paint.PenumbraOutline, center, penumbra, penumbra);
        dc.DrawEllipse(Paint.UmbraFill, Paint.UmbraOutline, center, umbra, umbra);

        var umbraLabel = _labels.Get("Eclipse_Label_Umbra", 11, Paint.UmbraText);
        dc.DrawText(umbraLabel, new Point(center.X - (umbraLabel.Width / 2), center.Y + umbra - umbraLabel.Height - 4));
        var penumbraLabel = _labels.Get("Eclipse_Label_Penumbra", 11, Paint.PenumbraText);
        dc.DrawText(penumbraLabel, new Point(center.X - (penumbraLabel.Width / 2), center.Y + penumbra - penumbraLabel.Height - 4));

        // The Moon's path across the sky (east is to the left).
        var pathY = center.Y - (lunar.MoonY * scale);
        dc.DrawLine(Paint.PathPen, new Point(16, pathY), new Point(bounds.Width - 10, pathY));
        DrawArrowHead(dc, Paint.PathArrow, new Point(10, pathY), new Vector(-1, 0), 8);

        if (!lunar.BehindEarth) return;

        var moon = new Point(center.X + (lunar.MoonX * scale), pathY);
        var radius = lunar.MoonRadius * scale;
        DrawSphere(dc, _moon, moon, radius, new LightVector(0, 0, 1), RenderScale());

        // Earth's shadow on the Moon: a sharp copper umbra and a penumbra that fades out towards its edge.
        dc.PushClip(SpaceDrawing.Ellipse(moon, radius, radius));
        dc.DrawEllipse(Paint.ShadowOnMoon(center, umbra, penumbra), null, center, penumbra, penumbra);
        dc.Pop();
    }

    private static void DrawStars(PaintContext dc, Rect bounds, double opacity)
    {
        if (opacity <= 0.01) return;

        dc.PushOpacity(opacity);
        foreach (var (x, y, r) in Stars)
        {
            dc.DrawEllipse(Paint.Star, null, new Point(x * bounds.Width, y * bounds.Height), r, r);
        }

        dc.Pop();
    }

    private static (double X, double Y, double R)[] CreateStars()
    {
        var random = new Random(1919);
        var stars = new (double, double, double)[46];
        for (var i = 0; i < stars.Length; i++)
        {
            stars[i] = (random.NextDouble(), random.NextDouble(), 0.5 + (Math.Pow(random.NextDouble(), 3) * 1.3));
        }

        return stars;
    }

    private static class Paint
    {
        public static readonly Color DayTop = Color.FromRgb(0x2E, 0x6C, 0xC0);
        public static readonly Color DayBottom = Color.FromRgb(0x8C, 0xBC, 0xEB);
        public static readonly Color NightTop = Color.FromRgb(0x04, 0x07, 0x12);
        public static readonly Color NightBottom = Color.FromRgb(0x1A, 0x22, 0x3A);
        public static readonly Color UmbraText = Color.FromArgb(0xE6, 0xF0, 0x9A, 0x78);
        public static readonly Color PenumbraText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);

        public static readonly IBrush NightSky = SpaceDrawing.LinearGradient(Color.FromRgb(0x03, 0x05, 0x0D), Color.FromRgb(0x0C, 0x13, 0x26), 90);
        public static readonly IBrush Star = Solid(230, 255, 255, 255);

        public static readonly IBrush SunDisc = SpaceDrawing.RadialGradient(
            (Color.FromRgb(0xFF, 0xFF, 0xF6), 0),
            (Color.FromRgb(0xFF, 0xF4, 0xCF), 0.7),
            (Color.FromRgb(0xFF, 0xD8, 0x88), 1));

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(220, 255, 250, 225), 0.3),
            (Color.FromArgb(70, 255, 240, 200), 0.45),
            (Color.FromArgb(0, 255, 240, 200), 1));

        public static readonly IBrush CoronaHalo = SpaceDrawing.RadialGradient(
            (Color.FromArgb(250, 245, 248, 255), 0.36),
            (Color.FromArgb(120, 210, 222, 255), 0.5),
            (Color.FromArgb(30, 190, 205, 255), 0.75),
            (Color.FromArgb(0, 190, 205, 255), 1));

        public static readonly IBrush Streamer = SpaceDrawing.RadialGradient(
            (Color.FromArgb(120, 235, 240, 255), 0),
            (Color.FromArgb(0, 235, 240, 255), 1));

        public static readonly IBrush MoonSilhouette = Solid(255, 0x1B, 0x22, 0x31);
        public static readonly IBrush MoonSilhouetteDark = Solid(255, 0x02, 0x03, 0x06);
        public static readonly IPen MoonOutline = MakePen(Solid(60, 255, 255, 255), 0.8);

        public static readonly IBrush Diamond = SpaceDrawing.RadialGradient(
            (Color.FromArgb(255, 255, 255, 255), 0),
            (Color.FromArgb(160, 255, 250, 230), 0.25),
            (Color.FromArgb(0, 255, 250, 230), 1));

        public static readonly IPen Flare = MakePen(Solid(170, 255, 255, 255), 1);

        public static readonly IBrush UmbraFill = Solid(40, 150, 50, 30);
        public static readonly IPen UmbraOutline = MakePen(Solid(170, 0xE0, 0x70, 0x3C), 1, dashed: true);
        public static readonly IPen PenumbraOutline = MakePen(Solid(110, 0xB0, 0xB8, 0xC8), 1, dashed: true);
        public static readonly IPen PathPen = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush PathArrow = Solid(150, 255, 255, 255);

        /// <summary>The penumbra, faint at its outer edge and darker towards the umbra.</summary>
        public static IBrush PenumbraFill(double umbraShare) => SpaceDrawing.RadialGradient(
            (Color.FromArgb(46, 0, 0, 0), Math.Clamp(umbraShare, 0, 1)),
            (Color.FromArgb(0, 0, 0, 0), 1));

        /// <summary>Earth's shadow as it darkens the Moon, centred on the shadow's axis.</summary>
        public static IBrush ShadowOnMoon(Point center, double umbra, double penumbra)
        {
            var edge = Math.Clamp(umbra / penumbra, 0, 1);
            // Drawn on the penumbra's own circle round the shadow's axis, so the gradient's relative centre is that axis.
            return SpaceDrawing.RadialGradient(
                (Color.FromArgb(210, 92, 26, 10), 0),
                (Color.FromArgb(200, 120, 38, 16), edge),
                (Color.FromArgb(190, 0, 0, 0), Math.Min(1, edge + 0.004)),
                (Color.FromArgb(0, 0, 0, 0), 1));
        }
    }
}
