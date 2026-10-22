using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// The seasons chapter: which view is shown, playback, the observer's latitude and Earth's tilt, and what the Sun does
/// there on the current day. The diagram (<see cref="SeasonsDiagram"/>) moves <see cref="Day"/> and
/// <see cref="TimeOfDay"/>; everything else follows them. <see cref="Lesson"/> is the spoken walkthrough of the
/// current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it. One instance serves both pages
/// of the chapter's spread.
/// </summary>
public partial class SeasonsViewModel : ObservableObject, IDisposable
{
    /// <summary>Where the chapter starts before the reader picks a place: Beijing, one of the presets.</summary>
    internal const double DefaultLatitude = 39.9;

    internal const string LatitudeSetting = "seasons.latitude";

    private static readonly IBrush SpringBrush = Frozen(0x8F, 0xD1, 0x8B);
    private static readonly IBrush SummerBrush = Frozen(0xFF, 0xB3, 0x47);
    private static readonly IBrush AutumnBrush = Frozen(0xE8, 0x87, 0x5A);
    private static readonly IBrush WinterBrush = Frozen(0x8E, 0xC5, 0xFF);
    private static readonly IBrush NeutralBrush = Frozen(0xC9, 0xD1, 0xDC);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private readonly ISettingsStore _settings;
    private int _views;

    public SeasonsViewModel(INarrationService narration, ISettingsStore settings)
    {
        _settings = settings;
        _latitude = double.TryParse(settings.Get(LatitudeSetting), NumberStyles.Float, CultureInfo.InvariantCulture, out var saved)
            ? Math.Clamp(saved, -90, 90)
            : DefaultLatitude;
        Lesson = new LessonPlayer(narration, () => SeasonsLessons.For(this));
        Card = new ObjectCard(narration, Lesson, SeasonsDiagram.ObjectTitleKey, SeasonsDiagram.ObjectClipKey);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

        // Start at today's date; setting it refreshes everything.
        Day = SeasonsGeometry.Today();
    }

    /// <summary>A view of the chapter came on screen (its pages are shown or about to be turned to).</summary>
    public void ViewAttached() => _views++;

    /// <summary>
    /// A view of the chapter left the screen. Once none is left the reader has turned away, so the walkthrough and any
    /// explanation being read stop rather than talking over the next chapter.
    /// </summary>
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

