using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>The Tides chapter's left page: each view's own pictures, the tide now, today's tides and the coming ones.</summary>
public sealed class TidesTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, TidesViewModel tides)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public TidesViewModel Tides { get; } = tides;

    public IChapterContent Content => Tides;
}

/// <summary>The Tides chapter's right page: the two bulges, spring and neap tides, or the coast, with playback.</summary>
public sealed class TidesDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, TidesViewModel tides)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public TidesViewModel Tides { get; } = tides;

    public IChapterContent Content => Tides;
}

public partial class TidesTextPageView : ChapterPageView
{
    public TidesTextPageView()
    {
        InitializeComponent();
    }
}

public partial class TidesDiagramPageView : ChapterPageView
{
    public TidesDiagramPageView()
    {
        InitializeComponent();
    }
}
