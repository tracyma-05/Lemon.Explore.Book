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

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>A row of the "what is happening" card: a name and its value.</summary>
public sealed record StarStat(string Name, string Value);

/// <summary>A row of the fates table: the kind of star, its mass, how long it shines, and how it ends.</summary>
public sealed record StarFateRow(string Name, string Range, string Lifetime, string Ending, bool IsCurrent);

/// <summary>
/// The life of a star page: which view is shown, the star's mass, playback through its life, and the star at the
/// current moment. The diagram (<see cref="StarDiagram"/>) moves <see cref="Story"/> and, in fates mode, may pick a new
/// <see cref="Mass"/>; everything else follows them. <see cref="Lesson"/> is the spoken walkthrough of the current view,
/// and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class StarLifeViewModel : ObservableObject, IChapterContent, IDisposable
{
    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private IReadOnlyList<StarStageSpan> _track;

    public StarLifeViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => StarLifeLessons.For(this));
        Card = new ObjectCard(narration, Lesson, StarDiagram.ObjectTitleKey, StarDiagram.ObjectClipKey);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

        _track = StarModel.Track(_mass);
        _story = StarModel.StoryAtAge(_track, StarModel.SunAgeYears);
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
    [NotifyPropertyChangedFor(nameof(IsLife), nameof(IsDiagram), nameof(IsFates), nameof(ShowsStrip),
        nameof(Subtitle), nameof(ScaleNote), nameof(DragHint), nameof(Facts))]
    private StarMode _mode;

    public bool IsLife
    {
        get => Mode == StarMode.Life;
        set
        {
            if (value) Mode = StarMode.Life;
        }
    }

    public bool IsDiagram
    {
        get => Mode == StarMode.Diagram;
        set
        {
            if (value) Mode = StarMode.Diagram;
        }
    }

    public bool IsFates
    {
        get => Mode == StarMode.Fates;
        set
        {
            if (value) Mode = StarMode.Fates;
        }
    }

    /// <summary>The views with the strip of stages along the bottom.</summary>
    public bool ShowsStrip => Mode != StarMode.Fates;

    public string Subtitle => _localization.GetString(Mode switch
    {
        StarMode.Diagram => "Star_Diagram_Subtitle",
        StarMode.Fates => "Star_Fates_Subtitle",
        _ => "Star_Life_Subtitle",
    });

    public string ScaleNote => _localization.GetString(Mode switch
    {
        StarMode.Diagram => "Star_Diagram_Note",
        StarMode.Fates => "Star_Fates_Note",
        _ => "Star_Life_Note",
    });

    public string DragHint => _localization.GetString(Mode == StarMode.Fates ? "Star_DragHint_Fates" : "Star_DragHint_Strip");

    [ObservableProperty]
    private bool _showSun = true;

    [ObservableProperty]
    private bool _showOrbits = true;

    [ObservableProperty]
    private bool _showKnownStars = true;

    [ObservableProperty]
    private bool _showRadiusLines = true;

    #endregion

    #region the star's mass

    /// <summary>The star's initial mass, Suns.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogMass), nameof(MassText), nameof(ClassName), nameof(IsSunLike))]
    private double _mass = 1;

    /// <summary>The mass slider works in log₁₀ of the mass, so a red dwarf and a 40-Sun star are both easy to reach.</summary>
    public double LogMass
    {
        get => Math.Log10(Mass);
        set => Mass = Math.Round(Math.Pow(10, Math.Clamp(value, MinLogMass, MaxLogMass)), value < 0 ? 2 : 1);
    }

    public double MinLogMass => Math.Log10(StarModel.MinMass);

    public double MaxLogMass => Math.Log10(StarModel.MaxMass);

    public string MassText => _localization.Format("Star_Mass_Value", StarFormat.Times(Mass));

    public string ClassName => _localization.GetString($"Star_Class_{StarModel.ClassOf(Mass)}");

    /// <summary>Planets' orbits only make sense round a star like the Sun.</summary>
    public bool IsSunLike => StarModel.ClassOf(Mass) == StarClass.SunLike;

    /// <summary>Picks a star by mass, e.g. a preset chip.</summary>
    [RelayCommand]
    private void SetMass(string? mass)
    {
        if (double.TryParse(mass, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Mass = value;
    }

    partial void OnMassChanged(double oldValue, double newValue)
    {
        // Keep the same point of the life, as a share of the story.
        var oldLength = StarModel.StoryLength(_track);
        _track = StarModel.Track(newValue);
        var fraction = oldLength > 0 ? Story / oldLength : 0;
        OnPropertyChanged(nameof(StoryLength));
        Story = fraction * StarModel.StoryLength(_track);
        Refresh();
    }

    #endregion

    #region playback

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Playback seconds into the star's track (see <see cref="StarModel"/>).</summary>
    [ObservableProperty]
    private double _story;

    public double StoryLength => StarModel.StoryLength(_track);

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>To the start of the next or (with "Previous") the current or previous stage, and pause.</summary>
    [RelayCommand]
    private void Step(string? direction)
    {
        var state = StarModel.At(_track, Mass, Story);
        var start = StarModel.StoryStart(_track, state.Index);
        var index = direction == "Previous"
            ? Story - start > 0.3 ? state.Index : Math.Max(0, state.Index - 1)
            : Math.Min(_track.Count - 1, state.Index + 1);
        Story = StarModel.StoryStart(_track, index);
        IsPlaying = false;
    }

    /// <summary>The Sun as it is today, 4.6 billion years old, halfway through its life on the main sequence.</summary>
    [RelayCommand]
    private void TodaySun()
    {
        Mass = 1;
        Story = StarModel.StoryAtAge(_track, StarModel.SunAgeYears);
        IsPlaying = false;
    }

    partial void OnStoryChanged(double value) => Refresh();

    partial void OnModeChanged(StarMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Refresh();
    }

    /// <summary>
    /// For a walkthrough: to <paramref name="share"/> of the way through a stage of the current star's life, paused.
    /// A stage the star never reaches leaves it where it is.
    /// </summary>
    internal void GoToStage(StarStage stage, double share = 0.5)
    {
        var index = Enumerable.Range(0, _track.Count).FirstOrDefault(i => _track[i].Stage == stage, -1);
        if (index < 0) return;
        Story = StarModel.StoryStart(_track, index) + (_track[index].Seconds * share);
        IsPlaying = false;
    }

    #endregion

    #region walkthrough and clicked things

    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _stageTitle = string.Empty;
    private string _stageDescription = string.Empty;
    private string _coreText = string.Empty;
    private IBrush _stageBrush = Brushes.White;
    private StarStage _stage;
    private int _stageIndex;
    private IReadOnlyList<StarStat> _stats = [];
    private IReadOnlyList<StarFateRow> _fateRows = [];

    /// <summary>Beside the timeline: the star's age.</summary>
    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

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

    /// <summary>What is burning inside, under the cut-open star.</summary>
    public string CoreText
    {
        get => _coreText;
        private set => SetProperty(ref _coreText, value);
    }

    public IBrush StageBrush
    {
        get => _stageBrush;
        private set => SetProperty(ref _stageBrush, value);
    }

    public StarStage Stage
    {
        get => _stage;
        private set => SetProperty(ref _stage, value);
    }

    public int StageIndex
    {
        get => _stageIndex;
        private set => SetProperty(ref _stageIndex, value);
    }

    public IReadOnlyList<StarStat> Stats
    {
        get => _stats;
        private set => SetProperty(ref _stats, value);
    }

    /// <summary>Fates view: the four kinds of star, the chosen one's marked.</summary>
    public IReadOnlyList<StarFateRow> FateRows
    {
        get => _fateRows;
        private set => SetProperty(ref _fateRows, value);
    }

    public IReadOnlyList<string> Facts => (Mode switch
    {
        StarMode.Diagram => new[] { "Star_Diagram_Fact1", "Star_Diagram_Fact2", "Star_Diagram_Fact3", "Star_Diagram_Fact4", "Star_Diagram_Fact5" },
        StarMode.Fates => ["Star_Fates_Fact1", "Star_Fates_Fact2", "Star_Fates_Fact3", "Star_Fates_Fact4", "Star_Fates_Fact5"],
        _ => ["Star_Life_Fact1", "Star_Life_Fact2", "Star_Life_Fact3", "Star_Life_Fact4", "Star_Life_Fact5"],
    }).Select(_localization.GetString).ToList();

    private void Refresh()
    {
        var state = StarModel.At(_track, Mass, Math.Min(Story, StarModel.StoryLength(_track)));
        var span = _track[state.Index];
        Stage = state.Stage;
        StageIndex = state.Index;
        ClockText = _localization.Format("Star_Clock", StarFormat.Years(state.AgeYears));

        var key = StarDiagram.StageKey(state.Stage);
        StageTitle = _localization.GetString(key + "_Title");
        StageDescription = _localization.GetString(key + "_Description");
        CoreText = _localization.GetString($"Star_Core_{state.Stage}");
        var brush = new ImmutableSolidColorBrush(StarDiagram.StageColor(span, Mass));
        StageBrush = brush;

        var stats = new List<StarStat>
        {
            new(_localization.GetString("Star_Stat_Age"), StarFormat.Years(state.AgeYears)),
            new(_localization.GetString("Star_Stat_StageLasts"), StarFormat.Years(span.Years)),
        };

        switch (state.Stage)
        {
            case StarStage.NeutronStar or StarStage.BlackHole:
                stats.Add(new StarStat(_localization.GetString("Star_Stat_Remnant"),
                    _localization.Format("Star_Stat_Remnant_Value", StarFormat.Times(StarModel.RemnantMass(Mass)))));
                stats.Add(new StarStat(_localization.GetString("Star_Stat_Size"),
                    _localization.Format("Star_Km", StarFormat.Km(StarModel.RemnantRadiusKm(Mass) * 2))));
                break;

            default:
                stats.Add(new StarStat(_localization.GetString("Star_Stat_Temperature"),
                    _localization.Format("Star_Stat_Temperature_Value", StarFormat.Kelvin(state.Temperature), _localization.GetString(ColourKey(state.Temperature)))));
                stats.Add(new StarStat(_localization.GetString("Star_Stat_Luminosity"), _localization.Format("Star_Times_Sun", StarFormat.Times(state.Luminosity))));
                if (state.Stage == StarStage.Supernova) break;
                stats.Add(state.Stage == StarStage.WhiteDwarf
                    ? new StarStat(_localization.GetString("Star_Stat_Size"),
                        _localization.Format("Star_Km", StarFormat.Km(state.Radius * StarModel.SunRadiusKm * 2)))
                    : new StarStat(_localization.GetString("Star_Stat_Radius"), _localization.Format("Star_Times_Sun", StarFormat.Times(state.Radius))));
                if (state.Stage == StarStage.WhiteDwarf)
                {
                    stats.Add(new StarStat(_localization.GetString("Star_Stat_Remnant"),
                        _localization.Format("Star_Stat_Remnant_Value", StarFormat.Times(StarModel.RemnantMass(Mass)))));
                }

                break;
        }

        stats.Add(new StarStat(_localization.GetString("Star_Stat_Mass"), MassText));
        Stats = stats;

        var current = StarModel.ClassOf(Mass);
        FateRows = Enum.GetValues<StarClass>()
            .Select(c => new StarFateRow(
                _localization.GetString($"Star_Class_{c}"),
                _localization.GetString($"Star_Class_{c}_Range"),
                _localization.GetString($"Star_Class_{c}_Lifetime"),
                _localization.GetString($"Star_Class_{c}_Ending"),
                c == current))
            .ToList();
    }

    /// <summary>The colour a star of this temperature looks.</summary>
    private static string ColourKey(double temperature) => temperature switch
    {
        < 3700 => "Star_Colour_Red",
        < 5200 => "Star_Colour_Orange",
        < 6000 => "Star_Colour_Yellow",
        < 7500 => "Star_Colour_YellowWhite",
        < 10000 => "Star_Colour_White",
        _ => "Star_Colour_Blue",
    };

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(MassText));
        OnPropertyChanged(nameof(ClassName));
        Refresh();
    }

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}
