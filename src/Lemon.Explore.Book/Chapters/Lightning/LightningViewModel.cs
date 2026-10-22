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

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>
/// The lightning and static page: which view is shown, playback, the rod and cloth and the air's humidity, the storm's
/// height and the field meter, the strike's current, rod and distance, and what is happening. The diagram
/// (<see cref="LightningDiagram"/>) pushes back the rod's charge and state, the cloud's field and flashes, and the
/// strike's story time. <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in the
/// diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class LightningViewModel : ObservableObject, IChapterContent, IDisposable
{
    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    public IExploreLocationService Location => _location;
    private readonly Random _random = new();

    /// <summary>What the card was last worked out for, so it is not rebuilt every frame while the clocks run.</summary>
    private string _shownKey = string.Empty;

    /// <summary>The storm's clock at its latest flash, so the card can say "flash!" for a moment.</summary>
    private double _flashAt = double.NegativeInfinity;

    public LightningViewModel(IExploreLocationService location, INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => LightningLessons.For(this));
        Card = new ObjectCard(narration, Lesson, LightningDiagram.ObjectTitleKey, LightningDiagram.ObjectClipKey);
        _location = location;
        location.Changed += OnLocationChanged;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        RodChoice.PropertyChanged += OnRodChosen;
        ClothChoice.PropertyChanged += OnClothChosen;
        StrokeChoice.PropertyChanged += OnStrokesChosen;
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
    [NotifyPropertyChangedFor(nameof(IsStatic), nameof(IsCloud), nameof(IsStrike), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts))]
    private LightningMode _mode;

    public bool IsStatic
    {
        get => Mode == LightningMode.Static;
        set
        {
            if (value) Mode = LightningMode.Static;
        }
    }

    public bool IsCloud
    {
        get => Mode == LightningMode.Cloud;
        set
        {
            if (value) Mode = LightningMode.Cloud;
        }
    }

    public bool IsStrike
    {
        get => Mode == LightningMode.Strike;
        set
        {
            if (value) Mode = LightningMode.Strike;
        }
    }

    public string Subtitle => _localization.GetString($"Lightning_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Lightning_{Mode}_Note");

    public string Hint => _localization.GetString($"Lightning_Hint_{Mode}");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Lightning_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(LightningMode value)
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

    #region static

    /// <summary>The rod chips, by <see cref="Material"/> name.</summary>
    public OscillationChoice RodChoice { get; } = new(nameof(Material.Balloon));

    /// <summary>The cloth chips, by <see cref="Material"/> name.</summary>
    public OscillationChoice ClothChoice { get; } = new(nameof(Material.Hair));

    [ObservableProperty]
    private Material _rod = Material.Balloon;

    [ObservableProperty]
    private Material _cloth = Material.Hair;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HumidityText))]
    private double _humidity = 30;

    public string HumidityText => $"{Number(Humidity, "0")} %";

    [ObservableProperty]
    private bool _autoHand = true;

    /// <summary>The part of its round the demonstrating hand keeps repeating (all of it unless one is being shown).</summary>
    [ObservableProperty]
    private HandScene _handScene;

    /// <summary>The rod's charge, nC (from the diagram; set to 0 to earth it).</summary>
    [ObservableProperty]
    private double _charge;

    [ObservableProperty]
    private StaticState _state;

    [ObservableProperty]
    private double _staticTime;

    private void OnRodChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Rod = Enum.Parse<Material>(RodChoice.Selected);
    }

    private void OnClothChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Cloth = Enum.Parse<Material>(ClothChoice.Selected);
    }

    partial void OnRodChanged(Material value)
    {
        // A different rod starts uncharged.
        Charge = 0;
        Refresh(force: true);
    }

    partial void OnClothChanged(Material value) => Refresh(force: true);

    partial void OnHumidityChanged(double value) => Refresh(force: true);

    partial void OnChargeChanged(double value)
    {
        if (Mode == LightningMode.Static) Refresh();
    }

    partial void OnStateChanged(StaticState value)
    {
        if (Mode == LightningMode.Static) Refresh();
    }

    /// <summary>Earths the rod: touch it to a tap, the charge drains away.</summary>
    [RelayCommand]
    private void Discharge() => Charge = 0;

    [RelayCommand]
    private void SetHumidity(string? percent)
    {
        if (double.TryParse(percent, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Humidity = value;
    }

    #endregion

    #region the cloud

    /// <summary>The height of the storm's top, km.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CloudTopText))]
    private double _cloudTop = 12;

    public string CloudTopText => _localization.Format("Lightning_Km_Value", Number(CloudTop, "0.0"));

    [ObservableProperty]
    private bool _showCharges = true;

    /// <summary>Where the field meter stands, km from under the storm (moved by clicking the ground).</summary>
    [ObservableProperty]
    private double _meterX = 2;

    [ObservableProperty]
    private double _groundField;

    [ObservableProperty]
    private double _cloudTime;

    [ObservableProperty]
    private FlashKind _lastFlash;

    [ObservableProperty]
    private int _flashCount;

    /// <summary>Set to make the storm flash now (the diagram sets it back to none).</summary>
    [ObservableProperty]
    private FlashKind _flashRequest;

    public double Latitude => _location.Current.Latitude;

    public string PlaceName => _location.Current.Name;

    partial void OnCloudTopChanged(double value) => Refresh(force: true);

    partial void OnMeterXChanged(double value) => Refresh(force: true);

    partial void OnGroundFieldChanged(double value)
    {
        if (Mode == LightningMode.Cloud) Refresh();
    }

    partial void OnCloudTimeChanged(double value)
    {
        if (Mode == LightningMode.Cloud) Refresh();
    }

    partial void OnFlashCountChanged(int value)
    {
        _flashAt = CloudTime;
        Refresh(force: true);
    }

    [RelayCommand]
    private void SetTop(string? km)
    {
        if (double.TryParse(km, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) CloudTop = value;
    }

    #endregion

    #region the strike

    /// <summary>The first stroke's peak current, kA.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeakCurrentText))]
    private double _peakCurrent = 30;

    public string PeakCurrentText => $"{Number(PeakCurrent, "0")} kA";

    [ObservableProperty]
    private bool _lightningRod = true;

    [ObservableProperty]
    private bool _showSphere = true;

    /// <summary>How far away you are, km.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistanceText), nameof(StrikeLength))]
    private double _distance = 3;

    public string DistanceText => _localization.Format("Lightning_Km_Value", Number(Distance, "0.0"));

    /// <summary>The stroke count chips, 1…4.</summary>
    public OscillationChoice StrokeChoice { get; } = new("3");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrikeLength))]
    private int _strokes = 3;

    [ObservableProperty]
    private int _strikeSeed = 7;

    /// <summary>Story seconds since the leader started (from the diagram, or the timeline).</summary>
    [ObservableProperty]
    private double _strikeTime;

    public double StrikeLength => StrikeTimeline.Length(Strokes, Distance);

    private StrikeModel.Strike Strike => StrikeModel.Leader(StrikeSeed, PeakCurrent, LightningRod);

    private void OnStrokesChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Strokes = int.Parse(StrokeChoice.Selected, CultureInfo.InvariantCulture);
    }

    partial void OnPeakCurrentChanged(double value) => OnStrikeChanged();

    partial void OnLightningRodChanged(bool value) => OnStrikeChanged();

    partial void OnDistanceChanged(double value) => Refresh(force: true);

    partial void OnStrokesChanged(int value)
    {
        StrokeChoice.Selected = value.ToString(CultureInfo.InvariantCulture);
        OnStrikeChanged();
    }

    partial void OnStrikeSeedChanged(int value) => OnStrikeChanged();

    partial void OnStrikeTimeChanged(double value)
    {
        if (Mode == LightningMode.Strike) Refresh();
    }

    private void OnStrikeChanged()
    {
        IsPlaying = true;
        Refresh(force: true);
    }

    /// <summary>A new strike: the leader finds another way down.</summary>
    [RelayCommand]
    private void NewStrike() => StrikeSeed = _random.Next(1, 100000);

    [RelayCommand]
    private void SetCurrent(string? kiloAmps)
    {
        if (double.TryParse(kiloAmps, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) PeakCurrent = value;
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
        // The rod's card follows it every 2 nC, the storm's every flash and kV/m, the strike's every part (and every
        // half second while the thunder travels).
        var key = Mode switch
        {
            LightningMode.Static => $"S|{State}|{Math.Round(Charge / 2)}",
            LightningMode.Cloud => $"C|{FlashCount}|{Math.Round(GroundField)}|{CloudTime - _flashAt < 2}",
            _ => StrikeKey(),
        };

        // The strike's clock ticks every frame; the rest of its card only changes with the part of the flash.
        if (Mode == LightningMode.Strike) ClockText = StrikeClock();
        if (!force && key == _shownKey) return;
        _shownKey = key;

        switch (Mode)
        {
            case LightningMode.Cloud:
                RefreshCloud();
                break;

            case LightningMode.Strike:
                RefreshStrike();
                break;

            default:
                RefreshStatic();
                break;
        }
    }

    private string StrikeKey()
    {
        var (part, _, real) = StrikeTimeline.At(StrikeTime, Strokes);
        return $"K|{part.Phase}|{part.Stroke}|{(part.Phase == StrikeTimeline.Phase.Thunder ? Math.Floor(real * 2) : 0)}|{StrikePart()}";
    }

    private void RefreshStatic()
    {
        var charge = Charge * 1e-9;
        var volts = StaticModel.Voltage(charge);
        PhaseTitle = _localization.GetString($"Lightning_Static_{State}_Title");
        PhaseDescription = _localization.GetString($"Lightning_Static_{State}_Description");
        PhaseBrush = Frozen(State is StaticState.Spark or StaticState.Touched ? Color.FromRgb(0xFF, 0xD3, 0x6B)
            : Math.Abs(Charge) < 3 ? Color.FromRgb(0xEE, 0xF1, 0xF6)
            : Charge < 0 ? Color.FromRgb(0x5A, 0xB0, 0xFF) : Color.FromRgb(0xFF, 0x6A, 0x5A));
        ClockText = _localization.Format("Lightning_Static_Clock", ChargeText(Charge), LightningDiagram.VoltageText(volts));

        var saturation = StaticModel.SaturationCharge(Rod, Cloth);
        var who = saturation == 0
            ? _localization.GetString("Lightning_Stat_Who_Same")
            : _localization.Format(saturation < 0 ? "Lightning_Stat_Who_Takes" : "Lightning_Stat_Who_Gives", LightningDiagram.MaterialName(Rod), LightningDiagram.MaterialName(Cloth));
        var gap = StaticModel.SparkGap(volts);
        var halfLife = StaticModel.HalfLife(Humidity);
        Stats =
        [
            new(_localization.GetString("Lightning_Stat_Who"), who),
            new(_localization.GetString("Lightning_Stat_Charge"), ChargeText(Charge)),
            new(_localization.GetString("Lightning_Stat_Electrons"), Math.Abs(Charge) < 0.05 ? "0" : Scientific(StaticModel.Electrons(charge))),
            new(_localization.GetString("Lightning_Stat_Voltage"), LightningDiagram.VoltageText(volts)),
            new(_localization.GetString("Lightning_Stat_Spark"), gap <= 0
                ? _localization.GetString("Lightning_Stat_Spark_None")
                : _localization.Format("Lightning_Stat_Spark_Value", Number(gap * 1000, gap < 0.001 ? "0.00" : "0.0"))),
            new(_localization.GetString("Lightning_Stat_HalfLife"), _localization.Format("Lightning_Stat_HalfLife_Value", Number(Humidity, "0"), LongDuration(halfLife))),
        ];
    }

    private void RefreshCloud()
    {
        var rate = CloudModel.FlashRate(CloudTop);
        var recent = CloudTime - _flashAt < 2 && LastFlash != FlashKind.None;
        var key = recent ? LastFlash.ToString() : GroundField > 5 ? "Danger" : "Charging";
        PhaseTitle = _localization.GetString($"Lightning_Cloud_{key}_Title");
        PhaseDescription = _localization.GetString($"Lightning_Cloud_{key}_Description");
        PhaseBrush = Frozen(key switch
        {
            "CloudToGround" => Color.FromRgb(0xFF, 0xD3, 0x6B),
            "Intracloud" => Color.FromRgb(0xB3, 0x9D, 0xFF),
            "Danger" => Color.FromRgb(0xFF, 0x6A, 0x5A),
            _ => Color.FromRgb(0x6F, 0xD8, 0xFF),
        });
        ClockText = _localization.Format("Lightning_Cloud_Clock", FlashCount, Number(rate, rate < 10 ? "0.0" : "0"));

        var ratio = CloudModel.IntracloudRatio(Latitude);
        Stats =
        [
            new(_localization.GetString("Lightning_Stat_Top"), _localization.Format("Lightning_Stat_Top_Value", Number(CloudTop, "0.0"), Number(CloudModel.Temperature(CloudTop), "0"))),
            new(_localization.GetString("Lightning_Stat_Rate"), _localization.Format("Lightning_Stat_Rate_Value", Number(rate, rate < 10 ? "0.0" : "0"), Number(60 / Math.Max(0.01, rate), "0"))),
            new(_localization.GetString("Lightning_Stat_Main"), _localization.Format("Lightning_Stat_Main_Value",
                Number(CloudModel.HeightOf(CloudModel.ReversalTemperature), "0.0"), Number(CloudModel.ReversalTemperature, "0"))),
            new(_localization.GetString("Lightning_Stat_Field"), _localization.Format(GroundField >= 0 ? "Lightning_Stat_Field_Up" : "Lightning_Stat_Field_Down",
                Number(Math.Abs(GroundField), "0.0"), Number(MeterX, "0.0"))),
            new(_localization.GetString("Lightning_Stat_Share"), _localization.Format("Lightning_Stat_Share_Value", PlaceName, Number(ratio, "0.0"),
                Number(100 * CloudModel.GroundShare(Latitude), "0"))),
        ];
    }

    /// <summary>The strike's part as a key of its strings: Leader, Attach, Stroke, Later, Glow, Dart, Thunder or Heard.</summary>
    private string StrikePart()
    {
        var (part, _, real) = StrikeTimeline.At(StrikeTime, Strokes);
        var since = real - StrikeTimeline.FirstStrokeReal;
        return part.Phase switch
        {
            StrikeTimeline.Phase.Stroke when part.Stroke > 0 => "Later",
            StrikeTimeline.Phase.Thunder => since >= StrikeModel.ThunderDelay(Distance) ? "Heard" : "Thunder",
            _ => part.Phase.ToString(),
        };
    }

    private string StrikeClock() =>
        _localization.Format("Lightning_Strike_Clock", _localization.GetString($"Lightning_Strike_{StrikePart()}_Short"), RealText(StrikeTimeline.At(StrikeTime, Strokes).Real));

    private void RefreshStrike()
    {
        var strike = Strike;
        var delay = StrikeModel.ThunderDelay(Distance);
        var key = StrikePart();
        PhaseTitle = _localization.GetString($"Lightning_Strike_{key}_Title");
        PhaseDescription = _localization.GetString($"Lightning_Strike_{key}_Description");
        PhaseBrush = Frozen(key switch
        {
            "Leader" or "Dart" => Color.FromRgb(0xB3, 0x9D, 0xFF),
            "Attach" => Color.FromRgb(0x6F, 0xD8, 0xFF),
            "Thunder" or "Heard" => Color.FromRgb(0x6F, 0xD8, 0xFF),
            _ => Color.FromRgb(0xFF, 0xD3, 0x6B),
        });

        var reach = StrikeModel.StrikingDistance(PeakCurrent);
        var target = _localization.GetString($"Lightning_Target_{strike.Target}");
        Stats =
        [
            new(_localization.GetString("Lightning_Stat_Peak"), _localization.Format("Lightning_Stat_Peak_Value", Number(PeakCurrent, "0"), Number(StrikeModel.LaterPeak(PeakCurrent), "0"))),
            new(_localization.GetString("Lightning_Stat_Reach"), _localization.Format("Lightning_Stat_Reach_Value", Number(reach, "0"))),
            new(_localization.GetString("Lightning_Stat_Hit"), _localization.Format($"Lightning_Stat_Hit_{strike.Target}", target)),
            new(_localization.GetString("Lightning_Stat_Strokes"), _localization.Format("Lightning_Stat_Strokes_Value", Strokes, LightningDiagram.DurationText(StrikeTimeline.FlashReal(Strokes)))),
            new(_localization.GetString("Lightning_Stat_Thunder"), _localization.Format("Lightning_Stat_Thunder_Value", Number(delay, "0.0"), Number(Distance, "0.0"))),
        ];
    }

    /// <summary>Real time since the leader started: in ms during the flash, in s while the thunder travels.</summary>
    private string RealText(double seconds) => seconds < 1
        ? _localization.Format("Lightning_Unit_Ms", Number(seconds * 1000, "0.0"))
        : _localization.Format("Lightning_Unit_S", Number(seconds, "0.0"));

    /// <summary>A charge in nC, signed with a true minus.</summary>
    private static string ChargeText(double nanoCoulombs) => Math.Abs(nanoCoulombs) < 0.05
        ? "0 nC"
        : $"{(nanoCoulombs < 0 ? "−" : "+")}{Number(Math.Abs(nanoCoulombs), "0")} nC";

    /// <summary>A long duration: seconds, minutes or hours.</summary>
    private string LongDuration(double seconds) => seconds < 90
        ? _localization.Format("Lightning_Unit_S", Number(seconds, seconds < 10 ? "0.0" : "0"))
        : seconds < 5400
            ? _localization.Format("Lightning_Unit_Min", Number(seconds / 60, "0"))
            : _localization.Format("Lightning_Unit_H", Number(seconds / 3600, "0.0"));

    /// <summary>A big number as m × 10ⁿ with a superscript power.</summary>
    internal static string Scientific(double value)
    {
        if (value <= 0) return "0";
        var power = (int)Math.Floor(Math.Log10(value));
        var mantissa = value / Math.Pow(10, power);
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var exponent = string.Concat(power.ToString(CultureInfo.InvariantCulture).Select(c => c == '-' ? '⁻' : digits[c - '0']));
        return $"{Number(mantissa, "0.0")} × 10{exponent}";
    }

    #endregion

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Latitude));
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
        OnPropertyChanged(nameof(CloudTopText));
        OnPropertyChanged(nameof(DistanceText));
        OnPropertyChanged(nameof(PlaceName));
        Refresh(force: true);
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _location.Changed -= OnLocationChanged;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        RodChoice.PropertyChanged -= OnRodChosen;
        ClothChoice.PropertyChanged -= OnClothChosen;
        StrokeChoice.PropertyChanged -= OnStrokesChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
