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

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// The aurora chapter: which view is shown; the solar wind (speed, density, the field's north–south part) and the Dungey cycle;
/// the trapped particle (electron or proton, energy, shell, pitch angle); the oval's Kp, the electrons' energy and the night
/// at the shared location; playback; and what is happening. The diagram (<see cref="AuroraDiagram"/>) runs
/// <see cref="CyclePhase"/> and <see cref="SkyHour"/>. <see cref="Lesson"/> is the spoken walkthrough of the current view,
/// and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class AuroraViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);
    private static readonly IBrush GreyBrush = Frozen(0x9A, 0xA5, 0xB5);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    public IExploreLocationService Location => _location;

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while the clock runs.</summary>
    private string _shownKey = string.Empty;

    public AuroraViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        Lesson = new LessonPlayer(narration, () => AuroraLessons.For(this));
        Card = new ObjectCard(narration, Lesson, AuroraDiagram.ObjectTitleKey, AuroraDiagram.ObjectClipKey);
        location.Changed += OnLocationChanged;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        WindChoice.PropertyChanged += OnWindChosen;
        SpeciesChoice.PropertyChanged += OnSpeciesChosen;
        EnergyChoice.PropertyChanged += OnParticleEnergyChosen;
        KpChoice.PropertyChanged += OnKpChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        SetNight(DateTime.UtcNow);
        _kp = Math.Round(AuroraModel.EstimatedKp(WindDensity, WindSpeed, Bz), 1);
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
    [NotifyPropertyChangedFor(nameof(IsShield), nameof(IsParticles), nameof(IsSky), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private AuroraMode _mode;

    public bool IsShield
    {
        get => Mode == AuroraMode.Shield;
        set
        {
            if (value) Mode = AuroraMode.Shield;
        }
    }

    public bool IsParticles
    {
        get => Mode == AuroraMode.Particles;
        set
        {
            if (value) Mode = AuroraMode.Particles;
        }
    }

    public bool IsSky
    {
        get => Mode == AuroraMode.Sky;
        set
        {
            if (value) Mode = AuroraMode.Sky;
        }
    }

    public string Subtitle => Mode == AuroraMode.Sky
        ? _localization.Format("Aurora_Sky_Subtitle", _location.Current.Name)
        : _localization.GetString($"Aurora_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Aurora_{Mode}_Note");

    public string Hint => _localization.GetString($"Aurora_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Aurora_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Aurora_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Aurora_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(AuroraMode value)
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
    private bool _showBelts = true;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    #endregion

    #region the solar wind

    /// <summary>Solar wind chips: calm, typical, a fast stream, a storm (a coronal mass ejection arriving).</summary>
    public OscillationChoice WindChoice { get; } = new("Typical");

    /// <summary>Solar wind speed, km/s.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindSpeedText))]
    private double _windSpeed = 400;

    /// <summary>Solar wind density, protons per cm³.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindDensityText))]
    private double _windDensity = 5;

    /// <summary>The interplanetary field's north–south part, nT.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BzText))]
    private double _bz = -5;

    /// <summary>Earth without a magnetic field.</summary>
    [ObservableProperty]
    private bool _noField;

    /// <summary>Where the followed field line is in the Dungey cycle, 0–1 (from the diagram).</summary>
    [ObservableProperty]
    private double _cyclePhase;

    public string WindSpeedText => _localization.Format("Aurora_Value_Speed", Number(WindSpeed, "0"));

    public string WindDensityText => _localization.Format("Aurora_Value_Density", Number(WindDensity, "0.#"));

    public string BzText => _localization.Format(Bz < -0.05 ? "Aurora_Value_BzSouth" : Bz > 0.05 ? "Aurora_Value_BzNorth" : "Aurora_Value_BzZero", Number(Math.Abs(Bz), "0.#"));

    partial void OnWindSpeedChanged(double value) => OnWindChanged();

    partial void OnWindDensityChanged(double value) => OnWindChanged();

    partial void OnBzChanged(double value) => OnWindChanged();

    partial void OnNoFieldChanged(bool value) => Refresh(force: true);

    partial void OnCyclePhaseChanged(double value) => Refresh();

    /// <summary>A preset being applied: its three values arrive one by one and must not clear the chip.</summary>
    private bool _applyingWind;

    /// <summary>The wind sets Kp: the oval in the sky view follows it (until Kp is set there by hand).</summary>
    private void OnWindChanged()
    {
        Kp = Math.Round(AuroraModel.EstimatedKp(WindDensity, WindSpeed, Bz), 1);
        if (!_applyingWind && WindPreset(WindChoice.Selected) is { } preset && preset != (WindSpeed, WindDensity, Bz)) WindChoice.Selected = string.Empty;
        Refresh(force: true);
    }

    /// <summary>The solar wind of a chip: speed, density and Bz.</summary>
    private static (double, double, double)? WindPreset(string id) => id switch
    {
        "Calm" => (330.0, 3.0, 3.0),
        "Typical" => (400.0, 5.0, -5.0),
        "Fast" => (650.0, 3.0, -4.0),
        "Storm" => (850.0, 25.0, -22.0),
        _ => null,
    };

    private void OnWindChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OscillationChoice.Selected) || WindPreset(WindChoice.Selected) is not { } preset) return;
        _applyingWind = true;
        try
        {
            (WindSpeed, WindDensity, Bz) = preset;
        }
        finally
        {
            _applyingWind = false;
        }
    }

    /// <summary>The cycle's stage now: arriving, opening, sinking to the tail, snapping back, returning (southward field only).</summary>
    public string CycleStage => NoField ? "NoField" : Bz >= 0 ? (Bz > 0 ? "North" : "Zero") : CyclePhase switch
    {
        < AuroraDiagram.StageOpen => "Arrive",
        < AuroraDiagram.StageSink => "Open",
        < AuroraDiagram.StageSnap => "Sink",
        < AuroraDiagram.StageFade => "Snap",
        _ => "Return",
    };

    /// <summary>The cycle held at a stage (and paused): for the walkthroughs.</summary>
    internal void HoldCycle(double phase)
    {
        CyclePhase = phase;
        IsPlaying = false;
    }

    #endregion

    #region the trapped particle

    /// <summary>Electron or proton.</summary>
    public OscillationChoice SpeciesChoice { get; } = new(nameof(AuroraSpecies.Electron));

    [ObservableProperty]
    private AuroraSpecies _species = AuroraSpecies.Electron;

    /// <summary>The particle's energy chips: 1 keV, 100 keV, 1 MeV (only its real numbers change; the picture is scaled).</summary>
    public OscillationChoice EnergyChoice { get; } = new("100");

    [ObservableProperty]
    private double _particleEnergy = 100;

    /// <summary>The particle's shell: where its field line crosses the equator, Earth radii.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShellText), nameof(LossCone))]
    private double _shell = 4;

    /// <summary>The particle's pitch angle at the equator, degrees.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PitchText))]
    private double _pitchAngle = 30;

    [ObservableProperty]
    private bool _showRingCurrent;

    /// <summary>Changed to send a fresh particle off.</summary>
    [ObservableProperty]
    private int _restartCount;

    public string ShellText => _localization.Format("Aurora_Value_Shell", Number(Shell, "0.0"), Number(AuroraModel.FootLatitude(Shell), "0"));

    public string PitchText => _localization.Format("Aurora_Value_Pitch", Number(PitchAngle, "0"));

    /// <summary>The loss cone on the particle's shell, degrees.</summary>
    public double LossCone => AuroraModel.LossCone(Shell);

    /// <summary>The pitch angle is inside the loss cone: the particle will be lost into the air.</summary>
    public bool IsInLossCone => PitchAngle < LossCone;

    partial void OnSpeciesChanged(AuroraSpecies value)
    {
        SpeciesChoice.Selected = value.ToString();
        Refresh(force: true);
    }

    partial void OnParticleEnergyChanged(double value) => Refresh(force: true);

    partial void OnShellChanged(double value) => Refresh(force: true);

    partial void OnPitchAngleChanged(double value)
    {
        OnPropertyChanged(nameof(IsInLossCone));
        Refresh(force: true);
    }

    private void OnSpeciesChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<AuroraSpecies>(SpeciesChoice.Selected, out var species)) Species = species;
    }

    private void OnParticleEnergyChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) ParticleEnergy = double.Parse(EnergyChoice.Selected, CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private void Restart()
    {
        RestartCount++;
        IsPlaying = true;
    }

    #endregion

    #region the oval and the night

    /// <summary>Kp chips: quiet, unsettled, storm, strong storm, the top of the scale.</summary>
    public OscillationChoice KpChoice { get; } = new(string.Empty);

    /// <summary>Geomagnetic activity, 0–9: from the solar wind, or set here.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KpText))]
    private double _kp;

    /// <summary>The energy of the electrons lighting the curtain, keV.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnergyText), nameof(EnergyLog))]
    private double _energyKeV = 5;

    /// <summary>The instant (UTC) the night's hours count from: local noon before it.</summary>
    [ObservableProperty]
    private DateTime _skyBase;

    /// <summary>Hours since <see cref="SkyBase"/>, 0–24 (from the diagram, the timeline or the buttons).</summary>
    [ObservableProperty]
    private double _skyHour;

    public string KpText => _localization.Format("Aurora_Value_Kp", Number(Kp, "0.#"), _localization.GetString(KpWordKey(Kp)));

    public string EnergyText => _localization.Format("Aurora_Value_Energy", Number(EnergyKeV, EnergyKeV < 1 ? "0.0" : "0"));

    /// <summary>The energy slider, logarithmic: 0.3 keV (−0.52) to 30 keV (1.48).</summary>
    public double EnergyLog
    {
        get => Math.Log10(EnergyKeV);
        set => EnergyKeV = Math.Round(Math.Pow(10, Math.Clamp(value, -0.52, 1.48)), 2);
    }

    partial void OnKpChanged(double value)
    {
        var chip = KpChoice.Selected;
        if (chip.Length > 0 && Math.Abs(double.Parse(chip, CultureInfo.InvariantCulture) - value) > 0.01) KpChoice.Selected = string.Empty;
        Refresh(force: true);
    }

    partial void OnEnergyKeVChanged(double value) => Refresh(force: true);

    partial void OnSkyHourChanged(double value) => Refresh();

    private void OnKpChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && KpChoice.Selected.Length > 0) Kp = double.Parse(KpChoice.Selected, CultureInfo.InvariantCulture);
    }

    private static string KpWordKey(double kp) => kp switch
    {
        < 3.5 => "Aurora_Kp_Quiet",
        < 4.5 => "Aurora_Kp_Active",
        < 6.5 => "Aurora_Kp_Storm",
        < 8.5 => "Aurora_Kp_Strong",
        _ => "Aurora_Kp_Extreme",
    };

    /// <summary>The moment shown.</summary>
    public DateTime SkyUtc => DateTime.SpecifyKind(SkyBase, DateTimeKind.Utc).AddHours(SkyHour);

    /// <summary>Counts the night's hours from the local noon before <paramref name="utc"/> at the shared location, and goes there.</summary>
    private void SetNight(DateTime utc)
    {
        var zone = _location.Current.TimeZone;
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        var noon = local.Date.AddHours(12);
        if (local < noon) noon = noon.AddDays(-1);
        SkyBase = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(noon, DateTimeKind.Unspecified), zone);
        SkyHour = Math.Round(Math.Clamp((utc - SkyBase).TotalHours, 0, 24), 4);
    }

    /// <summary>Back to now (and paused).</summary>
    [RelayCommand]
    private void Now()
    {
        SetNight(DateTime.UtcNow);
        IsPlaying = false;
    }

    /// <summary>The darkest hour of this night nearest magnetic midnight, when the oval comes closest (and paused).</summary>
    [RelayCommand]
    private void BestHour()
    {
        SkyHour = BestHourOfNight();
        IsPlaying = false;
    }

    /// <summary>The hour of the night (0–24 from the noon before) when the curtain stands highest in a dark sky.</summary>
    internal double BestHourOfNight()
    {
        var best = 12.0;
        var bestScore = double.NegativeInfinity;
        var top = AuroraModel.VisibleTop(EnergyKeV);
        for (var h = 0.0; h <= 24; h += 0.25)
        {
            var g = AuroraDiagram.Geometry(DateTime.SpecifyKind(SkyBase, DateTimeKind.Utc).AddHours(h), _location.Current.Latitude, _location.Current.Longitude, Kp);
            if (g.SunAltitude > -12) continue;
            var score = AuroraModel.Elevation(top, g.CurtainKm) + (10 * g.Brightness);
            if (score > bestScore)
            {
                bestScore = score;
                best = h;
            }
        }

        return best;
    }

    /// <summary>The night held at an hour (and paused): for the walkthroughs.</summary>
    internal void HoldHour(double hour)
    {
        SkyHour = Math.Clamp(hour, 0, 24);
        IsPlaying = false;
    }

    public double Latitude => _location.Current.Latitude;

    public double Longitude => _location.Current.Longitude;

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        SetNight(SkyUtc);
        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(Longitude));
        OnPropertyChanged(nameof(Subtitle));
        Refresh(force: true);
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
        var key = Mode switch
        {
            AuroraMode.Shield => $"S|{CycleStage}|{Math.Floor(CyclePhase * 20)}",
            AuroraMode.Particles => "P",
            _ => $"K|{Math.Floor(SkyHour * 12)}",
        };
        if (!force && key == _shownKey) return;
        _shownKey = key;
        OnPropertyChanged(nameof(CycleStage));

        switch (Mode)
        {
            case AuroraMode.Particles:
                RefreshParticles();
                break;

            case AuroraMode.Sky:
                RefreshSky();
                break;

            default:
                RefreshShield();
                break;
        }
    }

    private void RefreshShield()
    {
        var stage = CycleStage;
        ClockText = _localization.GetString($"Aurora_Cycle_{stage}");
        PhaseTitle = _localization.GetString($"Aurora_Stage_{stage}_Title");
        PhaseDescription = _localization.GetString($"Aurora_Stage_{stage}_Text");
        PhaseBrush = stage switch
        {
            "Snap" or "Return" => GreenBrush,
            "Open" or "Sink" => GoldBrush,
            "NoField" => RedBrush,
            _ => CyanBrush,
        };

        var pressure = AuroraModel.DynamicPressure(WindDensity, WindSpeed);
        var nose = AuroraModel.Standoff(pressure, Bz);
        var shock = AuroraModel.BowShockStandoff(nose, AuroraModel.MachNumber(WindDensity, WindSpeed, Bz));
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Aurora_Stat_Wind"), _localization.Format("Aurora_Stat_Wind_Value", Number(WindSpeed, "0"), Number(WindDensity, "0.#"))),
            new(_localization.GetString("Aurora_Stat_Pressure"), _localization.Format("Aurora_Stat_Pressure_Value", Number(pressure, "0.0"))),
            new(_localization.GetString("Aurora_Stat_Field"), BzText),
        };

        if (NoField)
        {
            stats.Add(new(_localization.GetString("Aurora_Stat_Shield"), _localization.GetString("Aurora_Stat_Shield_None")));
        }
        else
        {
            stats.Add(new(_localization.GetString("Aurora_Stat_Nose"), _localization.Format("Aurora_Stat_Distance_Value", Number(nose, "0.0"), Thousands(nose * AuroraModel.EarthRadiusKm))));
            stats.Add(new(_localization.GetString("Aurora_Stat_Shock"), _localization.Format("Aurora_Stat_Distance_Value", Number(shock, "0.0"), Thousands(shock * AuroraModel.EarthRadiusKm))));
            if (nose < AuroraModel.Geostationary) stats.Add(new(_localization.GetString("Aurora_Stat_Geo"), _localization.GetString("Aurora_Stat_Geo_Inside")));
        }

        stats.Add(new(_localization.GetString("Aurora_Stat_Kp"), KpText));
        stats.Add(new(_localization.GetString("Aurora_Stat_Arrival"), _localization.Format("Aurora_Stat_Arrival_Value", Number(149.6e6 / WindSpeed / 86400, "0.0"))));
        Stats = stats;
    }

    private void RefreshParticles()
    {
        var motion = AuroraModel.Motion(Species, ParticleEnergy, Shell, PitchAngle);
        var lost = IsInLossCone;
        var name = _localization.GetString(Species == AuroraSpecies.Electron ? "Aurora_Species_Electron" : "Aurora_Species_Proton");
        ClockText = string.Empty;
        PhaseTitle = _localization.GetString(lost ? "Aurora_Trap_Lost_Title" : "Aurora_Trap_Held_Title");
        PhaseDescription = _localization.GetString(lost ? "Aurora_Trap_Lost_Text" : Species == AuroraSpecies.Electron ? "Aurora_Trap_Held_Electron" : "Aurora_Trap_Held_Proton");
        PhaseBrush = lost ? GreenBrush : Species == AuroraSpecies.Electron ? CyanBrush : GoldBrush;
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Aurora_Stat_Particle"), _localization.Format("Aurora_Stat_Particle_Value", name, EnergyName(ParticleEnergy))),
            new(_localization.GetString("Aurora_Stat_Speed"), _localization.Format("Aurora_Stat_Speed_Value", Thousands(motion.Speed), Number(motion.Speed / 2997.92458, "0.#"))),
            new(_localization.GetString("Aurora_Stat_Gyro"), Duration(motion.GyroPeriod)),
            new(_localization.GetString("Aurora_Stat_Bounce"), Duration(motion.BouncePeriod)),
            new(_localization.GetString("Aurora_Stat_Drift"), _localization.Format(Species == AuroraSpecies.Electron ? "Aurora_Stat_Drift_East" : "Aurora_Stat_Drift_West", Duration(motion.DriftPeriod))),
            new(_localization.GetString("Aurora_Stat_Mirror"), lost
                ? _localization.GetString("Aurora_Stat_Mirror_Air")
                : _localization.Format("Aurora_Stat_Mirror_Value", Number(motion.MirrorLatitude, "0"))),
            new(_localization.GetString("Aurora_Stat_Cone"), _localization.Format("Aurora_Stat_Cone_Value", Number(motion.LossCone, "0.0"), Number(PitchAngle, "0"))),
            new(_localization.GetString("Aurora_Stat_FieldHere"), _localization.Format("Aurora_Stat_FieldHere_Value", Number(AuroraModel.EquatorialField / Math.Pow(Shell, 3), "0"))),
        };
        Stats = stats;
    }

    private void RefreshSky()
    {
        var utc = SkyUtc;
        var place = _location.Current;
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, place.TimeZone);
        ClockText = _localization.Format("Aurora_Clock", local.ToString("HH:mm", CultureInfo.InvariantCulture), place.Name);
        var g = AuroraDiagram.Geometry(utc, place.Latitude, place.Longitude, Kp);
        var visibleTop = AuroraModel.VisibleTop(EnergyKeV);
        var top = AuroraModel.Elevation(visibleTop, Math.Max(40, g.CurtainKm));
        var bottom = AuroraModel.Elevation(Math.Max(75, AuroraModel.PeakHeight(EnergyKeV) - 10), Math.Max(40, g.CurtainKm));
        var inside = Math.Abs(g.Mlat) >= g.Equatorward && Math.Abs(g.Mlat) <= g.Poleward;
        var state = g.SunAltitude > -6 ? "Day" : inside ? "Overhead" : bottom > 3 ? "High" : top > 0 ? "Low" : "Hidden";
        PhaseTitle = _localization.GetString($"Aurora_See_{state}_Title");
        PhaseDescription = _localization.GetString($"Aurora_See_{state}_Text");
        PhaseBrush = state switch
        {
            "Overhead" or "High" => GreenBrush,
            "Low" => GoldBrush,
            "Day" => GreyBrush,
            _ => RedBrush,
        };

        var needed = AuroraModel.KpToSee(g.Mlat, visibleTop);
        Stats =
        [
            new(_localization.GetString("Aurora_Stat_Place"), place.Name),
            new(_localization.GetString("Aurora_Stat_Mlat"), _localization.Format("Aurora_Stat_Mlat_Value", Number(Math.Abs(g.Mlat), "0.0"), _localization.GetString(g.Mlat >= 0 ? "Aurora_North" : "Aurora_South"))),
            new(_localization.GetString("Aurora_Stat_Mlt"), _localization.Format("Aurora_Stat_Mlt_Value", Clock(g.Mlt))),
            new(_localization.GetString("Aurora_Stat_Kp"), KpText),
            new(_localization.GetString("Aurora_Stat_Edge"), _localization.Format("Aurora_Stat_Edge_Value", Number(g.Equatorward, "0.0"))),
            new(_localization.GetString("Aurora_Stat_ToCurtain"), inside
                ? _localization.GetString("Aurora_Stat_ToCurtain_Inside")
                : _localization.Format("Aurora_Stat_ToCurtain_Value", Thousands(g.CurtainKm))),
            new(_localization.GetString("Aurora_Stat_Top"), top > 0
                ? _localization.Format("Aurora_Stat_Top_Value", Number(visibleTop, "0"), Number(top, "0"))
                : _localization.Format("Aurora_Stat_Top_Below", Number(visibleTop, "0"), Number(-top, "0"))),
            new(_localization.GetString("Aurora_Stat_Sun"), _localization.Format("Aurora_Stat_Sun_Value", Number(g.SunAltitude, "0"))),
            new(_localization.GetString("Aurora_Stat_Needed"), needed <= 9
                ? _localization.Format("Aurora_Stat_Needed_Value", Number(Math.Ceiling(needed * 3) / 3, "0.#"))
                : _localization.GetString("Aurora_Stat_Needed_Beyond")),
        ];
    }

    /// <summary>A duration in the handiest unit: µs, ms, s, minutes or hours.</summary>
    private string Duration(double seconds) => seconds switch
    {
        < 1e-3 => _localization.Format("Aurora_Unit_Us", Number(seconds * 1e6, seconds < 1e-5 ? "0.0" : "0")),
        < 1 => _localization.Format("Aurora_Unit_Ms", Number(seconds * 1e3, seconds < 1e-2 ? "0.0" : "0")),
        < 120 => _localization.Format("Aurora_Unit_S", Number(seconds, seconds < 10 ? "0.0" : "0")),
        < 7200 => _localization.Format("Aurora_Unit_Min", Number(seconds / 60, "0")),
        _ => _localization.Format("Aurora_Unit_H", Number(seconds / 3600, seconds < 36000 ? "0.0" : "0")),
    };

    private string EnergyName(double keV) => keV >= 1000
        ? _localization.Format("Aurora_Value_MeV", Number(keV / 1000, "0.#"))
        : _localization.Format("Aurora_Value_Energy", Number(keV, "0.#"));

    private static string Clock(double hours)
    {
        var minutes = (int)Math.Round(hours * 60) % 1440;
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    private static string Thousands(double value) => Math.Round(value).ToString("#,0", CultureInfo.InvariantCulture);

    #endregion

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(ChartCaption));
        OnPropertyChanged(nameof(ChartNote));
        OnPropertyChanged(nameof(WindSpeedText));
        OnPropertyChanged(nameof(WindDensityText));
        OnPropertyChanged(nameof(BzText));
        OnPropertyChanged(nameof(ShellText));
        OnPropertyChanged(nameof(PitchText));
        OnPropertyChanged(nameof(KpText));
        OnPropertyChanged(nameof(EnergyText));
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        WindChoice.PropertyChanged -= OnWindChosen;
        SpeciesChoice.PropertyChanged -= OnSpeciesChosen;
        EnergyChoice.PropertyChanged -= OnParticleEnergyChosen;
        KpChoice.PropertyChanged -= OnKpChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
