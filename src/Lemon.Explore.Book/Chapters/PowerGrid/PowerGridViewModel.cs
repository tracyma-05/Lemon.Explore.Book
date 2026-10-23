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

namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>
/// The generator-and-grid page: which view is shown, playback, the generator's speed, coil, magnet and connections, the
/// line's voltage, power and length, the grid's season, solar farms and events, and what is happening. The diagram
/// (<see cref="GridDiagram"/>) turns <see cref="Angle"/>, runs <see cref="Hour"/> and <see cref="EventTime"/>, and
/// switches <see cref="LampOn"/> and <see cref="FeedDc"/>. <see cref="Lesson"/> is the spoken walkthrough of the current
/// view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class PowerGridViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush GreenBrush = Frozen(0x8B, 0xE0, 0x8F);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9F, 0x5A);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x6A, 0x5A);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xA8, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    public IExploreLocationService Location => _location;

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while things move.</summary>
    private string _shownKey = string.Empty;

    public PowerGridViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        Lesson = new LessonPlayer(narration, () => PowerGridLessons.For(this));
        Card = new ObjectCard(narration, Lesson, GridDiagram.ObjectTitleKey, GridDiagram.ObjectClipKey);
        location.Changed += OnLocationChanged;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        KindChoice.PropertyChanged += OnKindChosen;
        TurnsChoice.PropertyChanged += OnTurnsChosen;
        FieldChoice.PropertyChanged += OnFieldChosen;
        PolesChoice.PropertyChanged += OnPolesChosen;
        VoltageChoice.PropertyChanged += OnVoltageChosen;
        SeasonChoice.PropertyChanged += OnSeasonChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
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
    [NotifyPropertyChangedFor(nameof(IsGenerator), nameof(IsTransmission), nameof(IsBalance), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private GridMode _mode;

    public bool IsGenerator
    {
        get => Mode == GridMode.Generator;
        set
        {
            if (value) Mode = GridMode.Generator;
        }
    }

    public bool IsTransmission
    {
        get => Mode == GridMode.Transmission;
        set
        {
            if (value) Mode = GridMode.Transmission;
        }
    }

    public bool IsBalance
    {
        get => Mode == GridMode.Balance;
        set
        {
            if (value) Mode = GridMode.Balance;
        }
    }

    public string Subtitle => _localization.GetString($"Grid_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Grid_{Mode}_Note");

    public string Hint => _localization.GetString($"Grid_Hint_{Mode}");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Grid_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(GridMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
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

    #region playback

    /// <summary>Playback speed chips: 0.25, 0.5, 1 or 2 times the normal pace.</summary>
    public OscillationChoice SpeedChoice { get; } = new("1");

    [ObservableProperty]
    private double _speed = 1;

    [ObservableProperty]
    private bool _isPlaying = true;

    [ObservableProperty]
    private bool _showLabels = true;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    #endregion

    #region the generator

    /// <summary>The coil's (or the magnet's) angle, 0…360° from facing the field (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _angle;

    /// <summary>Turns per second.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoilSpeedText))]
    private double _coilSpeed = GeneratorModel.DefaultSpeed;

    public string CoilSpeedText => _localization.Format("Grid_Gen_Speed_Value", Number(CoilSpeed, "0.0#"), Number(CoilSpeed * 60, "0"));

    /// <summary>Slip rings, commutator or the three-phase machine, by <see cref="GeneratorKind"/> name.</summary>
    public OscillationChoice KindChoice { get; } = new(nameof(GeneratorKind.Ac));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThreePhase), nameof(IsSingleCoil))]
    private GeneratorKind _kind;

    public bool IsThreePhase => Kind == GeneratorKind.ThreePhase;

    public bool IsSingleCoil => Kind != GeneratorKind.ThreePhase;

    /// <summary>Turns of wire on the coil: 50, 200 or 500.</summary>
    public OscillationChoice TurnsChoice { get; } = new(GeneratorModel.DefaultTurns.ToString(CultureInfo.InvariantCulture));

    [ObservableProperty]
    private int _turns = GeneratorModel.DefaultTurns;

    /// <summary>The magnet: a weak one (0.1 T) or a strong one (0.3 T).</summary>
    public OscillationChoice FieldChoice { get; } = new("0.3");

    [ObservableProperty]
    private double _field = GeneratorModel.DefaultField;

    [ObservableProperty]
    private bool _lampOn = true;

    /// <summary>The three-phase magnet's pairs of poles: 1 (two poles) or 2 (four).</summary>
    public OscillationChoice PolesChoice { get; } = new("1");

    [ObservableProperty]
    private int _polePairs = 1;

    partial void OnAngleChanged(double value)
    {
        if (Mode == GridMode.Generator) Refresh();
    }

    partial void OnCoilSpeedChanged(double value) => Refresh(force: true);

    partial void OnKindChanged(GeneratorKind value)
    {
        KindChoice.Selected = value.ToString();
        Refresh(force: true);
    }

    partial void OnTurnsChanged(int value)
    {
        TurnsChoice.Selected = value.ToString(CultureInfo.InvariantCulture);
        Refresh(force: true);
    }

    partial void OnFieldChanged(double value)
    {
        FieldChoice.Selected = Number(value, "0.0");
        Refresh(force: true);
    }

    partial void OnLampOnChanged(bool value) => Refresh(force: true);

    partial void OnPolePairsChanged(int value)
    {
        PolesChoice.Selected = value.ToString(CultureInfo.InvariantCulture);
        Refresh(force: true);
    }

    private void OnKindChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Kind = Enum.Parse<GeneratorKind>(KindChoice.Selected);
    }

    private void OnTurnsChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Turns = int.Parse(TurnsChoice.Selected, CultureInfo.InvariantCulture);
    }

    private void OnFieldChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Field = double.Parse(FieldChoice.Selected, CultureInfo.InvariantCulture);
    }

    private void OnPolesChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) PolePairs = int.Parse(PolesChoice.Selected, CultureInfo.InvariantCulture);
    }

    /// <summary>Holds the coil at an angle (degrees from facing the field).</summary>
    [RelayCommand]
    private void HoldAngle(string? degrees)
    {
        if (!double.TryParse(degrees, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        Angle = value;
        IsPlaying = false;
    }

    #endregion

    #region the line

    /// <summary>The line's voltage chips, kV.</summary>
    public OscillationChoice VoltageChoice { get; } = new(Number(TransmissionModel.DefaultKv, "0"));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineKvText))]
    private double _lineKv = TransmissionModel.DefaultKv;

    public string LineKvText => $"{Number(LineKv, "0")} kV";

    /// <summary>The power sent, MW.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PowerText))]
    private double _power = TransmissionModel.DefaultPower;

    public string PowerText => $"{Number(Power, "0")} MW";

    /// <summary>How far, km.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistanceText))]
    private double _distance = TransmissionModel.DefaultDistance;

    public string DistanceText => $"{Number(Distance, "0")} km";

    [ObservableProperty]
    private bool _feedDc;

    partial void OnLineKvChanged(double value)
    {
        VoltageChoice.Selected = Number(value, "0");
        Refresh(force: true);
    }

    partial void OnPowerChanged(double value) => Refresh(force: true);

    partial void OnDistanceChanged(double value) => Refresh(force: true);

    partial void OnFeedDcChanged(bool value) => Refresh(force: true);

    private void OnVoltageChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) LineKv = double.Parse(VoltageChoice.Selected, CultureInfo.InvariantCulture);
    }

    #endregion

    #region the grid's day

    public OscillationChoice SeasonChoice { get; } = new(nameof(GridSeason.Summer));

    [ObservableProperty]
    private GridSeason _season;

    /// <summary>The hour, local solar time (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _hour = 10;

    /// <summary>The solar farms' capacity, GW.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SolarText))]
    private double _solarCapacity = GridDayModel.DefaultSolar;

    public string SolarText => $"{Number(SolarCapacity, "0")} GW";

    public double MaxSolar => GridDayModel.MaxSolar;

    /// <summary>The viewer's latitude, which sets the Sun's height.</summary>
    public double Latitude => _location.Current.Latitude;

    /// <summary>Under the season chips: the Sun follows the shared location.</summary>
    public string SunNote => _localization.Format("Grid_Bal_SunNote", _location.Current.Name);

    /// <summary>The event being followed (<see cref="GridEvent.None"/> once it has played out).</summary>
    [ObservableProperty]
    private GridEvent _eventKind;

    [ObservableProperty]
    private IReadOnlyList<FrequencyPoint>? _eventTrace;

    /// <summary>The last event's trace, kept for the chart after it has played out.</summary>
    [ObservableProperty]
    private IReadOnlyList<FrequencyPoint>? _lastTrace;

    /// <summary>Seconds since the event (from the diagram).</summary>
    [ObservableProperty]
    private double _eventTime;

    /// <summary>How many events have been started, for a walkthrough's task.</summary>
    public int EventCount { get; private set; }

    private double _eventHour;

    /// <summary>Starts an event (by <see cref="GridEvent"/> name) at the hour shown.</summary>
    [RelayCommand]
    private void TriggerEvent(string? kind)
    {
        if (!Enum.TryParse<GridEvent>(kind, out var value) || value == GridEvent.None) return;
        var mix = Mix();
        var trace = FrequencyModel.Simulate(FrequencyModel.Size(value), mix.Demand, GridDayModel.Inertia(mix), GridDayModel.GovernedCapacity(mix));
        _eventHour = Hour;
        EventTime = 0;
        EventTrace = trace;
        LastTrace = trace;
        EventKind = value;
        EventCount++;
        IsPlaying = true;
        Refresh(force: true);
    }

    [RelayCommand]
    private void ClearEvent()
    {
        EventKind = GridEvent.None;
        EventTrace = null;
        Refresh(force: true);
    }

    /// <summary>Jumps the day to an hour (and ends an event).</summary>
    [RelayCommand]
    private void JumpToHour(string? hour)
    {
        if (double.TryParse(hour, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Hour = value;
    }

    partial void OnHourChanged(double value)
    {
        if (EventKind != GridEvent.None && Math.Abs(GridDayModel.Wrap(value) - GridDayModel.Wrap(_eventHour)) > 0.05) ClearEvent();
        if (Mode == GridMode.Balance) Refresh();
    }

    partial void OnEventTimeChanged(double value)
    {
        if (EventKind != GridEvent.None && value >= FrequencyModel.Duration - 1e-6)
        {
            // Played out: the day goes on, the chart keeps the trace.
            EventKind = GridEvent.None;
            EventTrace = null;
        }

        if (Mode == GridMode.Balance) Refresh();
    }

    partial void OnSeasonChanged(GridSeason value)
    {
        SeasonChoice.Selected = value.ToString();
        if (EventKind != GridEvent.None) ClearEvent();
        Refresh(force: true);
    }

    partial void OnSolarCapacityChanged(double value)
    {
        if (EventKind != GridEvent.None) ClearEvent();
        Refresh(force: true);
    }

    private void OnSeasonChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Season = Enum.Parse<GridSeason>(SeasonChoice.Selected);
    }

    internal GridMix Mix() => GridDayModel.Mix(Season, Latitude, Hour, SolarCapacity);

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

    private void Refresh(bool force = false)
    {
        // The generator's card follows the coil every 10°, the grid's every quarter hour or second of an event.
        var key = Mode switch
        {
            GridMode.Generator => $"G|{Math.Floor(Angle / 10)}",
            GridMode.Balance => EventKind != GridEvent.None ? $"E|{Math.Floor(EventTime)}" : $"B|{Math.Floor(Hour * 4)}",
            _ => "T",
        };
        if (!force && key == _shownKey) return;
        _shownKey = key;

        switch (Mode)
        {
            case GridMode.Transmission:
                RefreshLine();
                break;

            case GridMode.Balance:
                RefreshBalance();
                break;

            default:
                RefreshGenerator();
                break;
        }
    }

    private void RefreshGenerator()
    {
        var phi = Angle * Math.PI / 180;
        var peak = GeneratorModel.PeakEmf(Turns, Field, CoilSpeed);
        ClockText = _localization.Format("Grid_Gen_Clock", Number(Angle, "0"), Number(CoilSpeed, "0.0#"));
        if (Kind == GeneratorKind.ThreePhase)
        {
            var (a, b, c) = GeneratorModel.ThreePhase(peak, phi, PolePairs);
            var values = new[] { a, b, c };
            var top = Array.IndexOf(values, values.MaxBy(Math.Abs));
            PhaseTitle = _localization.Format("Grid_Gen_Three_Title", _localization.GetString($"Grid_Phase_{top}"));
            PhaseDescription = _localization.GetString("Grid_Gen_Three_Description");
            PhaseBrush = GoldBrush;
            Stats =
            [
                new(_localization.GetString("Grid_Stat_Angle"), $"{Number(Angle, "0")}°"),
                new(_localization.GetString("Grid_Stat_Phases"), $"{Volts(a)} · {Volts(b)} · {Volts(c)}"),
                new(_localization.GetString("Grid_Stat_Sum"), "0.0 V"),
                new(_localization.GetString("Grid_Stat_Frequency"), _localization.Format("Grid_Stat_Frequency_Three", Number(CoilSpeed * PolePairs, "0.0#"), Number(CoilSpeed, "0.0#"), PolePairs * 2)),
                new(_localization.GetString("Grid_Stat_Station"), _localization.Format("Grid_Stat_Station_Value", PolePairs * 2, Number(GeneratorModel.SynchronousRpm(PolePairs, 50), "0"))),
            ];
            return;
        }

        var facing = Math.Abs(Math.Cos(phi)) > 0.92;
        var along = Math.Abs(Math.Sin(phi)) > 0.92;
        var zone = facing ? "Facing" : along ? "Along" : "Between";
        PhaseTitle = _localization.GetString($"Grid_Gen_{zone}_Title");
        PhaseDescription = _localization.GetString($"Grid_Gen_{zone}_Description");
        PhaseBrush = facing ? GoldBrush : along ? GreenBrush : CyanBrush;

        var emf = GeneratorModel.Output(Kind, GeneratorModel.Emf(Turns, Field, CoilSpeed, phi));
        var current = GeneratorModel.Current(Turns, emf, LampOn);
        var peakCurrent = GeneratorModel.Current(Turns, peak, LampOn);
        var flux = GeneratorModel.Flux(Field, phi) * 1000;
        var force = GeneratorModel.HandForce(Turns, peak, LampOn, CoilSpeed);
        Stats =
        [
            new(_localization.GetString("Grid_Stat_Angle"), $"{Number(Angle, "0")}°"),
            new(_localization.GetString("Grid_Stat_Flux"), _localization.Format("Grid_Stat_Flux_Value", Number(flux, "0.00"), Number(Field * GeneratorModel.CoilArea * 1000, "0.0"))),
            new(_localization.GetString("Grid_Stat_Voltage"), Volts(emf)),
            new(_localization.GetString("Grid_Stat_Peak"), _localization.Format("Grid_Stat_Peak_Value", Turns, Number(Field, "0.0"), Number(CoilSpeed, "0.0#"), Number(peak, "0.0"))),
            new(_localization.GetString("Grid_Stat_Current"), LampOn
                ? _localization.Format("Grid_Stat_Current_Value", Number(Math.Abs(current), "0.00"), Number(peakCurrent, "0.00"))
                : _localization.GetString("Grid_Stat_Current_Off")),
            new(_localization.GetString("Grid_Stat_Lamp"), _localization.Format("Grid_Stat_Lamp_Value",
                Number(GeneratorModel.AverageLampPower(Turns, Field, CoilSpeed, LampOn), "0.0"), Number(GeneratorModel.LampWatts, "0"))),
            new(_localization.GetString("Grid_Stat_Force"), LampOn
                ? _localization.Format("Grid_Stat_Force_Value", Number(force, "0.0"))
                : _localization.GetString("Grid_Stat_Force_None")),
            new(_localization.GetString("Grid_Stat_Frequency"), _localization.Format("Grid_Stat_Frequency_Value", Number(CoilSpeed, "0.0#"), Number(CoilSpeed * 60, "0"))),
        ];
    }

    private void RefreshLine()
    {
        var line = TransmissionModel.Send(Power, LineKv, Distance);
        var state = FeedDc ? "Dc" : line.Impossible ? "Impossible" : line.LossFraction >= 0.05 || line.Overloaded ? "High" : "Good";
        PhaseTitle = _localization.GetString($"Grid_Line_{state}_Title");
        PhaseDescription = _localization.GetString($"Grid_Line_{state}_Description");
        PhaseBrush = state switch
        {
            "Good" => GreenBrush,
            "High" => OrangeBrush,
            "Impossible" => RedBrush,
            _ => BlueBrush,
        };
        ClockText = _localization.Format("Grid_Line_Clock", Number(LineKv, "0"), Number(Power, "0"), Number(Distance, "0"));
        Stats =
        [
            new(_localization.GetString("Grid_Stat_Sent"), $"{Number(Power, "0")} MW"),
            new(_localization.GetString("Grid_Stat_LineVoltage"), _localization.Format("Grid_Stat_LineVoltage_Value", Number(LineKv, "0"), Number(TransmissionModel.StepUpRatio(LineKv), "0.#"))),
            new(_localization.GetString("Grid_Stat_LineCurrent"), _localization.Format("Grid_Stat_LineCurrent_Value", Number(line.Current, "0"), Number(TransmissionModel.Ampacity, "0"))),
            new(_localization.GetString("Grid_Stat_Resistance"), _localization.Format("Grid_Stat_Resistance_Value", Number(TransmissionModel.Resistance(Distance), "0.0"), Number(Distance, "0"))),
            new(_localization.GetString("Grid_Stat_Loss"), line.Impossible
                ? _localization.GetString("Grid_Stat_Loss_Impossible")
                : _localization.Format("Grid_Stat_Loss_Value", Number(line.Loss, line.Loss < 10 ? "0.0" : "0"), Number(line.LossFraction * 100, line.LossFraction < 0.1 ? "0.0" : "0"))),
            new(_localization.GetString("Grid_Stat_Arrives"), line.Impossible ? "—" : $"{Number(line.Delivered, "0")} MW"),
        ];
    }

    private void RefreshBalance()
    {
        var mix = Mix();
        var inertia = GridDayModel.Inertia(mix);
        if (EventKind != GridEvent.None && EventTrace is { Count: > 0 } trace)
        {
            var point = FrequencyModel.At(trace, EventTime);
            var lowest = FrequencyModel.Lowest(trace);
            var state = point.Time < lowest.Time ? "Falling" : point.Time < 20 ? "Caught" : "Recovering";
            PhaseTitle = _localization.GetString($"Grid_Bal_{state}_Title");
            PhaseDescription = _localization.GetString($"Grid_Bal_{state}_Description") +
                (point.Shed > 0 ? " " + _localization.GetString("Grid_Bal_Shed_Description") : string.Empty);
            PhaseBrush = state == "Falling" ? RedBrush : state == "Caught" ? OrangeBrush : GreenBrush;
            ClockText = _localization.Format("Grid_Bal_EventClock", Number(point.Time, "0"), _localization.GetString($"Grid_Event_{EventKind}"));
            Stats =
            [
                new(_localization.GetString("Grid_Stat_Event"), _localization.Format("Grid_Stat_Event_Value", _localization.GetString($"Grid_Event_{EventKind}"), GridDiagram.ClockText(_eventHour))),
                new(_localization.GetString("Grid_Stat_GridFrequency"), _localization.Format("Grid_Stat_GridFrequency_Event", Number(point.Frequency, "0.00"), Number(lowest.Frequency, "0.00"), Number(lowest.Time, "0"))),
                new(_localization.GetString("Grid_Stat_FirstFall"), _localization.Format("Grid_Stat_FirstFall_Value",
                    Number(FrequencyModel.InitialFall(FrequencyModel.Size(EventKind), mix.Demand, inertia), "0.00"), Number(inertia, "0.0"))),
                new(_localization.GetString("Grid_Stat_Governors"), $"+{Number(point.Governors * 1000, "0")} MW"),
                new(_localization.GetString("Grid_Stat_Control"), $"+{Number(point.Control * 1000, "0")} MW"),
                new(_localization.GetString("Grid_Stat_Eased"), $"{Number(point.LoadEase * 1000, "0")} MW"),
                new(_localization.GetString("Grid_Stat_Shed"), point.Shed > 0 ? $"{Number(point.Shed * 1000, "0")} MW" : _localization.GetString("Grid_Stat_Shed_None")),
            ];
            return;
        }

        var hour = GridDayModel.Wrap(Hour);
        var sun = GridDayModel.SunHeight(Season, Latitude, hour);
        var key = hour < 6 || hour >= 23 ? "Night" : mix.Pumped < -0.05 && sun > 0.3 ? "Sunny" : mix.Demand >= 0.9 * (Season == GridSeason.Summer ? GridDayModel.SummerPeak : GridDayModel.WinterPeak) && sun < 0.3 ? "Peak" : "Day";
        PhaseTitle = _localization.GetString($"Grid_Bal_{key}_Title");
        PhaseDescription = _localization.GetString($"Grid_Bal_{key}_Description");
        PhaseBrush = key switch
        {
            "Night" => BlueBrush,
            "Sunny" => GoldBrush,
            "Peak" => OrangeBrush,
            _ => CyanBrush,
        };
        ClockText = GridDiagram.ClockText(hour);
        var pumped = mix.Pumped < -0.05
            ? _localization.Format("Grid_Stat_Pumped_Pumping", Number(-mix.Pumped, "0.0"))
            : mix.Pumped > 0.05 ? _localization.Format("Grid_Stat_Pumped_Generating", Number(mix.Pumped, "0.0")) : _localization.GetString("Grid_Stat_Pumped_Idle");
        Stats =
        [
            new(_localization.GetString("Grid_Stat_Time"), GridDiagram.ClockText(hour)),
            new(_localization.GetString("Grid_Stat_Demand"), $"{Number(mix.Demand, "0.0")} GW"),
            new(_localization.GetString("Grid_Stat_Renewables"), _localization.Format("Grid_Stat_Renewables_Value", Number(mix.Wind + mix.Solar, "0.0"), Number((mix.Wind + mix.Solar) / mix.Demand * 100, "0"))),
            new(_localization.GetString("Grid_Stat_Coal"), _localization.Format("Grid_Stat_Coal_Value", Number(mix.Coal, "0.0"), Number(GridDayModel.CoalMinimum, "0"))),
            new(_localization.GetString("Grid_Stat_Pumped"), pumped),
            new(_localization.GetString("Grid_Stat_Curtailed"), mix.Curtailed > 0.05 ? $"{Number(mix.Curtailed, "0.0")} GW" : _localization.GetString("Grid_Stat_Curtailed_None")),
            new(_localization.GetString("Grid_Stat_Inertia"), _localization.Format("Grid_Stat_Inertia_Value", Number(inertia, "0.0"))),
        ];
    }

    private static string Volts(double value) => $"{(value < -0.005 ? "−" : value > 0.005 ? "+" : string.Empty)}{Number(Math.Abs(value), "0.0")} V";

    #endregion

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(SunNote));
        if (EventKind != GridEvent.None) ClearEvent();
        Refresh(force: true);
    }

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(SunNote));
        OnPropertyChanged(nameof(CoilSpeedText));
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        KindChoice.PropertyChanged -= OnKindChosen;
        TurnsChoice.PropertyChanged -= OnTurnsChosen;
        FieldChoice.PropertyChanged -= OnFieldChosen;
        PolesChoice.PropertyChanged -= OnPolesChosen;
        VoltageChoice.PropertyChanged -= OnVoltageChosen;
        SeasonChoice.PropertyChanged -= OnSeasonChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
