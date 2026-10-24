using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>
/// The animated diagram of the Kepler's laws page. Ellipse mode: an orbit with the Sun at one focus, the other focus
/// and the string of the gardener's construction (<c>KeplerDiagram.Ellipse.cs</c>). Areas mode: the same orbit cut into
/// sectors of equal time, filling in as the planet goes round (<c>KeplerDiagram.Areas.cs</c>). Periods mode: the
/// planets at their real places today and a planet of your own whose distance you drag (<c>KeplerDiagram.Periods.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the animation clock (an animation frame per screen refresh while it is on screen) and pushes
/// <see cref="Phase"/>, <see cref="Years"/> and <see cref="YourAxis"/> back to their bindings, which the rest of the
/// page follows. Dragging puts the planet where the mouse is along its orbit (ellipse and areas) or moves your planet
/// nearer or farther (periods); either pauses playback.
/// </remarks>
public sealed partial class KeplerDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PhaseProperty)
        {
            OnPhaseChanged(change.GetNewValue<double>());
        }
        else if (change.Property == YearsProperty)
        {
            OnYearsChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Ellipse and areas: orbits per second of playback, one round in ten seconds whatever the body.</summary>
    private const double OrbitsPerSecond = 0.1;

    #region dependency properties

    public static readonly StyledProperty<KeplerMode> ModeProperty =
        AvaloniaProperty.Register<KeplerDiagram, KeplerMode>(nameof(Mode), KeplerMode.Ellipse);

    /// <summary>Ellipse and areas: the fraction of the period since perihelion, 0…1.</summary>
    public static readonly StyledProperty<double> PhaseProperty =
        AvaloniaProperty.Register<KeplerDiagram, double>(nameof(Phase), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Periods: Earth years since <see cref="StartUtc"/>.</summary>
    public static readonly StyledProperty<double> YearsProperty =
        AvaloniaProperty.Register<KeplerDiagram, double>(nameof(Years), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Periods: the moment the planets are shown at for <see cref="Years"/> = 0.</summary>
    public static readonly StyledProperty<DateTime> StartUtcProperty =
        AvaloniaProperty.Register<KeplerDiagram, DateTime>(nameof(StartUtc), DateTime.UtcNow);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<KeplerDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Ellipse and areas: the shape of the orbit, 0 (a circle) … <see cref="KeplerGeometry.MaxEccentricity"/>.</summary>
    public static readonly StyledProperty<double> EccentricityProperty =
        AvaloniaProperty.Register<KeplerDiagram, double>(nameof(Eccentricity), 0.5);

    /// <summary>Ellipse and areas: which body goes round (<see cref="KeplerBody.Id"/>), for its look and its distances.</summary>
    public static readonly StyledProperty<string> BodyIdProperty =
        AvaloniaProperty.Register<KeplerDiagram, string>(nameof(BodyId), "Custom");

    /// <summary>Areas: how many stretches of equal time the orbit is cut into.</summary>
    public static readonly StyledProperty<int> SectorsProperty =
        AvaloniaProperty.Register<KeplerDiagram, int>(nameof(Sectors), 12);

    /// <summary>Ellipse: the string from the planet to both foci.</summary>
    public static readonly StyledProperty<bool> ShowStringProperty =
        AvaloniaProperty.Register<KeplerDiagram, bool>(nameof(ShowString), true);

    /// <summary>Ellipse: a circle of the same size, to compare.</summary>
    public static readonly StyledProperty<bool> ShowCircleProperty =
        AvaloniaProperty.Register<KeplerDiagram, bool>(nameof(ShowCircle), false);

    /// <summary>Areas: the arrow of the planet's velocity.</summary>
    public static readonly StyledProperty<bool> ShowVelocityProperty =
        AvaloniaProperty.Register<KeplerDiagram, bool>(nameof(ShowVelocity), true);

    /// <summary>Periods: your planet's distance from the Sun, AU (<see cref="MinYourAxis"/>…<see cref="MaxYourAxis"/>).</summary>
    public static readonly StyledProperty<double> YourAxisProperty =
        AvaloniaProperty.Register<KeplerDiagram, double>(nameof(YourAxis), 2.5, defaultBindingMode: BindingMode.TwoWay);

    public const double MinYourAxis = 0.2;
    public const double MaxYourAxis = 10;

    public KeplerMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Phase
    {
        get => GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public double Years
    {
        get => GetValue(YearsProperty);
        set => SetValue(YearsProperty, value);
    }

    public DateTime StartUtc
    {
        get => GetValue(StartUtcProperty);
        set => SetValue(StartUtcProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double Eccentricity
    {
        get => GetValue(EccentricityProperty);
        set => SetValue(EccentricityProperty, value);
    }

    public string BodyId
    {
        get => GetValue(BodyIdProperty);
        set => SetValue(BodyIdProperty, value);
    }

    public int Sectors
    {
        get => GetValue(SectorsProperty);
        set => SetValue(SectorsProperty, value);
    }

    public bool ShowString
    {
        get => GetValue(ShowStringProperty);
        set => SetValue(ShowStringProperty, value);
    }

    public bool ShowCircle
    {
        get => GetValue(ShowCircleProperty);
        set => SetValue(ShowCircleProperty, value);
    }

    public bool ShowVelocity
    {
        get => GetValue(ShowVelocityProperty);
        set => SetValue(ShowVelocityProperty, value);
    }

    public double YourAxis
    {
        get => GetValue(YourAxisProperty);
        set => SetValue(YourAxisProperty, value);
    }

    #endregion

    private readonly BookStopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly PlanetRenderer _sun = new(SpaceDrawing.Body("Sun"));
    private readonly Dictionary<string, PlanetRenderer> _renderers = [];

    private double _lastSeconds;
    private bool _pushing;
    private double _phase;
    private double _years;
    private bool _dragging;

    /// <summary>The eccentricity drawn, easing towards <see cref="Eccentricity"/> so a new shape morphs into place.</summary>
    private double _shownE = double.NaN;

    public KeplerDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
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

    private void OnPhaseChanged(double value)
    {
        if (!_pushing) _phase = value;
    }

    private void OnYearsChanged(double value)
    {
        if (!_pushing) _years = value;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _stars.OnSizeChanged(_clock.Elapsed.TotalSeconds);
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.1);
        _lastSeconds = now;
        _reserve.Step(IsLessonActive, dt);

        // Ease the drawn shape and the zoom of the periods view towards their targets.
        var ease = 1 - Math.Exp(-dt * 7);
        var target = Math.Clamp(Eccentricity, 0, KeplerGeometry.MaxEccentricity);
        _shownE = double.IsNaN(_shownE) ? target : _shownE + ((target - _shownE) * ease);
        if (Math.Abs(_shownE - target) < 1e-4) _shownE = target;
        if (!_dragging) _viewRadius = double.IsNaN(_viewRadius) ? TargetViewRadius : _viewRadius + ((TargetViewRadius - _viewRadius) * ease);

        if (IsPlaying && !_dragging && dt > 0)
        {
            if (Mode == KeplerMode.Periods) PushYears(_years + (dt * YearsPerSecond));
            else PushPhase(_phase + (dt * OrbitsPerSecond));
        }

        InvalidateVisual();
    }

    private void PushPhase(double phase)
    {
        _phase = KeplerGeometry.Wrap(phase);
        _pushing = true;
        try
        {
            SetCurrentValue(PhaseProperty, _phase);
        }
        finally
        {
            _pushing = false;
        }
    }

    private void PushYears(double years)
    {
        _years = years >= MaxYears ? years - MaxYears : Math.Max(0, years);
        _pushing = true;
        try
        {
            SetCurrentValue(YearsProperty, _years);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    #region dragging

    private void DragTo(Point point)
    {
        if (Mode == KeplerMode.Periods)
        {
            // The zoom holds still while dragging, so the planet stays under the mouse.
            var distance = ((Vector)(point - _sunScreen)).Length / _periodScale;
            SetCurrentValue(YourAxisProperty, Math.Round(Math.Clamp(distance, MinYourAxis, MaxYourAxis), 2));
            return;
        }

        // The planet goes to the point of the orbit in the direction of the mouse as seen from the Sun.
        Vector v = point - _sunScreen;
        if (v.Length < 2) return;
        var trueAnomaly = Math.Atan2(-v.Y, v.X);
        PushPhase(KeplerGeometry.PhaseAt(trueAnomaly, ShownEccentricity));
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        var dpi = RenderScale();
        var now = _clock.Elapsed.TotalSeconds;
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, dpi, now, 0.8);
        _targets.Clear();

        switch (Mode)
        {
            case KeplerMode.Periods:
                DrawPeriods(dc, dpi, now);
                break;

            case KeplerMode.Areas:
                DrawAreas(dc, dpi, now);
                break;

            default:
                DrawEllipse(dc, dpi, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    private double ShownEccentricity => double.IsNaN(_shownE) ? Math.Clamp(Eccentricity, 0, KeplerGeometry.MaxEccentricity) : _shownE;

    /// <summary>A ray-traced body, one renderer per catalogue id.</summary>
    private PlanetRenderer Renderer(string id)
    {
        if (!_renderers.TryGetValue(id, out var renderer))
        {
            renderer = new PlanetRenderer(SpaceDrawing.Body(id));
            _renderers[id] = renderer;
        }

        return renderer;
    }

    /// <summary>The Sun with its glow.</summary>
    private void DrawSun(PaintContext dc, double dpi, Point at, double radius, double now)
    {
        var glow = radius * 3.2;
        dc.DrawEllipse(KeplerDrawing.SunGlow, null, at, glow, glow);
        SpaceDrawing.DrawSphere(dc, _sun, at, radius, new LightVector(0, 0, 1), dpi, now * 0.05);
    }

    private void DrawCenteredPill(PaintContext dc, string text, Point center, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        _labels.DrawTextPill(dc, text, new Point(center.X - (formatted.Width / 2) - 6, center.Y - (formatted.Height / 2) - 2), size, color);
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);
}
