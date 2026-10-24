using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// The animated diagram of the seasons chapter. Tilt and distance mode: Earth's year round the Sun in perspective, its
/// tilted axis always pointing the same way (<c>SeasonsDiagram.Orbit.cs</c>). Sun-path mode: the sky at one place,
/// with the Sun's path across it and a pole's shadow (<c>SeasonsDiagram.Sky.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the animation clock (an animation frame per screen refresh while it is on screen) and pushes
/// <see cref="Day"/> / <see cref="TimeOfDay"/> back to their bindings, which the rest of the chapter follows. Dragging
/// on the diagram moves Earth along its orbit, or the Sun along its path, and pauses playback.
/// </remarks>
public sealed partial class SeasonsDiagram : Control
{
    /// <summary>Days of the year per second of playback.</summary>
    private const double DaysPerSecond = 12;

    #region properties

    public static readonly StyledProperty<SeasonsMode> ModeProperty =
        AvaloniaProperty.Register<SeasonsDiagram, SeasonsMode>(nameof(Mode));

    /// <summary>Day of the year (see <see cref="SeasonsGeometry"/>).</summary>
    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<SeasonsDiagram, double>(nameof(Day), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Local solar time in hours, 0…24 (12 = noon).</summary>
    public static readonly StyledProperty<double> TimeOfDayProperty =
        AvaloniaProperty.Register<SeasonsDiagram, double>(nameof(TimeOfDay), 12, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<SeasonsDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sun-path mode: playback runs through the year (at a fixed time of day) instead of through one day.</summary>
    public static readonly StyledProperty<bool> AnimatesYearProperty =
        AvaloniaProperty.Register<SeasonsDiagram, bool>(nameof(AnimatesYear));

    /// <summary>The observer's latitude, −90…90 (north positive).</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<SeasonsDiagram, double>(nameof(Latitude), 40);

    /// <summary>Earth's axial tilt in degrees, 0…90.</summary>
    public static readonly StyledProperty<double> TiltProperty =
        AvaloniaProperty.Register<SeasonsDiagram, double>(nameof(Tilt), SeasonsGeometry.EarthTilt);

    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (orbit, axis, paths, todaypath).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<SeasonsDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the chapter shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<SeasonsDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<SeasonsDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<SeasonsDiagram, bool>(nameof(IsLessonActive));

    public SeasonsMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

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

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
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

    public bool AnimatesYear
    {
        get => GetValue(AnimatesYearProperty);
        set => SetValue(AnimatesYearProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public double Tilt
    {
        get => GetValue(TiltProperty);
        set => SetValue(TiltProperty, value);
    }

    #endregion

    private readonly BookStopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;

    private double _lastSeconds;
    private bool _pushing;
    private bool _attached;
    private double _day;
    private double _time = 12;
    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();
    private bool _dragging;
    private double _dragStartX;
    private double _dragStartTime;
    private string? _pressed;
    private Point _pressPoint;

    public SeasonsDiagram()
    {
        _labels = new LabelCache(this);
        _sun = new PlanetRenderer(SpaceBodies.Sun);
        ClipToBounds = true;
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    #region lifecycle

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _clock.Start();
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _clock.Stop();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _labels.Clear();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DayProperty && !_pushing)
        {
            _day = change.GetNewValue<double>();
        }
        else if (change.Property == TimeOfDayProperty && !_pushing)
        {
            _time = change.GetNewValue<double>();
        }
        else if (change.Property == BoundsProperty)
        {
            _stars.OnSizeChanged(_clock.Elapsed.TotalSeconds);
        }
    }

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

        if (IsEffectivelyVisible && Bounds.Width >= 1 && Bounds.Height >= 1)
        {
            Tick();
        }

        RequestFrame();
    }

    private void Tick()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.1);
        _lastSeconds = now;
        _reserve.Step(IsLessonActive, dt);

        if (IsPlaying && !_dragging && dt > 0)
        {
            if (Mode == SeasonsMode.SunPath && !AnimatesYear)
            {
                PushTime(_time + (dt * HoursPerSecond()));
            }
            else
            {
                PushDay(_day + (dt * DaysPerSecond));
            }
        }

        InvalidateVisual();
    }

    /// <summary>A day passes in about 12 seconds; the night goes by faster.</summary>
    private double HoursPerSecond()
    {
        var declination = SeasonsGeometry.Declination(_day, Tilt);
        var altitude = SeasonsGeometry.Altitude(Latitude, declination, SeasonsGeometry.HourAngle(_time));
        return altitude > 0 ? 1.6 : 1.6 + (2.4 * Math.Clamp(-altitude / 8, 0, 1));
    }

    private void PushDay(double day)
    {
        _day = SeasonsGeometry.Wrap(day);
        _pushing = true;
        try
        {
            SetCurrentValue(DayProperty, _day);
        }
        finally
        {
            _pushing = false;
        }
    }

