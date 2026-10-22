using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>
/// The eclipse chapter: which eclipse is shown, playback, the options of each mode (the Moon's distance, the observer's
/// position, the Moon's path through Earth's shadow) and the explanation of what is happening right now. The diagram
/// (<see cref="EclipseDiagram"/>) moves <see cref="MoonAngle"/> / <see cref="SeasonDay"/>; everything else follows them.
/// <see cref="Lesson"/> is the spoken walkthrough of the current mode, and clicking a thing in the diagram
/// (<see cref="Card"/>) explains it.
/// </summary>
public partial class EclipseViewModel : ObservableObject, IChapterContent, IDisposable
{
    /// <summary>Where a demonstration starts: a little before the Moon's shadow (or Earth's) arrives.</summary>
    private const double SolarStart = 322;
    private const double LunarStart = 128;

    /// <summary>How long before an eclipse season "next season" jumps to, in days.</summary>
    private const double SeasonLeadIn = 20;

    private static readonly IBrush NoneBrush = Frozen(0x9A, 0xA5, 0xB5);
    private static readonly IBrush PartialBrush = Frozen(0xFF, 0xC2, 0x66);
    private static readonly IBrush TotalSolarBrush = Frozen(0xE8, 0xEE, 0xFF);
    private static readonly IBrush AnnularBrush = Frozen(0xFF, 0x9A, 0x3D);
    private static readonly IBrush PenumbralBrush = Frozen(0xB9, 0xC2, 0xD0);
    private static readonly IBrush TotalLunarBrush = Frozen(0xE0, 0x70, 0x3C);
    private static readonly IBrush SeasonBrush = Frozen(0xFF, 0xD3, 0x6B);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    public EclipseViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => EclipseLessons.For(this));
        Card = new ObjectCard(narration, Lesson, EclipseDiagram.ObjectTitleKey, EclipseDiagram.ObjectClipKey);
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

    #region mode and options

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSolar), nameof(IsLunar), nameof(IsSeasons), nameof(IsMonthly), nameof(Subtitle), nameof(DragHint),
        nameof(Facts))]
    private EclipseMode _mode;

    public bool IsSolar
    {
        get => Mode == EclipseMode.Solar;
        set
        {
            if (value) Mode = EclipseMode.Solar;
        }
    }

    public bool IsLunar
    {
        get => Mode == EclipseMode.Lunar;
        set
        {
            if (value) Mode = EclipseMode.Lunar;
        }
    }

    public bool IsSeasons
    {
        get => Mode == EclipseMode.Seasons;
        set
        {
            if (value) Mode = EclipseMode.Seasons;
        }
    }

    /// <summary>Solar or lunar mode, where the timeline is the Moon's position in its orbit.</summary>
    public bool IsMonthly => Mode != EclipseMode.Seasons;

    public string Subtitle => _localization.GetString(Mode switch
    {
        EclipseMode.Lunar => "Eclipse_Lunar_Subtitle",
        EclipseMode.Seasons => "Eclipse_Seasons_Subtitle",
        _ => "Eclipse_Solar_Subtitle",
    });

    /// <summary>What dragging on the diagram does in this mode.</summary>
    public string DragHint => _localization.GetString(Mode == EclipseMode.Seasons ? "Eclipse_DragHint_Year" : "Eclipse_DragHint_Moon");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMoonFar))]
    private bool _isMoonNear = true;

    public bool IsMoonFar
    {
        get => !IsMoonNear;
        set
        {
            if (value) IsMoonNear = false;
        }
    }

    /// <summary>The observer north (+) or south (−) of the eclipse's centre line, −1…1.</summary>
    [ObservableProperty]
    private double _observerOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LunarHeight), nameof(IsPathCentral), nameof(IsPathOffCentre), nameof(IsPathGrazing))]
    private LunarPath _lunarPath;

    public double LunarHeight => EclipseGeometry.LunarPathHeight(LunarPath);

    public bool IsPathCentral
    {
        get => LunarPath == LunarPath.Central;
        set
        {
            if (value) LunarPath = LunarPath.Central;
        }
    }

    public bool IsPathOffCentre
    {
        get => LunarPath == LunarPath.OffCentre;
        set
        {
            if (value) LunarPath = LunarPath.OffCentre;
        }
    }

    public bool IsPathGrazing
    {
        get => LunarPath == LunarPath.Grazing;
        set
        {
            if (value) LunarPath = LunarPath.Grazing;
        }
    }

    #endregion

    #region playback

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>The Moon's position along its orbit in degrees: 0 = new moon, 180 = full moon.</summary>
    [ObservableProperty]
    private double _moonAngle = SolarStart;

    /// <summary>Days since the start of the seasons view.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DayOfYear))]
    private double _seasonDay;

    /// <summary>The seasons view's timeline: the day within the current year (moving it keeps the year).</summary>
    public double DayOfYear
    {
        get => SeasonDay - (EclipseSeasons.Year * Math.Floor(SeasonDay / EclipseSeasons.Year));
        set => SeasonDay = (EclipseSeasons.Year * Math.Floor(SeasonDay / EclipseSeasons.Year)) + value;
    }

    public double YearLength => EclipseSeasons.Year;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Runs the eclipse again from the start, or in the seasons view skips ahead to the next eclipse season.</summary>
    [RelayCommand]
    private void Replay()
    {
        if (Mode == EclipseMode.Seasons)
        {
            SeasonDay = EclipseSeasons.NextSeasonStart(SeasonDay) - SeasonLeadIn;
        }
        else
        {
            MoonAngle = Mode == EclipseMode.Lunar ? LunarStart : SolarStart;
        }

        IsPlaying = true;
    }

    partial void OnModeChanged(EclipseMode value)
    {
        // Switching modes by hand ends a walkthrough of the old mode; a walkthrough switching modes itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;

        if (value != EclipseMode.Seasons)
        {
            MoonAngle = value == EclipseMode.Lunar ? LunarStart : SolarStart;
        }

        IsPlaying = true;
        Refresh();
    }

    partial void OnMoonAngleChanged(double value) => Refresh();

    partial void OnSeasonDayChanged(double value) => Refresh();

    partial void OnIsMoonNearChanged(bool value) => Refresh();

    partial void OnObserverOffsetChanged(double value) => Refresh();

    partial void OnLunarPathChanged(LunarPath value) => Refresh();

    #endregion

    #region walkthrough and clicked things

    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    /// <summary>For a walkthrough: the Moon at <paramref name="degrees"/> along its orbit (0 = new moon), held there.</summary>
    internal void HoldMoonAt(double degrees)
    {
        MoonAngle = degrees;
        IsPlaying = false;
    }

    #endregion

    #region what is happening

    private string _stageTitle = string.Empty;
    private string _stageDescription = string.Empty;
    private string _measureText = string.Empty;
    private IBrush _stageBrush = NoneBrush;
    private string _lastNewMoonText = string.Empty;
    private string _lastFullMoonText = string.Empty;
    private string _yearCountText = string.Empty;

    /// <summary>The stage right now, e.g. "Total solar eclipse" or "Outside an eclipse season".</summary>
    public string StageTitle
    {
        get => _stageTitle;
        private set => SetProperty(ref _stageTitle, value);
    }

    public string StageDescription
    {
        get => _stageDescription;
        private set => SetProperty(ref _stageDescription, value);
    }

    /// <summary>How much of the Sun is covered, or how much of the Moon is in the umbra; empty when nothing is.</summary>
    public string MeasureText
    {
        get => _measureText;
        private set => SetProperty(ref _measureText, value);
    }

    public IBrush StageBrush
    {
        get => _stageBrush;
        private set => SetProperty(ref _stageBrush, value);
    }

    public string LastNewMoonText
    {
        get => _lastNewMoonText;
        private set => SetProperty(ref _lastNewMoonText, value);
    }

    public string LastFullMoonText
    {
        get => _lastFullMoonText;
        private set => SetProperty(ref _lastFullMoonText, value);
    }

    public string YearCountText
    {
        get => _yearCountText;
        private set => SetProperty(ref _yearCountText, value);
    }

    /// <summary>Background facts for the current mode.</summary>
    public IReadOnlyList<string> Facts => (Mode switch
    {
        EclipseMode.Lunar => new[] { "Eclipse_Lunar_Fact1", "Eclipse_Lunar_Fact2", "Eclipse_Lunar_Fact3", "Eclipse_Lunar_Fact4" },
        EclipseMode.Seasons => ["Eclipse_Seasons_Fact1", "Eclipse_Seasons_Fact2", "Eclipse_Seasons_Fact3", "Eclipse_Seasons_Fact4"],
        _ => ["Eclipse_Solar_Fact1", "Eclipse_Solar_Fact2", "Eclipse_Solar_Fact3", "Eclipse_Solar_Fact4"],
    }).Select(_localization.GetString).ToList();

    private void Refresh()
    {
        switch (Mode)
        {
            case EclipseMode.Solar:
            {
                var sky = EclipseGeometry.Solar(MoonAngle * Math.PI / 180, IsMoonNear, ObserverOffset);
                ShowStage(sky.Stage);
                MeasureText = sky.Coverage > 0.0005 ? _localization.Format("Eclipse_Coverage", sky.Coverage) : string.Empty;
                break;
            }

            case EclipseMode.Lunar:
            {
                var lunar = EclipseGeometry.Lunar(MoonAngle * Math.PI / 180, LunarHeight);
                ShowStage(lunar.Stage);
                MeasureText = lunar.UmbraCoverage > 0.0005 ? _localization.Format("Eclipse_UmbraCoverage", lunar.UmbraCoverage) : string.Empty;
                break;
            }

            default:
                ShowSeason();
                break;
        }
    }

    private void ShowStage(EclipseStage stage)
    {
        var (title, description, brush) = stage switch
        {
            EclipseStage.SolarPartial => ("Eclipse_Stage_SolarPartial", "Eclipse_Stage_SolarPartial_Description", PartialBrush),
            EclipseStage.SolarTotal => ("Eclipse_Stage_SolarTotal", "Eclipse_Stage_SolarTotal_Description", TotalSolarBrush),
            EclipseStage.SolarAnnular => ("Eclipse_Stage_SolarAnnular", "Eclipse_Stage_SolarAnnular_Description", AnnularBrush),
            EclipseStage.LunarPenumbral => ("Eclipse_Stage_LunarPenumbral", "Eclipse_Stage_LunarPenumbral_Description", PenumbralBrush),
            EclipseStage.LunarPartial => ("Eclipse_Stage_LunarPartial", "Eclipse_Stage_LunarPartial_Description", PartialBrush),
            EclipseStage.LunarTotal => ("Eclipse_Stage_LunarTotal", "Eclipse_Stage_LunarTotal_Description", TotalLunarBrush),
            _ when Mode == EclipseMode.Lunar => ("Eclipse_Stage_LunarNone", "Eclipse_Stage_LunarNone_Description", NoneBrush),
            _ => ("Eclipse_Stage_SolarNone", "Eclipse_Stage_SolarNone_Description", NoneBrush),
        };

        StageTitle = _localization.GetString(title);
        StageDescription = _localization.GetString(description);
        StageBrush = brush;
    }

    private void ShowSeason()
    {
        var day = SeasonDay;
        var inSeason = EclipseSeasons.InSeason(day);
        StageTitle = _localization.GetString(inSeason ? "Eclipse_Stage_InSeason" : "Eclipse_Stage_OutOfSeason");
        StageDescription = _localization.GetString(inSeason ? "Eclipse_Stage_InSeason_Description" : "Eclipse_Stage_OutOfSeason_Description");
        StageBrush = inSeason ? SeasonBrush : NoneBrush;
        MeasureText = string.Empty;

        // The last month always holds one new and one full moon.
        var recent = EclipseSeasons.Events(day - EclipseSeasons.SynodicMonth, day).ToList();
        var newMoon = recent.LastOrDefault(e => e.IsNewMoon);
        var fullMoon = recent.LastOrDefault(e => !e.IsNewMoon);
        LastNewMoonText = _localization.Format("Eclipse_Season_LastNewMoon", Degrees(newMoon.Latitude), _localization.GetString(ResultKey(newMoon.Kind)));
        LastFullMoonText = _localization.Format("Eclipse_Season_LastFullMoon", Degrees(fullMoon.Latitude), _localization.GetString(ResultKey(fullMoon.Kind)));

        var year = EclipseSeasons.Events(day - EclipseSeasons.Year, day).ToList();
        YearCountText = _localization.Format("Eclipse_Season_YearCount",
            year.Count(e => e.Kind == SeasonEventKind.SolarEclipse),
            year.Count(e => e.Kind == SeasonEventKind.LunarEclipse),
            year.Count(e => e.Kind == SeasonEventKind.PenumbralLunarEclipse));
    }

    private string Degrees(double value) => value.ToString("+0.0;−0.0;0.0", _localization.CurrentCulture);

    private static string ResultKey(SeasonEventKind kind) => kind switch
    {
        SeasonEventKind.SolarEclipse => "Eclipse_Result_Solar",
        SeasonEventKind.LunarEclipse => "Eclipse_Result_Lunar",
        SeasonEventKind.PenumbralLunarEclipse => "Eclipse_Result_Penumbral",
        _ => "Eclipse_Result_None",
    };

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        Refresh();
    }

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}
