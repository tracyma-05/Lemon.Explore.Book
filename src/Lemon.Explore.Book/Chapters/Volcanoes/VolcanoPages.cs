using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>The Volcanoes chapter's left page: the view's settings, its chart, and what is happening.</summary>
public sealed class VolcanoTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, VolcanoViewModel volcano)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public VolcanoViewModel Volcano { get; } = volcano;

    public IChapterContent Content => Volcano;
}

/// <summary>The Volcanoes chapter's right page: an eruption, the rock cycle, or rock under the magnifier.</summary>
public sealed class VolcanoDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, VolcanoViewModel volcano)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public VolcanoViewModel Volcano { get; } = volcano;

    public IChapterContent Content => Volcano;
}

public partial class VolcanoTextPageView : ChapterPageView
{
    public VolcanoTextPageView()
    {
        InitializeComponent();
    }
}

public partial class VolcanoDiagramPageView : ChapterPageView
{
    public VolcanoDiagramPageView()
    {
        InitializeComponent();
    }
}
