using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// The animated diagram of the aurora page. Shield: the magnetosphere side on in the solar wind, the magnetopause and bow
/// shock sized by the wind, and one field line taken round the Dungey cycle (<c>AuroraDiagram.Shield.cs</c>). Particles: an
/// electron or proton trapped in the dipole, spiralling, bouncing and drifting (<c>AuroraDiagram.Particles.cs</c>). Sky: the
/// auroral oval on the globe, a slice from the viewer to the curtain, and the sky the viewer sees (<c>AuroraDiagram.Sky.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock: it pushes <see cref="CyclePhase"/> (the Dungey cycle) and <see cref="SkyHour"/> (the night)
/// back as they run, and steps the trapped particle itself. The solar wind and the curtains' shimmer keep moving while
/// paused. The mouse and the clickable things are in <c>AuroraDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class AuroraDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == NoFieldProperty)
        {
            ResetWind();
        }
        else if (change.Property == CyclePhaseProperty)
        {
            OnCyclePhaseChanged(change.GetNewValue<double>());
        }
        else if (change.Property == SpeciesProperty)
        {
            RestartParticle();
        }
        else if (change.Property == ShellProperty)
        {
            RestartParticle();
        }
        else if (change.Property == PitchAngleProperty)
        {
            RestartParticle();
        }
        else if (change.Property == RestartCountProperty)
        {
            RestartParticle();
        }
        else if (change.Property == SkyHourProperty)
        {
            OnSkyHourChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Seconds one Dungey cycle takes at 1× with a 400 km/s wind (the real one takes about an hour).</summary>
    internal const double CycleSeconds = 18;

    /// <summary>Hours of the night per second at 1×.</summary>
    internal const double SkyHoursPerSecond = 0.4;

    #region dependency properties

    public static readonly StyledProperty<AuroraMode> ModeProperty =
        AvaloniaProperty.Register<AuroraDiagram, AuroraMode>(nameof(Mode), AuroraMode.Shield);

    /// <summary>Solar wind speed, km/s.</summary>
    public static readonly StyledProperty<double> WindSpeedProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(WindSpeed), 400.0);

    /// <summary>Solar wind density, protons per cm³.</summary>
    public static readonly StyledProperty<double> WindDensityProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(WindDensity), 5.0);

    /// <summary>The interplanetary field's north–south part, nT: negative is southward, which opens the shield.</summary>
    public static readonly StyledProperty<double> BzProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(Bz), -5.0);

    /// <summary>Shield: Earth without a magnetic field (the wind reaches the air, as at Mars and Venus).</summary>
    public static readonly StyledProperty<bool> NoFieldProperty =
        AvaloniaProperty.Register<AuroraDiagram, bool>(nameof(NoField), false);

    public static readonly StyledProperty<bool> ShowBeltsProperty =
        AvaloniaProperty.Register<AuroraDiagram, bool>(nameof(ShowBelts), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<AuroraDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Where the followed field line is in the Dungey cycle, 0–1 (pushed back as it runs).</summary>
    public static readonly StyledProperty<double> CyclePhaseProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(CyclePhase), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<AuroraSpecies> SpeciesProperty =
        AvaloniaProperty.Register<AuroraDiagram, AuroraSpecies>(nameof(Species), AuroraSpecies.Electron);

    /// <summary>The trapped particle's shell: where its field line crosses the equator, Earth radii.</summary>
    public static readonly StyledProperty<double> ShellProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(Shell), 4.0);

    /// <summary>The trapped particle's pitch angle at the equator, degrees.</summary>
    public static readonly StyledProperty<double> PitchAngleProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(PitchAngle), 30.0);

    /// <summary>Changed (any new value) to send a fresh particle off.</summary>
    public static readonly StyledProperty<int> RestartCountProperty =
        AvaloniaProperty.Register<AuroraDiagram, int>(nameof(RestartCount), 0);

    public static readonly StyledProperty<bool> ShowRingCurrentProperty =
        AvaloniaProperty.Register<AuroraDiagram, bool>(nameof(ShowRingCurrent), false);

    /// <summary>Geomagnetic activity, 0–9.</summary>
    public static readonly StyledProperty<double> KpProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(Kp), 3.0);

    /// <summary>The energy of the electrons that light the curtain, keV.</summary>
    public static readonly StyledProperty<double> EnergyKeVProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(EnergyKeV), 5.0);

    /// <summary>The viewer's place (the shared location).</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(Latitude), 28.23);

    public static readonly StyledProperty<double> LongitudeProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(Longitude), 112.94);

    /// <summary>The instant (UTC) the night's hours count from: local noon before it.</summary>
    public static readonly StyledProperty<DateTime> SkyBaseProperty =
        AvaloniaProperty.Register<AuroraDiagram, DateTime>(nameof(SkyBase), DateTime.UtcNow);

    /// <summary>Hours since <see cref="SkyBase"/>, 0–24 (pushed back as it runs).</summary>
    public static readonly StyledProperty<double> SkyHourProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(SkyHour), 12.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<AuroraDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<AuroraDiagram>(ModeProperty, WindSpeedProperty, WindDensityProperty, BzProperty, NoFieldProperty, ShowBeltsProperty, ShowLabelsProperty, CyclePhaseProperty, SpeciesProperty, ShellProperty, PitchAngleProperty, RestartCountProperty, ShowRingCurrentProperty, KpProperty, EnergyKeVProperty, LatitudeProperty, LongitudeProperty, SkyBaseProperty, SkyHourProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }


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

    public bool NoField
    {
        get => GetValue(NoFieldProperty);
        set => SetValue(NoFieldProperty, value);
    }

    public bool ShowBelts
    {
        get => GetValue(ShowBeltsProperty);
        set => SetValue(ShowBeltsProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public double CyclePhase
    {
        get => GetValue(CyclePhaseProperty);
        set => SetValue(CyclePhaseProperty, value);
    }

    public AuroraSpecies Species
    {
        get => GetValue(SpeciesProperty);
        set => SetValue(SpeciesProperty, value);
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

    public int RestartCount
    {
        get => GetValue(RestartCountProperty);
        set => SetValue(RestartCountProperty, value);
    }

    public bool ShowRingCurrent
    {
        get => GetValue(ShowRingCurrentProperty);
        set => SetValue(ShowRingCurrentProperty, value);
    }

    public double Kp
    {
        get => GetValue(KpProperty);
        set => SetValue(KpProperty, value);
    }

    public double EnergyKeV
    {
        get => GetValue(EnergyKeVProperty);
        set => SetValue(EnergyKeVProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public double Longitude
    {
        get => GetValue(LongitudeProperty);
        set => SetValue(LongitudeProperty, value);
    }

    public DateTime SkyBase
    {
        get => GetValue(SkyBaseProperty);
        set => SetValue(SkyBaseProperty, value);
    }

    public double SkyHour
    {
        get => GetValue(SkyHourProperty);
        set => SetValue(SkyHourProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double ToolbarHeight
    {
        get => GetValue(ToolbarHeightProperty);
        set => SetValue(ToolbarHeightProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly LabelCache _labels;

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>The Dungey cycle's phase and the night's hour, kept here while they run and pushed back to the page.</summary>
    private double _phase;
    private double _hour = 12;

    public AuroraDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    #region lifecycle

    private bool _attached;

    private void RequestFrame()
    {
        if (_attached)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    private void OnFrame(TimeSpan time)
    {
        if (!_attached)
        {
            return;
        }

        OnRendering();
        RequestFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _clock.Start();
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        _phase = CyclePhase;
        _hour = SkyHour;
        RequestFrame();
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _clock.Stop();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _labels.Clear();

    private void OnModeChanged()
    {
        _dragging = false;
        if (Mode == AuroraMode.Particles) RestartParticle();
    }

    private void OnCyclePhaseChanged(double value)
    {
        if (!_pushing) _phase = ((value % 1) + 1) % 1;
    }

    private void OnSkyHourChanged(double value)
    {
        if (!_pushing) _hour = Math.Clamp(value, 0, 24);
    }

    /// <summary>How long a Dungey cycle takes at 1×: faster wind, quicker cycle.</summary>
    private double CycleDuration => CycleSeconds * Math.Sqrt(400 / Math.Clamp(WindSpeed, 250, 1000));

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        var speed = Math.Clamp(Speed, 0.05, 8);
        var step = IsPlaying && !_dragging ? dt * speed : 0;
        switch (Mode)
        {
            case AuroraMode.Shield:
                // The wind keeps blowing while paused; only the followed line holds still.
                StepWind(dt * Math.Max(speed, 0.5));
                if (step > 0)
                {
                    _phase = (_phase + (step / CycleDuration)) % 1;
                    Push(CyclePhaseProperty, Math.Round(_phase, 4));
                }

                break;

            case AuroraMode.Particles:
                if (step > 0) StepParticle(step);
                _ringTime += dt * Math.Max(speed, 0.25);
                break;

            default:
                if (step > 0)
                {
                    _hour += step * SkyHoursPerSecond;
                    if (_hour >= 24) _hour -= 24;
                    Push(SkyHourProperty, Math.Round(_hour, 4));
                }

                break;
        }

        InvalidateVisual();
    }

    private void Push(AvaloniaProperty property, object value)
    {
        if (Equals(GetValue(property), value)) return;
        _pushing = true;
        try
        {
            SetCurrentValue(property, value);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    /// <summary>The room for the picture: below the title, above the toolbar (and a walkthrough's subtitles).</summary>
    private Rect Stage()
    {
        var top = TitleSpace;
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(0, top, Math.Max(100, ActualWidth), Math.Max(160, ActualHeight - top - bottom));
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        switch (Mode)
        {
            case AuroraMode.Particles:
                DrawParticlesMode(dc);
                break;

            case AuroraMode.Sky:
                DrawSkyMode(dc);
                break;

            default:
                DrawShieldMode(dc);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, _clock.Elapsed.TotalSeconds, width);
    }

    #region drawing helpers

    private PlanetRenderer? _earthSide;
    private PlanetRenderer? _earthTilted;
    private double _earthTilt = double.NaN;

    /// <summary>Earth with its (magnetic) axis straight up the screen, lit from the screen point <paramref name="sun"/>.</summary>
    private void DrawEarthSide(PaintContext dc, Point at, double radius, Point sun)
    {
        _earthSide ??= new PlanetRenderer(Body("Earth"), new LightVector(0, 1, 0));
        DrawSphere(dc, _earthSide, at, radius, LightFrom(at, sun, 0.15), RenderScale());
    }

    /// <summary>Earth with its axis leaning towards the viewer by <paramref name="tiltDegrees"/>, lit from the front left.</summary>
    private void DrawEarthTilted(PaintContext dc, Point at, double radius, double tiltDegrees, double spin)
    {
        var tilt = Math.Round(tiltDegrees, 1);
        if (_earthTilted is null || tilt != _earthTilt)
        {
            var (s, c) = Math.SinCos(tilt * Math.PI / 180);
            _earthTilted = new PlanetRenderer(Body("Earth"), new LightVector(0, c, s));
            _earthTilt = tilt;
        }

        DrawSphere(dc, _earthTilted, at, radius, LightVector.Normalize(-0.55, 0.35, 0.75), RenderScale(), spin);
    }

    /// <summary>Already localized text on a dark pill, centred on <paramref name="center"/> and kept inside the control.</summary>
    private Rect DrawPill(PaintContext dc, string text, Point center, Color color, double size = 11.5)
    {
        var formatted = _labels.Text(text, size, color);
        var x = Math.Clamp(center.X - (formatted.Width / 2) - 6, 2, Math.Max(2, ActualWidth - formatted.Width - 14));
        var y = Math.Clamp(center.Y - (formatted.Height / 2) - 2, 2, Math.Max(2, ActualHeight - formatted.Height - 6));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        return new Rect(x, y, formatted.Width + 12, formatted.Height + 4);
    }

    /// <summary>A pill that is also a target for <paramref name="id"/>.</summary>
    private Rect DrawTargetPill(PaintContext dc, string text, Point center, Color color, string id, double size = 11.5)
    {
        var box = DrawPill(dc, text, center, color, size);
        AddBoxTarget(id, box);
        return box;
    }

    /// <summary>Text with its left edge (or its centre) at a point, its middle at the point's height; where it went.</summary>
    private Rect DrawText(PaintContext dc, string text, Point at, Color color, double size = 11, bool center = false, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var topLeft = new Point(center ? at.X - (formatted.Width / 2) : at.X, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, topLeft);
        return new Rect(topLeft, new Size(formatted.Width, formatted.Height));
    }

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void DrawArrow(PaintContext dc, Point from, Point to, IBrush brush, double thickness = 2, double head = 9)
    {
        Vector v = to - from;
        if (v.Length < 3) return;
        var direction = v / v.Length;
        dc.DrawLine(MakePen(brush, thickness), from, to - (direction * head * 0.6));
        DrawArrowHead(dc, brush, to, direction, head);
    }

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed, bool Flat), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false, bool flat = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed, flat);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        pen = new ImmutablePen(brush, key.Item3, dashed ? new ImmutableDashStyle([4, 4], 0) : null,
            flat ? PenLineCap.Flat : PenLineCap.Round, PenLineJoin.Round);
        _fadedPens[key] = pen;
        return pen;
    }

    /// <summary>A frozen brush of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IBrush FadedBrush(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20));
        if (_fadedBrushes.TryGetValue(key, out var brush)) return brush;
        brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        _fadedBrushes[key] = brush;
        return brush;
    }

    /// <summary>A line through the points, as one figure.</summary>
    private static StreamGeometry Line(IEnumerable<Point> points, bool closed = false, bool filled = false)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, filled);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(closed);
        }
        return geometry;
    }

    private readonly Dictionary<(Color, int), IBrush> _glows = [];

    /// <summary>A soft round glow: a radial gradient of <paramref name="color"/> fading to nothing, made once and kept.</summary>
    private IBrush Glow(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20));
        if (_glows.TryGetValue(key, out var brush)) return brush;
        brush = SpaceDrawing.RadialGradient(
            (WithAlpha(color, key.Item2 / 20.0), 0),
            (WithAlpha(color, key.Item2 / 20.0 * 0.45), 0.4),
            (WithAlpha(color, 0), 1));
        _glows[key] = brush;
        return brush;
    }

    private static Point Mix(Point a, Point b, double t) => new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));

    /// <summary>A point of a cubic Bézier curve.</summary>
    private static Point Bezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        return new Point(
            (u * u * u * p0.X) + (3 * u * u * t * p1.X) + (3 * u * t * t * p2.X) + (t * t * t * p3.X),
            (u * u * u * p0.Y) + (3 * u * u * t * p1.Y) + (3 * u * t * t * p2.Y) + (t * t * t * p3.Y));
    }

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    /// <summary>0 before <paramref name="from"/>, 1 after <paramref name="to"/>, linear between.</summary>
    private static double Ramp(double value, double from, double to) => Math.Clamp((value - from) / (to - from), 0, 1);

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    #endregion

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
        public static readonly Color Field = Color.FromRgb(0x8F, 0xC4, 0xFF);
        public static readonly Color Imf = Color.FromRgb(0xFF, 0xC8, 0x7A);
        public static readonly Color Sun = Color.FromRgb(0xFF, 0xD8, 0x6A);
        public static readonly Color Belt = Color.FromRgb(0xE0, 0x7A, 0xFF);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x10, 0x16, 0x26)), new ImmutableGradientStop(1, Color.FromRgb(0x04, 0x06, 0x0C))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.8, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.8, RelativeUnit.Relative));

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient((Color.FromArgb(200, 0xFF, 0xE0, 0x80), 0), (Color.FromArgb(0, 0xFF, 0xA8, 0x3A), 1));

        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    }
}
