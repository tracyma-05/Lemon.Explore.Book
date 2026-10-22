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

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>
/// The typhoon chapter: which view is shown, playback, the storm's depth and options, the sea's warmth and landfall, the
/// track's month, subtropical high and start, and what is happening. The diagram (<see cref="TyphoonDiagram"/>) moves
/// the probe and turns <see cref="LandfallHours"/> and <see cref="TrackHour"/>. <see cref="Lesson"/> is the spoken
/// walkthrough of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class TyphoonViewModel : ObservableObject, IChapterContent, IDisposable
{
    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    /// <summary>Where the reader is, for the left page's location chip.</summary>
    public IExploreLocationService Location => _location;

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while the clocks run.</summary>
    private string _shownKey = string.Empty;

    public TyphoonViewModel(IExploreLocationService location, INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => TyphoonLessons.For(this));
        Card = new ObjectCard(narration, Lesson, TyphoonDiagram.ObjectTitleKey, TyphoonDiagram.ObjectClipKey);
        _location = location;
        location.Changed += OnLocationChanged;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        MonthChoice.PropertyChanged += OnMonthChosen;
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
    [NotifyPropertyChangedFor(nameof(IsStorm), nameof(IsEngine), nameof(IsTrack), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private TyphoonMode _mode;

    public bool IsStorm
    {
        get => Mode == TyphoonMode.Storm;
        set
        {
            if (value) Mode = TyphoonMode.Storm;
        }
    }

    public bool IsEngine
    {
        get => Mode == TyphoonMode.Engine;
        set
        {
            if (value) Mode = TyphoonMode.Engine;
        }
    }

    public bool IsTrack
    {
        get => Mode == TyphoonMode.Track;
        set
        {
            if (value) Mode = TyphoonMode.Track;
        }
    }

    public string Subtitle => _localization.GetString($"Typhoon_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Typhoon_{Mode}_Note");

    public string Hint => _localization.GetString($"Typhoon_Hint_{Mode}");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Typhoon_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(TyphoonMode value)
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

    #region the storm

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CentralPressureText))]
    private double _centralPressure = 950;

    public double MinPressure => 900;

    public double MaxPressure => 1004;

    public string CentralPressureText => $"{Number(CentralPressure, "0")} hPa";

    [ObservableProperty]
    private bool _southern;

    [ObservableProperty]
    private bool _spin = true;

    [ObservableProperty]
    private bool _moving;

    [ObservableProperty]
    private bool _showIsobars = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProbeDistance))]
    private double _probeX = 160;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProbeDistance))]
    private double _probeY;

    public double ProbeDistance => Math.Sqrt((ProbeX * ProbeX) + (ProbeY * ProbeY));

    partial void OnCentralPressureChanged(double value) => Refresh(force: true);

    partial void OnSouthernChanged(bool value) => Refresh(force: true);

    partial void OnSpinChanged(bool value) => Refresh(force: true);

    partial void OnMovingChanged(bool value) => Refresh(force: true);

    partial void OnProbeXChanged(double value) => Refresh();

    partial void OnProbeYChanged(double value) => Refresh();

    /// <summary>Sets the central pressure, hPa (the grade chips).</summary>
    [RelayCommand]
    private void SetPressure(string? hPa)
    {
        if (double.TryParse(hPa, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) CentralPressure = value;
    }

    #endregion

    #region the heat engine

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeaTemperatureText))]
    private double _seaTemperature = 29.5;

    public string SeaTemperatureText => $"{Number(SeaTemperature, "0.0")} °C";

    [ObservableProperty]
    private bool _landfall;

    /// <summary>Hours since landfall, 0…48 (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _landfallHours;

    public double LandfallSpan => TyphoonDiagram.LandfallSpan;

    partial void OnSeaTemperatureChanged(double value) => Refresh(force: true);

    partial void OnLandfallChanged(bool value)
    {
        LandfallHours = 0;
        IsPlaying = true;
        Refresh(force: true);
    }

    partial void OnLandfallHoursChanged(double value)
    {
        if (Mode == TyphoonMode.Engine) Refresh();
    }

    [RelayCommand]
    private void SetSea(string? celsius)
    {
        if (double.TryParse(celsius, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) SeaTemperature = value;
    }

    #endregion

    #region the track

    /// <summary>The month chips, 6…10.</summary>
    public OscillationChoice MonthChoice { get; } = new("8");

    [ObservableProperty]
    private int _month = 8;

    /// <summary>The subtropical high's west end, °E.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RidgeWestText))]
    private double _ridgeWest = 125;

    public string RidgeWestText => _localization.Format("Typhoon_Lon_E", Number(RidgeWest, "0"));

    [ObservableProperty]
    private double _startLatitude = 13.5;

    [ObservableProperty]
    private double _startLongitude = 144;

    /// <summary>Hours since the storm formed (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrackLength))]
    private double _trackHour;

    public double TrackLength => Track[^1].Hour;

    private TrackPoint[] Track => TrackModel.Track(new TrackOptions(StartLatitude, StartLongitude, RidgeWest, Month));

    public double PlaceLatitude => _location.Current.Latitude;

    public double PlaceLongitude => _location.Current.Longitude;

    public string PlaceName => _location.Current.Name;

    private void OnMonthChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Month = int.Parse(MonthChoice.Selected, CultureInfo.InvariantCulture);
    }

    partial void OnMonthChanged(int value)
    {
        MonthChoice.Selected = value.ToString(CultureInfo.InvariantCulture);
        OnTrackChanged();
    }

    partial void OnRidgeWestChanged(double value) => OnTrackChanged();

    partial void OnStartLatitudeChanged(double value) => OnTrackChanged();

    partial void OnStartLongitudeChanged(double value) => OnTrackChanged();

    partial void OnTrackHourChanged(double value)
    {
        if (value > TrackLength)
        {
            TrackHour = TrackLength;
            return;
        }

        if (Mode == TyphoonMode.Track) Refresh();
    }

    private void OnTrackChanged()
    {
        OnPropertyChanged(nameof(TrackLength));
        Refresh(force: true);
    }

    /// <summary>Starts the storm at one of the usual places (by <see cref="TrackModel.Starts"/> name).</summary>
    [RelayCommand]
    private void UseStart(string? name)
    {
        if (name is null || !TrackModel.Starts.TryGetValue(name, out var start)) return;
        (StartLatitude, StartLongitude) = start;
        IsPlaying = true;
    }

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = Brushes.White;
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
        // The probe's card follows it every 5 km, the landfall's every half hour, the track's every hour.
        var key = Mode switch
        {
            TyphoonMode.Storm => $"S|{Math.Round(ProbeX / 5)}|{Math.Round(ProbeY / 5)}",
            TyphoonMode.Engine => $"E|{Math.Floor(LandfallHours * 2)}",
            _ => $"T|{Math.Floor(TrackHour)}",
        };
        if (!force && key == _shownKey) return;
        _shownKey = key;

        switch (Mode)
        {
            case TyphoonMode.Engine:
                RefreshEngine();
                break;

            case TyphoonMode.Track:
                RefreshTrack();
                break;

            default:
                RefreshStorm();
                break;
        }
    }

    private string WindText(double speed) => _localization.Format("Typhoon_Stat_Wind_Value", Number(speed, "0"), Number(speed * 3.6, "0"),
        TyphoonDiagram.ForceText(StormModel.Beaufort(speed)));

    private void RefreshStorm()
    {
        var wind = StormModel.MaxWind(CentralPressure);
        var grade = StormModel.Grade(wind);
        var rmax = StormModel.RadiusOfMaxWind(CentralPressure);
        PhaseTitle = Spin ? TyphoonDiagram.GradeName(grade) : _localization.GetString("Typhoon_Storm_NoSpin_Title");
        PhaseDescription = Spin
            ? _localization.GetString($"Typhoon_Grade_{grade}_Description")
            : _localization.GetString("Typhoon_Storm_NoSpin_Description");
        PhaseBrush = Frozen(TyphoonDiagram.GradeColor(grade));
        ClockText = _localization.Format("Typhoon_Storm_Clock", TyphoonDiagram.GradeName(grade), Number(wind, "0"), Number(CentralPressure, "0"));

        var probe = ProbeDistance;
        var probeWind = StormModel.Wind(probe, CentralPressure);
        Stats =
        [
            new(_localization.GetString("Typhoon_Stat_Pressure"), CentralPressureText),
            new(_localization.GetString("Typhoon_Stat_MaxWind"), WindText(wind)),
            new(_localization.GetString("Typhoon_Stat_Rmax"), _localization.Format("Typhoon_Stat_Km_Value", Number(rmax, "0"))),
            new(_localization.GetString("Typhoon_Stat_Eye"), wind >= 30
                ? _localization.Format("Typhoon_Stat_Km_Value", Number(2 * StormModel.EyeRadius(CentralPressure), "0"))
                : _localization.GetString("Typhoon_Stat_Eye_None")),
            new(_localization.GetString("Typhoon_Stat_Turn"), _localization.GetString(!Spin ? "Typhoon_Stat_Turn_None" : Southern ? "Typhoon_Stat_Turn_Clockwise" : "Typhoon_Stat_Turn_Anticlockwise")),
            new(_localization.GetString("Typhoon_Stat_Probe"), _localization.Format("Typhoon_Stat_Probe_Value", Number(probe, "0"),
                Number(probeWind, "0"), TyphoonDiagram.ForceText(StormModel.Beaufort(probeWind)), Number(StormModel.Pressure(probe, CentralPressure), "0"))),
        ];
    }

    private void RefreshEngine()
    {
        var ceiling = StormModel.PotentialIntensity(SeaTemperature);
        var wind = TyphoonDiagram.EngineWind(SeaTemperature, Landfall, LandfallHours);
        var key = wind <= 0 ? "TooCool" : Landfall ? "Landfall" : "Running";
        PhaseTitle = _localization.GetString($"Typhoon_Engine_{key}_Title");
        PhaseDescription = _localization.GetString($"Typhoon_Engine_{key}_Description");
        PhaseBrush = Frozen(wind <= 0 ? Color.FromRgb(0x6F, 0xD8, 0xFF) : TyphoonDiagram.GradeColor(StormModel.Grade(wind)));
        ClockText = Landfall && wind > 0
            ? _localization.Format("Typhoon_Engine_ClockLand", Number(LandfallHours, "0"), Number(wind, "0"))
            : _localization.Format("Typhoon_Engine_Clock", Number(SeaTemperature, "0.0"));

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Typhoon_Stat_Sea"), SeaTemperatureText),
            new(_localization.GetString("Typhoon_Stat_Ceiling"), ceiling > 0
                ? _localization.Format("Typhoon_Stat_Ceiling_Value", Number(ceiling, "0"), TyphoonDiagram.GradeName(StormModel.Grade(ceiling)))
                : _localization.GetString("Typhoon_Stat_Ceiling_None")),
        };
        if (wind > 0)
        {
            stats.Add(new(_localization.GetString("Typhoon_Stat_MaxWind"), WindText(wind)));
            stats.Add(new(_localization.GetString("Typhoon_Stat_Grade"), TyphoonDiagram.GradeName(StormModel.Grade(wind))));
            stats.Add(new(_localization.GetString("Typhoon_Stat_Pressure"), $"{Number(StormModel.CentralPressure(wind), "0")} hPa"));
            stats.Add(new(_localization.GetString("Typhoon_Stat_Tops"), _localization.Format("Typhoon_Stat_Km_Value", Number(8 + (7 * StormModel.Smooth(12, 70, wind)), "0"))));
            if (Landfall) stats.Add(new(_localization.GetString("Typhoon_Stat_Ashore"), _localization.Format("Typhoon_Stat_Ashore_Value", Number(LandfallHours, "0"), Number(LandfallHours * 20, "0"))));
        }

        Stats = stats;
    }

    private void RefreshTrack()
    {
        var track = Track;
        var now = TrackModel.At(track, TrackHour);
        var grade = StormModel.Grade(now.Wind);
        var ended = TrackHour >= track[^1].Hour - 0.01;
        var falls = TrackModel.Landfalls(track);
        var growing = TrackHour < track[^1].Hour - 1 && TrackModel.At(track, TrackHour + 1).Wind > now.Wind + 0.05;
        var key = ended ? "Over" : now.OverLand ? "Land" : growing ? "Growing" : "Sea";
        PhaseTitle = _localization.GetString($"Typhoon_Track_{key}_Title");
        PhaseDescription = _localization.GetString($"Typhoon_Track_{key}_Description");
        PhaseBrush = Frozen(TyphoonDiagram.GradeColor(grade));
        var day = (int)Math.Floor(TrackHour / 24) + 1;
        var hour = (int)Math.Floor(TrackHour % 24);
        ClockText = _localization.Format("Typhoon_Track_Clock", day, hour.ToString("00", CultureInfo.InvariantCulture));

        var (index, distance, wind) = TrackModel.ClosestPass(track, PlaceLatitude, PlaceLongitude);
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Typhoon_Stat_Where"), _localization.Format("Typhoon_Stat_Where_Value", Number(now.Latitude, "0.0"), Number(now.Longitude, "0.0"))),
            new(_localization.GetString("Typhoon_Stat_Grade"), grade == StormGrade.None ? _localization.GetString("Typhoon_Track_Dissipated") : TyphoonDiagram.GradeName(grade)),
            new(_localization.GetString("Typhoon_Stat_MaxWind"), WindText(now.Wind)),
            new(_localization.GetString("Typhoon_Stat_Pressure"), $"{Number(StormModel.CentralPressure(now.Wind), "0")} hPa"),
            new(_localization.GetString("Typhoon_Stat_Sea"), now.OverLand ? _localization.GetString("Typhoon_Stat_Sea_Land") : $"{Number(TrackModel.SeaTemperature(now.Latitude, Month), "0.0")} °C"),
            new(_localization.GetString("Typhoon_Stat_Landfalls"), falls.Count == 0
                ? _localization.GetString("Typhoon_Stat_Landfalls_None")
                : string.Join(_localization.GetString("Typhoon_List_Separator"), falls.Select(f => _localization.Format("Typhoon_Stat_Landfall_Item",
                    _localization.GetString($"Typhoon_City_{f.City.Id}"), TyphoonDiagram.GradeName(StormModel.Grade(track[f.Index].Wind)))))),
            new(_localization.GetString("Typhoon_Stat_Pass"), distance > 1500
                ? _localization.Format("Typhoon_Stat_Pass_Far", PlaceName)
                : _localization.Format("Typhoon_Stat_Pass_Value", PlaceName, Number(distance, "0"), TyphoonDiagram.ForceText(StormModel.Beaufort(wind)),
                    Number(track[index].Hour / 24, "0.0"))),
        };
        Stats = stats;
    }

    #endregion

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PlaceLatitude));
        OnPropertyChanged(nameof(PlaceLongitude));
        OnPropertyChanged(nameof(PlaceName));
        Refresh(force: true);
    }

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(Color color) => new ImmutableSolidColorBrush(color);

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(RidgeWestText));
        OnPropertyChanged(nameof(PlaceName));
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        MonthChoice.PropertyChanged -= OnMonthChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
