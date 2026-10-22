using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>A retrograde period in the side panel's list: its dates, how long and where, and the day to go to.</summary>
public sealed record RetroPeriodItem(string Dates, string Detail, RetroPeriod Period, bool IsNow);

/// <summary>
/// The retrograde chapter: which view is shown, which planet, the date and the loop round it, playback, the frame round the
/// epicycles, and what is happening. The diagram (<see cref="RetroDiagram"/>) runs <see cref="Day"/> (repeating the loop
/// between <see cref="WindowStart"/> and <see cref="WindowEnd"/> in the sky and from above). <see cref="Lesson"/> is the
/// spoken walkthrough of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class RetrogradeViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x7A, 0x4A);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xA8, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while the date runs.</summary>
    private string _shownKey = string.Empty;

    public RetrogradeViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        _day = Math.Round(RetroModel.Today, 3);
        _trailStart = _day;
        _eastLeft = location.Current.Latitude >= 0;
        Lesson = new LessonPlayer(narration, () => RetrogradeLessons.For(this));
        Card = new ObjectCard(narration, Lesson, RetroDiagram.ObjectTitleKey, RetroDiagram.ObjectClipKey);
        location.Changed += OnLocationChanged;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        PlanetChoice.PropertyChanged += OnPlanetChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        UpdateWindow(keepDay: false);
        RefreshUpcoming();
        Refresh(force: true);
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

    #region mode

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSky), nameof(IsAbove), nameof(IsEpicycle), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private RetroMode _mode;

    public bool IsSky
    {
        get => Mode == RetroMode.Sky;
        set
        {
            if (value) Mode = RetroMode.Sky;
        }
    }

    public bool IsAbove
    {
        get => Mode == RetroMode.Above;
        set
        {
            if (value) Mode = RetroMode.Above;
        }
    }

    public bool IsEpicycle
    {
        get => Mode == RetroMode.Epicycle;
        set
        {
            if (value) Mode = RetroMode.Epicycle;
        }
    }

    public string Subtitle => _localization.Format($"Retro_{Mode}_Subtitle", PlanetName);

    public string ScaleNote => _localization.GetString($"Retro_{Mode}_Note");

    public string Hint => _localization.GetString($"Retro_Hint_{Mode}");

    /// <summary>The shared observer location, for the location chip on the page.</summary>
    public IExploreLocationService Location => _location;

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Retro_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(RetroMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
        if (value == RetroMode.Epicycle) TrailStart = Day;
        else if (Day < WindowStart || Day > WindowEnd) Day = WindowStart;
        Refresh(force: true);
    }

    /// <summary>The spoken walkthrough of the current view.</summary>
    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a click or a drag before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region the planet

    /// <summary>Planet chips: Mercury, Venus, Mars, Jupiter or Saturn.</summary>
    public OscillationChoice PlanetChoice { get; } = new(nameof(RetroPlanet.Mars));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanetName), nameof(Subtitle), nameof(IsInner))]
    private RetroPlanet _planet = RetroPlanet.Mars;

    public string PlanetName => _localization.GetString($"Retro_Planet_{Planet}");

    public bool IsInner => RetroModel.IsInner(Planet);

    partial void OnPlanetChanged(RetroPlanet value)
    {
        PlanetChoice.Selected = value.ToString();
        if (!Lesson.IsApplying) Card.Id = null;
        UpdateWindow(keepDay: Mode == RetroMode.Epicycle);
        TrailStart = Day;
        RefreshUpcoming();
        Refresh(force: true);
    }

    private void OnPlanetChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<RetroPlanet>(PlanetChoice.Selected, out var planet)) Planet = planet;
    }

    #endregion

    #region playback

    /// <summary>Playback speed chips: 0.25, 0.5, 1 or 2 times the normal pace.</summary>
    public OscillationChoice SpeedChoice { get; } = new("1");

    [ObservableProperty]
    private double _speed = 1;

    [ObservableProperty]
    private bool _isPlaying = true;

    [ObservableProperty]
    private bool _showLabels = true;

    [ObservableProperty]
    private bool _showWholePath = true;

    [ObservableProperty]
    private bool _showGuides = true;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    #endregion

    #region the date and the loop

    /// <summary>The date, days since 2000 January 1 12:00 (from the diagram, the timeline or the buttons).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateText))]
    private double _day;

    public string DateText => RetroDiagram.DateText(Day);

    /// <summary>The retrograde period whose loop is shown.</summary>
    [ObservableProperty]
    private RetroPeriod _period = RetroModel.Periods(RetroPlanet.Mars)[0];

    [ObservableProperty]
    private double _windowStart;

    [ObservableProperty]
    private double _windowEnd;

    /// <summary>Round the epicycles: the day the drawn trail begins.</summary>
    [ObservableProperty]
    private double _trailStart;

    partial void OnTrailStartChanged(double value)
    {
        if (Mode == RetroMode.Epicycle) Refresh(force: true);
    }

    public double MinDay => RetroModel.MinDay;

    public double MaxDay => RetroModel.MaxDay;

    /// <summary>The timeline in the sky and from above: how far through the loop's days the date is, 0 … 1.</summary>
    public double LoopProgress
    {
        get => WindowEnd > WindowStart ? Math.Clamp((Day - WindowStart) / (WindowEnd - WindowStart), 0, 1) : 0;
        set => Day = Math.Round(WindowStart + (Math.Clamp(value, 0, 1) * (WindowEnd - WindowStart)), 3);
    }

    partial void OnDayChanged(double value)
    {
        UpdateWindow(keepDay: true);
        OnPropertyChanged(nameof(LoopProgress));
        Refresh();
    }

    /// <summary>
    /// Follows the date with the loop shown: the period whose days hold it (or the nearest). In the sky and from above a date
    /// outside the loop's days is moved to its first day, unless <paramref name="keepDay"/> lets it stand.
    /// </summary>
    private void UpdateWindow(bool keepDay)
    {
        var period = RetroModel.Apparition(Planet, Day);
        if (period != Period || WindowEnd <= WindowStart)
        {
            var half = RetroModel.HalfWindow(Planet);
            Period = period;
            WindowStart = Math.Round(period.Middle - half, 3);
            WindowEnd = Math.Round(period.Middle + half, 3);
        }

        if (!keepDay && Mode != RetroMode.Epicycle && (Day < WindowStart || Day > WindowEnd)) Day = WindowStart;
    }

    /// <summary>Goes to today's date and holds it.</summary>
    [RelayCommand]
    private void Today()
    {
        Day = Math.Round(RetroModel.Today, 3);
        if (Mode != RetroMode.Epicycle) Day = Math.Clamp(Day, WindowStart, WindowEnd);
        else TrailStart = Day;
        IsPlaying = false;
    }

    /// <summary>The loop after (+1) or before (−1) the one shown.</summary>
    [RelayCommand]
    private void StepLoop(string? direction)
    {
        var periods = RetroModel.Periods(Planet);
        var index = periods.ToList().IndexOf(Period) + (direction == "-1" ? -1 : 1);
        if (index < 0 || index >= periods.Count) return;
        GoTo(periods[index]);
    }

    /// <summary>Goes to a loop of the list.</summary>
    [RelayCommand]
    private void JumpToPeriod(RetroPeriodItem? item)
    {
        if (item is not null) GoTo(item.Period);
    }

    /// <summary>A loop from its beginning (in the sky and from above), or round the epicycles a fresh trail ending at its middle.</summary>
    internal void GoTo(RetroPeriod period)
    {
        var half = RetroModel.HalfWindow(Planet);
        if (Mode == RetroMode.Epicycle)
        {
            Day = Math.Round(period.Middle, 3);
            TrailStart = Day;
        }
        else
        {
            Day = Math.Round(period.Middle - half, 3);
        }

        IsPlaying = true;
    }

    /// <summary>The loop again from its first day; round the epicycles a fresh trail from now.</summary>
    [RelayCommand]
    private void Restart()
    {
        if (Mode == RetroMode.Epicycle) TrailStart = Day;
        else Day = WindowStart;
        IsPlaying = true;
    }

    /// <summary>Round the epicycles: the whole pattern at once (the trail as long as it goes).</summary>
    [RelayCommand]
    private void WholePattern() => TrailStart = Math.Max(RetroModel.MinDay, Day - RetroDiagram.PatternDays(Planet));

    /// <summary>The date held at a day (and paused): for the walkthroughs.</summary>
    internal void Hold(double day)
    {
        Day = Math.Round(day, 3);
        IsPlaying = false;
    }

    #endregion

    #region the frame

    /// <summary>Round the epicycles: the Sun held still (Copernicus) rather than Earth (Ptolemy).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEarthFrame))]
    private bool _sunCentered;

    public bool IsEarthFrame
    {
        get => !SunCentered;
        set => SunCentered = !value;
    }

    partial void OnSunCenteredChanged(bool value) => Refresh(force: true);

    #endregion

    #region where

    /// <summary>The sky strip with east on the left (northern hemisphere) or the right (southern), from the shared location.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrientationNote))]
    private bool _eastLeft;

    public string OrientationNote => _localization.Format(EastLeft ? "Retro_Orientation_North" : "Retro_Orientation_South", _location.Current.Name);

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        EastLeft = _location.Current.Latitude >= 0;
        OnPropertyChanged(nameof(OrientationNote));
    }

    #endregion

    #region the loops of the planet

    private IReadOnlyList<RetroPeriodItem> _upcoming = [];
    private string _todayStatus = string.Empty;

    /// <summary>The planet's loops from now: the one going on (if any) and the next few.</summary>
    public IReadOnlyList<RetroPeriodItem> Upcoming
    {
        get => _upcoming;
        private set => SetProperty(ref _upcoming, value);
    }

    /// <summary>Whether the planet is going backwards today, or when it next will.</summary>
    public string TodayStatus
    {
        get => _todayStatus;
        private set => SetProperty(ref _todayStatus, value);
    }

    private void RefreshUpcoming()
    {
        var today = RetroModel.Today;
        Upcoming = RetroModel.Upcoming(Planet, today, 4)
            .Select(p => new RetroPeriodItem(
                _localization.Format("Retro_Upcoming_Range", RetroDiagram.DateText(p.Start), RetroDiagram.DateText(p.End)),
                _localization.Format("Retro_Upcoming_Detail", Number(p.Duration, "0"), Zodiac(RetroModel.Geo(Planet, p.Middle).Longitude)),
                p,
                p.Contains(today)))
            .ToList();

        var during = RetroModel.During(Planet, today);
        var next = RetroModel.Upcoming(Planet, today, 1).FirstOrDefault();
        TodayStatus = during is not null
            ? _localization.Format("Retro_Today_In", PlanetName, RetroDiagram.DateText(during.End))
            : next is not null
                ? _localization.Format("Retro_Today_Next", PlanetName, RetroDiagram.DateText(next.Start), Number(next.Start - today, "0"))
                : string.Empty;
    }

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = GoldBrush;
    private IReadOnlyList<OscillationStat> _stats = [];

    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    public string PhaseTitle
    {
        get => _phaseTitle;
        private set => SetProperty(ref _phaseTitle, value);
    }

    public string PhaseDescription
    {
        get => _phaseDescription;
        private set => SetProperty(ref _phaseDescription, value);
    }

    public IBrush PhaseBrush
    {
        get => _phaseBrush;
        private set => SetProperty(ref _phaseBrush, value);
    }

    public IReadOnlyList<OscillationStat> Stats
    {
        get => _stats;
        private set => SetProperty(ref _stats, value);
    }

    /// <summary>Where the planet is in its loop: drifting east, standing still near a station, or going back west.</summary>
    public string Phase
    {
        get
        {
            var margin = (Period.Duration * 0.06) + 1;
            if (Math.Abs(Day - Period.Start) < margin || Math.Abs(Day - Period.End) < margin) return "Station";
            return Period.Contains(Day) ? "Retro" : "Pro";
        }
    }

    private void Refresh(bool force = false)
    {
        // About four times a second of playback, and whenever the phase changes.
        var perSecond = Mode == RetroMode.Epicycle ? RetroDiagram.PatternDaysPerSecond(Planet) : RetroDiagram.LoopDaysPerSecond(Planet);
        var key = $"{Mode}|{Planet}|{Math.Floor(Day / Math.Max(0.5, perSecond / 4))}|{Phase}|{SunCentered}";
        if (!force && key == _shownKey) return;
        _shownKey = key;

        ClockText = DateText;
        if (Mode == RetroMode.Epicycle) RefreshEpicycle();
        else RefreshLoop();
    }

    private void RefreshLoop()
    {
        var phase = Phase;
        PhaseTitle = _localization.Format($"Retro_Phase_{phase}_Title", PlanetName);
        PhaseDescription = _localization.GetString($"Retro_Phase_{phase}_{(IsInner ? "Inner" : "Outer")}");
        PhaseBrush = phase switch
        {
            "Retro" => OrangeBrush,
            "Station" => CyanBrush,
            _ => GoldBrush,
        };

        var geo = RetroModel.Geo(Planet, Day);
        var rate = RetroModel.Rate(Planet, Day);
        var elongation = RetroModel.Elongation(Planet, Day);
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Retro_Stat_Date"), DateText),
            new(_localization.GetString("Retro_Stat_Where"), _localization.Format("Retro_Stat_Where_Value", Zodiac(geo.Longitude), Number(geo.Longitude, "0.0"))),
            new(_localization.GetString("Retro_Stat_Motion"), Math.Abs(rate) < 0.004
                ? _localization.GetString("Retro_Stat_Motion_Still")
                : _localization.Format(rate > 0 ? "Retro_Stat_Motion_East" : "Retro_Stat_Motion_West", Number(Math.Abs(rate), "0.00"))),
            new(_localization.GetString("Retro_Stat_Distance"), DistanceText(geo.Distance)),
            new(_localization.GetString("Retro_Stat_Elongation"), _localization.Format(elongation >= 0 ? "Retro_Stat_Elongation_East" : "Retro_Stat_Elongation_West", Number(Math.Abs(elongation), "0"))),
            new(_localization.GetString("Retro_Stat_Loop"), _localization.Format("Retro_Stat_Loop_Value", RetroDiagram.DateText(Period.Start), RetroDiagram.DateText(Period.End), Number(Period.Duration, "0"))),
            new(_localization.GetString("Retro_Stat_Progress"), ProgressText()),
        };

        if (Mode == RetroMode.Above)
        {
            stats.Add(new(_localization.GetString("Retro_Stat_Speeds"), _localization.Format("Retro_Stat_Speeds_Value",
                Number(RetroModel.SpeedKmS(null, Day), "0.0"), PlanetName, Number(RetroModel.SpeedKmS(Planet, Day), "0.0"))));
        }

        Stats = stats;
    }

    /// <summary>Where the date is against the loop: days to go, the day of it, or days since.</summary>
    private string ProgressText()
    {
        if (Day < Period.Start)
        {
            var days = Math.Ceiling(Period.Start - Day);
            return days <= 1 ? _localization.GetString("Retro_Stat_Progress_Tomorrow") : _localization.Format("Retro_Stat_Progress_Before", Number(days, "0"));
        }

        if (Day > Period.End)
        {
            var days = Math.Floor(Day - Period.End);
            return days < 1 ? _localization.GetString("Retro_Stat_Progress_JustEnded")
                : days < 2 ? _localization.GetString("Retro_Stat_Progress_Yesterday")
                : _localization.Format("Retro_Stat_Progress_After", Number(days, "0"));
        }

        return _localization.Format("Retro_Stat_Progress_Inside", Number(Math.Floor(Day - Period.Start) + 1, "0"), Number(Math.Round(Period.Duration), "0"));
    }

    private void RefreshEpicycle()
    {
        var frame = SunCentered ? "Sun" : "Earth";
        PhaseTitle = _localization.Format($"Retro_Epicycle_{frame}_Title", PlanetName);
        PhaseDescription = _localization.GetString($"Retro_Epicycle_{frame}_{(IsInner ? "Inner" : "Outer")}");
        PhaseBrush = SunCentered ? GoldBrush : BlueBrush;

        var from = Math.Max(TrailStart, Day - RetroDiagram.PatternDays(Planet));
        var loops = RetroModel.Periods(Planet).Count(p => p.Middle >= from && p.Middle <= Day);
        var synodic = RetroModel.SynodicDays(Planet);
        Stats =
        [
            new(_localization.GetString("Retro_Stat_Date"), DateText),
            new(_localization.GetString("Retro_Stat_Frame"), _localization.GetString(SunCentered ? "Retro_Frame_Sun" : "Retro_Frame_Earth")),
            new(_localization.GetString("Retro_Stat_Distance"), DistanceText(RetroModel.Geo(Planet, Day).Distance)),
            new(_localization.GetString("Retro_Stat_Loops"), _localization.Format("Retro_Stat_Loops_Value", loops, Number((Day - from) / 365.25, "0.0"))),
            new(_localization.GetString("Retro_Stat_Synodic"), _localization.Format("Retro_Stat_Synodic_Value", Number(synodic, "0"), Number(synodic / 30.44, "0.0"))),
            new(_localization.GetString("Retro_Stat_Pattern"), _localization.GetString($"Retro_Pattern_{Planet}")),
        ];
    }

    /// <summary>A distance in AU and in kilometres (millions in English, 万 in Chinese, from <c>Retro_Km_Unit</c>).</summary>
    private string DistanceText(double au)
    {
        var unit = double.Parse(_localization.GetString("Retro_Km_Unit"), CultureInfo.InvariantCulture);
        return _localization.Format("Retro_Stat_Distance_Value", Number(au, "0.00"), Number(au * RetroModel.KmPerAu / unit, "0"));
    }

    private string Zodiac(double longitude) => _localization.GetString(ZodiacKey(ZodiacSky.ConstellationAt(longitude)));

    /// <summary>The resource key of a zodiac constellation's name.</summary>
    private static string ZodiacKey(string id) => "Retro_Zodiac_" + id;

    #endregion

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(PlanetName));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(OrientationNote));
        RefreshUpcoming();
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        PlanetChoice.PropertyChanged -= OnPlanetChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
