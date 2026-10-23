using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>The Relativity chapter's left page: the light clock, the train or the twins' settings, their charts, and what is happening.</summary>
public sealed class RelativityTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, RelativityViewModel relativity)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public RelativityViewModel Relativity { get; } = relativity;

    public IChapterContent Content => Relativity;
}

/// <summary>The Relativity chapter's right page: a light clock, lightning and a train, or the twins' trip, with playback.</summary>
public sealed class RelativityDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, RelativityViewModel relativity)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public RelativityViewModel Relativity { get; } = relativity;

    public IChapterContent Content => Relativity;
}

public partial class RelativityTextPageView : ChapterPageView
{
    public RelativityTextPageView()
    {
        InitializeComponent();
    }
}

public partial class RelativityDiagramPageView : ChapterPageView
{
    public RelativityDiagramPageView()
    {
        InitializeComponent();
    }
}
