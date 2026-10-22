using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>The seasons chapter's left page: what the picture shows, the controls, and what is happening.</summary>
public sealed class SeasonsTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, SeasonsViewModel seasons)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public SeasonsViewModel Seasons { get; } = seasons;

    public IChapterContent Content => Seasons;
}

/// <summary>The seasons chapter's right page: the animated diagram with its walkthrough.</summary>
public sealed class SeasonsDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, SeasonsViewModel seasons)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public SeasonsViewModel Seasons { get; } = seasons;

    public IChapterContent Content => Seasons;
}
