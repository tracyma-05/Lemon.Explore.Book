using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>
/// The animated diagram of the day-and-night page. Map and time-zone mode: the flat world map with the night side
/// and the line between day and night sweeping west (<c>DayNightDiagram.Map.cs</c>). Globe mode: Earth lit from the
/// left, where that line is a great circle (<c>DayNightDiagram.Globe.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the animation clock (an animation frame per screen refresh while it is on screen) and pushes
/// <see cref="Day"/> / <see cref="UtcHours"/> back to their bindings, which the rest of the page follows. On the map,
/// a click picks a place and dragging sideways moves the Sun; on the globe, dragging turns the view.
/// </remarks>
public sealed partial class DayNightDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DayProperty)
        {
            OnDayChanged(change.GetNewValue<double>());
        }
        else if (change.Property == UtcHoursProperty)
        {
            OnUtcHoursChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Hours of the day per second of playback: a day in 16 seconds.</summary>
    private const double HoursPerSecond = 1.5;

    /// <summary>Days per second when playback runs through the year.</summary>
    private const double DaysPerSecond = 10;

    /// <summary>Pointer travel (in pixels) before a press counts as a drag rather than a click.</summary>
    private const double DragThreshold = 4;

    #region dependency properties

    public static readonly StyledProperty<DayNightMode> ModeProperty =
        AvaloniaProperty.Register<DayNightDiagram, DayNightMode>(nameof(Mode), DayNightMode.Map);

    public static readonly StyledProperty<int> YearProperty =
        AvaloniaProperty.Register<DayNightDiagram, int>(nameof(Year), 2026);

    /// <summary>Day of the year in UTC, 0 for 1 January; only its whole part counts.</summary>
    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<DayNightDiagram, double>(nameof(Day), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Time of day in UTC, 0…24.</summary>
    public static readonly StyledProperty<double> UtcHoursProperty =
        AvaloniaProperty.Register<DayNightDiagram, double>(nameof(UtcHours), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<DayNightDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Playback runs through the year at a fixed time of day instead of through the day.</summary>
    public static readonly StyledProperty<bool> AnimatesYearProperty =
        AvaloniaProperty.Register<DayNightDiagram, bool>(nameof(AnimatesYear), false);

    public static readonly StyledProperty<double> PlaceLatitudeProperty =
        AvaloniaProperty.Register<DayNightDiagram, double>(nameof(PlaceLatitude), 39.9, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> PlaceLongitudeProperty =
        AvaloniaProperty.Register<DayNightDiagram, double>(nameof(PlaceLongitude), 116.4, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The chosen city's id (<see cref="WorldCities"/>), or empty for a point picked on the map.</summary>
    public static readonly StyledProperty<string> PlaceCityProperty =
        AvaloniaProperty.Register<DayNightDiagram, string>(nameof(PlaceCity), "Beijing", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Map mode: also draw where civil, nautical and astronomical twilight end.</summary>
    public static readonly StyledProperty<bool> ShowTwilightProperty =
        AvaloniaProperty.Register<DayNightDiagram, bool>(nameof(ShowTwilight), true);

    /// <summary>Globe mode: how far the view is turned towards the Sun, −80…80°.</summary>
    public static readonly StyledProperty<double> ViewTowardSunProperty =
        AvaloniaProperty.Register<DayNightDiagram, double>(nameof(ViewTowardSun), 20.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Globe mode: how far above the equator the view is, −70…70°.</summary>
    public static readonly StyledProperty<double> ViewElevationProperty =
        AvaloniaProperty.Register<DayNightDiagram, double>(nameof(ViewElevation), 15.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (terminator, noonline, dateline, newday).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<DayNightDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<DayNightDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<DayNightDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<DayNightDiagram, bool>(nameof(IsLessonActive), false);

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

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    public bool IsLessonActive
    {
        get => GetValue(IsLessonActiveProperty);
        set => SetValue(IsLessonActiveProperty, value);
    }

    public DayNightMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public int Year
    {
        get => GetValue(YearProperty);
        set => SetValue(YearProperty, value);
    }

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    public double UtcHours
    {
        get => GetValue(UtcHoursProperty);
        set => SetValue(UtcHoursProperty, value);
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

    public double PlaceLatitude
    {
        get => GetValue(PlaceLatitudeProperty);
        set => SetValue(PlaceLatitudeProperty, value);
    }

    public double PlaceLongitude
    {
        get => GetValue(PlaceLongitudeProperty);
        set => SetValue(PlaceLongitudeProperty, value);
    }

    public string PlaceCity
    {
        get => GetValue(PlaceCityProperty);
        set => SetValue(PlaceCityProperty, value);
    }

    public bool ShowTwilight
    {
        get => GetValue(ShowTwilightProperty);
        set => SetValue(ShowTwilightProperty, value);
    }

    public double ViewTowardSun
    {
        get => GetValue(ViewTowardSunProperty);
        set => SetValue(ViewTowardSunProperty, value);
    }

    public double ViewElevation
    {
        get => GetValue(ViewElevationProperty);
        set => SetValue(ViewElevationProperty, value);
    }

    #endregion

    private readonly BookStopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly MapPainter _map = new();
    private readonly GlobeRenderer _globe = new();
    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();

    private double _lastSeconds;
    private bool _pushing;
    private double _day;
    private double _hours;

    private bool _pressed;
    private bool _dragging;
    private Point _pressPoint;
    private DateTime _pressInstant;
    private double _pressTowardSun;
    private double _pressElevation;

    public DayNightDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        WorldMap.WarmUp();
    }

    /// <summary>The instant shown.</summary>
    private DateTime Utc => StartOfYear(Year).AddDays(Math.Floor(_day)).AddHours(_hours);

    internal static DateTime StartOfYear(int year) => new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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

    private void OnDayChanged(double value)
    {
        if (!_pushing) _day = value;
    }

    private void OnUtcHoursChanged(double value)
    {
        if (!_pushing) _hours = value;
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
            if (AnimatesYear)
            {
                Push(_day + (dt * DaysPerSecond), _hours);
            }
            else
            {
                Push(_day, _hours + (dt * HoursPerSecond));
            }
        }

        InvalidateVisual();
    }

    private void Push(DateTime utc)
    {
        var start = StartOfYear(Year);
        var span = utc - start;
        Push(Math.Floor(span.TotalDays), (span.TotalDays - Math.Floor(span.TotalDays)) * 24);
    }

    /// <summary>Moves to <paramref name="day"/> and <paramref name="hours"/>, carrying whole days and wrapping within the year.</summary>
    private void Push(double day, double hours)
    {
        var carry = Math.Floor(hours / 24);
        hours -= 24 * carry;
        var days = DateTime.IsLeapYear(Year) ? 366 : 365;
        day += carry;
        day -= days * Math.Floor(day / days);

        _day = day;
        _hours = hours;
        _pushing = true;
        try
        {
            SetCurrentValue(DayProperty, _day);
            SetCurrentValue(UtcHoursProperty, _hours);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    #region pointer

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);

        _pressed = true;
        _dragging = false;
        _pressPoint = e.GetPosition(this);
        _pressInstant = Utc;
        _pressTowardSun = ViewTowardSun;
        _pressElevation = ViewElevation;
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (!_pressed)
        {
            _targets.Hover = _targets.Find(point)?.Id;
            return;
        }

        Vector moved = point - _pressPoint;
        if (!_dragging && moved.Length < DragThreshold) return;

        if (!_dragging)
        {
            _dragging = true;
            Interacted();
            if (Mode != DayNightMode.Globe) SetCurrentValue(IsPlayingProperty, false);
        }

        if (Mode == DayNightMode.Globe)
        {
            SetCurrentValue(ViewTowardSunProperty, Math.Clamp(_pressTowardSun + (moved.X * 0.4), -80, 80));
            SetCurrentValue(ViewElevationProperty, Math.Clamp(_pressElevation + (moved.Y * 0.4), -70, 70));
        }
        else
        {
            // The Sun follows the pointer: right is east, where it was earlier in the day.
            var map = MapRect();
            Push(_pressInstant.AddHours(-moved.X / Math.Max(map.Width, 1) * 24));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed) return;

        if (!_dragging) Pick(e.GetPosition(this));
        _pressed = false;
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        _dragging = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _targets.Hover = null;
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A thing to explain under the pointer (a line's name, the Sun, the date line…), or else a city or a place to choose.</summary>
    private void Pick(Point point)
    {
        Interacted();
        if (_targets.Find(point)?.Id is { } id)
        {
            // Cleared first so clicking the same thing again says it again.
            SetCurrentValue(SelectedObjectProperty, null);
            SetCurrentValue(SelectedObjectProperty, id);
            return;
        }

        var place = Mode == DayNightMode.Globe ? GlobePointAt(point) : MapPointAt(point);
        if (place is not { } picked) return;

        var city = NearestCity(point);
        if (city is not null)
        {
            SetCurrentValue(PlaceCityProperty, city.Id);
            SetCurrentValue(PlaceLatitudeProperty, city.Latitude);
            SetCurrentValue(PlaceLongitudeProperty, city.Longitude);
        }
        else
        {
            SetCurrentValue(PlaceCityProperty, string.Empty);
            SetCurrentValue(PlaceLatitudeProperty, Math.Round(picked.Latitude, 1));
            SetCurrentValue(PlaceLongitudeProperty, Math.Round(picked.Longitude, 1));
        }
    }

    /// <summary>The city whose dot is within a few pixels of <paramref name="point"/>.</summary>
    private WorldCity? NearestCity(Point point)
    {
        WorldCity? best = null;
        var bestDistance = 12.0;
        foreach (var city in WorldCities.All)
        {
            if (ScreenPoint(city.Location) is not { } at) continue;
            var distance = ((Vector)(at - point)).Length;
            if (distance < bestDistance)
            {
                best = city;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Where a place is drawn in the current mode, or null when it is out of sight.</summary>
    private Point? ScreenPoint(GeoPoint place) => Mode == DayNightMode.Globe ? GlobeScreenPoint(place) : MapPainter.ToScreen(MapRect(), place);

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

        var utc = Utc;
        var sun = DayNightGeometry.Sun(utc);
        if (Mode == DayNightMode.Globe)
        {
            _stars.Draw(dc, width, height, dpi, now, 0.8);
            DrawGlobe(dc, dpi, utc, sun, now);
        }
        else
        {
            _stars.Draw(dc, width, height, dpi, now, 0.35);
            DrawMap(dc, utc, sun, now);
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    /// <summary>
    /// The thing a target id stands for, which names its resource keys (<see cref="ObjectTitleKey"/>,
    /// <see cref="ObjectClipKey"/>). Both tropics share one, as do both polar circles.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "sun" => "Sun",
        "sunriseline" => "SunriseLine",
        "sunsetline" => "SunsetLine",
        "noon" => "Noon",
        "midnight" => "Midnight",
        "tropic" => "Tropics",
        "polarcircle" => "PolarCircle",
        "equator" => "Equator",
        "zone" => "Zone",
        "utc" => "Utc",
        "newday" => "NewDay",
        "dateline" => "DateLine",
        "sunlight" => "Sunlight",
        "axis" => "Axis",
        "angle" => "Angle",
        "polarday" => "PolarDay",
        "polarnight" => "PolarNight",
        _ => null,
    };

    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "DayNight_Obj_" + name : null;

    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_DayNight_Obj_" + name : null;

    private static bool IsLineHighlight(string id) => id is "terminator" or "noonline" or "newday";

    /// <summary>A target over a text label drawn at <paramref name="topLeft"/>.</summary>
    private void AddLabelTarget(string id, Point topLeft, double width, double height) =>
        _targets.AddBox(id, new Rect(topLeft, new Size(width, height)));

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    /// <summary>The zone a place keeps its clocks on: the city's real zone, or the ideal zone of its meridian.</summary>
    internal static TimeZoneInfo ZoneOf(string? cityId, double longitude) =>
        WorldCities.Find(cityId)?.TimeZone ?? WorldCities.Fixed(TimeSpan.FromHours(DayNightGeometry.NominalZone(longitude)));

    /// <summary>"6.2° S" style latitude in the current language.</summary>
    internal static string LatitudeText(double latitude) => Math.Abs(latitude) < 0.05
        ? Loc("DayNight_Equator")
        : LocFormat(latitude > 0 ? "DayNight_Latitude_North" : "DayNight_Latitude_South", Math.Abs(latitude));

    /// <summary>"116.4° E" style longitude in the current language.</summary>
    internal static string LongitudeText(double longitude)
    {
        longitude = DayNightGeometry.Normalize180(longitude);
        if (Math.Abs(longitude) < 0.05 || Math.Abs(longitude) > 179.95) return LocFormat("DayNight_Longitude_Zero", Math.Abs(longitude));
        return LocFormat(longitude > 0 ? "DayNight_Longitude_East" : "DayNight_Longitude_West", Math.Abs(longitude));
    }

    /// <summary>A clock time as hours and minutes, e.g. 06:12.</summary>
    internal static string ClockText(DateTime time) => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A month and day in the current language, e.g. "June 21" or "6月21日".</summary>
    internal static string DateText(DateTime date) => date.ToString("M", LocalizationService.Instance.CurrentCulture);
}
