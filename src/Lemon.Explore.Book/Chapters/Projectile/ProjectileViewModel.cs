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

namespace Lemon.Explore.Book.Chapters.Projectile;

/// <summary>
/// The projectile chapter: which view is shown, where the ball is thrown and what it is, the angle, speed and height of the
/// shot, the "fall together" scenes, and what is happening. The diagram (<see cref="ProjectileDiagram"/>) plays the
/// flights and moves <see cref="Time"/>; aiming its cannon sets <see cref="Angle"/> and <see cref="LaunchSpeed"/>.
/// <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in the diagram
/// (<see cref="Card"/>) explains it.
/// </summary>
public partial class ProjectileViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x8B, 0xE0, 0x8F);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9F, 0x5A);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x7A, 0x6B);
    private static readonly IBrush VioletBrush = Frozen(0xB3, 0x9D, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>Launch mode's shot, and the same shot without air.</summary>
    private Flight _flight = ProjectilePhysics.Fly(20, 45, 0, 9.81, 0);
    private Flight _airless = ProjectilePhysics.Fly(20, 45, 0, 9.81, 0);

    /// <summary>The angle that throws the farthest, and how far, for the current settings.</summary>
    private double _bestAngle = 45;
    private double _bestRange;

    /// <summary>The tenth of a second of playback the card was last worked out for, so it is not rebuilt every frame.</summary>
    private long _shownTenth = -1;

    private readonly IExploreLocationService _location;

    /// <summary>The shared observer location: Earth's gravity here (shown and changed on the left page).</summary>
    public IExploreLocationService Location => _location;

    public ProjectileViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        Lesson = new LessonPlayer(narration, () => ProjectileLessons.For(this));
        Card = new ObjectCard(narration, Lesson, ProjectileDiagram.ObjectTitleKey, ProjectileDiagram.ObjectClipKey);
        location.Changed += OnLocationChanged;
        PlaceChoice.PropertyChanged += OnPlaceChosen;
        BallChoice.PropertyChanged += OnBallChosen;
        SceneChoice.PropertyChanged += OnSceneChosen;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        Rebuild();
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
    [NotifyPropertyChangedFor(nameof(IsLaunch), nameof(IsBestAngle), nameof(IsTogether), nameof(IsThrowing), nameof(ChartAngle), nameof(ModeKey),
        nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts))]
    private ProjectileMode _mode;

    public bool IsLaunch
    {
        get => Mode == ProjectileMode.Launch;
        set
        {
            if (value) Mode = ProjectileMode.Launch;
        }
    }

    public bool IsBestAngle
    {
        get => Mode == ProjectileMode.BestAngle;
        set
        {
            if (value) Mode = ProjectileMode.BestAngle;
        }
    }

    public bool IsTogether
    {
        get => Mode == ProjectileMode.Together;
        set
        {
            if (value) Mode = ProjectileMode.Together;
        }
    }

    /// <summary>Launch and best angle: a ball thrown with the angle, speed, height, ball and air of the panel.</summary>
    public bool IsThrowing => Mode != ProjectileMode.Together;

    /// <summary>The mode, or for the "fall together" view its scene: picks the subtitle, notes and facts.</summary>
    public string ModeKey => Mode == ProjectileMode.Together ? $"Together_{Scene}" : Mode.ToString();

    public string Subtitle => _localization.GetString($"Proj_{ModeKey}_Subtitle");

    public string ScaleNote => _localization.GetString($"Proj_{ModeKey}_Note");

    public string DragHint => _localization.GetString($"Proj_DragHint_{ModeKey}");

    partial void OnModeChanged(ProjectileMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Rebuild();
    }

    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports aiming or a click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region place, ball and playback

    /// <summary>Where it is thrown, by <see cref="ProjectilePlace"/> name.</summary>
    public OscillationChoice PlaceChoice { get; } = new(nameof(ProjectilePlace.Earth));

    /// <summary>What is thrown, by <see cref="ProjectileBall"/> name.</summary>
    public OscillationChoice BallChoice { get; } = new(nameof(ProjectileBall.Baseball));

    /// <summary>The "fall together" scene, by <see cref="TogetherScene"/> name.</summary>
    public OscillationChoice SceneChoice { get; } = new(nameof(TogetherScene.DropAndThrow));

    /// <summary>Playback speed chips: 0.25, 0.5, 1 or 2 times real time.</summary>
    public OscillationChoice SpeedChoice { get; } = new("1");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Gravity), nameof(HasAir), nameof(ShowsNoAirNote))]
    private ProjectilePlace _place;

    [ObservableProperty]
    private ProjectileBall _ball = ProjectileBall.Baseball;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeKey), nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts),
        nameof(IsDropAndThrow), nameof(IsMonkeyHunter))]
    private TogetherScene _scene;

    public bool IsDropAndThrow => Scene == TogetherScene.DropAndThrow;

    public bool IsMonkeyHunter => Scene == TogetherScene.MonkeyHunter;

    public double Gravity => ProjectilePhysics.Gravity(Place, EarthGravity);

    /// <summary>Gravity on Earth at the shared observer location (9.780 m/s² at the equator, 9.832 at the poles).</summary>
    public double EarthGravity => _location.Current.Gravity;

    /// <summary>Under the place chips: whose gravity "Earth" is.</summary>
    public string EarthGravityNote => _localization.Format(
        "Location_Gravity_Note",
        _location.Current.Name,
        EarthGravity.ToString("0.000", CultureInfo.InvariantCulture));

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(EarthGravity));
        OnPropertyChanged(nameof(Gravity));
        OnPropertyChanged(nameof(EarthGravityNote));
        Rebuild();
    }

    /// <summary>Whether there is any air here to slow the ball (none on the Moon).</summary>
    public bool HasAir => ProjectilePhysics.AirDensity(Place) > 0;

    [ObservableProperty]
    private double _speed = 1;

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Goes up to fire again from the start.</summary>
    [ObservableProperty]
    private int _launchCount;

    /// <summary>Goes up to clear the kept paths.</summary>
    [ObservableProperty]
    private int _clearTrailsCount;

    /// <summary>Whether the air slows the ball (drag growing as the square of its speed).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoAirNote))]
    private bool _airDrag;

    private void OnPlaceChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Place = Enum.Parse<ProjectilePlace>(PlaceChoice.Selected);
    }

    private void OnBallChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Ball = Enum.Parse<ProjectileBall>(BallChoice.Selected);
    }

    private void OnSceneChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Scene = Enum.Parse<TogetherScene>(SceneChoice.Selected);
    }

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    partial void OnPlaceChanged(ProjectilePlace value)
    {
        PlaceChoice.Selected = value.ToString();
        Rebuild();
    }

    partial void OnBallChanged(ProjectileBall value)
    {
        BallChoice.Selected = value.ToString();
        Rebuild();
    }

    partial void OnSceneChanged(TogetherScene value)
    {
        // The "fall together" walkthrough picks its scenes itself; picking one by hand ends it.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        SceneChoice.Selected = value.ToString();
        Rebuild();
    }

    partial void OnAirDragChanged(bool value) => Rebuild();

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Fires again from the start.</summary>
    [RelayCommand]
    private void Launch()
    {
        LaunchCount++;
        IsPlaying = true;
    }

    [RelayCommand]
    private void ClearTrails() => ClearTrailsCount++;

    /// <summary>Air switched on where there is none: the panel says so.</summary>
    public bool ShowsNoAirNote => AirDrag && !HasAir;

    #endregion

    #region the shot

    /// <summary>Launch angle above level, degrees.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleText), nameof(ChartAngle))]
    private double _angle = 45;

    /// <summary>Launch speed, m/s.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaunchSpeedText))]
    private double _launchSpeed = 20;

    /// <summary>Height of the cliff it is fired from, metres.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CliffHeightText))]
    private double _cliffHeight;

    [ObservableProperty]
    private bool _showVectors = true;

    [ObservableProperty]
    private bool _showShadows;

    [ObservableProperty]
    private bool _showStrobe = true;

    [ObservableProperty]
    private bool _keepTrails = true;

    public string AngleText => $"{Angle.ToString("0", CultureInfo.InvariantCulture)}°";

    public string LaunchSpeedText => $"{LaunchSpeed.ToString("0.#", CultureInfo.InvariantCulture)} m/s";

    public string CliffHeightText => $"{CliffHeight.ToString("0", CultureInfo.InvariantCulture)} m";

    /// <summary>The angle the range chart marks: the shot's in launch mode, none (the fan's) in best-angle mode.</summary>
    public double ChartAngle => Mode == ProjectileMode.Launch ? Angle : double.NaN;

    [RelayCommand]
    private void SetAngle(string? angle)
    {
        if (double.TryParse(angle, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Angle = value;
    }

    partial void OnAngleChanged(double value) => Rebuild();

    partial void OnLaunchSpeedChanged(double value) => Rebuild();

    partial void OnCliffHeightChanged(double value) => Rebuild();

    #endregion

    #region falling together

    /// <summary>Drop and throw: the table's height, metres.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TowerHeightText))]
    private double _towerHeight = 20;

    /// <summary>Drop and throw: how fast the second ball is pushed off sideways, m/s.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThrowSpeedText))]
    private double _throwSpeed = 8;

    /// <summary>The dart's speed, m/s.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DartSpeedText))]
    private double _dartSpeed = 25;

    /// <summary>Whether the monkey lets go the moment the dart is fired.</summary>
    [ObservableProperty]
    private bool _monkeyLetsGo = true;

    public string TowerHeightText => $"{TowerHeight.ToString("0", CultureInfo.InvariantCulture)} m";

    public string ThrowSpeedText => $"{ThrowSpeed.ToString("0.#", CultureInfo.InvariantCulture)} m/s";

    public string DartSpeedText => $"{DartSpeed.ToString("0.#", CultureInfo.InvariantCulture)} m/s";

    /// <summary>The slowest dart that still reaches the monkey before it lands, rounded up a little.</summary>
    [RelayCommand]
    private void SetMinimumDartSpeed() => DartSpeed = Math.Ceiling((ProjectilePhysics.MonkeyMinSpeed(Gravity) + 0.2) * 2) / 2;

    partial void OnTowerHeightChanged(double value) => Rebuild();

    partial void OnThrowSpeedChanged(double value) => Rebuild();

    partial void OnDartSpeedChanged(double value) => Rebuild();

    partial void OnMonkeyLetsGoChanged(bool value) => Rebuild();

    #endregion

    #region time

    /// <summary>Seconds since launch (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _time;

    /// <summary>How long the scene runs, seconds: the timeline's end.</summary>
    [ObservableProperty]
    private double _duration = 1;

    partial void OnTimeChanged(double value)
    {
        // About ten times a second of playback, however much a long flight is sped up.
        var boost = Mode == ProjectileMode.Together ? 1 : ProjectilePhysics.PlaybackBoost(Duration);
        var tenth = (long)Math.Floor(value * 10 / boost);
        if (tenth == _shownTenth) return;
        _shownTenth = tenth;
        Refresh();
    }

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = Brushes.White;
    private IReadOnlyList<OscillationStat> _stats = [];

    /// <summary>Beside the playback buttons: the time, and how much a long flight is sped up.</summary>
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

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Proj_{ModeKey}_Fact{i}"))
        .ToList();

    private double Drag => ProjectilePhysics.DragFactor(Ball, Place, AirDrag);

    /// <summary>Works the flights out again for the current settings, then the card.</summary>
    private void Rebuild()
    {
        var gravity = Gravity;
        var drag = Drag;
        switch (Mode)
        {
            case ProjectileMode.Together:
                Duration = Scene == TogetherScene.MonkeyHunter
                    ? ProjectilePhysics.MonkeyEnd(DartSpeed, gravity, MonkeyLetsGo).Time
                    : ProjectilePhysics.FallTime(TowerHeight, gravity);
                break;

            default:
                _flight = ProjectilePhysics.Fly(LaunchSpeed, Angle, CliffHeight, gravity, drag);
                _airless = drag > 0 ? ProjectilePhysics.Fly(LaunchSpeed, Angle, CliffHeight, gravity, 0) : _flight;
                _bestAngle = ProjectilePhysics.BestAngle(LaunchSpeed, CliffHeight, gravity, drag);
                _bestRange = ProjectilePhysics.Range(LaunchSpeed, _bestAngle, CliffHeight, gravity, drag);
                Duration = Mode == ProjectileMode.BestAngle
                    ? ProjectilePhysics.FanAngles.Append(_bestAngle).Max(a => ProjectilePhysics.Fly(LaunchSpeed, a, CliffHeight, gravity, drag).Duration)
                    : _flight.Duration;
                break;
        }

        _shownTenth = -1;
        Refresh();
    }

    private void Refresh()
    {
        var boost = Mode == ProjectileMode.Together ? 1 : ProjectilePhysics.PlaybackBoost(Duration);
        var time = Time.ToString(Time < 100 ? "0.00" : "0.0", CultureInfo.InvariantCulture);
        ClockText = boost > 1 ? _localization.Format("Proj_Clock_Boost", time, boost) : _localization.Format("Proj_Clock", time);

        switch (Mode)
        {
            case ProjectileMode.BestAngle:
                RefreshBestAngle();
                break;

            case ProjectileMode.Together when Scene == TogetherScene.MonkeyHunter:
                RefreshMonkey();
                break;

            case ProjectileMode.Together:
                RefreshDropAndThrow();
                break;

            default:
                RefreshLaunch();
                break;
        }
    }

    private void RefreshLaunch()
    {
        var flight = _flight;
        var now = flight.At(Time);
        var stage = Time <= 0 && flight.Duration > 0 ? (FlightStage?)null : ProjectilePhysics.Stage(flight, Time);
        var key = stage?.ToString() ?? "Ready";
        PhaseTitle = _localization.GetString($"Proj_Stage_{key}_Title");
        PhaseDescription = _localization.GetString($"Proj_Stage_{key}_Description")
            + (Drag > 0 ? " " + _localization.GetString("Proj_Stage_AirNote") : string.Empty);
        PhaseBrush = stage switch
        {
            FlightStage.Rising => CyanBrush,
            FlightStage.Top => GoldBrush,
            FlightStage.Falling => GreenBrush,
            FlightStage.Landed => OrangeBrush,
            _ => VioletBrush,
        };

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Proj_Stat_Place"), PlaceText()),
            new(_localization.GetString("Proj_Stat_Ball"), BallText()),
            new(_localization.GetString("Proj_Stat_Now"), _localization.Format("Proj_Stat_Now_Value", Metres(now.X), Metres(now.Y))),
            new(_localization.GetString("Proj_Stat_Speed"), _localization.Format("Proj_Stat_Speed_Value", MetresPerSecond(now.Speed),
                Number(now.Vx, "0.0"), Number(now.Vy, "0.0"))),
            new(_localization.GetString("Proj_Stat_Range"), Metres(flight.Range)),
            new(_localization.GetString("Proj_Stat_Peak"), Metres(flight.Peak)),
            new(_localization.GetString("Proj_Stat_FlightTime"), Seconds(flight.Duration)),
            new(_localization.GetString("Proj_Stat_Landing"), $"{MetresPerSecond(flight.Landing.Speed)} · {Number(flight.LandingAngle, "0")}°"),
        };

        if (Drag > 0)
        {
            var lost = 1 - (flight.Range / Math.Max(1e-9, _airless.Range));
            stats.Add(new OscillationStat(_localization.GetString("Proj_Stat_Airless"),
                _localization.Format("Proj_Stat_Airless_Value", Metres(_airless.Range), Number(lost * 100, "0"))));
        }

        stats.Add(new OscillationStat(_localization.GetString("Proj_Stat_BestAngle"), BestText()));
        Stats = stats;
    }

    private void RefreshBestAngle()
    {
        var gravity = Gravity;
        var drag = Drag;
        var landed = Time >= Duration - 1e-9;
        var reason = drag > 0 ? "Air" : CliffHeight > 0.5 ? "Height" : "Ground";
        PhaseTitle = landed
            ? _localization.Format("Proj_Best_Landed_Title", Number(_bestAngle, "0"))
            : _localization.GetString("Proj_Best_Flying_Title");
        PhaseDescription = landed
            ? _localization.GetString($"Proj_Best_Landed_{reason}_Description")
            : _localization.GetString("Proj_Best_Flying_Description");
        PhaseBrush = landed ? GreenBrush : CyanBrush;

        var range45 = ProjectilePhysics.Range(LaunchSpeed, 45, CliffHeight, gravity, drag);
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Proj_Stat_Place"), PlaceText()),
            new(_localization.GetString("Proj_Stat_Ball"), BallText()),
            new(_localization.GetString("Proj_Stat_Launch"), _localization.Format("Proj_Stat_Launch_Value", MetresPerSecond(LaunchSpeed), Metres(CliffHeight))),
            new(_localization.GetString("Proj_Stat_BestAngle"), BestText()),
            new(_localization.GetString("Proj_Stat_At45"), Metres(range45)),
            new(_localization.GetString("Proj_Stat_Pairs"), _localization.Format("Proj_Stat_Pairs_Value",
                Metres(ProjectilePhysics.Range(LaunchSpeed, 30, CliffHeight, gravity, drag)),
                Metres(ProjectilePhysics.Range(LaunchSpeed, 60, CliffHeight, gravity, drag)))),
        };

        if (drag > 0)
        {
            stats.Add(new OscillationStat(_localization.GetString("Proj_Stat_AirlessBest"), _localization.Format("Proj_Stat_Best_Value",
                Number(ProjectilePhysics.IdealBestAngle(LaunchSpeed, CliffHeight, gravity), "0"),
                Metres(ProjectilePhysics.IdealMaxRange(LaunchSpeed, CliffHeight, gravity)))));
        }

        Stats = stats;
    }

    private void RefreshDropAndThrow()
    {
        var gravity = Gravity;
        var duration = Duration;
        var time = Math.Clamp(Time, 0, duration);
        var landed = Time >= duration - 1e-9;
        var key = landed ? "Landed" : time <= 0 ? "Ready" : "Falling";
        PhaseTitle = _localization.GetString($"Proj_Drop_{key}_Title");
        PhaseDescription = _localization.GetString($"Proj_Drop_{key}_Description");
        PhaseBrush = landed ? GoldBrush : time <= 0 ? VioletBrush : CyanBrush;

        var strobe = ProjectileDiagram.TogetherStrobe(duration);
        Stats =
        [
            new(_localization.GetString("Proj_Stat_Place"), PlaceText()),
            new(_localization.GetString("Proj_Stat_TableHeight"), Metres(TowerHeight)),
            new(_localization.GetString("Proj_Stat_ThrowSpeed"), MetresPerSecond(ThrowSpeed)),
            new(_localization.GetString("Proj_Stat_Fallen"), Metres(ProjectilePhysics.FallDistance(time, gravity))),
            new(_localization.GetString("Proj_Stat_FallTime"), Seconds(duration)),
            new(_localization.GetString("Proj_Stat_ThrowRange"), Metres(ThrowSpeed * duration)),
            new(_localization.GetString("Proj_Stat_FirstStep"), _localization.Format("Proj_Stat_FirstStep_Value",
                Number(strobe, "0.##"), Metres(ProjectilePhysics.FallDistance(strobe, gravity)))),
        ];
    }

    private void RefreshMonkey()
    {
        var gravity = Gravity;
        var (end, outcome) = ProjectilePhysics.MonkeyEnd(DartSpeed, gravity, MonkeyLetsGo);
        var time = Math.Clamp(Time, 0, end);
        var done = Time >= end - 1e-9;
        var key = done ? outcome.ToString() : time <= 0 ? "Ready" : MonkeyLetsGo ? "Flying" : "FlyingHeld";
        PhaseTitle = _localization.GetString($"Proj_Monkey_{key}_Title");
        PhaseDescription = _localization.GetString($"Proj_Monkey_{key}_Description");
        PhaseBrush = key switch
        {
            "Hit" => GoldBrush,
            "Short" => RedBrush,
            "Below" => VioletBrush,
            "Ready" => VioletBrush,
            _ => CyanBrush,
        };

        var reach = Math.Sqrt((ProjectilePhysics.MonkeyDistance * ProjectilePhysics.MonkeyDistance)
            + (ProjectilePhysics.MonkeyHeight * ProjectilePhysics.MonkeyHeight)) / DartSpeed;
        Stats =
        [
            new(_localization.GetString("Proj_Stat_Place"), PlaceText()),
            new(_localization.GetString("Proj_Stat_Branch"), _localization.Format("Proj_Stat_Branch_Value",
                Metres(ProjectilePhysics.MonkeyDistance), Metres(ProjectilePhysics.MonkeyHeight))),
            new(_localization.GetString("Proj_Stat_Aim"), $"{Number(ProjectilePhysics.MonkeyAim, "0.0")}°"),
            new(_localization.GetString("Proj_Stat_DartSpeed"), MetresPerSecond(DartSpeed)),
            new(_localization.GetString("Proj_Stat_MinSpeed"), MetresPerSecond(ProjectilePhysics.MonkeyMinSpeed(gravity))),
            new(_localization.GetString("Proj_Stat_Reach"), outcome == MonkeyOutcome.Short
                ? _localization.GetString("Proj_Stat_Reach_Never")
                : Seconds(reach)),
            new(_localization.GetString("Proj_Stat_BothFallen"), Metres(ProjectilePhysics.FallDistance(time, gravity))),
        ];
    }

    private string PlaceText() => _localization.Format("Proj_Stat_Place_Value", PlaceName(),
        Number(Gravity, "0.00"));

    /// <summary>"Baseball · stops speeding up at 40 m/s", or "· no air" where there is none or it is switched off.</summary>
    private string BallText()
    {
        var name = _localization.GetString($"Proj_Ball_{Ball}");
        var drag = Drag;
        if (drag <= 0)
        {
            return _localization.Format(HasAir ? "Proj_Stat_Ball_AirOff" : "Proj_Stat_Ball_NoAir", name);
        }

        return _localization.Format("Proj_Stat_Ball_Terminal", name, MetresPerSecond(ProjectilePhysics.TerminalSpeed(Gravity, drag)));
    }

    private string BestText() => _localization.Format("Proj_Stat_Best_Value", Number(_bestAngle, "0"), Metres(_bestRange));

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Metres(double metres) => ProjectileDiagram.Metres(metres);

    private static string MetresPerSecond(double speed) => $"{Number(speed, speed < 100 ? "0.0" : "0")} m/s";

    private static string Seconds(double seconds) => $"{Number(seconds, seconds < 10 ? "0.00" : "0.0")} s";

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    /// <summary>The place's name; Earth names the shared observer location, e.g. 地球（长沙）.</summary>
    private string PlaceName()
    {
        var name = _localization.GetString($"Proj_Place_{Place}");
        return Place == ProjectilePlace.Earth ? _localization.Format("Location_EarthAt", name, _location.Current.Name) : name;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(EarthGravityNote));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        PlaceChoice.PropertyChanged -= OnPlaceChosen;
        BallChoice.PropertyChanged -= OnBallChosen;
        SceneChoice.PropertyChanged -= OnSceneChosen;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
