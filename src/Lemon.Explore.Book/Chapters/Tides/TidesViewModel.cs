using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>A row of the "what is happening" card: a name and its value.</summary>
public sealed record TideStat(string Name, string Value);

/// <summary>A high or low tide of today at the coast.</summary>
public sealed record TideTableRow(string Name, string Time, string Height, bool IsHigh);

/// <summary>A coming spring or neap tide, from the real date of the new moon, quarter or full moon.</summary>
public sealed record UpcomingTide(string Name, string When);

/// <summary>
/// The tides chapter: which view is shown, playback, and the tide at the current moment of the model. The diagram
/// (<see cref="TidesDiagram"/>) moves <see cref="Day"/>; everything else follows it. <see cref="Lesson"/> is the spoken
/// walkthrough of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class TidesViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush HighBrush = Frozen(0x7F, 0xE0, 0xFF);
    private static readonly IBrush FallingBrush = Frozen(0x5A, 0xA9, 0xFF);
    private static readonly IBrush LowBrush = Frozen(0xE8, 0xC3, 0x8A);
    private static readonly IBrush RisingBrush = Frozen(0x6F, 0xE3, 0xC0);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IBookNavigator _navigator;
    private readonly IExploreLocationService _location;

    public TidesViewModel(IBookNavigator navigator, IExploreLocationService location,
        INarrationService narration)
    {
        _navigator = navigator;
        _location = location;
        Lesson = new LessonPlayer(narration, () => TidesLessons.For(this));
        Card = new ObjectCard(narration, Lesson, TidesDiagram.ObjectTitleKey, TidesDiagram.ObjectClipKey);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        location.Changed += OnLocationChanged;

        // Start at today's Moon and the time on the clock; setting it refreshes everything.
        Day = TodayInModel();
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
    [NotifyPropertyChangedFor(nameof(IsBulges), nameof(IsSpringNeap), nameof(IsCoast), nameof(ShowsTideTimes),
        nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts))]
    private TidesMode _mode;

    public bool IsBulges
    {
        get => Mode == TidesMode.Bulges;
        set
        {
            if (value) Mode = TidesMode.Bulges;
        }
    }

    public bool IsSpringNeap
    {
        get => Mode == TidesMode.SpringNeap;
        set
        {
            if (value) Mode = TidesMode.SpringNeap;
        }
    }

    public bool IsCoast
    {
        get => Mode == TidesMode.Coast;
        set
        {
            if (value) Mode = TidesMode.Coast;
        }
    }

    /// <summary>The views that follow the water through the day, and so can jump to the next high or low tide.</summary>
    public bool ShowsTideTimes => Mode != TidesMode.SpringNeap;

    public string Subtitle => _localization.GetString(Mode switch
    {
        TidesMode.SpringNeap => "Tides_SpringNeap_Subtitle",
        TidesMode.Coast => "Tides_Coast_Subtitle",
        _ => "Tides_Bulges_Subtitle",
    });

    public string ScaleNote => Mode switch
    {
        TidesMode.SpringNeap => _localization.GetString("Tides_SpringNeap_Note"),
        TidesMode.Coast => _localization.Format("Tides_Coast_Note", ExplorePlace.LatitudeText(CoastLatitude), _location.Current.Name),
        _ => _localization.GetString("Tides_Bulges_Note"),
    };

    /// <summary>The coast's sky is that of the shared observer location's latitude.</summary>
    public double CoastLatitude => _location.Current.Latitude;

    /// <summary>The shared observer location, for the location chip on the page.</summary>
    public IExploreLocationService Location => _location;

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CoastLatitude));
        OnPropertyChanged(nameof(ScaleNote));
        RefreshUpcoming();
    }

    /// <summary>What dragging on the diagram does in this view.</summary>
    public string DragHint => _localization.GetString(Mode == TidesMode.SpringNeap ? "Tides_DragHint_Moon" : "Tides_DragHint_Time");

    /// <summary>Bulges view: arrows of the tidal force round Earth.</summary>
    [ObservableProperty]
    private bool _showForces = true;

    [RelayCommand]
    private void OpenMoonPhases() => _navigator.GoToPage(BookContents.ChapterPageOf("Moon-Phases"));

    #endregion

    #region playback

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Days since a new moon at 00:00 (see <see cref="TideGeometry"/>), 0…<see cref="MoonPhaseGeometry.SynodicMonth"/>.</summary>
    [ObservableProperty]
    private double _day;

    public double MonthLength => MoonPhaseGeometry.SynodicMonth;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>
    /// Jumps to a principal phase (by <see cref="MoonPhase"/> name), to today, or to the next high or low tide, and
    /// pauses there.
    /// </summary>
    [RelayCommand]
    private void JumpTo(string? target)
    {
        Day = target switch
        {
            "NextHigh" => MoonPhaseGeometry.Wrap(TideGeometry.Next(Day + (1.0 / 1440), true, IncludeSun).Day),
            "NextLow" => MoonPhaseGeometry.Wrap(TideGeometry.Next(Day + (1.0 / 1440), false, IncludeSun).Day),
            _ when Enum.TryParse<MoonPhase>(target, out var phase) => MoonPhaseGeometry.AgeOf(phase),
            _ => TodayInModel(),
        };
        IsPlaying = false;
    }

    partial void OnModeChanged(TidesMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
        Refresh();
    }

    partial void OnDayChanged(double value) => Refresh();

    /// <summary>The bulges view shows the Moon's tide alone; the others add the Sun's.</summary>
    private bool IncludeSun => Mode != TidesMode.Bulges;

    /// <summary>
    /// Today's Moon in the model, at the time on the clock: the whole number of days nearest to the real age that puts
    /// the fraction at the local time, so the Moon is at most half a day off.
    /// </summary>
    private double TodayInModel()
    {
        var utc = DateTime.UtcNow;
        var now = TimeZoneInfo.ConvertTimeFromUtc(utc, _location.Current.TimeZone);
        var clock = now.TimeOfDay.TotalHours / 24;
        var age = MoonPhaseGeometry.AgeAt(utc);
        return MoonPhaseGeometry.Wrap(Math.Round(age - clock) + clock);
    }

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

    private string _clockText = string.Empty;
    private string _stageTitle = string.Empty;
    private string _stageDescription = string.Empty;
    private IBrush _stageBrush = HighBrush;
    private IReadOnlyList<TideStat> _stats = [];
    private IReadOnlyList<TideTableRow> _todayTides = [];
    private string _todayTidesCaption = string.Empty;
    private string _todayText = string.Empty;
    private IReadOnlyList<UpcomingTide> _upcoming = [];

    /// <summary>Beside the timeline, e.g. "Day 3 · 14:20".</summary>
    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    /// <summary>High water, falling, low water or rising; in spring and neap mode, how big the tides are.</summary>
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

    public IReadOnlyList<TideStat> Stats
    {
        get => _stats;
        private set => SetProperty(ref _stats, value);
    }

    /// <summary>Coast view: the high and low tides of the current day.</summary>
    public IReadOnlyList<TideTableRow> TodayTides
    {
        get => _todayTides;
        private set => SetProperty(ref _todayTides, value);
    }

    public string TodayTidesCaption
    {
        get => _todayTidesCaption;
        private set => SetProperty(ref _todayTidesCaption, value);
    }

    /// <summary>The real Moon today and what it means for the tides.</summary>
    public string TodayText
    {
        get => _todayText;
        private set => SetProperty(ref _todayText, value);
    }

    /// <summary>The coming spring and neap tides, in date order.</summary>
    public IReadOnlyList<UpcomingTide> Upcoming
    {
        get => _upcoming;
        private set => SetProperty(ref _upcoming, value);
    }

    /// <summary>Background facts for the current view.</summary>
    public IReadOnlyList<string> Facts => (Mode switch
    {
        TidesMode.SpringNeap => new[] { "Tides_SpringNeap_Fact1", "Tides_SpringNeap_Fact2", "Tides_SpringNeap_Fact3", "Tides_SpringNeap_Fact4", "Tides_SpringNeap_Fact5" },
        TidesMode.Coast => ["Tides_Coast_Fact1", "Tides_Coast_Fact2", "Tides_Coast_Fact3", "Tides_Coast_Fact4", "Tides_Coast_Fact5"],
        _ => ["Tides_Bulges_Fact1", "Tides_Bulges_Fact2", "Tides_Bulges_Fact3", "Tides_Bulges_Fact4", "Tides_Bulges_Fact5"],
    }).Select(_localization.GetString).ToList();

    private void Refresh()
    {
        var day = Day;
        var hour = TideGeometry.HourOfDay(day);
        ClockText = _localization.Format("Tides_Clock", (int)Math.Floor(day) + 1, SeasonsDiagram.ClockText(hour));

        if (Mode == TidesMode.SpringNeap)
        {
            RefreshSpringNeap(day);
        }
        else
        {
            RefreshTide(day);
        }
    }

    /// <summary>The water at the observer: the stage, the next high and low tides, and the Moon or the range.</summary>
    private void RefreshTide(double day)
    {
        var includeSun = IncludeSun;
        var stage = TideGeometry.StageAt(day, includeSun);
        StageTitle = _localization.GetString(TidesDiagram.StageKey(stage));
        StageDescription = _localization.GetString(TidesDiagram.StageKey(stage) + "_Description");
        StageBrush = stage switch
        {
            TideStage.High => HighBrush,
            TideStage.Low => LowBrush,
            TideStage.Rising => RisingBrush,
            _ => FallingBrush,
        };

        var nextHigh = TideGeometry.Next(day + (1.0 / 1440), true, includeSun);
        var nextLow = TideGeometry.Next(day + (1.0 / 1440), false, includeSun);
        var stats = new List<TideStat>();
        if (Mode == TidesMode.Coast)
        {
            stats.Add(new TideStat(_localization.GetString("Tides_Stat_Level"), _localization.Format("Tides_Stat_Level_Value", TideGeometry.CoastHeight(day))));
        }
        else
        {
            stats.Add(new TideStat(_localization.GetString("Tides_Stat_Moon"), MoonText(day)));
        }

        stats.Add(new TideStat(_localization.GetString("Tides_Stat_NextHigh"), TurnText(day, nextHigh)));
        stats.Add(new TideStat(_localization.GetString("Tides_Stat_NextLow"), TurnText(day, nextLow)));
        if (Mode == TidesMode.Coast)
        {
            stats.Add(new TideStat(_localization.GetString("Tides_Stat_Range"),
                _localization.Format("Tides_Stat_Range_Value", TideGeometry.Range(day) * TideGeometry.CoastMetres, KindName(day))));
        }
        else
        {
            var (h, m) = TidesDrawing.HoursMinutes(TideGeometry.TidePeriodHours);
            stats.Add(new TideStat(_localization.GetString("Tides_Stat_Period"), _localization.Format("Tides_Duration", h, m)));
        }

        Stats = stats;

        // The coast's tide table for the current day.
        var today = Math.Floor(day);
        TodayTidesCaption = _localization.Format("Tides_Today_Caption", (int)today + 1);
        TodayTides = TideGeometry.Turns(today, today + 1)
            .Select(t => new TideTableRow(
                _localization.GetString(t.IsHigh ? "Tides_Table_High" : "Tides_Table_Low"),
                SeasonsDiagram.ClockText(TideGeometry.HourOfDay(t.Day)),
                _localization.Format("Tides_Metres", TideGeometry.ToCoastMetres(t.Height)),
                t.IsHigh))
            .ToList();
    }

    /// <summary>How big the tides are: spring, neap or between, with the Moon's phase and the angle to the Sun.</summary>
    private void RefreshSpringNeap(double day)
    {
        var fraction = TideGeometry.SpringFraction(day);
        var key = KindKey(day);
        StageTitle = _localization.GetString(key);
        StageDescription = _localization.GetString(key + "_Description");
        StageBrush = Frozen(SpaceDrawing.Lerp(TidesDrawing.Low, TidesDrawing.High, fraction));

        var moonAngle = TideGeometry.MoonAngle(day);
        var folded = SeasonsGeometry.Normalize360(moonAngle) % 180;
        var between = Math.Min(folded, 180 - folded);
        var phase = MoonPhaseGeometry.PhaseOf(day);
        Stats =
        [
            new TideStat(_localization.GetString("Tides_Stat_Phase"),
                _localization.Format("Tides_Stat_Phase_Value", _localization.GetString(MoonDrawing.PhaseKey(phase)), day)),
            new TideStat(_localization.GetString("Tides_Stat_Angle"), _localization.Format("Tides_Stat_Angle_Value", between)),
            new TideStat(_localization.GetString("Tides_Stat_Range"),
                _localization.Format("Tides_Stat_RangeOf", TideGeometry.Range(day) * TideGeometry.CoastMetres,
                    TideGeometry.SpringRange * TideGeometry.CoastMetres, TideGeometry.NeapRange * TideGeometry.CoastMetres)),
            new TideStat(_localization.GetString("Tides_Stat_HighWater"),
                _localization.Format("Tides_Metres", TideGeometry.ToCoastMetres(TideGeometry.Range(day) / 2))),
        ];
    }

    private static string KindKey(double day)
    {
        var fraction = TideGeometry.SpringFraction(day);
        return fraction > 0.8 ? "Tides_Kind_Spring"
            : fraction < 0.2 ? "Tides_Kind_Neap"
            : TideGeometry.IsRangeGrowing(day) ? "Tides_Kind_Growing" : "Tides_Kind_Shrinking";
    }

    private string KindName(double day) => _localization.GetString(KindKey(day));

    /// <summary>Where the Moon is for the observer on the equator: how high, east or west, or below the horizon.</summary>
    private string MoonText(double day)
    {
        var hourAngle = TideGeometry.MoonHourAngle(day);
        var altitude = 90 - Math.Abs(hourAngle);
        if (altitude < 0) return _localization.Format("Tides_Stat_Moon_Down", -altitude);
        if (altitude > 85) return _localization.GetString("Tides_Stat_Moon_Overhead");
        return _localization.Format(hourAngle < 0 ? "Tides_Stat_Moon_East" : "Tides_Stat_Moon_West", altitude);
    }

    /// <summary>A coming high or low tide: its time and how long until then.</summary>
    private string TurnText(double day, TideTurn turn)
    {
        var (h, m) = TidesDrawing.HoursMinutes((turn.Day - day) * 24);
        return _localization.Format("Tides_Stat_Turn_Value", SeasonsDiagram.ClockText(TideGeometry.HourOfDay(turn.Day)),
            _localization.Format("Tides_Duration", h, m));
    }

    /// <summary>The coming spring and neap tides from the real dates of the principal phases, and today's Moon.</summary>
    private void RefreshUpcoming()
    {
        var now = DateTime.UtcNow;
        var culture = LocalizationService.Instance.CurrentCulture;
        Upcoming = new[] { MoonPhase.New, MoonPhase.FirstQuarter, MoonPhase.Full, MoonPhase.LastQuarter }
            .Select(p => (Phase: p, Time: TimeZoneInfo.ConvertTimeFromUtc(MoonPhaseGeometry.NextPhase(now, p), _location.Current.TimeZone)))
            .OrderBy(p => p.Time)
            .Select(p => new UpcomingTide(
                _localization.Format(p.Phase is MoonPhase.New or MoonPhase.Full
                    ? "Tides_Upcoming_Spring" : "Tides_Upcoming_Neap", _localization.GetString(MoonDrawing.PhaseKey(p.Phase))),
                _localization.Format("Tides_Upcoming_When", p.Time.ToString("M", culture), p.Time.ToString("ddd", culture))))
            .ToList();

        var age = MoonPhaseGeometry.AgeAt(now);
        TodayText = _localization.Format("Tides_Today_Text", age, KindName(age));
    }

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        Refresh();
        RefreshUpcoming();
    }

    private static IBrush Frozen(byte r, byte g, byte b) => Frozen(Color.FromRgb(r, g, b));

    private static IBrush Frozen(Color color) => new ImmutableSolidColorBrush(color);

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _location.Changed -= OnLocationChanged;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}
