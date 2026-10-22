using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>A row of the "what is happening" card: a name and its value.</summary>
public sealed record KeplerStat(string Name, string Value);

/// <summary>A row of the third-law table: a body, its distance, its year, and T² / a³.</summary>
public sealed record KeplerPeriodRow(string Name, string Distance, string Period, string Ratio);

/// <summary>
/// One choice out of several (the body, the number of sectors) for a row of option chips: each chip binds its
/// <c>IsChecked</c> to <c>[id]</c>.
/// </summary>
public sealed class KeplerChoice(string selected) : ObservableObject
{
    private string _selected = selected;

    public string Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            // Avalonia's indexer bindings listen for the indexer's own name, not WPF's "Item[]".
            OnPropertyChanged("Item");
        }
    }

    public bool this[string id]
    {
        get => _selected == id;
        set
        {
            if (value) Selected = id;
        }
    }
}

/// <summary>
/// The Kepler's laws page: which law is shown, the orbit's body and shape, playback, and the numbers of the current
/// moment. The diagram (<see cref="KeplerDiagram"/>) moves <see cref="Phase"/>, <see cref="Years"/> and
/// <see cref="YourAxis"/>; everything else follows them. <see cref="Lesson"/> is the spoken walkthrough of the current
/// view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class KeplerViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush FastBrush = Frozen(0xFF, 0x8A, 0x65);
    private static readonly IBrush SlowBrush = Frozen(0x6F, 0xC3, 0xFF);
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush OrbitBrush = Frozen(0xB8, 0xC7, 0xFF);
    private static readonly IBrush YouBrush = Frozen(0x6F, 0xE3, 0xC0);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IBookNavigator _navigator;

    /// <summary>The example orbit's eccentricity, kept while another body is shown.</summary>
    private double _customEccentricity = KeplerGeometry.Body("Custom").Eccentricity;

    private bool _choosingBody;

    public KeplerViewModel(IBookNavigator navigator, INarrationService narration)
    {
        _navigator = navigator;
        Lesson = new LessonPlayer(narration, () => KeplerLessons.For(this));
        Card = new ObjectCard(narration, Lesson, KeplerDiagram.ObjectTitleKey, KeplerDiagram.ObjectClipKey);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

        Body.PropertyChanged += OnBodyChosen;
        SectorChoice.PropertyChanged += OnSectorsChosen;

        StartUtc = DateTime.UtcNow;
        _eccentricity = _customEccentricity;
        _phase = 0.08;
        Refresh();
        RefreshPeriodRows();
        RefreshEarth();
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

    #region mode and options

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEllipse), nameof(IsAreas), nameof(IsPeriods), nameof(IsOrbitMode),
        nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts))]
    private KeplerMode _mode;

    public bool IsEllipse
    {
        get => Mode == KeplerMode.Ellipse;
        set
        {
            if (value) Mode = KeplerMode.Ellipse;
        }
    }

    public bool IsAreas
    {
        get => Mode == KeplerMode.Areas;
        set
        {
            if (value) Mode = KeplerMode.Areas;
        }
    }

    public bool IsPeriods
    {
        get => Mode == KeplerMode.Periods;
        set
        {
            if (value) Mode = KeplerMode.Periods;
        }
    }

    /// <summary>The ellipse and areas views, which show one body's orbit and its shape.</summary>
    public bool IsOrbitMode => Mode != KeplerMode.Periods;

    public string Subtitle => _localization.GetString(Mode switch
    {
        KeplerMode.Areas => "Kepler_Areas_Subtitle",
        KeplerMode.Periods => "Kepler_Periods_Subtitle",
        _ => "Kepler_Ellipse_Subtitle",
    });

    public string ScaleNote => _localization.GetString(Mode switch
    {
        KeplerMode.Areas => "Kepler_Areas_Note",
        KeplerMode.Periods => "Kepler_Periods_Note",
        _ => "Kepler_Ellipse_Note",
    });

    /// <summary>What dragging on the diagram does in this view.</summary>
    public string DragHint => _localization.GetString(Mode == KeplerMode.Periods ? "Kepler_DragHint_You" : "Kepler_DragHint_Orbit");

    /// <summary>Which body goes round in the ellipse and areas views (<see cref="KeplerBody.Id"/>).</summary>
    public KeplerChoice Body { get; } = new("Custom");

    /// <summary>Areas view: how many stretches of equal time.</summary>
    public KeplerChoice SectorChoice { get; } = new("12");

    [ObservableProperty]
    private int _sectors = 12;

    /// <summary>The shape of the orbit; moving it away from the chosen body's switches to the example orbit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EccentricityText))]
    private double _eccentricity;

    public double MaxEccentricity => KeplerGeometry.MaxEccentricity;

    public string EccentricityText => _localization.Format("Kepler_Ecc_Value", Eccentricity.ToString("0.000"));

    /// <summary>The chosen body's semi-major axis in AU, for the speeds in km/s.</summary>
    public double SemiMajorAxis => CurrentBody.SemiMajorAxisAu;

    [ObservableProperty]
    private bool _showString = true;

    [ObservableProperty]
    private bool _showCircle;

    [ObservableProperty]
    private bool _showVelocity = true;

    /// <summary>Periods view: your planet's distance from the Sun, AU.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(YourAxisText))]
    private double _yourAxis = 2.5;

    public double MinYourAxis => KeplerDiagram.MinYourAxis;

    public double MaxYourAxis => KeplerDiagram.MaxYourAxis;

    public string YourAxisText => _localization.Format("Kepler_YourAxis_Value", KeplerFormat.Au(YourAxis),
        KeplerFormat.Duration(KeplerGeometry.PeriodYears(YourAxis)));

    /// <summary>Puts your planet where a real one is, by its distance in AU.</summary>
    [RelayCommand]
    private void SetYourAxis(string? au)
    {
        if (double.TryParse(au, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            YourAxis = Math.Clamp(value, MinYourAxis, MaxYourAxis);
        }
    }

    [RelayCommand]
    private void OpenSolarSystem() => _navigator.GoToPage(BookContents.ChapterPageOf("Solar-System"));

    private KeplerBody CurrentBody => Body.Selected == "Custom"
        ? new KeplerBody("Custom", 1, Eccentricity)
        : KeplerGeometry.Body(Body.Selected);

    private void OnBodyChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(KeplerChoice.Selected)) return;

        _choosingBody = true;
        try
        {
            Eccentricity = Body.Selected == "Custom" ? _customEccentricity : KeplerGeometry.Body(Body.Selected).Eccentricity;
        }
        finally
        {
            _choosingBody = false;
        }

        OnPropertyChanged(nameof(SemiMajorAxis));
        Refresh();
    }

    private void OnSectorsChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeplerChoice.Selected) && int.TryParse(SectorChoice.Selected, out var count)) Sectors = count;
    }

    partial void OnEccentricityChanged(double value)
    {
        if (!_choosingBody)
        {
            // The slider shapes the example orbit.
            _customEccentricity = value;
            if (Body.Selected != "Custom") Body.Selected = "Custom";
        }

        Refresh();
    }

    partial void OnYourAxisChanged(double value) => Refresh();

    #endregion

    #region playback

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Ellipse and areas: the fraction of the period since perihelion, 0…1.</summary>
    [ObservableProperty]
    private double _phase;

    /// <summary>Periods: Earth years since the page opened.</summary>
    [ObservableProperty]
    private double _years;

    public double MaxYears => KeplerDiagram.MaxYears;

    /// <summary>The moment the periods view starts at: now.</summary>
    public DateTime StartUtc { get; }

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Jumps to perihelion, aphelion or (periods view) today, and pauses there.</summary>
    [RelayCommand]
    private void JumpTo(string? target)
    {
        switch (target)
        {
            case "Perihelion":
                Phase = 0;
                break;
            case "Aphelion":
                Phase = 0.5;
                break;
            default:
                Years = 0;
                break;
        }

        IsPlaying = false;
    }

    partial void OnModeChanged(KeplerMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
        Refresh();
    }

    partial void OnPhaseChanged(double value) => Refresh();

    partial void OnYearsChanged(double value) => Refresh();

    #endregion

    #region walkthrough and clicked things

    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    /// <summary>For a walkthrough: the example orbit with the given shape.</summary>
    internal void ShowExampleOrbit(double eccentricity)
    {
        Body.Selected = "Custom";
        Eccentricity = eccentricity;
    }

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _stageTitle = string.Empty;
    private string _stageDescription = string.Empty;
    private IBrush _stageBrush = OrbitBrush;
    private IReadOnlyList<KeplerStat> _stats = [];
    private IReadOnlyList<KeplerPeriodRow> _periodRows = [];
    private IReadOnlyList<KeplerStat> _earthApsides = [];

    /// <summary>Beside the timeline: time since perihelion of the period, or the date in the periods view.</summary>
    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    public string StageTitle
    {
        get => _stageTitle;
        private set => SetProperty(ref _stageTitle, value);
    }

    public string StageDescription
    {
        get => _stageDescription;
        private set => SetProperty(ref _stageDescription, value);
    }

    public IBrush StageBrush
    {
        get => _stageBrush;
        private set => SetProperty(ref _stageBrush, value);
    }

    public IReadOnlyList<KeplerStat> Stats
    {
        get => _stats;
        private set => SetProperty(ref _stats, value);
    }

    /// <summary>Periods view: each body's distance, year and T² / a³ from measured values.</summary>
    public IReadOnlyList<KeplerPeriodRow> PeriodRows
    {
        get => _periodRows;
        private set => SetProperty(ref _periodRows, value);
    }

    /// <summary>Earth's coming perihelion and aphelion with their distances.</summary>
    public IReadOnlyList<KeplerStat> EarthApsides
    {
        get => _earthApsides;
        private set => SetProperty(ref _earthApsides, value);
    }

    /// <summary>Background facts for the current view.</summary>
    public IReadOnlyList<string> Facts => (Mode switch
    {
        KeplerMode.Areas => new[] { "Kepler_Areas_Fact1", "Kepler_Areas_Fact2", "Kepler_Areas_Fact3", "Kepler_Areas_Fact4", "Kepler_Areas_Fact5" },
        KeplerMode.Periods => ["Kepler_Periods_Fact1", "Kepler_Periods_Fact2", "Kepler_Periods_Fact3", "Kepler_Periods_Fact4", "Kepler_Periods_Fact5"],
        _ => ["Kepler_Ellipse_Fact1", "Kepler_Ellipse_Fact2", "Kepler_Ellipse_Fact3", "Kepler_Ellipse_Fact4", "Kepler_Ellipse_Fact5"],
    }).Select(_localization.GetString).ToList();

    private void Refresh()
    {
        if (Mode == KeplerMode.Periods)
        {
            RefreshPeriods();
        }
        else
        {
            RefreshOrbit();
        }
    }

    /// <summary>Where the body is on its orbit, how far from the Sun and the other focus, and how fast.</summary>
    private void RefreshOrbit()
    {
        var body = CurrentBody;
        var e = body.Eccentricity;
        var a = body.SemiMajorAxisAu;
        var point = KeplerGeometry.At(Phase, e);
        var period = body.PeriodYears;
        ClockText = _localization.Format("Kepler_Clock_Orbit", KeplerFormat.Duration(Phase * period, period), KeplerFormat.Duration(period));

        var stage = KeplerGeometry.StageAt(Phase, e);
        StageTitle = _localization.GetString($"Kepler_Stage_{stage}");
        StageDescription = _localization.GetString($"Kepler_Stage_{stage}_Description");
        StageBrush = stage switch
        {
            OrbitStage.Perihelion => FastBrush,
            OrbitStage.Aphelion => SlowBrush,
            OrbitStage.Approaching => GoldBrush,
            OrbitStage.Receding => OrbitBrush,
            _ => YouBrush,
        };

        var stats = new List<KeplerStat>
        {
            new(_localization.GetString("Kepler_Stat_Body"), _localization.GetString(KeplerDiagram.BodyNameKey(body.Id))),
            new(_localization.GetString("Kepler_Stat_Distance"), DistanceText(point.Radius * a)),
        };

        if (Mode == KeplerMode.Ellipse)
        {
            stats.Add(new KeplerStat(_localization.GetString("Kepler_Stat_OtherFocus"), DistanceText((2 - point.Radius) * a)));
            stats.Add(new KeplerStat(_localization.GetString("Kepler_Stat_Sum"),
                _localization.Format("Kepler_Stat_Sum_Value", KeplerFormat.Au(2 * a))));
            stats.Add(new KeplerStat(_localization.GetString("Kepler_Stat_Ecc"),
                _localization.Format("Kepler_Stat_Ecc_Value", e.ToString("0.000"), KeplerFormat.Au(e * a))));
            stats.Add(new KeplerStat(_localization.GetString("Kepler_Stat_Range"),
                _localization.Format("Kepler_Stat_Range_Value", KeplerFormat.Au(body.PerihelionAu), KeplerFormat.Au(body.AphelionAu))));
        }
        else
        {
            stats.Add(new KeplerStat(_localization.GetString("Kepler_Stat_Speed"),
                _localization.Format("Kepler_Stat_Speed_Value", KeplerGeometry.SpeedKmS(a, point.Radius))));
            stats.Add(new KeplerStat(_localization.GetString("Kepler_Stat_SpeedRange"),
                _localization.Format("Kepler_Stat_SpeedRange_Value", KeplerGeometry.SpeedKmS(a, 1 + e), KeplerGeometry.SpeedKmS(a, 1 - e))));
            stats.Add(new KeplerStat(_localization.GetString("Kepler_Stat_Swept"),
                _localization.Format("Kepler_Stat_Swept_Value", Phase * 100)));
            stats.Add(new KeplerStat(_localization.GetString("Kepler_Stat_Period"), KeplerFormat.Duration(period)));
        }

        Stats = stats;
    }

    /// <summary>Your planet's year from its distance, checked against T² = a³, and the laps so far.</summary>
    private void RefreshPeriods()
    {
        var culture = LocalizationService.Instance.CurrentCulture;
        ClockText = StartUtc.ToLocalTime().AddDays(Years * KeplerGeometry.DaysPerYear).ToString("Y", culture);

        var a = YourAxis;
        var period = KeplerGeometry.PeriodYears(a);
        StageTitle = _localization.Format("Kepler_You_Title", KeplerFormat.Duration(period));
        StageDescription = _localization.GetString(a < 0.9 ? "Kepler_You_Inner" : a <= 1.1 ? "Kepler_You_Earth" : "Kepler_You_Outer");
        StageBrush = YouBrush;

        Stats =
        [
            new KeplerStat(_localization.GetString("Kepler_Stat_YourAxis"), DistanceText(a)),
            new KeplerStat(_localization.GetString("Kepler_Stat_Period"), KeplerFormat.Duration(period)),
            new KeplerStat(_localization.GetString("Kepler_Stat_Check"),
                _localization.Format("Kepler_Stat_Check_Value", period.ToString("0.00"), a.ToString("0.00"))),
            new KeplerStat(_localization.GetString("Kepler_Stat_AverageSpeed"),
                _localization.Format("Kepler_Stat_AverageSpeed_Value", KeplerGeometry.CircularSpeedKmS(a))),
            new KeplerStat(_localization.GetString("Kepler_Stat_Laps"),
                _localization.Format("Kepler_Stat_Laps_Value", Math.Floor(Years / period), Math.Floor(Years))),
        ];
    }

    private void RefreshPeriodRows()
    {
        PeriodRows = KeplerGeometry.PeriodPoints
            .Select(p => new KeplerPeriodRow(
                _localization.GetString(KeplerDiagram.BodyNameKey(p.Id)),
                KeplerFormat.Au(p.SemiMajorAxisAu),
                KeplerFormat.Years(p.PeriodYears),
                p.Ratio.ToString("0.000")))
            .ToList();
    }

    /// <summary>Earth's next perihelion and aphelion from its orbit, with the distance at each.</summary>
    private void RefreshEarth()
    {
        var orbit = SolarSystemCatalog.Bodies.First(b => b.Id == "Earth").Orbit!;
        var earth = KeplerGeometry.Body("Earth");
        var culture = LocalizationService.Instance.CurrentCulture;
        var now = DateTime.UtcNow;
        var perihelion = KeplerGeometry.NextApsis(orbit, now, perihelion: true).ToLocalTime();
        var aphelion = KeplerGeometry.NextApsis(orbit, now, perihelion: false).ToLocalTime();
        EarthApsides = new[]
            {
                (Time: perihelion, Key: "Kepler_Earth_Perihelion", Au: earth.PerihelionAu),
                (Time: aphelion, Key: "Kepler_Earth_Aphelion", Au: earth.AphelionAu),
            }
            .OrderBy(x => x.Time)
            .Select(x => new KeplerStat(_localization.GetString(x.Key),
                _localization.Format("Kepler_Earth_When", x.Time.ToString("M", culture), KmText(x.Au))))
            .ToList();
    }

    /// <summary>
    /// A distance in AU and in kilometres, counted in the unit the language uses for big distances (millions in
    /// English, 亿 in Chinese): <c>Kepler_Km_Unit</c> holds how many kilometres that is.
    /// </summary>
    private string DistanceText(double au)
    {
        return _localization.Format("Kepler_Distance", KeplerFormat.Au(au), KmText(au));
    }

    private string KmText(double au)
    {
        var km = au * KeplerGeometry.AuMetres / 1000;
        var unit = double.TryParse(_localization.GetString("Kepler_Km_Unit"), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0 ? value : 1e6;
        return _localization.Format("Kepler_Km", km / unit);
    }

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(EccentricityText));
        OnPropertyChanged(nameof(YourAxisText));
        Refresh();
        RefreshPeriodRows();
        RefreshEarth();
    }

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        Body.PropertyChanged -= OnBodyChosen;
        SectorChoice.PropertyChanged -= OnSectorsChosen;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}
