using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Oscillations;

/// <summary>The Oscillations chapter's left page: where it swings, the view's settings, its charts, and what is happening.</summary>
public sealed class OscillationsTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, OscillationsViewModel osc)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public OscillationsViewModel Osc { get; } = osc;

    public IChapterContent Content => Osc;
}

/// <summary>The Oscillations chapter's right page: a pendulum, a mass on a spring, or a pendulum wave, with playback.</summary>
public sealed class OscillationsDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, OscillationsViewModel osc)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public OscillationsViewModel Osc { get; } = osc;

    public IChapterContent Content => Osc;
}

public partial class OscillationsTextPageView : ChapterPageView
{
    public OscillationsTextPageView()
    {
        InitializeComponent();
    }
}

public partial class OscillationsDiagramPageView : ChapterPageView
{
    public OscillationsDiagramPageView()
    {
        InitializeComponent();
    }
}
