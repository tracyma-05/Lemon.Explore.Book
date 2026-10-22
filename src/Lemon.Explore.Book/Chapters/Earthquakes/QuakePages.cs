using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>The Earthquakes chapter's left page: the view's settings, its chart, and what is happening.</summary>
public sealed class QuakeTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, QuakeViewModel quake)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public QuakeViewModel Quake { get; } = quake;

    public IChapterContent Content => Quake;
}

/// <summary>The Earthquakes chapter's right page: the plates on the map, a boundary in section, or the waves through the Earth.</summary>
public sealed class QuakeDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, QuakeViewModel quake)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public QuakeViewModel Quake { get; } = quake;

    public IChapterContent Content => Quake;
}

public partial class QuakeTextPageView : ChapterPageView
{
    public QuakeTextPageView()
    {
        InitializeComponent();
    }
}

public partial class QuakeDiagramPageView : ChapterPageView
{
    public QuakeDiagramPageView()
    {
        InitializeComponent();
    }
}
