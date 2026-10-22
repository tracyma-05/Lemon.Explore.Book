using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.ViewModels.Pages;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>A coming new moon, quarter or full moon, with its date and time in the user's time zone.</summary>
public sealed record UpcomingPhase(string Name, string When);

/// <summary>
/// The moon-phases page: which view is shown, playback, the hemisphere, and the Moon at its current age. The diagram
/// (<see cref="MoonPhasesDiagram"/>) moves <see cref="Age"/> and <see cref="TimeOfDay"/>; everything else follows them.
/// <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in the diagram
/// (<see cref="Card"/>) explains it.
/// </summary>
public partial class MoonPhasesViewModel : ObservableObject, IChapterContent, IDisposable
{
    /// <summary>Beijing and Sydney, the observers of "the other hemisphere" from the shared location.</summary>
    private const double NorthLatitude = 39.9;

    private const double SouthLatitude = -33.9;

    private static readonly IBrush NewBrush = Frozen(0x9A, 0xA4, 0xB4);
    private static readonly IBrush WaxingBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush FullBrush = Frozen(0xF2, 0xF4, 0xF8);
    private static readonly IBrush WaningBrush = Frozen(0x8E, 0xC5, 0xFF);

    /// <summary>
    /// The book has no observer location of its own yet: the Moon is watched from Beijing (one of the presets of the
    /// seasons chapter) on China Standard Time, and the other hemisphere's observer is Sydney, as in the original.
    /// </summary>
    private const double HomeLatitude = NorthLatitude;

    private static readonly TimeZoneInfo HomeZone = FindZone("Asia/Shanghai", TimeSpan.FromHours(8));

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private readonly IBookNavigator _navigator;
    private int _views;

