using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Rainbow;

/// <summary>The Rainbow chapter's left page: the view's settings, its charts, and what is happening.</summary>
public sealed class RainbowTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, RainbowViewModel rainbow)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public RainbowViewModel Rainbow { get; } = rainbow;

    public IChapterContent Content => Rainbow;
}

/// <summary>The Rainbow chapter's right page: light bending at a surface, rays through a raindrop, or the bow in the sky, with its main value.</summary>
public sealed class RainbowDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, RainbowViewModel rainbow)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public RainbowViewModel Rainbow { get; } = rainbow;

    public IChapterContent Content => Rainbow;
}

public partial class RainbowTextPageView : ChapterPageView
{
    public RainbowTextPageView()
    {
        InitializeComponent();
    }
}

public partial class RainbowDiagramPageView : ChapterPageView
{
    public RainbowDiagramPageView()
    {
        InitializeComponent();
    }
}
