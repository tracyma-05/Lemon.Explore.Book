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

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>
/// The relativity chapter: which view is shown; the speed, shared by all three; whose point of view the light clock and the
/// train are seen from; what each view draws; the twins' star and where in the trip we are; and what is happening. The
/// diagram (<see cref="RelativityDiagram"/>) runs the clocks, the flash and the trip and reports back the ticks, the
/// flash's phase and the trip's time. <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a
/// thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class RelativityViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);

    /// <summary>The train's length where it stands still, in the model's units (300 m each; light crosses one in 1 µs).</summary>
    private const double TrainLength = 2;

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>The trip moves every frame; the card is rebuilt a few times a second, or when its state changes.</summary>
    private readonly Stopwatch _sinceRefresh = Stopwatch.StartNew();

    /// <summary>Brings the card up to date shortly after a burst of changes stops (the last change of a drag or a step).</summary>
    private readonly Avalonia.Threading.DispatcherTimer _lateRefresh = new() { Interval = TimeSpan.FromSeconds(0.3) };

    private string _state = string.Empty;

    public RelativityViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => RelativityLessons.For(this));
        Card = new ObjectCard(narration, Lesson, RelativityDiagram.ObjectTitleKey, RelativityDiagram.ObjectClipKey);
        FrameChoice.PropertyChanged += OnFrameChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        _lateRefresh.Tick += OnLateRefresh;
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
    [NotifyPropertyChangedFor(nameof(IsLightClock), nameof(IsSimultaneity), nameof(IsTwins), nameof(HasFrame), nameof(Subtitle), nameof(ScaleNote),
        nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote), nameof(GroundFrameText), nameof(ShipFrameText))]
    private RelativityMode _mode;

    public bool IsLightClock
    {
        get => Mode == RelativityMode.LightClock;
        set
        {
            if (value) Mode = RelativityMode.LightClock;
        }
    }

    public bool IsSimultaneity
    {
        get => Mode == RelativityMode.Simultaneity;
        set
        {
            if (value) Mode = RelativityMode.Simultaneity;
        }
    }

    public bool IsTwins
    {
        get => Mode == RelativityMode.Twins;
        set
        {
            if (value) Mode = RelativityMode.Twins;
        }
    }

    /// <summary>The light clock and the train can be seen from either side; the twins' diagram is Earth's.</summary>
    public bool HasFrame => Mode != RelativityMode.Twins;

    public string Subtitle => _localization.GetString($"Rel_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Rel_{Mode}_Note");

    public string Hint => _localization.GetString($"Rel_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Rel_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Rel_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Rel_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(RelativityMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        TrainHold = double.NaN;
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

    #region speed and point of view

    /// <summary>The ship's (train's) speed as a fraction of light's, 0 to 0.99.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BetaText), nameof(Gamma))]
    private double _beta = 0.6;

    public double Gamma => RelativityModel.Gamma(Beta);

    public string BetaText => _localization.Format("Rel_Value_Beta", Number(Beta, "0.00"));

    partial void OnBetaChanged(double value) => RefreshSoon();

    /// <summary>Sets the speed to one of the shortcuts (a fraction of light's speed, as text).</summary>
    [RelayCommand]
    public void SetSpeed(string? beta)
    {
        if (double.TryParse(beta, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Beta = Math.Clamp(value, 0, 0.99);
    }

    public OscillationChoice FrameChoice { get; } = new(nameof(RelativityFrame.Ground));

    [ObservableProperty]
    private RelativityFrame _frame = RelativityFrame.Ground;

    /// <summary>The frame chips' words: the ground or the ship in the light clock, the platform or the train in the train view.</summary>
    public string GroundFrameText => _localization.GetString(Mode == RelativityMode.Simultaneity ? "Rel_Frame_Platform" : "Rel_Frame_Ground");

    public string ShipFrameText => _localization.GetString(Mode == RelativityMode.Simultaneity ? "Rel_Frame_Train" : "Rel_Frame_Ship");

    partial void OnFrameChanged(RelativityFrame value)
    {
        FrameChoice.Selected = value.ToString();
        RefreshSoon(force: true);
    }

    private void OnFrameChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<RelativityFrame>(FrameChoice.Selected, out var frame)) Frame = frame;
    }

    #endregion

    #region light clock

    [ObservableProperty]
    private bool _showTriangle = true;

    [ObservableProperty]
    private bool _showTrail = true;

    /// <summary>Changed to set both clocks back to 0.</summary>
    [ObservableProperty]
    private int _resetCount;

    [ObservableProperty]
    private int _stillTicks;

    [ObservableProperty]
    private int _movingTicks;

    partial void OnStillTicksChanged(int value) => RefreshSoon();

    partial void OnMovingTicksChanged(int value) => RefreshSoon();

    [RelayCommand]
    public void ResetClocks()
    {
        ResetCount++;
        IsPlaying = true;
    }

    #endregion

    #region the train

    [ObservableProperty]
    private bool _showClocks;

    [ObservableProperty]
    private bool _showLengths = true;

    /// <summary>Changed to run the flash again.</summary>
    [ObservableProperty]
    private int _flashCount;

    /// <summary>"Before", "Flight", "RearHit" or "BothHit", from the diagram.</summary>
    [ObservableProperty]
    private string _trainPhase = "Before";

    /// <summary>The frame time the train is held at (µs), or NaN to let it run.</summary>
    [ObservableProperty]
    private double _trainHold = double.NaN;

    partial void OnTrainPhaseChanged(string value) => RefreshSoon(force: true);

    partial void OnShowClocksChanged(bool value) => RefreshSoon(force: true);

    [RelayCommand]
    public void FlashAgain()
    {
        TrainHold = double.NaN;
        FlashCount++;
        IsPlaying = true;
    }

    /// <summary>When (frame time, µs) the flash reaches the back and the front of the train, in the frame being watched.</summary>
    public (double Rear, double Front) HitTimes()
    {
        if (Frame == RelativityFrame.Ship) return (TrainLength / 2, TrainLength / 2);
        var (rear, _, front, _) = RelativityModel.TrainHits(TrainLength, Beta);
        return (rear, front);
    }

    /// <summary>Holds the run at a moment: "Before" the flash, just after the back is hit ("Rear"), or once both are ("Both").</summary>
    public void HoldTrain(string moment)
    {
        var (rear, front) = HitTimes();
        TrainHold = moment switch
        {
            "Before" => -0.4,
            "Flight" => rear * 0.6,
            "Rear" => rear + ((front - rear) * 0.35),
            _ => front,
        };
    }

    #endregion

    #region the twins

    /// <summary>The star's distance, light-years.</summary>
    [ObservableProperty]
    private double _distance = 4;

    /// <summary>Earth's time since the ship left, years.</summary>
    [ObservableProperty]
    private double _twinTime;

    [ObservableProperty]
    private bool _showSignals;

    [ObservableProperty]
    private bool _showNowLines;

    [ObservableProperty]
    private bool _showAxes;

    public string DistanceText => _localization.Format("Rel_Value_Ly", Number(Distance, "0.0"));

    public TwinTrip Trip => RelativityModel.Trip(Distance, RelativityDiagram.TripSpeed(Beta));

    partial void OnDistanceChanged(double value)
    {
        OnPropertyChanged(nameof(DistanceText));
        RefreshSoon();
    }

    partial void OnTwinTimeChanged(double value) => RefreshSoon();

    partial void OnShowSignalsChanged(bool value) => RefreshSoon(force: true);

    [RelayCommand]
    public void RestartTrip()
    {
        TwinTime = 0;
        IsPlaying = true;
    }

    /// <summary>Holds the trip at a moment: "Start", "Out" (halfway there), "Turn" (just before it), "Back" (halfway home) or "Home".</summary>
    public void HoldTrip(string moment)
    {
        var trip = Trip;
        IsPlaying = false;
        TwinTime = moment switch
        {
            "Start" => 0,
            "Out" => trip.Turn * 0.5,
            "Turn" => trip.Turn * 0.995,
            "Back" => trip.Turn * 1.5,
            _ => trip.EarthYears,
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

    /// <summary>The light clock: "Still" (hardly moving), "Ground" or "Ship" (whose view).</summary>
    public string ClockState => Beta < 0.02 ? "Still" : Frame == RelativityFrame.Ground ? "Ground" : "Ship";

    /// <summary>The train: "Before", "Flight", "RearHit", "Together" (both ends hit at once) or "Apart" (the front later).</summary>
    public string TrainState => TrainPhase switch
    {
        "BothHit" => Frame == RelativityFrame.Ship || Beta < 0.02 ? "Together" : "Apart",
        "RearHit" or "Flight" or "Before" => TrainPhase,
        _ => "Before",
    };

    /// <summary>The twins: "Start", "Out", "Back" or "Home".</summary>
    public string TwinState
    {
        get
        {
            var trip = Trip;
            return TwinTime <= 1e-6 ? "Start" : TwinTime < trip.Turn ? "Out" : TwinTime < trip.EarthYears - 1e-6 ? "Back" : "Home";
        }
    }

    private string CurrentState => Mode switch
    {
        RelativityMode.Simultaneity => TrainState,
        RelativityMode.Twins => TwinState,
        _ => ClockState,
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
            case RelativityMode.Simultaneity:
                RefreshTrain();
                break;

            case RelativityMode.Twins:
                RefreshTwins();
                break;

            default:
                RefreshClock();
                break;
        }
    }

    private void RefreshClock()
    {
        var state = ClockState;
        PhaseTitle = _localization.GetString($"Rel_Clock_{state}_Title");
        PhaseDescription = _localization.GetString($"Rel_Clock_{state}_Text");
        PhaseBrush = state switch
        {
            "Still" => GreenBrush,
            "Ship" => CyanBrush,
            _ => GoldBrush,
        };

        var gamma = Gamma;
        var ground = Frame == RelativityFrame.Ground;
        var (muon, muonWithout) = RelativityModel.MuonRange(Beta);
        Stats =
        [
            new(_localization.GetString("Rel_Stat_Speed"), SpeedText()),
            new(_localization.GetString("Rel_Stat_Gamma"), GammaText(gamma)),
            new(_localization.GetString("Rel_Stat_GroundTicks"), Number(ground ? StillTicks : MovingTicks, "0")),
            new(_localization.GetString("Rel_Stat_ShipTicks"), Number(ground ? MovingTicks : StillTicks, "0")),
            new(_localization.GetString("Rel_Stat_YearOnShip"), _localization.Format("Rel_Value_Days", Number(365.25 / gamma, "0"))),
            new(_localization.GetString("Rel_Stat_Muon"), Beta < 0.02
                ? _localization.GetString("Rel_Value_MuonSlow")
                : _localization.Format("Rel_Value_Muon", Km(muon), Km(muonWithout))),
        ];
    }

    private void RefreshTrain()
    {
        var state = TrainState;
        PhaseTitle = _localization.GetString($"Rel_Train_{state}_Title");
        PhaseDescription = _localization.GetString($"Rel_Train_{state}_Text");
        PhaseBrush = state switch
        {
            "Before" => CyanBrush,
            "Flight" => GoldBrush,
            "RearHit" => OrangeBrush,
            "Together" => GreenBrush,
            _ => VioletBrush,
        };

        var gamma = Gamma;
        var onTrain = Frame == RelativityFrame.Ship;
        var rest = TrainLength * RelativityDiagram.MetresPerUnit;
        var (rear, front) = HitTimes();
        Stats =
        [
            new(_localization.GetString("Rel_Stat_Speed"), SpeedText()),
            new(_localization.GetString("Rel_Stat_Gamma"), GammaText(gamma)),
            new(_localization.GetString("Rel_Stat_TrainLength"), _localization.Format("Rel_Value_M", Number(onTrain ? rest : rest / gamma, "0"))),
            new(_localization.GetString("Rel_Stat_PlatformLength"), _localization.Format("Rel_Value_M", Number(onTrain ? rest / gamma : rest, "0"))),
            new(_localization.GetString("Rel_Stat_RearHit"), _localization.Format("Rel_Value_Us", Number(rear, "0.00"))),
            new(_localization.GetString("Rel_Stat_FrontHit"), _localization.Format("Rel_Value_Us", Number(front, "0.00"))),
            new(_localization.GetString("Rel_Stat_Gap"), _localization.Format("Rel_Value_Us", Number(front - rear, "0.00"))),
        ];
    }

    private void RefreshTwins()
    {
        var state = TwinState;
        PhaseTitle = _localization.GetString($"Rel_Twin_{state}_Title");
        PhaseDescription = _localization.GetString($"Rel_Twin_{state}_Text");
        PhaseBrush = state switch
        {
            "Start" => GreenBrush,
            "Out" => CyanBrush,
            "Back" => VioletBrush,
            _ => GoldBrush,
        };

        var trip = Trip;
        var t = Math.Clamp(TwinTime, 0, trip.EarthYears);
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Rel_Stat_Speed"), _localization.Format("Rel_Value_Beta", Number(trip.Beta, "0.00"))),
            new(_localization.GetString("Rel_Stat_Gamma"), GammaText(trip.Gamma)),
            new(_localization.GetString("Rel_Stat_StarEarth"), _localization.Format("Rel_Value_Ly", Number(trip.Distance, "0.0"))),
            new(_localization.GetString("Rel_Stat_StarShip"), _localization.Format("Rel_Value_Ly", Number(trip.ShipDistance, "0.00"))),
            new(_localization.GetString("Rel_Stat_EarthWaits"), _localization.Format("Rel_Value_Years", Number(trip.EarthYears, "0.0"))),
            new(_localization.GetString("Rel_Stat_ShipLives"), _localization.Format("Rel_Value_Years", Number(trip.ShipYears, "0.0"))),
            new(_localization.GetString("Rel_Stat_Younger"), _localization.Format("Rel_Value_Years", Number(trip.Younger, "0.0"))),
        };

        if (ShowSignals)
        {
            var toShip = Enumerable.Range(1, Math.Max(0, (int)Math.Ceiling(trip.EarthYears) - 1)).Count(k => trip.ArrivesAtShip(k) is { } at && at <= t);
            var toEarth = Enumerable.Range(1, Math.Max(0, (int)Math.Floor(trip.ShipYears))).Count(k => k * trip.Gamma < trip.EarthYears && trip.ArrivesAtEarth(k * trip.Gamma) <= t);
            stats.Add(new(_localization.GetString("Rel_Stat_SignalsShip"), Number(toShip, "0")));
            stats.Add(new(_localization.GetString("Rel_Stat_SignalsEarth"), Number(toEarth, "0")));
        }

        Stats = stats;
    }

    #endregion

    #region formatting

    private string SpeedText() => _localization.Format("Rel_Value_Speed", Number(Beta, "0.00"), Number(Beta * RelativityModel.LightKmPerSecond, "#,##0"));

    private string GammaText(double gamma) => _localization.Format("Rel_Value_Gamma", Number(gamma, gamma < 10 ? "0.000" : "0.00"));

    private string Km(double km) => km < 1
        ? _localization.Format("Rel_Value_M", Number(km * 1000, "0"))
        : _localization.Format("Rel_Value_Km", Number(km, km < 10 ? "0.0" : "0"));

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(ChartCaption));
        OnPropertyChanged(nameof(ChartNote));
        OnPropertyChanged(nameof(BetaText));
        OnPropertyChanged(nameof(DistanceText));
        OnPropertyChanged(nameof(GroundFrameText));
        OnPropertyChanged(nameof(ShipFrameText));
        Refresh();
    }

    public void Dispose()
    {
        _lateRefresh.Stop();
        _lateRefresh.Tick -= OnLateRefresh;
        Lesson.Dispose();
        Card.Dispose();
        FrameChoice.PropertyChanged -= OnFrameChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
