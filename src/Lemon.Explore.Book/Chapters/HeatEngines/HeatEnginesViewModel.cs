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

namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>
/// The engine-and-fridge page: which view is shown, playback, the engine's compression ratio and speed, the fridge's
/// room and door, the ideal cycle's temperatures and direction, and what is happening. The diagram
/// (<see cref="HeatDiagram"/>) turns <see cref="Angle"/> and <see cref="Phase"/> and opens <see cref="DoorOpen"/>.
/// <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in the diagram
/// (<see cref="Card"/>) explains it.
/// </summary>
public partial class HeatEnginesViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush[] StrokeBrushes =
    [
        Frozen(0x5A, 0xB0, 0xFF),
        Frozen(0xB3, 0x9D, 0xFF),
        Frozen(0xFF, 0x9F, 0x5A),
        Frozen(0xA8, 0xB0, 0xBC),
    ];

    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9F, 0x5A);
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x7A, 0x5A);
    private static readonly IBrush VioletBrush = Frozen(0xB3, 0x9D, 0xFF);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xB0, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    public IExploreLocationService Location => _location;

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while the engine turns.</summary>
    private string _shownKey = string.Empty;

    public HeatEnginesViewModel(IExploreLocationService location, INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => HeatEnginesLessons.For(this));
        Card = new ObjectCard(narration, Lesson, HeatDiagram.ObjectTitleKey, HeatDiagram.ObjectClipKey);
        _location = location;
        location.Changed += OnLocationChanged;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        RpmChoice.PropertyChanged += OnRpmChosen;
        DirectionChoice.PropertyChanged += OnDirectionChosen;
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
    [NotifyPropertyChangedFor(nameof(IsEngine), nameof(IsFridge), nameof(IsCycle), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private HeatMode _mode;

    public bool IsEngine
    {
        get => Mode == HeatMode.Engine;
        set
        {
            if (value) Mode = HeatMode.Engine;
        }
    }

    public bool IsFridge
    {
        get => Mode == HeatMode.Fridge;
        set
        {
            if (value) Mode = HeatMode.Fridge;
        }
    }

    public bool IsCycle
    {
        get => Mode == HeatMode.Cycle;
        set
        {
            if (value) Mode = HeatMode.Cycle;
        }
    }

    public string Subtitle => _localization.GetString($"Heat_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Heat_{Mode}_Note");

    public string Hint => _localization.GetString($"Heat_Hint_{Mode}");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Heat_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(HeatMode value)
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

    #region the engine

    /// <summary>The crank angle, 0…720° (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _angle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompressionRatioText))]
    private double _compressionRatio = EngineModel.DefaultCompressionRatio;

    public double MinCompressionRatio => EngineModel.MinCompressionRatio;

    public double MaxCompressionRatio => EngineModel.MaxCompressionRatio;

    public string CompressionRatioText => $"{Number(CompressionRatio, "0.0")} : 1";

    /// <summary>Engine speed chips: idling, town, motorway, flat out (rpm).</summary>
    public OscillationChoice RpmChoice { get; } = new("3000");

    [ObservableProperty]
    private double _rpm = 3000;

    /// <summary>The outside air's pressure at the shared observer location's height, bar.</summary>
    public double Ambient => EngineModel.AmbientPressure(_location.Current.Elevation);

    /// <summary>Under the chips: the engine is breathing the air of the shared observer location.</summary>
    public string AltitudeNote => _localization.Format(
        "Heat_Engine_AltitudeNote",
        _location.Current.Name,
        Number(_location.Current.Elevation, "0"),
        Number(Ambient * 1000, "0"),
        Number(Ambient / EngineModel.SeaLevelPressure * 100, "0"));

    partial void OnAngleChanged(double value)
    {
        if (Mode == HeatMode.Engine) Refresh();
    }

    partial void OnCompressionRatioChanged(double value) => Refresh(force: true);

    partial void OnRpmChanged(double value) => Refresh(force: true);

    private void OnRpmChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Rpm = double.Parse(RpmChoice.Selected, CultureInfo.InvariantCulture);
    }

    /// <summary>Jumps to the middle of a stroke (by <see cref="EngineStroke"/> name), or to the spark, and holds it there.</summary>
    [RelayCommand]
    private void JumpToStroke(string? target)
    {
        Angle = target == "Spark"
            ? EngineModel.SparkAngle + 12
            : Enum.TryParse<EngineStroke>(target, out var stroke) ? ((int)stroke * 180) + 90 : Angle;
        IsPlaying = false;
    }

    [RelayCommand]
    private void ResetCompression() => CompressionRatio = EngineModel.DefaultCompressionRatio;

    #endregion

    #region the fridge

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoomText))]
    private double _room = FridgeModel.DefaultRoom;

    public string RoomText => $"{Number(Room, "0")} °C";

    [ObservableProperty]
    private bool _doorOpen;

    partial void OnRoomChanged(double value) => Refresh(force: true);

    partial void OnDoorOpenChanged(bool value) => Refresh(force: true);

    [RelayCommand]
    private void SetRoom(string? room)
    {
        if (double.TryParse(room, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Room = value;
    }

    #endregion

    #region the ideal cycle

    /// <summary>Where in the engine's cycle the gas is, 0…4 (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _phase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotText))]
    private double _hot = 500;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColdText))]
    private double _cold = 300;

    public string HotText => $"{Number(Hot, "0")} K · {Number(Hot - 273.15, "0")} °C";

    public string ColdText => $"{Number(Cold, "0")} K · {Number(Cold - 273.15, "0")} °C";

    /// <summary>Engine, fridge or heat pump, by <see cref="CycleDirection"/> name.</summary>
    public OscillationChoice DirectionChoice { get; } = new(nameof(CycleDirection.Engine));

    [ObservableProperty]
    private CycleDirection _direction;

    partial void OnPhaseChanged(double value)
    {
        if (Mode == HeatMode.Cycle) Refresh();
    }

    partial void OnHotChanged(double value)
    {
        if (Cold > value - 10) Cold = value - 10;
        Refresh(force: true);
    }

    partial void OnColdChanged(double value)
    {
        if (Hot < value + 10) Hot = value + 10;
        Refresh(force: true);
    }

    private void OnDirectionChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Direction = Enum.Parse<CycleDirection>(DirectionChoice.Selected);
    }

    partial void OnDirectionChanged(CycleDirection value)
    {
        DirectionChoice.Selected = value.ToString();
        Refresh(force: true);
    }

    /// <summary>Real pairs of temperatures (by name): a power station, a car engine, a fridge, a heat pump in winter.</summary>
    [RelayCommand]
    private void UsePreset(string? preset)
    {
        (Hot, Cold, Direction) = preset switch
        {
            "PowerStation" => (840.0, 300.0, CycleDirection.Engine),
            "CarEngine" => (900.0, 300.0, CycleDirection.Engine),
            "Fridge" => (313.0, 248.0, CycleDirection.Fridge),
            "HeatPump" => (318.0, 268.0, CycleDirection.HeatPump),
            _ => (Hot, Cold, Direction),
        };
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

    private void Refresh(bool force = false)
    {
        // The engine's card follows the crank every 5°, the cycle's every twentieth of a step.
        var key = Mode switch
        {
            HeatMode.Engine => $"E|{Math.Floor(EngineModel.Wrap(Angle) / 5)}",
            HeatMode.Cycle => $"C|{Math.Floor(Phase * 20)}",
            _ => "F",
        };
        if (!force && key == _shownKey) return;
        _shownKey = key;

        switch (Mode)
        {
            case HeatMode.Fridge:
                RefreshFridge();
                break;

            case HeatMode.Cycle:
                RefreshCycle();
                break;

            default:
                RefreshEngine();
                break;
        }
    }

    private void RefreshEngine()
    {
        var state = EngineModel.At(Angle, CompressionRatio, Ambient);
        var stroke = state.Stroke;
        var burning = state.Angle > EngineModel.SparkAngle && state.Angle < 420 && state.Burned < 0.98;
        var key = burning ? "Burning" : stroke.ToString();
        PhaseTitle = _localization.GetString($"Heat_Engine_{key}_Title");
        PhaseDescription = _localization.GetString($"Heat_Engine_{key}_Description");
        PhaseBrush = StrokeBrushes[(int)stroke];

        // The real engine at this speed does a whole cycle in 120/rpm seconds; the picture's 1× takes 4 s.
        var slowdown = 4 / (120 / Rpm) / Math.Max(0.05, Speed);
        ClockText = _localization.Format("Heat_Engine_Clock", (int)stroke + 1, _localization.GetString($"Heat_Stroke_{stroke}"), Number(state.Angle, "0"));

        var work = EngineModel.WorkPerCycle(CompressionRatio, Ambient);
        var power = EngineModel.Power(Rpm, CompressionRatio, Ambient);
        Stats =
        [
            new(_localization.GetString("Heat_Stat_Stroke"), _localization.Format("Heat_Stat_Stroke_Value", (int)stroke + 1, _localization.GetString($"Heat_Stroke_{stroke}"))),
            new(_localization.GetString("Heat_Stat_Crank"), $"{Number(state.Angle, "0")}°"),
            new(_localization.GetString("Heat_Stat_Pressure"), $"{Number(state.Pressure, state.Pressure < 10 ? "0.00" : "0.0")} bar"),
            new(_localization.GetString("Heat_Stat_Temperature"), $"{Number(state.Temperature - 273.15, "0")} °C"),
            new(_localization.GetString("Heat_Stat_Volume"), $"{Number(state.Volume * 1e6, "0")} cm³"),
            new(_localization.GetString("Heat_Stat_Work"), _localization.Format("Heat_Stat_Work_Value", Number(work, "0"), Number(EngineModel.MeanEffectivePressure(CompressionRatio, Ambient), "0.0"))),
            new(_localization.GetString("Heat_Stat_Power"), _localization.Format("Heat_Stat_Power_Value", Number(Rpm, "0"), Number(power / 1000, "0"))),
            new(_localization.GetString("Heat_Stat_Slowdown"), _localization.Format("Heat_Stat_Slowdown_Value", Number(Rpm / 120, "0.#"), Number(slowdown, "0"))),
        ];
    }

    private void RefreshFridge()
    {
        var room = Room;
        var duty = FridgeModel.DutyCycle(room, DoorOpen);
        var power = FridgeModel.CompressorPower(room);
        var cop = FridgeModel.RealCop(room);
        var key = DoorOpen ? "DoorOpen" : room >= 32 ? "Hot" : "Running";
        PhaseTitle = _localization.GetString($"Heat_Fridge_{key}_Title");
        PhaseDescription = _localization.GetString($"Heat_Fridge_{key}_Description");
        PhaseBrush = DoorOpen ? OrangeBrush : room >= 32 ? RedBrush : CyanBrush;
        ClockText = _localization.Format("Heat_Fridge_Clock", Number(room, "0"));

        var daily = DoorOpen ? duty * power * 24 / 1000 : FridgeModel.DailyEnergy(room);
        Stats =
        [
            new(_localization.GetString("Heat_Stat_Inside"), _localization.Format("Heat_Stat_Inside_Value", Number(FridgeModel.FridgeTemperature, "0"), Number(FridgeModel.FreezerTemperature, "0"))),
            new(_localization.GetString("Heat_Stat_Boiling"), _localization.Format("Heat_Stat_Boiling_Value", Number(FridgeModel.Evaporating, "0"), Number(FridgeModel.LowPressure, "0.00"))),
            new(_localization.GetString("Heat_Stat_Condensing"), _localization.Format("Heat_Stat_Condensing_Value", Number(FridgeModel.Condensing(room), "0"), Number(FridgeModel.HighPressure(room), "0.0"))),
            new(_localization.GetString("Heat_Stat_HeatOut"), _localization.Format("Heat_Stat_HeatOut_Value",
                Number(FridgeModel.CoolingPower, "0"), Number(power, "0"), Number(FridgeModel.CoolingPower + power, "0"))),
            new(_localization.GetString("Heat_Stat_Cop"), _localization.Format("Heat_Stat_Cop_Value", Number(cop, "0.0"), Number(FridgeModel.CarnotCop(room), "0.0"))),
            new(_localization.GetString("Heat_Stat_Duty"), duty >= 0.999
                ? _localization.GetString("Heat_Stat_Duty_Always")
                : _localization.Format("Heat_Stat_Duty_Value", Number(duty * 100, "0"), Number(duty * 60, "0"))),
            new(_localization.GetString("Heat_Stat_Daily"), _localization.Format("Heat_Stat_Daily_Value", Number(daily, "0.00"), Number(daily * 365, "0"))),
        ];
    }

    private void RefreshCycle()
    {
        var hot = Hot;
        var cold = Cold;
        var state = CarnotModel.At(Phase, hot, cold);
        var forward = Direction == CycleDirection.Engine;
        var stepKey = $"Heat_Cycle_Step{state.Step}_{(forward ? "Forward" : "Backward")}";
        PhaseTitle = _localization.Format("Heat_Cycle_StepLabel", HeatDiagram.StepNumber(state.Step, forward), _localization.GetString(stepKey));
        PhaseDescription = _localization.GetString($"{stepKey}_Description");
        PhaseBrush = state.Step switch
        {
            0 => RedBrush,
            2 => BlueBrush,
            _ => VioletBrush,
        };
        ClockText = _localization.Format("Heat_Cycle_Clock", HeatDiagram.StepNumber(state.Step, forward), _localization.GetString(stepKey));

        var efficiency = CarnotModel.Efficiency(hot, cold);
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Heat_Stat_GasTemperature"), $"{Number(state.Temperature, "0")} K · {Number(state.Temperature - 273.15, "0")} °C"),
            new(_localization.GetString("Heat_Stat_Ratio"), _localization.Format("Heat_Stat_Ratio_Value", Number(cold / hot, "0.00"))),
        };
        switch (Direction)
        {
            case CycleDirection.Engine:
                stats.Add(new(_localization.GetString("Heat_Stat_CarnotEfficiency"), $"{Number(efficiency * 100, "0")}%"));
                stats.Add(new(_localization.GetString("Heat_Stat_PerHundred"), _localization.Format("Heat_Stat_PerHundred_Engine", Number(efficiency * 100, "0"), Number(100 - (efficiency * 100), "0"))));
                break;

            case CycleDirection.Fridge:
                var cop = CarnotModel.FridgeCop(hot, cold);
                stats.Add(new(_localization.GetString("Heat_Stat_CarnotCop"), Number(cop, "0.0")));
                stats.Add(new(_localization.GetString("Heat_Stat_PerHundred"), _localization.Format("Heat_Stat_PerHundred_Fridge", Number(cop * 100, "0"), Number((cop + 1) * 100, "0"))));
                break;

            default:
                var pump = CarnotModel.HeatPumpCop(hot, cold);
                stats.Add(new(_localization.GetString("Heat_Stat_CarnotCop"), Number(pump, "0.0")));
                stats.Add(new(_localization.GetString("Heat_Stat_PerHundred"), _localization.Format("Heat_Stat_PerHundred_HeatPump", Number(pump * 100, "0"), Number((pump - 1) * 100, "0"))));
                break;
        }

        Stats = stats;
    }

    #endregion

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Ambient));
        OnPropertyChanged(nameof(AltitudeNote));
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
        OnPropertyChanged(nameof(AltitudeNote));
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        RpmChoice.PropertyChanged -= OnRpmChosen;
        DirectionChoice.PropertyChanged -= OnDirectionChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
