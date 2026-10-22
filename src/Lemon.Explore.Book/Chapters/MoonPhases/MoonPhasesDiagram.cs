using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>
/// The animated diagram of the moon-phases page. Phases mode: Earth and the Moon's orbit seen from above, sunlight from
/// the left, the Moon always half lit (<c>MoonPhasesDiagram.Orbit.cs</c>). Sky mode: the sky facing the equator, with
/// the Sun and the Moon crossing it through the day (<c>MoonPhasesDiagram.Sky.cs</c>). Month mode: Earth moving round
/// the Sun while the Moon goes round Earth, against the direction of a distant star (<c>MoonPhasesDiagram.Month.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the animation clock (an animation frame per screen refresh while it is on screen) and pushes
/// <see cref="Age"/> / <see cref="TimeOfDay"/> back to their bindings, which the rest of the page follows. Dragging on
/// the diagram moves the Moon along its orbit, or the time of day in the sky, and pauses playback.
/// </remarks>
public sealed partial class MoonPhasesDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AgeProperty)
        {
            OnAgeChanged(change.GetNewValue<double>());
        }
        else if (change.Property == TimeOfDayProperty)
        {
            OnTimeOfDayChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Days of the month per second of playback: a month in about 15 seconds.</summary>
    private const double DaysPerSecond = 2;

    /// <summary>Sky mode, through the day: a day in about 16 seconds.</summary>
    private const double HoursPerSecond = 1.5;

    #region dependency properties

    public static readonly StyledProperty<MoonPhasesMode> ModeProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, MoonPhasesMode>(nameof(Mode), MoonPhasesMode.Phases);

    /// <summary>The Moon's age: days since new moon, 0…<see cref="MoonPhaseGeometry.SynodicMonth"/>.</summary>
    public static readonly StyledProperty<double> AgeProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, double>(nameof(Age), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sky mode: local solar time in hours, 0…24 (12 = noon).</summary>
    public static readonly StyledProperty<double> TimeOfDayProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, double>(nameof(TimeOfDay), 20.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sky mode: playback runs through the month at a fixed time of day instead of through one day.</summary>
    public static readonly StyledProperty<bool> AnimatesMonthProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, bool>(nameof(AnimatesMonth), false);

    /// <summary>The observer is in the southern hemisphere: the sky faces north and the Moon looks upside down.</summary>
    public static readonly StyledProperty<bool> IsSouthernProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, bool>(nameof(IsSouthern), false);

    /// <summary>Sky mode: the observer's latitude, −90…90 (north positive).</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, double>(nameof(Latitude), 40.0);

    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (orbit, halves, path, angle).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, ICommand?>(nameof(InteractionCommand));

    public string? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public string? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<MoonPhasesDiagram, bool>(nameof(IsLessonActive), false);

    public bool IsLessonActive
    {
        get => GetValue(IsLessonActiveProperty);
        set => SetValue(IsLessonActiveProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    public MoonPhasesMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Age
    {
        get => GetValue(AgeProperty);
        set => SetValue(AgeProperty, value);
    }

    public double TimeOfDay
    {
        get => GetValue(TimeOfDayProperty);
        set => SetValue(TimeOfDayProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public bool AnimatesMonth
    {
        get => GetValue(AnimatesMonthProperty);
        set => SetValue(AnimatesMonthProperty, value);
    }

    public bool IsSouthern
    {
        get => GetValue(IsSouthernProperty);
        set => SetValue(IsSouthernProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly PlanetRenderer _sun = new(SpaceDrawing.Body("Sun"));
    private readonly CelestialBody _sunBody = SpaceDrawing.Body("Sun");
    private readonly PlanetRenderer _earthTop = MoonDrawing.TopRenderer("Earth");
    private readonly PlanetRenderer _moonTop = MoonDrawing.TopRenderer("Moon");
    private PlanetRenderer? _moonDisc;
    private bool _moonDiscSouthern;

    private double _lastSeconds;
    private bool _pushing;
    private double _age;
    private double _time = 20;
    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();
    private string? _pressed;
    private Point _pressPoint;
    private bool _dragging;
    private double _dragStartX;
    private double _dragStartTime;
    private double _dragStartAge;

    public MoonPhasesDiagram()
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

    private void OnAgeChanged(double value)
    {
        if (!_pushing) _age = value;
    }

    private void OnTimeOfDayChanged(double value)
    {
        if (!_pushing) _time = value;
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

        if (IsPlaying && !_dragging && dt > 0)
        {
            if (Mode == MoonPhasesMode.Sky && !AnimatesMonth)
            {
                PushTime(_time + (dt * HoursPerSecond));
            }
            else
            {
                PushAge(_age + (dt * DaysPerSecond));
            }
        }

        InvalidateVisual();
    }

    private void PushAge(double age)
    {
        _age = MoonPhaseGeometry.Wrap(age);
        _pushing = true;
        try
        {
            SetCurrentValue(AgeProperty, _age);
        }
        finally
        {
            _pushing = false;
        }
    }

    private void PushTime(double hours)
    {
        _time = MoonPhaseGeometry.WrapHours(hours);
        _pushing = true;
        try
        {
            SetCurrentValue(TimeOfDayProperty, _time);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    #region dragging and clicking

    /// <summary>
    /// Pressing on a thing (the Moon, a phase in the outer ring, the distant star…) waits to see whether it is a click,
    /// which explains it, or the start of a drag; pressing anywhere else starts dragging at once.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);

        var point = e.GetPosition(this);
        _pressPoint = point;
        _pressed = _targets.Find(point)?.Id;
        if (_pressed is null) BeginDrag(point);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null && ((Vector)(point - _pressPoint)).Length > 4)
        {
            _pressed = null;
            BeginDrag(_pressPoint);
        }

        if (_dragging) DragTo(point);
        else if (_pressed is null) _targets.Hover = _targets.Find(point)?.Id;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);
        if (picked is not null) Pick(picked);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _targets.Hover = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        _pressed = null;
    }

    private void BeginDrag(Point point)
    {
        Interacted();
        _dragging = true;
        _dragStartX = point.X;
        _dragStartTime = _time;
        _dragStartAge = _age;
        SetCurrentValue(IsPlayingProperty, false);
        DragTo(point);
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: go to its moment where it has one, and show (and say) what it is.</summary>
    private void Pick(string id)
    {
        Interacted();
        if (PhaseOfTarget(id) is { } phase) GoToAge(MoonPhaseGeometry.AgeOf(phase));
        switch (id)
        {
            case "start": GoToAge(0); break;
            case "sidereal": GoToAge(MoonPhaseGeometry.SiderealMonth); break;
            case "synodic": GoToAge(MoonPhaseGeometry.SynodicMonth - 0.01); break;
            case "moonrise": GoToTime(MoonPhaseGeometry.MoonriseHour(_age)); break;
            case "moonset": GoToTime(MoonPhaseGeometry.MoonsetHour(_age)); break;
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    private void GoToAge(double age)
    {
        SetCurrentValue(IsPlayingProperty, false);
        PushAge(age);
    }

    private void GoToTime(double hours)
    {
        SetCurrentValue(IsPlayingProperty, false);
        PushTime(hours);
    }

    /// <summary>The phase of an outer-ring target such as <c>ring-FirstQuarter</c>.</summary>
    private static MoonPhase? PhaseOfTarget(string? id) =>
        id is not null && id.StartsWith("ring-", StringComparison.Ordinal) && Enum.TryParse<MoonPhase>(id[5..], out var phase) ? phase : null;

    /// <summary>
    /// The thing a target id stands for, which names its resource keys (<see cref="ObjectTitleKey"/>,
    /// <see cref="ObjectClipKey"/>). A phase in the outer ring is the phase's own name; moonrise and moonset share one.
    /// </summary>
    internal static string? ObjectName(string? id) => PhaseOfTarget(id)?.ToString() ?? id switch
    {
        "earth" => "Earth",
        "moon" => "Moon",
        "sunlight" => "Sunlight",
        "skymoon" => "SkyMoon",
        "skysun" => "SkySun",
        "moonrise" or "moonset" => "Moonrise",
        "sun" => "Sun",
        "month-earth" => "EarthOrbit",
        "month-moon" => "MoonOrbit",
        "star" => "Star",
        "start" => "Start",
        "sidereal" => "Sidereal",
        "synodic" => "Synodic",
        _ => null,
    };

    /// <summary>The resource key of a target's title: a phase's own name, or <c>MoonPhases_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id) =>
        PhaseOfTarget(id) is { } phase ? MoonDrawing.PhaseKey(phase) : ObjectName(id) is { } name ? "MoonPhases_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_MoonPhases_Obj_" + name : null;

    private static bool IsLineHighlight(string id) => id is "orbit" or "halves" or "path" or "angle";

    private void DragTo(Point point)
    {
        var width = Math.Max(ActualWidth, 1);
        switch (Mode)
        {
            case MoonPhasesMode.Phases:
                if (AgeAt(point) is { } age) PushAge(age);
                break;

            case MoonPhasesMode.Sky:
                // Across the whole width is a whole day. Facing south the sky turns from left to right, facing north
                // from right to left; either way the Sun and the Moon follow the pointer.
                var direction = IsSouthern ? -1 : 1;
                PushTime(_dragStartTime + (direction * (point.X - _dragStartX) / width * 24));
                break;

            default:
                // Across the whole width is a month.
                var dragged = _dragStartAge + ((point.X - _dragStartX) / width * MoonPhaseGeometry.SynodicMonth);
                PushAge(Math.Clamp(dragged, 0, MoonPhaseGeometry.SynodicMonth - 1e-6));
                break;
        }
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
        _targets.Clear();

        switch (Mode)
        {
            case MoonPhasesMode.Sky:
                DrawSky(dc, dpi, now);
                break;

            case MoonPhasesMode.Month:
                _stars.Draw(dc, width, height, dpi, now, 0.8);
                DrawMonth(dc, dpi, now);
                break;

            default:
                _stars.Draw(dc, width, height, dpi, now, 0.8);
                DrawOrbit(dc, dpi, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    /// <summary>The Moon as seen from Earth, made again when the hemisphere changes.</summary>
    private PlanetRenderer MoonDisc()
    {
        if (_moonDisc is null || _moonDiscSouthern != IsSouthern)
        {
            _moonDisc = MoonDrawing.DiscRenderer(IsSouthern);
            _moonDiscSouthern = IsSouthern;
        }

        return _moonDisc;
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);
}
