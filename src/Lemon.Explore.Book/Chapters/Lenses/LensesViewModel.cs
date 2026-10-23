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

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// The lenses chapter: which view is shown; the bench's lens (convex or concave, focal length), candle, screen and how the
/// light is drawn; the eye's chart distance, age, short or long sight, glasses and light; the camera's focal length,
/// aperture, focus, shutter, ISO and the scene's light; and what is happening. The diagram (<see cref="LensDiagram"/>)
/// pushes back what is dragged. <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing
/// in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class LensesViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);

    /// <summary>The slider ends that stand for infinity (log₁₀ of metres).</summary>
    private const double EyeInfinityLog = 2.0;

    private const double FocusInfinityLog = 2.3;

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    public LensesViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => LensesLessons.For(this));
        Card = new ObjectCard(narration, Lesson, LensDiagram.ObjectTitleKey, LensDiagram.ObjectClipKey);
        KindChoice.PropertyChanged += OnKindChosen;
        RayChoice.PropertyChanged += OnRayChosen;
        GlassesChoice.PropertyChanged += OnGlassesChosen;
        LightChoice.PropertyChanged += OnLightChosen;
        SceneChoice.PropertyChanged += OnSceneChosen;
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
    [NotifyPropertyChangedFor(nameof(IsBench), nameof(IsEye), nameof(IsCamera), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private LensMode _mode;

    public bool IsBench
    {
        get => Mode == LensMode.Bench;
        set
        {
            if (value) Mode = LensMode.Bench;
        }
    }

    public bool IsEye
    {
        get => Mode == LensMode.Eye;
        set
        {
            if (value) Mode = LensMode.Eye;
        }
    }

    public bool IsCamera
    {
        get => Mode == LensMode.Camera;
        set
        {
            if (value) Mode = LensMode.Camera;
        }
    }

    public string Subtitle => _localization.GetString($"Lens_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Lens_{Mode}_Note");

    public string Hint => _localization.GetString($"Lens_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Lens_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Lens_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Lens_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(LensMode value)
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

    #region the bench

    public OscillationChoice KindChoice { get; } = new(nameof(LensKind.Convex));

    [ObservableProperty]
    private LensKind _lensKind = LensKind.Convex;

    /// <summary>The lens's focal length, cm (its size).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocalText))]
    private double _focalLength = 15;

    /// <summary>The candle's distance from the lens, cm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ObjectText))]
    private double _objectDistance = 45;

    /// <summary>The candle's height to the flame's tip, cm.</summary>
    [ObservableProperty]
    private double _objectHeight = 3;

    /// <summary>The screen's distance behind the lens, cm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScreenText))]
    private double _screenDistance = 22.5;

    [ObservableProperty]
    private bool _showScreen = true;

    public OscillationChoice RayChoice { get; } = new(nameof(Chapters.Lenses.RayStyle.Principal));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSunlight))]
    private RayStyle _rayStyle = RayStyle.Principal;

    [ObservableProperty]
    private bool _showColours;

    public bool IsSunlight => RayStyle == RayStyle.Sunlight;

    /// <summary>The focal length with its sign: negative for a concave lens.</summary>
    public double SignedFocal => LensKind == LensKind.Convex ? FocalLength : -FocalLength;

    /// <summary>Where the image is, cm behind the lens (negative: a virtual image on the candle's side).</summary>
    public double ImageDistance => IsSunlight ? SignedFocal : LensModel.ImageDistance(SignedFocal, ObjectDistance);

    public string FocalText => _localization.Format("Lens_Value_Focal", LensDiagram.Centimetres(FocalLength), Number(LensModel.Dioptres(SignedFocal), "+0.0;-0.0"));

    public string ObjectText => LensDiagram.Centimetres(ObjectDistance);

    public string ScreenText => LensDiagram.Centimetres(ScreenDistance);

    partial void OnLensKindChanged(LensKind value)
    {
        KindChoice.Selected = value.ToString();
        OnPropertyChanged(nameof(FocalText));
        Refresh();
    }

    partial void OnFocalLengthChanged(double value) => Refresh();

    partial void OnObjectDistanceChanged(double value) => Refresh();

    partial void OnObjectHeightChanged(double value) => Refresh();

    partial void OnScreenDistanceChanged(double value) => Refresh();

    partial void OnShowScreenChanged(bool value) => Refresh();

    partial void OnRayStyleChanged(RayStyle value)
    {
        RayChoice.Selected = value.ToString();
        Refresh();
    }

    private void OnKindChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<LensKind>(KindChoice.Selected, out var kind)) LensKind = kind;
    }

    private void OnRayChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<RayStyle>(RayChoice.Selected, out var style)) RayStyle = style;
    }

    /// <summary>
    /// Puts the candle where a camera (3 f), a copier (2 f), a projector (1.5 f) or a magnifying glass (0.6 f) has its
    /// subject, with a convex lens; a real image is caught on the screen.
    /// </summary>
    [RelayCommand]
    private void Preset(string? use)
    {
        var factor = use switch
        {
            "Copier" => 2.0,
            "Projector" => 1.5,
            "Magnifier" => 0.6,
            _ => 3.0,
        };
        LensKind = LensKind.Convex;
        if (RayStyle == RayStyle.Sunlight) RayStyle = RayStyle.Principal;
        ObjectDistance = Math.Round(Math.Clamp(FocalLength * factor, 3, 68) * 2) / 2;
        FindImage();
    }

    /// <summary>Moves the screen to where the image is, if there is one to catch.</summary>
    [RelayCommand]
    private void FindImage()
    {
        var image = ImageDistance;
        if (image > 3 && image <= 88)
        {
            ShowScreen = true;
            ScreenDistance = Math.Round(image * 2) / 2;
        }
    }

    #endregion

    #region the eye

    /// <summary>How far the chart is, m (+∞: far away).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistanceText), nameof(DistanceLog))]
    private double _distance = double.PositiveInfinity;

    /// <summary>Age, years.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AgeText))]
    private double _age = 20;

    /// <summary>Short sight (negative) or long sight (positive), dioptres.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorText))]
    private double _error;

    public OscillationChoice GlassesChoice { get; } = new(nameof(Spectacles.None));

    [ObservableProperty]
    private Spectacles _glasses = Spectacles.None;

    public OscillationChoice LightChoice { get; } = new(nameof(LightLevel.Indoor));

    [ObservableProperty]
    private LightLevel _light = LightLevel.Indoor;

    /// <summary>The distance slider: log₁₀ of metres from 7 cm, its far end infinity.</summary>
    public double DistanceLog
    {
        get => double.IsPositiveInfinity(Distance) ? EyeInfinityLog : Math.Log10(Distance);
        set => Distance = value >= EyeInfinityLog - 0.05 ? double.PositiveInfinity : Tidy(Math.Pow(10, value));
    }

    public string DistanceText => LensDiagram.Metres(Distance);

    public string AgeText => _localization.Format("Lens_Value_Age", Number(Age, "0"));

    public string ErrorText => Math.Abs(Error) < 0.01
        ? _localization.GetString("Lens_Value_NoError")
        : _localization.Format(Error < 0 ? "Lens_Value_Myopia" : "Lens_Value_Hyperopia", Number(Error, "+0.00;-0.00"));

    /// <summary>What reaches the eye and what it does with it now.</summary>
    public EyeFocus EyeFocus => LensModel.Focus(Error, Age, LensModel.VergenceAtEye(Distance, GlassesPower));

    public double GlassesPower => LensModel.GlassesPower(Glasses, Error);

    partial void OnDistanceChanged(double value) => Refresh();

    partial void OnAgeChanged(double value) => Refresh();

    partial void OnErrorChanged(double value) => Refresh();

    partial void OnGlassesChanged(Spectacles value)
    {
        GlassesChoice.Selected = value.ToString();
        Refresh();
    }

    partial void OnLightChanged(LightLevel value)
    {
        LightChoice.Selected = value.ToString();
        Refresh();
    }

    private void OnGlassesChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<Spectacles>(GlassesChoice.Selected, out var glasses)) Glasses = glasses;
    }

    private void OnLightChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<LightLevel>(LightChoice.Selected, out var light)) Light = light;
    }

    /// <summary>Puts the chart at a distance: "Inf" or metres.</summary>
    [RelayCommand]
    private void SetDistance(string? metres) =>
        Distance = metres is null || metres == "Inf" ? double.PositiveInfinity : double.Parse(metres, CultureInfo.InvariantCulture);

    /// <summary>An eye that sees normally, a short-sighted one (−3 D) or a long-sighted one (+2 D).</summary>
    [RelayCommand]
    private void SetError(string? dioptres) => Error = double.Parse(dioptres ?? "0", CultureInfo.InvariantCulture);

    #endregion

    #region the camera

    /// <summary>The lens's focal length, mm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocalMmText), nameof(FocalLog))]
    private double _focalMm = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FNumberText), nameof(FStopIndex))]
    private double _fNumber = 5.6;

    /// <summary>Where the lens is focused, m (+∞: infinity).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocusText), nameof(FocusLog))]
    private double _focusDistance = 5;

    /// <summary>The shutter time, s.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShutterText), nameof(ShutterIndex))]
    private double _shutter = 1.0 / 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsoText), nameof(IsoIndex))]
    private int _iso = 100;

    public OscillationChoice SceneChoice { get; } = new(nameof(SceneLight.Sun));

    [ObservableProperty]
    private SceneLight _scene = SceneLight.Sun;

    /// <summary>The focal-length slider: log₁₀ of mm, 18 to 200.</summary>
    public double FocalLog
    {
        get => Math.Log10(FocalMm);
        set => FocalMm = Math.Round(Math.Pow(10, Math.Clamp(value, Math.Log10(18), Math.Log10(200))));
    }

    /// <summary>The f-number as a stop: 0 (f/1.4) to 8 (f/22).</summary>
    public int FStopIndex
    {
        get => Nearest(LensModel.FStops, FNumber);
        set => FNumber = LensModel.FStops[Math.Clamp(value, 0, LensModel.FStops.Length - 1)];
    }

    public int ShutterIndex
    {
        get => Nearest(LensModel.Shutters, Shutter);
        set => Shutter = LensModel.Shutters[Math.Clamp(value, 0, LensModel.Shutters.Length - 1)];
    }

    public int IsoIndex
    {
        get => Nearest(LensModel.Isos.Select(i => (double)i).ToArray(), Iso);
        set => Iso = LensModel.Isos[Math.Clamp(value, 0, LensModel.Isos.Length - 1)];
    }

    /// <summary>The focus slider: log₁₀ of metres from 30 cm, its far end infinity.</summary>
    public double FocusLog
    {
        get => double.IsPositiveInfinity(FocusDistance) ? FocusInfinityLog : Math.Log10(FocusDistance);
        set => FocusDistance = value >= FocusInfinityLog - 0.08 ? double.PositiveInfinity : Tidy(Math.Pow(10, value));
    }

    public string FocalMmText => _localization.Format("Lens_Value_Mm", Number(FocalMm, "0"));

    public string FNumberText => LensDiagram.FText(FNumber);

    public string FocusText => LensDiagram.Metres(FocusDistance);

    public string ShutterText => LensDiagram.ShutterText(Shutter);

    public string IsoText => Iso.ToString(CultureInfo.InvariantCulture);

    /// <summary>How many stops too bright (negative: too dark) the photo is.</summary>
    public double ExposureError => LensModel.ExposureError(Scene, FNumber, Shutter, Iso);

    partial void OnFocalMmChanged(double value) => Refresh();

    partial void OnFNumberChanged(double value) => Refresh();

    partial void OnFocusDistanceChanged(double value) => Refresh();

    partial void OnShutterChanged(double value) => Refresh();

    partial void OnIsoChanged(int value) => Refresh();

    partial void OnSceneChanged(SceneLight value)
    {
        SceneChoice.Selected = value.ToString();
        Refresh();
    }

    private void OnSceneChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<SceneLight>(SceneChoice.Selected, out var scene)) Scene = scene;
    }

    /// <summary>Picks the shutter time that exposes this scene right at this aperture and ISO (as a camera's automatic mode does).</summary>
    [RelayCommand]
    private void AutoExpose()
    {
        var best = 0;
        for (var i = 1; i < LensModel.Shutters.Length; i++)
        {
            if (Math.Abs(LensModel.ExposureError(Scene, FNumber, LensModel.Shutters[i], Iso)) < Math.Abs(LensModel.ExposureError(Scene, FNumber, LensModel.Shutters[best], Iso))) best = i;
        }

        Shutter = LensModel.Shutters[best];
    }

    private static int Nearest(double[] values, double value)
    {
        var best = 0;
        for (var i = 1; i < values.Length; i++)
        {
            if (Math.Abs(Math.Log(values[i] / value)) < Math.Abs(Math.Log(values[best] / value))) best = i;
        }

        return best;
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
            case LensMode.Eye:
                RefreshEye();
                break;

            case LensMode.Camera:
                RefreshCamera();
                break;

            default:
                RefreshBench();
                break;
        }
    }

    /// <summary>What the lens makes of the candle: "Bench_Smaller" … "Bench_Concave", "Bench_Sunlight".</summary>
    public string BenchState
    {
        get
        {
            if (IsSunlight) return "Sunlight";
            if (LensKind == LensKind.Concave) return "Concave";
            var x = ObjectDistance / FocalLength;
            return x switch
            {
                > 2.04 => "Smaller",
                >= 1.96 => "Same",
                > 1.02 => "Larger",
                >= 0.98 => "None",
                _ => "Magnifier",
            };
        }
    }

    private void RefreshBench()
    {
        var state = BenchState;
        PhaseTitle = _localization.GetString($"Lens_Bench_{state}_Title");
        PhaseDescription = _localization.GetString($"Lens_Bench_{state}_Text");
        PhaseBrush = state switch
        {
            "Magnifier" or "Concave" => CyanBrush,
            "None" => OrangeBrush,
            "Same" => GreenBrush,
            _ => GoldBrush,
        };

        var f = SignedFocal;
        var stats = new List<OscillationStat>
        {
            new(_localization.GetString("Lens_Stat_Focal"), FocalText),
        };

        if (IsSunlight)
        {
            stats.Add(new(_localization.GetString("Lens_Stat_Image"), _localization.GetString(f > 0 ? "Lens_Value_AtF" : "Lens_Value_FromF")));
        }
        else
        {
            var u = ObjectDistance;
            var v = LensModel.ImageDistance(f, u);
            var m = LensModel.Magnification(f, u);
            stats.Add(new(_localization.GetString("Lens_Stat_U"), LensDiagram.Centimetres(u)));
            if (double.IsFinite(v) && Math.Abs(v) < 5000)
            {
                stats.Add(new(_localization.GetString("Lens_Stat_V"), v > 0
                    ? _localization.Format("Lens_Value_Behind", LensDiagram.Centimetres(v))
                    : _localization.Format("Lens_Value_InFront", LensDiagram.Centimetres(-v))));
                stats.Add(new(_localization.GetString("Lens_Stat_Magnification"), _localization.Format(m < 0 ? "Lens_Value_Inverted" : "Lens_Value_Upright", Number(Math.Abs(m), Math.Abs(m) < 10 ? "0.00" : "0"))));
                stats.Add(new(_localization.GetString("Lens_Stat_ImageHeight"), LensDiagram.Centimetres(Math.Abs(m * ObjectHeight))));
                stats.Add(new(_localization.GetString("Lens_Stat_Equation"), $"1/{Number(u, "0.#")} + 1/{Number(v, "0.#")} = 1/{Number(f, "0.#")}"));
            }
            else
            {
                stats.Add(new(_localization.GetString("Lens_Stat_V"), _localization.GetString("Lens_Value_Infinity")));
            }
        }

        if (ShowScreen)
        {
            var blur = IsSunlight
                ? LensModel.BlurOnScreen(f, 1e9, LensDiagram.LensAperture, ScreenDistance)
                : LensModel.BlurOnScreen(f, ObjectDistance, LensDiagram.LensAperture, ScreenDistance);
            stats.Add(new(_localization.GetString("Lens_Stat_Screen"), ScreenText));
            stats.Add(new(_localization.GetString("Lens_Stat_Blur"), blur < 0.15 ? _localization.GetString("Lens_Value_Sharp") : LensDiagram.Centimetres(blur)));
        }

        stats.Add(new(_localization.GetString("Lens_Stat_Power"), _localization.Format("Lens_Value_Dioptres", Number(LensModel.Dioptres(f), "+0.0;-0.0"))));
        Stats = stats;
    }

    /// <summary>How the eye is doing: "Sharp", "SharpNear", "Front", "TooClose", "Behind".</summary>
    public string EyeState
    {
        get
        {
            var focus = EyeFocus;
            if (Math.Abs(focus.Defocus) < 0.25) return focus.Used > 0.25 ? "SharpNear" : "Sharp";
            if (focus.Defocus > 0) return "Front";

            // Behind the retina: too close if the eye (with its glasses) could still focus far away, else long sight.
            var farNeeds = Error - LensModel.VergenceAtEye(double.PositiveInfinity, GlassesPower);
            return farNeeds <= focus.Amplitude ? "TooClose" : "Behind";
        }
    }

    private void RefreshEye()
    {
        var state = EyeState;
        PhaseTitle = _localization.GetString($"Lens_Eye_{state}_Title");
        PhaseDescription = _localization.GetString($"Lens_Eye_{state}_Text");
        PhaseBrush = state switch
        {
            "Sharp" or "SharpNear" => GreenBrush,
            "Front" => CyanBrush,
            _ => OrangeBrush,
        };

        var focus = EyeFocus;
        var glasses = GlassesPower;
        var pupil = LensModel.Pupil(Light);
        var blur = LensModel.BlurAngle(focus.Defocus, pupil) * 180 / Math.PI * 60;
        var detail = LensModel.SmallestDetail(blur);
        var near = LensModel.DistanceFor(Error - focus.Amplitude, glasses);
        var far = LensModel.DistanceFor(Error, glasses);
        var length = LensModel.AxialLength(Error) * 1000;
        Stats =
        [
            new(_localization.GetString("Lens_Stat_Distance"), DistanceText),
            new(_localization.GetString("Lens_Stat_Accommodation"), _localization.Format("Lens_Value_Accommodation", Number(focus.Used, "0.0"), Number(focus.Amplitude, "0.0"))),
            new(_localization.GetString("Lens_Stat_LensPower"), _localization.Format("Lens_Value_Dioptres", Number(LensModel.LensPower(length / 1000, focus.Focused), "0.0"))),
            new(_localization.GetString("Lens_Stat_NearPoint"), double.IsNaN(near) ? _localization.GetString("Lens_Value_None") : LensDiagram.Metres(near)),
            new(_localization.GetString("Lens_Stat_FarPoint"), double.IsNaN(far) ? _localization.GetString("Lens_Value_BeyondInfinity") : LensDiagram.Metres(far)),
            new(_localization.GetString("Lens_Stat_EyeLength"), _localization.Format("Lens_Value_Mm", Number(length, "0.0"))),
            new(_localization.GetString("Lens_Stat_Glasses"), Glasses == Spectacles.None ? _localization.GetString("Lens_Value_None") : _localization.Format("Lens_Value_Dioptres", Number(glasses, "+0.00;-0.00"))),
            new(_localization.GetString("Lens_Stat_FocusError"), Math.Abs(focus.Defocus) < 0.25 ? _localization.GetString("Lens_Value_Sharp") : _localization.Format("Lens_Value_Dioptres", Number(Math.Abs(focus.Defocus), "0.00"))),
            new(_localization.GetString("Lens_Stat_Pupil"), _localization.Format("Lens_Value_Mm", Number(pupil * 1000, "0.0"))),
            new(_localization.GetString("Lens_Stat_Acuity"), _localization.Format("Lens_Value_Acuity", Number(LensModel.FivePointScore(detail), "0.0"), Number(20 * detail, "0"))),
        ];
    }

    private void RefreshCamera()
    {
        var error = ExposureError;
        var state = error >= 0.7 ? "Bright" : error <= -0.7 ? "Dark" : "Good";
        PhaseTitle = _localization.GetString($"Lens_Camera_{state}_Title");
        var (near, far) = LensModel.DepthOfField(FocalMm, FNumber, FocusDistance);
        var sharp = double.IsPositiveInfinity(far)
            ? _localization.Format("Lens_Camera_SharpToInfinity", LensDiagram.Metres(near))
            : _localization.Format("Lens_Camera_SharpRange", LensDiagram.Metres(near), LensDiagram.Metres(far));
        PhaseDescription = _localization.GetString($"Lens_Camera_{state}_Text") + " " + sharp;
        PhaseBrush = state == "Good" ? GreenBrush : state == "Bright" ? GoldBrush : RedBrush;

        // The ball crosses at 4 m/s: its smear in ball widths is the same at any focal length.
        var smear = 4.0 * Shutter / CameraPhoto.BallSize;
        Stats =
        [
            new(_localization.GetString("Lens_Stat_CameraFocal"), FocalMmText),
            new(_localization.GetString("Lens_Stat_Fov"), _localization.Format("Lens_Value_Degrees", Number(LensModel.FieldOfView(FocalMm), "0"))),
            new(_localization.GetString("Lens_Stat_Aperture"), _localization.Format("Lens_Value_ApertureSize", FNumberText, Number(FocalMm / FNumber, "0.0"))),
            new(_localization.GetString("Lens_Stat_Focus"), FocusText),
            new(_localization.GetString("Lens_Stat_Dof"), double.IsPositiveInfinity(far)
                ? _localization.Format("Lens_Value_RangeToInfinity", LensDiagram.Metres(near))
                : _localization.Format("Lens_Value_Range", LensDiagram.Metres(near), LensDiagram.Metres(far))),
            new(_localization.GetString("Lens_Stat_Hyperfocal"), LensDiagram.Metres(LensModel.Hyperfocal(FocalMm, FNumber))),
            new(_localization.GetString("Lens_Stat_LensToSensor"), _localization.Format("Lens_Value_Mm", Number(LensModel.SensorDistance(FocalMm, FocusDistance), "0.0"))),
            new(_localization.GetString("Lens_Stat_Shutter"), ShutterText),
            new(_localization.GetString("Lens_Stat_Iso"), IsoText),
            new(_localization.GetString("Lens_Stat_Exposure"), Math.Abs(error) < 0.2
                ? _localization.GetString("Lens_Value_ExposureRight")
                : _localization.Format(error > 0 ? "Lens_Value_Over" : "Lens_Value_Under", Number(Math.Abs(error), "0.0"))),
            new(_localization.GetString("Lens_Stat_Smear"), _localization.Format("Lens_Value_Smear", Number(smear, "0.0"))),
        ];
    }

    #endregion

    /// <summary>A distance rounded to two significant figures.</summary>
    private static double Tidy(double metres)
    {
        var digits = Math.Max(0, 1 - (int)Math.Floor(Math.Log10(metres)));
        return Math.Round(metres, Math.Min(digits, 6));
    }

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
        OnPropertyChanged(nameof(FocalText));
        OnPropertyChanged(nameof(ObjectText));
        OnPropertyChanged(nameof(ScreenText));
        OnPropertyChanged(nameof(DistanceText));
        OnPropertyChanged(nameof(AgeText));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(FocalMmText));
        OnPropertyChanged(nameof(FocusText));
        OnPropertyChanged(nameof(ShutterText));
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        KindChoice.PropertyChanged -= OnKindChosen;
        RayChoice.PropertyChanged -= OnRayChosen;
        GlassesChoice.PropertyChanged -= OnGlassesChosen;
        LightChoice.PropertyChanged -= OnLightChosen;
        SceneChoice.PropertyChanged -= OnSceneChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
