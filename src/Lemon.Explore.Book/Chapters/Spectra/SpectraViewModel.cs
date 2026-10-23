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

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>
/// The spectra chapter: which view is shown; the hydrogen atom's level, how it falls, the lamp's photons and the electron
/// cloud; the spectroscope's source, gas, a gas to compare with, the picked line and the guessing game; the star's
/// temperature and speed; and what is happening. The diagram (<see cref="SpectraDiagram"/>) runs the atom and pushes back
/// its level and what it last did. <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing
/// in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class SpectraViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly Random _random = new();

    public SpectraViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => SpectraLessons.For(this));
        Card = new ObjectCard(narration, Lesson, SpectraDiagram.ObjectTitleKey, SpectraDiagram.ObjectClipKey);
        FallChoice.PropertyChanged += OnFallChosen;
        ShapeChoice.PropertyChanged += OnShapeChosen;
        SourceChoice.PropertyChanged += OnSourceChosen;
        GasChoice.PropertyChanged += OnGasChosen;
        StarChoice.PropertyChanged += OnStarChosen;
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
    [NotifyPropertyChangedFor(nameof(IsAtom), nameof(IsLamps), nameof(IsStars), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private SpectraMode _mode;

    public bool IsAtom
    {
        get => Mode == SpectraMode.Atom;
        set
        {
            if (value) Mode = SpectraMode.Atom;
        }
    }

    public bool IsLamps
    {
        get => Mode == SpectraMode.Lamps;
        set
        {
            if (value) Mode = SpectraMode.Lamps;
        }
    }

    public bool IsStars
    {
        get => Mode == SpectraMode.Stars;
        set
        {
            if (value) Mode = SpectraMode.Stars;
        }
    }

    public string Subtitle => _localization.GetString($"Spec_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Spec_{Mode}_Note");

    public string Hint => _localization.GetString($"Spec_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Spec_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Spec_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Spec_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(SpectraMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
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
    private bool _isPlaying = true;

    [ObservableProperty]
    private bool _showLabels = true;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    #endregion

    #region the atom

    /// <summary>The electron's level, 1–6 (0 when set free). The atom pushes back its falls.</summary>
    [ObservableProperty]
    private int _level = 1;

    public OscillationChoice FallChoice { get; } = new(nameof(Chapters.Spectra.FallMode.Chance));

    [ObservableProperty]
    private FallMode _fallMode = FallMode.Chance;

    /// <summary>The electron stays up until it is dropped (or the switch is turned off).</summary>
    [ObservableProperty]
    private bool _hold;

    /// <summary>The wavelength of the photons the lamp fires, nm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PhotonText), nameof(PhotonLog))]
    private double _photonNm = 656.28;

    /// <summary>Changed to fire a photon.</summary>
    [ObservableProperty]
    private int _fireCount;

    /// <summary>Changed to clear the lines collected.</summary>
    [ObservableProperty]
    private int _clearCount;

    [ObservableProperty]
    private bool _showCloud;

    public OscillationChoice ShapeChoice { get; } = new("0");

    [ObservableProperty]
    private int _cloudShape;

    /// <summary>What the atom last did, as the diagram reports it: "emit 3 2", "absorb 1 3", "free 2", "pass 640.0", "capture 4".</summary>
    [ObservableProperty]
    private string? _lastEvent;

    /// <summary>The lamp's wavelength slider: log₁₀ nm, 90 to 2000.</summary>
    public double PhotonLog
    {
        get => Math.Log10(PhotonNm);
        set => PhotonNm = Math.Round(Math.Pow(10, Math.Clamp(value, Math.Log10(60), Math.Log10(2000))), 1);
    }

    public string PhotonText => _localization.Format("Spec_Value_Photon", SpectraDiagram.Nanometres(PhotonNm), Number(SpectraModel.PhotonEnergy(PhotonNm), "0.00"));

    partial void OnLevelChanged(int value) => Refresh();

    partial void OnFallModeChanged(FallMode value) => FallChoice.Selected = value.ToString();

    partial void OnPhotonNmChanged(double value) => Refresh();

    partial void OnLastEventChanged(string? value) => Refresh();

    partial void OnShowCloudChanged(bool value) => Refresh();

    partial void OnCloudShapeChanged(int value) => ShapeChoice.Selected = value.ToString(CultureInfo.InvariantCulture);

    private void OnFallChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<FallMode>(FallChoice.Selected, out var mode)) FallMode = mode;
    }

    private void OnShapeChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && int.TryParse(ShapeChoice.Selected, out var shape)) CloudShape = shape;
    }

    [RelayCommand]
    private void Fire() => FireCount++;

    [RelayCommand]
    private void Clear() => ClearCount++;

    /// <summary>Sets the lamp's wavelength, nm (from a chip).</summary>
    [RelayCommand]
    private void SetPhoton(string? nm)
    {
        if (double.TryParse(nm, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) PhotonNm = value;
    }

    /// <summary>Puts the electron back on the ground level, quietly.</summary>
    [RelayCommand]
    private void Ground() => Level = 1;

    #endregion

    #region the lamps

    public OscillationChoice SourceChoice { get; } = new(nameof(LightSource.Lamp));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesGas))]
    private LightSource _source = LightSource.Lamp;

    public OscillationChoice GasChoice { get; } = new(nameof(Gas.Hydrogen));

    [ObservableProperty]
    private Gas _gas = Gas.Hydrogen;

    /// <summary>The lamp's gas is hidden: the viewer guesses it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGasChoice))]
    private bool _mystery;

    [ObservableProperty]
    private Gas? _compareGas;

    /// <summary>The line picked in the spectrum, nm (NaN for none).</summary>
    [ObservableProperty]
    private double _selectedLine = double.NaN;

    [ObservableProperty]
    private bool _zoom;

    /// <summary>What the last guess got: empty before one.</summary>
    [ObservableProperty]
    private string _guessText = string.Empty;

    /// <summary>The gas chips matter: a lamp or a cool gas.</summary>
    public bool UsesGas => Source is LightSource.Lamp or LightSource.ThroughGas;

    /// <summary>The gas chips show (not while guessing).</summary>
    public bool ShowGasChoice => !Mystery;

    partial void OnSourceChanged(LightSource value)
    {
        SourceChoice.Selected = value.ToString();
        if (value != LightSource.Lamp) Mystery = false;
        SelectedLine = double.NaN;
        Refresh();
    }

    partial void OnGasChanged(Gas value)
    {
        GasChoice.Selected = value.ToString();
        SelectedLine = double.NaN;
        Refresh();
    }

    partial void OnMysteryChanged(bool value) => Refresh();

    partial void OnCompareGasChanged(Gas? value) => Refresh();

    partial void OnSelectedLineChanged(double value) => Refresh();

    private void OnSourceChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<LightSource>(SourceChoice.Selected, out var source)) Source = source;
    }

    private void OnGasChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<Gas>(GasChoice.Selected, out var gas)) Gas = gas;
    }

    /// <summary>A lamp of a gas picked by chance, its name hidden: which is it?</summary>
    [RelayCommand]
    private void NewMystery()
    {
        var gases = Enum.GetValues<Gas>().Where(g => g != Gas || !Mystery).ToArray();
        StartMystery(gases[_random.Next(gases.Length)]);
    }

    /// <summary>Starts the guessing game with a known gas (for the walkthrough).</summary>
    internal void StartMystery(Gas gas)
    {
        Source = LightSource.Lamp;
        Gas = gas;
        Mystery = true;
        CompareGas = null;
        GuessText = string.Empty;
        Refresh();
    }

    /// <summary>A guess at the hidden gas: right ends the game and names it, wrong says so.</summary>
    [RelayCommand]
    private void Guess(string? gas)
    {
        if (!Mystery || !Enum.TryParse<Gas>(gas, out var guess)) return;
        var name = _localization.GetString(SpectraDiagram.GasKey(guess));
        if (guess == Gas)
        {
            GuessText = _localization.Format("Spec_Guess_Right", name);
            Mystery = false;
        }
        else
        {
            GuessText = _localization.Format("Spec_Guess_Wrong", name);
        }
    }

    #endregion

    #region the stars

    /// <summary>The star chips: one of <see cref="SpectraModel.Stars"/> by key, or empty for a temperature of your own.</summary>
    public OscillationChoice StarChoice { get; } = new("Sun");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KelvinText), nameof(KelvinLog))]
    private double _kelvin = SpectraModel.SunKelvin;

    /// <summary>How fast the star moves away, km/s (negative: towards us).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VelocityText))]
    private double _velocity;

    /// <summary>The temperature slider: log₁₀ K, 2500 to 40,000.</summary>
    public double KelvinLog
    {
        get => Math.Log10(Kelvin);
        set => Kelvin = Math.Round(Math.Pow(10, Math.Clamp(value, Math.Log10(2500), Math.Log10(40_000))) / 10) * 10;
    }

    public string KelvinText => _localization.Format("Spec_Value_Kelvin", Number(Kelvin, "#,0"));

    public string VelocityText => Math.Abs(Velocity) < 1
        ? _localization.GetString("Spec_Value_AtRest")
        : _localization.Format(Velocity > 0 ? "Spec_Value_Away" : "Spec_Value_Towards", Number(Math.Abs(Velocity), "#,0"));

    partial void OnKelvinChanged(double value)
    {
        var preset = SpectraModel.Stars.FirstOrDefault(s => Math.Abs(s.Kelvin - value) < 1);
        StarChoice.Selected = preset?.Key ?? string.Empty;
        Refresh();
    }

    partial void OnVelocityChanged(double value) => Refresh();

    private void OnStarChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OscillationChoice.Selected)) return;
        if (SpectraModel.Stars.FirstOrDefault(s => s.Key == StarChoice.Selected) is { } star) Kelvin = star.Kelvin;
    }

    /// <summary>Sets the star's speed, km/s (from a chip).</summary>
    [RelayCommand]
    private void SetVelocity(string? kms)
    {
        if (double.TryParse(kms, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Velocity = value;
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

    private void Refresh()
    {
        switch (Mode)
        {
            case SpectraMode.Lamps:
                RefreshLamps();
                break;

            case SpectraMode.Stars:
                RefreshStars();
                break;

            default:
                RefreshAtom();
                break;
        }
    }

    private void RefreshAtom()
    {
        var state = Level == 0 ? "Free" : Level == 1 ? "Ground" : "Excited";
        PhaseTitle = Level >= 2 ? _localization.Format("Spec_Atom_Excited_Title", Level) : _localization.GetString($"Spec_Atom_{state}_Title");
        PhaseDescription = _localization.GetString($"Spec_Atom_{state}_Text");
        PhaseBrush = state switch
        {
            "Free" => VioletBrush,
            "Excited" => GoldBrush,
            _ => CyanBrush,
        };

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Spec_Stat_Level"), Level == 0 ? _localization.GetString("Spec_Value_Free") : Level.ToString(CultureInfo.InvariantCulture)),
            new(_localization.GetString("Spec_Stat_Energy"), Level == 0 ? _localization.GetString("Spec_Value_AboveZero") : _localization.Format("Spec_Value_Ev", Number(SpectraModel.Energy(Level), "0.00"))),
            new(_localization.GetString("Spec_Stat_Last"), DescribeEvent()),
            new(_localization.GetString("Spec_Stat_Lamp"), PhotonText),
            new(_localization.GetString("Spec_Stat_WouldDo"), DescribeLamp()),
        };
        if (Level >= 1) stats.Add(new(_localization.GetString("Spec_Stat_ToFree"), _localization.Format("Spec_Value_Ev", Number(-SpectraModel.Energy(Level), "0.00"))));
        Stats = stats;
    }

    /// <summary>The atom's last event in words.</summary>
    private string DescribeEvent()
    {
        var parts = (LastEvent ?? string.Empty).Split(' ');
        if (parts.Length < 2) return _localization.GetString("Spec_Value_Nothing");
        int Part(int i) => i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
        switch (parts[0])
        {
            case "emit":
            {
                var nm = SpectraModel.Wavelength(Part(2), Part(1));
                return _localization.Format("Spec_Event_Emit", Part(1), Part(2), SpectraDiagram.Nanometres(nm), _localization.GetString(SeriesKey(Part(2))), _localization.GetString(RegionKey(nm)));
            }

            case "absorb":
                return _localization.Format("Spec_Event_Absorb", Part(1), Part(2), SpectraDiagram.Nanometres(SpectraModel.Wavelength(Part(1), Part(2))));

            case "free":
                return _localization.Format("Spec_Event_Free", Part(1));

            case "capture":
                return _localization.Format("Spec_Event_Capture", Part(1));

            default:
                return _localization.Format("Spec_Event_Pass", parts[1]);
        }
    }

    /// <summary>What the lamp's photon would do to the electron where it is now.</summary>
    private string DescribeLamp()
    {
        if (Level == 0) return _localization.GetString("Spec_Value_NoElectron");
        var result = SpectraModel.Absorb(Level, PhotonNm);
        return result switch
        {
            0 => _localization.GetString("Spec_Value_WouldFree"),
            -1 => _localization.GetString("Spec_Value_WouldPass"),
            _ => _localization.Format("Spec_Value_WouldLift", result),
        };
    }

    private static string SeriesKey(int lower) => SpectraModel.SeriesKey(lower) switch
    {
        "Lyman" => "Spec_Series_Lyman",
        "Balmer" => "Spec_Series_Balmer",
        "Paschen" => "Spec_Series_Paschen",
        "Brackett" => "Spec_Series_Brackett",
        _ => "Spec_Series_Pfund",
    };

    private static string RegionKey(double nm) => nm switch
    {
        < SpectraModel.VisibleFrom => "Spec_Value_Ultraviolet",
        > SpectraModel.VisibleTo => "Spec_Value_Infrared",
        _ => "Spec_Value_Visible",
    };

    private void RefreshLamps()
    {
        string state;
        if (Mystery)
        {
            state = "Mystery";
        }
        else
        {
            state = Source switch
            {
                LightSource.Bulb => "Bulb",
                LightSource.Sun => "Sun",
                LightSource.ThroughGas => "ThroughGas",
                _ => "Lamp",
            };
        }

        var gasName = _localization.GetString(SpectraDiagram.GasKey(Gas));
        PhaseTitle = state is "Lamp" or "ThroughGas" ? _localization.Format($"Spec_Lamps_{state}_Title", gasName) : _localization.GetString($"Spec_Lamps_{state}_Title");
        PhaseDescription = _localization.GetString($"Spec_Lamps_{state}_Text");
        PhaseBrush = state switch
        {
            "Mystery" => GoldBrush,
            "Lamp" => GreenBrush,
            "Sun" => GoldBrush,
            _ => CyanBrush,
        };

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Spec_Stat_Kind"), _localization.GetString(Source switch
            {
                LightSource.Lamp => "Spec_Value_Bright",
                LightSource.Bulb => "Spec_Value_Continuous",
                _ => "Spec_Value_Dark",
            })),
        };

        if (UsesGas && !Mystery) stats.Add(new(_localization.GetString("Spec_Stat_Lines"), SpectraModel.Lines(Gas).Count.ToString(CultureInfo.InvariantCulture)));
        if (CompareGas is { } other) stats.Add(new(_localization.GetString("Spec_Stat_Compare"), _localization.GetString(SpectraDiagram.GasKey(other))));
        if (!double.IsNaN(SelectedLine))
        {
            stats.Add(new(_localization.GetString("Spec_Stat_Picked"), SpectraDiagram.Nanometres(SelectedLine)));
            stats.Add(new(_localization.GetString("Spec_Stat_PhotonEnergy"), _localization.Format("Spec_Value_Ev", Number(SpectraModel.PhotonEnergy(SelectedLine), "0.00"))));

            // A hydrogen line: which jump made it.
            if (Gas == Gas.Hydrogen && UsesGas && !Mystery)
            {
                for (var upper = 3; upper <= 7; upper++)
                {
                    if (Math.Abs(SpectraModel.Wavelength(2, upper) - SelectedLine) < 0.2)
                    {
                        stats.Add(new(_localization.GetString("Spec_Stat_Jump"), _localization.Format("Spec_Value_Jump", upper, 2)));
                    }
                }
            }
        }

        Stats = stats;
    }

    private void RefreshStars()
    {
        var type = SpectraModel.SpectralClass(Kelvin);
        var preset = SpectraModel.Stars.FirstOrDefault(s => Math.Abs(s.Kelvin - Kelvin) < 1);
        PhaseTitle = preset is not null
            ? _localization.Format("Spec_Stars_Named_Title", _localization.GetString(StarKey(preset.Key)), preset.Type)
            : _localization.Format("Spec_Stars_Title", type);
        PhaseDescription = _localization.GetString(type switch
        {
            'O' => "Spec_Class_O_Text",
            'B' => "Spec_Class_B_Text",
            'A' => "Spec_Class_A_Text",
            'F' => "Spec_Class_F_Text",
            'G' => "Spec_Class_G_Text",
            'K' => "Spec_Class_K_Text",
            _ => "Spec_Class_M_Text",
        });
        PhaseBrush = new ImmutableSolidColorBrush(SpectraModel.BodyColour(Kelvin));

        var peak = SpectraModel.PeakWavelength(Kelvin);
        var families = SpectraModel.LineFamilies(Kelvin);
        Stats =
        [
            new(_localization.GetString("Spec_Stat_Temperature"), KelvinText),
            new(_localization.GetString("Spec_Stat_Class"), type.ToString()),
            new(_localization.GetString("Spec_Stat_Peak"), _localization.Format("Spec_Value_PeakAt", SpectraDiagram.Nanometres(peak), _localization.GetString(RegionKey(peak)))),
            new(_localization.GetString("Spec_Stat_Glow"), _localization.Format("Spec_Value_TimesSun", Number(Math.Pow(Kelvin / SpectraModel.SunKelvin, 4), Kelvin < SpectraModel.SunKelvin ? "0.00" : "0.0"))),
            new(_localization.GetString("Spec_Stat_HydrogenLines"), _localization.Format("Spec_Value_Percent", Number(families.Hydrogen * 100, "0"))),
            new(_localization.GetString("Spec_Stat_Speed"), VelocityText),
            new(_localization.GetString("Spec_Stat_AlphaShift"), _localization.Format("Spec_Value_Shift", Number(SpectraModel.Shift(656.28, Velocity) - 656.28, "+0.00;-0.00;0.00"))),
        ];
    }

    /// <summary>A star preset's name key.</summary>
    internal static string StarKey(string key) => key switch
    {
        "Spica" => "Spec_Star_Spica",
        "Sirius" => "Spec_Star_Sirius",
        "Procyon" => "Spec_Star_Procyon",
        "Arcturus" => "Spec_Star_Arcturus",
        "Betelgeuse" => "Spec_Star_Betelgeuse",
        _ => "Spec_Star_Sun",
    };

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
        OnPropertyChanged(nameof(PhotonText));
        OnPropertyChanged(nameof(KelvinText));
        OnPropertyChanged(nameof(VelocityText));
        GuessText = string.Empty;
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        FallChoice.PropertyChanged -= OnFallChosen;
        ShapeChoice.PropertyChanged -= OnShapeChosen;
        SourceChoice.PropertyChanged -= OnSourceChosen;
        GasChoice.PropertyChanged -= OnGasChosen;
        StarChoice.PropertyChanged -= OnStarChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
