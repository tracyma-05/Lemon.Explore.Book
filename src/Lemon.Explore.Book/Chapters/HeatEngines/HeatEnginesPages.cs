using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>The Heat engines chapter's left page: the engine, the fridge or the cycle's settings, their charts, and what is happening.</summary>
public sealed class HeatEnginesTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, HeatEnginesViewModel heat)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public HeatEnginesViewModel Heat { get; } = heat;

    public IChapterContent Content => Heat;
}

/// <summary>The Heat engines chapter's right page: a four-stroke engine, a fridge, or the ideal cycle, with playback.</summary>
public sealed class HeatEnginesDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, HeatEnginesViewModel heat)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public HeatEnginesViewModel Heat { get; } = heat;

    public IChapterContent Content => Heat;
}

public partial class HeatEnginesTextPageView : ChapterPageView
{
    public HeatEnginesTextPageView()
    {
        InitializeComponent();
    }
}

public partial class HeatEnginesDiagramPageView : ChapterPageView
{
    public HeatEnginesDiagramPageView()
    {
        InitializeComponent();
    }
}
