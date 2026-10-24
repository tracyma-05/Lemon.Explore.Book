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
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>
/// The acids and bases page: which view is shown; what is in the beaker and its indicator; the everyday liquid chosen on
/// the pH scale, how often it was watered down and the indicator it is shown in; the titration's acid, indicator,
/// strength, tap and the base run in; and what is happening. The diagram (<see cref="AcidBaseDiagram"/>) drips, pours and
/// runs the burette and reports back. <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a
/// thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class AcidBaseViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>The base runs in every frame while the tap is open; the card is rebuilt a few times a second, or when its state changes.</summary>
    private readonly BookStopwatch _sinceRefresh = BookStopwatch.StartNew();

    /// <summary>Brings the card up to date shortly after a burst of changes stops (the last change of a drag or a step).</summary>
    private readonly Avalonia.Threading.DispatcherTimer _lateRefresh = new() { Interval = TimeSpan.FromSeconds(0.3) };

    private string _state = string.Empty;

    public AcidBaseViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => AcidBaseLessons.For(this));
        Card = new ObjectCard(narration, Lesson, AcidBaseDiagram.ObjectTitleKey, AcidBaseDiagram.ObjectClipKey);
        BeakerIndicatorChoice.PropertyChanged += OnBeakerIndicatorChosen;
        ScaleIndicatorChoice.PropertyChanged += OnScaleIndicatorChosen;
        AcidChoice.PropertyChanged += OnAcidChosen;
        FlaskIndicatorChoice.PropertyChanged += OnFlaskIndicatorChosen;
        TapChoice.PropertyChanged += OnTapChosen;
        MolarChoice.PropertyChanged += OnMolarChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        _lateRefresh.Tick += OnLateRefresh;
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
    [NotifyPropertyChangedFor(nameof(IsParticles), nameof(IsScale), nameof(IsNeutralize), nameof(Subtitle), nameof(ScaleNote),
        nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private AcidBaseMode _mode;

    public bool IsParticles
    {
        get => Mode == AcidBaseMode.Particles;
        set
        {
            if (value) Mode = AcidBaseMode.Particles;
        }
    }

    public bool IsScale
    {
        get => Mode == AcidBaseMode.Scale;
        set
        {
            if (value) Mode = AcidBaseMode.Scale;
        }
    }

    public bool IsNeutralize
    {
        get => Mode == AcidBaseMode.Neutralize;
        set
        {
            if (value) Mode = AcidBaseMode.Neutralize;
        }
    }

    public string Subtitle => _localization.GetString($"Acid_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Acid_{Mode}_Note");

    public string Hint => _localization.GetString($"Acid_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Acid_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Acid_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Acid_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(AcidBaseMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Tap = TapState.Closed;
        Refresh();
    }

    /// <summary>The spoken walkthrough of the current view.</summary>
    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a click or a drag before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    [ObservableProperty]
    private bool _showLabels = true;

    [ObservableProperty]
    private bool _showParticles = true;

    #endregion

    #region the beaker

    /// <summary>What is in the beaker of the particles view.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BeakerPh))]
    private AcidBaseModel.Beaker _beaker = AcidBaseModel.Beaker.Water();

    [ObservableProperty]
    private Indicator _beakerIndicator = Indicator.None;

    public OscillationChoice BeakerIndicatorChoice { get; } = new(nameof(Indicator.None));

    public double BeakerPh => AcidBaseModel.Ph(Beaker.Solution);

    partial void OnBeakerChanged(AcidBaseModel.Beaker value) => RefreshSoon();

    partial void OnBeakerIndicatorChanged(Indicator value) => BeakerIndicatorChoice.Selected = value.ToString();

    private void OnBeakerIndicatorChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<Indicator>(BeakerIndicatorChoice.Selected, out var indicator)) BeakerIndicator = indicator;
    }

    /// <summary>A drop of a bottle landed in the beaker (the diagram's drops); water waters it down ten times.</summary>
    [RelayCommand]
    private void Drop(Reagent reagent)
    {
        if (reagent == Reagent.Water) Beaker = Beaker.DilutedTenTimes();
        else AddDrops(reagent, 1);
    }

    /// <summary><paramref name="drops"/> drops of a bottle into the beaker at once (side panel, walkthrough).</summary>
    public void AddDrops(Reagent reagent, int drops) => Beaker = Beaker.Add(reagent, drops * AcidBaseModel.DropMl);

    /// <summary>A millilitre (twenty drops) of a bottle, from the side panel.</summary>
    [RelayCommand]
    private void AddMl(Reagent reagent) => AddDrops(reagent, 20);

    [RelayCommand]
    private void DiluteBeaker() => Beaker = Beaker.DilutedTenTimes();

    /// <summary>Fresh water in the beaker.</summary>
    [RelayCommand]
    public void EmptyBeaker() => Beaker = AcidBaseModel.Beaker.Water();

    #endregion

    #region the scale

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePh))]
    private string _substanceId = "Lemon";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePh))]
    private int _dilutions;

    [ObservableProperty]
    private Indicator _scaleIndicator = Indicator.Universal;

    public OscillationChoice ScaleIndicatorChoice { get; } = new(nameof(Indicator.Universal));

    public Substance Substance => AcidBaseModel.Substance(SubstanceId);

    public double ScalePh => AcidBaseModel.DilutedPh(Substance, Dilutions);

    partial void OnSubstanceIdChanged(string value)
    {
        Dilutions = 0;
        RefreshSoon(force: true);
    }

    partial void OnDilutionsChanged(int value) => RefreshSoon(force: true);

    partial void OnScaleIndicatorChanged(Indicator value) => ScaleIndicatorChoice.Selected = value.ToString();

    private void OnScaleIndicatorChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<Indicator>(ScaleIndicatorChoice.Selected, out var indicator)) ScaleIndicator = indicator;
    }

    [RelayCommand]
    private void Dilute() => Dilutions = Math.Min(AcidBaseDiagram.MaxDilutions, Dilutions + 1);

    [RelayCommand]
    private void Undilute() => Dilutions = 0;

    #endregion

    #region the titration

    [ObservableProperty]
    private TitrationAcid _acid = TitrationAcid.Strong;

    [ObservableProperty]
    private Indicator _flaskIndicator = Indicator.Phenolphthalein;

    /// <summary>The base run in from the burette, mL.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitrationPh), nameof(BaseMlText))]
    private double _baseMl;

    public string BaseMlText => _localization.Format("Acid_Value_Ml", Number(BaseMl, "0.00"));

    [ObservableProperty]
    private TapState _tap = TapState.Closed;

    [ObservableProperty]
    private double _molar = AcidBaseModel.BottleMolar;

    [ObservableProperty]
    private bool _showCompare;

    public OscillationChoice AcidChoice { get; } = new(nameof(TitrationAcid.Strong));

    public OscillationChoice FlaskIndicatorChoice { get; } = new(nameof(Indicator.Phenolphthalein));

    public OscillationChoice TapChoice { get; } = new(nameof(TapState.Closed));

    public OscillationChoice MolarChoice { get; } = new("0.1");

    public double TitrationPh => AcidBaseModel.TitrationPh(Acid, BaseMl, Molar);

    public string Stage => AcidBaseModel.TitrationStage(Acid, BaseMl);

    partial void OnAcidChanged(TitrationAcid value)
    {
        AcidChoice.Selected = value.ToString();
        RefreshSoon(force: true);
    }

    partial void OnFlaskIndicatorChanged(Indicator value)
    {
        FlaskIndicatorChoice.Selected = value.ToString();
        RefreshSoon(force: true);
    }

    partial void OnBaseMlChanged(double value) => RefreshSoon();

    partial void OnTapChanged(TapState value) => TapChoice.Selected = value.ToString();

    partial void OnMolarChanged(double value)
    {
        MolarChoice.Selected = value.ToString("0.#", CultureInfo.InvariantCulture);
        RefreshSoon(force: true);
    }

    private void OnAcidChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<TitrationAcid>(AcidChoice.Selected, out var acid)) Acid = acid;
    }

    private void OnFlaskIndicatorChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<Indicator>(FlaskIndicatorChoice.Selected, out var indicator)) FlaskIndicator = indicator;
    }

    private void OnTapChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<TapState>(TapChoice.Selected, out var tap)) Tap = tap;
    }

    private void OnMolarChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)
            && double.TryParse(MolarChoice.Selected, NumberStyles.Float, CultureInfo.InvariantCulture, out var molar)) Molar = molar;
    }

    /// <summary>The burette filled again and the flask given fresh acid.</summary>
    [RelayCommand]
    public void Refill()
    {
        Tap = TapState.Closed;
        BaseMl = 0;
    }

    /// <summary>One drop from the burette.</summary>
    [RelayCommand]
    private void OneDrop() => BaseMl = Math.Min(AcidBaseModel.BuretteMl, Math.Round((BaseMl + AcidBaseModel.DropMl) * 100) / 100);

    /// <summary>Holds the titration at <paramref name="ml"/> of base, tap shut.</summary>
    public void HoldAt(double ml)
    {
        Tap = TapState.Closed;
        BaseMl = Math.Clamp(ml, 0, AcidBaseModel.BuretteMl);
    }

    /// <summary>Opens the tap from <paramref name="ml"/>.</summary>
    public void RunFrom(double ml, TapState tap)
    {
        BaseMl = Math.Clamp(ml, 0, AcidBaseModel.BuretteMl);
        Tap = tap;
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

    /// <summary>The beaker: "Water" (nothing added, or watered down to nearly nothing), else "Acidic", "Neutral" or "Basic".</summary>
    public string BeakerStateName
    {
        get
        {
            var b = Beaker;
            if (b.StrongAcid + b.WeakAcid + b.StrongBase + b.WeakBase < 1e-9) return "Water";
            return AcidBaseModel.Nature(BeakerPh);
        }
    }

    private string CurrentState => Mode switch
    {
        AcidBaseMode.Scale => SubstanceId + Dilutions,
        AcidBaseMode.Neutralize => Acid + Stage + FlaskIndicator,
        _ => BeakerStateName,
    };

    /// <summary>The card, at most a few times a second unless something changed that it names.</summary>
    private void RefreshSoon(bool force = false)
    {
        if (!force && CurrentState == _state && _sinceRefresh.Elapsed.TotalSeconds < 0.25)
        {
            _lateRefresh.Stop();
            _lateRefresh.Start();
            return;
        }

        Refresh();
    }

    private void OnLateRefresh(object? sender, EventArgs e)
    {
        _lateRefresh.Stop();
        Refresh();
    }

    private void Refresh()
    {
        _lateRefresh.Stop();
        _sinceRefresh.Restart();
        _state = CurrentState;
        switch (Mode)
        {
            case AcidBaseMode.Scale:
                RefreshScale();
                break;

            case AcidBaseMode.Neutralize:
                RefreshNeutralize();
                break;

            default:
                RefreshParticles();
                break;
        }
    }

    private void RefreshParticles()
    {
        var state = BeakerStateName;
        PhaseTitle = _localization.GetString($"Acid_Beaker_{state}_Title");
        PhaseDescription = _localization.GetString($"Acid_Beaker_{state}_Text");
        PhaseBrush = state switch
        {
            "Acidic" => RedBrush,
            "Basic" => VioletBrush,
            "Neutral" => GreenBrush,
            _ => CyanBrush,
        };

        var b = Beaker;
        var species = AcidBaseModel.SpeciesOf(b.Solution);
        Stats =
        [
            new(_localization.GetString("Acid_Stat_Ph"), Number(species.Ph, "0.00")),
            new(_localization.GetString("Acid_Stat_Hydronium"), _localization.Format("Acid_Value_Molar", AcidBaseDiagram.Scientific(species.Hydronium))),
            new(_localization.GetString("Acid_Stat_Hydroxide"), _localization.Format("Acid_Value_Molar", AcidBaseDiagram.Scientific(species.Hydroxide))),
            new(_localization.GetString("Acid_Stat_Product"), _localization.Format("Acid_Value_Product", AcidBaseDiagram.Scientific(species.Hydronium * species.Hydroxide))),
            new(_localization.GetString("Acid_Stat_Acids"), _localization.Format("Acid_Value_Mmol2", Number(b.StrongAcid, "0.###"), Number(b.WeakAcid, "0.###"))),
            new(_localization.GetString("Acid_Stat_Bases"), _localization.Format("Acid_Value_Mmol2", Number(b.StrongBase, "0.###"), Number(b.WeakBase, "0.###"))),
            new(_localization.GetString("Acid_Stat_Volume"), _localization.Format("Acid_Value_Ml", Number(b.VolumeMl, "0.##"))),
        ];
    }

    private void RefreshScale()
    {
        var substance = Substance;
        var ph = ScalePh;
        PhaseTitle = _localization.GetString(substance.NameKey);
        PhaseDescription = _localization.GetString(AcidBaseDiagram.ObjectClipKey("sub_" + substance.Id) ?? "Acid_Sub_Water");
        PhaseBrush = AcidBaseModel.Nature(ph) switch
        {
            "Acidic" => RedBrush,
            "Basic" => VioletBrush,
            _ => GreenBrush,
        };

        var times = Math.Pow(10, Math.Abs(7 - ph));
        var compare = times < 1.5
            ? _localization.GetString("Acid_Scale_AsWater")
            : _localization.Format(ph < 7 ? "Acid_Scale_More" : "Acid_Scale_Less", AcidBaseDiagram.TimesText(times));
        Stats =
        [
            new(_localization.GetString("Acid_Stat_Ph"), Number(ph, "0.0")),
            new(_localization.GetString("Acid_Stat_Nature"), _localization.GetString($"Acid_Nature_{AcidBaseModel.Nature(ph)}")),
            new(_localization.GetString("Acid_Stat_Hydronium"), _localization.Format("Acid_Value_Molar", AcidBaseDiagram.Scientific(Math.Pow(10, -ph)))),
            new(_localization.GetString("Acid_Stat_Compare"), compare),
            new(_localization.GetString("Acid_Stat_Kind"), _localization.GetString($"Acid_Kind_{substance.Kind}")),
            new(_localization.GetString("Acid_Stat_Diluted"), Dilutions == 0
                ? _localization.GetString("Acid_Scale_Undiluted")
                : _localization.Format("Acid_Scale_Diluted", Number(Math.Pow(10, Dilutions), "#,##0"))),
        ];
    }

    private void RefreshNeutralize()
    {
        var stage = Stage;
        PhaseTitle = _localization.GetString($"Acid_Stage_{stage}_Title");
        PhaseDescription = _localization.GetString(Acid == TitrationAcid.Strong ? $"Acid_Stage_{stage}_Strong" : $"Acid_Stage_{stage}_Weak");
        PhaseBrush = stage switch
        {
            "Start" => Acid == TitrationAcid.Strong ? RedBrush : OrangeBrush,
            "Before" => OrangeBrush,
            "Half" => CyanBrush,
            "Equivalence" => GreenBrush,
            _ => VioletBrush,
        };

        var acidLeft = Math.Max(0, (AcidBaseModel.FlaskAcidMl - BaseMl) * Molar);
        Stats =
        [
            new(_localization.GetString("Acid_Stat_Base"), _localization.Format("Acid_Value_Ml", Number(BaseMl, "0.00"))),
            new(_localization.GetString("Acid_Stat_Ph"), Number(TitrationPh, "0.00")),
            new(_localization.GetString("Acid_Stat_AcidLeft"), _localization.Format("Acid_Value_Mmol", Number(acidLeft, "0.00"))),
            new(_localization.GetString("Acid_Stat_Temperature"), _localization.Format("Acid_Value_Celsius",
                Number(AcidBaseDiagram.RoomTemperature + AcidBaseModel.TemperatureRise(Acid, BaseMl, Molar), "0.00"))),
            new(_localization.GetString("Acid_Stat_Colour"), _localization.GetString(ColourKey(FlaskIndicator, TitrationPh))),
        ];
    }

    /// <summary>The colour an indicator shows, in words: <c>Acid_Colour_Pink</c>.</summary>
    internal static string ColourKey(Indicator indicator, double ph)
    {
        var name = indicator switch
        {
            Indicator.Phenolphthalein => ph < 8.2 ? "Colourless" : ph < 9.2 ? "PalePink" : ph < 13.2 ? "Pink" : "Colourless",
            Indicator.Bromothymol => ph < 6.0 ? "Yellow" : ph < 7.6 ? "Green" : "Blue",
            Indicator.MethylOrange => ph < 3.1 ? "Red" : ph < 4.4 ? "Orange" : "Yellow",
            Indicator.Litmus => ph < 4.5 ? "Red" : ph < 8.3 ? "Purple" : "Blue",
            Indicator.Universal => ph < 3 ? "Red" : ph < 4.5 ? "Orange" : ph < 6.3 ? "Yellow" : ph < 7.5 ? "Green" : ph < 9.5 ? "Blue" : "Purple",
            Indicator.Cabbage => ph < 3 ? "Red" : ph < 5.5 ? "Pink" : ph < 7.5 ? "Purple" : ph < 9 ? "Blue" : ph < 12 ? "Green" : "Yellow",
            _ => "Colourless",
        };
        return "Acid_Colour_" + name;
    }

    #endregion

    #region formatting

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
        OnPropertyChanged(nameof(BaseMlText));
        Refresh();
    }

    public void Dispose()
    {
        _lateRefresh.Stop();
        _lateRefresh.Tick -= OnLateRefresh;
        Lesson.Dispose();
        Card.Dispose();
        BeakerIndicatorChoice.PropertyChanged -= OnBeakerIndicatorChosen;
        ScaleIndicatorChoice.PropertyChanged -= OnScaleIndicatorChosen;
        AcidChoice.PropertyChanged -= OnAcidChosen;
        FlaskIndicatorChoice.PropertyChanged -= OnFlaskIndicatorChosen;
        TapChoice.PropertyChanged -= OnTapChosen;
        MolarChoice.PropertyChanged -= OnMolarChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
