using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>
/// The day-and-night page: which view is shown, playback, the chosen place, and what the Sun and the clocks do there
/// at the current instant. The diagram (<see cref="DayNightDiagram"/>) moves <see cref="Day"/> and
/// <see cref="UtcHours"/> and picks the place; everything else follows them. <see cref="Lesson"/> is the spoken
/// walkthrough of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class DayNightViewModel : ObservableObject, IChapterContent, IDisposable
{
    private const double DefaultTowardSun = 20;
    private const double DefaultElevation = 15;

    private static readonly IBrush DayBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush LowSunBrush = Frozen(0xFF, 0xA8, 0x6B);
    private static readonly IBrush TwilightBrush = Frozen(0xE8, 0x9A, 0x8A);
    private static readonly IBrush NauticalBrush = Frozen(0x8E, 0xC5, 0xFF);
    private static readonly IBrush NightBrush = Frozen(0xA4, 0xB2, 0xE0);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;
    private (double Lat, double Lon, string City, DateOnly Date) _eventsKey;
    private SunEvents _events;

    public DayNightViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        Lesson = new LessonPlayer(narration, () => DayNightLessons.For(this));
        Card = new ObjectCard(narration, Lesson, DayNightDiagram.ObjectTitleKey, DayNightDiagram.ObjectClipKey);
        Year = DateTime.UtcNow.Year;
        Clocks = new ObservableCollection<CityClock>(WorldCities.All.Select(c => new CityClock(c)));
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

        // Start at this moment at the shared observer location; setting the time refreshes everything.
        GoHome();
        location.Changed += OnLocationChanged;
        SetInstant(DateTime.UtcNow);
    }

    /// <summary>A view of the chapter came on screen.</summary>
    public void ViewAttached() => _views++;

    /// <summary>The last view of the chapter left the screen: the walkthrough and any explanation being read stop.</summary>
    public void ViewDetached()
    {
        _views = Math.Max(0, _views - 1);
        if (_views > 0)
        {
            return;
        }

        Lesson.Stop();
        Card.Close();
    }

    /// <summary>The year shown (the current one); the date slider runs through it.</summary>
    public int Year { get; }

    public double LastDay => DateTime.IsLeapYear(Year) ? 365 : 364;

    #region mode and options

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMap), nameof(IsTimeZones), nameof(IsGlobe), nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts))]
    private DayNightMode _mode;

    public bool IsMap
    {
        get => Mode == DayNightMode.Map;
        set
        {
            if (value) Mode = DayNightMode.Map;
        }
    }

    public bool IsTimeZones
    {
        get => Mode == DayNightMode.TimeZones;
        set
        {
            if (value) Mode = DayNightMode.TimeZones;
        }
    }

    public bool IsGlobe
    {
        get => Mode == DayNightMode.Globe;
        set
        {
            if (value) Mode = DayNightMode.Globe;
        }
    }

    public string Subtitle => _localization.GetString(Mode switch
    {
        DayNightMode.TimeZones => "DayNight_TimeZones_Subtitle",
        DayNightMode.Globe => "DayNight_Globe_Subtitle",
        _ => "DayNight_Map_Subtitle",
    });

    public string ScaleNote => _localization.GetString(Mode switch
    {
        DayNightMode.TimeZones => "DayNight_TimeZones_Note",
        DayNightMode.Globe => "DayNight_Globe_Note",
        _ => "DayNight_Map_Note",
    });

    /// <summary>What clicking and dragging on the diagram do in this view.</summary>
    public string DragHint => _localization.GetString(Mode == DayNightMode.Globe ? "DayNight_DragHint_Globe" : "DayNight_DragHint_Map");

    /// <summary>Map view: also draw where the three twilights end.</summary>
    [ObservableProperty]
    private bool _showTwilight = true;

    /// <summary>Globe view: how far the view is turned towards the Sun.</summary>
    [ObservableProperty]
    private double _viewTowardSun = DefaultTowardSun;

    /// <summary>Globe view: how far above the equator the view is.</summary>
    [ObservableProperty]
    private double _viewElevation = DefaultElevation;

    /// <summary>Playback runs through the year at the same time each day instead of through the day.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimatesDay))]
    private bool _animatesYear;

    public bool AnimatesDay
    {
        get => !AnimatesYear;
        set
        {
            if (value) AnimatesYear = false;
        }
    }

    [RelayCommand]
    private void ResetView()
    {
        ViewTowardSun = DefaultTowardSun;
        ViewElevation = DefaultElevation;
    }

    #endregion

    #region place

    [ObservableProperty]
    private double _placeLatitude;

    [ObservableProperty]
    private double _placeLongitude;

    /// <summary>The chosen city's id, or empty for a point picked on the map.</summary>
    [ObservableProperty]
    private string _placeCity = string.Empty;

    /// <summary>The shared observer location's name, for the chip that goes back to it.</summary>
    /// <summary>The shared observer location, for the location chip on the page.</summary>
    public IExploreLocationService Location => _location;

    public string HomeName => _location.Current.Name;

    /// <summary>Whether the page needs its own "home" button: not when home is one of the cities it lists anyway.</summary>
    public bool ShowsHomeButton => _location.Current.Id is not ("Beijing" or "Kashgar" or "London" or "NewYork" or "Sydney" or "Tromso");

    /// <summary>Back to the shared observer location: its city, or its custom point.</summary>
    [RelayCommand]
    private void GoHome()
    {
        var home = _location.Current;
        if (home.AsWorldCity is { } city)
        {
            SelectCity(city.Id);
            return;
        }

        PlaceCity = string.Empty;
        PlaceLatitude = home.Latitude;
        PlaceLongitude = home.Longitude;
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HomeName));
        OnPropertyChanged(nameof(ShowsHomeButton));
        GoHome();
    }

    [RelayCommand]
    private void SelectCity(string? id)
    {
        if (WorldCities.Find(id) is not { } city) return;
        PlaceCity = city.Id;
        PlaceLatitude = city.Latitude;
        PlaceLongitude = city.Longitude;
    }

    partial void OnPlaceLatitudeChanged(double value) => Refresh();

    partial void OnPlaceLongitudeChanged(double value) => Refresh();

    partial void OnPlaceCityChanged(string value) => Refresh();

    #endregion

    #region playback

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Day of the year in UTC, 0 for 1 January; only its whole part counts.</summary>
    [ObservableProperty]
    private double _day;

    /// <summary>Time of day in UTC, 0…24.</summary>
    [ObservableProperty]
    private double _utcHours;

    /// <summary>The instant shown, in UTC.</summary>
    public DateTime Instant { get; private set; }

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Jumps to the date of an equinox or solstice (by <see cref="SeasonEvent"/> name), or to now, and pauses.</summary>
    [RelayCommand]
    private void JumpTo(string? target)
    {
        if (Enum.TryParse<SeasonEvent>(target, out var e))
        {
            Day = DayNightGeometry.EventTime(Year, e).DayOfYear - 1;
        }
        else
        {
            SetInstant(DateTime.UtcNow);
        }

        IsPlaying = false;
    }

    private void SetInstant(DateTime utc)
    {
        if (utc.Year != Year) utc = DayNightDiagram.StartOfYear(Year);
        UtcHours = utc.TimeOfDay.TotalHours;
        Day = utc.DayOfYear - 1;
        Refresh();
    }

    partial void OnModeChanged(DayNightMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
        Refresh();
    }

    partial void OnDayChanged(double value) => Refresh();

    partial void OnUtcHoursChanged(double value) => Refresh();

    #endregion

    #region walkthrough and clicked things

    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    /// <summary>Puts the date on an equinox or solstice without pausing (for walkthroughs).</summary>
    internal void GoToEvent(SeasonEvent e) => Day = DayNightGeometry.EventTime(Year, e).DayOfYear - 1;

    #endregion

    #region what is happening

    private string _placeName = string.Empty;
    private string _dateText = string.Empty;
    private string _placeTimeText = string.Empty;
    private string _utcText = string.Empty;
    private string _localTimeText = string.Empty;
    private string _solarTimeText = string.Empty;
    private string _altitudeText = string.Empty;
    private string _sunriseText = string.Empty;
    private string _dayLengthText = string.Empty;
    private string _subsolarText = string.Empty;
    private string _stageTitle = string.Empty;
    private string _stageDescription = string.Empty;
    private IBrush _stageBrush = DayBrush;
    private string _clocksNote = string.Empty;
    private string _chartCaption = string.Empty;
    private string _globeCaption = string.Empty;

    public string PlaceName
    {
        get => _placeName;
        private set => SetProperty(ref _placeName, value);
    }

    /// <summary>The date at the chosen place.</summary>
    public string DateText
    {
        get => _dateText;
        private set => SetProperty(ref _dateText, value);
    }

    /// <summary>The time on the clocks at the chosen place, e.g. "14:30".</summary>
    public string PlaceTimeText
    {
        get => _placeTimeText;
        private set => SetProperty(ref _placeTimeText, value);
    }

    /// <summary>The same instant in UTC, e.g. "UTC 06:30".</summary>
    public string UtcText
    {
        get => _utcText;
        private set => SetProperty(ref _utcText, value);
    }

    public string LocalTimeText
    {
        get => _localTimeText;
        private set => SetProperty(ref _localTimeText, value);
    }

    public string SolarTimeText
    {
        get => _solarTimeText;
        private set => SetProperty(ref _solarTimeText, value);
    }

    public string AltitudeText
    {
        get => _altitudeText;
        private set => SetProperty(ref _altitudeText, value);
    }

    public string SunriseText
    {
        get => _sunriseText;
        private set => SetProperty(ref _sunriseText, value);
    }

    public string DayLengthText
    {
        get => _dayLengthText;
        private set => SetProperty(ref _dayLengthText, value);
    }

    /// <summary>Where the Sun is overhead right now.</summary>
    public string SubsolarText
    {
        get => _subsolarText;
        private set => SetProperty(ref _subsolarText, value);
    }

    /// <summary>The place and its part of the day, e.g. "Beijing: dusk".</summary>
    public string StageTitle
    {
        get => _stageTitle;
        private set => SetProperty(ref _stageTitle, value);
    }

    public string StageDescription
    {
        get => _stageDescription;
        private set => SetProperty(ref _stageDescription, value);
    }

    public IBrush StageBrush
    {
        get => _stageBrush;
        private set => SetProperty(ref _stageBrush, value);
    }

    /// <summary>Under the world clocks: what "+1 day" is relative to.</summary>
    public string ClocksNote
    {
        get => _clocksNote;
        private set => SetProperty(ref _clocksNote, value);
    }

    public string ChartCaption
    {
        get => _chartCaption;
        private set => SetProperty(ref _chartCaption, value);
    }

    public string GlobeCaption
    {
        get => _globeCaption;
        private set => SetProperty(ref _globeCaption, value);
    }

    /// <summary>Time-zone view: the time in every city on the map.</summary>
    public ObservableCollection<CityClock> Clocks { get; }

    /// <summary>Background facts for the current view.</summary>
    public IReadOnlyList<string> Facts => (Mode switch
    {
        DayNightMode.TimeZones => new[] { "DayNight_TimeZones_Fact1", "DayNight_TimeZones_Fact2", "DayNight_TimeZones_Fact3", "DayNight_TimeZones_Fact4", "DayNight_TimeZones_Fact5", "DayNight_TimeZones_Fact6" },
        DayNightMode.Globe => ["DayNight_Globe_Fact1", "DayNight_Globe_Fact2", "DayNight_Globe_Fact3", "DayNight_Globe_Fact4", "DayNight_Globe_Fact5"],
        _ => ["DayNight_Map_Fact1", "DayNight_Map_Fact2", "DayNight_Map_Fact3", "DayNight_Map_Fact4", "DayNight_Map_Fact5"],
    }).Select(_localization.GetString).ToList();

    private void Refresh()
    {
        var days = (int)LastDay + 1;
        var utc = DayNightDiagram.StartOfYear(Year).AddDays(Math.Clamp(Math.Floor(Day), 0, days - 1)).AddHours(Math.Clamp(UtcHours, 0, 24));
        Instant = utc;
        OnPropertyChanged(nameof(Instant));

        var sun = DayNightGeometry.Sun(utc);
        var latitude = Math.Clamp(PlaceLatitude, -90, 90);
        var longitude = DayNightGeometry.Normalize180(PlaceLongitude);
        var zone = DayNightDiagram.ZoneOf(PlaceCity, longitude);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        var offset = zone.GetUtcOffset(utc);

        PlaceName = WorldCities.Find(PlaceCity) is { } city
            ? _localization.GetString(city.NameKey)
            : _localization.Format("DayNight_Place_Point", DayNightDiagram.LatitudeText(latitude), DayNightDiagram.LongitudeText(longitude));
        DateText = DayNightDiagram.DateText(local);
        PlaceTimeText = _localization.Format("DayNight_Toolbar_Place", DateText, DayNightDiagram.ClockText(local), PlaceName);
        UtcText = _localization.Format("DayNight_Toolbar_Utc", DayNightDiagram.ClockText(utc));
        LocalTimeText = _localization.Format("DayNight_Stat_LocalTime_Value", DayNightDiagram.ClockText(local), DateText,
            WorldCities.Find(PlaceCity) is null ? _localization.Format("DayNight_Zone_Ideal", WorldCities.OffsetText(offset)) : WorldCities.OffsetText(offset));

        var solar = DayNightGeometry.SolarTime(sun, longitude);
        SolarTimeText = _localization.Format("DayNight_Stat_Solar_Value", Clock(solar), ClockDifference(local.TimeOfDay.TotalHours - solar));

        var altitude = DayNightGeometry.Altitude(sun, latitude, longitude);
        AltitudeText = altitude >= 0
            ? _localization.Format("DayNight_Degrees", altitude)
            : _localization.Format("DayNight_Stat_Below", -altitude);

        var date = DateOnly.FromDateTime(local);
        var key = (latitude, longitude, PlaceCity ?? string.Empty, date);
        if (key != _eventsKey)
        {
            _eventsKey = key;
            _events = DayNightGeometry.EventsOn(date, zone, latitude, longitude);
        }

        SunriseText = _events.AlwaysUp
            ? _localization.GetString("DayNight_Stat_PolarDay")
            : _events.AlwaysDown
                ? _localization.GetString("DayNight_Stat_PolarNight")
                : _localization.Format("DayNight_Stat_SunriseSunset", LocalClock(_events.Rise, zone), LocalClock(_events.Set, zone));
        var minutes = (int)Math.Round(_events.Daylight.TotalMinutes);
        DayLengthText = _localization.Format("DayNight_Duration", minutes / 60, minutes % 60);
        SubsolarText = _localization.Format("DayNight_Stat_Subsolar_Value", DayNightDiagram.LatitudeText(sun.Declination), DayNightDiagram.LongitudeText(sun.SubsolarLongitude));

        ShowStage(DayNightGeometry.PhaseOf(altitude, DayNightGeometry.HourAngle(sun, longitude)));

        ClocksNote = _localization.Format("DayNight_Clocks_Note", PlaceName);
        ChartCaption = _localization.Format("DayNight_Chart_Caption", PlaceName);
        GlobeCaption = _localization.Format("DayNight_PlaceGlobe_Caption", PlaceName);

        foreach (var clock in Clocks) clock.Update(utc, sun, date, PlaceCity, _localization);
    }

    private void ShowStage(DayPhase phase)
    {
        string name;
        string description;
        IBrush brush;
        if (_events.AlwaysUp)
        {
            (name, description, brush) = ("DayNight_Phase_PolarDay", "DayNight_Stage_PolarDay_Description", DayBrush);
        }
        else if (_events.AlwaysDown && phase is not (DayPhase.Day or DayPhase.Sunrise or DayPhase.Sunset))
        {
            (name, description, brush) = ("DayNight_Phase_PolarNight", "DayNight_Stage_PolarNight_Description", NightBrush);
        }
        else
        {
            (name, description, brush) = phase switch
            {
                DayPhase.Day => ("DayNight_Phase_Day", "DayNight_Stage_Day_Description", DayBrush),
                DayPhase.Sunrise => ("DayNight_Phase_Sunrise", "DayNight_Stage_Sunrise_Description", LowSunBrush),
                DayPhase.Sunset => ("DayNight_Phase_Sunset", "DayNight_Stage_Sunset_Description", LowSunBrush),
                DayPhase.Dawn => ("DayNight_Phase_Dawn", "DayNight_Stage_Dawn_Description", TwilightBrush),
                DayPhase.Dusk => ("DayNight_Phase_Dusk", "DayNight_Stage_Dusk_Description", TwilightBrush),
                DayPhase.NauticalTwilight => ("DayNight_Phase_NauticalTwilight", "DayNight_Stage_Nautical_Description", NauticalBrush),
                DayPhase.AstronomicalTwilight => ("DayNight_Phase_AstronomicalTwilight", "DayNight_Stage_Astronomical_Description", NightBrush),
                _ => ("DayNight_Phase_Night", "DayNight_Stage_Night_Description", NightBrush),
            };
        }

        StageTitle = _localization.Format("DayNight_Stage_Title", PlaceName, _localization.GetString(name));
        StageDescription = _localization.GetString(description);
        StageBrush = brush;
    }

    private static string Clock(double hours)
    {
        var minutes = (int)Math.Round(DayNightGeometry.Wrap24(hours) * 60) % 1440;
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    private string LocalClock(DateTime? utc, TimeZoneInfo zone) =>
        utc is { } value ? DayNightDiagram.ClockText(TimeZoneInfo.ConvertTimeFromUtc(value, zone)) : _localization.GetString("DayNight_Stat_None");

    /// <summary>How far the clock runs ahead of or behind the Sun, e.g. "clock 2 h 51 min ahead".</summary>
    private string ClockDifference(double hours)
    {
        hours -= 24 * Math.Round(hours / 24);
        var minutes = (int)Math.Round(Math.Abs(hours) * 60);
        if (minutes < 3) return _localization.GetString("DayNight_Clock_Same");

        var span = minutes >= 60
            ? _localization.Format("DayNight_Duration", minutes / 60, minutes % 60)
            : _localization.Format("DayNight_Minutes", minutes);
        return _localization.Format(hours > 0 ? "DayNight_Clock_Ahead" : "DayNight_Clock_Behind", span);
    }

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HomeName));
        OnPropertyChanged(nameof(ShowsHomeButton));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        Refresh();
    }

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _location.Changed -= OnLocationChanged;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>One row of the world clock: a city, the time on its clocks, and its date against the chosen place's.</summary>
public sealed partial class CityClock(WorldCity city) : ObservableObject
{
    public string Id => city.Id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _timeText = string.Empty;

