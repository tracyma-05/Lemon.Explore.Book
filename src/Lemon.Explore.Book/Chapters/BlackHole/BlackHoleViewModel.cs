using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The black-hole page: which view is shown; the up-close view's tilt, what is drawn and which real black hole it stands
/// for; the orbits view's probe launch and what it reports back (where it is, its clock, a far clock); the lensing view's
/// source, the hole's place and mass; and what is happening. The diagram (<see cref="BlackHoleDiagram"/>) turns the disk,
/// flies the probe and moves the hole while playing. <see cref="Lesson"/> is the spoken walkthrough of the current view,
/// and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class BlackHoleViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);

    /// <summary>The Einstein radius in the picture (view units) at the slider's middle.</summary>
    private const double BaseEinstein = 0.32;

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>The probe moves every frame; the card is rebuilt a few times a second, or when its state changes.</summary>
    private readonly BookStopwatch _sinceRefresh = BookStopwatch.StartNew();

    /// <summary>Brings the card up to date shortly after a burst of changes stops (the last change of a drag or a step).</summary>
    private readonly Avalonia.Threading.DispatcherTimer _lateRefresh = new() { Interval = TimeSpan.FromSeconds(0.3) };

    private string _state = string.Empty;

    /// <summary>Set while a ready launch fills in the start, so those changes do not turn it into a custom one.</summary>
    private bool _launching;

    public BlackHoleViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => BlackHoleLessons.For(this));
        Card = new ObjectCard(narration, Lesson, BlackHoleDiagram.ObjectTitleKey, BlackHoleDiagram.ObjectClipKey);
        ExampleChoice.PropertyChanged += OnExampleChosen;
        LaunchChoice.PropertyChanged += OnLaunchChosen;
        SourceChoice.PropertyChanged += OnSourceChosen;
        LensChoice.PropertyChanged += OnLensChosen;
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
    [NotifyPropertyChangedFor(nameof(IsShadow), nameof(IsOrbits), nameof(IsLensing), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private BlackHoleMode _mode;

    public bool IsShadow
    {
        get => Mode == BlackHoleMode.Shadow;
        set
        {
            if (value) Mode = BlackHoleMode.Shadow;
        }
    }

    public bool IsOrbits
    {
        get => Mode == BlackHoleMode.Orbits;
        set
        {
            if (value) Mode = BlackHoleMode.Orbits;
        }
    }

    public bool IsLensing
    {
        get => Mode == BlackHoleMode.Lensing;
        set
        {
            if (value) Mode = BlackHoleMode.Lensing;
        }
    }

    public string Subtitle => _localization.GetString($"Hole_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Hole_{Mode}_Note");

    public string Hint => _localization.GetString($"Hole_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Hole_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Hole_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Hole_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(BlackHoleMode value)
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

    #region up close

    public OscillationChoice ExampleChoice { get; } = new(BlackHoleExample.SgrA.Id);

    /// <summary>Degrees between the disk's axis and the line of sight.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InclinationText))]
    private double _inclination = 80;

    [ObservableProperty]
    private bool _bendLight = true;

    [ObservableProperty]
    private bool _showDisk = true;

    [ObservableProperty]
    private bool _doppler = true;

    [ObservableProperty]
    private bool _showStars = true;

    /// <summary>The real black hole the picture stands for.</summary>
    [ObservableProperty]
    private string _exampleId = BlackHoleExample.SgrA.Id;

    public string InclinationText => _localization.Format("Hole_Value_Degrees", Number(Inclination, "0"));

    public BlackHoleExample Example => BlackHoleExample.Find(ExampleId) ?? BlackHoleExample.SgrA;

    partial void OnInclinationChanged(double value) => RefreshSoon();

    partial void OnBendLightChanged(bool value) => RefreshSoon(force: true);

    partial void OnShowDiskChanged(bool value) => RefreshSoon(force: true);

    partial void OnDopplerChanged(bool value) => RefreshSoon(force: true);

    partial void OnExampleIdChanged(string value)
    {
        ExampleChoice.Selected = value;
        RefreshSoon(force: true);
    }

    private void OnExampleChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && BlackHoleExample.Find(ExampleChoice.Selected) is { } example) ExampleId = example.Id;
    }

    /// <summary>Tilts the view to a named angle: "Above", "Tilted" or "Edge".</summary>
    [RelayCommand]
    public void TiltTo(string? view) => Inclination = view switch
    {
        "Above" => 8,
        "Tilted" => 50,
        _ => 86,
    };

    #endregion

    #region orbits

    public OscillationChoice LaunchChoice { get; } = new(nameof(ProbeLaunch.Circle));

    [ObservableProperty]
    private ProbeLaunch _launch = ProbeLaunch.Circle;

    [ObservableProperty]
    private double _startRadius = 10;

    [ObservableProperty]
    private double _startRadialSpeed;

    [ObservableProperty]
    private double _startAngularMomentum = BlackHoleModel.CircularAngularMomentum(10);

    /// <summary>Changed to send the probe off again.</summary>
    [ObservableProperty]
    private int _launchCount;

    [ObservableProperty]
    private bool _showRays = true;

    [ObservableProperty]
    private bool _showNewton = true;

    [ObservableProperty]
    private double _probeRadius = 10;

    [ObservableProperty]
    private double _probeTime;

    [ObservableProperty]
    private double _farTime;

    /// <summary>"Orbiting", "Lost" or "Escaped", from the diagram.</summary>
    [ObservableProperty]
    private string _probeState = "Orbiting";

    partial void OnLaunchChanged(ProbeLaunch value)
    {
        LaunchChoice.Selected = value.ToString();
        RefreshSoon(force: true);
    }

    partial void OnStartRadiusChanged(double value) => OnStartChanged();

    partial void OnStartRadialSpeedChanged(double value) => OnStartChanged();

    partial void OnStartAngularMomentumChanged(double value) => OnStartChanged();

    private void OnStartChanged()
    {
        if (!_launching) Launch = ProbeLaunch.Custom;
        RefreshSoon(force: true);
    }

    partial void OnProbeRadiusChanged(double value) => RefreshSoon();

    partial void OnProbeStateChanged(string value) => RefreshSoon(force: true);

    private void OnLaunchChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<ProbeLaunch>(LaunchChoice.Selected, out var launch) && launch != ProbeLaunch.Custom)
        {
            ChooseLaunch(launch);
        }
    }

    /// <summary>Sets the probe up with one of the ready launches and sends it off.</summary>
    public void ChooseLaunch(ProbeLaunch launch)
    {
        _launching = true;
        try
        {
            var (r, rDot, l) = Probe.Launch(launch);
            StartRadius = r;
            StartRadialSpeed = rDot;
            StartAngularMomentum = l;
            Launch = launch;
        }
        finally
        {
            _launching = false;
        }

        Relaunch();
    }

    /// <summary>Sends the probe off again from its start.</summary>
    [RelayCommand]
    private void Relaunch()
    {
        LaunchCount++;
        IsPlaying = true;
    }

    #endregion

    #region lensing

    public OscillationChoice SourceChoice { get; } = new(nameof(LensSource.Galaxy));

    public OscillationChoice LensChoice { get; } = new(LensExample.Galaxy.Id);

    [ObservableProperty]
    private LensSource _lensSource = LensSource.Galaxy;

    [ObservableProperty]
    private double _lensX = -1.6;

    [ObservableProperty]
    private double _lensY = BlackHoleDiagram.TrackY;

    /// <summary>The hole's mass as a slider position, −1 (a quarter) to 1 (four times), 0 the example's own.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinsteinRadius), nameof(MassText))]
    private double _massSlider;

    [ObservableProperty]
    private bool _lensOn = true;

    [ObservableProperty]
    private bool _showRing = true;

    [ObservableProperty]
    private bool _showTruePosition = true;

    [ObservableProperty]
    private string _lensId = LensExample.Galaxy.Id;

    public double MassFactor => Math.Pow(4, MassSlider);

    /// <summary>The Einstein radius in the picture: it grows as the square root of the mass.</summary>
    public double EinsteinRadius => BaseEinstein * Math.Sqrt(MassFactor);

    public string MassText => _localization.Format("Hole_Value_Times", Number(MassFactor, MassFactor < 1 ? "0.00" : "0.0"));

    public LensExample LensExample => LensExample.Find(LensId) ?? LensExample.Galaxy;

    /// <summary>The hole's distance from the line to the source's middle, in Einstein radii.</summary>
    public double Alignment => Math.Sqrt((LensX * LensX) + (LensY * LensY)) / EinsteinRadius;

    partial void OnLensSourceChanged(LensSource value)
    {
        SourceChoice.Selected = value.ToString();
        RefreshSoon(force: true);
    }

    partial void OnLensXChanged(double value) => RefreshSoon();

    partial void OnLensYChanged(double value) => RefreshSoon();

    partial void OnMassSliderChanged(double value) => RefreshSoon();

    partial void OnLensOnChanged(bool value) => RefreshSoon(force: true);

    partial void OnLensIdChanged(string value)
    {
        LensChoice.Selected = value;
        RefreshSoon(force: true);
    }

    private void OnSourceChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<LensSource>(SourceChoice.Selected, out var source)) LensSource = source;
    }

    private void OnLensChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && LensExample.Find(LensChoice.Selected) is { } lens) LensId = lens.Id;
    }

    /// <summary>Starts the hole's drift along the track again.</summary>
    [RelayCommand]
    private void RestartTrack()
    {
        LensX = -BlackHoleDiagram.TrackEnd;
        LensY = BlackHoleDiagram.TrackY;
        IsPlaying = true;
    }

    /// <summary>Puts the hole exactly in front of the source's middle and holds it there: the Einstein ring.</summary>
    [RelayCommand]
    public void LineUp()
    {
        IsPlaying = false;
        LensX = 0;
        LensY = 0;
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

    /// <summary>What the up-close view shows: "Straight", "NoDisk", "Flat", "Above", "Tilted" or "Edge".</summary>
    public string ShadowState => !BendLight ? "Straight" : !ShowDisk ? "NoDisk" : !Doppler ? "Flat" : Inclination < 30 ? "Above" : Inclination < 70 ? "Tilted" : "Edge";

    /// <summary>What the probe is doing: "Lost", "Escaped" or how it was launched.</summary>
    public string OrbitState => ProbeState is "Lost" or "Escaped" ? ProbeState : Launch.ToString();

    /// <summary>How lined up the hole is with the source: "Off", "Far", "Near", "Inside" or "Ring".</summary>
    public string LensState => !LensOn ? "Off" : Alignment < 0.08 ? "Ring" : Alignment < 1 ? "Inside" : Alignment < 2 ? "Near" : "Far";

    private string CurrentState => Mode switch
    {
        BlackHoleMode.Orbits => OrbitState,
        BlackHoleMode.Lensing => LensState,
        _ => ShadowState,
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
            case BlackHoleMode.Orbits:
                RefreshOrbits();
                break;

            case BlackHoleMode.Lensing:
                RefreshLensing();
                break;

            default:
                RefreshShadow();
                break;
        }
    }

    private void RefreshShadow()
    {
        var state = ShadowState;
        PhaseTitle = _localization.GetString($"Hole_Shadow_{state}_Title");
        PhaseDescription = _localization.GetString($"Hole_Shadow_{state}_Text");
        PhaseBrush = state switch
        {
            "Straight" => RedBrush,
            "NoDisk" => VioletBrush,
            "Flat" => OrangeBrush,
            "Above" => CyanBrush,
            _ => GoldBrush,
        };

        var example = Example;
        Stats =
        [
            new(_localization.GetString("Hole_Stat_Example"), _localization.GetString(ExampleNameKey(example.Id))),
            new(_localization.GetString("Hole_Stat_Mass"), _localization.GetString(ExampleMassKey(example.Id))),
            new(_localization.GetString("Hole_Stat_Horizon"), Km(example.HorizonKm)),
            new(_localization.GetString("Hole_Stat_ShadowSize"), _localization.Format("Hole_Value_Shadow", Km(example.HorizonKm * BlackHoleModel.CriticalImpact))),
            new(_localization.GetString("Hole_Stat_FromEarth"), _localization.Format("Hole_Value_MicroArcsec", Number(example.ShadowMicroArcsec, example.ShadowMicroArcsec < 0.01 ? "0.0000" : example.ShadowMicroArcsec < 1 ? "0.000" : "0"))),
            new(_localization.GetString("Hole_Stat_Lap"), Duration(example.PhotonLapSeconds)),
            new(_localization.GetString("Hole_Stat_Inner"), Duration(example.IscoOrbitSeconds)),
            new(_localization.GetString("Hole_Stat_Tilt"), InclinationText),
        ];
    }

    private void RefreshOrbits()
    {
        var state = OrbitState;
        PhaseTitle = _localization.GetString($"Hole_Orbit_{state}_Title");
        PhaseDescription = _localization.GetString($"Hole_Orbit_{state}_Text");
        PhaseBrush = state switch
        {
            "Lost" or "Plunge" => RedBrush,
            "Escaped" or "Escape" => GreenBrush,
            "Isco" => VioletBrush,
            "Rosette" => OrangeBrush,
            "Custom" => CyanBrush,
            _ => GoldBrush,
        };

        var r0 = Math.Max(StartRadius, 2.3);
        var energy = BlackHoleModel.Energy(r0, StartRadialSpeed, StartAngularMomentum);
        var lost = ProbeState == "Lost";
        var rate = lost ? 0 : BlackHoleModel.ClockRate(ProbeRadius, energy);
        Stats =
        [
            new(_localization.GetString("Hole_Stat_Radius"), lost ? _localization.GetString("Hole_Value_Inside") : _localization.Format("Hole_Value_M", Number(ProbeRadius, "0.0"))),
            new(_localization.GetString("Hole_Stat_ProbeClock"), _localization.Format("Hole_Value_M", Number(ProbeTime, "0"))),
            new(_localization.GetString("Hole_Stat_FarClock"), double.IsInfinity(FarTime) ? _localization.GetString("Hole_Clock_Forever") : _localization.Format("Hole_Value_M", Number(FarTime, "0"))),
            new(_localization.GetString("Hole_Stat_ClockRate"), _localization.Format("Hole_Value_Percent", Number(rate * 100, "0"))),
            new(_localization.GetString("Hole_Stat_Start"), _localization.Format("Hole_Value_Start", Number(r0, "0.0"), Number(Math.Sqrt((StartRadialSpeed * StartRadialSpeed) + Math.Pow(StartAngularMomentum / r0, 2)), "0.00"))),
            new(_localization.GetString("Hole_Stat_Energy"), Number(energy, "0.000")),
            new(_localization.GetString("Hole_Stat_Momentum"), Number(StartAngularMomentum, "0.00")),
        ];
    }

    private void RefreshLensing()
    {
        var state = LensState;
        PhaseTitle = _localization.GetString($"Hole_Lens_{state}_Title");
        PhaseDescription = _localization.GetString($"Hole_Lens_{state}_Text");
        PhaseBrush = state switch
        {
            "Off" => RedBrush,
            "Ring" => GoldBrush,
            "Inside" => OrangeBrush,
            "Near" => CyanBrush,
            _ => GreenBrush,
        };

        var lens = LensExample;
        var u = Alignment;
        var theta = lens.EinsteinArcsec(MassFactor);
        var (outer, inner, _, _) = BlackHoleModel.LensImages(Math.Max(u, 1e-6));
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Hole_Stat_Lens"), _localization.GetString(LensNameKey(lens.Id))),
            new(_localization.GetString("Hole_Stat_LensMass"), MassText),
            new(_localization.GetString("Hole_Stat_Einstein"), Angle(theta)),
            new(_localization.GetString("Hole_Stat_Alignment"), _localization.Format("Hole_Value_Einsteins", Number(u, "0.00"))),
        };
        if (LensOn)
        {
            var gain = BlackHoleModel.Magnification(u);
            stats.Add(new(_localization.GetString("Hole_Stat_Brighter"), gain > 99
                ? _localization.GetString("Hole_Value_RingBright")
                : _localization.Format("Hole_Value_Times", Number(gain, "0.00"))));
            stats.Add(new(_localization.GetString("Hole_Stat_Apart"), Angle((outer - inner) * theta)));
        }

        Stats = stats;
    }

    #endregion

    #region formatting

    internal static string ExampleNameKey(string id) => id switch
    {
        "CygX1" => "Hole_Example_CygX1",
        "M87" => "Hole_Example_M87",
        _ => "Hole_Example_SgrA",
    };

    internal static string ExampleMassKey(string id) => id switch
    {
        "CygX1" => "Hole_Example_CygX1_Mass",
        "M87" => "Hole_Example_M87_Mass",
        _ => "Hole_Example_SgrA_Mass",
    };

    internal static string LensNameKey(string id) => id switch
    {
        "Stellar" => "Hole_LensKind_Stellar",
        "Cluster" => "Hole_LensKind_Cluster",
        _ => "Hole_LensKind_Galaxy",
    };

    /// <summary>A distance in km, in millions (or 万) of km, or in astronomical units when it is that big.</summary>
    private string Km(double km)
    {
        const double au = 149_597_870.7;
        if (km < 10_000) return _localization.Format("Hole_Value_Km", Number(km, km < 100 ? "0.0" : "0"));
        if (km < 0.5 * au)
        {
            var unit = double.Parse(_localization.GetString("Hole_Km_Big_Unit"), CultureInfo.InvariantCulture);
            var value = km / unit;
            return _localization.Format("Hole_Value_KmBig", Number(value, value < 10 ? "0.0" : "0"));
        }

        return _localization.Format("Hole_Value_Au", Number(km / au, km / au < 10 ? "0.0" : "0"));
    }

    /// <summary>A time in milliseconds, seconds, minutes, hours or days.</summary>
    private string Duration(double seconds)
    {
        if (seconds < 1) return _localization.Format("Hole_Value_Ms", Number(seconds * 1000, seconds < 0.01 ? "0.00" : "0.0"));
        if (seconds < 120) return _localization.Format("Hole_Value_Seconds", Number(seconds, "0"));
        if (seconds < 7200) return _localization.Format("Hole_Value_Minutes", Number(seconds / 60, "0"));
        if (seconds < 172800) return _localization.Format("Hole_Value_Hours", Number(seconds / 3600, "0.0"));
        return _localization.Format("Hole_Value_Days", Number(seconds / 86400, "0.0"));
    }

    /// <summary>An angle on the sky in arcseconds, or milliarcseconds when small.</summary>
    private string Angle(double arcsec) => arcsec < 1
        ? _localization.Format("Hole_Value_Mas", Number(arcsec * 1000, arcsec < 0.01 ? "0.0" : "0"))
        : _localization.Format("Hole_Value_Arcsec", Number(arcsec, arcsec < 10 ? "0.0" : "0"));

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
        OnPropertyChanged(nameof(InclinationText));
        OnPropertyChanged(nameof(MassText));
        Refresh();
    }

    public void Dispose()
    {
        _lateRefresh.Stop();
        _lateRefresh.Tick -= OnLateRefresh;
        Lesson.Dispose();
        Card.Dispose();
        ExampleChoice.PropertyChanged -= OnExampleChosen;
        LaunchChoice.PropertyChanged -= OnLaunchChosen;
        SourceChoice.PropertyChanged -= OnSourceChosen;
        LensChoice.PropertyChanged -= OnLensChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
