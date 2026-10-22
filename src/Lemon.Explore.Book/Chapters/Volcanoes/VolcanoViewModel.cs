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

namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>
/// The volcanoes and rocks page: which view is shown and whether time runs; the eruption's magma (silica, water, or a real
/// volcano's); the grain followed round the rock cycle; and the magnifier's cooling magma, tank of settling mud and squeezed
/// rock; and what is happening. The diagram (<see cref="VolcanoDiagram"/>) runs the clock and the simulations. <see cref="Lesson"/>
/// is the spoken walkthrough of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class VolcanoViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush SandBrush = Frozen(0xD9, 0xBE, 0x84);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>Brings the card up to date a few times a second: the eruption's phase, the cooling and the sinking grains move on their own.</summary>
    private readonly Avalonia.Threading.DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(0.4) };

    private string _state = string.Empty;
    private bool _settingPreset;

    public VolcanoViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => VolcanoLessons.For(this));
        Card = new ObjectCard(narration, Lesson, VolcanoDiagram.ObjectTitleKey, VolcanoDiagram.ObjectClipKey);
        _crystals = NewCrystals();
        PresetChoice.PropertyChanged += OnPresetChosen;
        JourneyChoice.PropertyChanged += OnJourneyChosen;
        ProcessChoice.PropertyChanged += OnProcessChosen;
        MagmaChoice.PropertyChanged += OnMagmaChosen;
        ParentChoice.PropertyChanged += OnParentChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        _refresh.Tick += OnRefreshTick;
        _refresh.Start();
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
    [NotifyPropertyChangedFor(nameof(IsEruption), nameof(IsCycle), nameof(IsMagnifier), nameof(Subtitle), nameof(ScaleNote), nameof(Hint),
        nameof(Facts), nameof(ChartCaption), nameof(ChartNote), nameof(HasTimeline))]
    private VolcanoMode _mode;

    public bool IsEruption
    {
        get => Mode == VolcanoMode.Eruption;
        set
        {
            if (value) Mode = VolcanoMode.Eruption;
        }
    }

    public bool IsCycle
    {
        get => Mode == VolcanoMode.Cycle;
        set
        {
            if (value) Mode = VolcanoMode.Cycle;
        }
    }

    public bool IsMagnifier
    {
        get => Mode == VolcanoMode.Magnifier;
        set
        {
            if (value) Mode = VolcanoMode.Magnifier;
        }
    }

    /// <summary>The view's name in its resource keys: Eruption, Cycle, or the magnifier's process (Cooling, Settling, Squeezing).</summary>
    private string ViewKey => Mode == VolcanoMode.Magnifier ? Process.ToString() : Mode.ToString();

    public string Subtitle => _localization.GetString($"Volc_{ViewKey}_Subtitle");

    public string ScaleNote => _localization.GetString($"Volc_{ViewKey}_Note");

    public string Hint => _localization.GetString($"Volc_Hint_{ViewKey}");

    public string ChartCaption => _localization.GetString($"Volc_Chart_{ViewKey}_Caption");

    public string ChartNote => _localization.GetString($"Volc_Chart_{ViewKey}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Volc_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(VolcanoMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Restart();
        Refresh();
    }

    /// <summary>The spoken walkthrough of the current view.</summary>
    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a click or a drag before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    /// <summary>Whether time runs (the diagram stops it at the end of a journey or a cooling).</summary>
    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>How far on, 0 to 1: through the eruption's round, the grain's journey or the cooling.</summary>
    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _showLabels = true;

    [ObservableProperty]
    private bool _showLevels = true;

    /// <summary>Whether the toolbar's timeline means something: the round, a followed grain's journey, the cooling.</summary>
    public bool HasTimeline => Mode == VolcanoMode.Eruption || (Mode == VolcanoMode.Cycle && JourneyId is not null)
        || (Mode == VolcanoMode.Magnifier && Process == MagnifierProcess.Cooling);

    /// <summary>Play or pause; at the end of a journey or a cooling, play starts it again.</summary>
    [RelayCommand]
    private void TogglePlay()
    {
        if (!IsPlaying && Progress >= 1 && Mode != VolcanoMode.Eruption)
        {
            Restart();
            return;
        }

        IsPlaying = !IsPlaying;
    }

    /// <summary>From the start again: a new round, the journey from its first stop, fresh magma.</summary>
    [RelayCommand]
    public void Restart()
    {
        Progress = 0;
        IsPlaying = true;
        if (Mode == VolcanoMode.Magnifier && Process == MagnifierProcess.Cooling) Crystals = NewCrystals();
    }

    partial void OnProgressChanged(double value)
    {
        // Going back on the cooling replays the same magma from the start to there.
        if (Mode == VolcanoMode.Magnifier && Process == MagnifierProcess.Cooling && (int)Math.Round(value * CrystalSim.Steps) < Crystals.Step)
        {
            var fresh = NewCrystals();
            fresh.AdvanceTo((int)Math.Round(value * CrystalSim.Steps));
            Crystals = fresh;
        }

        RefreshSoon();
    }

    #endregion

    #region the eruption

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SilicaText), nameof(Style))]
    private double _silica = VolcanoModel.Preset("Kilauea").Silica;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaterText), nameof(Style))]
    private double _water = VolcanoModel.Preset("Kilauea").Water;

    public OscillationChoice PresetChoice { get; } = new("Kilauea");

    public string SilicaText => _localization.Format("Volc_Value_SilicaRock", Number(Silica, "0"), _localization.GetString($"Volc_State_{VolcanoModel.RockName(Silica)}"));

    public string WaterText => _localization.Format("Volc_Value_Percent", Number(Water, "0.0"));

    public EruptionStyle Style => VolcanoModel.StyleOf(Silica, Water);

    partial void OnSilicaChanged(double value) => OnMagmaChanged();

    partial void OnWaterChanged(double value) => OnMagmaChanged();

    private void OnMagmaChanged()
    {
        // Moved by hand: no longer a real volcano's magma.
        if (!_settingPreset) PresetChoice.Selected = string.Empty;
        RefreshSoon(force: true);
    }

    /// <summary>A real volcano's magma.</summary>
    public void UsePreset(string id)
    {
        var preset = VolcanoModel.Preset(id);
        _settingPreset = true;
        Silica = preset.Silica;
        Water = preset.Water;
        PresetChoice.Selected = preset.Id;
        _settingPreset = false;
        RefreshSoon(force: true);
    }

    private void OnPresetChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && !_settingPreset && PresetChoice.Selected.Length > 0) UsePreset(PresetChoice.Selected);
    }

    /// <summary>Straight to the eruption.</summary>
    [RelayCommand]
    private void EruptNow()
    {
        Mode = VolcanoMode.Eruption;
        Progress = VolcanoModel.EruptionStart;
        IsPlaying = true;
    }

    /// <summary>Holds the round at <paramref name="progress"/>, paused (walkthrough).</summary>
    public void HoldRound(double progress)
    {
        IsPlaying = false;
        Progress = progress;
    }

    /// <summary>Runs the round from <paramref name="progress"/> (walkthrough).</summary>
    public void RunRound(double progress)
    {
        Progress = progress;
        IsPlaying = true;
    }

    #endregion

    #region the rock cycle

    /// <summary>The grain followed round the cycle, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTimeline), nameof(Journey))]
    private string? _journeyId;

    public OscillationChoice JourneyChoice { get; } = new(string.Empty);

    public Journey? Journey => JourneyId is null ? null : VolcanoModel.JourneyOf(JourneyId);

    partial void OnJourneyIdChanged(string? value)
    {
        JourneyChoice.Selected = value ?? string.Empty;
        Progress = 0;
        IsPlaying = value is not null;
        RefreshSoon(force: true);
    }

    private void OnJourneyChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OscillationChoice.Selected)) return;
        var id = JourneyChoice.Selected.Length > 0 ? JourneyChoice.Selected : null;
        if (id != JourneyId) JourneyId = id;
    }

    /// <summary>Follows a grain from its first stop (or the same grain again from the start).</summary>
    public void Follow(string id)
    {
        if (JourneyId == id)
        {
            Progress = 0;
            IsPlaying = true;
        }
        else
        {
            JourneyId = id;
        }
    }

    [RelayCommand]
    private void StopFollowing() => JourneyId = null;

    /// <summary>Holds the followed grain just arrived at stop <paramref name="step"/>, paused (walkthrough).</summary>
    public void HoldAtStop(string id, int step)
    {
        Follow(id);
        var journey = VolcanoModel.JourneyOf(id);
        IsPlaying = false;
        Progress = Math.Clamp(step / (double)Math.Max(1, journey.Steps.Count - 1), 0, 1);
    }

    #endregion

    #region the magnifier

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCooling), nameof(IsSettling), nameof(IsSqueezing), nameof(Subtitle), nameof(ScaleNote), nameof(Hint),
        nameof(ChartCaption), nameof(ChartNote), nameof(HasTimeline))]
    private MagnifierProcess _process;

    public OscillationChoice ProcessChoice { get; } = new(nameof(MagnifierProcess.Cooling));

    public bool IsCooling => Process == MagnifierProcess.Cooling;

    public bool IsSettling => Process == MagnifierProcess.Settling;

    public bool IsSqueezing => Process == MagnifierProcess.Squeezing;

    partial void OnProcessChanged(MagnifierProcess value)
    {
        ProcessChoice.Selected = value.ToString();
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Restart();
        Refresh();
    }

    private void OnProcessChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<MagnifierProcess>(ProcessChoice.Selected, out var process)) Process = process;
    }

    /// <summary>Cooling: the magma.</summary>
    [ObservableProperty]
    private MagmaKind _coolMagma = MagmaKind.Felsic;

    public OscillationChoice MagmaChoice { get; } = new(nameof(MagmaKind.Felsic));

    /// <summary>Cooling: log₁₀ of the cooling rate in °C a year (−3 deep in a pluton … 7.5 quenched in water).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoolRateText))]
    private double _coolRate = -3;

    [ObservableProperty]
    private bool _gas;

    [ObservableProperty]
    private bool _twoStage;

    public string CoolRateText => RateText(CoolRate);

    /// <summary>Cooling: the slice of magma cooling now (replaced when a setting changes or the timeline goes back).</summary>
    [ObservableProperty]
    private CrystalSim _crystals;

    partial void OnCoolMagmaChanged(MagmaKind value)
    {
        MagmaChoice.Selected = value.ToString();
        CoolAgain();
    }

    partial void OnCoolRateChanged(double value) => CoolAgain();

    partial void OnGasChanged(bool value) => CoolAgain();

    partial void OnTwoStageChanged(bool value) => CoolAgain();

    private void OnMagmaChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<MagmaKind>(MagmaChoice.Selected, out var magma)) CoolMagma = magma;
    }

    private CrystalSim NewCrystals() => new(CoolMagma, CoolRate, Gas, TwoStage);

    /// <summary>Melts the slice again and cools it from the start with the settings now.</summary>
    [RelayCommand]
    public void CoolAgain()
    {
        Crystals = NewCrystals();
        if (Mode == VolcanoMode.Magnifier && Process == MagnifierProcess.Cooling)
        {
            Progress = 0;
            IsPlaying = true;
        }

        RefreshSoon(force: true);
    }

    /// <summary>Cools a fresh slice straight to <paramref name="progress"/> and holds it there (walkthrough).</summary>
    public void CoolTo(double progress)
    {
        var fresh = NewCrystals();
        fresh.AdvanceTo((int)Math.Round(progress * CrystalSim.Steps));
        Crystals = fresh;
        IsPlaying = false;
        Progress = progress;
    }

    /// <summary>Settling: the tank the river pours into.</summary>
    public SedimentSim Tank { get; } = new();

    /// <summary>Settling: how deep the layers are buried, km.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BurialText))]
    private double _burialKm;

    public string BurialText => _localization.Format("Volc_Value_Km", Number(BurialKm, "0.0"));

    partial void OnBurialKmChanged(double value) => RefreshSoon();

    [RelayCommand]
    public void Pour()
    {
        Tank.Pour();
        IsPlaying = true;
        RefreshSoon(force: true);
    }

    [RelayCommand]
    public void EmptyTank()
    {
        Tank.Clear();
        BurialKm = 0;
        RefreshSoon(force: true);
    }

    /// <summary>Lets the grains in the tank settle at once (walkthrough steps that talk about the finished layers).</summary>
    public void SettleNow()
    {
        for (var i = 0; i < 800 && Tank.Falling.Count > 0; i++) Tank.Step(0.1);
        RefreshSoon(force: true);
    }

    /// <summary>Squeezing: the rock buried.</summary>
    [ObservableProperty]
    private string _squeezeParent = "Shale";

    public OscillationChoice ParentChoice { get; } = new("Shale");

    /// <summary>Squeezing: how deep it is, km.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DepthText))]
    private double _squeezeDepthKm = 4;

    public string DepthText => _localization.Format("Volc_Value_Km", Number(SqueezeDepthKm, "0"));

    public string SqueezedRock => VolcanoModel.MetamorphicRock(SqueezeParent, SqueezeDepthKm);

    partial void OnSqueezeParentChanged(string value)
    {
        ParentChoice.Selected = value;
        RefreshSoon(force: true);
    }

    partial void OnSqueezeDepthKmChanged(double value) => RefreshSoon();

    private void OnParentChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && ParentChoice.Selected.Length > 0) SqueezeParent = ParentChoice.Selected;
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

    /// <summary>The settling tank's state: Empty, Sinking, Settled, Squeezed or Rock.</summary>
    public string TankState
    {
        get
        {
            if (Tank.Pours == 0) return "Empty";
            if (BurialKm >= 1.5) return "Rock";
            if (BurialKm >= 0.3) return "Squeezed";
            return Tank.Falling.Count > 0 ? "Sinking" : "Settled";
        }
    }

    /// <summary>What the card names now; when it changes the card is rebuilt at once.</summary>
    private string CurrentState => Mode switch
    {
        VolcanoMode.Eruption => $"{VolcanoModel.PhaseAt(Progress)}{Style}{PresetChoice.Selected}",
        VolcanoMode.Cycle => Journey is { } j ? $"{j.Id}{VolcanoDiagram.JourneyPosition(j, Progress).Step}" : "none",
        _ => Process switch
        {
            MagnifierProcess.Cooling => CrystalSim.RockName(CoolMagma, CoolRate, Gas, TwoStage) + Crystals.IsFinished,
            MagnifierProcess.Settling => TankState,
            _ => SqueezedRock + SqueezeParent,
        },
    };

    private DateTime _lastRefresh;

    private void RefreshSoon(bool force = false)
    {
        if (force || CurrentState != _state || (DateTime.UtcNow - _lastRefresh).TotalSeconds > 0.3) Refresh();
    }

    private void OnRefreshTick(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        _lastRefresh = DateTime.UtcNow;
        _state = CurrentState;
        switch (Mode)
        {
            case VolcanoMode.Cycle:
                RefreshCycle();
                break;

            case VolcanoMode.Magnifier:
                switch (Process)
                {
                    case MagnifierProcess.Settling:
                        RefreshSettling();
                        break;

                    case MagnifierProcess.Squeezing:
                        RefreshSqueezing();
                        break;

                    default:
                        RefreshCooling();
                        break;
                }

                break;

            default:
                RefreshEruption();
                break;
        }
    }

    private void RefreshEruption()
    {
        var phase = VolcanoModel.PhaseAt(Progress);
        var style = Style;
        if (phase == EruptionPhase.Erupting)
        {
            PhaseTitle = _localization.GetString($"Volc_Style_{style}");
            PhaseDescription = _localization.GetString($"Volc_StyleText_{style}");
            PhaseBrush = VolcanoModel.IsExplosive(style) ? RedBrush : OrangeBrush;
        }
        else
        {
            PhaseTitle = _localization.GetString($"Volc_Phase_{phase}_Title");
            PhaseDescription = _localization.GetString($"Volc_Phase_{phase}_Text");
            PhaseBrush = phase == EruptionPhase.Calm ? CyanBrush : GoldBrush;
        }

        var logEta = VolcanoModel.LogViscosity(Silica);
        var sat = VolcanoModel.SaturationDepth(Silica, Water);
        var frag = VolcanoModel.FragmentationDepth(Silica, Water);
        var height = VolcanoModel.EruptionHeightKm(style, Water);
        var heightText = style switch
        {
            EruptionStyle.Dome => _localization.GetString("Volc_Stat_DomeGrows"),
            EruptionStyle.Hawaiian or EruptionStyle.Strombolian => _localization.Format("Volc_Value_Metres", Number(height * 1000, "0")),
            _ => _localization.Format("Volc_Value_Km", Number(height, "0")),
        };
        var shapeKey = VolcanoModel.Shape(Silica, Water) switch
        {
            ConeShape.Shield => "Volc_Label_Shield",
            ConeShape.Caldera => "Volc_Label_Caldera",
            _ => "Volc_Label_Strato",
        };
        Stats =
        [
            new(_localization.GetString("Volc_Stat_Magma"), SilicaText),
            new(_localization.GetString("Volc_Stat_Temperature"), _localization.Format("Volc_Value_Celsius", Number(VolcanoModel.Temperature(Silica), "0"))),
            new(_localization.GetString("Volc_Stat_Viscosity"), _localization.Format("Volc_Value_Viscosity", Number(logEta, "0.0"),
                _localization.GetString($"Volc_Like_{VolcanoModel.ViscosityLike(logEta)}"))),
            new(_localization.GetString("Volc_Stat_Water"), WaterText),
            new(_localization.GetString("Volc_Stat_Bubbles"), sat < 0.05
                ? _localization.GetString("Volc_Stat_NearVent")
                : _localization.Format("Volc_Value_KmDeep", Number(sat, "0.0"))),
            new(_localization.GetString("Volc_Stat_Frag"), VolcanoModel.IsExplosive(style) && frag is { } f
                ? _localization.Format("Volc_Value_MetresDeep", Number(f * 1000, "0"))
                : _localization.GetString(style == EruptionStyle.Dome ? "Volc_Stat_GasLeaks" : "Volc_Stat_GasEscapes")),
            new(_localization.GetString(style switch
            {
                EruptionStyle.Hawaiian => "Volc_Stat_Fountain",
                EruptionStyle.Strombolian => "Volc_Stat_Bursts",
                EruptionStyle.Dome => "Volc_Obj_Dome",
                _ => "Volc_Stat_Height",
            }), heightText),
            new(_localization.GetString("Volc_Stat_Pressure"), _localization.Format("Volc_Value_Mpa", Number(VolcanoModel.Overpressure(Progress), "0"))),
            new(_localization.GetString("Volc_Stat_Shape"), _localization.GetString(shapeKey)),
        ];
    }

    private void RefreshCycle()
    {
        if (Journey is not { } journey)
        {
            PhaseTitle = _localization.GetString("Volc_Cycle_Title");
            PhaseDescription = _localization.GetString("Volc_Cycle_Text");
            PhaseBrush = GoldBrush;
            Stats =
            [
                new(_localization.GetString("Volc_Family_Igneous"), _localization.GetString("Volc_CycleStat_Igneous")),
                new(_localization.GetString("Volc_Family_Sedimentary"), _localization.GetString("Volc_CycleStat_Sedimentary")),
                new(_localization.GetString("Volc_Family_Metamorphic"), _localization.GetString("Volc_CycleStat_Metamorphic")),
            ];
            return;
        }

        var (step, along) = VolcanoDiagram.JourneyPosition(journey, Progress);
        var arrived = along > 0.92 || Progress >= 1;
        var shown = arrived ? step : step - 1;
        var stop = journey.Steps[shown];
        PhaseTitle = shown == 0
            ? _localization.GetString($"Volc_Journey_{journey.Id}")
            : _localization.GetString($"Volc_Process_{stop.Process}");
        PhaseDescription = _localization.GetString($"Volc_Step_{journey.Id}_{shown}");
        PhaseBrush = VolcanoModel.FamilyOf(stop.State) switch
        {
            RockFamily.Igneous => OrangeBrush,
            RockFamily.Sedimentary => SandBrush,
            RockFamily.Metamorphic => VioletBrush,
            _ => CyanBrush,
        };
        var family = VolcanoModel.FamilyOf(stop.State) is { } f
            ? _localization.GetString($"Volc_Family_{f}")
            : _localization.GetString(stop.State is "Magma" or "Lava" ? "Volc_Family_Magma" : "Volc_Family_Sediment");
        Stats =
        [
            new(_localization.GetString("Volc_Stat_Now"), _localization.GetString($"Volc_State_{stop.State}")),
            new(_localization.GetString("Volc_Stat_Family"), family),
            new(_localization.GetString("Volc_Stat_Step"), _localization.Format("Volc_Value_StepOf", shown + 1, journey.Steps.Count)),
            new(_localization.GetString("Volc_Stat_StepTime"), shown == 0 ? "—" : VolcanoFormat.Years(stop.Years)),
            new(_localization.GetString("Volc_Stat_SoFar"), shown == 0 ? "—" : VolcanoFormat.Years(journey.YearsTo(shown))),
        ];
    }

    private void RefreshCooling()
    {
        var sim = Crystals;
        var rock = CrystalSim.RockName(CoolMagma, CoolRate, Gas, TwoStage);
        PhaseTitle = sim.IsFinished
            ? _localization.GetString($"Volc_State_{rock}")
            : _localization.Format("Volc_Cooling_Becoming", _localization.GetString($"Volc_State_{rock}"));
        PhaseDescription = _localization.GetString($"Volc_RockText_{rock}");
        PhaseBrush = sim.IsFinished ? GreenBrush : OrangeBrush;
        Stats =
        [
            new(_localization.GetString("Volc_Stat_Magma"), _localization.GetString($"Volc_Magma_{CoolMagma}")),
            new(_localization.GetString("Volc_Stat_CoolRate"), TwoStage ? _localization.GetString("Volc_Stat_TwoStage") : RateText(CoolRate)),
            new(_localization.GetString("Volc_Stat_CoolTime"), TwoStage ? "—" : DurationText(CrystalSim.CoolingYears(CoolMagma, CoolRate))),
            new(_localization.GetString("Volc_Stat_Temperature"), _localization.Format("Volc_Value_Celsius", Number(sim.Temperature, "0"))),
            new(_localization.GetString("Volc_Stat_Crystals"), sim.CrystalCount.ToString("#,##0", CultureInfo.InvariantCulture)),
            new(_localization.GetString("Volc_Stat_Size"), _localization.Format("Volc_Value_Mm", Number(sim.MeanSizeMm, sim.MeanSizeMm < 0.1 ? "0.00" : "0.0"))),
            new(_localization.GetString("Volc_Stat_Glass"), _localization.Format("Volc_Value_Percent", Number(sim.GlassShare * 100, "0"))),
            new(_localization.GetString("Volc_Stat_BubbleShare"), _localization.Format("Volc_Value_Percent", Number(sim.BubbleShare * 100, "0"))),
        ];
    }

    private void RefreshSettling()
    {
        var state = TankState;
        PhaseTitle = _localization.GetString($"Volc_Tank_{state}_Title");
        PhaseDescription = _localization.GetString($"Volc_Tank_{state}_Text");
        PhaseBrush = state switch
        {
            "Rock" => GoldBrush,
            "Squeezed" => VioletBrush,
            "Sinking" => CyanBrush,
            _ => SandBrush,
        };
        var sinking = Tank.Falling.Count;
        Stats =
        [
            new(_localization.GetString("Volc_Stat_Layers"), Tank.Pours.ToString(CultureInfo.InvariantCulture)),
            new(_localization.GetString("Volc_Stat_Sinking"), sinking.ToString("#,##0", CultureInfo.InvariantCulture)),
            new(_localization.GetString("Volc_Stat_Burial"), BurialText),
            new(_localization.GetString("Volc_Stat_PoreSand"), _localization.Format("Volc_Value_Percent", Number(VolcanoModel.Porosity(false, BurialKm) * 100, "0"))),
            new(_localization.GetString("Volc_Stat_PoreMud"), _localization.Format("Volc_Value_Percent", Number(VolcanoModel.Porosity(true, BurialKm) * 100, "0"))),
            new(_localization.GetString("Volc_Stat_Cement"), _localization.Format("Volc_Value_Percent", Number(VolcanoModel.Cement(BurialKm) * 100, "0"))),
            new(_localization.GetString("Volc_Stat_Thickness"), _localization.Format("Volc_Value_Percent", Number(VolcanoModel.Thickness(true, BurialKm) * 100, "0"))),
        ];
    }

    private void RefreshSqueezing()
    {
        var rock = SqueezedRock;
        PhaseTitle = _localization.GetString($"Volc_State_{rock}");
        PhaseDescription = _localization.GetString($"Volc_RockText_{rock}");
        PhaseBrush = VolcanoModel.FamilyOf(rock) == RockFamily.Metamorphic ? VioletBrush : SandBrush;
        var kbar = VolcanoModel.KilobarsAt(SqueezeDepthKm);
        Stats =
        [
            new(_localization.GetString("Volc_Stat_Depth"), DepthText),
            new(_localization.GetString("Volc_Stat_Temperature"), _localization.Format("Volc_Value_Celsius", Number(VolcanoModel.TemperatureAt(SqueezeDepthKm), "0"))),
            new(_localization.GetString("Volc_Stat_RockPressure"), _localization.Format("Volc_Value_Kbar", Number(kbar, "0.0"), Number(kbar * 987, "#,##0"))),
            new(_localization.GetString("Volc_Stat_Parent"), _localization.GetString($"Volc_State_{SqueezeParent}")),
            new(_localization.GetString("Volc_Stat_Family"), _localization.GetString(VolcanoModel.FamilyOf(rock) == RockFamily.Metamorphic ? "Volc_Family_Metamorphic" : "Volc_Family_Sedimentary")),
        ];
    }

    #endregion

    #region formatting

    /// <summary>A cooling rate in words: "1 °C every 1,000 years", "9 °C an hour", "1 °C a second".</summary>
    internal string RateText(double logRate)
    {
        var perYear = Math.Pow(10, logRate);
        if (perYear < 1) return _localization.Format("Volc_Rate_Every", VolcanoFormat.Years(1 / perYear, about: false));
        if (perYear < 365) return _localization.Format("Volc_Rate_Year", Rounded(perYear));
        if (perYear < 8760) return _localization.Format("Volc_Rate_Day", Rounded(perYear / 365));
        if (perYear < 525_600) return _localization.Format("Volc_Rate_Hour", Rounded(perYear / 8760));
        if (perYear < 31_536_000) return _localization.Format("Volc_Rate_Minute", Rounded(perYear / 525_600));
        return _localization.Format("Volc_Rate_Second", Rounded(perYear / 31_536_000));
    }

    /// <summary>How long a cooling takes: years, else days, hours, minutes.</summary>
    internal string DurationText(double years)
    {
        if (years >= 1) return VolcanoFormat.Years(years);
        var days = years * 365;
        if (days >= 1) return _localization.Format("Volc_Duration_Days", Rounded(days));
        var hours = days * 24;
        if (hours >= 1) return _localization.Format("Volc_Duration_Hours", Rounded(hours));
        return _localization.Format("Volc_Duration_Minutes", Rounded(Math.Max(1, hours * 60)));
    }

    /// <summary>Two significant figures at most, no more than needed: 9, 26, 270, 3,000.</summary>
    private static string Rounded(double value)
    {
        if (value < 10) return Number(Math.Round(value, value < 2 ? 1 : 0), "0.#");
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)) - 1);
        return Number(Math.Round(value / magnitude) * magnitude, "#,##0");
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
        OnPropertyChanged(nameof(ChartCaption));
        OnPropertyChanged(nameof(ChartNote));
        OnPropertyChanged(nameof(SilicaText));
        OnPropertyChanged(nameof(WaterText));
        OnPropertyChanged(nameof(CoolRateText));
        OnPropertyChanged(nameof(BurialText));
        OnPropertyChanged(nameof(DepthText));
        Refresh();
    }

    public void Dispose()
    {
        _refresh.Stop();
        _refresh.Tick -= OnRefreshTick;
        Lesson.Dispose();
        Card.Dispose();
        PresetChoice.PropertyChanged -= OnPresetChosen;
        JourneyChoice.PropertyChanged -= OnJourneyChosen;
        ProcessChoice.PropertyChanged -= OnProcessChosen;
        MagmaChoice.PropertyChanged -= OnMagmaChosen;
        ParentChoice.PropertyChanged -= OnParentChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Years in the reader's language: "about 10,000 years", "about 30 million years"; "约1万年", "约3000万年".</summary>
internal static class VolcanoFormat
{
    public static string Years(double years, bool about = true)
    {
        string number;
        if (IsChinese)
        {
            number = years switch
            {
                >= 1e8 => Short(years / 1e8) + LocalizationService.Instance.GetString("Volc_Unit_Yi"),
                >= 1e4 => Short(years / 1e4) + LocalizationService.Instance.GetString("Volc_Unit_Wan"),
                _ => Short(years),
            };
        }
        else
        {
            number = years >= 1e6 ? Short(years / 1e6) + LocalizationService.Instance.GetString("Volc_Unit_Million") : Short(years);
        }

        return LocalizationService.Instance.Format(about ? "Volc_Years_About" : "Volc_Years", number);
    }

    /// <summary>A short number: 0.5, 3, 30, 3,000 (Chinese writes 3000 without the comma).</summary>
    private static string Short(double value) =>
        value.ToString(value >= 100 && !IsChinese ? "#,##0" : value >= 10 ? "0" : "0.#", CultureInfo.InvariantCulture);

    private static bool IsChinese => LocalizationService.Instance.CurrentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
}
