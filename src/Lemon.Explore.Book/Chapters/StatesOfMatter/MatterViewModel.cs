using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Book.Shell.Controls.Explore;
using Lemon.Book.Shell.Explore.Lessons;
using Lemon.Book.Shell.Explore.Narration;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Book.Shell.Infrastructures.Timing;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The states of matter page: which view is shown; the substance; the temperature and pressure the cylinder and the
/// phase diagram share; the heating view's place, burner and the heat put in so far; and what is happening. The diagram
/// (<see cref="MatterDiagram"/>) runs the burner and moves the molecules. <see cref="Lesson"/> is the spoken walkthrough
/// of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class MatterViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xA8, 0xFF);
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>The heat runs up every frame while the burner is on; the card is rebuilt a few times a second, or when its state changes.</summary>
    private readonly BookStopwatch _sinceRefresh = BookStopwatch.StartNew();

    /// <summary>Brings the card up to date shortly after a burst of changes stops (the last change of a drag).</summary>
    private readonly Avalonia.Threading.DispatcherTimer _lateRefresh = new() { Interval = TimeSpan.FromSeconds(0.3) };

    private string _state = string.Empty;

    public MatterViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => MatterLessons.For(this));
        Card = new ObjectCard(narration, Lesson, MatterDiagram.ObjectTitleKey, MatterDiagram.ObjectClipKey);
        SubstanceChoice.PropertyChanged += OnSubstanceChosen;
        PlaceChoice.PropertyChanged += OnPlaceChosen;
        HeatChoice.PropertyChanged += OnHeatChosen;
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
    [NotifyPropertyChangedFor(nameof(IsParticles), nameof(IsHeating), nameof(IsDiagram), nameof(IsStateView), nameof(Subtitle), nameof(ModeNote),
        nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private MatterMode _mode;

    public bool IsParticles
    {
        get => Mode == MatterMode.Particles;
        set
        {
            if (value) Mode = MatterMode.Particles;
        }
    }

    public bool IsHeating
    {
        get => Mode == MatterMode.Heating;
        set
        {
            if (value) Mode = MatterMode.Heating;
        }
    }

    public bool IsDiagram
    {
        get => Mode == MatterMode.Diagram;
        set
        {
            if (value) Mode = MatterMode.Diagram;
        }
    }

    /// <summary>The cylinder and the phase diagram: both show the temperature and pressure the sliders set.</summary>
    public bool IsStateView => Mode != MatterMode.Heating;

    public string Subtitle => _localization.GetString($"Matter_{Mode}_Subtitle");

    public string ModeNote => _localization.GetString($"Matter_{Mode}_Note");

    public string Hint => _localization.GetString($"Matter_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Matter_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Matter_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Matter_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(MatterMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Heat = HeatSetting.Off;
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
    private bool _showLabels = true;

    /// <summary>The solid's bonds drawn between neighbouring molecules.</summary>
    [ObservableProperty]
    private bool _showBonds = true;

    #endregion

    #region the substance

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Model), nameof(MinCelsius), nameof(MaxCelsius), nameof(State), nameof(Curve), nameof(TotalKj))]
    private MatterSubstance _substance = MatterSubstance.Water;

    public OscillationChoice SubstanceChoice { get; } = new(nameof(MatterSubstance.Water));

    public MatterModel.Substance Model => MatterModel.Of(Substance);

    public double MinCelsius => Model.MinCelsius;

    public double MaxCelsius => Model.MaxCelsius;

    partial void OnSubstanceChanged(MatterSubstance value)
    {
        SubstanceChoice.Selected = value.ToString();

        // Each substance starts at room pressure in its everyday state: water at 20 °C, carbon dioxide's dry ice at −90 °C.
        Celsius = value == MatterSubstance.CarbonDioxide ? -90 : 20;
        PressureLog = Math.Log10(MatterModel.Atmosphere);
        HeatKj = 0;
        Heat = HeatSetting.Off;
        RefreshSoon(force: true);
    }

    private void OnSubstanceChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<MatterSubstance>(SubstanceChoice.Selected, out var substance)) Substance = substance;
    }

    #endregion

    #region temperature and pressure

    /// <summary>The temperature of the cylinder and the phase diagram's dot, °C.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kelvin), nameof(State), nameof(CelsiusText))]
    private double _celsius = 20;

    /// <summary>The pressure as a power of ten of pascals (0 is 1 Pa, 8 is 100 MPa), so a slider covers it evenly.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pressure), nameof(State), nameof(PressureText))]
    private double _pressureLog = Math.Log10(MatterModel.Atmosphere);

    public double Kelvin => Celsius + MatterModel.ZeroCelsius;

    public double Pressure => Math.Pow(10, PressureLog);

    public MatterState State => MatterModel.StateAt(Model, Kelvin, Pressure);

    public string CelsiusText => _localization.Format("Matter_Value_Celsius", Number(Celsius, "0"));

    public string PressureText => PressureWords(Pressure);

    partial void OnCelsiusChanged(double value)
    {
        var clamped = Math.Clamp(value, MinCelsius, MaxCelsius);
        if (clamped != value)
        {
            Celsius = clamped;
            return;
        }

        RefreshSoon();
    }

    partial void OnPressureLogChanged(double value)
    {
        var clamped = Math.Clamp(value, 0, 8);
        if (clamped != value)
        {
            PressureLog = clamped;
            return;
        }

        RefreshSoon();
    }

    /// <summary>Puts the temperature and pressure at once (walkthrough, the chart's places).</summary>
    public void SetState(double celsius, double pascals)
    {
        Celsius = celsius;
        PressureLog = Math.Log10(Math.Clamp(pascals, MatterModel.MinPressure, MatterModel.MaxPressure));
    }

    [RelayCommand]
    private void Warmer() => Celsius = Math.Min(MaxCelsius, Math.Round(Celsius + 10));

    [RelayCommand]
    private void Cooler() => Celsius = Math.Max(MinCelsius, Math.Round(Celsius - 10));

    [RelayCommand]
    private void Squeeze() => PressureLog = Math.Min(8, Math.Round((PressureLog + 0.5) * 2) / 2);

    [RelayCommand]
    private void Release() => PressureLog = Math.Max(0, Math.Round((PressureLog - 0.5) * 2) / 2);

    /// <summary>Back to room pressure.</summary>
    [RelayCommand]
    private void OneAtmosphere() => PressureLog = Math.Log10(MatterModel.Atmosphere);

    #endregion

    #region heating

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeatingPressure), nameof(Curve), nameof(TotalKj))]
    private HeatingPlace _place = HeatingPlace.SeaLevel;

    public OscillationChoice PlaceChoice { get; } = new(nameof(HeatingPlace.SeaLevel));

    [ObservableProperty]
    private HeatSetting _heat = HeatSetting.Off;

    public OscillationChoice HeatChoice { get; } = new(nameof(HeatSetting.Off));

    /// <summary>The heat put into the sample so far, kJ; the burner runs it up.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeatCelsius), nameof(HeatKjText))]
    private double _heatKj;

    public double HeatingPressure => MatterModel.PressureAt(Place);

    public IReadOnlyList<MatterModel.Stretch> Curve => MatterModel.HeatingCurve(Model, HeatingPressure);

    public double TotalKj => MatterModel.TotalKj(Curve);

    public double HeatCelsius => MatterModel.CelsiusAt(Curve, HeatKj);

    public MatterModel.Stretch Stretch => MatterModel.StretchAt(Curve, HeatKj);

    public (double Solid, double Liquid, double Gas) Fractions => MatterModel.FractionsAt(Curve, HeatKj);

    public string HeatKjText => _localization.Format("Matter_Value_Kj", Number(HeatKj, "0.0"));

    partial void OnPlaceChanged(HeatingPlace value)
    {
        PlaceChoice.Selected = value.ToString();
        HeatKj = Math.Min(HeatKj, TotalKj);
        RefreshSoon(force: true);
    }

    partial void OnHeatChanged(HeatSetting value) => HeatChoice.Selected = value.ToString();

    partial void OnHeatKjChanged(double value)
    {
        var clamped = Math.Clamp(value, 0, TotalKj);
        if (clamped != value)
        {
            HeatKj = clamped;
            return;
        }

        RefreshSoon();
    }

    private void OnPlaceChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<HeatingPlace>(PlaceChoice.Selected, out var place)) Place = place;
    }

    private void OnHeatChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<HeatSetting>(HeatChoice.Selected, out var heat)) Heat = heat;
    }

    /// <summary>A fresh sample at its start temperature, burner off.</summary>
    [RelayCommand]
    public void Restart()
    {
        Heat = HeatSetting.Off;
        HeatKj = 0;
    }

    /// <summary>Holds the heating at <paramref name="kj"/>, burner off.</summary>
    public void HoldAt(double kj)
    {
        Heat = HeatSetting.Off;
        HeatKj = Math.Clamp(kj, 0, TotalKj);
    }

    /// <summary>Lights the burner from <paramref name="kj"/>.</summary>
    public void RunFrom(double kj, HeatSetting heat)
    {
        HeatKj = Math.Clamp(kj, 0, TotalKj);
        Heat = heat;
    }

    /// <summary>The heat at a share of the way through the stretch of <paramref name="kind"/> (the end of the curve if there is none).</summary>
    public double KjInto(string kind, double share)
    {
        var stretch = Curve.FirstOrDefault(s => s.Kind == kind);
        return stretch is null ? TotalKj : stretch.StartKj + (share * stretch.Kj);
    }

    #endregion

    #region what is happening

    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = CyanBrush;
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

    private string CurrentState => Mode == MatterMode.Heating
        ? $"{Substance}{Place}{Stretch.Kind}"
        : $"{Substance}{State}";

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
        if (Mode == MatterMode.Heating) RefreshHeating();
        else RefreshState();
    }

    /// <summary>The resource key naming a state of a substance: <c>Matter_Name_Water_Solid</c> (ice).</summary>
    internal static string StateNameKey(MatterSubstance substance, MatterState state) => $"Matter_Name_{substance}_{state}";

    internal static IBrush StateBrush(MatterState state) => state switch
    {
        MatterState.Solid => CyanBrush,
        MatterState.Liquid => BlueBrush,
        MatterState.Gas => GoldBrush,
        _ => VioletBrush,
    };

    private void RefreshState()
    {
        var s = Model;
        var state = State;
        PhaseTitle = _localization.GetString(StateNameKey(Substance, state));
        PhaseDescription = _localization.GetString($"Matter_State_{state}_Text");
        PhaseBrush = StateBrush(state);

        var p = Pressure;
        var limit = MatterModel.SolidLimit(s, p) - MatterModel.ZeroCelsius;
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Matter_Stat_Temperature"), _localization.Format("Matter_Value_CelsiusKelvin", Number(Celsius, "0"), Number(Kelvin, "0"))),
            new(_localization.GetString("Matter_Stat_Pressure"), PressureWords(p)),
            new(_localization.GetString("Matter_Stat_State"), _localization.GetString($"Matter_State_{state}")),
            new(_localization.GetString(p < s.TripleP ? "Matter_Stat_Sublimes" : "Matter_Stat_Melts"),
                _localization.Format("Matter_Value_Celsius", Number(limit, "0.0"))),
        };
        if (p >= s.TripleP && p < s.CriticalP)
        {
            stats.Add(new(_localization.GetString("Matter_Stat_Boils"),
                _localization.Format("Matter_Value_Celsius", Number(MatterModel.BoilingTemperature(s, p) - MatterModel.ZeroCelsius, "0.0"))));
        }

        stats.Add(new(_localization.GetString("Matter_Stat_Speed"), _localization.Format("Matter_Value_Speed", Number(Speed(Substance, Kelvin), "0"))));
        Stats = stats;
    }

    private void RefreshHeating()
    {
        var stretch = Stretch;
        PhaseTitle = _localization.GetString($"Matter_Stretch_{stretch.Kind}_Title");
        PhaseDescription = _localization.GetString($"Matter_Stretch_{stretch.Kind}_{Substance}");
        PhaseBrush = stretch.Kind switch
        {
            "WarmSolid" => CyanBrush,
            "WarmLiquid" => BlueBrush,
            "WarmGas" => GoldBrush,
            _ => OrangeBrush,
        };

        var (solid, liquid, gas) = Fractions;
        var curve = Curve;
        var change = curve.FirstOrDefault(c => c.Kind is "Boil" or "Sublime");
        Stats =
        [
            new(_localization.GetString("Matter_Stat_Heat"), HeatKjText),
            new(_localization.GetString("Matter_Stat_Temperature"), _localization.Format("Matter_Value_Celsius", Number(HeatCelsius, "0.0"))),
            new(_localization.GetString("Matter_Stat_Pressure"), PressureWords(HeatingPressure)),
            new(_localization.GetString(change?.Kind == "Sublime" ? "Matter_Stat_Sublimes" : "Matter_Stat_Boils"),
                _localization.Format("Matter_Value_Celsius", Number(change?.StartC ?? 0, "0.0"))),
            new(_localization.GetString("Matter_Stat_Shares"), _localization.Format("Matter_Value_Shares",
                Number(solid * 100, "0"), Number(liquid * 100, "0"), Number(gas * 100, "0"))),
        ];
    }

    /// <summary>The molecules' root-mean-square speed, m/s: √(3RT/M).</summary>
    internal static double Speed(MatterSubstance substance, double kelvin)
    {
        var molarMass = substance == MatterSubstance.CarbonDioxide ? 0.04401 : 0.018015;
        return Math.Sqrt(3 * 8.314 * kelvin / molarMass);
    }

    /// <summary>A pressure in words: pascals, kilopascals or megapascals, and in atmospheres from a hundredth of one up.</summary>
    internal static string PressureWords(double pascals)
    {
        var localization = LocalizationService.Instance;
        var value = pascals < 1000
            ? localization.Format("Matter_Value_Pa", Number(pascals, pascals < 10 ? "0.0" : "0"))
            : pascals < 1e6
                ? localization.Format("Matter_Value_Kpa", Number(pascals / 1000, pascals < 1e4 ? "0.00" : pascals < 1e5 ? "0.0" : "0"))
                : localization.Format("Matter_Value_Mpa", Number(pascals / 1e6, pascals < 1e7 ? "0.00" : "0.0"));
        var atm = pascals / MatterModel.Atmosphere;
        return atm < 0.01 ? value : localization.Format("Matter_Value_WithAtm", value, Number(atm, atm < 10 ? "0.00" : "0"));
    }

    #endregion

    #region formatting

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ModeNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(ChartCaption));
        OnPropertyChanged(nameof(ChartNote));
        OnPropertyChanged(nameof(CelsiusText));
        OnPropertyChanged(nameof(PressureText));
        OnPropertyChanged(nameof(HeatKjText));
        Refresh();
    }

    public void Dispose()
    {
        _lateRefresh.Stop();
        _lateRefresh.Tick -= OnLateRefresh;
        Lesson.Dispose();
        Card.Dispose();
        SubstanceChoice.PropertyChanged -= OnSubstanceChosen;
        PlaceChoice.PropertyChanged -= OnPlaceChosen;
        HeatChoice.PropertyChanged -= OnHeatChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
