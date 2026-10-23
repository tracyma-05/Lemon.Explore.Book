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

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>
/// The flight chapter: which view is shown; the wind tunnel's wing (shape, upside down, angle of attack, air speed) and what
/// it shows; the flying plane's controls (angle of attack, throttle, load) and where it is, which the diagram
/// (<see cref="FlightDiagram"/>) moves as it flies; the 3D plane's bank, speed and demonstration; and what is happening.
/// <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in the diagram
/// (<see cref="Card"/>) explains it.
/// </summary>
public partial class FlightViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>The flying plane moves every frame; its card is rebuilt a few times a second, or when its state changes.</summary>
    private readonly Stopwatch _sinceRefresh = Stopwatch.StartNew();

    private string _forcesState = string.Empty;

    public FlightViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => FlightLessons.For(this));
        Card = new ObjectCard(narration, Lesson, FlightDiagram.ObjectTitleKey, FlightDiagram.ObjectClipKey);
        ShapeChoice.PropertyChanged += OnShapeChosen;
        DemoChoice.PropertyChanged += OnDemoChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        ApplyScenario("Cruise");
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
    [NotifyPropertyChangedFor(nameof(IsWing), nameof(IsForces), nameof(IsTurn), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private FlightMode _mode;

    public bool IsWing
    {
        get => Mode == FlightMode.Wing;
        set
        {
            if (value) Mode = FlightMode.Wing;
        }
    }

    public bool IsForces
    {
        get => Mode == FlightMode.Forces;
        set
        {
            if (value) Mode = FlightMode.Forces;
        }
    }

    public bool IsTurn
    {
        get => Mode == FlightMode.Turn;
        set
        {
            if (value) Mode = FlightMode.Turn;
        }
    }

    public string Subtitle => _localization.GetString($"Flight_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Flight_{Mode}_Note");

    public string Hint => _localization.GetString($"Flight_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Flight_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Flight_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Flight_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(FlightMode value)
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

    #region the wing

    public OscillationChoice ShapeChoice { get; } = new(nameof(WingShape.Cambered));

    [ObservableProperty]
    private WingShape _shape = WingShape.Cambered;

    [ObservableProperty]
    private bool _inverted;

    /// <summary>The wing's angle of attack, degrees.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlphaText))]
    private double _alpha = 4;

    /// <summary>The tunnel's air speed, km/h.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AirSpeedText))]
    private double _airSpeed = 200;

    [ObservableProperty]
    private bool _showPressure = true;

    [ObservableProperty]
    private bool _showStreamlines = true;

    [ObservableProperty]
    private bool _showSmoke = true;

    [ObservableProperty]
    private bool _showTimelines;

    [ObservableProperty]
    private bool _showArrows;

    public string AlphaText => FlightDiagram.Degrees(Alpha);

    public string AirSpeedText => _localization.Format("Flight_Value_Kmh", Number(AirSpeed, "0"));

    partial void OnShapeChanged(WingShape value)
    {
        ShapeChoice.Selected = value.ToString();
        Refresh();
    }

    partial void OnInvertedChanged(bool value) => Refresh();

    partial void OnAlphaChanged(double value) => Refresh();

    partial void OnAirSpeedChanged(double value) => Refresh();

    private void OnShapeChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<WingShape>(ShapeChoice.Selected, out var shape)) Shape = shape;
    }

    /// <summary>Releases columns of smoke together to test whether the air over the top and underneath meet again.</summary>
    [RelayCommand]
    private void EqualTransit() => ShowTimelines = true;

    #endregion

    #region the flying plane

    /// <summary>The throttle, 0 to 1.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThrottleText))]
    private double _throttle = 0.7;

    /// <summary>The plane's angle of attack, degrees.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaneAlphaText))]
    private double _planeAlpha = 1.3;

    /// <summary>The plane's mass with its load, kg.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MassText))]
    private double _mass = 1000;

    /// <summary>Air speed, m/s (moved by the diagram as the plane flies).</summary>
    [ObservableProperty]
    private double _speed = 55;

    /// <summary>Height above the runway, m.</summary>
    [ObservableProperty]
    private double _altitude = 1000;

    /// <summary>Climb angle, rad.</summary>
    [ObservableProperty]
    private double _pathAngle;

    [ObservableProperty]
    private bool _onGround;

    /// <summary>Distance from the start of the runway, m.</summary>
    [ObservableProperty]
    private double _positionX = 2500;

    public string ThrottleText => _localization.Format("Flight_Value_Percent", Number(Throttle * 100, "0"));

    public string PlaneAlphaText => FlightDiagram.Degrees(PlaneAlpha);

    public string MassText => _localization.Format("Flight_Value_Kg", Number(Mass, "0"));

    /// <summary>The forces on the plane now.</summary>
    public FlightForces Forces => Trainer.Forces(new FlightState(PositionX, Altitude, Speed, PathAngle, OnGround), PlaneAlpha, Throttle, Mass);

    /// <summary>Metres per second up (negative going down).</summary>
    public double ClimbRate => Speed * Math.Sin(PathAngle);

    partial void OnThrottleChanged(double value) => RefreshForcesSoon(force: true);

    partial void OnPlaneAlphaChanged(double value) => RefreshForcesSoon(force: true);

    partial void OnMassChanged(double value) => RefreshForcesSoon(force: true);

    partial void OnSpeedChanged(double value) => RefreshForcesSoon();

    partial void OnAltitudeChanged(double value) => RefreshForcesSoon();

    partial void OnPathAngleChanged(double value) => RefreshForcesSoon();

    partial void OnOnGroundChanged(bool value) => RefreshForcesSoon(force: true);

    /// <summary>
    /// Puts the plane into a flight: "Takeoff" (at the start of the runway, full power), "Climb", "Cruise" (trimmed level),
    /// "Glide" (engine off at the best glide), "Stall" (slow, nose high) or "Landing" (on the approach to the runway).
    /// </summary>
    [RelayCommand]
    public void ApplyScenario(string? name)
    {
        switch (name)
        {
            case "Takeoff":
                PlaneAlpha = 0;
                Throttle = 1;
                SetState(20, 0, 0, 0, true);
                break;

            case "Climb":
                PlaneAlpha = 6;
                Throttle = 1;
                Steady(300, 1200);
                break;

            case "Glide":
                PlaneAlpha = Math.Round(Trainer.AlphaFor(Trainer.BestGlideLift), 1);
                Throttle = 0;
                Steady(1500, 3000);
                break;

            case "Stall":
                PlaneAlpha = 18;
                Throttle = 0.3;
                SetState(3000, 1200, 30, 0, false);
                break;

            case "Landing":
            {
                PlaneAlpha = 7;
                Throttle = 0.15;
                var eq = Trainer.Equilibrium(PlaneAlpha, Throttle, Mass, 100, 35);
                var (speed, path) = eq ?? (35, -0.06);
                const double height = 110;
                var distance = height / Math.Tan(Math.Max(0.02, -path));
                SetState(160 - distance, height, speed, path, false);
                break;
            }

            default:
            {
                Throttle = 0.7;
                SetState(2500, 1000, 55, 0, false);
                if (Trainer.TrimLevel(55, Mass, 1000) is { } trim)
                {
                    PlaneAlpha = trim.Alpha;
                    Throttle = trim.Throttle;
                }

                break;
            }
        }

        RefreshForcesSoon(force: true);
    }

    /// <summary>The plane in the steady flight its controls settle into, at a height and place.</summary>
    private void Steady(double height, double x)
    {
        var eq = Trainer.Equilibrium(PlaneAlpha, Throttle, Mass, height, 50);
        var (speed, path) = eq ?? (45, 0);
        SetState(x, height, speed, path, false);
    }

    private void SetState(double x, double height, double speed, double path, bool onGround)
    {
        OnGround = onGround;
        PositionX = x;
        Altitude = height;
        Speed = speed;
        PathAngle = path;
    }

    /// <summary>Trims the plane for level flight at a speed (m/s), lifting it off the runway first if needed.</summary>
    [RelayCommand]
    private void Trim(double speed)
    {
        var height = OnGround || Altitude < 50 ? 800 : Altitude;
        if (Trainer.TrimLevel(speed, Mass, height) is not { } trim) return;
        PlaneAlpha = trim.Alpha;
        Throttle = trim.Throttle;
        SetState(OnGround ? 2500 : PositionX, height, speed, 0, false);
        RefreshForcesSoon(force: true);
    }

    /// <summary>Levels off at the speed flown now.</summary>
    [RelayCommand]
    private void LevelOff() => Trim(Math.Max(Speed, Trainer.StallSpeed(Mass, Altitude) * 1.2));

    #endregion

    #region the turn

    /// <summary>The bank asked for, degrees (right wing down positive).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BankText))]
    private double _bank;

    /// <summary>The 3D plane's air speed, km/h.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TurnSpeedText))]
    private double _turnSpeed = 180;

    public OscillationChoice DemoChoice { get; } = new(nameof(TurnDemo.Turn));

    [ObservableProperty]
    private TurnDemo _demo = TurnDemo.Turn;

    public string BankText => Math.Abs(Bank) < 0.5
        ? _localization.GetString("Flight_Value_Level")
        : _localization.Format("Flight_Value_Bank", Number(Math.Abs(Bank), "0"), _localization.GetString(Bank > 0 ? "Flight_Value_Right" : "Flight_Value_Left"));

    public string TurnSpeedText => _localization.Format("Flight_Value_Kmh", Number(TurnSpeed, "0"));

    partial void OnBankChanged(double value) => Refresh();

    partial void OnTurnSpeedChanged(double value) => Refresh();

    partial void OnDemoChanged(TurnDemo value)
    {
        DemoChoice.Selected = value.ToString();
        Refresh();
    }

    private void OnDemoChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<TurnDemo>(DemoChoice.Selected, out var demo)) Demo = demo;
    }

    [RelayCommand]
    private void LevelWings()
    {
        Demo = TurnDemo.Turn;
        Bank = 0;
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

    private void Refresh()
    {
        switch (Mode)
        {
            case FlightMode.Forces:
                RefreshForces();
                break;

            case FlightMode.Turn:
                RefreshTurn();
                break;

            default:
                RefreshWing();
                break;
        }
    }

    /// <summary>What the wing is doing: "Lift", "NearStall", "Stall", "NoLift", "Down".</summary>
    public string WingState
    {
        get
        {
            if (WingSection.Separation(Shape, Inverted, Alpha) > 0.3) return "Stall";
            var cl = WingSection.Lift(Shape, Inverted, Alpha);
            if (Math.Abs(cl) < 0.05) return "NoLift";
            if (cl < 0) return "Down";
            return Alpha > WingSection.StallAlpha(Shape, Inverted) - 2.5 ? "NearStall" : "Lift";
        }
    }

    private void RefreshWing()
    {
        var state = WingState;
        PhaseTitle = _localization.GetString($"Flight_Wing_{state}_Title");
        PhaseDescription = _localization.GetString($"Flight_Wing_{state}_Text");
        PhaseBrush = state switch
        {
            "Stall" => RedBrush,
            "NearStall" => OrangeBrush,
            "NoLift" or "Down" => CyanBrush,
            _ => GreenBrush,
        };

        var cl = WingSection.Lift(Shape, Inverted, Alpha);
        var cd = WingSection.Drag(Shape, Inverted, Alpha);
        var v = AirSpeed / 3.6;
        var perSquareMetre = 0.5 * 1.225 * v * v * cl;

        // The fastest air over the wing's surface, as a multiple of the wind.
        var airfoil = WingSection.Airfoil(Shape, Inverted, Alpha);
        var fastest = Enumerable.Range(1, 359).Max(i => airfoil.Surface(i * Math.PI / 180).Velocity.Length);
        Stats =
        [
            new(_localization.GetString("Flight_Stat_Aoa"), AlphaText),
            new(_localization.GetString("Flight_Stat_Cl"), Number(cl, "0.00")),
            new(_localization.GetString("Flight_Stat_Cd"), Number(cd, "0.000")),
            new(_localization.GetString("Flight_Stat_LiftToDrag"), Math.Abs(cd) < 1e-6 ? "—" : Number(cl / cd, "0")),
            new(_localization.GetString("Flight_Stat_PerSquareMetre"), _localization.Format("Flight_Value_LiftPerM2", FlightDiagram.Newtons(perSquareMetre), Number(perSquareMetre / Trainer.G, "0"))),
            new(_localization.GetString("Flight_Stat_Fastest"), _localization.Format("Flight_Value_Times", Number(Math.Min(fastest, 9), "0.0"), Number(Math.Min(fastest, 9) * AirSpeed, "0"))),
            new(_localization.GetString("Flight_Stat_StallAngle"), FlightDiagram.Degrees(WingSection.StallAlpha(Shape, Inverted))),
            new(_localization.GetString("Flight_Stat_ZeroLift"), FlightDiagram.Degrees(WingSection.ZeroLiftAlpha(Shape, Inverted))),
        ];
    }

    /// <summary>What the flying plane is doing: "Parked", "Rolling", "Braking", "Stall", "Glide", "Climb", "Descend", "Level".</summary>
    public string ForcesState
    {
        get
        {
            var f = Forces;
            if (OnGround) return Speed < 0.5 ? "Parked" : Throttle < 0.05 ? "Braking" : "Rolling";
            if (f.Separation > 0.25) return "Stall";
            if (Throttle < 0.02) return "Glide";
            var climb = ClimbRate;
            return climb > 0.5 ? "Climb" : climb < -0.5 ? "Descend" : "Level";
        }
    }

    /// <summary>The flying plane's card, at most a few times a second unless something changed that the card names.</summary>
    private void RefreshForcesSoon(bool force = false)
    {
        if (Mode != FlightMode.Forces) return;
        var state = ForcesState;
        if (!force && state == _forcesState && _sinceRefresh.Elapsed.TotalSeconds < 0.3) return;
        RefreshForces();
    }

    private void RefreshForces()
    {
        _sinceRefresh.Restart();
        var state = ForcesState;
        _forcesState = state;
        PhaseTitle = _localization.GetString($"Flight_Forces_{state}_Title");
        PhaseDescription = _localization.GetString($"Flight_Forces_{state}_Text");
        PhaseBrush = state switch
        {
            "Stall" => RedBrush,
            "Climb" => GreenBrush,
            "Descend" or "Glide" => CyanBrush,
            "Braking" => OrangeBrush,
            _ => GoldBrush,
        };

        var f = Forces;
        Stats =
        [
            new(_localization.GetString("Flight_Stat_Speed"), FlightDiagram.Kmh(Speed)),
            new(_localization.GetString("Flight_Stat_Altitude"), FlightDiagram.Metres(Altitude)),
            new(_localization.GetString("Flight_Stat_Climb"), _localization.Format("Flight_Value_Climb", Number(ClimbRate, "+0.0;-0.0;0.0"), Number(PathAngle * 180 / Math.PI, "+0.0;-0.0;0.0"))),
            new(_localization.GetString("Flight_Stat_Aoa"), FlightDiagram.Degrees(f.Alpha)),
            new(_localization.GetString("Flight_Stat_Lift"), FlightDiagram.Newtons(f.Lift)),
            new(_localization.GetString("Flight_Stat_Weight"), FlightDiagram.Newtons(f.Weight)),
            new(_localization.GetString("Flight_Stat_Thrust"), FlightDiagram.Newtons(f.Thrust)),
            new(_localization.GetString("Flight_Stat_Drag"), FlightDiagram.Newtons(f.Drag)),
            new(_localization.GetString("Flight_Stat_LiftToDrag"), f.Drag < 1 ? "—" : Number(f.Lift / f.Drag, "0.0")),
            new(_localization.GetString("Flight_Stat_StallSpeed"), FlightDiagram.Kmh(Trainer.StallSpeed(Mass, Altitude))),
            new(_localization.GetString("Flight_Stat_Density"), _localization.Format("Flight_Value_Density", Number(f.Density, "0.000"))),
        ];
    }

    /// <summary>What the 3D plane is doing: "Roll", "Pitch", "Yaw", "Straight", "Turning", "Steep", "TurnStall".</summary>
    public string TurnState
    {
        get
        {
            if (Demo != TurnDemo.Turn) return Demo.ToString();
            if (Math.Abs(Bank) < 2) return "Straight";
            if (TurnSpeed / 3.6 < Trainer.StallSpeed(1000, 0, Trainer.LoadFactor(Bank))) return "TurnStall";
            return Math.Abs(Bank) >= 45 ? "Steep" : "Turning";
        }
    }

    private void RefreshTurn()
    {
        var state = TurnState;
        PhaseTitle = _localization.GetString($"Flight_Turn_{state}_Title");
        PhaseDescription = _localization.GetString($"Flight_Turn_{state}_Text");
        PhaseBrush = state switch
        {
            "TurnStall" => RedBrush,
            "Steep" => OrangeBrush,
            "Roll" => CyanBrush,
            "Pitch" or "Straight" => GreenBrush,
            "Yaw" => Frozen(0xB0, 0x8C, 0xFF),
            _ => GoldBrush,
        };

        var v = TurnSpeed / 3.6;
        var bank = Demo == TurnDemo.Turn ? Bank : 0;
        var n = Trainer.LoadFactor(bank);
        var radius = Trainer.TurnRadius(v, bank);
        var rate = Math.Abs(Trainer.TurnRate(v, bank));
        Stats =
        [
            new(_localization.GetString("Flight_Stat_Bank"), BankText),
            new(_localization.GetString("Flight_Stat_Speed"), TurnSpeedText),
            new(_localization.GetString("Flight_Stat_LoadFactor"), _localization.Format("Flight_Value_G", Number(n, "0.00"))),
            new(_localization.GetString("Flight_Stat_Radius"), FlightDiagram.Metres(radius)),
            new(_localization.GetString("Flight_Stat_TurnRate"), rate < 0.01 ? "—" : _localization.Format("Flight_Value_DegPerSecond", Number(rate, "0.0"))),
            new(_localization.GetString("Flight_Stat_Circle"), rate < 0.01 ? "—" : _localization.Format("Flight_Value_Seconds", Number(360 / rate, "0"))),
            new(_localization.GetString("Flight_Stat_LiftNeeded"), FlightDiagram.Newtons(1000 * Trainer.G * n)),
            new(_localization.GetString("Flight_Stat_TurnStall"), FlightDiagram.Kmh(Trainer.StallSpeed(1000, 0, n))),
        ];
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
        OnPropertyChanged(nameof(AlphaText));
        OnPropertyChanged(nameof(AirSpeedText));
        OnPropertyChanged(nameof(ThrottleText));
        OnPropertyChanged(nameof(PlaneAlphaText));
        OnPropertyChanged(nameof(MassText));
        OnPropertyChanged(nameof(BankText));
        OnPropertyChanged(nameof(TurnSpeedText));
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        ShapeChoice.PropertyChanged -= OnShapeChosen;
        DemoChoice.PropertyChanged -= OnDemoChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
