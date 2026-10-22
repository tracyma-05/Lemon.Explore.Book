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

namespace Lemon.Explore.Book.Chapters.Oscillations;

/// <summary>
/// The pendulum and spring page: which view is shown, where the experiment is done, the pendulum's length and swing, the
/// spring's mass, stiffness and pull, the motor shaking it, and what is happening. The diagram
/// (<see cref="OscillatorDiagram"/>) runs the motion and pushes back <see cref="Stage"/>, <see cref="Energy"/>,
/// <see cref="MeasuredPeriod"/> and, for the wave, <see cref="WaveTime"/>. <see cref="Lesson"/> is the spoken walkthrough
/// of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class OscillationsViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x8B, 0xE0, 0x8F);
    private static readonly IBrush GreyBrush = Frozen(0x9A, 0xA5, 0xB5);
    private static readonly IBrush VioletBrush = Frozen(0xB3, 0x9D, 0xFF);

    /// <summary>The length that makes a pendulum swing one way in a second on Earth (a two-second period).</summary>
    public const double SecondsPendulum = 0.994;

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    private readonly IExploreLocationService _location;

    public IExploreLocationService Location => _location;

    public OscillationsViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        Lesson = new LessonPlayer(narration, () => OscillationsLessons.For(this));
        Card = new ObjectCard(narration, Lesson, OscillatorDiagram.ObjectTitleKey, OscillatorDiagram.ObjectClipKey);
        location.Changed += OnLocationChanged;
        PlaceChoice.PropertyChanged += OnPlaceChosen;
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
    [NotifyPropertyChangedFor(nameof(IsPendulum), nameof(IsSpring), nameof(IsWave), nameof(ShowsEnergy), nameof(Subtitle),
        nameof(ScaleNote), nameof(DragHint), nameof(Facts))]
    private OscillationMode _mode;

    public bool IsPendulum
    {
        get => Mode == OscillationMode.Pendulum;
        set
        {
            if (value) Mode = OscillationMode.Pendulum;
        }
    }

    public bool IsSpring
    {
        get => Mode == OscillationMode.Spring;
        set
        {
            if (value) Mode = OscillationMode.Spring;
        }
    }

    public bool IsWave
    {
        get => Mode == OscillationMode.Wave;
        set
        {
            if (value) Mode = OscillationMode.Wave;
        }
    }

    public string Subtitle => _localization.GetString($"Osc_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Osc_{Mode}_Note");

    public string DragHint => _localization.GetString($"Osc_DragHint_{Mode}");

    partial void OnModeChanged(OscillationMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Refresh();
    }

    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region place and playback

    /// <summary>Where the experiment is done, by <see cref="OscillatorPlace"/> name.</summary>
    public OscillationChoice PlaceChoice { get; } = new(nameof(OscillatorPlace.Earth));

    /// <summary>Playback speed chips: 0.25, 0.5, 1 or 2 times real time.</summary>
    public OscillationChoice SpeedChoice { get; } = new("1");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Gravity), nameof(WaveCycle))]
    private OscillatorPlace _place;

    public double Gravity => OscillatorPhysics.Gravity(Place, EarthGravity);

    /// <summary>Gravity on Earth at the shared observer location (9.780 m/s² at the equator, 9.832 at the poles).</summary>
    public double EarthGravity => _location.Current.Gravity;

    /// <summary>Under the place chips: whose gravity "Earth" is.</summary>
    public string EarthGravityNote => _localization.Format(
        "Location_Gravity_Note",
        _location.Current.Name,
        EarthGravity.ToString("0.000", CultureInfo.InvariantCulture));

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(EarthGravity));
        OnPropertyChanged(nameof(Gravity));
        OnPropertyChanged(nameof(EarthGravityNote));
        OnPropertyChanged(nameof(WaveCycle));
        Refresh();
    }

    [ObservableProperty]
    private double _speed = 1;

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Goes up to let the pendulum or the mass go again.</summary>
    [ObservableProperty]
    private int _releaseCount;

    /// <summary>Whether the air slows the swing down.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveDamping))]
    private bool _airDrag;

    [ObservableProperty]
    private bool _showForces;

    private void OnPlaceChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OscillationChoice.Selected)) return;
        Place = Enum.Parse<OscillatorPlace>(PlaceChoice.Selected);
    }

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OscillationChoice.Selected)) return;
        Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    partial void OnPlaceChanged(OscillatorPlace value)
    {
        PlaceChoice.Selected = value.ToString();
        Refresh();
    }

    partial void OnAirDragChanged(bool value) => Refresh();

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Lets the pendulum or the mass go again (the wave: from the start of its cycle).</summary>
    [RelayCommand]
    private void Release()
    {
        if (Mode == OscillationMode.Wave) WaveTime = 0;
        else ReleaseCount++;
        IsPlaying = true;
    }

    #endregion

    #region pendulum

    /// <summary>Length of the pendulum, metres.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LengthText))]
    private double _length = 1;

    /// <summary>How far it is let go, degrees.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmplitudeText))]
    private double _amplitude = 20;

    public string LengthText => $"{Length.ToString("0.00", CultureInfo.InvariantCulture)} m";

    public string AmplitudeText => $"{Amplitude.ToString("0", CultureInfo.InvariantCulture)}°";

    /// <summary>A faint twin swinging exactly as the formula says.</summary>
    [ObservableProperty]
    private bool _showFormula;

    public double MinLength => 0.1;

    public double MaxLength => OscillatorDiagram.MaxLength;

    [RelayCommand]
    private void SetLength(string? length)
    {
        if (double.TryParse(length, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Length = value;
    }

    partial void OnLengthChanged(double value) => Refresh();

    partial void OnAmplitudeChanged(double value) => Refresh();

    #endregion

    #region spring

    /// <summary>Mass on the spring, kilograms.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaturalFrequency), nameof(MassText), nameof(EffectiveDamping))]
    private double _mass = 0.5;

    /// <summary>Stiffness of the spring, N/m.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaturalFrequency), nameof(StiffnessText), nameof(EffectiveDamping))]
    private double _stiffness = 20;

    /// <summary>How far it is pulled from where it rests, metres.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PullCentimetres), nameof(PullText))]
    private double _pull = 0.12;

    public string MassText => $"{Mass.ToString("0.00", CultureInfo.InvariantCulture)} kg";

    public string StiffnessText => $"{Stiffness.ToString("0", CultureInfo.InvariantCulture)} N/m";

    public string PullText => $"{(Pull * 100).ToString("0", CultureInfo.InvariantCulture)} cm";

    public double MaxPull => OscillatorDiagram.MaxPull;

    /// <summary>The pull in centimetres, for the slider.</summary>
    public double PullCentimetres
    {
        get => Pull * 100;
        set => Pull = Math.Round(value) / 100;
    }

    /// <summary>A motor shakes the top of the spring up and down.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveDamping), nameof(ShowsEnergy))]
    private bool _shake;

    /// <summary>How often the motor shakes it, hertz.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DriveText))]
    private double _driveFrequency = 1;

    public string DriveText => Hertz(DriveFrequency);

    public double MaxDriveFrequency => ResonanceChart.MaxFrequency;

    /// <summary>The spring's own frequency, hertz.</summary>
    public double NaturalFrequency => 1 / OscillatorPhysics.SpringPeriod(Mass, Stiffness);

    /// <summary>Damping as the diagram applies it to the spring, per second.</summary>
    public double EffectiveDamping => Shake
        ? OscillatorPhysics.ShakenDamping(OscillatorPhysics.SpringOmega(Mass, Stiffness), AirDrag ? OscillatorPhysics.SpringDamping : 0)
        : AirDrag ? OscillatorPhysics.SpringDamping : 0;

    /// <summary>The energy chart: the pendulum and the spring, but not while the motor feeds energy in.</summary>
    public bool ShowsEnergy => Mode == OscillationMode.Pendulum || (Mode == OscillationMode.Spring && !Shake);

    /// <summary>Sets the motor to the spring's own frequency.</summary>
    [RelayCommand]
    private void ShakeAtOwnFrequency()
    {
        DriveFrequency = Math.Round(NaturalFrequency, 2);
        Shake = true;
    }

    partial void OnMassChanged(double value) => Refresh();

    partial void OnStiffnessChanged(double value) => Refresh();

    partial void OnPullChanged(double value) => Refresh();

    partial void OnShakeChanged(bool value) => Refresh();

    partial void OnDriveFrequencyChanged(double value) => Refresh();

    #endregion

    #region pendulum wave

    /// <summary>Seconds since the wave's pendulums were let go together.</summary>
    [ObservableProperty]
    private double _waveTime;

    /// <summary>Seconds until the wave is back in line where it is.</summary>
    public double WaveCycle => OscillatorPhysics.WaveCycle(Gravity);

    /// <summary>Jumps to a fraction of the cycle (e.g. "0.5": two rows) and pauses.</summary>
    [RelayCommand]
    private void JumpTo(string? fraction)
    {
        if (!double.TryParse(fraction, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        WaveTime = value * WaveCycle;
        IsPlaying = false;
    }

    partial void OnWaveTimeChanged(double value)
    {
        if (Mode == OscillationMode.Wave) Refresh();
    }

    #endregion

    #region what the diagram reports

    /// <summary>Where in its swing it is (from the diagram).</summary>
    [ObservableProperty]
    private SwingStage _stage;

    /// <summary>How its energy is shared out now (from the diagram).</summary>
    [ObservableProperty]
    private EnergySplit _energy;

    /// <summary>The period last timed on the paper, seconds; NaN until there is one (from the diagram).</summary>
    [ObservableProperty]
    private double _measuredPeriod = double.NaN;

    partial void OnStageChanged(SwingStage value) => Refresh();

    partial void OnMeasuredPeriodChanged(double value) => Refresh();

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = Brushes.White;
    private IReadOnlyList<OscillationStat> _stats = [];

    /// <summary>Beside the playback buttons: the period, or the time through the wave's cycle.</summary>
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

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Osc_{Mode}_Fact{i}"))
        .ToList();

    private void Refresh()
    {
        switch (Mode)
        {
            case OscillationMode.Spring:
                RefreshSpring();
                break;

            case OscillationMode.Wave:
                RefreshWave();
                break;

            default:
                RefreshPendulum();
                break;
        }
    }

    private void RefreshPendulum()
    {
        var radians = Amplitude * Math.PI / 180;
        var period = OscillatorPhysics.PendulumPeriod(Length, Gravity, radians);
        var formula = OscillatorPhysics.SmallAnglePeriod(Length, Gravity);
        ClockText = _localization.Format("Osc_Clock_Period", Seconds(period));
        SetStage("Pendulum");

        Stats =
        [
            new(_localization.GetString("Osc_Stat_Place"), PlaceText()),
            new(_localization.GetString("Osc_Stat_Length"), $"{Length.ToString("0.00", CultureInfo.InvariantCulture)} m"),
            new(_localization.GetString("Osc_Stat_Amplitude"), $"{Amplitude.ToString("0", CultureInfo.InvariantCulture)}°"),
            new(_localization.GetString("Osc_Stat_PeriodExact"), Seconds(period)),
            new(_localization.GetString("Osc_Stat_Formula"), Seconds(formula)),
            new(_localization.GetString("Osc_Stat_Longer"), _localization.Format("Osc_Stat_Longer_Value",
                ((period / formula - 1) * 100).ToString(period / formula < 1.1 ? "0.0" : "0", CultureInfo.InvariantCulture))),
            new(_localization.GetString("Osc_Stat_Measured"), MeasuredText()),
            new(_localization.GetString("Osc_Stat_TopSpeed"),
                $"{OscillatorPhysics.PendulumTopSpeed(Length, Gravity, radians).ToString("0.00", CultureInfo.InvariantCulture)} m/s"),
        ];
    }

    private void RefreshSpring()
    {
        var period = OscillatorPhysics.SpringPeriod(Mass, Stiffness);
        var stretch = OscillatorPhysics.SpringStretch(Mass, Stiffness, Gravity);
        ClockText = _localization.Format("Osc_Clock_Period", Seconds(period));
        SetStage("Spring");

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Osc_Stat_Place"), PlaceText()),
            new(_localization.GetString("Osc_Stat_Mass"), $"{Mass.ToString("0.00", CultureInfo.InvariantCulture)} kg"),
            new(_localization.GetString("Osc_Stat_Stiffness"), $"{Stiffness.ToString("0", CultureInfo.InvariantCulture)} N/m"),
            new(_localization.GetString("Osc_Stat_Stretch"), $"{(stretch * 100).ToString(stretch < 0.1 ? "0.0" : "0", CultureInfo.InvariantCulture)} cm"),
            new(_localization.GetString("Osc_Stat_Period"), Seconds(period)),
            new(_localization.GetString("Osc_Stat_Frequency"), Hertz(1 / period)),
            new(_localization.GetString("Osc_Stat_Measured"), MeasuredText()),
        };

        if (Shake)
        {
            var omega = 2 * Math.PI / period;
            var response = OscillatorPhysics.ShakenResponse(omega, EffectiveDamping, 2 * Math.PI * DriveFrequency);
            stats.Add(new OscillationStat(_localization.GetString("Osc_Stat_Drive"), Hertz(DriveFrequency)));
            stats.Add(new OscillationStat(_localization.GetString("Osc_Stat_Response"), _localization.Format("Osc_Stat_Response_Value",
                response.ToString(response < 10 ? "0.0" : "0", CultureInfo.InvariantCulture))));
        }

        Stats = stats;
    }

    private void RefreshWave()
    {
        var cycle = WaveCycle;
        var fraction = WaveTime / cycle;
        ClockText = _localization.Format("Osc_Clock_Wave", WaveTime.ToString("0.0", CultureInfo.InvariantCulture),
            cycle.ToString("0", CultureInfo.InvariantCulture));

        var pattern = OscillatorPhysics.Pattern(fraction, out var rows);
        PhaseTitle = pattern == WavePattern.Rows
            ? _localization.Format("Osc_Wave_Pattern_Rows_Title", rows)
            : _localization.GetString($"Osc_Wave_Pattern_{pattern}_Title");
        PhaseDescription = pattern == WavePattern.Rows
            ? _localization.Format("Osc_Wave_Pattern_Rows_Description", $"{Math.Round(fraction * rows) % rows}/{rows}", rows)
            : _localization.GetString($"Osc_Wave_Pattern_{pattern}_Description");
        PhaseBrush = pattern switch
        {
            WavePattern.InLine => GoldBrush,
            WavePattern.Snake => CyanBrush,
            WavePattern.Rows => GreenBrush,
            _ => VioletBrush,
        };

        var degrees = (fraction - Math.Floor(fraction)) * 360;
        var gravity = Gravity;
        var last = OscillatorPhysics.WaveCount - 1;
        Stats =
        [
            new(_localization.GetString("Osc_Stat_Place"), PlaceText()),
            new(_localization.GetString("Osc_Stat_Cycle"), _localization.Format("Osc_Stat_Cycle_Value", cycle.ToString("0", CultureInfo.InvariantCulture))),
            new(_localization.GetString("Osc_Stat_Longest"),
                $"{(OscillatorPhysics.WaveLength(0) * 100).ToString("0.0", CultureInfo.InvariantCulture)} cm · {Seconds(OscillatorPhysics.WavePeriod(0, gravity))}"),
            new(_localization.GetString("Osc_Stat_Shortest"),
                $"{(OscillatorPhysics.WaveLength(last) * 100).ToString("0.0", CultureInfo.InvariantCulture)} cm · {Seconds(OscillatorPhysics.WavePeriod(last, gravity))}"),
            new(_localization.GetString("Osc_Stat_Swings"), _localization.Format("Osc_Stat_Swings_Value",
                OscillatorPhysics.WaveFirstSwings, OscillatorPhysics.WaveFirstSwings + last)),
            new(_localization.GetString("Osc_Stat_Neighbours"), _localization.Format("Osc_Stat_Neighbours_Value",
                degrees.ToString("0", CultureInfo.InvariantCulture))),
        ];
    }

    /// <summary>The card's title, text and colour for where the pendulum or the mass is in its swing.</summary>
    private void SetStage(string kind)
    {
        PhaseTitle = _localization.GetString($"Osc_{kind}_Stage_{Stage}_Title");
        PhaseDescription = _localization.GetString($"Osc_{kind}_Stage_{Stage}_Description");
        PhaseBrush = Stage switch
        {
            SwingStage.Turning => GoldBrush,
            SwingStage.Middle => CyanBrush,
            SwingStage.Falling or SwingStage.Rising => GreenBrush,
            SwingStage.Held => VioletBrush,
            _ => GreyBrush,
        };
    }

    private string PlaceText() => _localization.Format("Osc_Stat_Place_Value", PlaceName(),
        Gravity.ToString("0.00", CultureInfo.InvariantCulture));

    private string MeasuredText() => double.IsNaN(MeasuredPeriod)
        ? _localization.GetString("Osc_Stat_Measured_Waiting")
        : Seconds(MeasuredPeriod);

    private static string Seconds(double seconds) =>
        $"{seconds.ToString(seconds < 10 ? "0.00" : "0.0", CultureInfo.InvariantCulture)} s";

    private static string Hertz(double hertz) => $"{hertz.ToString(hertz < 10 ? "0.00" : "0.0", CultureInfo.InvariantCulture)} Hz";

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    /// <summary>The place's name; Earth names the shared observer location, e.g. 地球（长沙）.</summary>
    private string PlaceName()
    {
        var name = _localization.GetString($"Osc_Place_{Place}");
        return Place == OscillatorPlace.Earth ? _localization.Format("Location_EarthAt", name, _location.Current.Name) : name;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(EarthGravityNote));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        Refresh();
    }

    public void Dispose()
    {
        _location.Changed -= OnLocationChanged;
        PlaceChoice.PropertyChanged -= OnPlaceChosen;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}