    private void PushTime(double hours)
    {
        _time = hours - (24 * Math.Floor(hours / 24));
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
    /// Pressing on a thing (the Sun, a solstice, the pole…) waits to see whether it is a click, which explains it, or
    /// the start of a drag; pressing anywhere else starts dragging at once.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(this);
        var point = e.GetPosition(this);
        _pressPoint = point;
        _pressed = _targets.Find(point)?.Id;
        if (_pressed is null)
        {
            BeginDrag(point);
        }

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

        if (_dragging)
        {
            DragTo(point);
        }
        else if (_pressed is null)
        {
            _targets.Hover = _targets.Find(point)?.Id;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);
        if (picked is not null)
        {
            Pick(picked);
        }
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
        SetCurrentValue(IsPlayingProperty, false);
        DragTo(point);
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <summary>A click on a thing: go to its day or time where it has one, and show (and say) what it is.</summary>
    private void Pick(string id)
    {
        Interacted();
        var latitude = Math.Clamp(Latitude, -90, 90);
        var declination = SeasonsGeometry.Declination(_day, Math.Clamp(Tilt, 0, 90));
        var h0 = SeasonsGeometry.SunsetHourAngle(latitude, declination);
        switch (id)
        {
            case "march": GoToDay(SeasonsGeometry.EventDay(SeasonEvent.MarchEquinox)); break;
            case "june": GoToDay(SeasonsGeometry.EventDay(SeasonEvent.JuneSolstice)); break;
            case "september": GoToDay(SeasonsGeometry.EventDay(SeasonEvent.SeptemberEquinox)); break;
            case "december": GoToDay(SeasonsGeometry.EventDay(SeasonEvent.DecemberSolstice)); break;
            case "perihelion": GoToDay(SeasonsGeometry.PerihelionDay()); break;
            case "aphelion": GoToDay(SeasonsGeometry.AphelionDay()); break;
            case "noon": GoToTime(12); break;
            case "sunrise": GoToTime(12 - (h0 / 15)); break;
            case "sunset": GoToTime(12 + (h0 / 15)); break;
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    private void GoToDay(double day)
    {
        SetCurrentValue(IsPlayingProperty, false);
        PushDay(day);
    }

    private void GoToTime(double hours)
    {
        SetCurrentValue(IsPlayingProperty, false);
        PushTime(hours);
    }

    /// <summary>Every id the diagram can show as a clickable thing (for the coverage test).</summary>
    internal static IReadOnlyList<string> TargetIds { get; } =
        ["sun", "earth", "axis", "march", "september", "june", "december", "perihelion", "aphelion", "skysun", "noon", "pole", "sunrise", "sunset"];

    /// <summary>
    /// The thing a target id stands for, which names its resource keys: <c>Seasons_Obj_&lt;name&gt;</c> for its title and
    /// <c>Narr_Seasons_Obj_&lt;name&gt;</c> for its explanation. Both equinoxes share one, as do sunrise and sunset.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "sun" => "Sun",
        "earth" => "Earth",
        "axis" => "Axis",
        "march" or "september" => "Equinox",
        "june" => "June",
        "december" => "December",
        "perihelion" => "Perihelion",
        "aphelion" => "Aphelion",
        "skysun" => "SkySun",
        "noon" => "Noon",
        "pole" => "Shadow",
        "sunrise" or "sunset" => "Sunrise",
        _ => null,
    };

    /// <summary>The resource key of a target's title, or null.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Seasons_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Seasons_Obj_" + name : null;

    private static bool IsLineHighlight(string id) => id is "orbit" or "axis" or "paths" or "todaypath";

    private void DragTo(Point point)
    {
        if (Mode == SeasonsMode.SunPath)
        {
            // Across the whole width is a whole day; the Sun moves east to west as the pointer moves right.
            var width = Math.Max(Bounds.Width, 1);
            PushTime(_dragStartTime + ((point.X - _dragStartX) / width * 24));
        }
        else
        {
            PushDay(DayAt(point));
        }
    }

    #endregion

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    public override void Render(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1)
        {
            return;
        }

        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var now = _clock.Elapsed.TotalSeconds;
        dc.FillRectangle(Brushes.Black, new Rect(0, 0, width, height));
        _targets.Clear();

        if (Mode == SeasonsMode.SunPath)
        {
            DrawSky(dc, scale, now);
        }
        else
        {
            _stars.Draw(dc, width, height, scale, now, 0.8);
            DrawOrbit(dc, scale, now);
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, id => ObjectTitleKey(id), IsLineHighlight, now, width);
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    internal static string SeasonKey(Season season) => season switch
    {
        Season.Spring => "Seasons_Season_Spring",
        Season.Summer => "Seasons_Season_Summer",
        Season.Autumn => "Seasons_Season_Autumn",
        _ => "Seasons_Season_Winter",
    };

    internal static string EventKey(SeasonEvent e) => e switch
    {
        SeasonEvent.MarchEquinox => "Seasons_Event_MarchEquinox",
        SeasonEvent.JuneSolstice => "Seasons_Event_JuneSolstice",
        SeasonEvent.SeptemberEquinox => "Seasons_Event_SeptemberEquinox",
        _ => "Seasons_Event_DecemberSolstice",
    };

    /// <summary>A month and day in the current language, e.g. "June 21" or "6月21日".</summary>
    internal static string DateText(double day) =>
        SeasonsGeometry.DateOf(day).ToString("M", LocalizationService.Instance.CurrentCulture);
}