    public MoonPhasesViewModel(INarrationService narration, IBookNavigator navigator)
    {
        _navigator = navigator;
        Lesson = new LessonPlayer(narration, () => MoonPhasesLessons.For(this));
        Card = new ObjectCard(narration, Lesson, MoonPhasesDiagram.ObjectTitleKey, MoonPhasesDiagram.ObjectClipKey);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

        // Start at tonight's Moon; setting it refreshes everything.
        Age = MoonPhaseGeometry.AgeAt(DateTime.UtcNow);
        RefreshUpcoming();
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

    #region mode and options

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPhases), nameof(IsSky), nameof(IsMonth), nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts))]
    private MoonPhasesMode _mode;

    public bool IsPhases
    {
        get => Mode == MoonPhasesMode.Phases;
        set
        {
            if (value) Mode = MoonPhasesMode.Phases;
        }
    }

    public bool IsSky
    {
        get => Mode == MoonPhasesMode.Sky;
        set
        {
            if (value) Mode = MoonPhasesMode.Sky;
        }
    }

    public bool IsMonth
    {
        get => Mode == MoonPhasesMode.Month;
        set
        {
            if (value) Mode = MoonPhasesMode.Month;
        }
    }

    public string Subtitle => _localization.GetString(Mode switch
    {
        MoonPhasesMode.Sky => "MoonPhases_Sky_Subtitle",
        MoonPhasesMode.Month => "MoonPhases_Month_Subtitle",
        _ => "MoonPhases_Phases_Subtitle",
    });

    public string ScaleNote => Mode switch
    {
        MoonPhasesMode.Sky => _localization.GetString("MoonPhases_Sky_Note"),
        MoonPhasesMode.Month => _localization.GetString("MoonPhases_Month_Note"),
        _ => _localization.Format("MoonPhases_Phases_Note", HemisphereName),
    };

    /// <summary>What dragging on the diagram does in this view.</summary>
    public string DragHint => _localization.GetString(Mode switch
    {
        MoonPhasesMode.Sky => "MoonPhases_DragHint_Sky",
        MoonPhasesMode.Month => "MoonPhases_DragHint_Month",
        _ => "MoonPhases_DragHint_Orbit",
    });

    /// <summary>
    /// The Moon is watched from the other hemisphere (Sydney, or Beijing when the shared location is south of the
    /// equator) instead of from the shared observer location.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAtHome), nameof(Latitude), nameof(IsSouthern), nameof(ScaleNote))]
    private bool _isAway;

    public bool IsAtHome
    {
        get => !IsAway;
        set
        {
            if (value) IsAway = false;
        }
    }

    private static bool HomeIsSouthern => HomeLatitude < 0;

    public double Latitude => !IsAway ? HomeLatitude : HomeIsSouthern ? NorthLatitude : SouthLatitude;

    /// <summary>The observer is south of the equator: the Moon is seen upside down.</summary>
    public bool IsSouthern => Latitude < 0;

    /// <summary>The first hemisphere option, e.g. 长沙（北半球）.</summary>
    public string HomeOptionText => _localization.Format(
        "MoonPhases_Hemisphere_Home",
        _localization.GetString("Seasons_City_Beijing"),
        _localization.GetString(HomeIsSouthern ? "MoonPhases_Hemisphere_SouthShort" : "MoonPhases_Hemisphere_NorthShort"));

    /// <summary>The second option: the other hemisphere's city.</summary>
    public string AwayOptionText => _localization.GetString(HomeIsSouthern ? "MoonPhases_Hemisphere_North" : "MoonPhases_Hemisphere_South");

    private string HemisphereName => IsAway ? AwayOptionText : HomeOptionText;

    /// <summary>Sky view: playback runs through the month at the same time each day instead of through one day.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimatesDay))]
    private bool _animatesMonth;

    public bool AnimatesDay
    {
        get => !AnimatesMonth;
        set
        {
            if (value) AnimatesMonth = false;
        }
    }

    [RelayCommand]
    private void OpenEclipses() =>
        _navigator.GoToPage(BookContents.ChapterPage(BookContents.Chapters.ToList().FindIndex(c => c.Slug == "Eclipses")));

    #endregion

    #region playback

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Days since new moon, 0…<see cref="MoonPhaseGeometry.SynodicMonth"/>.</summary>
    [ObservableProperty]
    private double _age;

    /// <summary>Sky view: local solar time in hours.</summary>
    [ObservableProperty]
    private double _timeOfDay = 20;

    public double MonthLength => MoonPhaseGeometry.SynodicMonth;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Jumps to a phase (by <see cref="MoonPhase"/> name) or to tonight's Moon, and pauses there.</summary>
    [RelayCommand]
    private void JumpTo(string? target)
    {
        Age = Enum.TryParse<MoonPhase>(target, out var phase) ? MoonPhaseGeometry.AgeOf(phase) : MoonPhaseGeometry.AgeAt(DateTime.UtcNow);
        IsPlaying = false;
    }

    partial void OnModeChanged(MoonPhasesMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;

        // The month view tells its story from one new moon to the next.
        if (value == MoonPhasesMode.Month) Age = 0;
        IsPlaying = true;
        Refresh();
    }

    partial void OnAgeChanged(double value) => Refresh();

    partial void OnTimeOfDayChanged(double value) => Refresh();

    partial void OnIsAwayChanged(bool value) => Refresh();

    #endregion

    #region walkthrough and clicked things

    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region what is happening

    private string _ageText = string.Empty;
    private string _ageShortText = string.Empty;
    private string _stageTitle = string.Empty;
    private string _stageDescription = string.Empty;
    private IBrush _stageBrush = NewBrush;
    private string _illuminationText = string.Empty;
    private string _elongationText = string.Empty;
    private string _riseSetText = string.Empty;
    private string _skyNowText = string.Empty;
    private string _monthText = string.Empty;
    private string _todayText = string.Empty;
    private IReadOnlyList<UpcomingPhase> _upcoming = [];

    public string AgeText
    {
        get => _ageText;
        private set => SetProperty(ref _ageText, value);
    }

    /// <summary>Beside the timeline, e.g. "Day 7.4".</summary>
    public string AgeShortText
    {
        get => _ageShortText;
        private set => SetProperty(ref _ageShortText, value);
    }

    /// <summary>The phase's name, e.g. "Waxing crescent".</summary>
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

    public string IlluminationText
    {
        get => _illuminationText;
        private set => SetProperty(ref _illuminationText, value);
    }

    public string ElongationText
    {
        get => _elongationText;
        private set => SetProperty(ref _elongationText, value);
    }

    public string RiseSetText
    {
        get => _riseSetText;
        private set => SetProperty(ref _riseSetText, value);
    }

    /// <summary>Sky view: the time and where the Moon and the Sun are right now.</summary>
    public string SkyNowText
    {
        get => _skyNowText;
        private set => SetProperty(ref _skyNowText, value);
    }

    /// <summary>Month view: how far the Moon has turned against the stars and against the Sun.</summary>
    public string MonthText
    {
        get => _monthText;
        private set => SetProperty(ref _monthText, value);
    }

    /// <summary>Tonight's real Moon, e.g. "Today: waxing crescent, 12% lit".</summary>
    public string TodayText
    {
        get => _todayText;
        private set => SetProperty(ref _todayText, value);
    }

    /// <summary>The next new moon, first quarter, full moon and last quarter, in date order.</summary>
    public IReadOnlyList<UpcomingPhase> Upcoming
    {
        get => _upcoming;
        private set => SetProperty(ref _upcoming, value);
    }

    /// <summary>Background facts for the current view.</summary>
    public IReadOnlyList<string> Facts => (Mode switch
    {
        MoonPhasesMode.Sky => new[] { "MoonPhases_Sky_Fact1", "MoonPhases_Sky_Fact2", "MoonPhases_Sky_Fact3", "MoonPhases_Sky_Fact4" },
        MoonPhasesMode.Month => ["MoonPhases_Month_Fact1", "MoonPhases_Month_Fact2", "MoonPhases_Month_Fact3", "MoonPhases_Month_Fact4"],
        _ => ["MoonPhases_Phases_Fact1", "MoonPhases_Phases_Fact2", "MoonPhases_Phases_Fact3", "MoonPhases_Phases_Fact4", "MoonPhases_Phases_Fact5"],
    }).Select(_localization.GetString).ToList();

    private void Refresh()
    {
        var age = MoonPhaseGeometry.Wrap(Age);
        var phase = MoonPhaseGeometry.PhaseOf(age);
        var elongation = MoonPhaseGeometry.Elongation(age);

        AgeText = _localization.Format("MoonPhases_Stat_Age_Value", age, MoonPhaseGeometry.SynodicMonth);
        AgeShortText = _localization.Format("MoonPhases_Age_Short", age);
        StageTitle = _localization.GetString(MoonDrawing.PhaseKey(phase));
        StageDescription = _localization.GetString(MoonDrawing.PhaseKey(phase) + "_Description");
        StageBrush = phase switch
        {
            MoonPhase.New => NewBrush,
            MoonPhase.Full => FullBrush,
            _ => MoonPhaseGeometry.IsWaxing(age) ? WaxingBrush : WaningBrush,
        };

        IlluminationText = _localization.Format("Seasons_Percent", MoonPhaseGeometry.Illumination(age));
        ElongationText = elongation <= 180
            ? _localization.Format("MoonPhases_Stat_East", elongation)
            : _localization.Format("MoonPhases_Stat_West", 360 - elongation);
        RiseSetText = _localization.Format("MoonPhases_Stat_RiseSet_Value",
            SeasonsDiagram.ClockText(MoonPhaseGeometry.MoonriseHour(age)),
            SeasonsDiagram.ClockText(MoonPhaseGeometry.TransitHour(age)),
            SeasonsDiagram.ClockText(MoonPhaseGeometry.MoonsetHour(age)));

        var moon = MoonPhaseGeometry.MoonDirection(Latitude, age, TimeOfDay);
        var altitude = MoonPhaseGeometry.Altitude(moon);
        var time = SeasonsDiagram.ClockText(TimeOfDay);
        var sunUp = MoonPhaseGeometry.Altitude(MoonPhaseGeometry.SunDirection(Latitude, TimeOfDay)) > 0;
        var sun = _localization.GetString(sunUp ? "MoonPhases_SkyNow_SunUp" : "MoonPhases_SkyNow_SunDown");
        SkyNowText = altitude > 0
            ? _localization.Format("MoonPhases_SkyNow_Up", time, altitude, _localization.GetString(DirectionKey(MoonPhaseGeometry.Azimuth(moon))), sun)
            : _localization.Format("MoonPhases_SkyNow_Down", time, sun);

        var stars = MoonPhaseGeometry.SiderealAngle(Age);
        var aroundSun = MoonPhaseGeometry.EarthOrbitAngle(Age);
        MonthText = _localization.Format("MoonPhases_Month_Text", Age, stars, stars - aroundSun, aroundSun);
    }

    /// <summary>The upcoming phases and tonight's Moon, from the real dates.</summary>
    private void RefreshUpcoming()
    {
        var now = DateTime.UtcNow;
        var culture = LocalizationService.Instance.CurrentCulture;
        Upcoming = new[] { MoonPhase.New, MoonPhase.FirstQuarter, MoonPhase.Full, MoonPhase.LastQuarter }
            .Select(p => (Phase: p, Time: TimeZoneInfo.ConvertTimeFromUtc(MoonPhaseGeometry.NextPhase(now, p), HomeZone)))
            .OrderBy(p => p.Time)
            .Select(p => new UpcomingPhase(
                _localization.GetString(MoonDrawing.PhaseKey(p.Phase)),
                _localization.Format("MoonPhases_Upcoming_When", p.Time.ToString("M", culture), p.Time.ToString("ddd", culture), p.Time.ToString("HH:mm", culture))))
            .ToList();

        var today = MoonPhaseGeometry.AgeAt(now);
        TodayText = _localization.Format("MoonPhases_Today_Text",
            _localization.GetString(MoonDrawing.PhaseKey(MoonPhaseGeometry.PhaseOf(today))), MoonPhaseGeometry.Illumination(today), today);
    }

    /// <summary>The compass direction (one of eight) of an azimuth.</summary>
    private static string DirectionKey(double azimuth) => ((int)Math.Floor((SeasonsGeometry.Normalize360(azimuth) + 22.5) / 45) % 8) switch
    {
        0 => "MoonPhases_Direction_N",
        1 => "MoonPhases_Direction_NE",
        2 => "MoonPhases_Direction_E",
        3 => "MoonPhases_Direction_SE",
        4 => "MoonPhases_Direction_S",
        5 => "MoonPhases_Direction_SW",
        6 => "MoonPhases_Direction_W",
        _ => "MoonPhases_Direction_NW",
    };

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HomeOptionText));
        OnPropertyChanged(nameof(AwayOptionText));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        Refresh();
        RefreshUpcoming();
    }

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    /// <summary>A time zone by IANA id, or a fixed offset where the platform has no zone data (the browser may not).</summary>
    private static TimeZoneInfo FindZone(string id, TimeSpan offset)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);
        }
    }

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}
