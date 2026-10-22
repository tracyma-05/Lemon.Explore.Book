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

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>A chip for one way a molecule can wiggle; checking it shows that wiggle.</summary>
public sealed partial class VibrationOption(string id, string name, bool active, Action<string> select) : ObservableObject
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    /// <summary>Does this wiggle catch infrared (shift the molecule's charge)?</summary>
    public bool Active { get; } = active;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value) select(Id);
    }
}

/// <summary>
/// The greenhouse-effect page: which view is shown, playback, the planet's CO₂, the molecule and its wiggle, the
/// glass-house day's season and options, and what is happening. The diagram (<see cref="GreenhouseDiagram"/>) turns
/// <see cref="Hour"/> and opens <see cref="VentsOpen"/> and <see cref="CarWindowOpen"/>. <see cref="Lesson"/> is the
/// spoken walkthrough of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class GreenhouseViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9F, 0x5A);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x6A, 0x5A);
    private static readonly IBrush GreenBrush = Frozen(0x8B, 0xE0, 0x8F);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xB0, 0xFF);
    private static readonly IBrush DimBrush = Frozen(0x9A, 0xA5, 0xB5);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    /// <summary>Where the reader is, for the left page's location chip.</summary>
    public IExploreLocationService Location => _location;

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while the day runs.</summary>
    private string _shownKey = string.Empty;

    public GreenhouseViewModel(IExploreLocationService location, INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => GreenhouseLessons.For(this));
        Card = new ObjectCard(narration, Lesson, GreenhouseDiagram.ObjectTitleKey, GreenhouseDiagram.ObjectClipKey);
        _location = location;
        location.Changed += OnLocationChanged;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        GasChoice.PropertyChanged += OnGasChosen;
        SeasonChoice.PropertyChanged += OnSeasonChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        BuildVibrations();
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
    [NotifyPropertyChangedFor(nameof(IsPlanet), nameof(IsMolecules), nameof(IsGlassHouse), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private GreenhouseMode _mode;

    public bool IsPlanet
    {
        get => Mode == GreenhouseMode.Planet;
        set
        {
            if (value) Mode = GreenhouseMode.Planet;
        }
    }

    public bool IsMolecules
    {
        get => Mode == GreenhouseMode.Molecules;
        set
        {
            if (value) Mode = GreenhouseMode.Molecules;
        }
    }

    public bool IsGlassHouse
    {
        get => Mode == GreenhouseMode.GlassHouse;
        set
        {
            if (value) Mode = GreenhouseMode.GlassHouse;
        }
    }

    public string Subtitle => _localization.GetString($"Green_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Green_{Mode}_Note");

    public string Hint => _localization.GetString($"Green_Hint_{Mode}");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Green_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(GreenhouseMode value)
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

    #region the planet

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Co2Text))]
    private double _co2 = PlanetModel.TodayCo2;

    public double MinCo2 => PlanetModel.MinCo2;

    public double MaxCo2 => PlanetModel.MaxCo2;

    public string Co2Text => NoGases ? _localization.GetString("Green_Co2_None") : $"{Number(Co2, "0")} ppm";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Co2Text), nameof(HasGases))]
    private bool _noGases;

    public bool HasGases => !NoGases;

    [ObservableProperty]
    private bool _showBudget;

    partial void OnCo2Changed(double value) => Refresh(force: true);

    partial void OnNoGasesChanged(bool value) => Refresh(force: true);

    /// <summary>Sets the CO₂ to a preset amount, ppm (and brings the gases back if they were taken away).</summary>
    [RelayCommand]
    private void SetCo2(string? ppm)
    {
        if (!double.TryParse(ppm, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        NoGases = false;
        Co2 = value;
    }

    #endregion

    #region the molecules

    /// <summary>The molecule shown, by <see cref="Chapters.Greenhouse.Gas"/> name.</summary>
    public OscillationChoice GasChoice { get; } = new(nameof(Chapters.Greenhouse.Gas.CO2));

    [ObservableProperty]
    private Gas _gas = Chapters.Greenhouse.Gas.CO2;

    /// <summary>The chosen molecule's ways to wiggle, as chips.</summary>
    [ObservableProperty]
    private IReadOnlyList<VibrationOption> _vibrations = [];

    [ObservableProperty]
    private string _vibrationId = "Bend";

    public bool HasVibrations => Vibrations.Count > 0;

    private void OnGasChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Gas = Enum.Parse<Gas>(GasChoice.Selected);
    }

    partial void OnGasChanged(Gas value)
    {
        GasChoice.Selected = value.ToString();
        BuildVibrations();
        Refresh(force: true);
    }

    partial void OnVibrationIdChanged(string value)
    {
        foreach (var option in Vibrations) option.IsSelected = option.Id == value;
        Refresh(force: true);
    }

    partial void OnVibrationsChanged(IReadOnlyList<VibrationOption> value) => OnPropertyChanged(nameof(HasVibrations));

    /// <summary>The chips for the molecule's wiggles; the first one that catches infrared is shown first.</summary>
    private void BuildVibrations()
    {
        var molecule = Molecules.Get(Gas);
        var options = molecule.Modes
            .Select(m => new VibrationOption(m.Id, _localization.GetString($"Green_Vib_{m.Id}"), m.Active, id => VibrationId = id))
            .ToList();
        var keep = molecule.Modes.Any(m => m.Id == VibrationId)
            ? VibrationId
            : (molecule.Modes.FirstOrDefault(m => m.Active) ?? molecule.Modes.FirstOrDefault())?.Id ?? string.Empty;
        foreach (var option in options) option.IsSelected = option.Id == keep;
        Vibrations = options;
        VibrationId = keep;
    }

    #endregion

    #region the glass house

    /// <summary>The hour of the day, 0…24 (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _hour = 10;

    /// <summary>Summer, spring or autumn, winter (by <see cref="Chapters.Greenhouse.Season"/> name).</summary>
    public OscillationChoice SeasonChoice { get; } = new(nameof(Chapters.Greenhouse.Season.Summer));

    [ObservableProperty]
    private Season _season = Chapters.Greenhouse.Season.Summer;

    [ObservableProperty]
    private bool _ventsOpen;

    [ObservableProperty]
    private bool _saltPane;

    [ObservableProperty]
    private bool _carWindowOpen;

    /// <summary>The shared observer location's latitude, which sets the Sun's path.</summary>
    public double Latitude => _location.Current.Latitude;

    /// <summary>Under the season chips: where the Sun's path is for, and what the outside air is.</summary>
    public string LocationNote
    {
        get
        {
            var (low, high) = GlassHouseModel.OutsideRange(Season);
            return _localization.Format("Green_Glass_LocationNote", _location.Current.Name, Number(Math.Abs(Latitude), "0.0"),
                _localization.GetString(Latitude >= 0 ? "Green_North" : "Green_South"), Number(low, "0"), Number(high, "0"));
        }
    }

    partial void OnHourChanged(double value)
    {
        if (Mode == GreenhouseMode.GlassHouse) Refresh();
    }

    private void OnSeasonChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Season = Enum.Parse<Season>(SeasonChoice.Selected);
    }

    partial void OnSeasonChanged(Season value)
    {
        SeasonChoice.Selected = value.ToString();
        OnPropertyChanged(nameof(LocationNote));
        Refresh(force: true);
    }

    partial void OnVentsOpenChanged(bool value) => Refresh(force: true);

    partial void OnSaltPaneChanged(bool value) => Refresh(force: true);

    partial void OnCarWindowOpenChanged(bool value) => Refresh(force: true);

    /// <summary>Jumps to an hour of the day (e.g. "14") and holds it there.</summary>
    [RelayCommand]
    private void JumpToHour(string? hour)
    {
        if (!double.TryParse(hour, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        Hour = value;
        IsPlaying = false;
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
        // The day's card follows the clock every six minutes; the other views change only with their settings.
        var key = Mode == GreenhouseMode.GlassHouse ? $"G|{Math.Floor(Hour * 10)}" : Mode.ToString();
        if (!force && key == _shownKey) return;
        _shownKey = key;

        switch (Mode)
        {
            case GreenhouseMode.Molecules:
                RefreshMolecules();
                break;

            case GreenhouseMode.GlassHouse:
                RefreshGlassHouse();
                break;

            default:
                RefreshPlanet();
                break;
        }
    }

    private void RefreshPlanet()
    {
        var key = NoGases ? "Bare" : Co2 < 240 ? "IceAge" : Co2 < 320 ? "Preindustrial" : Co2 < 480 ? "Today" : "More";
        PhaseTitle = _localization.GetString($"Green_Planet_{key}_Title");
        PhaseDescription = _localization.GetString($"Green_Planet_{key}_Description");
        PhaseBrush = key switch
        {
            "Bare" => CyanBrush,
            "IceAge" => BlueBrush,
            "Preindustrial" => GreenBrush,
            "Today" => GoldBrush,
            _ => RedBrush,
        };

        var surface = NoGases ? PlanetModel.BareCelsius : PlanetModel.SurfaceCelsius(Co2);
        ClockText = _localization.Format("Green_Planet_Clock", Co2Text, Number(surface, "0.0"));
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Green_Stat_Co2"), Co2Text),
            new(_localization.GetString("Green_Stat_Surface"), $"{Number(surface, "0.0")} °C"),
            new(_localization.GetString("Green_Stat_Bare"), _localization.Format("Green_Stat_Bare_Value",
                Number(PlanetModel.BareCelsius, "0.0"), Number(surface - PlanetModel.BareCelsius, "0.0"))),
        };
        if (!NoGases)
        {
            var forcing = PlanetModel.Forcing(Co2);
            stats.Add(new(_localization.GetString("Green_Stat_Forcing"), _localization.Format("Green_Stat_Forcing_Value", (forcing >= 0 ? "+" : "−") + Number(Math.Abs(forcing), "0.0"))));
            stats.Add(new(_localization.GetString("Green_Stat_Emission"), _localization.Format("Green_Stat_Emission_Value",
                Number(PlanetModel.EmissionHeight(Co2), "0.0"), Number(PlanetModel.BareCelsius, "0.0"))));
            stats.Add(new(_localization.GetString("Green_Stat_Window"), _localization.Format("Green_Stat_Window_Value", Number(EnergyBudget.WindowShare * 100, "0"))));
        }

        stats.Add(new(_localization.GetString("Green_Stat_Glow"), _localization.Format("Green_Stat_Glow_Value",
            Number(PlanetModel.SurfaceEmission(surface), "0"), Number(EnergyBudget.AbsorbedBySurface, "0"))));
        Stats = stats;
    }

    private void RefreshMolecules()
    {
        var molecule = Molecules.Get(Gas);
        var gas = Gas.ToString();
        PhaseTitle = _localization.GetString($"Green_Gas_{gas}");
        PhaseDescription = _localization.GetString($"Green_Gas_{gas}_Description");
        PhaseBrush = Gas switch
        {
            Chapters.Greenhouse.Gas.CO2 => OrangeBrush,
            Chapters.Greenhouse.Gas.H2O => BlueBrush,
            Chapters.Greenhouse.Gas.CH4 => GreenBrush,
            _ => DimBrush,
        };

        var mode = molecule.Modes.FirstOrDefault(m => m.Id == VibrationId);
        ClockText = mode is null
            ? _localization.GetString($"Green_Gas_{gas}_Short")
            : _localization.Format("Green_Mol_Clock", _localization.GetString($"Green_Gas_{gas}_Short"), _localization.GetString($"Green_Vib_{mode.Id}"));

        var active = molecule.Modes.Count(m => m.Active);
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Green_Stat_AirShare"), _localization.GetString($"Green_Gas_{gas}_Share")),
            new(_localization.GetString("Green_Stat_Shape"), _localization.GetString($"Green_Gas_{gas}_Shape")),
            new(_localization.GetString("Green_Stat_Wiggles"), molecule.Modes.Length == 0
                ? _localization.GetString("Green_Stat_Wiggles_None")
                : _localization.Format("Green_Stat_Wiggles_Value", molecule.Modes.Length, active)),
        };
        if (mode is not null)
        {
            var wave = mode.Spin
                ? _localization.GetString("Green_Mol_SpinBand")
                : _localization.Format("Green_Mol_ModeBand", Number(mode.Wavenumber, "0"), Number(mode.Wavelength, mode.Wavelength < 10 ? "0.00" : "0.0"));
            stats.Add(new(_localization.GetString("Green_Stat_Wiggle"), _localization.Format(mode.Active ? "Green_Stat_Wiggle_Active" : "Green_Stat_Wiggle_Inactive",
                _localization.GetString($"Green_Vib_{mode.Id}"), wave)));
        }

        stats.Add(new(_localization.GetString("Green_Stat_Caught"), _localization.Format("Green_Stat_Caught_Value", Number(InfraredModel.CaughtShare(Gas) * 100, "0"))));
        stats.Add(new(_localization.GetString("Green_Stat_Lifetime"), _localization.GetString($"Green_Gas_{gas}_Lifetime")));
        Stats = stats;
    }

    private void RefreshGlassHouse()
    {
        var options = new DayOptions(Latitude, Season, VentsOpen, SaltPane, CarWindowOpen);
        var state = GlassHouseModel.At(options, Hour);
        var key = state.SunAltitude <= 0 ? "Night" : Hour < 12 ? "Morning" : state.Sunlight < 350 ? "Evening" : VentsOpen ? "AfternoonVents" : "Afternoon";
        PhaseTitle = _localization.GetString($"Green_Glass_{key}_Title");
        PhaseDescription = _localization.GetString($"Green_Glass_{key}_Description");
        PhaseBrush = key switch
        {
            "Night" => BlueBrush,
            "Morning" => GoldBrush,
            "Afternoon" => RedBrush,
            "AfternoonVents" => GreenBrush,
            _ => OrangeBrush,
        };

        var clock = Clock(Hour);
        ClockText = state.SunAltitude > 0
            ? _localization.Format("Green_Glass_Clock", clock, Number(state.SunAltitude, "0"))
            : _localization.Format("Green_Glass_ClockNight", clock);

        string Warmer(double value) => _localization.Format("Green_Stat_Warmer_Value", Number(value, "0"), (value - state.Outside >= 0 ? "+" : "−") + Number(Math.Abs(value - state.Outside), "0"));
        Stats =
        [
            new(_localization.GetString("Green_Stat_Time"), _localization.Format("Green_Stat_Time_Value", clock)),
            new(_localization.GetString("Green_Stat_Sun"), state.SunAltitude > 0
                ? _localization.Format("Green_Stat_Sun_Value", Number(state.SunAltitude, "0"), Number(state.Sunlight, "0"))
                : _localization.GetString("Green_Stat_Sun_Down")),
            new(_localization.GetString("Green_Stat_Outside"), $"{Number(state.Outside, "0")} °C"),
            new(_localization.GetString("Green_Stat_Greenhouse"), Warmer(state.Greenhouse)),
            new(_localization.GetString("Green_Stat_Floor"), $"{Number(state.Floor, "0")} °C"),
            new(_localization.GetString("Green_Stat_Car"), Warmer(state.Car)),
            new(_localization.GetString("Green_Stat_AirChanges"), _localization.Format(VentsOpen ? "Green_Stat_AirChanges_Open" : "Green_Stat_AirChanges_Shut",
                Number(VentsOpen ? GlassHouseModel.OpenAirChanges : GlassHouseModel.ShutAirChanges, "0.#"))),
            new(_localization.GetString("Green_Stat_Roof"), _localization.GetString(SaltPane ? "Green_Stat_Roof_Salt" : "Green_Stat_Roof_Glass")),
        ];
    }

    /// <summary>A local solar time as h:mm.</summary>
    private static string Clock(double hour)
    {
        var minutes = (int)Math.Round(hour * 60) % (24 * 60);
        return $"{minutes / 60}:{minutes % 60:00}";
    }

    #endregion

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(LocationNote));
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
        OnPropertyChanged(nameof(Co2Text));
        OnPropertyChanged(nameof(LocationNote));
        BuildVibrations();
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        GasChoice.PropertyChanged -= OnGasChosen;
        SeasonChoice.PropertyChanged -= OnSeasonChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
