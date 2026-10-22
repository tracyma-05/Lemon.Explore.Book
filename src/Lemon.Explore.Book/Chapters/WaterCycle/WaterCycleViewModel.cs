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

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>
/// The water-cycle page: which view is shown, the warmth of the landscape's world, the way of moving picked and the
/// followed drop's route, the air lifted in the cloud view, and what is happening. The diagram
/// (<see cref="WaterCycleDiagram"/>) pushes back <see cref="LegIndex"/> as the drop moves, <see cref="SelectedProcess"/>
/// when a label is clicked, and <see cref="Time"/> as the parcel rises.
/// <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in the diagram
/// (<see cref="Card"/>) explains it.
/// </summary>
public partial class WaterCycleViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x8B, 0xE0, 0x8F);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9F, 0x5A);
    private static readonly IBrush WhiteBrush = Frozen(0xEE, 0xF1, 0xF6);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xB0, 0xFF);
    private static readonly IBrush VioletBrush = Frozen(0xB3, 0x9D, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    private IReadOnlyList<DropStep> _journey = CycleScene.Journey(DropRoute.River, 0);
    private ParcelAscent _ascent = WaterCycleModel.Lift(20, 0.6, 6000);
    private IReadOnlyList<ParcelPoint> _descent = [];

    /// <summary>The tenth of a second of the parcel's way the card was last worked out for.</summary>
    private long _shownTenth = -1;

    public WaterCycleViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => WaterCycleLessons.For(this));
        Card = new ObjectCard(narration, Lesson, WaterCycleDiagram.ObjectTitleKey, WaterCycleDiagram.ObjectClipKey);
        ProcessChoice.PropertyChanged += OnProcessChosen;
        RouteChoice.PropertyChanged += OnRouteChosen;
        LiftChoice.PropertyChanged += OnLiftChosen;
        StoresViewChoice.PropertyChanged += OnStoresViewChosen;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        RebuildCloud();
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
    [NotifyPropertyChangedFor(nameof(IsCycle), nameof(IsCloud), nameof(IsStores), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private WaterMode _mode;

    public bool IsCycle
    {
        get => Mode == WaterMode.Cycle;
        set
        {
            if (value) Mode = WaterMode.Cycle;
        }
    }

    public bool IsCloud
    {
        get => Mode == WaterMode.Cloud;
        set
        {
            if (value) Mode = WaterMode.Cloud;
        }
    }

    public bool IsStores
    {
        get => Mode == WaterMode.Stores;
        set
        {
            if (value) Mode = WaterMode.Stores;
        }
    }

    /// <summary>The mode, or for the stores its picture: picks the subtitle, notes and facts.</summary>
    private string ModeKey => Mode == WaterMode.Stores ? $"Stores_{StoresView}" : Mode.ToString();

    public string Subtitle => _localization.GetString($"Water_{ModeKey}_Subtitle");

    public string ScaleNote => _localization.GetString($"Water_{ModeKey}_Note");

    public string Hint => _localization.GetString($"Water_Hint_{ModeKey}");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Water_{ModeKey}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(WaterMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Refresh();
    }

    /// <summary>The spoken walkthrough of the current view.</summary>
    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region playback

    /// <summary>Playback speed chips: 0.5, 1, 2 or 4 times the normal pace.</summary>
    public OscillationChoice SpeedChoice { get; } = new("1");

    [ObservableProperty]
    private double _speed = 1;

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Goes up to start the drop or the parcel again from the beginning.</summary>
    [ObservableProperty]
    private int _restartCount;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    [RelayCommand]
    private void Restart()
    {
        RestartCount++;
        IsPlaying = true;
    }

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    #endregion

    #region the cycle

    /// <summary>The way of moving lit up, by <see cref="WaterProcess"/> name ("None" for all of them).</summary>
    public OscillationChoice ProcessChoice { get; } = new(nameof(WaterProcess.None));

    /// <summary>The followed drop's route, by <see cref="DropRoute"/> name.</summary>
    public OscillationChoice RouteChoice { get; } = new(nameof(DropRoute.River));

    [ObservableProperty]
    private WaterProcess _selectedProcess;

    [ObservableProperty]
    private DropRoute _route;

    [ObservableProperty]
    private bool _showLabels = true;

    [ObservableProperty]
    private bool _followDrop;

    /// <summary>How much warmer (or colder) than today's world the landscape is, °C.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WarmingText))]
    private double _warming;

    /// <summary>The step of the followed drop's journey (from the diagram).</summary>
    [ObservableProperty]
    private int _legIndex;

    public string WarmingText => Math.Abs(Warming) < 0.05
        ? _localization.GetString("Water_Warming_Today")
        : $"{(Warming > 0 ? "+" : "−")}{Math.Abs(Warming).ToString("0.#", CultureInfo.InvariantCulture)} °C";

    private void OnProcessChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) SelectedProcess = Enum.Parse<WaterProcess>(ProcessChoice.Selected);
    }

    private void OnRouteChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Route = Enum.Parse<DropRoute>(RouteChoice.Selected);
    }

    partial void OnSelectedProcessChanged(WaterProcess value)
    {
        ProcessChoice.Selected = value.ToString();
        Refresh();
    }

    partial void OnRouteChanged(DropRoute value)
    {
        RouteChoice.Selected = value.ToString();
        _journey = CycleScene.Journey(value, Warming);
        Refresh();
    }

    partial void OnFollowDropChanged(bool value) => Refresh();

    partial void OnWarmingChanged(double value)
    {
        _journey = CycleScene.Journey(Route, value);
        Refresh();
    }

    partial void OnLegIndexChanged(int value) => Refresh();

    [RelayCommand]
    private void SetWarming(string? warming)
    {
        if (double.TryParse(warming, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Warming = value;
    }

    #endregion

    #region the cloud

    /// <summary>What lifts the air, by <see cref="CloudLift"/> name.</summary>
    public OscillationChoice LiftChoice { get; } = new(nameof(CloudLift.Thermal));

    [ObservableProperty]
    private CloudLift _lift;

    /// <summary>The air's temperature at the ground, °C.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroundTemperatureText))]
    private double _groundTemperature = 24;

    /// <summary>The air's relative humidity at the ground, percent.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HumidityText), nameof(HumidityShare))]
    private double _humidityPercent = 60;

    /// <summary>Seconds of the parcel's way (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _time;

    public double Duration => CloudScene.Duration;

    public double HumidityShare => HumidityPercent / 100;

    public string GroundTemperatureText => $"{GroundTemperature.ToString("0", CultureInfo.InvariantCulture)} °C";

    public string HumidityText => _localization.Format("Water_Humidity_Value", HumidityPercent.ToString("0", CultureInfo.InvariantCulture),
        WaterCycleModel.DewPoint(GroundTemperature, HumidityShare).ToString("0.0", CultureInfo.InvariantCulture));

    private void OnLiftChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Lift = Enum.Parse<CloudLift>(LiftChoice.Selected);
    }

    partial void OnLiftChanged(CloudLift value)
    {
        LiftChoice.Selected = value.ToString();
        RebuildCloud();
    }

    partial void OnGroundTemperatureChanged(double value)
    {
        OnPropertyChanged(nameof(HumidityText));
        RebuildCloud();
    }

    partial void OnHumidityPercentChanged(double value) => RebuildCloud();

    partial void OnTimeChanged(double value)
    {
        var tenth = (long)Math.Floor(value * 10);
        if (tenth == _shownTenth) return;
        _shownTenth = tenth;
        Refresh();
    }

    private void RebuildCloud()
    {
        _ascent = WaterCycleModel.Lift(GroundTemperature, HumidityShare, WaterCycleModel.LiftTop(Lift));
        _descent = Lift == CloudLift.Mountain ? WaterCycleModel.OverMountain(_ascent) : [];
        _shownTenth = -1;
        Refresh();
    }

    #endregion

    #region the stores

    /// <summary>The stores view's picture, by <see cref="StoresView"/> name.</summary>
    public OscillationChoice StoresViewChoice { get; } = new(nameof(Chapters.WaterCycle.StoresView.Spheres));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private StoresView _storesView;

    private void OnStoresViewChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) StoresView = Enum.Parse<StoresView>(StoresViewChoice.Selected);
    }

    partial void OnStoresViewChanged(StoresView value)
    {
        StoresViewChoice.Selected = value.ToString();
        Refresh();
    }

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = Brushes.White;
    private IReadOnlyList<OscillationStat> _stats = [];

    /// <summary>Beside the playback buttons in the cloud view: the parcel's height and time.</summary>
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

    private void Refresh()
    {
        switch (Mode)
        {
            case WaterMode.Cloud:
                RefreshCloud();
                break;

            case WaterMode.Stores:
                RefreshStores();
                break;

            default:
                if (FollowDrop) RefreshDrop();
                else RefreshCycle();
                break;
        }
    }

    private void RefreshCycle()
    {
        var process = SelectedProcess;
        var key = process == WaterProcess.None ? "Overview" : process.ToString();
        PhaseTitle = process == WaterProcess.None
            ? _localization.GetString("Water_Process_Overview_Title")
            : _localization.GetString($"Water_Process_{process}");
        PhaseDescription = _localization.GetString($"Water_Process_{key}_Description");
        PhaseBrush = process switch
        {
            WaterProcess.Evaporation or WaterProcess.Transpiration or WaterProcess.Sublimation or WaterProcess.Transport => WhiteBrush,
            WaterProcess.Condensation => CyanBrush,
            WaterProcess.Precipitation => BlueBrush,
            WaterProcess.None => GoldBrush,
            _ => GreenBrush,
        };

        var stats = new List<OscillationStat>();
        if (process != WaterProcess.None)
        {
            stats.Add(new OscillationStat(_localization.GetString("Water_Stat_EachYear"), _localization.GetString($"Water_Process_{process}_Amount")));
        }

        stats.AddRange(WorldStats());
        Stats = stats;
    }

    /// <summary>The landscape's world: its warmth, how much more vapour the air holds, the freezing level and the snow line.</summary>
    private IEnumerable<OscillationStat> WorldStats()
    {
        var change = (WaterCycleModel.VapourFactor(CycleScene.SeaLevelTemperature, Warming) - 1) * 100;
        yield return new OscillationStat(_localization.GetString("Water_Stat_SeaAir"),
            $"{Number(CycleScene.SeaLevelTemperature + Warming, "0.#")} °C · {WarmingText}");
        yield return new OscillationStat(_localization.GetString("Water_Stat_Vapour"), Math.Abs(Warming) < 0.05
            ? _localization.Format("Water_Stat_Vapour_Today", Number(WaterCycleModel.SaturationDensity(CycleScene.SeaLevelTemperature), "0.0"))
            : _localization.Format("Water_Stat_Vapour_Change", (change >= 0 ? "+" : "−") + Number(Math.Abs(change), "0"),
                Number(WaterCycleModel.SaturationDensity(CycleScene.SeaLevelTemperature + Warming), "0.0")));
        yield return new OscillationStat(_localization.GetString("Water_Stat_Freezing"), Metres(CycleScene.FreezingLevel(Warming)));
        yield return new OscillationStat(_localization.GetString("Water_Stat_SnowLine"), Metres(CycleScene.SnowLine(Warming)));
    }

    private void RefreshDrop()
    {
        var index = Math.Clamp(LegIndex, 0, _journey.Count - 1);
        var leg = _journey[index].Leg;
        PhaseTitle = _localization.Format("Water_Drop_Title", _localization.GetString($"Water_Leg_{leg}"));
        PhaseDescription = _localization.GetString($"Water_Leg_{leg}_Description");
        PhaseBrush = leg switch
        {
            DropLeg.Ocean => BlueBrush,
            DropLeg.Evaporation or DropLeg.Transport or DropLeg.Transpiration => WhiteBrush,
            DropLeg.Condensation => CyanBrush,
            DropLeg.Rain or DropLeg.River or DropLeg.Snowmelt => BlueBrush,
            DropLeg.Snowfall or DropLeg.Snowpack => WhiteBrush,
            _ => GreenBrush,
        };

        // The typical time the steps already behind it took, not counting the long stay in the sea it started from.
        var soFar = _journey.Skip(1).Take(Math.Max(0, index - 1)).Sum(s => WaterCycleModel.TypicalDays(s.Leg));
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Water_Stat_Route"), _localization.GetString($"Water_Route_{Route}")),
            new(_localization.GetString("Water_Stat_Step"), _localization.Format("Water_Stat_Step_Value", index + 1, _journey.Count)),
            new(_localization.GetString("Water_Stat_TypicalStay"), _localization.Format("Water_Stat_TypicalStay_Value",
                WaterFormat.Duration(WaterCycleModel.TypicalDays(leg)))),
        };

        if (index > 1)
        {
            stats.Add(new OscillationStat(_localization.GetString("Water_Stat_SoFar"), index == _journey.Count - 1
                ? _localization.Format("Water_Stat_SoFar_Home", WaterFormat.Duration(soFar))
                : _localization.Format("Water_Stat_TypicalStay_Value", WaterFormat.Duration(soFar))));
        }

        stats.Add(new OscillationStat(_localization.GetString("Water_Stat_Freezing"), Metres(CycleScene.FreezingLevel(Warming))));
        Stats = stats;
    }

    private void RefreshCloud()
    {
        var place = CloudScene.AtTime(Lift, Time);
        var state = CloudScene.State(_ascent, _descent, place);
        var arrived = Time >= CloudScene.MoveSeconds - 1e-9;
        var rains = WaterCycleModel.Rains(_ascent);
        var snow = WaterCycleModel.SnowsAtGround(GroundTemperature);

        string key;
        if (place.Descending) key = rains ? arrived ? "Foehn" : "Descending" : arrived ? "Back" : "DescendingBack";
        else if (!state.Saturated) key = arrived ? "NoCloud" : "Rising";
        else if (arrived || state.Height >= _ascent.CloudBase + WaterCycleModel.RainingDepth) key = rains ? snow ? "Snowing" : "Raining" : "ThinCloud";
        else if (!double.IsNaN(_ascent.FreezingLevel) && state.Height >= _ascent.FreezingLevel) key = "Freezing";
        else key = "InCloud";

        PhaseTitle = _localization.GetString($"Water_Cloud_{key}_Title");
        PhaseDescription = _localization.GetString($"Water_Cloud_{key}_Description");
        PhaseBrush = key switch
        {
            "Rising" => GoldBrush,
            "InCloud" => WhiteBrush,
            "Freezing" => CyanBrush,
            "Raining" => BlueBrush,
            "Snowing" => WhiteBrush,
            "Descending" or "Foehn" => OrangeBrush,
            "DescendingBack" or "Back" => GreenBrush,
            _ => VioletBrush,
        };

        ClockText = _localization.Format("Water_Clock", Number(Time, "0.0"), Metres(state.Height));

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Water_Stat_Height"), $"{Metres(state.Height)} · {Number(state.Pressure, "0")} hPa"),
            new(_localization.GetString("Water_Stat_Temperature"), $"{Number(state.Temperature, "0.0")} °C"),
            new(_localization.GetString("Water_Stat_DewPoint"), $"{Number(state.DewPoint, "0.0")} °C · {Number(state.Humidity * 100, "0")}%"),
            new(_localization.GetString("Water_Stat_Swelling"), _localization.Format("Water_Stat_Swelling_Value",
                Number(WaterCycleModel.Swelling(_ascent.Ground, state), "0.00"))),
            new(_localization.GetString("Water_Stat_CloudBase"), _ascent.HasCloud
                ? _localization.Format("Water_Stat_CloudBase_Value", Metres(_ascent.CloudBase),
                    Metres(WaterCycleModel.CloudBaseRuleOfThumb(_ascent.Ground.Temperature, _ascent.Ground.DewPoint)))
                : _localization.Format("Water_Stat_CloudBase_None", Metres(_ascent.Top.Height))),
        };

        if (state.Saturated && !place.Descending)
        {
            stats.Add(new OscillationStat(_localization.GetString("Water_Stat_Condensed"), _localization.Format("Water_Stat_Condensed_Value",
                Number(state.Condensed, "0.0"))));
        }

        if (!double.IsNaN(_ascent.FreezingLevel))
        {
            stats.Add(new OscillationStat(_localization.GetString("Water_Stat_ParcelFreezing"), Metres(_ascent.FreezingLevel)));
        }

        if (Lift == CloudLift.Mountain && _descent.Count > 0)
        {
            var lee = _descent[^1];
            stats.Add(new OscillationStat(_localization.GetString("Water_Stat_Lee"), _localization.Format("Water_Stat_Lee_Value",
                Number(lee.Temperature, "0.0"), Number(lee.Humidity * 100, "0"), Number(lee.Temperature - _ascent.Ground.Temperature, "+0.0;−0.0;0.0"))));
        }

        Stats = stats;
    }

    private void RefreshStores()
    {
        var key = StoresView.ToString();
        PhaseTitle = _localization.GetString($"Water_Stores_{key}_Title");
        PhaseDescription = _localization.GetString($"Water_Stores_{key}_Description");
        PhaseBrush = StoresView == StoresView.Spheres ? BlueBrush : CyanBrush;

        if (StoresView == StoresView.Spheres)
        {
            Stats =
            [
                new(_localization.GetString("Water_Stat_AllWater"), _localization.Format("Water_Stat_Ball_Value",
                    WaterFormat.Volume(WaterCycleModel.TotalWater), Number(WaterCycleModel.SphereDiameter(WaterCycleModel.TotalWater), "N0"))),
                new(_localization.GetString("Water_Stat_Salty"), $"{Number(WaterCycleModel.Share(WaterStore.Oceans) * 100, "0.0")}%"),
                new(_localization.GetString("Water_Stat_Fresh"), _localization.Format("Water_Stat_Fresh_Value",
                    Number(WaterCycleModel.FreshWater / WaterCycleModel.TotalWater * 100, "0.0"),
                    Number(WaterCycleModel.Volume(WaterStore.IceAndSnow) / WaterCycleModel.FreshWater * 100, "0"))),
                new(_localization.GetString("Water_Stat_FreshLiquid"), _localization.Format("Water_Stat_Ball_Value",
                    WaterFormat.Volume(WaterCycleModel.FreshLiquidWater), Number(WaterCycleModel.SphereDiameter(WaterCycleModel.FreshLiquidWater), "N0"))),
                new(_localization.GetString("Water_Stat_LakesRivers"), _localization.Format("Water_Stat_Ball_Value",
                    WaterFormat.Volume(WaterCycleModel.LakesAndRivers), Number(WaterCycleModel.SphereDiameter(WaterCycleModel.LakesAndRivers), "0.0"))),
                new(_localization.GetString("Water_Stat_Earth"), $"{Number(WaterCycleModel.EarthDiameter, "N0")} km"),
            ];
            return;
        }

        Stats =
        [
            new(_localization.GetString("Water_Stat_YearlyCycle"), WaterFormat.Flux(WaterCycleModel.YearlyCycle)),
            new(_localization.GetString("Water_Stat_BackOnSea"), $"{Number(WaterCycleModel.FallsBackOnSea * 100, "0")}%"),
            new(_localization.GetString("Water_Stat_AirStay"), WaterFormat.Duration(WaterCycleModel.AtmosphereDays)),
            new(_localization.GetString("Water_Stat_RiverStay"), WaterFormat.Duration(WaterCycleModel.RiverDays)),
            new(_localization.GetString("Water_Stat_OceanStay"), WaterFormat.Duration(WaterCycleModel.OceanYears * 365.25)),
            new(_localization.GetString("Water_Stat_RainDepth"), _localization.GetString("Water_Stat_RainDepth_Value")),
        ];
    }

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Metres(double metres) => $"{Number(metres, "N0")} m";

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(WarmingText));
        OnPropertyChanged(nameof(HumidityText));
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        ProcessChoice.PropertyChanged -= OnProcessChosen;
        RouteChoice.PropertyChanged -= OnRouteChosen;
        LiftChoice.PropertyChanged -= OnLiftChosen;
        StoresViewChoice.PropertyChanged -= OnStoresViewChosen;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
