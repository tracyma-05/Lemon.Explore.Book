using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The ice-age page: which view is shown; the time, shared by all three, and how fast it plays; which orbital cycle the
/// orbit view shows; what each view draws; and what is happening. The diagram (<see cref="IceAgeDiagram"/>) plays the
/// time and reports it back. <see cref="Lesson"/> is the spoken walkthrough of the current view, and clicking a thing in
/// the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class IceAgeViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);
    private static readonly IBrush IceBrush = Frozen(0xDD, 0xEE, 0xFA);
    private static readonly IBrush BlueBrush = Frozen(0x5A, 0xA8, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    /// <summary>Time moves every frame while playing; the card is rebuilt a few times a second, or when its state changes.</summary>
    private readonly Stopwatch _sinceRefresh = Stopwatch.StartNew();

    /// <summary>Brings the card up to date shortly after a burst of changes stops (the last change of a drag or a step).</summary>
    private readonly Avalonia.Threading.DispatcherTimer _lateRefresh = new() { Interval = TimeSpan.FromSeconds(0.3) };

    private string _state = string.Empty;

    public IceAgeViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => IceAgeLessons.For(this));
        Card = new ObjectCard(narration, Lesson, IceAgeDiagram.ObjectTitleKey, IceAgeDiagram.ObjectClipKey);
        CycleChoice.PropertyChanged += OnCycleChosen;
        SpeedChoice.PropertyChanged += OnSpeedChosen;
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
    [NotifyPropertyChangedFor(nameof(IsOrbit), nameof(IsSunshine), nameof(IsSheet), nameof(Subtitle), nameof(ScaleNote),
        nameof(Hint), nameof(Facts), nameof(ChartCaption), nameof(ChartNote))]
    private IceAgeMode _mode;

    public bool IsOrbit
    {
        get => Mode == IceAgeMode.Orbit;
        set
        {
            if (value) Mode = IceAgeMode.Orbit;
        }
    }

    public bool IsSunshine
    {
        get => Mode == IceAgeMode.Sunshine;
        set
        {
            if (value) Mode = IceAgeMode.Sunshine;
        }
    }

    public bool IsSheet
    {
        get => Mode == IceAgeMode.Sheet;
        set
        {
            if (value) Mode = IceAgeMode.Sheet;
        }
    }

    public string Subtitle => _localization.GetString($"Ice_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Ice_{Mode}_Note");

    public string Hint => _localization.GetString($"Ice_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Ice_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Ice_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Ice_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(IceAgeMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        IsPlaying = false;
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

    #endregion

    #region time

    /// <summary>The time shown, thousand years from 1950 (negative: ago).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText))]
    private double _timeKa;

    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>Thousand years per second while playing.</summary>
    [ObservableProperty]
    private double _speed = 5;

    public string TimeText => IceAgeDiagram.TimeText(TimeKa);

    public OscillationChoice SpeedChoice { get; } = new("5");

    partial void OnTimeKaChanged(double value) => RefreshSoon();

    private void OnSpeedChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected)
            && double.TryParse(SpeedChoice.Selected, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed)) Speed = speed;
    }

    partial void OnSpeedChanged(double value) => SpeedChoice.Selected = Number(value, "0");

    [RelayCommand]
    private void TogglePlay()
    {
        if (!IsPlaying) TimeKa = IceAgeDiagram.PlayFrom(TimeKa);
        IsPlaying = !IsPlaying;
    }

    /// <summary>Goes to a moment (thousand years from 1950, as text) and stops there.</summary>
    [RelayCommand]
    public void JumpTo(string? kyr)
    {
        if (double.TryParse(kyr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Hold(value);
    }

    /// <summary>Goes to <paramref name="kyr"/> and stops there.</summary>
    public void Hold(double kyr)
    {
        IsPlaying = false;
        TimeKa = Math.Clamp(kyr, IceRecord.Start, IceRecord.End);
    }

    /// <summary>Plays from <paramref name="kyr"/> at <paramref name="speed"/> thousand years a second.</summary>
    public void PlayFrom(double kyr, double speed)
    {
        Speed = speed;
        TimeKa = Math.Clamp(kyr, IceRecord.Start, IceRecord.End);
        IsPlaying = true;
    }

    #endregion

    #region what is drawn

    [ObservableProperty]
    private IceAgeCycle _cycle = IceAgeCycle.All;

    public OscillationChoice CycleChoice { get; } = new(nameof(IceAgeCycle.All));

    partial void OnCycleChanged(IceAgeCycle value)
    {
        CycleChoice.Selected = value.ToString();
        RefreshSoon(force: true);
    }

    private void OnCycleChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<IceAgeCycle>(CycleChoice.Selected, out var cycle)) Cycle = cycle;
    }

    [ObservableProperty]
    private bool _exaggerate = true;

    [ObservableProperty]
    private bool _showTerminations = true;

    [ObservableProperty]
    private bool _showArrows = true;

    [ObservableProperty]
    private bool _showGround = true;

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

    /// <summary>The model now.</summary>
    public IceMoment Moment => IceRecord.Instance.At(TimeKa);

    /// <summary>The ice: "Warm", "Growing", "Holding" (a glacial, the ice not growing), "Full" or "Melting" (a termination).</summary>
    public string IceStateName
    {
        get
        {
            var moment = Moment;
            var trend = IceRecord.Instance.IceTrend(TimeKa);
            return moment.State switch
            {
                IceState.Interglacial => moment.IceShare >= 0.35 && trend < 0 ? "Melting" : "Warm",
                IceState.Glacial => trend > 0.004 ? "Growing" : "Holding",
                _ => "Full",
            };
        }
    }

    /// <summary>The slice: "None" (no sheet on it), "Growing", "Melting" or "Steady".</summary>
    public string SheetStateName
    {
        get
        {
            var trend = IceRecord.Instance.IceTrend(TimeKa);
            if (!IceAgeDiagram.Sheet(Moment.IceShare).Exists) return "None";
            return trend > 0.004 ? "Growing" : trend < -0.004 ? "Melting" : "Steady";
        }
    }

    private string CurrentState => Mode switch
    {
        IceAgeMode.Sunshine => IceStateName,
        IceAgeMode.Sheet => SheetStateName,
        _ => Cycle.ToString(),
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
            case IceAgeMode.Sunshine:
                RefreshSunshine();
                break;

            case IceAgeMode.Sheet:
                RefreshSheet();
                break;

            default:
                RefreshOrbit();
                break;
        }
    }

    private void RefreshOrbit()
    {
        var cycle = Cycle.ToString();
        PhaseTitle = _localization.GetString($"Ice_Cycle_{cycle}_Title");
        PhaseDescription = _localization.GetString($"Ice_Cycle_{cycle}_Text");
        PhaseBrush = Cycle switch
        {
            IceAgeCycle.Eccentricity => OrangeBrush,
            IceAgeCycle.Tilt => GreenBrush,
            IceAgeCycle.Precession => VioletBrush,
            _ => GoldBrush,
        };

        var orbit = IceAgeModel.Elements(TimeKa, Cycle);
        var e = orbit.Eccentricity;
        var nearFar = (((1 + e) / (1 - e)) * ((1 + e) / (1 - e))) - 1;
        var summerDays = IceAgeModel.NorthernSummerDays(orbit);
        Stats =
        [
            new(_localization.GetString("Ice_Stat_Time"), TimeText),
            new(_localization.GetString("Ice_Stat_Eccentricity"), Number(e, "0.0000")),
            new(_localization.GetString("Ice_Stat_NearFar"), _localization.Format("Ice_Value_Percent", Number(nearFar * 100, "0.0"))),
            new(_localization.GetString("Ice_Stat_Tilt"), _localization.Format("Ice_Value_Degrees", Number(orbit.Obliquity, "0.00"))),
            new(_localization.GetString("Ice_Stat_Perihelion"), IceAgeDiagram.DateText(IceAgeModel.PerihelionDay(orbit))),
            new(_localization.GetString("Ice_Stat_Halves"), _localization.Format("Ice_Value_Halves", Number(summerDays, "0.0"), Number(IceAgeModel.YearDays - summerDays, "0.0"))),
            SunshineStat(IceAgeModel.DailyInsolation(orbit, IceAgeModel.KeyLatitude)),
        ];
    }

    private void RefreshSunshine()
    {
        var state = IceStateName;
        PhaseTitle = _localization.GetString($"Ice_State_{state}_Title");
        PhaseDescription = _localization.GetString($"Ice_State_{state}_Text");
        PhaseBrush = state switch
        {
            "Warm" => GreenBrush,
            "Growing" => CyanBrush,
            "Holding" => BlueBrush,
            "Full" => IceBrush,
            _ => OrangeBrush,
        };

        var moment = Moment;
        Stats =
        [
            new(_localization.GetString("Ice_Stat_Time"), TimeText),
            SunshineStat(moment.Sunshine),
            new(_localization.GetString("Ice_Stat_Ice"), _localization.Format("Ice_Value_Percent", Number(moment.IceShare * 100, "0"))),
            new(_localization.GetString("Ice_Stat_Sea"), _localization.Format("Ice_Value_M", Signed(moment.SeaLevel, "0"))),
            new(_localization.GetString("Ice_Stat_Co2"), _localization.Format("Ice_Value_Ppm", Number(moment.Co2, "0"))),
            new(_localization.GetString("Ice_Stat_Temperature"), _localization.Format("Ice_Value_Celsius", Signed(moment.Temperature, "0.0"))),
            new(_localization.GetString("Ice_Stat_State"), _localization.GetString($"Ice_StateName_{moment.State}")),
        ];
    }

    private void RefreshSheet()
    {
        var state = SheetStateName;
        PhaseTitle = _localization.GetString($"Ice_Sheet_{state}_Title");
        PhaseDescription = _localization.GetString($"Ice_Sheet_{state}_Text");
        PhaseBrush = state switch
        {
            "None" => GreenBrush,
            "Growing" => CyanBrush,
            "Melting" => OrangeBrush,
            _ => IceBrush,
        };

        var moment = Moment;
        var sheet = IceAgeDiagram.Sheet(moment.IceShare);
        var sinking = Enumerable.Range(0, 120).Select(i => IceAgeDiagram.Sinking(moment, 24 + (i * 0.5))).Max();
        var stats = new List<OscillationStat> { new(_localization.GetString("Ice_Stat_Time"), TimeText) };
        if (sheet.Exists)
        {
            stats.Add(new(_localization.GetString("Ice_Stat_Height"), _localization.Format("Ice_Value_Km", Number(sheet.Height / 1000, "0.0"))));
            stats.Add(new(_localization.GetString("Ice_Stat_SouthEdge"), _localization.Format("Ice_Value_LatN", Number(sheet.South, "0.0"))));
        }

        stats.Add(new(_localization.GetString("Ice_Stat_SnowLine"), _localization.Format("Ice_Value_M", Number(Math.Round(IceAgeDiagram.SnowLine(moment.Sunshine) / 10) * 10, "#,##0"))));
        stats.Add(new(_localization.GetString("Ice_Stat_Sinking"), _localization.Format("Ice_Value_M", Number(Math.Round(sinking / 10) * 10, "0"))));
        stats.Add(new(_localization.GetString("Ice_Stat_Sea"), _localization.Format("Ice_Value_M", Signed(moment.SeaLevel, "0"))));
        stats.Add(new(_localization.GetString("Ice_Stat_Co2"), _localization.Format("Ice_Value_Ppm", Number(moment.Co2, "0"))));
        Stats = stats;
    }

    private OscillationStat SunshineStat(double sunshine)
    {
        var change = sunshine - IceRecord.Instance.At(0).Sunshine;
        return new(_localization.GetString("Ice_Stat_Sunshine"), _localization.Format("Ice_Value_Sunshine", Number(sunshine, "0"), Signed(change, "0")));
    }

    #endregion

    #region formatting

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>A value with its sign: "+3", "−120".</summary>
    private static string Signed(double value, string format)
    {
        var text = Number(Math.Abs(value), format);
        return text.Trim('0', '.').Length == 0 ? text : (value < 0 ? "−" : "+") + text;
    }

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
        OnPropertyChanged(nameof(TimeText));
        Refresh();
    }

    public void Dispose()
    {
        _lateRefresh.Stop();
        _lateRefresh.Tick -= OnLateRefresh;
        Lesson.Dispose();
        Card.Dispose();
        CycleChoice.PropertyChanged -= OnCycleChosen;
        SpeedChoice.PropertyChanged -= OnSpeedChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
