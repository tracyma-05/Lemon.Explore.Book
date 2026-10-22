using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>The Kepler chapter's left page: the orbit's settings, each view's own pictures, the planets' periods.</summary>
public sealed class KeplerTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, KeplerViewModel kepler)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public KeplerViewModel Kepler { get; } = kepler;

    public IChapterContent Content => Kepler;
}

/// <summary>The Kepler chapter's right page: the ellipse, equal areas or the periods, with playback.</summary>
public sealed class KeplerDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, KeplerViewModel kepler)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public KeplerViewModel Kepler { get; } = kepler;

    public IChapterContent Content => Kepler;
}

public partial class KeplerTextPageView : ChapterPageView
{
    public KeplerTextPageView()
    {
        InitializeComponent();
    }
}

public partial class KeplerDiagramPageView : ChapterPageView
{
    public KeplerDiagramPageView()
    {
        InitializeComponent();
    }
}
