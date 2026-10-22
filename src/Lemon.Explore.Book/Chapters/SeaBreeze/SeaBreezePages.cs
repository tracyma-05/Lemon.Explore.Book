using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>The Sea breeze chapter's left page: the view's settings, its chart, and what is happening.</summary>
public sealed class SeaBreezeTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, SeaBreezeViewModel breeze)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public SeaBreezeViewModel Breeze { get; } = breeze;

    public IChapterContent Content => Breeze;
}

/// <summary>The Sea breeze chapter's right page: a day on the coast, the breeze step by step, or the monsoon, with playback.</summary>
public sealed class SeaBreezeDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, SeaBreezeViewModel breeze)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public SeaBreezeViewModel Breeze { get; } = breeze;

    public IChapterContent Content => Breeze;
}

public partial class SeaBreezeTextPageView : ChapterPageView
{
    public SeaBreezeTextPageView()
    {
        InitializeComponent();
    }
}

public partial class SeaBreezeDiagramPageView : ChapterPageView
{
    public SeaBreezeDiagramPageView()
    {
        InitializeComponent();
    }
}
