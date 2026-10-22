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

namespace Lemon.Explore.Book.Chapters.Rainbow;

/// <summary>
/// The rainbow chapter: which view is shown, the material and the light, the beam's angle, where the followed ray enters the
/// drop, the Sun's height and the drops' size, and what is happening. The diagram (<see cref="RainbowDiagram"/>) sweeps
/// and drags <see cref="Angle"/>, <see cref="Impact"/> and <see cref="SunAltitude"/>.
/// </summary>
public partial class RainbowViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x8B, 0xE0, 0x8F);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9F, 0x5A);
    private static readonly IBrush VioletBrush = Frozen(0xB3, 0x9D, 0xFF);
    private static readonly IBrush GreyBrush = Frozen(0x9A, 0xA5, 0xB5);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>What the card was last worked out for, so a sweep does not rebuild it every frame.</summary>
    private string _shownKey = string.Empty;

    private readonly IExploreLocationService _location;

    public IExploreLocationService Location => _location;

    public RainbowViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        Lesson = new LessonPlayer(narration, () => RainbowLessons.For(this));
        Card = new ObjectCard(narration, Lesson, RainbowDiagram.ObjectTitleKey, RainbowDiagram.ObjectClipKey);
        location.Changed += OnLocationChanged;
        SceneChoice.PropertyChanged += OnChosen;
        MediumChoice.PropertyChanged += OnChosen;
        ColourChoice.PropertyChanged += OnChosen;
        OrderChoice.PropertyChanged += OnChosen;
        DropChoice.PropertyChanged += OnChosen;
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
    [NotifyPropertyChangedFor(nameof(IsRefraction), nameof(IsDrop), nameof(IsSky), nameof(IsSurface), nameof(IsPrism), nameof(ModeKey),
        nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts), nameof(ShowsSpreadNote), nameof(ProfileSecondary))]
    private RainbowMode _mode;

    public bool IsRefraction
    {
        get => Mode == RainbowMode.Refraction;
        set
        {
            if (value) Mode = RainbowMode.Refraction;
        }
    }

    public bool IsDrop
    {
        get => Mode == RainbowMode.Drop;
        set
        {
            if (value) Mode = RainbowMode.Drop;
        }
    }

    public bool IsSky
    {
        get => Mode == RainbowMode.Sky;
        set
        {
            if (value) Mode = RainbowMode.Sky;
        }
    }

    /// <summary>The refraction view on a flat surface (the angle chips and "from inside" only apply there).</summary>
    public bool IsSurface => Mode == RainbowMode.Refraction && Scene == RefractionScene.Surface;

    public bool IsPrism => Mode == RainbowMode.Refraction && Scene == RefractionScene.Prism;

    /// <summary>The mode, or for the refraction view its scene: picks the subtitle, notes and facts.</summary>
    public string ModeKey => Mode == RainbowMode.Refraction ? $"Refraction_{Scene}" : Mode.ToString();

    /// <summary>The drop view following only the secondary bow says so: twice reflected, out at 51°.</summary>
    public string Subtitle => _localization.GetString(Mode == RainbowMode.Drop && Order == BowOrder.Secondary
        ? "Rain_Drop_Secondary_Subtitle"
        : $"Rain_{ModeKey}_Subtitle");

    public string ScaleNote => _localization.GetString(Mode == RainbowMode.Drop && Order == BowOrder.Secondary
        ? "Rain_Drop_Secondary_Note"
        : $"Rain_{ModeKey}_Note");

    public string DragHint => _localization.GetString($"Rain_DragHint_{ModeKey}");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Rain_{ModeKey}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(RainbowMode value)
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

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region choices

    public OscillationChoice SceneChoice { get; } = new(nameof(RefractionScene.Surface));

    public OscillationChoice MediumChoice { get; } = new(nameof(OpticalMedium.Water));

    public OscillationChoice ColourChoice { get; } = new(nameof(LightColour.White));

    public OscillationChoice OrderChoice { get; } = new(nameof(BowOrder.Primary));

    public OscillationChoice DropChoice { get; } = new(nameof(DropSize.Rain));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeKey), nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts), nameof(IsSurface), nameof(IsPrism))]
    private RefractionScene _scene;

    [ObservableProperty]
    private OpticalMedium _medium;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSpreadNote), nameof(IsWhite))]
    private LightColour _colour;

    /// <summary>White light: only then can its colours be spread wider.</summary>
    public bool IsWhite => Colour == LightColour.White;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileSecondary), nameof(Subtitle), nameof(ScaleNote))]
    private BowOrder _order;

    [ObservableProperty]
    private DropSize _dropSize = DropSize.Rain;

    private void OnChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OscillationChoice.Selected)) return;
        if (ReferenceEquals(sender, SceneChoice)) Scene = Enum.Parse<RefractionScene>(SceneChoice.Selected);
        else if (ReferenceEquals(sender, MediumChoice)) Medium = Enum.Parse<OpticalMedium>(MediumChoice.Selected);
        else if (ReferenceEquals(sender, ColourChoice)) Colour = Enum.Parse<LightColour>(ColourChoice.Selected);
        else if (ReferenceEquals(sender, OrderChoice)) Order = Enum.Parse<BowOrder>(OrderChoice.Selected);
        else if (ReferenceEquals(sender, DropChoice)) DropSize = Enum.Parse<DropSize>(DropChoice.Selected);
    }

    partial void OnSceneChanged(RefractionScene value)
    {
        // The refraction walkthrough picks its scene itself; picking one by hand ends it.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        SceneChoice.Selected = value.ToString();

        // A prism is glass unless chosen otherwise, and needs a steep enough beam to get out of its second face.
        if (value == RefractionScene.Prism)
        {
            if (Medium == OpticalMedium.Water) Medium = OpticalMedium.Glass;
            if (Angle < 30) Angle = 50;
        }

        Refresh();
    }

    partial void OnMediumChanged(OpticalMedium value)
    {
        MediumChoice.Selected = value.ToString();
        Refresh();
    }

    partial void OnColourChanged(LightColour value)
    {
        ColourChoice.Selected = value.ToString();
        Refresh();
    }

    partial void OnOrderChanged(BowOrder value)
    {
        OrderChoice.Selected = value.ToString();
        Refresh();
    }

    partial void OnDropSizeChanged(DropSize value)
    {
        DropChoice.Selected = value.ToString();
        Refresh();
    }

    /// <summary>Surface: the light comes from inside the material out into the air.</summary>
    [ObservableProperty]
    private bool _fromInside;

    [ObservableProperty]
    private bool _showWaves = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSpreadNote))]
    private bool _exaggerate = true;

    [ObservableProperty]
    private bool _showSheet = true;

    [ObservableProperty]
    private bool _showLosses = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileSecondary))]
    private bool _showSecondary = true;

    [ObservableProperty]
    private bool _showGuides = true;

    /// <summary>Refraction with white light spread wider than it is: the panel says so.</summary>
    public bool ShowsSpreadNote => Mode == RainbowMode.Refraction && Colour == LightColour.White && Exaggerate;

    /// <summary>Whether the brightness chart shows the secondary bow: the order chosen in the drop view, the switch in the sky.</summary>
    public bool ProfileSecondary => Mode == RainbowMode.Drop ? Order != BowOrder.Primary : ShowSecondary;

    partial void OnFromInsideChanged(bool value) => Refresh();

    partial void OnExaggerateChanged(bool value) => Refresh();

    partial void OnShowSecondaryChanged(bool value) => Refresh();

    #endregion

    #region the main values and playback

    /// <summary>Refraction: the beam's angle from the normal, degrees.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleText))]
    private double _angle = 40;

    /// <summary>Drop: how far off centre the followed ray enters, in radii.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImpactText))]
    private double _impact = 0.5;

    /// <summary>Sky: the Sun's height, degrees.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SunAltitudeText))]
    private double _sunAltitude = 20;

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Whether the main value runs back and forth by itself.</summary>
    [ObservableProperty]
    private bool _isSweeping = true;

    /// <summary>A sweep stopped: show the exact values again.</summary>
    partial void OnIsSweepingChanged(bool value) => Refresh();

    public string AngleText => $"{Number(Angle, "0")}°";

    public string ImpactText => _localization.Format("Rain_Impact_Value", Number(Impact, "0.00"));

    public string SunAltitudeText => $"{Number(SunAltitude, "0")}°";

    partial void OnAngleChanged(double value) => Refresh();

    partial void OnImpactChanged(double value) => Refresh();

    partial void OnSunAltitudeChanged(double value) => Refresh();

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    [RelayCommand]
    private void ToggleSweep()
    {
        IsSweeping = !IsSweeping;
        if (IsSweeping) IsPlaying = true;
    }

    [RelayCommand]
    private void SetAngle(string? angle)
    {
        if (!double.TryParse(angle, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        IsSweeping = false;
        Angle = value;
    }

    /// <summary>Puts the beam at the critical angle (from inside) or at the prism's least turn.</summary>
    [RelayCommand]
    private void SetSpecialAngle()
    {
        IsSweeping = false;
        var n = RainbowOptics.Index(Medium, Wavelength);
        Angle = Scene == RefractionScene.Prism
            ? Math.Round(RainbowOptics.MinimumDeviationIncidence(n, RainbowDiagram.PrismApex), 1)
            : Math.Round(RainbowOptics.CriticalAngle(n, 1) - 0.3, 1);
    }

    /// <summary>Follows the rainbow ray of the bow shown.</summary>
    [RelayCommand]
    private void SetRainbowRay()
    {
        IsSweeping = false;
        Impact = Math.Round(RainbowOptics.RainbowImpact(Order == BowOrder.Secondary ? 2 : 1, RainbowOptics.Index(OpticalMedium.Water, Wavelength)), 3);
    }

    /// <summary>The primary bow's angle from the antisolar point: with the Sun higher than this it is below the horizon.</summary>
    private const double BowRadius = 42;

    /// <summary>The chip that puts the Sun where it is right now at the shared observer location.</summary>
    public string HomeNowText => _localization.Format("Rain_Home_Now", _location.Current.Name);

    /// <summary>When the Sun is low enough for a rainbow today at the shared observer location.</summary>
    public string HomeWindowText
    {
        get
        {
            var home = _location.Current;
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, home.TimeZone));
            var low = DayNightGeometry.EventsOn(today, home.TimeZone, home.Latitude, home.Longitude, BowRadius);
            if (low.AlwaysDown) return _localization.Format("Rain_Home_AllDay", home.Name, Number(BowRadius, "0"));
            if (low.AlwaysUp || low.Rise is not { } above || low.Set is not { } below)
            {
                return _localization.Format("Rain_Home_Never", home.Name, Number(BowRadius, "0"));
            }

            string Clock(DateTime utc) => DayNightDiagram.ClockText(TimeZoneInfo.ConvertTimeFromUtc(utc, home.TimeZone));
            return _localization.Format("Rain_Home_Window", home.Name, Number(BowRadius, "0"), Clock(above), Clock(below));
        }
    }

    /// <summary>Sets the Sun's height to the real one now at the shared observer location (0° while it is down).</summary>
    [RelayCommand]
    private void SetHomeSun()
    {
        var home = _location.Current;
        IsSweeping = false;
        SunAltitude = Math.Round(Math.Clamp(DayNightGeometry.Altitude(DateTime.UtcNow, home.Latitude, home.Longitude), 0, 60), 1);
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HomeNowText));
        OnPropertyChanged(nameof(HomeWindowText));
    }

    [RelayCommand]
    private void SetSunAltitude(string? altitude)
    {
        if (!double.TryParse(altitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        IsSweeping = false;
        SunAltitude = value;
    }

    #endregion

    #region what is happening

    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = Brushes.White;
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

    /// <summary>The wavelength the numbers are given for: yellow for white light.</summary>
    private double Wavelength => Colour == LightColour.White ? RainbowOptics.YellowNm : RainbowOptics.Wavelength(Colour);

    private void Refresh(bool force = false)
    {
        // A sweep moves the value every frame, and a new list of stats lays the side panel out again: while sweeping, rebuild
        // the card about ten times a second (in coarse steps of the value), otherwise whenever a shown digit would change.
        var key = IsSweeping
            ? string.Create(CultureInfo.InvariantCulture,
                $"{Mode}|{Scene}|{Medium}|{Colour}|{Order}|{DropSize}|{FromInside}|{ShowSecondary}|{Math.Round(Angle)}|{Math.Round(Impact * 100)}|{Math.Round(SunAltitude * 2)}")
            : string.Create(CultureInfo.InvariantCulture,
                $"{Mode}|{Scene}|{Medium}|{Colour}|{Order}|{DropSize}|{FromInside}|{ShowSecondary}|{Angle:0.0}|{Impact:0.000}|{SunAltitude:0.0}");
        if (!force && key == _shownKey) return;
        _shownKey = key;

        switch (Mode)
        {
            case RainbowMode.Drop:
                RefreshDrop();
                break;

            case RainbowMode.Sky:
                RefreshSky();
                break;

            default:
                if (Scene == RefractionScene.Prism) RefreshPrism();
                else RefreshSurface();
                break;
        }
    }

    private void RefreshSurface()
    {
        var n = RainbowOptics.Index(Medium, Wavelength);
        var (n1, n2) = FromInside ? (n, 1.0) : (1.0, n);
        var angle = Math.Clamp(Angle, 0, 89);
        var bent = RainbowOptics.Refraction(n1, n2, angle);
        var share = RainbowOptics.Reflectance(n1, n2, angle);
        var critical = RainbowOptics.CriticalAngle(n, 1);

        var key = angle < 0.5 ? "Straight"
            : double.IsNaN(bent) ? "Total"
            : FromInside && critical - angle < 4 ? "Grazing"
            : FromInside ? "Away"
            : "Toward";
        PhaseTitle = _localization.GetString($"Rain_Surface_{key}_Title");
        PhaseDescription = _localization.GetString($"Rain_Surface_{key}_Description");
        PhaseBrush = key switch
        {
            "Total" => VioletBrush,
            "Grazing" => OrangeBrush,
            "Straight" => GreyBrush,
            _ => CyanBrush,
        };

        var medium = MediumName();
        var air = _localization.GetString("Rain_Medium_Air");
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Rain_Stat_Path"), _localization.Format("Rain_Stat_Path_Value", FromInside ? medium : air, FromInside ? air : medium)),
            new(_localization.GetString("Rain_Stat_Indices"), $"n₁ = {Number(n1, "0.000")} · n₂ = {Number(n2, "0.000")}"),
            new(_localization.GetString("Rain_Stat_Incidence"), $"{Number(angle, "0.0")}°"),
            new(_localization.GetString("Rain_Stat_Refraction"), double.IsNaN(bent) ? _localization.GetString("Rain_Stat_None") : $"{Number(bent, "0.0")}°"),
            new(_localization.GetString("Rain_Stat_Shares"), _localization.Format("Rain_Stat_Shares_Value", RainbowDiagram.Percent(share),
                RainbowDiagram.Percent(1 - share))),
            new(_localization.GetString("Rain_Stat_Speed"), _localization.Format("Rain_Stat_Speed_Value", medium, RainbowDiagram.Speed(n),
                Number(100 / n, "0"))),
        };

        if (Colour != LightColour.White)
        {
            stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Wavelength"),
                _localization.Format("Rain_Stat_Wavelength_Value", Number(Wavelength, "0"), Number(Wavelength / n, "0"), medium)));
        }
        else if (!double.IsNaN(bent))
        {
            var red = RainbowOptics.Index(Medium, RainbowOptics.RedNm);
            var violet = RainbowOptics.Index(Medium, RainbowOptics.VioletNm);
            var redBent = FromInside ? RainbowOptics.Refraction(red, 1, angle) : RainbowOptics.Refraction(1, red, angle);
            var violetBent = FromInside ? RainbowOptics.Refraction(violet, 1, angle) : RainbowOptics.Refraction(1, violet, angle);
            stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Colours"), _localization.Format("Rain_Stat_Colours_Value",
                Angles(redBent), Angles(violetBent))));
        }

        stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Critical"), _localization.Format("Rain_Stat_Critical_Value",
            Number(critical, "0.0"), medium)));
        Stats = stats;
    }

    private void RefreshPrism()
    {
        var apex = RainbowDiagram.PrismApex;
        var angle = Math.Clamp(Angle, 0, 89);
        var n = RainbowOptics.Index(Medium, Wavelength);
        var turn = RainbowOptics.PrismDeviation(n, apex, angle);
        var best = RainbowOptics.MinimumDeviationIncidence(n, apex);
        var key = double.IsNaN(turn) ? "Trapped" : Math.Abs(angle - best) < 2.5 ? "Least" : Colour == LightColour.White ? "Split" : "Bent";
        PhaseTitle = _localization.GetString($"Rain_Prism_{key}_Title");
        PhaseDescription = _localization.GetString($"Rain_Prism_{key}_Description");
        PhaseBrush = key switch
        {
            "Trapped" => VioletBrush,
            "Least" => GoldBrush,
            _ => CyanBrush,
        };

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Rain_Stat_Prism"), _localization.Format("Rain_Stat_Prism_Value", MediumName(), Number(apex, "0"))),
            new(_localization.GetString("Rain_Stat_Incidence"), $"{Number(angle, "0.0")}°"),
            new(_localization.GetString("Rain_Stat_Turn"), double.IsNaN(turn) ? _localization.GetString("Rain_Stat_None") : $"{Number(turn, "0.0")}°"),
            new(_localization.GetString("Rain_Stat_Least"), _localization.Format("Rain_Stat_Least_Value",
                Number(RainbowOptics.MinimumDeviation(n, apex), "0.0"), Number(best, "0.0"))),
        };

        if (Colour == LightColour.White)
        {
            var red = RainbowOptics.PrismDeviation(RainbowOptics.Index(Medium, RainbowOptics.RedNm), apex, angle);
            var violet = RainbowOptics.PrismDeviation(RainbowOptics.Index(Medium, RainbowOptics.VioletNm), apex, angle);
            stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Colours"), _localization.Format("Rain_Stat_Colours_Value",
                Angles(red), Angles(violet))));
            if (!double.IsNaN(red) && !double.IsNaN(violet))
            {
                stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Spread"), $"{Number(violet - red, "0.00")}°"));
            }
        }

        stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Indices"), _localization.Format("Rain_Stat_Dispersion_Value",
            Number(RainbowOptics.Index(Medium, RainbowOptics.RedNm), "0.000"), Number(RainbowOptics.Index(Medium, RainbowOptics.VioletNm), "0.000"))));
        Stats = stats;
    }

    private void RefreshDrop()
    {
        var n = RainbowOptics.Index(OpticalMedium.Water, Wavelength);
        var impact = Math.Clamp(Impact, 0, 0.995);
        var order = Order == BowOrder.Secondary ? 2 : 1;
        var bowImpact = RainbowOptics.RainbowImpact(order, n);
        var key = Math.Abs(impact - bowImpact) < 0.035 ? "Rainbow" : impact < bowImpact ? "Middle" : "Edge";
        PhaseTitle = _localization.GetString($"Rain_Drop_{key}_Title");
        PhaseDescription = _localization.GetString($"Rain_Drop_{key}_{(order == 1 ? "Primary" : "Secondary")}");
        PhaseBrush = key == "Rainbow" ? GoldBrush : key == "Middle" ? CyanBrush : GreenBrush;

        var incidence = Math.Asin(impact) / RainbowOptics.Deg;
        var inside = Math.Asin(impact / n) / RainbowOptics.Deg;
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Rain_Stat_Impact"), _localization.Format("Rain_Impact_Value", Number(impact, "0.00"))),
            new(_localization.GetString("Rain_Stat_InAngles"), _localization.Format("Rain_Stat_InAngles_Value", Number(incidence, "0.0"), Number(inside, "0.0"))),
        };

        foreach (var k in Order == BowOrder.Both ? new[] { 1, 2 } : [order])
        {
            var name = _localization.GetString(k == 1 ? "Rain_Order_Primary" : "Rain_Order_Secondary");
            stats.Add(new OscillationStat(_localization.Format("Rain_Stat_Exit", name),
                _localization.Format("Rain_Stat_Exit_Value", Number(RainbowOptics.ExitAngle(k, n, impact), "0.0"),
                    RainbowDiagram.Percent(RainbowOptics.Throughput(k, n, impact)))));
            stats.Add(new OscillationStat(_localization.Format("Rain_Stat_Bow", name), _localization.Format("Rain_Stat_Bow_Value",
                Number(RainbowOptics.RainbowAngle(k, n), "0.0"),
                Number(RainbowOptics.RainbowAngle(k, RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.RedNm)), "0.0"),
                Number(RainbowOptics.RainbowAngle(k, RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.VioletNm)), "0.0"),
                Number(RainbowOptics.RainbowImpact(k, n), "0.00"))));
        }

        stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Polarised"), _localization.Format("Rain_Stat_Polarised_Value",
            Number(RainbowOptics.Polarisation(1, n) * 100, "0"))));
        Stats = stats;
    }

    private void RefreshSky()
    {
        var sun = Math.Clamp(SunAltitude, 0, 60);
        var yellow = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm);
        var red = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.RedNm);
        var violet = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.VioletNm);
        var primary = RainbowOptics.RainbowAngle(1, yellow);
        var secondary = RainbowOptics.RainbowAngle(2, yellow);
        var primaryTop = RainbowOptics.TopAltitude(RainbowOptics.RainbowAngle(1, red), sun);
        var secondaryTop = RainbowOptics.TopAltitude(RainbowOptics.RainbowAngle(2, violet), sun);

        var key = primaryTop > 0
            ? sun < 3 ? "Low" : "Sinking"
            : ShowSecondary && secondaryTop > 0 ? "SecondaryOnly" : "TooHigh";
        PhaseTitle = _localization.GetString($"Rain_Sky_{key}_Title");
        PhaseDescription = _localization.GetString($"Rain_Sky_{key}_Description");
        PhaseBrush = key switch
        {
            "Low" => OrangeBrush,
            "Sinking" => GoldBrush,
            "SecondaryOnly" => VioletBrush,
            _ => GreyBrush,
        };

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Rain_Stat_Sun"), $"{Number(sun, "0.0")}°"),
            new(_localization.GetString("Rain_Stat_Antisolar"), _localization.Format("Rain_Stat_Antisolar_Value", Number(sun > 0 ? -sun : 0, "0.0"))),
            new(_localization.Format("Rain_Stat_Top", _localization.GetString("Rain_Order_Primary")), TopText(primary, sun)),
        };

        if (ShowSecondary)
        {
            stats.Add(new OscillationStat(_localization.Format("Rain_Stat_Top", _localization.GetString("Rain_Order_Secondary")), TopText(secondary, sun)));
        }

        // Colours: how far the bow is smeared against how far red is from violet.
        var size = DropSize;
        var smear = RainbowOptics.Smearing(RainbowOptics.YellowNm, RainbowOptics.Diameter(size));
        var spread = RainbowOptics.RainbowAngle(1, red) - RainbowOptics.RainbowAngle(1, violet);
        var purity = smear < spread / 3 ? "Vivid" : smear < spread ? "Pastel" : "White";
        stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Drops"), _localization.GetString($"Rain_Size_{size}")));
        stats.Add(new OscillationStat(_localization.GetString("Rain_Stat_Colour"), _localization.Format("Rain_Stat_Colour_Value",
            _localization.GetString($"Rain_Purity_{purity}"), Number(smear, "0.0"), Number(spread, "0.0"))));
        Stats = stats;
    }

    /// <summary>"22.0° up · 38% of its circle above the horizon", or "below the horizon".</summary>
    private string TopText(double radius, double sun)
    {
        var top = RainbowOptics.TopAltitude(radius, sun);
        return top <= 0
            ? _localization.GetString("Rain_Stat_Top_Below")
            : _localization.Format("Rain_Stat_Top_Value", Number(top, "0.0"), Number(RainbowOptics.ShareAboveHorizon(radius, sun) * 100, "0"));
    }

    private string MediumName() => _localization.GetString($"Rain_Medium_{Medium}");

    private static string Angles(double angle) => double.IsNaN(angle) ? "—" : $"{Number(angle, "0.0")}°";

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HomeNowText));
        OnPropertyChanged(nameof(HomeWindowText));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(ImpactText));
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SceneChoice.PropertyChanged -= OnChosen;
        MediumChoice.PropertyChanged -= OnChosen;
        ColourChoice.PropertyChanged -= OnChosen;
        OrderChoice.PropertyChanged -= OnChosen;
        DropChoice.PropertyChanged -= OnChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
