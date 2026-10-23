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

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// The sound chapter: which view is shown; the loudspeaker's tone (frequency, level), what it travels through and the air's
/// temperature; the guitar string (which string, fret, tension, where it is plucked) or the pipe (open or closed, length,
/// which resonance); the instrument, the key and a second note; playback and the page's own sound; and what is happening.
/// The diagram (<see cref="SoundDiagram"/>) asks for plucks and notes, and <see cref="SoundEngine"/> plays them.
/// <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in the diagram
/// (<see cref="Card"/>) explains it.
/// </summary>
/// <remarks>Walkthroughs make no sound themselves: a pluck or note set up by a step (<see cref="LessonPlayer.IsApplying"/>) is silent.</remarks>
public partial class SoundViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);

    /// <summary>How far away the "it reaches you after" line puts the listener, m.</summary>
    private const double ListenerDistance = 10;

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly SoundEngine _audio;
    private double[] _custom = SoundModel.Harmonics(Instrument.Custom);
    private bool _editingHarmonics;

    public SoundViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => SoundLessons.For(this));
        Card = new ObjectCard(narration, Lesson, SoundDiagram.ObjectTitleKey, SoundDiagram.ObjectClipKey);
        _audio = new SoundEngine(() => !narration.IsFinished && !narration.IsPaused);
        SpeedChoice.PropertyChanged += OnSpeedChosen;
        FrequencyChoice.PropertyChanged += OnFrequencyChosen;
        MediumChoice.PropertyChanged += OnMediumChosen;
        ResonatorChoice.PropertyChanged += OnResonatorChosen;
        StringChoice.PropertyChanged += OnStringChosen;
        PipeModeChoice.PropertyChanged += OnPipeModeChosen;
        InstrumentChoice.PropertyChanged += OnInstrumentChosen;
        SecondChoice.PropertyChanged += OnSecondChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        Refresh();
    }

    /// <summary>A view of the chapter came on screen.</summary>
    public void ViewAttached() => _views++;

    /// <summary>The last view of the chapter left the screen: the walkthrough and any explanation being read stop, and so
    /// does the chapter's own sound.</summary>
    public void ViewDetached()
    {
        _views = Math.Max(0, _views - 1);
        if (_views > 0)
        {
            return;
        }

        Lesson.Stop();
        Card.Close();
        StopSound();
    }

    #region mode

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWave), nameof(IsStrings), nameof(IsTimbre), nameof(ShowPluck), nameof(ShowBlow), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private SoundMode _mode;

    public bool IsWave
    {
        get => Mode == SoundMode.Wave;
        set
        {
            if (value) Mode = SoundMode.Wave;
        }
    }

    public bool IsStrings
    {
        get => Mode == SoundMode.Strings;
        set
        {
            if (value) Mode = SoundMode.Strings;
        }
    }

    public bool IsTimbre
    {
        get => Mode == SoundMode.Timbre;
        set
        {
            if (value) Mode = SoundMode.Timbre;
        }
    }

    public string Subtitle => _localization.GetString($"Sound_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Sound_{Mode}_Note");

    public string Hint => Mode == SoundMode.Strings && Resonator != Resonator.String
        ? _localization.GetString("Sound_Hint_Pipe")
        : _localization.GetString($"Sound_Hint_{Mode}");

    public string ChartCaption => Mode == SoundMode.Strings && Resonator != Resonator.String
        ? _localization.GetString("Sound_Chart_Pipe_Caption")
        : _localization.GetString($"Sound_Chart_{Mode}_Caption");

    public string ChartNote => Mode == SoundMode.Strings && Resonator != Resonator.String
        ? _localization.GetString("Sound_Chart_Pipe_Note")
        : _localization.GetString($"Sound_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Sound_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(SoundMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = true;
        _audio.Stop();
        UpdateTone();
        Refresh();
    }

    /// <summary>The spoken walkthrough of the current view.</summary>
    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a click or a drag before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region playback and sound

    /// <summary>Playback speed chips: 0.25, 0.5, 1 or 2 times the (already slowed) pace.</summary>
    public OscillationChoice SpeedChoice { get; } = new("1");

    [ObservableProperty]
    private double _speed = 1;

    [ObservableProperty]
    private bool _isPlaying = true;

    [ObservableProperty]
    private bool _showLabels = true;

    /// <summary>The page's own sound (plucks, notes, the tone); off mutes it.</summary>
    [ObservableProperty]
    private bool _soundOn = true;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)) Speed = double.Parse(SpeedChoice.Selected, CultureInfo.InvariantCulture);
    }

    partial void OnSoundOnChanged(bool value)
    {
        _audio.Enabled = value;
        UpdateTone();
    }

    /// <summary>Stops every sound (the page is left).</summary>
    public void StopSound()
    {
        ToneOn = false;
        _audio.Stop();
    }

    /// <summary>Whether a sound asked for now should be heard: not while a walkthrough is setting a step up.</summary>
    private bool Audible => !Lesson.IsApplying;

    #endregion

    #region the wave

    /// <summary>Tone chips: a low A, concert A, the ear's most sensitive range, and a test of the top of hearing.</summary>
    public OscillationChoice FrequencyChoice { get; } = new("440");

    /// <summary>The loudspeaker's frequency, Hz.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrequencyText), nameof(FrequencyLog))]
    private double _frequency = 440;

    /// <summary>The sound's level, dB.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoudnessText))]
    private double _loudness = 60;

    public OscillationChoice MediumChoice { get; } = new(nameof(SoundMedium.Air));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAir))]
    private SoundMedium _medium = SoundMedium.Air;

    /// <summary>The air's temperature, °C (also the pipes').</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemperatureText))]
    private double _temperature = 20;

    /// <summary>The loudspeaker's tone is heard (the wave view only).</summary>
    [ObservableProperty]
    private bool _toneOn;

    public bool IsAir => Medium == SoundMedium.Air;

    /// <summary>The frequency slider, logarithmic: 20 Hz (1.30) to 20 kHz (4.30).</summary>
    public double FrequencyLog
    {
        get => Math.Log10(Frequency);
        set
        {
            var hz = Math.Pow(10, Math.Clamp(value, Math.Log10(SoundModel.LowestHeard), Math.Log10(SoundModel.HighestHeard)));
            Frequency = Math.Round(hz, hz < 100 ? 1 : 0);
        }
    }

    /// <summary>The frequency, with its piano key when it has one.</summary>
    public string FrequencyText => SoundModel.IsOnPiano(Frequency)
        ? _localization.Format("Sound_Value_Note", SoundDiagram.Hertz(Frequency), SoundModel.NoteName(Frequency))
        : SoundDiagram.Hertz(Frequency);

    public string LoudnessText => _localization.Format("Sound_Value_Level", Number(Loudness, "0"), _localization.GetString(LoudKey(Loudness)));

    public string TemperatureText => _localization.Format("Sound_Value_Celsius", Number(Temperature, "0"));

    partial void OnFrequencyChanged(double value)
    {
        if (FrequencyChoice.Selected.Length > 0 && Math.Abs(double.Parse(FrequencyChoice.Selected, CultureInfo.InvariantCulture) - value) > 0.5) FrequencyChoice.Selected = string.Empty;
        UpdateTone();
        Refresh();
    }

    partial void OnLoudnessChanged(double value)
    {
        UpdateTone();
        Refresh();
    }

    partial void OnMediumChanged(SoundMedium value)
    {
        MediumChoice.Selected = value.ToString();
        UpdateTone();
        Refresh();
    }

    partial void OnTemperatureChanged(double value)
    {
        OnPropertyChanged(nameof(PipeFrequency));
        Refresh();
    }

    partial void OnToneOnChanged(bool value) => UpdateTone();

    private void OnFrequencyChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && FrequencyChoice.Selected.Length > 0) Frequency = double.Parse(FrequencyChoice.Selected, CultureInfo.InvariantCulture);
    }

    private void OnMediumChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<SoundMedium>(MediumChoice.Selected, out var medium)) Medium = medium;
    }

    /// <summary>
    /// The tone through the speakers: on in the wave view when asked for (and not in a vacuum). Its loudness follows the
    /// level loosely, half the real slope and topped well below full scale: the computer's own volume sets what you hear.
    /// </summary>
    private void UpdateTone()
    {
        var on = Mode == SoundMode.Wave && ToneOn && Medium != SoundMedium.Vacuum;
        _audio.SetTone(Frequency, on ? 0.3 * Math.Pow(10, (Math.Clamp(Loudness, 0, 120) - 120) / 40) : 0);
    }

    [RelayCommand]
    private void ToggleTone() => ToneOn = !ToneOn;

    private static string LoudKey(double decibels) => SoundModel.LoudnessExample(decibels) switch
    {
        "Breath" => "Sound_Loud_Breath",
        "Whisper" => "Sound_Loud_Whisper",
        "Library" => "Sound_Loud_Library",
        "Talk" => "Sound_Loud_Talk",
        "Traffic" => "Sound_Loud_Traffic",
        "Drill" => "Sound_Loud_Drill",
        "Concert" => "Sound_Loud_Concert",
        "Thunder" => "Sound_Loud_Thunder",
        _ => "Sound_Loud_Jet",
    };

    #endregion

    #region strings and pipes

    public OscillationChoice ResonatorChoice { get; } = new(nameof(Resonator.String));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsString), nameof(IsPipe), nameof(ShowPluck), nameof(ShowBlow), nameof(Hint), nameof(ChartCaption), nameof(ChartNote))]
    private Resonator _resonator = Resonator.String;

    public bool IsString => Resonator == Resonator.String;

    public bool IsPipe => Resonator != Resonator.String;

    /// <summary>The toolbar's pluck button: the strings view with a string.</summary>
    public bool ShowPluck => Mode == SoundMode.Strings && IsString;

    /// <summary>The toolbar's blow button: the strings view with a pipe.</summary>
    public bool ShowBlow => Mode == SoundMode.Strings && IsPipe;

    /// <summary>Which guitar string: 0 (low E) to 5 (high E).</summary>
    public OscillationChoice StringChoice { get; } = new("1");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StringFrequency), nameof(TensionText))]
    private int _stringIndex = 1;

    /// <summary>The fret held down, 0 (open) to 12.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StringFrequency), nameof(FretText))]
    private int _fret;

    /// <summary>The string's tension as a share of its usual one (tuning peg turned): 0.8–1.25.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StringFrequency), nameof(TensionText))]
    private double _tensionScale = 1;

    /// <summary>Where the string is plucked, a fraction of the vibrating length from the fret (or nut).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PluckText))]
    private double _pluckPoint = 0.2;

    [ObservableProperty]
    private double _pluckHeight = 0.8;

    /// <summary>Changed to pluck the string again.</summary>
    [ObservableProperty]
    private int _pluckCount;

    /// <summary>One harmonic alone (1–6), or 0 for the plucked mix.</summary>
    [ObservableProperty]
    private int _soloHarmonic;

    [ObservableProperty]
    private bool _showHarmonics = true;

    /// <summary>The pipe's length, m.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PipeFrequency), nameof(PipeLengthText))]
    private double _pipeLength = 0.6;

    /// <summary>Which resonance of the pipe sounds, 1–4.</summary>
    public OscillationChoice PipeModeChoice { get; } = new("1");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PipeFrequency))]
    private int _pipeMode = 1;

    /// <summary>The open string's note (MIDI).</summary>
    public int OpenStringMidi => SoundModel.GuitarStrings[Math.Clamp(StringIndex, 0, 5)];

    /// <summary>The string's tension, N.</summary>
    public double Tension => SoundModel.GuitarTensions[Math.Clamp(StringIndex, 0, 5)] * TensionScale;

    /// <summary>The string's fundamental now, Hz: the open note, retuned by the tension, raised a semitone per fret.</summary>
    public double StringFrequency => SoundModel.NoteFrequency(OpenStringMidi) * Math.Sqrt(TensionScale) * Math.Pow(2, Fret / 12.0);

    /// <summary>The pipe's sounding frequency, Hz.</summary>
    public double PipeFrequency => SoundModel.PipeFrequency(PipeMode, PipeLength, SoundModel.SpeedInAir(Temperature), Resonator == Resonator.ClosedPipe);

    public string FretText => Fret == 0 ? _localization.GetString("Sound_Value_Open") : _localization.Format("Sound_Value_Fret", Fret);

    public string TensionText => _localization.Format("Sound_Value_Newton", Number(Tension, "0"));

    public string PluckText => _localization.Format("Sound_Value_Percent", Number(PluckPoint * 100, "0"));

    public string PipeLengthText => SoundDiagram.Metres(PipeLength);

    partial void OnResonatorChanged(Resonator value)
    {
        ResonatorChoice.Selected = value.ToString();
        OnPropertyChanged(nameof(PipeFrequency));
        Card.Id = null;
        if (value != Resonator.String && Audible) PlayPipe();
        Refresh();
    }

    partial void OnStringIndexChanged(int value)
    {
        StringChoice.Selected = value.ToString(CultureInfo.InvariantCulture);
        Repluck();
    }

    partial void OnFretChanged(int value) => Repluck();

    partial void OnTensionScaleChanged(double value) => Refresh();

    partial void OnPluckPointChanged(double value) => Refresh();

    partial void OnSoloHarmonicChanged(int value)
    {
        if (value > 0 && Audible) PlayHarmonic(value);
        Refresh();
    }

    partial void OnPipeLengthChanged(double value) => Refresh();

    partial void OnPipeModeChanged(int value)
    {
        PipeModeChoice.Selected = value.ToString(CultureInfo.InvariantCulture);
        if (Audible) PlayPipe();
        Refresh();
    }

    private void OnResonatorChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<Resonator>(ResonatorChoice.Selected, out var resonator)) Resonator = resonator;
    }

    private void OnStringChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && int.TryParse(StringChoice.Selected, out var index)) StringIndex = index;
    }

    private void OnPipeModeChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && int.TryParse(PipeModeChoice.Selected, out var mode)) PipeMode = mode;
    }

    /// <summary>Plucks the string: where the diagram says it was let go, or (from the button) at the pick.</summary>
    [RelayCommand]
    private void Pluck(PluckRequest? request)
    {
        if (request is { } r)
        {
            PluckPoint = Math.Round(Math.Clamp(r.Point, 0.05, 0.95), 3);
            PluckHeight = r.Height;
        }
        else
        {
            PluckHeight = 0.8;
        }

        SoloHarmonic = 0;
        PluckCount++;
        if (Audible) PlayPluck();
        Refresh();
    }

    /// <summary>The string plucked again after a change of string or fret (silently while a walkthrough sets a step up).</summary>
    private void Repluck()
    {
        SoloHarmonic = 0;
        PluckHeight = 0.8;
        PluckCount++;
        if (Audible) PlayPluck();
        Refresh();
    }

    /// <summary>Starts the string again without a sound: for the walkthroughs.</summary>
    internal void PluckQuietly(double point)
    {
        PluckPoint = point;
        PluckHeight = 0.8;
        SoloHarmonic = 0;
        PluckCount++;
        Refresh();
    }

    /// <summary>The pipe's note, played again (the "blow" button).</summary>
    [RelayCommand]
    private void Blow() => PlayPipe();

    private void PlayPluck()
    {
        const int Count = 16;
        var amplitudes = new double[Count];
        var decays = new double[Count];
        for (var n = 1; n <= Count; n++)
        {
            amplitudes[n - 1] = SoundModel.PluckAmplitude(n, PluckPoint);
            decays[n - 1] = SoundModel.StringDecay(n);
        }

        _audio.Play(new SoundNote(StringFrequency, amplitudes, decays, new NoteEnvelope(0.002, 6, 0.3, 0, 0, 0), 0.75 * Math.Clamp(Math.Abs(PluckHeight), 0.3, 1)));
    }

    private void PlayHarmonic(int n) =>
        _audio.Play(new SoundNote(StringFrequency * n, [1], [SoundModel.StringDecay(n) * 1.5], new NoteEnvelope(0.003, 5, 0.3, 0, 0, 0), 0.55));

    private void PlayPipe()
    {
        double[] tone = Resonator == Resonator.ClosedPipe ? [1, 0.02, 0.3, 0.01, 0.12, 0.01, 0.05] : [1, 0.3, 0.12, 0.05, 0.02];
        _audio.Play(new SoundNote(PipeFrequency, tone, [], new NoteEnvelope(0.08, 1.3, 0.2, 0, 0, 0.003), 0.5));
    }

    #endregion

    #region timbre

    public OscillationChoice InstrumentChoice { get; } = new(nameof(Instrument.Fork));

    [ObservableProperty]
    private Instrument _instrument = Instrument.Fork;

    /// <summary>The overtones of the instrument (or the ones dragged by hand), the fundamental first.</summary>
    [ObservableProperty]
    private double[] _harmonics = SoundModel.Harmonics(Instrument.Fork);

    /// <summary>The key played, MIDI (60 = C4, 69 = A4).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteText))]
    private int _noteMidi = 69;

    public OscillationChoice SecondChoice { get; } = new(nameof(SecondNote.None));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBeats))]
    private SecondNote _secondNote = SecondNote.None;

    /// <summary>How far off the beating note is, Hz.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetuneText))]
    private double _detune = 3;

    /// <summary>Changed every time a note is played.</summary>
    [ObservableProperty]
    private int _noteCount;

    public bool IsBeats => SecondNote == SecondNote.Beats;

    public double NoteFrequency => SoundModel.NoteFrequency(NoteMidi);

    public string NoteText => _localization.Format("Sound_Value_Note", SoundDiagram.Hertz(NoteFrequency), SoundModel.NoteName(NoteMidi));

    public string DetuneText => _localization.Format("Sound_Value_Hz", Number(Detune, "0.0"));

    partial void OnInstrumentChanged(Instrument value)
    {
        InstrumentChoice.Selected = value.ToString();
        if (!_editingHarmonics) Harmonics = value == Instrument.Custom ? (double[])_custom.Clone() : SoundModel.Harmonics(value);
        if (Audible && !_editingHarmonics) PlayNote();
        Refresh();
    }

    partial void OnNoteMidiChanged(int value) => Refresh();

    partial void OnSecondNoteChanged(SecondNote value)
    {
        SecondChoice.Selected = value.ToString();
        if (Audible) PlayNote();
        Refresh();
    }

    partial void OnDetuneChanged(double value) => Refresh();

    private void OnInstrumentChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<Instrument>(InstrumentChoice.Selected, out var instrument)) Instrument = instrument;
    }

    private void OnSecondChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<SecondNote>(SecondChoice.Selected, out var second)) SecondNote = second;
    }

    /// <summary>Plays the key (and the second note) on the instrument.</summary>
    [RelayCommand]
    private void PlayNote()
    {
        NoteCount++;
        var envelope = SoundModel.Envelope(Instrument);
        var decays = Enumerable.Range(1, Harmonics.Length)
            .Select(n => envelope.DecayTime > 0 ? envelope.DecayTime / Math.Pow(n, envelope.OvertoneDecay) : 0)
            .ToArray();
        var second = SoundModel.SecondFrequency(NoteFrequency, SecondNote, Detune);
        var gain = second > 0 ? 0.42 : 0.6;
        _audio.Play(new SoundNote(NoteFrequency, Harmonics, decays, envelope, gain));
        if (second > 0) _audio.Play(new SoundNote(second, Harmonics, decays, envelope, gain));
    }

    /// <summary>A bar of the overtones dragged: the instrument becomes "your own", and the note plays when the drag ends.</summary>
    [RelayCommand]
    private void EditHarmonic(HarmonicEdit edit)
    {
        if (edit.Index < 0 || edit.Index >= SoundModel.HarmonicCount) return;
        if (Instrument != Instrument.Custom) _custom = (double[])Harmonics.Clone();

        // The fundamental never quite goes: a note with no fundamental at all is still heard at its pitch, but it confuses here.
        _custom[edit.Index] = Math.Round(Math.Clamp(edit.Value, edit.Index == 0 ? 0.05 : 0, 1), 2);
        _editingHarmonics = true;
        try
        {
            Instrument = Instrument.Custom;
            Harmonics = (double[])_custom.Clone();
        }
        finally
        {
            _editingHarmonics = false;
        }

        if (edit.Done) PlayNote();
    }

    /// <summary>The overtones as frequencies, e.g. "880 · 1320 · 1760 Hz".</summary>
    private string OvertoneList()
    {
        var shown = Enumerable.Range(2, 3).Select(n => Number(NoteFrequency * n, "0"));
        return _localization.Format("Sound_Value_Overtones", string.Join(" · ", shown));
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
            case SoundMode.Strings:
                if (Resonator == Resonator.String) RefreshString();
                else RefreshPipe();
                break;

            case SoundMode.Timbre:
                RefreshTimbre();
                break;

            default:
                RefreshWave();
                break;
        }
    }

    private void RefreshWave()
    {
        var speed = SoundModel.Speed(Medium, Temperature);
        var state = Medium == SoundMedium.Vacuum ? "Vacuum"
            : Frequency < SoundModel.LowestHeard ? "Infra"
            : Frequency > SoundModel.HighestHeard ? "Ultra"
            : Loudness >= SoundModel.PainThreshold - 10 ? "Pain"
            : SoundModel.IsAudible(Frequency, Loudness) ? (Frequency > 15_000 ? "High" : "Heard")
            : "Quiet";
        PhaseTitle = _localization.GetString($"Sound_State_{state}_Title");
        PhaseDescription = _localization.GetString($"Sound_State_{state}_Text");
        PhaseBrush = state switch
        {
            "Heard" => GreenBrush,
            "High" => GoldBrush,
            "Vacuum" or "Pain" => RedBrush,
            _ => CyanBrush,
        };

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Sound_Stat_Frequency"), FrequencyText),
            new(_localization.GetString("Sound_Stat_Period"), Duration(1 / Frequency)),
            new(_localization.GetString("Sound_Stat_Level"), LoudnessText),
        };

        if (speed > 0)
        {
            var pressure = SoundModel.PressureAmplitude(Loudness);
            stats.Add(new(_localization.GetString("Sound_Stat_Speed"), _localization.Format("Sound_Value_Speed", Number(speed, "0"))));
            stats.Add(new(_localization.GetString("Sound_Stat_Wavelength"), SoundDiagram.Metres(SoundModel.Wavelength(Frequency, speed))));
            stats.Add(new(_localization.GetString("Sound_Stat_PressureSwing"), _localization.Format("Sound_Value_PressureSwing",
                Significant(pressure), Significant(pressure / SoundModel.AirPressure * 100))));
            stats.Add(new(_localization.GetString("Sound_Stat_Displacement"), _localization.Format("Sound_Value_PlusMinus",
                Tiny(SoundModel.DisplacementAmplitude(Loudness, Frequency, Medium, Temperature)))));
            stats.Add(new(_localization.GetString("Sound_Stat_Intensity"), _localization.Format("Sound_Value_Intensity",
                Scientific(SoundModel.Intensity(Loudness, Medium, Temperature)))));
            stats.Add(new(_localization.GetString("Sound_Stat_Arrives"), _localization.Format("Sound_Value_Arrives",
                Number(ListenerDistance, "0"), Duration(ListenerDistance / speed))));
        }
        else
        {
            stats.Add(new(_localization.GetString("Sound_Stat_Speed"), _localization.GetString("Sound_Value_NoSpeed")));
        }

        stats.Add(new(_localization.GetString("Sound_Stat_Threshold"), _localization.Format("Sound_Value_Db", Number(SoundModel.HearingThreshold(Frequency), "0"))));
        Stats = stats;
    }

    private void RefreshString()
    {
        var length = SoundModel.FrettedLength(SoundModel.GuitarScale, Fret);
        var open = SoundModel.NoteFrequency(OpenStringMidi) * Math.Sqrt(TensionScale);
        var mu = SoundModel.MassPerMetre(SoundModel.NoteFrequency(OpenStringMidi), SoundModel.GuitarScale, SoundModel.GuitarTensions[Math.Clamp(StringIndex, 0, 5)]);
        var missing = Enumerable.Range(1, 12).Where(n => SoundModel.IsSilenced(n, PluckPoint)).ToList();
        if (SoloHarmonic > 0)
        {
            PhaseTitle = _localization.Format("Sound_String_Solo_Title", SoloHarmonic);
            PhaseDescription = _localization.GetString(SoloHarmonic == 1 ? "Sound_String_Solo1_Text" : "Sound_String_Solo_Text");
            PhaseBrush = SoloHarmonic == 1 ? GoldBrush : CyanBrush;
        }
        else
        {
            var middle = Math.Abs(PluckPoint - 0.5) < 0.03;
            PhaseTitle = _localization.GetString(middle ? "Sound_String_Middle_Title" : PluckPoint < 0.12 || PluckPoint > 0.88 ? "Sound_String_Edge_Title" : "Sound_String_Plucked_Title");
            PhaseDescription = _localization.GetString(middle ? "Sound_String_Middle_Text" : PluckPoint < 0.12 || PluckPoint > 0.88 ? "Sound_String_Edge_Text" : "Sound_String_Plucked_Text");
            PhaseBrush = middle ? VioletBrush : GoldBrush;
        }

        Stats =
        [
            new(_localization.GetString("Sound_Stat_String"), _localization.Format("Sound_Value_String", 6 - StringIndex, SoundModel.NoteName(OpenStringMidi))),
            new(_localization.GetString("Sound_Stat_Fret"), FretText),
            new(_localization.GetString("Sound_Stat_Length"), SoundDiagram.Metres(length)),
            new(_localization.GetString("Sound_Stat_Tension"), TensionText),
            new(_localization.GetString("Sound_Stat_Mass"), _localization.Format("Sound_Value_GramsPerMetre", Number(mu * 1000, "0.0"))),
            new(_localization.GetString("Sound_Stat_StringSpeed"), _localization.Format("Sound_Value_Speed", Number(SoundModel.StringWaveSpeed(Tension, mu), "0"))),
            new(_localization.GetString("Sound_Stat_Fundamental"), _localization.Format("Sound_Value_Note", SoundDiagram.Hertz(StringFrequency), SoundModel.NoteName(StringFrequency))),
            new(_localization.GetString("Sound_Stat_Open"), SoundDiagram.Hertz(open)),
            new(_localization.GetString("Sound_Stat_PluckedAt"), PluckText),
            new(_localization.GetString("Sound_Stat_Missing"), missing.Count == 0
                ? _localization.GetString("Sound_Value_None")
                : string.Join(", ", missing.Select(n => n.ToString(CultureInfo.InvariantCulture)))),
        ];
    }

    private void RefreshPipe()
    {
        var closed = Resonator == Resonator.ClosedPipe;
        var speed = SoundModel.SpeedInAir(Temperature);
        var harmonic = SoundModel.PipeHarmonic(PipeMode, closed);
        PhaseTitle = _localization.GetString(closed ? "Sound_Pipe_Closed_Title" : "Sound_Pipe_Open_Title");
        PhaseDescription = _localization.GetString(closed ? "Sound_Pipe_Closed_Text" : "Sound_Pipe_Open_Text");
        PhaseBrush = closed ? GoldBrush : GreenBrush;
        Stats =
        [
            new(_localization.GetString("Sound_Stat_PipeLength"), PipeLengthText),
            new(_localization.GetString("Sound_Stat_Ends"), _localization.GetString(closed ? "Sound_Value_OneClosed" : "Sound_Value_BothOpen")),
            new(_localization.GetString("Sound_Stat_Resonance"), _localization.Format("Sound_Value_Resonance", PipeMode, harmonic)),
            new(_localization.GetString("Sound_Stat_Frequency"), _localization.Format("Sound_Value_Note", SoundDiagram.Hertz(PipeFrequency), SoundModel.NoteName(PipeFrequency))),
            new(_localization.GetString("Sound_Stat_Wavelength"), SoundDiagram.Metres(SoundModel.Wavelength(PipeFrequency, speed))),
            new(_localization.GetString("Sound_Stat_Speed"), _localization.Format("Sound_Value_SpeedAt", Number(speed, "0"), Number(Temperature, "0"))),
            new(_localization.GetString("Sound_Stat_Allowed"), _localization.GetString(closed ? "Sound_Chart_OddOnly" : "Sound_Chart_AllHarmonics")),
            new(_localization.GetString("Sound_Stat_EndCorrection"), SoundDiagram.Metres(SoundModel.EffectiveLength(PipeLength, closed) - PipeLength)),
        ];
    }

    private void RefreshTimbre()
    {
        PhaseTitle = _localization.GetString(SoundDiagram.InstrumentKey(Instrument));
        PhaseDescription = _localization.GetString(Instrument switch
        {
            Instrument.Flute => "Sound_Timbre_Flute_Text",
            Instrument.Clarinet => "Sound_Timbre_Clarinet_Text",
            Instrument.Violin => "Sound_Timbre_Violin_Text",
            Instrument.Trumpet => "Sound_Timbre_Trumpet_Text",
            Instrument.Piano => "Sound_Timbre_Piano_Text",
            Instrument.Custom => "Sound_Timbre_Custom_Text",
            _ => "Sound_Timbre_Fork_Text",
        });
        PhaseBrush = Instrument == Instrument.Fork ? CyanBrush : Instrument == Instrument.Custom ? VioletBrush : GoldBrush;

        var strongest = 1;
        for (var n = 2; n <= Harmonics.Length; n++)
        {
            if (Harmonics[n - 1] > Harmonics[strongest - 1]) strongest = n;
        }

        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Sound_Stat_Note"), NoteText),
            new(_localization.GetString("Sound_Stat_Period"), Duration(1 / NoteFrequency)),
            new(_localization.GetString("Sound_Stat_WavelengthAir"), SoundDiagram.Metres(SoundModel.Wavelength(NoteFrequency, SoundModel.SpeedInAir(20)))),
            new(_localization.GetString("Sound_Stat_Overtones"), OvertoneList()),
            new(_localization.GetString("Sound_Stat_Strongest"), _localization.Format("Sound_Value_Harmonic", strongest)),
            new(_localization.GetString("Sound_Stat_KeyStep"), _localization.GetString("Sound_Value_KeyStep")),
        };

        var second = SoundModel.SecondFrequency(NoteFrequency, SecondNote, Detune);
        switch (SecondNote)
        {
            case SecondNote.Beats:
                stats.Add(new(_localization.GetString("Sound_Stat_Second"), SoundDiagram.Hertz(second)));
                stats.Add(new(_localization.GetString("Sound_Stat_Beats"), _localization.Format("Sound_Value_Beats", Number(Detune, "0.0"))));
                break;

            case SecondNote.Octave:
            case SecondNote.Fifth:
                stats.Add(new(_localization.GetString("Sound_Stat_Second"), _localization.Format("Sound_Value_Note", SoundDiagram.Hertz(second), SoundModel.NoteName(NoteMidi + SoundModel.SecondSemitones(SecondNote)))));
                stats.Add(new(_localization.GetString("Sound_Stat_Ratio"), _localization.Format(SecondNote == SecondNote.Octave ? "Sound_Value_RatioOctave" : "Sound_Value_RatioFifth", Number(second / NoteFrequency, "0.000"))));
                break;
        }

        Stats = stats;
    }

    /// <summary>A duration in the handiest unit: µs, ms or s.</summary>
    private string Duration(double seconds) => seconds switch
    {
        < 1e-3 => _localization.Format("Sound_Value_Us", Number(seconds * 1e6, "0")),
        < 1 => _localization.Format("Sound_Value_Ms", Number(seconds * 1e3, seconds < 0.01 ? "0.00" : seconds < 0.1 ? "0.0" : "0")),
        _ => _localization.Format("Sound_Value_S", Number(seconds, "0.0")),
    };

    /// <summary>A tiny length: pm, nm, µm or mm.</summary>
    private string Tiny(double metres) => metres switch
    {
        < 1e-9 => _localization.Format("Sound_Value_Pm", Significant(metres * 1e12)),
        < 1e-6 => _localization.Format("Sound_Value_Nm", Significant(metres * 1e9)),
        < 1e-3 => _localization.Format("Sound_Value_Um", Significant(metres * 1e6)),
        _ => _localization.Format("Sound_Value_Mm", Significant(metres * 1e3)),
    };

    /// <summary>A number to two significant figures, written out (0.00028, 2.8, 280).</summary>
    private static string Significant(double value)
    {
        if (value <= 0) return "0";
        var digits = Math.Max(0, 1 - (int)Math.Floor(Math.Log10(value)));
        return Math.Round(value, Math.Min(digits, 12)).ToString(digits > 0 ? "0." + new string('#', digits) : "0", CultureInfo.InvariantCulture);
    }

    /// <summary>A number as m × 10ⁿ with superscript digits.</summary>
    private static string Scientific(double value)
    {
        if (value <= 0) return "0";
        var exponent = (int)Math.Floor(Math.Log10(value));
        var mantissa = value / Math.Pow(10, exponent);
        const string Superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var power = string.Concat(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture).Select(c => Superscripts[c - '0']));
        return $"{Number(mantissa, "0.0")}×10{(exponent < 0 ? "⁻" : string.Empty)}{power}";
    }

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
        OnPropertyChanged(nameof(FrequencyText));
        OnPropertyChanged(nameof(LoudnessText));
        OnPropertyChanged(nameof(TemperatureText));
        OnPropertyChanged(nameof(FretText));
        OnPropertyChanged(nameof(TensionText));
        OnPropertyChanged(nameof(PluckText));
        OnPropertyChanged(nameof(PipeLengthText));
        OnPropertyChanged(nameof(NoteText));
        OnPropertyChanged(nameof(DetuneText));
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        _audio.Dispose();
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        FrequencyChoice.PropertyChanged -= OnFrequencyChosen;
        MediumChoice.PropertyChanged -= OnMediumChosen;
        ResonatorChoice.PropertyChanged -= OnResonatorChosen;
        StringChoice.PropertyChanged -= OnStringChosen;
        PipeModeChoice.PropertyChanged -= OnPipeModeChosen;
        InstrumentChoice.PropertyChanged -= OnInstrumentChosen;
        SecondChoice.PropertyChanged -= OnSecondChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