    /// <summary>"+1 day", "−1 day" or empty when the city's date is the chosen place's.</summary>
    [ObservableProperty]
    private string _dayText = string.Empty;

    [ObservableProperty]
    private string _offsetText = string.Empty;

    /// <summary>The Sun there: up, rising or setting, or down.</summary>
    [ObservableProperty]
    private Geometry _icon = SkyIcons.Sun;

    [ObservableProperty]
    private bool _isDaylight;

    [ObservableProperty]
    private bool _isSelected;

    internal void Update(DateTime utc, SunPosition sun, DateOnly placeDate, string? placeCity, ILocalizationService localization)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, city.TimeZone);
        Name = localization.GetString(city.NameKey);
        TimeText = DayNightDiagram.ClockText(local);
        OffsetText = WorldCities.OffsetText(city.TimeZone.GetUtcOffset(utc));

        var days = DateOnly.FromDateTime(local).DayNumber - placeDate.DayNumber;
        DayText = days switch
        {
            0 => string.Empty,
            > 0 => localization.Format("DayNight_Clocks_Later", days),
            _ => localization.Format("DayNight_Clocks_Earlier", -days),
        };

        var altitude = DayNightGeometry.Altitude(sun, city.Latitude, city.Longitude);
        var phase = DayNightGeometry.PhaseOf(altitude, DayNightGeometry.HourAngle(sun, city.Longitude));
        Icon = phase switch
        {
            DayPhase.Day => SkyIcons.Sun,
            DayPhase.Sunrise or DayPhase.Dawn => SkyIcons.Sunrise,
            DayPhase.Sunset or DayPhase.Dusk => SkyIcons.Sunset,
            _ => SkyIcons.Night,
        };
        IsDaylight = altitude >= DayNightGeometry.SunriseAltitude;
        IsSelected = city.Id == placeCity;
    }
}
