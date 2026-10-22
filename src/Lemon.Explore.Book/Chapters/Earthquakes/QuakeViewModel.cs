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

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>
/// The earthquake chapter: which view is shown; the map's plates, layers and what is picked on it; the boundary section, its
/// clock and settings; the quake and the station inside the Earth; and what is happening. The diagram
/// (<see cref="QuakeDiagram"/>) runs the clocks and pushes them back. <see cref="Lesson"/> is the spoken walkthrough of the
/// current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class QuakeViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);
    private static readonly IBrush VioletBrush = Frozen(0xC9, 0x8C, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    public IExploreLocationService Location => _location;

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while a clock runs.</summary>
    private string _shownKey = string.Empty;

    public QuakeViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        Lesson = new LessonPlayer(narration, () => QuakeLessons.For(this));
        Card = new ObjectCard(narration, Lesson, QuakeDiagram.ObjectTitleKey, QuakeDiagram.ObjectClipKey);
        location.Changed += OnLocationChanged;
        SectionChoice.PropertyChanged += OnSectionChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        Refresh(force: true);
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
    [NotifyPropertyChangedFor(nameof(IsPlates), nameof(IsBoundaries), nameof(IsInside), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private QuakeMode _mode;

    public bool IsPlates
    {
        get => Mode == QuakeMode.Plates;
        set
        {
            if (value) Mode = QuakeMode.Plates;
        }
    }

    public bool IsBoundaries
    {
        get => Mode == QuakeMode.Boundaries;
        set
        {
            if (value) Mode = QuakeMode.Boundaries;
        }
    }

    public bool IsInside
    {
        get => Mode == QuakeMode.Inside;
        set
        {
            if (value) Mode = QuakeMode.Inside;
        }
    }

    /// <summary>The view's (or the section's) key part: <c>Plates</c>, <c>Ridge</c>, <c>Inside</c> …</summary>
    private string ViewKey => Mode == QuakeMode.Boundaries ? Section.ToString() : Mode.ToString();

    public string Subtitle => _localization.GetString($"Quake_{ViewKey}_Subtitle");

    public string ScaleNote => _localization.GetString($"Quake_{ViewKey}_Note");

    public string Hint => _localization.GetString($"Quake_Hint_{ViewKey}");

    public string ChartCaption => _localization.GetString($"Quake_Chart_{ViewKey}_Caption");

    public string ChartNote => _localization.GetString($"Quake_Chart_{ViewKey}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Quake_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(QuakeMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        if (value == QuakeMode.Inside && WaveTime >= QuakeDiagram.WaveDuration) WaveTime = 0;
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

    [ObservableProperty]
    private bool _isPlaying = true;

    [ObservableProperty]
    private bool _showLabels = true;

    [RelayCommand]
    private void TogglePlay()
    {
        // At the end of a run, play starts it again.
        if (!IsPlaying)
        {
            if (Mode == QuakeMode.Inside && WaveTime >= QuakeDiagram.WaveDuration) WaveTime = 0;
            if (Mode == QuakeMode.Boundaries && Section is SectionKind.Ridge or SectionKind.Collision && SectionTime <= 0) SectionTime = SectionStart(Section);
        }

        IsPlaying = !IsPlaying;
    }

    #endregion

    #region the map

    /// <summary>The textbook's six plates instead of all fifteen.</summary>
    [ObservableProperty]
    private bool _sixPlates;

    [ObservableProperty]
    private bool _showQuakes = true;

    [ObservableProperty]
    private bool _showVolcanoes = true;

    [ObservableProperty]
    private bool _showArrows = true;

    /// <summary>The boundary picked on the map (index into <see cref="PlateModel.Boundaries"/>), −1 for none.</summary>
    [ObservableProperty]
    private int _selectedBoundary = -1;

    /// <summary>The plate picked on the map, or null.</summary>
    [ObservableProperty]
    private string? _selectedPlate;

    /// <summary>The famous quake picked, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FamousMagnitude), nameof(HasFamous))]
    private string? _famous;

    public double FamousMagnitude => FamousQuake?.Magnitude ?? 0;

    public bool HasFamous => FamousQuake is not null;

    private FamousQuake? FamousQuake => PlateModel.Famous.FirstOrDefault(q => q.Id == Famous);

    public double PlaceLatitude => _location.Current.Latitude;

    public double PlaceLongitude => _location.Current.Longitude;

    public string PlaceName => _location.Current.Name;

    partial void OnSixPlatesChanged(bool value) => Refresh(force: true);

    partial void OnSelectedBoundaryChanged(int value)
    {
        if (value >= 0) SelectedPlate = null;
        Refresh(force: true);
    }

    partial void OnSelectedPlateChanged(string? value)
    {
        if (value is not null) SelectedBoundary = -1;
        Refresh(force: true);
    }

    partial void OnFamousChanged(string? value) => Refresh(force: true);

    /// <summary>Picks a famous quake (from a chip) and explains it.</summary>
    [RelayCommand]
    private void PickFamous(string? id)
    {
        if (PlateModel.Famous.FirstOrDefault(q => q.Id == id) is not { } quake) return;
        Famous = quake.Id;
        Card.Id = null;
        Card.Id = QuakeDiagram.FamousTargetId(quake.Id);
    }

    /// <summary>Clears what is picked on the map.</summary>
    [RelayCommand]
    private void ClearPicks()
    {
        Famous = null;
        SelectedBoundary = -1;
        SelectedPlate = null;
        Card.Id = null;
    }

    #endregion

    #region the boundaries

    public OscillationChoice SectionChoice { get; } = new(nameof(SectionKind.Ridge));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(ChartCaption), nameof(ChartNote), nameof(IsRidge), nameof(IsSubduction), nameof(IsCollision), nameof(IsTransform), nameof(HasTimeline), nameof(CanQuake))]
    private SectionKind _section;

    public bool IsRidge => Section == SectionKind.Ridge;

    public bool IsSubduction => Section == SectionKind.Subduction;

    public bool IsCollision => Section == SectionKind.Collision;

    public bool IsTransform => Section == SectionKind.Transform;

    /// <summary>The ridge and the collision run through a stretch of time the slider shows.</summary>
    public bool HasTimeline => Section is SectionKind.Ridge or SectionKind.Collision;

    /// <summary>The sinking plate and the fault can be made to break now.</summary>
    public bool CanQuake => Section is SectionKind.Subduction or SectionKind.Transform;

    /// <summary>The section's clock (see <see cref="QuakeDiagram.SectionTime"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeFraction), nameof(TimeText))]
    private double _sectionTime = SectionStart(SectionKind.Ridge);

    /// <summary>How fast each side of the ridge moves away, cm a year.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HalfRateText))]
    private double _halfRate = 2.5;

    /// <summary>How steeply the sinking plate goes down, °.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DipText))]
    private double _dip = 40;

    /// <summary>The upper plate is ocean (an island arc) rather than a continent.</summary>
    [ObservableProperty]
    private bool _overOcean;

    /// <summary>The fault creeps instead of sticking.</summary>
    [ObservableProperty]
    private bool _creep;

    /// <summary>Changed to set off the section's quake now.</summary>
    [ObservableProperty]
    private int _quakeRequest;

    /// <summary>Quakes so far in the section (the diagram pushes it).</summary>
    [ObservableProperty]
    private int _quakeCount;

    /// <summary>The last quake's slip, m (the diagram pushes it).</summary>
    [ObservableProperty]
    private double _lastSlip;

    /// <summary>The section's timeline as 0 (its start) … 1 (today), for the slider.</summary>
    public double TimeFraction
    {
        get => HasTimeline ? 1 - (SectionTime / SectionStart(Section)) : 0;
        set
        {
            if (!HasTimeline) return;
            SectionTime = Math.Round((1 - Math.Clamp(value, 0, 1)) * SectionStart(Section), 2);
            IsPlaying = false;
        }
    }

    public string TimeText => Section switch
    {
        SectionKind.Ridge or SectionKind.Collision => SectionTime <= 0.005
            ? _localization.GetString("Quake_Value_Today")
            : _localization.Format("Quake_Value_MaAgo", Number(SectionTime, SectionTime < 10 ? "0.0" : "0")),
        _ => _localization.Format("Quake_Value_Years", Number(SectionTime, "0")),
    };

    public string HalfRateText => _localization.Format("Quake_Value_CmYear", Number(HalfRate, "0.0"));

    public string DipText => _localization.Format("Quake_Value_Degrees", Number(Dip, "0"));

    /// <summary>Where a section's clock starts: 12 Ma ago at the ridge, 80 for India, year 0 otherwise.</summary>
    internal static double SectionStart(SectionKind kind) => kind switch
    {
        SectionKind.Ridge => 12,
        SectionKind.Collision => 80,
        _ => 0,
    };

    partial void OnSectionChanged(SectionKind value)
    {
        SectionChoice.Selected = value.ToString();
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        RestartSection();
    }

    partial void OnSectionTimeChanged(double value) => Refresh();

    partial void OnHalfRateChanged(double value) => Refresh(force: true);

    partial void OnDipChanged(double value) => Refresh(force: true);

    partial void OnOverOceanChanged(bool value) => Refresh(force: true);

    partial void OnCreepChanged(bool value)
    {
        RestartSection();
        Refresh(force: true);
    }

    partial void OnQuakeCountChanged(int value) => Refresh(force: true);

    partial void OnLastSlipChanged(double value) => Refresh(force: true);

    private void OnSectionChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<SectionKind>(SectionChoice.Selected, out var kind)) Section = kind;
    }

    /// <summary>Starts the section's clock over (and its fences, for the fault).</summary>
    [RelayCommand]
    private void RestartSection()
    {
        SectionTime = SectionStart(Section);
        QuakeCount = 0;
        LastSlip = 0;
        IsPlaying = true;
        Refresh(force: true);
    }

    [RelayCommand]
    private void QuakeNow() => QuakeRequest++;

    /// <summary>Sets the ridge's spreading (from a chip): slow like the Atlantic, fast like the East Pacific Rise.</summary>
    [RelayCommand]
    private void SetHalfRate(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)) HalfRate = rate;
    }

    #endregion

    #region inside the Earth

    /// <summary>Where the quake is on the rim, ° clockwise from the top.</summary>
    [ObservableProperty]
    private double _quakeAngle;

    /// <summary>How far round from the quake the station is, °.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StationText))]
    private double _stationDistance = 60;

    /// <summary>The station's name when it is the viewer's place; null for a point on the rim.</summary>
    [ObservableProperty]
    private string? _stationName;

    /// <summary>Seconds since the quake (the diagram pushes it).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaveTimeText))]
    private double _waveTime;

    [ObservableProperty]
    private bool _showRays = true;

    /// <summary>The quake's magnitude: how hard the seismogram swings and how much energy it sends out.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MagnitudeText))]
    private double _magnitude = 7;

    public string StationText => _localization.Format("Quake_Value_DegreesKm", Number(StationDistance, "0"), Number(Math.Round(StationDistance * PlateModel.KmPerDegree / 10) * 10, "#,0"));

    public string WaveTimeText => _localization.Format("Quake_Value_MinSec", Number(Math.Floor(WaveTime / 60), "0"), Number(Math.Floor(WaveTime % 60), "00"));

    public string MagnitudeText => Number(Magnitude, "0.0");

    partial void OnQuakeAngleChanged(double value) => Refresh();

    partial void OnStationDistanceChanged(double value) => Refresh(force: true);

    partial void OnWaveTimeChanged(double value) => Refresh();

    partial void OnMagnitudeChanged(double value) => Refresh(force: true);

    /// <summary>Sets the quake off again from the start.</summary>
    [RelayCommand]
    private void RestartWaves()
    {
        WaveTime = 0;
        IsPlaying = true;
    }

    /// <summary>
    /// The famous quake (or the first one) as seen from the viewer's place: the station is put as far round as the
    /// place is from the quake, and the waves start over.
    /// </summary>
    [RelayCommand]
    private void FromFamous(string? id)
    {
        var quake = PlateModel.Famous.FirstOrDefault(q => q.Id == id) ?? FamousQuake ?? PlateModel.Famous.First(q => q.Id == "Tohoku2011");
        Famous = quake.Id;
        Magnitude = quake.Magnitude;
        var place = _location.Current;
        StationDistance = Math.Round(PlateModel.ArcDegrees(new GeoPoint(quake.Latitude, quake.Longitude), new GeoPoint(place.Latitude, place.Longitude)));
        StationName = place.Name;
        RestartWaves();
    }

    /// <summary>Sets the station's distance (from a chip), as a point on the rim.</summary>
    [RelayCommand]
    private void SetStation(string? degrees)
    {
        if (!double.TryParse(degrees, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        StationName = null;
        StationDistance = value;
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

    private void Refresh(bool force = false)
    {
        // The clocks change every frame: rebuild the card only when what it says would change.
        var key = Mode switch
        {
            QuakeMode.Boundaries => $"{Section}|{Math.Round(SectionTime, Section == SectionKind.Ridge ? 1 : 0)}|{QuakeCount}",
            QuakeMode.Inside => $"{Math.Floor(WaveTime / 5)}|{QuakeAngle}",
            _ => "map",
        };
        if (!force && key == _shownKey) return;
        _shownKey = key;

        switch (Mode)
        {
            case QuakeMode.Boundaries:
                RefreshSection();
                break;

            case QuakeMode.Inside:
                RefreshInside();
                break;

            default:
                RefreshMap();
                break;
        }
    }

    private void RefreshMap()
    {
        var place = _location.Current;
        var here = new GeoPoint(place.Latitude, place.Longitude);
        var stats = new List<OscillationStat>();

        if (FamousQuake is { } quake)
        {
            PhaseTitle = _localization.Format("Quake_Map_Famous_Title", _localization.GetString(QuakeDiagram.ObjectTitleKey(QuakeDiagram.FamousTargetId(quake.Id))!), quake.Year);
            PhaseDescription = _localization.GetString(KindTextKey(quake.Kind));
            PhaseBrush = GoldBrush;
            var energy = PlateModel.Energy(quake.Magnitude) / 4.184e15;
            stats.Add(new(_localization.GetString("Quake_Stat_Magnitude"), Number(quake.Magnitude, "0.0")));
            stats.Add(new(_localization.GetString("Quake_Stat_Depth"), _localization.Format("Quake_Value_Km", Number(quake.Depth, "0"))));
            stats.Add(new(_localization.GetString("Quake_Stat_Energy"), _localization.Format("Quake_Value_Megatons", Number(energy, energy < 10 ? "0.0" : "#,0"))));
            stats.Add(new(_localization.GetString("Quake_Stat_Boundary"), _localization.GetString(QuakeDiagram.KindKey(quake.Kind))));
            stats.Add(new(_localization.GetString("Quake_Stat_FromYou"), _localization.Format("Quake_Value_Km", Number(Math.Round(PlateModel.Distance(here, new GeoPoint(quake.Latitude, quake.Longitude)) / 10) * 10, "#,0"))));
        }
        else if (SelectedBoundary >= 0 && SelectedBoundary < PlateModel.Boundaries.Count)
        {
            var boundary = PlateModel.Boundaries[SelectedBoundary];
            PhaseTitle = _localization.GetString(BoundaryKey(boundary.Id));
            PhaseDescription = _localization.GetString(KindTextKey(boundary.Kind));
            PhaseBrush = boundary.Kind switch
            {
                BoundaryKind.Ridge => RedBrush,
                BoundaryKind.Trench => CyanBrush,
                BoundaryKind.Collision => VioletBrush,
                BoundaryKind.Transform => GreenBrush,
                _ => GoldBrush,
            };
            stats.Add(new(_localization.GetString("Quake_Stat_Kind"), _localization.GetString(QuakeDiagram.KindKey(boundary.Kind))));
            if (boundary.Kind != BoundaryKind.Rift)
            {
                stats.Add(new(_localization.GetString("Quake_Stat_Plates"), _localization.Format("Quake_Value_TwoPlates", _localization.GetString(QuakeDiagram.PlateKey(boundary.A)), _localization.GetString(QuakeDiagram.PlateKey(boundary.B)))));
                var (closing, sliding) = PlateModel.RelativeMotion(boundary, Math.Max(0, (boundary.Points.Length - 2) / 2), 0.5);
                stats.Add(new(_localization.GetString(closing >= 0 ? "Quake_Stat_Closing" : "Quake_Stat_Opening"), _localization.Format("Quake_Value_CmYear", Number(Math.Abs(closing), "0.0"))));
                stats.Add(new(_localization.GetString("Quake_Stat_Sliding"), _localization.Format("Quake_Value_CmYear", Number(sliding, "0.0"))));
            }

            if (boundary.Kind == BoundaryKind.Trench) stats.Add(new(_localization.GetString("Quake_Stat_Sinks"), _localization.GetString(QuakeDiagram.PlateKey(boundary.A))));
        }
        else if (SelectedPlate is { } id)
        {
            var plate = PlateModel.Find(id);
            PhaseTitle = _localization.GetString(SixPlates ? QuakeDiagram.GroupKey(plate.Group) : QuakeDiagram.PlateKey(id));
            PhaseDescription = _localization.GetString(SixPlates ? "Quake_Map_Group_Text" : "Quake_Map_Plate_Text");
            PhaseBrush = CyanBrush;
            var (speed, azimuth) = PlateModel.Velocity(plate, plate.Label.Latitude, plate.Label.Longitude);
            stats.Add(new(_localization.GetString("Quake_Stat_Speed"), _localization.Format("Quake_Value_CmYearToward", Number(speed, "0.0"), _localization.GetString(CompassKey(azimuth)))));
            stats.Add(new(_localization.GetString("Quake_Stat_Share"), _localization.Format("Quake_Value_PercentMap", Number(AreaShare(id, SixPlates) * 100, "0"))));
            if (SixPlates && PlateModel.Plates.Count(p => p.Group == plate.Group) > 1)
            {
                var parts = string.Join(_localization.GetString("Quake_Value_ListSeparator"), PlateModel.Plates.Where(p => p.Group == plate.Group).Select(p => _localization.GetString(QuakeDiagram.PlateKey(p.Id))));
                stats.Add(new(_localization.GetString("Quake_Stat_Parts"), parts));
            }
        }
        else
        {
            var plate = PlateModel.PlateAt(place.Latitude, place.Longitude);
            var nearest = PlateModel.Nearest(here);
            PhaseTitle = _localization.Format("Quake_Map_Place_Title", place.Name, _localization.GetString(SixPlates ? QuakeDiagram.GroupKey(plate.Group) : QuakeDiagram.PlateKey(plate.Id)));
            PhaseDescription = _localization.GetString(nearest.Km < 300 ? "Quake_Map_Place_Near_Text" : "Quake_Map_Place_Far_Text");
            PhaseBrush = CyanBrush;
            var (speed, azimuth) = PlateModel.Velocity(plate, place.Latitude, place.Longitude);
            stats.Add(new(_localization.GetString("Quake_Stat_Place"), place.Name));
            stats.Add(new(_localization.GetString("Quake_Stat_YourPlate"), _localization.GetString(QuakeDiagram.PlateKey(plate.Id))));
            stats.Add(new(_localization.GetString("Quake_Stat_SpeedHere"), _localization.Format("Quake_Value_CmYearToward", Number(speed, "0.0"), _localization.GetString(CompassKey(azimuth)))));
            if (nearest.Boundary >= 0)
            {
                var boundary = PlateModel.Boundaries[nearest.Boundary];
                stats.Add(new(_localization.GetString("Quake_Stat_Nearest"), _localization.GetString(BoundaryKey(boundary.Id))));
                stats.Add(new(_localization.GetString("Quake_Stat_NearestKm"), _localization.Format("Quake_Value_Km", Number(Math.Round(nearest.Km / 10) * 10, "#,0"))));
            }

            stats.Add(new(_localization.GetString("Quake_Stat_InTenYears"), _localization.Format("Quake_Value_Cm", Number(speed * 10, "0"))));
        }

        Stats = stats;
    }

    /// <summary>What share of the map (by area) a plate or textbook group covers.</summary>
    private static double AreaShare(string id, bool group)
    {
        var plate = PlateModel.Find(id);
        var grid = PlateModel.PlateGrid;
        double total = 0;
        double mine = 0;
        for (var y = 0; y < PlateModel.GridHeight; y++)
        {
            var weight = Math.Cos((90 - ((y + 0.5) * 180.0 / PlateModel.GridHeight)) * Math.PI / 180);
            for (var x = 0; x < PlateModel.GridWidth; x++)
            {
                var p = PlateModel.Plates[grid[(y * PlateModel.GridWidth) + x]];
                total += weight;
                if (p.Id == id || (group && p.Group == plate.Group)) mine += weight;
            }
        }

        return mine / total;
    }

    private void RefreshSection()
    {
        var stats = new List<OscillationStat>();
        switch (Section)
        {
            case SectionKind.Ridge:
            {
                var normal = BoundaryModel.IsNormal(SectionTime);
                PhaseTitle = _localization.GetString(normal ? "Quake_Ridge_Normal_Title" : "Quake_Ridge_Reversed_Title");
                PhaseDescription = _localization.GetString(BoundaryModel.HasRiftValley(HalfRate) ? "Quake_Ridge_Slow_Text" : "Quake_Ridge_Fast_Text");
                PhaseBrush = normal ? GoldBrush : CyanBrush;
                stats.Add(new(_localization.GetString("Quake_Stat_When"), TimeText));
                stats.Add(new(_localization.GetString("Quake_Stat_Field"), _localization.GetString(normal ? "Quake_Value_FieldNormal" : "Quake_Value_FieldReversed")));
                stats.Add(new(_localization.GetString("Quake_Stat_Spreading"), _localization.Format("Quake_Value_CmYear", Number(HalfRate * 2, "0.0"))));
                stats.Add(new(_localization.GetString("Quake_Stat_EdgeAge"), _localization.Format("Quake_Value_Ma", Number(BoundaryModel.Age(400, HalfRate), "0.0"))));
                stats.Add(new(_localization.GetString("Quake_Stat_EdgeDepth"), _localization.Format("Quake_Value_KmDeep", Number(BoundaryModel.SeaDepth(BoundaryModel.Age(400, HalfRate)), "0.0"))));
                stats.Add(new(_localization.GetString("Quake_Stat_InLifetime"), _localization.Format("Quake_Value_M", Number(HalfRate * 2 * 80 / 100, "0.0"))));
                break;
            }

            case SectionKind.Subduction:
            {
                var share = Math.Clamp((SectionTime - LastQuakeYear) / QuakeDiagram.MegathrustCycle, 0, 1);
                PhaseTitle = _localization.GetString(QuakeCount > 0 && SectionTime - LastQuakeYear < 15 ? "Quake_Slab_Quake_Title" : "Quake_Slab_Locked_Title");
                PhaseDescription = _localization.GetString(QuakeCount > 0 && SectionTime - LastQuakeYear < 15 ? "Quake_Slab_Quake_Text" : "Quake_Slab_Locked_Text");
                PhaseBrush = QuakeCount > 0 && SectionTime - LastQuakeYear < 15 ? RedBrush : CyanBrush;
                stats.Add(new(_localization.GetString("Quake_Stat_Dip"), DipText));
                stats.Add(new(_localization.GetString("Quake_Stat_ArcFrom"), _localization.Format("Quake_Value_Km", Number(Math.Round(BoundaryModel.ArcDistance(Dip) / 10) * 10, "0"))));
                stats.Add(new(_localization.GetString("Quake_Stat_Since"), _localization.Format("Quake_Value_Years", Number(SectionTime - LastQuakeYear, "0"))));
                stats.Add(new(_localization.GetString("Quake_Stat_Stored"), _localization.Format("Quake_Value_M", Number(QuakeDiagram.ConvergenceRate / 100 * (SectionTime - LastQuakeYear), "0.0"))));
                if (QuakeCount > 0)
                {
                    stats.Add(new(_localization.GetString("Quake_Stat_LastQuake"), _localization.Format("Quake_Value_SlipMagnitude", Number(LastSlip, "0.0"), Number(BoundaryModel.Magnitude(LastSlip, 600, 40), "0.0"))));
                }

                stats.Add(new(_localization.GetString("Quake_Stat_Readiness"), _localization.Format("Quake_Value_Percent", Number(share * 100, "0"))));
                break;
            }

            case SectionKind.Collision:
            {
                var met = SectionTime <= BoundaryModel.ContactAge;
                var (himalaya, tibet) = BoundaryModel.Heights(SectionTime);
                PhaseTitle = _localization.GetString(met ? "Quake_Collision_Met_Title" : "Quake_Collision_Ocean_Title");
                PhaseDescription = _localization.GetString(met ? "Quake_Collision_Met_Text" : "Quake_Collision_Ocean_Text");
                PhaseBrush = met ? VioletBrush : CyanBrush;
                stats.Add(new(_localization.GetString("Quake_Stat_When"), TimeText));
                stats.Add(new(_localization.GetString("Quake_Stat_IndiaSpeed"), _localization.Format("Quake_Value_CmYear", Number(BoundaryModel.IndiaSpeed(SectionTime), "0"))));
                if (!met)
                {
                    stats.Add(new(_localization.GetString("Quake_Stat_OceanLeft"), _localization.Format("Quake_Value_Km", Number(Math.Round(BoundaryModel.TethysWidth(SectionTime) / 10) * 10, "#,0"))));
                }
                else
                {
                    stats.Add(new(_localization.GetString("Quake_Stat_Himalaya"), _localization.Format("Quake_Value_KmHigh", Number(himalaya, "0.0"))));
                    stats.Add(new(_localization.GetString("Quake_Stat_Tibet"), _localization.Format("Quake_Value_KmHigh", Number(tibet, "0.0"))));
                    stats.Add(new(_localization.GetString("Quake_Stat_Crust"), _localization.Format("Quake_Value_Km", Number(BoundaryModel.TibetCrust(SectionTime), "0"))));
                    stats.Add(new(_localization.GetString("Quake_Stat_Pushed"), _localization.Format("Quake_Value_Km", Number(Math.Round(BoundaryModel.IndiaTravel(BoundaryModel.ContactAge, SectionTime) / 10) * 10, "#,0"))));
                }

                break;
            }

            default:
            {
                var since = SectionTime - LastQuakeYear;
                var justBroke = QuakeCount > 0 && since < 10;
                PhaseTitle = _localization.GetString(Creep ? "Quake_Fault_Creep_Title" : justBroke ? "Quake_Fault_Quake_Title" : "Quake_Fault_Locked_Title");
                PhaseDescription = _localization.GetString(Creep ? "Quake_Fault_Creep_Text" : justBroke ? "Quake_Fault_Quake_Text" : "Quake_Fault_Locked_Text");
                PhaseBrush = Creep ? GreenBrush : justBroke ? RedBrush : GoldBrush;
                stats.Add(new(_localization.GetString("Quake_Stat_Years"), _localization.Format("Quake_Value_Years", Number(SectionTime, "0"))));
                stats.Add(new(_localization.GetString("Quake_Stat_Rate"), _localization.Format("Quake_Value_CmYear", Number(QuakeDiagram.FaultRate, "0.0"))));
                if (!Creep)
                {
                    stats.Add(new(_localization.GetString("Quake_Stat_Since"), _localization.Format("Quake_Value_Years", Number(since, "0"))));
                    stats.Add(new(_localization.GetString("Quake_Stat_Stored"), _localization.Format("Quake_Value_M", Number(QuakeDiagram.FaultRate / 100 * since, "0.0"))));
                    stats.Add(new(_localization.GetString("Quake_Stat_Quakes"), QuakeCount.ToString(CultureInfo.InvariantCulture)));
                    if (QuakeCount > 0)
                    {
                        stats.Add(new(_localization.GetString("Quake_Stat_LastQuake"), _localization.Format("Quake_Value_SlipMagnitude", Number(LastSlip, "0.0"), Number(BoundaryModel.Magnitude(LastSlip, 300), "0.0"))));
                    }
                }
                else
                {
                    stats.Add(new(_localization.GetString("Quake_Stat_Offset"), _localization.Format("Quake_Value_M", Number(QuakeDiagram.FaultRate / 100 * SectionTime, "0.0"))));
                }

                break;
            }
        }

        Stats = stats;
    }

    /// <summary>The section year of the last quake, noted when the diagram reports it.</summary>
    private double LastQuakeYear => QuakeCount == 0 ? 0 : _lastQuakeYearSeen;

    private double _lastQuakeYearSeen;

    partial void OnQuakeCountChanging(int value)
    {
        // A new quake: it happened now.
        _lastQuakeYearSeen = value > QuakeCount ? SectionTime : value == 0 ? 0 : _lastQuakeYearSeen;
    }

    private void RefreshInside()
    {
        var (p, s) = EarthModel.FirstArrivals(StationDistance);
        var surface = EarthModel.SurfaceWaveTime(StationDistance);
        var arrivals = EarthModel.Arrivals(StationDistance);
        var (pEnd, pkpStart, sEnd) = QuakeDiagram.ShadowZones;
        var inPShadow = StationDistance > pEnd && StationDistance < pkpStart;
        var inSShadow = StationDistance > sEnd;
        var stage = WaveTime <= 0 ? "Start"
            : p is { } pt && WaveTime < pt && arrivals.Count > 0 && WaveTime < arrivals[0].Time ? "Travelling"
            : s is { } st && WaveTime < st ? "PArrived"
            : WaveTime < surface ? "SArrived"
            : "Done";
        if (inPShadow && stage is "PArrived" or "SArrived") stage = "Shadow";

        PhaseTitle = _localization.GetString($"Quake_Inside_{stage}_Title");
        PhaseDescription = _localization.GetString(inPShadow ? "Quake_Inside_PShadow_Text" : inSShadow ? "Quake_Inside_SShadow_Text" : $"Quake_Inside_{stage}_Text");
        PhaseBrush = stage switch
        {
            "PArrived" => GoldBrush,
            "SArrived" => CyanBrush,
            "Shadow" => RedBrush,
            _ => VioletBrush,
        };

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Quake_Stat_SinceQuake"), WaveTimeText),
            new(_localization.GetString("Quake_Stat_Station"), StationName is { Length: > 0 } name ? $"{name} · {StationText}" : StationText),
            new(_localization.GetString("Quake_Stat_PArrives"), p is { } pTime ? MinSec(pTime) : _localization.GetString("Quake_Value_NoneShadow")),
            new(_localization.GetString("Quake_Stat_SArrives"), s is { } sTime ? MinSec(sTime) : _localization.GetString("Quake_Value_NoneShadow")),
        };
        if (p is { } a && s is { } b) stats.Add(new(_localization.GetString("Quake_Stat_SMinusP"), MinSec(b - a)));
        stats.Add(new(_localization.GetString("Quake_Stat_SurfaceArrives"), MinSec(surface)));
        if (inPShadow && arrivals.FirstOrDefault(x => x.Phase is Phase.PKIKP or Phase.PKiKP) is { Time: > 0 } faint)
        {
            stats.Add(new(_localization.GetString("Quake_Stat_Faint"), MinSec(faint.Time)));
        }

        stats.Add(new(_localization.GetString("Quake_Stat_Energy"), _localization.Format("Quake_Value_Megatons", Number(PlateModel.Energy(Magnitude) / 4.184e15, Magnitude < 7.7 ? "0.00" : "#,0"))));
        Stats = stats;
    }

    private string MinSec(double seconds) => _localization.Format("Quake_Value_MinSec", Number(Math.Floor(seconds / 60), "0"), Number(Math.Floor(seconds % 60), "00"));

    /// <summary>The text explaining a kind of boundary.</summary>
    private static string KindTextKey(BoundaryKind kind) => kind switch
    {
        BoundaryKind.Ridge => "Quake_Kind_Ridge_Text",
        BoundaryKind.Trench => "Quake_Kind_Trench_Text",
        BoundaryKind.Collision => "Quake_Kind_Collision_Text",
        BoundaryKind.Transform => "Quake_Kind_Transform_Text",
        BoundaryKind.Rift => "Quake_Kind_Rift_Text",
        _ => "Quake_Kind_Diffuse_Text",
    };

    /// <summary>A boundary's name key: <c>Quake_Boundary_SanAndreas</c>.</summary>
    internal static string BoundaryKey(string id) => "Quake_Boundary_" + id;

    /// <summary>One of eight compass directions for an azimuth.</summary>
    private static string CompassKey(double azimuth) => ((int)Math.Round(PlateModel.Normalize360(azimuth) / 45) % 8) switch
    {
        0 => "Quake_Dir_N",
        1 => "Quake_Dir_NE",
        2 => "Quake_Dir_E",
        3 => "Quake_Dir_SE",
        4 => "Quake_Dir_S",
        5 => "Quake_Dir_SW",
        6 => "Quake_Dir_W",
        _ => "Quake_Dir_NW",
    };

    #endregion

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PlaceLatitude));
        OnPropertyChanged(nameof(PlaceLongitude));
        OnPropertyChanged(nameof(PlaceName));
        if (StationName is not null) FromFamous(Famous);
        Refresh(force: true);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(ChartCaption));
        OnPropertyChanged(nameof(ChartNote));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(HalfRateText));
        OnPropertyChanged(nameof(DipText));
        OnPropertyChanged(nameof(StationText));
        OnPropertyChanged(nameof(WaveTimeText));
        OnPropertyChanged(nameof(PlaceName));
        if (StationName is not null) StationName = _location.Current.Name;
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SectionChoice.PropertyChanged -= OnSectionChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
