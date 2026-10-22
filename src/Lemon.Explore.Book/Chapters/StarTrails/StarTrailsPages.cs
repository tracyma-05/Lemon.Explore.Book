using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>The Star trails chapter's left page: where and when, each view's options and chart, and what is happening.</summary>
public sealed class StarTrailsTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, StarTrailsViewModel trails)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public StarTrailsViewModel Trails { get; } = trails;

    public IChapterContent Content => Trails;
}

/// <summary>The Star trails chapter's right page: the camera, the turning sky or the year, with playback.</summary>
public sealed class StarTrailsDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, StarTrailsViewModel trails)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public StarTrailsViewModel Trails { get; } = trails;

    public IChapterContent Content => Trails;
}

public partial class StarTrailsTextPageView : ChapterPageView
{
    public StarTrailsTextPageView()
    {
        InitializeComponent();
    }
}

public partial class StarTrailsDiagramPageView : ChapterPageView
{
    public StarTrailsDiagramPageView()
    {
        InitializeComponent();
    }
}
