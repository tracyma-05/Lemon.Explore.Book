using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>The Retrograde chapter's left page: the planet, its loops, each view's options, the chart and what is happening.</summary>
public sealed class RetrogradeTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, RetrogradeViewModel retro)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public RetrogradeViewModel Retro { get; } = retro;

    public IChapterContent Content => Retro;
}

/// <summary>The Retrograde chapter's right page: the sky strip, the orbits from above, or the epicycles, with playback.</summary>
public sealed class RetrogradeDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, RetrogradeViewModel retro)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public RetrogradeViewModel Retro { get; } = retro;

    public IChapterContent Content => Retro;
}

public partial class RetrogradeTextPageView : ChapterPageView
{
    public RetrogradeTextPageView()
    {
        InitializeComponent();
    }
}

public partial class RetrogradeDiagramPageView : ChapterPageView
{
    public RetrogradeDiagramPageView()
    {
        InitializeComponent();
    }
}
