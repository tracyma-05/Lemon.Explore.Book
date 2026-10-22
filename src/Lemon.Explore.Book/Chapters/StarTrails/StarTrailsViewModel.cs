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

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>
/// The star-trails page: which view is shown, playback, the latitude (the shared location's, or travelled to), the date,
/// the camera's direction and exposure, and what is happening. The diagram (<see cref="TrailsDiagram"/>) runs
/// <see cref="Exposure"/>, <see cref="SphereTime"/> and <see cref="Day"/> and turns the camera. <see cref="Lesson"/> is the
/// spoken walkthrough of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class StarTrailsViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xA8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x8B, 0xE0, 0x8F);
    private static readonly IBrush VioletBrush = Frozen(0xB3, 0x9D, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while things move.</summary>
    private string _shownKey = string.Empty;

    public StarTrailsViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        _latitude = Math.Round(location.Current.Latitude, 1);
        _day = DateTime.Today.DayOfYear;
        _viewAzimuth = _latitude >= 0 ? 0 : 180;
        Lesson = new LessonPlayer(narration, () => StarTrailsLessons.For(this));
        Card = new ObjectCard(narration, Lesson, TrailsDiagram.ObjectTitleKey, TrailsDiagram.ObjectClipKey);
        location.Changed += OnLocationChanged;
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
    [NotifyPropertyChangedFor(nameof(IsCamera), nameof(IsSphere), nameof(IsYear), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private TrailsMode _mode;

    public bool IsCamera
    {
        get => Mode == TrailsMode.Camera;
        set
        {
            if (value) Mode = TrailsMode.Camera;
        }
    }

    public bool IsSphere
    {
        get => Mode == TrailsMode.Sphere;
        set
        {
            if (value) Mode = TrailsMode.Sphere;
        }
    }

    public bool IsYear
    {
        get => Mode == TrailsMode.Year;
        set
        {
            if (value) Mode = TrailsMode.Year;
        }
    }

    public string Subtitle => _localization.GetString($"Trails_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Trails_{Mode}_Note");

    public string Hint => _localization.GetString($"Trails_Hint_{Mode}");

    /// <summary>The shared observer location, for the location chip on the page.</summary>
    public IExploreLocationService Location => _location;

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Trails_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(TrailsMode value)
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

    [ObservableProperty]
    private bool _showFigures = true;

    [RelayCommand]
    private void TogglePlay()
    {
        // A finished exposure starts again.
        if (!IsPlaying && Mode == TrailsMode.Camera && Exposure >= TrailsDiagram.MaxExposure) Exposure = 0;
        IsPlaying = !IsPlaying;
    }

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    #endregion

    #region where and when

    /// <summary>The observer's latitude, degrees; starts at the shared location's.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LatitudeText))]
    private double _latitude;

    public string LatitudeText => ExplorePlace.LatitudeText(Latitude);

    /// <summary>Under the latitude: whether it is the shared location's, or somewhere travelled to.</summary>
    public string PlaceNote => Math.Abs(Latitude - _location.Current.Latitude) < 0.15
        ? _localization.Format("Trails_Place_Home", _location.Current.Name)
        : _localization.GetString("Trails_Place_Away");

    /// <summary>The day of the year (1 = 1 January).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateText))]
    private double _day;

    public string DateText => TrailsDiagram.DateText(Day);

    partial void OnLatitudeChanged(double value)
    {
        OnPropertyChanged(nameof(PlaceNote));
        Refresh(force: true);
    }

    partial void OnDayChanged(double value)
    {
        if (Mode == TrailsMode.Year) Refresh();
        else Refresh(force: true);
    }

    /// <summary>Back to the shared location's latitude.</summary>
    [RelayCommand]
    private void UseMyLatitude() => Latitude = Math.Round(_location.Current.Latitude, 1);

    /// <summary>Travels to a latitude (degrees, as text).</summary>
    [RelayCommand]
    private void SetLatitude(string? latitude)
    {
        if (double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Latitude = value;
    }

    [RelayCommand]
    private void Today() => Day = DateTime.Today.DayOfYear;

    /// <summary>Jumps to a day of the year (as text) and holds the year there.</summary>
    [RelayCommand]
    private void JumpToDay(string? day)
    {
        if (!double.TryParse(day, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        Day = value;
        if (Mode == TrailsMode.Year) IsPlaying = false;
    }

    #endregion

    #region the camera

    /// <summary>When the shutter opened, local time.</summary>
    public double StartHour => 20;

    /// <summary>How long the shutter has been open, hours (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _exposure;

    public double MaxExposure => TrailsDiagram.MaxExposure;

    [ObservableProperty]
    private double _viewAzimuth;

    [ObservableProperty]
    private double _viewAltitude = 30;

    partial void OnExposureChanged(double value)
    {
        if (Mode == TrailsMode.Camera) Refresh();
    }

    partial void OnViewAzimuthChanged(double value)
    {
        if (Mode == TrailsMode.Camera) Refresh();
    }

    partial void OnViewAltitudeChanged(double value)
    {
        if (Mode == TrailsMode.Camera) Refresh();
    }

    /// <summary>Points the camera: towards the visible pole, east, the equator, west, or straight up.</summary>
    [RelayCommand]
    private void Face(string? facing)
    {
        var pole = Latitude >= 0 ? 0.0 : 180.0;
        (ViewAzimuth, ViewAltitude) = facing switch
        {
            "Pole" => (pole, Math.Clamp(Math.Abs(Latitude), 25, 45)),
            "Equator" => (SkyModel.Wrap360(pole + 180), 30.0),
            "East" => (90.0, 25.0),
            "West" => (270.0, 25.0),
            "Up" => (ViewAzimuth, 90.0),
            _ => (ViewAzimuth, ViewAltitude),
        };
    }

    /// <summary>Opens the shutter again from 20:00.</summary>
    [RelayCommand]
    private void Restart()
    {
        Exposure = 0;
        IsPlaying = true;
    }

    /// <summary>Which way the camera points, for the card: Pole, Equator, East, West or Up.</summary>
    public string Facing
    {
        get
        {
            if (ViewAltitude >= 70) return "Up";
            var pole = Latitude >= 0 ? 0.0 : 180.0;
            var off = SkyModel.Wrap360(ViewAzimuth - pole);
            return off switch
            {
                < 45 or >= 315 => "Pole",
                >= 135 and < 225 => "Equator",
                _ => SkyModel.Wrap360(ViewAzimuth) < 180 ? "East" : "West",
            };
        }
    }

    #endregion

    #region the sphere

    /// <summary>The sidereal time on the sphere, hours (from the diagram).</summary>
    [ObservableProperty]
    private double _sphereTime;

    partial void OnSphereTimeChanged(double value)
    {
        if (Mode == TrailsMode.Sphere) Refresh();
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
        // The camera's card follows every 5 minutes of exposure and the way it faces, the sphere's every sidereal
        // quarter hour, the year's every day.
        var key = Mode switch
        {
            TrailsMode.Camera => $"C|{Math.Floor(Exposure * 12)}|{Facing}",
            TrailsMode.Sphere => $"S|{Math.Floor(SphereTime * 4)}",
            _ => $"Y|{Math.Floor(Day)}",
        };
        if (!force && key == _shownKey) return;
        _shownKey = key;

        switch (Mode)
        {
            case TrailsMode.Sphere:
                RefreshSphere();
                break;

            case TrailsMode.Year:
                RefreshYear();
                break;

            default:
                RefreshCamera();
                break;
        }
    }

    private void RefreshCamera()
    {
        var facing = Facing;
        PhaseTitle = _localization.GetString($"Trails_Camera_{facing}_Title");
        PhaseDescription = _localization.GetString($"Trails_Camera_{facing}_Description");
        PhaseBrush = facing switch
        {
            "Pole" => CyanBrush,
            "Up" => VioletBrush,
            "Equator" => GoldBrush,
            _ => GreenBrush,
        };

        var minutes = (int)Math.Round(Exposure * 60);
        var end = (StartHour + Exposure) % 24;
        ClockText = _localization.Format("Trails_Camera_Clock", Clock(StartHour), Clock(end), minutes / 60, (minutes % 60).ToString("00"));
        Stats =
        [
            new(_localization.GetString("Trails_Stat_Latitude"), LatitudeText),
            new(_localization.GetString("Trails_Stat_Pole"), _localization.Format(Latitude >= 0 ? "Trails_Stat_Pole_North" : "Trails_Stat_Pole_South", Number(Math.Abs(Latitude), "0.#"))),
            new(_localization.GetString("Trails_Stat_Exposure"), _localization.Format("Trails_Stat_Exposure_Value", minutes / 60, (minutes % 60).ToString("00"), Number(Exposure * SkyModel.DegreesPerHour, "0"))),
            new(_localization.GetString("Trails_Stat_Opened"), _localization.Format("Trails_Stat_Opened_Value", Clock(StartHour), DateText)),
            new(_localization.GetString("Trails_Stat_NeverSet"), Math.Abs(Latitude) < 0.5
                ? _localization.GetString("Trails_Stat_NeverSet_None")
                : _localization.Format("Trails_Stat_NeverSet_Value", Number(Math.Abs(Latitude), "0"))),
            new(_localization.GetString("Trails_Stat_Visible"), _localization.Format("Trails_Stat_Visible_Value", Number(SkyModel.VisibleShare(Latitude) * 100, "0"))),
        ];
    }

    private void RefreshSphere()
    {
        var band = Math.Abs(Latitude) >= 80 ? "Pole" : Math.Abs(Latitude) <= 8 ? "Equator" : "Middle";
        PhaseTitle = _localization.GetString($"Trails_Sphere_{band}_Title");
        PhaseDescription = _localization.GetString($"Trails_Sphere_{band}_Description");
        PhaseBrush = band switch
        {
            "Pole" => BlueBrush,
            "Equator" => GreenBrush,
            _ => GoldBrush,
        };
        ClockText = _localization.Format("Trails_Sphere_Clock", Clock(SphereTime));

        var hours = string.Join(" · ", new[] { "Vega", "Sirius", "Canopus" }.Select(id =>
        {
            var star = StarCatalog.Get(id);
            var up = SkyModel.HoursAbove(star.Dec, Latitude) * SkyModel.SiderealRate;
            return $"{_localization.GetString(TrailsDiagram.ObjectTitleKey(TrailsDiagram.StarId(id))!)} {Number(up, "0")} h";
        }));
        Stats =
        [
            new(_localization.GetString("Trails_Stat_Latitude"), LatitudeText),
            new(_localization.GetString("Trails_Stat_Pole"), _localization.Format(Latitude >= 0 ? "Trails_Stat_Pole_North" : "Trails_Stat_Pole_South", Number(Math.Abs(Latitude), "0.#"))),
            new(_localization.GetString("Trails_Stat_NeverSet"), Math.Abs(Latitude) < 0.5
                ? _localization.GetString("Trails_Stat_NeverSet_None")
                : _localization.Format("Trails_Stat_NeverSet_Value", Number(Math.Abs(Latitude), "0"))),
            new(_localization.GetString("Trails_Stat_NeverRise"), Math.Abs(Latitude) < 0.5
                ? _localization.GetString("Trails_Stat_NeverSet_None")
                : _localization.Format("Trails_Stat_NeverRise_Value", Number(Math.Abs(Latitude), "0"))),
            new(_localization.GetString("Trails_Stat_Visible"), _localization.Format("Trails_Stat_Visible_Value", Number(SkyModel.VisibleShare(Latitude) * 100, "0"))),
            new(_localization.GetString("Trails_Stat_HoursUp"), hours),
        ];
    }

    private void RefreshYear()
    {
        var midnight = SkyModel.SiderealTime(Day, 0);
        var facing = TrailsDiagram.RingFigures.MinBy(id =>
        {
            var (ra, _) = StarCatalog.Center(StarCatalog.Figures.First(f => f.Id == id));
            return Math.Abs(SkyModel.Wrap24(ra - midnight + 12) - 12);
        })!;
        var name = _localization.GetString(TrailsDiagram.ObjectTitleKey(TrailsDiagram.FigureId(facing))!);
        PhaseTitle = _localization.Format("Trails_Year_Title", DateText, name);
        PhaseDescription = _localization.GetString("Trails_Year_Description");
        PhaseBrush = CyanBrush;
        ClockText = DateText;

        var (orionRa, _) = StarCatalog.Center(StarCatalog.Figures.First(f => f.Id == "Orion"));
        var orion = SkyModel.TransitTime(orionRa, Day);
        var (sunset, sunrise) = TransitChart.SunsetSunrise(Day, Latitude);
        var inDark = orion >= sunset || orion + 24 <= sunrise;
        var orionText = inDark ? Clock(orion) : _localization.Format("Trails_Stat_InDaylight", Clock(orion));
        Stats =
        [
            new(_localization.GetString("Trails_Stat_Date"), DateText),
            new(_localization.GetString("Trails_Stat_Midnight"), name),
            new(_localization.GetString("Trails_Stat_OrionHighest"), orionText),
            new(_localization.GetString("Trails_Stat_Earlier"), _localization.GetString("Trails_Stat_Earlier_Value")),
            new(_localization.GetString("Trails_Stat_SiderealDay"), _localization.GetString("Trails_Stat_SiderealDay_Value")),
        ];
    }

    /// <summary>Hours as a 24-hour clock, e.g. 21:30.</summary>
    private static string Clock(double hours)
    {
        var minutes = (int)Math.Round(SkyModel.Wrap24(hours) * 60) % (24 * 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    #endregion

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        UseMyLatitude();
        OnPropertyChanged(nameof(PlaceNote));
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
        OnPropertyChanged(nameof(LatitudeText));
        OnPropertyChanged(nameof(PlaceNote));
        OnPropertyChanged(nameof(DateText));
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
