using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// The expanding-universe page: which view is shown; the Hubble view's moment, home galaxy and what it shows; the
/// redshift view's galaxy (its redshift) and how far its light has come; the history view's universe, time axis and the
/// moment looked at; and what is happening. The diagram (<see cref="ExpansionDiagram"/>) moves the moment while playing.
/// <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in the diagram
/// (<see cref="Card"/>) explains it.
/// </summary>
public partial class ExpansionViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);
    private static readonly IBrush PinkBrush = Frozen(0xFF, 0x7E, 0xC0);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);

    /// <summary>The highest redshift the slider reaches.</summary>
    private const double MaxRedshift = 15;

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>The moment moves every frame while playing; the card is rebuilt a few times a second, or when its state changes.</summary>
    private readonly Stopwatch _sinceRefresh = Stopwatch.StartNew();

    private string _state = string.Empty;

    /// <summary>Brings the card up to date shortly after a burst of changes stops (the last change of a drag or a step).</summary>
    private readonly Avalonia.Threading.DispatcherTimer _lateRefresh = new() { Interval = TimeSpan.FromSeconds(0.3) };

    /// <summary>Set while a galaxy button sets the redshift, so the redshift change does not drop the galaxy's name.</summary>
    private bool _choosingSource;

    public ExpansionViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => ExpansionLessons.For(this));
        Card = new ObjectCard(narration, Lesson, ExpansionDiagram.ObjectTitleKey, ExpansionDiagram.ObjectClipKey);
        SourceChoice.PropertyChanged += OnSourceChosen;
        UniverseChoice.PropertyChanged += OnUniverseChosen;
        AxisChoice.PropertyChanged += OnAxisChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        _lateRefresh.Tick += OnLateRefresh;
        _cosmicTime = Universe.Ours.Age;
        Refresh();
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
    [NotifyPropertyChangedFor(nameof(IsHubble), nameof(IsRedshift), nameof(IsHistory), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private ExpansionMode _mode;

    public bool IsHubble
    {
        get => Mode == ExpansionMode.Hubble;
        set
        {
            if (value) Mode = ExpansionMode.Hubble;
        }
    }

    public bool IsRedshift
    {
        get => Mode == ExpansionMode.Redshift;
        set
        {
            if (value) Mode = ExpansionMode.Redshift;
        }
    }

    public bool IsHistory
    {
        get => Mode == ExpansionMode.History;
        set
        {
            if (value) Mode = ExpansionMode.History;
        }
    }

    public string Subtitle => _localization.GetString($"Expand_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Expand_{Mode}_Note");

    public string Hint => _localization.GetString($"Expand_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Expand_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Expand_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Expand_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(ExpansionMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
        Refresh();
    }

    /// <summary>The spoken walkthrough of the current view.</summary>
    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a click or a drag before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    [ObservableProperty]
    private bool _isPlaying = true;

    [ObservableProperty]
    private bool _showLabels = true;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    #endregion

    #region hubble

    /// <summary>How far through the span the patch is, 0 (0.6 of today's size) to 1 (now).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HubbleWhenText))]
    private double _hubbleProgress;

    /// <summary>How long ago the moment shown is ("now" at the end).</summary>
    public string HubbleWhenText
    {
        get
        {
            var ago = Universe.Ours.Age - HubbleSpan.Time(HubbleProgress);
            return ago > 0.02 ? _localization.Format("Expand_Ago", ExpansionFormat.Time(ago)) : _localization.GetString("Expand_Now");
        }
    }

    public string JourneyText => _localization.Format("Expand_Value_Percent", Number(Journey * 100, "0"));

    /// <summary>The galaxy everything is seen from.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAwayFromHome))]
    private GalaxyCell _home = GalaxyCell.MilkyWay;

    [ObservableProperty]
    private bool _showArrows = true;

    [ObservableProperty]
    private bool _showOverlay;

    [ObservableProperty]
    private bool _showGrid;

    [ObservableProperty]
    private bool _showRulers = true;

    /// <summary>Home is a galaxy other than the Milky Way.</summary>
    public bool IsAwayFromHome => !Home.IsMilkyWay;

    partial void OnHubbleProgressChanged(double value) => RefreshSoon();

    partial void OnHomeChanged(GalaxyCell value) => RefreshSoon(force: true);

    partial void OnShowOverlayChanged(bool value) => RefreshSoon(force: true);

    /// <summary>Moves home back to the Milky Way.</summary>
    [RelayCommand]
    private void GoHome() => Home = GalaxyCell.MilkyWay;

    /// <summary>Starts the span again from the beginning.</summary>
    [RelayCommand]
    private void RestartSpan()
    {
        HubbleProgress = 0;
        IsPlaying = true;
    }

    #endregion

    #region redshift

    public OscillationChoice SourceChoice { get; } = new("Halfway");

    /// <summary>How much the galaxy's light is stretched when it arrives (z).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RedshiftText), nameof(RedshiftSlider))]
    private double _redshift = 1;

    /// <summary>The galaxy picked from the list, or null for a redshift chosen by hand.</summary>
    [ObservableProperty]
    private string? _sourceId = "Halfway";

    /// <summary>How far the light has come, 0 (it just left) to 1 (it is here).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JourneyText))]
    private double _journey;

    public string RedshiftText => _localization.Format("Expand_Value_Redshift", Number(Redshift, Redshift < 0.1 ? "0.0000" : Redshift < 10 ? "0.00" : "0.0"), Number(1 + Redshift, Redshift < 0.1 ? "0.0000" : "0.00"));

    /// <summary>The redshift as a slider position: 1 + z grows by equal factors along it.</summary>
    public double RedshiftSlider
    {
        get => Math.Log(1 + Redshift) / Math.Log(1 + MaxRedshift);
        set
        {
            var z = Math.Exp(Math.Clamp(value, 0, 1) * Math.Log(1 + MaxRedshift)) - 1;
            Redshift = Math.Round(z, z < 0.1 ? 4 : z < 1 ? 3 : 2);
        }
    }

    partial void OnRedshiftChanged(double value)
    {
        if (!_choosingSource)
        {
            var match = LightSource.All.FirstOrDefault(s => Math.Abs(s.Redshift - value) < 1e-9);
            SourceId = match?.Id;
        }

        Journey = 0;
        IsPlaying = true;
        RefreshSoon(force: true);
    }

    partial void OnSourceIdChanged(string? value)
    {
        SourceChoice.Selected = value ?? string.Empty;
        RefreshSoon(force: true);
    }

    partial void OnJourneyChanged(double value) => RefreshSoon();

    private void OnSourceChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && LightSource.Find(SourceChoice.Selected) is { } source) ChooseSource(source.Id);
    }

    /// <summary>Picks one of the listed galaxies (its redshift with it).</summary>
    public void ChooseSource(string id)
    {
        if (LightSource.Find(id) is not { } source) return;
        _choosingSource = true;
        try
        {
            SourceId = source.Id;
            Redshift = source.Redshift;
        }
        finally
        {
            _choosingSource = false;
        }
    }

    /// <summary>Sends the light off again.</summary>
    [RelayCommand]
    private void Replay()
    {
        Journey = 0;
        IsPlaying = true;
    }

    #endregion

    #region history

    public OscillationChoice UniverseChoice { get; } = new(nameof(UniverseKind.Ours));

    public OscillationChoice AxisChoice { get; } = new(nameof(TimeAxis.Linear));

    [ObservableProperty]
    private UniverseKind _universeKind = UniverseKind.Ours;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeSlider))]
    private TimeAxis _axis = TimeAxis.Linear;

    /// <summary>The moment looked at, Gyr after the start.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeSlider), nameof(CosmicTimeText))]
    private double _cosmicTime;

    /// <summary>The moment as a place along the axis, 0 to 1 (so the slider runs like the picture).</summary>
    public double TimeSlider
    {
        get => CosmicHistory.Fraction(Axis, CosmicTime);
        set
        {
            var end = Math.Min(CosmicHistory.End, Universe.Of(UniverseKind).CrunchTime ?? double.MaxValue);
            CosmicTime = Math.Clamp(CosmicHistory.Time(Axis, value), CosmicHistory.Earliest(Axis), end);
        }
    }

    public string CosmicTimeText => ExpansionFormat.Time(CosmicTime);

    partial void OnUniverseKindChanged(UniverseKind value)
    {
        UniverseChoice.Selected = value.ToString();
        var end = Math.Min(CosmicHistory.End, Universe.Of(value).CrunchTime ?? double.MaxValue);
        if (CosmicTime > end) CosmicTime = end;
        RefreshSoon(force: true);
    }

    partial void OnAxisChanged(TimeAxis value)
    {
        AxisChoice.Selected = value.ToString();
        RefreshSoon(force: true);
    }

    partial void OnCosmicTimeChanged(double value) => RefreshSoon();

    private void OnUniverseChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<UniverseKind>(UniverseChoice.Selected, out var kind)) UniverseKind = kind;
    }

    private void OnAxisChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<TimeAxis>(AxisChoice.Selected, out var axis)) Axis = axis;
    }

    /// <summary>Moves the moment to a named point of history ("Start", "Cmb", "Now", "Future") and holds it there.</summary>
    [RelayCommand]
    public void JumpTo(string? point)
    {
        var universe = Universe.Of(UniverseKind);
        var end = Math.Min(CosmicHistory.End, universe.CrunchTime ?? double.MaxValue);
        IsPlaying = false;
        CosmicTime = point switch
        {
            "Start" => CosmicHistory.Earliest(Axis),
            "Cmb" => universe.TimeAt(1 / (1 + Universe.CmbRedshift)),
            "Future" => Math.Min(end - 0.05, universe.Age + 10),
            _ => universe.Age,
        };
    }

    #endregion

    #region what is happening

    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = GoldBrush;
    private IReadOnlyList<OscillationStat> _stats = [];

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

    /// <summary>What the Hubble view shows: "Home", "NewHome", "Overlay".</summary>
    public string HubbleState => !Home.IsMilkyWay ? "NewHome" : ShowOverlay ? "Overlay" : "Home";

    /// <summary>What kind of light the redshift view follows: "Nearby", "Visible", "Infrared", "Deep".</summary>
    public string RedshiftState
    {
        get
        {
            if (Redshift < 0.05) return "Nearby";
            var arrives = SpectralLine.Wave.Nanometres * (1 + Redshift);
            if (arrives <= 750) return "Visible";
            return Redshift < 6 ? "Infrared" : "Deep";
        }
    }

    /// <summary>The stage of history at the moment looked at.</summary>
    public CosmicEra Era => CosmicHistory.EraAt(Universe.Of(UniverseKind), CosmicTime);

    private string CurrentState => Mode switch
    {
        ExpansionMode.Redshift => RedshiftState,
        ExpansionMode.History => Era.ToString(),
        _ => HubbleState,
    };

    /// <summary>The card, at most a few times a second unless something changed that it names.</summary>
    private void RefreshSoon(bool force = false)
    {
        if (!force && CurrentState == _state && _sinceRefresh.Elapsed.TotalSeconds < 0.25)
        {
            _lateRefresh.Stop();
            _lateRefresh.Start();
            return;
        }

        Refresh();
    }

    private void OnLateRefresh(object? sender, EventArgs e)
    {
        _lateRefresh.Stop();
        Refresh();
    }

    private void Refresh()
    {
        _lateRefresh.Stop();
        _sinceRefresh.Restart();
        _state = CurrentState;
        switch (Mode)
        {
            case ExpansionMode.Redshift:
                RefreshRedshift();
                break;

            case ExpansionMode.History:
                RefreshHistory();
                break;

            default:
                RefreshHubble();
                break;
        }
    }

    private void RefreshHubble()
    {
        var state = HubbleState;
        PhaseTitle = _localization.GetString($"Expand_Hubble_{state}_Title");
        PhaseDescription = _localization.GetString($"Expand_Hubble_{state}_Text");
        PhaseBrush = state switch
        {
            "NewHome" => CyanBrush,
            "Overlay" => GreenBrush,
            _ => GoldBrush,
        };

        var u = Universe.Ours;
        var a = HubbleSpan.Scale(HubbleProgress);
        var rate = u.Hubble(a);
        var ago = u.Age - HubbleSpan.Time(HubbleProgress);
        var home = GalaxyField.At(Home);
        string Ruler(int di, int dj)
        {
            var g = GalaxyField.At(new GalaxyCell(Home.I + di, Home.J + dj));
            var d = (g.Position - home.Position).Length * a;
            return _localization.Format("Expand_Value_DistanceSpeed", ExpansionFormat.Distance(d), ExpansionFormat.Speed(rate * d));
        }

        var homeText = Home.IsMilkyWay
            ? _localization.GetString("Expand_Value_HomeMilkyWay")
            : _localization.Format("Expand_Value_HomeElsewhere", ExpansionFormat.Distance(home.Position.Length * a));
        Stats =
        [
            new(_localization.GetString("Expand_Stat_When"), ago > 0.02 ? _localization.Format("Expand_Ago", ExpansionFormat.Time(ago)) : _localization.GetString("Expand_Now")),
            new(_localization.GetString("Expand_Stat_Size"), _localization.Format("Expand_Value_TimesToday", Number(a, "0.00"))),
            new(_localization.GetString("Expand_Stat_Rate"), _localization.Format("Expand_Value_Rate", Number(rate, "0"))),
            new(_localization.GetString("Expand_Stat_Home"), homeText),
            new(_localization.GetString("Expand_Stat_Near"), Ruler(1, 0)),
            new(_localization.GetString("Expand_Stat_Middle"), Ruler(-2, 1)),
            new(_localization.GetString("Expand_Stat_Far"), Ruler(3, -2)),
        ];
    }

    private void RefreshRedshift()
    {
        var state = RedshiftState;
        PhaseTitle = _localization.GetString($"Expand_Redshift_{state}_Title");
        PhaseDescription = _localization.GetString($"Expand_Redshift_{state}_Text");
        PhaseBrush = state switch
        {
            "Nearby" => GreenBrush,
            "Visible" => OrangeBrush,
            "Infrared" => RedBrush,
            _ => VioletBrush,
        };

        var u = Universe.Ours;
        var aE = Universe.ScaleForRedshift(Redshift);
        var trip = ExpansionDiagram.LightTrip.At(Redshift, Journey);
        var speed = u.RecessionSpeedNow(aE);
        var arrives = SpectralLine.Wave.Nanometres * (1 + Redshift);
        var band = _localization.GetString(arrives > 750 ? "Expand_Value_Infrared" : "Expand_Value_Visible");
        Stats =
        [
            new(_localization.GetString("Expand_Stat_Redshift"), RedshiftText),
            new(_localization.GetString("Expand_Stat_LeftAt"), _localization.Format("Expand_Value_UniverseAge", ExpansionFormat.Time(u.TimeAt(aE)), ExpansionFormat.Size(aE))),
            new(_localization.GetString("Expand_Stat_Travel"), ExpansionFormat.Time(u.LookbackTime(aE))),
            new(_localization.GetString("Expand_Stat_Then"), ExpansionFormat.Distance(trip.SourceThen)),
            new(_localization.GetString("Expand_Stat_Now"), ExpansionFormat.Distance(u.ComovingDistance(aE))),
            new(_localization.GetString("Expand_Stat_Speed"), _localization.Format("Expand_Value_SpeedC", ExpansionFormat.Speed(speed), Number(speed / Universe.C, speed < Universe.C * 0.1 ? "0.000" : "0.00"))),
            new(_localization.GetString("Expand_Stat_Hbeta"), _localization.Format("Expand_Value_Nm", Number(arrives, "0"), band)),
            new(_localization.GetString("Expand_Stat_Journey"), _localization.Format("Expand_Value_Percent", Number(Journey * 100, "0"))),
        ];
    }

    private void RefreshHistory()
    {
        var era = Era;
        var universe = Universe.Of(UniverseKind);
        PhaseTitle = _localization.GetString(ExpansionDiagram.EraTitleKey(era));
        PhaseDescription = _localization.GetString($"Expand_Era_{era}_Text");
        PhaseBrush = era switch
        {
            CosmicEra.Start or CosmicEra.Plasma => OrangeBrush,
            CosmicEra.DarkAges => VioletBrush,
            CosmicEra.Dawn => CyanBrush,
            CosmicEra.Speeding or CosmicEra.FutureFast => PinkBrush,
            CosmicEra.Now => GreenBrush,
            CosmicEra.Contracting or CosmicEra.Crunch or CosmicEra.Ended => RedBrush,
            _ => GoldBrush,
        };

        var time = CosmicTime;
        var a = universe.ScaleAt(time);
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Expand_Stat_Universe"), _localization.GetString($"Expand_Universe_{UniverseKind}")),
            new(_localization.GetString("Expand_Stat_Time"), ExpansionFormat.Time(time)),
        };
        if (era != CosmicEra.Ended && a > 0)
        {
            var rate = universe.HubbleAtTime(time);
            var q = universe.Deceleration(a);
            stats.Add(new(_localization.GetString("Expand_Stat_Size"), ExpansionFormat.Size(a)));
            stats.Add(new(_localization.GetString("Expand_Stat_Temperature"), ExpansionFormat.Kelvin(Universe.Temperature(a))));
            stats.Add(new(_localization.GetString("Expand_Stat_Rate"), rate < 0
                ? _localization.Format("Expand_Value_Falling", Number(-rate, "0"))
                : _localization.Format("Expand_Value_Rate", ExpansionFormat.Short(rate))));
            stats.Add(new(_localization.GetString("Expand_Stat_Trend"), _localization.GetString(
                universe.IsContracting(time) ? "Expand_Value_Collapsing" : q < 0 ? "Expand_Value_SpeedingUp" : "Expand_Value_SlowingDown")));
            if (time < universe.Age - 0.01 && UniverseKind == UniverseKind.Ours)
            {
                stats.Add(new(_localization.GetString("Expand_Stat_SeenAs"), _localization.Format("Expand_Value_SeenAs", ExpansionFormat.Short(Universe.RedshiftForScale(a)))));
            }
        }

        stats.Add(new(_localization.GetString("Expand_Stat_AgeToday"), ExpansionFormat.Time(universe.Age)));
        if (universe.CrunchTime is { } crunch) stats.Add(new(_localization.GetString("Expand_Stat_Crunch"), ExpansionFormat.Time(crunch)));
        Stats = stats;
    }

    #endregion

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(ChartCaption));
        OnPropertyChanged(nameof(ChartNote));
        OnPropertyChanged(nameof(RedshiftText));
        OnPropertyChanged(nameof(CosmicTimeText));
        OnPropertyChanged(nameof(HubbleWhenText));
        OnPropertyChanged(nameof(JourneyText));
        Refresh();
    }

    public void Dispose()
    {
        _lateRefresh.Stop();
        _lateRefresh.Tick -= OnLateRefresh;
        Lesson.Dispose();
        Card.Dispose();
        SourceChoice.PropertyChanged -= OnSourceChosen;
        UniverseChoice.PropertyChanged -= OnUniverseChosen;
        AxisChoice.PropertyChanged -= OnAxisChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