    #region mode and options

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTilt), nameof(IsSunPath), nameof(IsDistance), nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts))]
    private SeasonsMode _mode;

    public bool IsTilt
    {
        get => Mode == SeasonsMode.Tilt;
        set
        {
            if (value) Mode = SeasonsMode.Tilt;
        }
    }

    public bool IsSunPath
    {
        get => Mode == SeasonsMode.SunPath;
        set
        {
            if (value) Mode = SeasonsMode.SunPath;
        }
    }

    public bool IsDistance
    {
        get => Mode == SeasonsMode.Distance;
        set
        {
            if (value) Mode = SeasonsMode.Distance;
        }
    }

    public string Subtitle => _localization.GetString(Mode switch
    {
        SeasonsMode.SunPath => "Seasons_SunPath_Subtitle",
        SeasonsMode.Distance => "Seasons_Distance_Subtitle",
        _ => "Seasons_Tilt_Subtitle",
    });

    public string ScaleNote => _localization.GetString(Mode switch
    {
        SeasonsMode.SunPath => "Seasons_SunPath_Note",
        SeasonsMode.Distance => "Seasons_Distance_Note",
        _ => "Seasons_Tilt_Note",
    });

    /// <summary>What dragging on the diagram does in this view.</summary>
    public string DragHint => _localization.GetString(Mode == SeasonsMode.SunPath ? "Seasons_DragHint_Sky" : "Seasons_DragHint_Orbit");

    /// <summary>The observer's latitude, −90…90 (north positive); remembered on this device.</summary>
    [ObservableProperty]
    private double _latitude;

    /// <summary>Earth's axial tilt, 0…90.</summary>
    [ObservableProperty]
    private double _tilt = SeasonsGeometry.EarthTilt;

    /// <summary>Sun-path view: playback runs through the year instead of through the day.</summary>
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
    private void SetLatitude(string? latitude)
    {
        if (double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            Latitude = Math.Clamp(value, -90, 90);
        }
    }

    [RelayCommand]
    private void ResetTilt() => Tilt = SeasonsGeometry.EarthTilt;

    #endregion

    #region playback

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Day of the year, 0…365 (see <see cref="SeasonsGeometry"/>).</summary>
    [ObservableProperty]
    private double _day;

    /// <summary>Sun-path view: local solar time in hours.</summary>
    [ObservableProperty]
    private double _timeOfDay = 12;

    public double YearLength => SeasonsGeometry.Year;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Jumps to an equinox or solstice (by <see cref="SeasonEvent"/> name) or to today, and pauses there.</summary>
    [RelayCommand]
    private void JumpTo(string? target)
    {
        Day = Enum.TryParse<SeasonEvent>(target, out var e) ? SeasonsGeometry.EventDay(e) : SeasonsGeometry.Today();
        IsPlaying = false;
    }

    partial void OnModeChanged(SeasonsMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
        Refresh();
    }

    partial void OnDayChanged(double value) => Refresh();

    partial void OnTimeOfDayChanged(double value) => Refresh();

    partial void OnLatitudeChanged(double value)
    {
        _settings.Set(LatitudeSetting, value.ToString("R", CultureInfo.InvariantCulture));
        Refresh();
    }

    partial void OnTiltChanged(double value) => Refresh();

    #endregion

    #region walkthrough and clicked things

    public LessonPlayer Lesson { get; }

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    #endregion

    #region what is happening

    private double _declination;
    private double _noonAltitude;
    private string _dateText = string.Empty;
    private string _timeText = string.Empty;
    private string _latitudeText = string.Empty;
    private string _tiltText = string.Empty;
    private string _stageTitle = string.Empty;
    private string _stageDescription = string.Empty;
    private IBrush _stageBrush = NeutralBrush;
    private string _noonAltitudeText = string.Empty;
    private string _dayLengthText = string.Empty;
    private string _sunriseText = string.Empty;
    private string _sunlightText = string.Empty;
    private string _distanceText = string.Empty;
    private string _beamText = string.Empty;
    private string _sunNowText = string.Empty;
    private string _latitudeLegend = string.Empty;
    private string _oppositeLegend = string.Empty;
    private string _northLegend = string.Empty;
    private string _southLegend = string.Empty;

    /// <summary>The latitude where the Sun is overhead at noon today.</summary>
    public double Declination
    {
        get => _declination;
        private set => SetProperty(ref _declination, value);
    }

    public double NoonAltitude
    {
        get => _noonAltitude;
        private set => SetProperty(ref _noonAltitude, value);
    }

    public string DateText
    {
        get => _dateText;
        private set => SetProperty(ref _dateText, value);
    }

    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    public string LatitudeText
    {
        get => _latitudeText;
        private set => SetProperty(ref _latitudeText, value);
    }

    public string TiltText
    {
        get => _tiltText;
        private set => SetProperty(ref _tiltText, value);
    }

    /// <summary>The season where the observer is, e.g. "Northern hemisphere: autumn".</summary>
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

    public string NoonAltitudeText
    {
        get => _noonAltitudeText;
        private set => SetProperty(ref _noonAltitudeText, value);
    }

    public string DayLengthText
    {
        get => _dayLengthText;
        private set => SetProperty(ref _dayLengthText, value);
    }

    public string SunriseText
    {
        get => _sunriseText;
        private set => SetProperty(ref _sunriseText, value);
    }

    public string SunlightText
    {
        get => _sunlightText;
        private set => SetProperty(ref _sunlightText, value);
    }

    public string DistanceText
    {
        get => _distanceText;
        private set => SetProperty(ref _distanceText, value);
    }

    /// <summary>Under the beam picture: how thinly the noon sunlight is spread.</summary>
    public string BeamText
    {
        get => _beamText;
        private set => SetProperty(ref _beamText, value);
    }

    /// <summary>Sun-path view: the time and where the Sun is right now.</summary>
    public string SunNowText
    {
        get => _sunNowText;
        private set => SetProperty(ref _sunNowText, value);
    }

    public string LatitudeLegend
    {
        get => _latitudeLegend;
        private set => SetProperty(ref _latitudeLegend, value);
    }

    public string OppositeLegend
    {
        get => _oppositeLegend;
        private set => SetProperty(ref _oppositeLegend, value);
    }

    public string NorthLegend
    {
        get => _northLegend;
        private set => SetProperty(ref _northLegend, value);
    }

    public string SouthLegend
    {
        get => _southLegend;
        private set => SetProperty(ref _southLegend, value);
    }

    /// <summary>Background facts for the current view.</summary>
    public IReadOnlyList<string> Facts => (Mode switch
    {
        SeasonsMode.SunPath => new[] { "Seasons_SunPath_Fact1", "Seasons_SunPath_Fact2", "Seasons_SunPath_Fact3", "Seasons_SunPath_Fact4" },
        SeasonsMode.Distance => ["Seasons_Distance_Fact1", "Seasons_Distance_Fact2", "Seasons_Distance_Fact3", "Seasons_Distance_Fact4"],
        _ => ["Seasons_Tilt_Fact1", "Seasons_Tilt_Fact2", "Seasons_Tilt_Fact3", "Seasons_Tilt_Fact4", "Seasons_Tilt_Fact5"],
    }).Select(_localization.GetString).ToList();

    private void Refresh()
    {
        var latitude = Math.Clamp(Latitude, -90, 90);
        var tilt = Math.Clamp(Tilt, 0, 90);
        var declination = SeasonsGeometry.Declination(Day, tilt);
        var noon = SeasonsGeometry.NoonAltitude(latitude, declination);
        var hours = SeasonsGeometry.DayLength(latitude, declination);

        Declination = declination;
        NoonAltitude = noon;
        DateText = SeasonsDiagram.DateText(Day);
        TimeText = SeasonsDiagram.ClockText(TimeOfDay);
        LatitudeText = LatitudeName(latitude);
        TiltText = _localization.Format("Seasons_Degrees", tilt);

        NoonAltitudeText = noon > 0
            ? _localization.Format("Seasons_Degrees", noon)
            : _localization.GetString("Seasons_Stat_BelowHorizon");
        var minutes = (int)Math.Round(hours * 60);
        DayLengthText = _localization.Format("Seasons_Duration", minutes / 60, minutes % 60);
        var h0 = SeasonsGeometry.SunsetHourAngle(latitude, declination);
        SunriseText = hours >= 24 - 1e-6
            ? _localization.GetString("Seasons_Stat_PolarDay")
            : hours <= 1e-6
                ? _localization.GetString("Seasons_Stat_PolarNight")
                : _localization.Format("Seasons_Stat_SunriseSunset", SeasonsDiagram.ClockText(12 - (h0 / 15)), SeasonsDiagram.ClockText(12 + (h0 / 15)));
        SunlightText = _localization.Format("Seasons_Percent", SeasonsGeometry.DailySunlight(latitude, declination));
        DistanceText = _localization.Format("Seasons_Million_Km", SeasonsGeometry.SunDistance(Day) * SeasonsGeometry.AstronomicalUnitKm / 1e6);
        BeamText = noon > 0
            ? _localization.Format("Seasons_Beam_Text", Math.Sin(SeasonsGeometry.ToRadians(noon)))
            : _localization.GetString("Seasons_Beam_None");

        var now = SeasonsGeometry.Altitude(latitude, declination, SeasonsGeometry.HourAngle(TimeOfDay));
        SunNowText = now > 0
            ? _localization.Format("Seasons_SunNow_Up", TimeText, now)
            : _localization.Format("Seasons_SunNow_Down", TimeText);

        LatitudeLegend = _localization.Format("Seasons_Legend_Latitude", LatitudeText);
        OppositeLegend = _localization.Format("Seasons_Legend_Opposite", LatitudeName(-latitude));
        NorthLegend = LatitudeName(Math.Abs(latitude));
        SouthLegend = LatitudeName(-Math.Abs(latitude));

        ShowStage(latitude, tilt);
    }

    private void ShowStage(double latitude, double tilt)
    {
        if (tilt < 0.5)
        {
            StageTitle = _localization.GetString("Seasons_Stage_NoTilt");
            StageDescription = _localization.GetString("Seasons_Stage_NoTilt_Description");
            StageBrush = NeutralBrush;
            return;
        }

        if (Math.Abs(latitude) < 1 && tilt <= 30)
        {
            StageTitle = _localization.GetString("Seasons_Stage_Equator");
            StageDescription = _localization.GetString("Seasons_Stage_Equator_Description");
            StageBrush = SummerBrush;
            return;
        }

        var north = latitude >= 0;
        var season = SeasonsGeometry.SeasonAt(SeasonsGeometry.SunLongitude(Day), north);
        StageTitle = _localization.Format("Seasons_Stage_Title",
            _localization.GetString(north ? "Seasons_Hemisphere_North" : "Seasons_Hemisphere_South"),
            _localization.GetString(SeasonsDiagram.SeasonKey(season)));
        StageDescription = _localization.GetString(season switch
        {
            Season.Spring => "Seasons_Stage_Spring_Description",
            Season.Summer => "Seasons_Stage_Summer_Description",
            Season.Autumn => "Seasons_Stage_Autumn_Description",
            _ => "Seasons_Stage_Winter_Description",
        });
        StageBrush = season switch
        {
            Season.Spring => SpringBrush,
            Season.Summer => SummerBrush,
            Season.Autumn => AutumnBrush,
            _ => WinterBrush,
        };
    }

    /// <summary>E.g. "39.9° N", "33.9° S" or "Equator".</summary>
    private string LatitudeName(double latitude)
    {
        if (Math.Abs(latitude) < 0.05) return _localization.GetString("Seasons_Latitude_Equator");
        return _localization.Format(latitude > 0 ? "Seasons_Latitude_North" : "Seasons_Latitude_South", Math.Abs(latitude));
    }

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
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
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}
