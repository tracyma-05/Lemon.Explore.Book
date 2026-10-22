using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>
/// The sea-and-land-breeze page: which view is shown, the coast's sky and time of day, the cell's land and sea
/// temperatures and step, the monsoon map's month and city, and what is happening. The diagram
/// (<see cref="BreezeDiagram"/>) runs the clock and pushes back <see cref="Hour"/>, <see cref="StepTime"/> and
/// <see cref="Month"/>, and <see cref="City"/> when a city is clicked. <see cref="Lesson"/> is the spoken walkthrough
/// of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class SeaBreezeViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9F, 0x5A);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xB0, 0xFF);
    private static readonly IBrush VioletBrush = Frozen(0xC8, 0xB8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x8B, 0xE0, 0x8F);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>The tenth of an hour (coast) or twentieth of a month (monsoon) the card was last worked out for.</summary>
    private long _shownStep = -1;

    private readonly IExploreLocationService _location;

    /// <summary>Where the reader is, for the left page's location chip (the monsoon view starts at the nearest city).</summary>
    public IExploreLocationService Location => _location;

    public SeaBreezeViewModel(IExploreLocationService location, INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => SeaBreezeLessons.For(this));
        Card = new ObjectCard(narration, Lesson, BreezeDiagram.ObjectTitleKey, BreezeDiagram.ObjectClipKey);
        _location = location;
        location.Changed += OnLocationChanged;
        _city = HomeCity ?? MonsoonCity.Mumbai;
        CityChoice.Selected = _city.ToString();
        SkyChoice.PropertyChanged += OnSkyChosen;
        CellTimeChoice.PropertyChanged += OnCellTimeChosen;
        CityChoice.PropertyChanged += OnCityChosen;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
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
    [NotifyPropertyChangedFor(nameof(IsCoast), nameof(IsCell), nameof(IsMonsoon), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private BreezeMode _mode;

    public bool IsCoast
    {
        get => Mode == BreezeMode.Coast;
        set
        {
            if (value) Mode = BreezeMode.Coast;
        }
    }

    public bool IsCell
    {
        get => Mode == BreezeMode.Cell;
        set
        {
            if (value) Mode = BreezeMode.Cell;
        }
    }

    public bool IsMonsoon
    {
        get => Mode == BreezeMode.Monsoon;
        set
        {
            if (value) Mode = BreezeMode.Monsoon;
        }
    }

    public string Subtitle => _localization.GetString($"Breeze_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Breeze_{Mode}_Note");

    public string Hint => _localization.GetString($"Breeze_Hint_{Mode}");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Breeze_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(BreezeMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        _shownStep = -1;
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

    [ObservableProperty]
    private bool _showFlow = true;

    [ObservableProperty]
    private bool _showPressure;

    [ObservableProperty]
    private bool _showLabels = true;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    #endregion

    #region the coast

    /// <summary>Clear or overcast, by <see cref="BreezeSky"/> name.</summary>
    public OscillationChoice SkyChoice { get; } = new(nameof(BreezeSky.Clear));

    [ObservableProperty]
    private BreezeSky _sky;

    /// <summary>The time of day on the coast, hours (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _hour = 14;

    private void OnSkyChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Sky = Enum.Parse<BreezeSky>(SkyChoice.Selected);
    }

    partial void OnSkyChanged(BreezeSky value)
    {
        SkyChoice.Selected = value.ToString();
        _shownStep = -1;
        Refresh();
    }

    partial void OnHourChanged(double value)
    {
        if (Mode != BreezeMode.Coast) return;
        var step = (long)Math.Floor(BreezeModel.Wrap(value) * 10);
        if (step == _shownStep) return;
        _shownStep = step;
        Refresh();
    }

    /// <summary>Jumps to a time of day ("14" or "14.5") and holds it there.</summary>
    [RelayCommand]
    private void JumpToHour(string? hour)
    {
        if (!double.TryParse(hour, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        Hour = value;
        IsPlaying = false;
    }

    #endregion

    #region the cell

    /// <summary>Day or night presets for the cell's temperatures, by <see cref="CellTime"/> name.</summary>
    public OscillationChoice CellTimeChoice { get; } = new(nameof(Chapters.SeaBreeze.CellTime.Day));

    [ObservableProperty]
    private CellTime _cellTime;

    /// <summary>The cell's land air, °C.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LandTemperatureText))]
    private double _landTemperature = 32;

    /// <summary>The cell's sea air, °C.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeaTemperatureText))]
    private double _seaTemperature = 24;

    /// <summary>Seconds into the cell's story (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _stepTime;

    public double CellDuration => BreezeModel.CellDuration;

    public string LandTemperatureText => $"{LandTemperature.ToString("0", CultureInfo.InvariantCulture)} °C";

    public string SeaTemperatureText => $"{SeaTemperature.ToString("0", CultureInfo.InvariantCulture)} °C";

    private void OnCellTimeChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) CellTime = Enum.Parse<CellTime>(CellTimeChoice.Selected);
    }

    partial void OnCellTimeChanged(CellTime value)
    {
        CellTimeChoice.Selected = value.ToString();
        (LandTemperature, SeaTemperature) = value == CellTime.Day ? (32.0, 24.0) : (18.0, 24.0);
        StepTime = 0;
        IsPlaying = true;
    }

    partial void OnLandTemperatureChanged(double value) => Refresh();

    partial void OnSeaTemperatureChanged(double value) => Refresh();

    partial void OnStepTimeChanged(double value)
    {
        if (Mode != BreezeMode.Cell) return;
        var step = (long)BreezeModel.StageAt(value);
        if (step == _shownStep) return;
        _shownStep = step;
        Refresh();
    }

    /// <summary>Back to the first step and play.</summary>
    [RelayCommand]
    private void RestartCell()
    {
        StepTime = 0;
        IsPlaying = true;
    }

    /// <summary>The previous or next step ("-1" or "1"), shown done and held.</summary>
    [RelayCommand]
    private void Step(string? direction)
    {
        var by = direction == "-1" ? -1 : 1;
        var stage = Math.Clamp((int)BreezeModel.StageAt(StepTime) + by, 0, BreezeModel.StageCount - 1);
        StepTime = (stage + 0.7) * BreezeModel.StageSeconds;
        IsPlaying = false;
    }

    #endregion

    #region the monsoon

    /// <summary>The city whose rain is charted, by <see cref="MonsoonCity"/> name.</summary>
    public OscillationChoice CityChoice { get; } = new(nameof(MonsoonCity.Mumbai));

    [ObservableProperty]
    private MonsoonCity _city;

    /// <summary>Months since 1 January (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _month = 6.5;

    /// <summary>The monsoon city at the shared observer location, if it is one of them.</summary>
    private MonsoonCity? HomeCity => BreezeModel.CityNear(_location.Current.Latitude, _location.Current.Longitude);

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (HomeCity is { } city) City = city;
    }

    private void OnCityChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) City = Enum.Parse<MonsoonCity>(CityChoice.Selected);
    }

    partial void OnCityChanged(MonsoonCity value)
    {
        CityChoice.Selected = value.ToString();
        _shownStep = -1;
        Refresh();
    }

    partial void OnMonthChanged(double value)
    {
        if (Mode != BreezeMode.Monsoon) return;
        var step = (long)Math.Floor((((value % 12) + 12) % 12) * 20);
        if (step == _shownStep) return;
        _shownStep = step;
        Refresh();
    }

    /// <summary>Jumps to the middle of a month ("1" for January) and holds it there.</summary>
    [RelayCommand]
    private void JumpToMonth(string? month)
    {
        if (!int.TryParse(month, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return;
        Month = value - 0.5;
        IsPlaying = false;
    }

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = Brushes.White;
    private IReadOnlyList<OscillationStat> _stats = [];

    /// <summary>Beside the timeline: the time of day, the step, or the month.</summary>
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
            case BreezeMode.Cell:
                RefreshCell();
                break;

            case BreezeMode.Monsoon:
                RefreshMonsoon();
                break;

            default:
                RefreshCoast();
                break;
        }
    }

    private void RefreshCoast()
    {
        var hour = BreezeModel.Wrap(Hour);
        var phase = BreezeModel.Phase(hour, Sky);
        PhaseTitle = _localization.GetString($"Breeze_Coast_{phase}_Title");
        PhaseDescription = _localization.GetString($"Breeze_Coast_{phase}_Description");
        PhaseBrush = phase switch
        {
            CoastPhase.LandBreeze => VioletBrush,
            CoastPhase.SeaBreeze or CoastPhase.SeaBreezeBuilding or CoastPhase.SeaBreezeFading => CyanBrush,
            _ => GoldBrush,
        };

        var land = BreezeModel.LandTemperature(hour, Sky);
        var sea = BreezeModel.SeaTemperature(hour, Sky);
        var wind = BreezeModel.ShoreWind(hour, Sky);
        ClockText = _localization.Format("Breeze_Clock", BreezeDiagram.Clock(hour), Number(Math.Abs(wind), "0.0"));

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Breeze_Stat_Time"), BreezeDiagram.Clock(hour)),
            new(_localization.GetString("Breeze_Stat_Land"), $"{Number(land, "0.0")} °C"),
            new(_localization.GetString("Breeze_Stat_Sea"), $"{Number(sea, "0.0")} °C"),
            new(_localization.GetString("Breeze_Stat_Contrast"), $"{Number(land - sea, "+0.0;−0.0;0.0")} °C"),
            new(_localization.GetString("Breeze_Stat_Wind"), Math.Abs(wind) < BreezeModel.Calm
                ? _localization.Format("Breeze_Stat_Wind_Calm", Number(Math.Abs(wind), "0.0"))
                : _localization.Format(wind > 0 ? "Breeze_Stat_Wind_Onshore" : "Breeze_Stat_Wind_Offshore", Number(Math.Abs(wind), "0.0"), BreezeModel.Beaufort(wind))),
        };

        // The pressure difference that drives it, from the contrast a little earlier (the breeze lags it).
        var lagged = BreezeModel.Contrast(hour - BreezeModel.Lag, Sky);
        var surface = BreezeModel.SurfaceDifference(BreezeModel.SeaMean + lagged, BreezeModel.SeaMean);
        stats.Add(new OscillationStat(_localization.GetString("Breeze_Stat_Pressure"), Math.Abs(surface) < 0.05
            ? _localization.GetString("Breeze_Stat_Pressure_Even")
            : _localization.Format(surface < 0 ? "Breeze_Stat_Pressure_LandLow" : "Breeze_Stat_Pressure_LandHigh", Number(Math.Abs(surface), "0.0"))));

        var front = BreezeModel.FrontDistance(hour, Sky);
        var reach = BreezeModel.LandBreezeReach(hour, Sky);
        if (front > 0)
        {
            stats.Add(new OscillationStat(_localization.GetString("Breeze_Stat_Front"), _localization.Format("Breeze_Stat_Front_Value", Number(front, "0"))));
            stats.Add(new OscillationStat(_localization.GetString("Breeze_Stat_Depth"), $"{Number(BreezeModel.SeaBreezeDepth, "N0")} m"));
        }
        else if (reach > 0)
        {
            stats.Add(new OscillationStat(_localization.GetString("Breeze_Stat_Reach"), _localization.Format("Breeze_Stat_Reach_Value", Number(reach, "0"))));
            stats.Add(new OscillationStat(_localization.GetString("Breeze_Stat_Depth"), $"{Number(BreezeModel.LandBreezeDepth, "N0")} m"));
        }

        Stats = stats;
    }

    private void RefreshCell()
    {
        var stage = BreezeModel.StageAt(StepTime);
        var difference = LandTemperature - SeaTemperature;
        var even = Math.Abs(difference) < 0.5;
        var time = difference >= 0 ? "Day" : "Night";
        if (even && stage > CellStage.Even)
        {
            PhaseTitle = _localization.GetString("Breeze_Cell_NoDifference");
            PhaseDescription = _localization.GetString("Breeze_Cell_NoDifference_Description");
            PhaseBrush = GoldBrush;
        }
        else
        {
            PhaseTitle = _localization.GetString($"Breeze_Cell_{stage}_{time}_Title");
            PhaseDescription = _localization.GetString($"Breeze_Cell_{stage}_{time}_Description");
            PhaseBrush = stage switch
            {
                CellStage.Even => GreenBrush,
                CellStage.Heating => difference >= 0 ? OrangeBrush : BlueBrush,
                CellStage.SurfaceWind or CellStage.Circulation => difference >= 0 ? CyanBrush : VioletBrush,
                _ => GoldBrush,
            };
        }

        ClockText = _localization.Format("Breeze_Cell_Clock", (int)stage + 1, BreezeModel.StageCount);

        var seaKelvin = SeaTemperature + 273.15;
        var landKelvin = LandTemperature + 273.15;
        var seaLayer = BreezeModel.HeightOf(900, BreezeModel.SurfacePressure, z => BreezeModel.SeaColumn(SeaTemperature, z));
        var landLayer = BreezeModel.HeightOf(900, BreezeModel.SurfacePressure, z => BreezeModel.LandColumn(LandTemperature, SeaTemperature, z));
        var aloft = BreezeModel.AloftDifference(LandTemperature, SeaTemperature);
        var surface = BreezeModel.SurfaceDifference(LandTemperature, SeaTemperature);
        Stats =
        [
            new(_localization.GetString("Breeze_Stat_Land"), $"{Number(LandTemperature, "0")} °C"),
            new(_localization.GetString("Breeze_Stat_Sea"), $"{Number(SeaTemperature, "0")} °C"),
            new(_localization.GetString("Breeze_Stat_Level900"), _localization.Format("Breeze_Stat_Level900_Value",
                Number(seaLayer, "N0"), Number(landLayer, "N0"), Number(landLayer - seaLayer, "+0;−0;0"))),
            new(_localization.GetString("Breeze_Stat_Expansion"), _localization.Format("Breeze_Stat_Expansion_Value",
                Number((landKelvin / seaKelvin - 1) * 100, "+0.0;−0.0;0.0"))),
            new(_localization.GetString("Breeze_Stat_Aloft"), $"{Number(aloft, "+0.00;−0.00;0.00")} hPa"),
            new(_localization.GetString("Breeze_Stat_Surface"), $"{Number(surface, "+0.00;−0.00;0.00")} hPa"),
        ];
    }

    private void RefreshMonsoon()
    {
        var month = Month;
        var summer = BreezeModel.Summer(month);
        var number = BreezeModel.MonthNumber(month);
        var season = summer >= 0.75 ? "Summer" : summer <= 0.25 ? "Winter" : number is >= 3 and <= 6 ? "Spring" : "Autumn";
        PhaseTitle = _localization.GetString($"Breeze_Monsoon_{season}_Title");
        PhaseDescription = _localization.GetString($"Breeze_Monsoon_{season}_Description");
        PhaseBrush = season switch
        {
            "Summer" => CyanBrush,
            "Winter" => VioletBrush,
            _ => GreenBrush,
        };

        var monthName = BreezeDiagram.MonthName(number);
        ClockText = monthName;
        var city = BreezeModel.City(City);
        var (u, v) = BreezeModel.Wind(city.Latitude, city.Longitude, month);
        var speed = Math.Sqrt((u * u) + (v * v));
        var from = BreezeModel.CompassKeys[BreezeModel.CompassFrom(u, v)];
        var siberian = BreezeModel.Systems.First(s => s.Key == "Siberian");
        var asian = BreezeModel.Systems.First(s => s.Key == "AsianLow");

        Stats =
        [
            new(_localization.GetString("Breeze_Stat_Month"), monthName),
            new(_localization.GetString("Breeze_Stat_City"), _localization.GetString($"Breeze_City_{City}")),
            new(_localization.GetString("Breeze_Stat_CityWind"), _localization.Format("Breeze_Stat_CityWind_Value",
                _localization.GetString($"Breeze_Compass_{from}"), Number(speed, "0"), _localization.GetString(FromSea(city, u, v) ? "Breeze_FromSea" : "Breeze_FromLand"))),
            new(_localization.GetString("Breeze_Stat_Rain"), _localization.Format("Breeze_Stat_Rain_Value",
                Number(BreezeModel.RainIn(City, month), "0"), Number(city.YearRain, "N0"))),
            new(_localization.GetString("Breeze_Stat_Siberian"), $"{Number(BreezeModel.CentralPressure(siberian, month), "0")} hPa"),
            new(_localization.GetString("Breeze_Stat_AsianLow"), $"{Number(BreezeModel.CentralPressure(asian, month), "0")} hPa"),
        ];
    }

    /// <summary>Whether the wind reaching a city has come mostly over the sea in the last thousand kilometres or so.</summary>
    private static bool FromSea(MonsoonCityInfo city, double u, double v)
    {
        var speed = Math.Sqrt((u * u) + (v * v));
        if (speed < 1e-6) return false;
        var mask = WorldMap.LandMask;
        var land = 0.0;
        for (var k = 1; k <= 10; k++)
        {
            var degrees = k * 1.0;
            var latitude = city.Latitude - (v / speed * degrees);
            var longitude = city.Longitude - (u / speed * degrees / Math.Max(0.3, Math.Cos(city.Latitude * Math.PI / 180)));
            land += mask.Sample(latitude, longitude);
        }

        return land < 5;
    }

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SkyChoice.PropertyChanged -= OnSkyChosen;
        CellTimeChoice.PropertyChanged -= OnCellTimeChosen;
        CityChoice.PropertyChanged -= OnCityChosen;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
