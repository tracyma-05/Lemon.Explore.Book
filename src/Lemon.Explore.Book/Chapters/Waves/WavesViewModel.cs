using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Waves;

/// <summary>
/// The waves chapter: which view is shown, the ripple tank's dippers and probe, the light, slits and screen, the moving
/// source and its listener, and what is happening. The diagram (<see cref="WavesDiagram"/>) runs the clock and pushes
/// back <see cref="Time"/>, and the probes and the listener when they are dragged. <see cref="Lesson"/> is the spoken
/// walkthrough of the current view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class WavesViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreyBrush = Frozen(0x9A, 0xA5, 0xB5);
    private static readonly IBrush WhiteBrush = Frozen(0xEE, 0xF1, 0xF6);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9F, 0x5A);
    private static readonly IBrush VioletBrush = Frozen(0xB3, 0x9D, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    public WavesViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => WavesLessons.For(this));
        Card = new ObjectCard(narration, Lesson, WavesDiagram.ObjectTitleKey, WavesDiagram.ObjectClipKey);
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

    #region mode and playback

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRipple), nameof(IsSlits), nameof(IsDoppler), nameof(Subtitle), nameof(ScaleNote),
        nameof(DragHint), nameof(Facts))]
    private WaveMode _mode;

    public bool IsRipple
    {
        get => Mode == WaveMode.Ripple;
        set
        {
            if (value) Mode = WaveMode.Ripple;
        }
    }

    public bool IsSlits
    {
        get => Mode == WaveMode.Slits;
        set
        {
            if (value) Mode = WaveMode.Slits;
        }
    }

    public bool IsDoppler
    {
        get => Mode == WaveMode.Doppler;
        set
        {
            if (value) Mode = WaveMode.Doppler;
        }
    }

    public string Subtitle => _localization.GetString($"Waves_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Waves_{Mode}_Note");

    public string DragHint => _localization.GetString($"Waves_DragHint_{Mode}");

    partial void OnModeChanged(WaveMode value)
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

    /// <summary>Playback speed chips: 0.25, 0.5, 1 or 2 times the mode's own pace.</summary>
    public OscillationChoice SpeedChoice { get; } = new("1");

    [ObservableProperty]
    private double _speed = 1;

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Seconds of the diagram's clock (real seconds of the tank and of the Doppler pass).</summary>
    [ObservableProperty]
    private double _time;

    /// <summary>Goes up to start the waves (or the pass) again.</summary>
    [ObservableProperty]
    private int _restartCount;

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OscillationChoice.Selected)) return;
        Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    partial void OnTimeChanged(double value)
    {
        if (Mode == WaveMode.Doppler) Refresh();
    }

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    [RelayCommand]
    private void Restart()
    {
        RestartCount++;
        IsPlaying = true;
    }

    #endregion

    #region ripple tank

    /// <summary>Wavelength of the ripples, cm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WavelengthText))]
    private double _wavelength = 2.5;

    /// <summary>Distance between the dippers, cm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeparationText))]
    private double _separation = 6;

    /// <summary>The dippers move opposite ways.</summary>
    [ObservableProperty]
    private bool _opposite;

    /// <summary>Dipper B is on.</summary>
    [ObservableProperty]
    private bool _secondSource = true;

    [ObservableProperty]
    private bool _showLines;

    [ObservableProperty]
    private bool _showAverage;

    /// <summary>The probe, cm from the midpoint between the dippers (y down).</summary>
    [ObservableProperty]
    private Point _probe = new(5, 9);

    public string WavelengthText => $"{Cm(Wavelength)} · {Hertz(WavePhysics.RippleFrequency(Wavelength))}";

    public string SeparationText => Cm(Separation);

    /// <summary>Puts the probe on the first line of still water, or of the biggest waves, 9 cm out.</summary>
    [RelayCommand]
    private void ProbeOn(string? kind)
    {
        var differences = kind == "Quiet"
            ? WavePhysics.QuietDifferences(Separation, Wavelength, Opposite)
            : WavePhysics.LoudDifferences(Separation, Wavelength, Opposite);
        if (differences.Count == 0) return;

        // The nearest line to the right of the middle (the middle itself if it is the only one), where it is 9 cm out.
        var delta = differences.Where(d => d > 1e-9).DefaultIfEmpty(0).Min();
        var point = WavePhysics.Hyperbola(delta, Separation, 9).Last();
        Probe = new Point(Math.Round(point.X, 2), Math.Round(point.Y, 2));
        SecondSource = true;
    }

    partial void OnWavelengthChanged(double value) => Refresh();

    partial void OnSeparationChanged(double value) => Refresh();

    partial void OnOppositeChanged(bool value) => Refresh();

    partial void OnSecondSourceChanged(bool value) => Refresh();

    partial void OnProbeChanged(Point value) => Refresh();

    #endregion

    #region light through two slits

    /// <summary>The light's wavelength, nm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LightText))]
    private double _lightWavelength = 532;

    /// <summary>Distance between the slits, mm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SlitSpacingText))]
    private double _slitSpacing = 0.25;

    /// <summary>Distance from the slits to the screen, m.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScreenDistanceText))]
    private double _screenDistance = 2;

    [ObservableProperty]
    private bool _oneSlit;

    [ObservableProperty]
    private bool _whiteLight;

    /// <summary>The probe in the picture, units from the midpoint between the slits (y down): on bright stripe 1.</summary>
    [ObservableProperty]
    private Point _slitProbe = new(WavesDiagram.SlitScreen, -6.77);

    public string LightText => $"{Number(LightWavelength, "0")} nm";

    public string SlitSpacingText => $"{Number(SlitSpacing, "0.00")} mm";

    public string ScreenDistanceText => $"{Number(ScreenDistance, "0.0")} m";

    [RelayCommand]
    private void SetLight(string? nanometres)
    {
        if (!double.TryParse(nanometres, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        LightWavelength = value;
        WhiteLight = false;
    }

    /// <summary>Puts the probe on the screen where one slit is <c>n</c> wavelengths ("1") or n and a half ("0.5") farther.</summary>
    [RelayCommand]
    private void ProbeOnStripe(string? wavelengths)
    {
        if (!double.TryParse(wavelengths, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        var lambda = WavesDiagram.PictureWavelength(WhiteLight ? 550 : LightWavelength);
        var spacing = WavesDiagram.PictureSpacing(SlitSpacing);
        if (value * lambda >= spacing) return;
        SlitProbe = new Point(WavesDiagram.SlitScreen, -Math.Round(WavesDiagram.StripeHeight(value * lambda, spacing), 2));
    }

    partial void OnLightWavelengthChanged(double value) => Refresh();

    partial void OnSlitSpacingChanged(double value) => Refresh();

    partial void OnScreenDistanceChanged(double value) => Refresh();

    partial void OnOneSlitChanged(bool value) => Refresh();

    partial void OnWhiteLightChanged(bool value) => Refresh();

    partial void OnSlitProbeChanged(Point value) => Refresh();

    #endregion

    #region moving source

    /// <summary>The source's speed as a share of the speed of sound.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MachText), nameof(LoopDuration))]
    private double _mach = 0.5;

    /// <summary>Where the listener stands, metres (y down from the middle of the road).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoopDuration))]
    private Point _listener = new(60, 45);

    public string MachText => _localization.Format("Waves_Mach_Value", Number(Mach, "0.00"),
        Number(Mach * WavePhysics.SpeedOfSound, "0"), Number(Mach * WavePhysics.SpeedOfSound * 3.6, "0"));

    /// <summary>Seconds of one pass, for the timeline.</summary>
    public double LoopDuration => WavePhysics.LoopDuration(Mach, Listener);

    [RelayCommand]
    private void SetMach(string? mach)
    {
        if (!double.TryParse(mach, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return;
        Mach = value;
        RestartCount++;
        IsPlaying = true;
    }

    partial void OnMachChanged(double value)
    {
        if (Time > LoopDuration) Time = 0;
        Refresh();
    }

    partial void OnListenerChanged(Point value) => Refresh();

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = Brushes.White;
    private IReadOnlyList<OscillationStat> _stats = [];

    /// <summary>Beside the playback buttons: the frequency and how much it is slowed, or the time through the pass.</summary>
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
        .Select(i => _localization.GetString($"Waves_{Mode}_Fact{i}"))
        .ToList();

    private void Refresh()
    {
        switch (Mode)
        {
            case WaveMode.Slits:
                RefreshSlits();
                break;

            case WaveMode.Doppler:
                RefreshDoppler();
                break;

            default:
                RefreshRipple();
                break;
        }
    }

    private void RefreshRipple()
    {
        var frequency = WavePhysics.RippleFrequency(Wavelength);
        ClockText = _localization.Format("Waves_Clock_Ripple", Number(frequency, "0.0"), Number(WavePhysics.RippleSlowdown, "0"));

        var a = ((Vector)(Probe - WavePhysics.SourceA(Separation))).Length;
        var b = ((Vector)(Probe - WavePhysics.SourceB(Separation))).Length;
        var difference = a - b;
        var inWavelengths = difference / Wavelength;
        var (_, _, _, share) = WavePhysics.Combined(Probe, Separation, Wavelength, Opposite, SecondSource);

        if (!SecondSource)
        {
            PhaseTitle = _localization.GetString("Waves_Ripple_Single_Title");
            PhaseDescription = _localization.GetString("Waves_Ripple_Single_Description");
            PhaseBrush = CyanBrush;
        }
        else
        {
            var meeting = WavePhysics.MeetingOf(WavePhysics.PhaseGap(difference, Wavelength, Opposite));
            PhaseTitle = _localization.GetString($"Waves_Ripple_{meeting}_Title");
            PhaseDescription = _localization.Format($"Waves_Ripple_{meeting}_Description{(Opposite ? "Opposite" : string.Empty)}",
                Number(Math.Abs(inWavelengths), "0.00"));
            PhaseBrush = meeting switch
            {
                Meeting.InStep => GoldBrush,
                Meeting.OutOfStep => WhiteBrush,
                _ => CyanBrush,
            };
        }

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Waves_Stat_Wavelength"), Cm(Wavelength)),
            new(_localization.GetString("Waves_Stat_Frequency"), Hertz(frequency)),
            new(_localization.GetString("Waves_Stat_RippleSpeed"), $"{Number(WavePhysics.RippleSpeed, "0")} cm/s"),
            new(_localization.GetString("Waves_Stat_Separation"), Cm(Separation)),
            new(_localization.GetString("Waves_Stat_FromA"), Cm(a)),
        };

        if (SecondSource)
        {
            stats.Add(new OscillationStat(_localization.GetString("Waves_Stat_FromB"), Cm(b)));
            stats.Add(new OscillationStat(_localization.GetString("Waves_Stat_Difference"),
                _localization.Format("Waves_Stat_Difference_Value", Number(difference, "0.00"), Number(inWavelengths, "0.00"))));
            stats.Add(new OscillationStat(_localization.GetString("Waves_Stat_Swing"),
                _localization.Format("Waves_Stat_Swing_Value", Number(share * 100, "0"))));
            stats.Add(new OscillationStat(_localization.GetString("Waves_Stat_QuietLines"),
                WavePhysics.QuietDifferences(Separation, Wavelength, Opposite).Count.ToString(CultureInfo.InvariantCulture)));
        }

        Stats = stats;
    }

    private void RefreshSlits()
    {
        var nm = WhiteLight ? 550 : LightWavelength;
        var lambda = WavesDiagram.PictureWavelength(nm);
        var visible = WavesDiagram.SlitWaveSpeed / lambda;
        var slowed = WavePhysics.LightFrequency(nm) / visible / 1e12;
        ClockText = _localization.Format("Waves_Clock_Slits", Number(WavePhysics.LightFrequency(nm) / 1e12, "0"), Number(slowed, "0"));

        var spacing = WavesDiagram.PictureSpacing(SlitSpacing);
        var upper = new Point(0, -spacing / 2);
        var lower = new Point(0, spacing / 2);
        var inWavelengths = (((Vector)(SlitProbe - upper)).Length - ((Vector)(SlitProbe - lower)).Length) / lambda;
        var meeting = WavePhysics.MeetingOf(2 * Math.PI * inWavelengths);
        var order = (int)Math.Round(Math.Abs(inWavelengths));

        if (OneSlit)
        {
            PhaseTitle = _localization.GetString("Waves_Slits_One_Title");
            PhaseDescription = _localization.GetString("Waves_Slits_One_Description");
            PhaseBrush = CyanBrush;
        }
        else if (WhiteLight)
        {
            PhaseTitle = _localization.GetString("Waves_Slits_White_Title");
            PhaseDescription = _localization.GetString("Waves_Slits_White_Description");
            PhaseBrush = WhiteBrush;
        }
        else if (meeting == Meeting.InStep)
        {
            PhaseTitle = order == 0
                ? _localization.GetString("Waves_Slits_Central_Title")
                : _localization.Format("Waves_Slits_Bright_Title", order);
            PhaseDescription = _localization.Format("Waves_Slits_Bright_Description", Number(Math.Abs(inWavelengths), "0.00"));
            PhaseBrush = GoldBrush;
        }
        else if (meeting == Meeting.OutOfStep)
        {
            PhaseTitle = _localization.GetString("Waves_Slits_Dark_Title");
            PhaseDescription = _localization.Format("Waves_Slits_Dark_Description", Number(Math.Abs(inWavelengths), "0.00"));
            PhaseBrush = GreyBrush;
        }
        else
        {
            PhaseTitle = _localization.GetString("Waves_Slits_Between_Title");
            PhaseDescription = _localization.Format("Waves_Slits_Between_Description", Number(Math.Abs(inWavelengths), "0.00"));
            PhaseBrush = CyanBrush;
        }

        var fringe = WavePhysics.FringeSpacing(LightWavelength, ScreenDistance, SlitSpacing);
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Waves_Stat_Light"), WhiteLight
                ? _localization.GetString("Waves_Colour_White")
                : $"{Number(LightWavelength, "0")} nm · {_localization.GetString($"Waves_Colour_{ColourName(LightWavelength)}")}"),
            new(_localization.GetString("Waves_Stat_LightFrequency"), $"{Number(WavePhysics.LightFrequency(nm) / 1e12, "0")} THz"),
            new(_localization.GetString("Waves_Stat_SlitSpacing"), SlitSpacingText),
            new(_localization.GetString("Waves_Stat_SlitWidth"), $"{Number(WavePhysics.SlitWidth, "0.00")} mm"),
            new(_localization.GetString("Waves_Stat_ScreenDistance"), ScreenDistanceText),
        };

        if (!OneSlit)
        {
            stats.Add(new OscillationStat(_localization.GetString("Waves_Stat_FringeSpacing"), WhiteLight
                ? _localization.GetString("Waves_Stat_FringeSpacing_White")
                : $"{Number(fringe, fringe < 10 ? "0.00" : "0.0")} mm"));
            stats.Add(new OscillationStat(_localization.GetString("Waves_Stat_InGlow"),
                WavePhysics.StripesInMiddle(SlitSpacing).ToString(CultureInfo.InvariantCulture)));
            stats.Add(new OscillationStat(_localization.GetString("Waves_Stat_ProbeDifference"),
                _localization.Format("Waves_Stat_Wavelengths_Value", Number(Math.Abs(inWavelengths), "0.00"))));
        }

        Stats = stats;
    }

    private void RefreshDoppler()
    {
        var loop = LoopDuration;
        ClockText = _localization.Format("Waves_Clock_Doppler", Number(Math.Min(Time, loop), "0.00"), Number(loop, "0.00"));

        var sounds = WavePhysics.Heard(Listener, Time, Mach);
        var boom = WavePhysics.ConeArrival(Listener, Mach);
        var hearing = WavePhysics.HearingOf(sounds, Mach, Time, boom);
        PhaseTitle = _localization.GetString($"Waves_Doppler_{hearing}_Title");
        PhaseDescription = hearing switch
        {
            Hearing.Approaching or Hearing.Receding or Hearing.Passing => _localization.Format($"Waves_Doppler_{hearing}_Description",
                Number(sounds[0].Frequency, "0")),
            _ => _localization.GetString($"Waves_Doppler_{hearing}_Description"),
        };
        PhaseBrush = hearing switch
        {
            Hearing.Approaching => GoldBrush,
            Hearing.Receding => CyanBrush,
            Hearing.Boom => OrangeBrush,
            Hearing.Silent => GreyBrush,
            Hearing.Two => VioletBrush,
            _ => WhiteBrush,
        };

        var heard = sounds.Count switch
        {
            0 => _localization.GetString("Waves_Stat_Heard_None"),
            1 => Pitch(sounds[0].Frequency),
            _ => $"{Pitch(sounds[0].Frequency)} + {Pitch(sounds[1].Frequency)}",
        };

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Waves_Stat_SourceSpeed"), MachText),
            new(_localization.GetString("Waves_Stat_SourcePitch"), Pitch(WavePhysics.SourcePitch)),
            new(_localization.GetString("Waves_Stat_Heard"), heard),
            new(_localization.GetString("Waves_Stat_Ahead"), Mach < 1
                ? Pitch(WavePhysics.Ahead(Mach))
                : _localization.GetString("Waves_Stat_Ahead_None")),
            new(_localization.GetString("Waves_Stat_Behind"), Pitch(WavePhysics.Behind(Mach))),
            new(_localization.GetString("Waves_Stat_Distance"), $"{Number(Math.Abs(Listener.Y), "0")} m"),
        };

        if (Mach > 1)
        {
            stats.Add(new OscillationStat(_localization.GetString("Waves_Stat_Cone"),
                $"{Number(WavePhysics.ConeAngle(Mach) * 180 / Math.PI, "0")}°"));
        }

        Stats = stats;
    }

    /// <summary>The name of a colour of the rainbow, by wavelength.</summary>
    private static string ColourName(double nm) => nm switch
    {
        < 450 => "Violet",
        < 490 => "Blue",
        < 520 => "Cyan",
        < 565 => "Green",
        < 590 => "Yellow",
        < 625 => "Orange",
        _ => "Red",
    };

    private static string Pitch(double hertz) => $"{Number(hertz, "0")} Hz · {WavePhysics.NoteName(hertz)}";

    private static string Cm(double cm) => $"{Number(cm, "0.0")} cm";

    private static string Hertz(double hertz) => $"{Number(hertz, hertz < 10 ? "0.0" : "0")} Hz";

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(MachText));
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
