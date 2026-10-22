using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>The Black hole chapter's left page: the view's settings, its chart, and what is happening.</summary>
public sealed class BlackHoleTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, BlackHoleViewModel hole)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public BlackHoleViewModel Hole { get; } = hole;

    public IChapterContent Content => Hole;
}

/// <summary>The Black hole chapter's right page: up close, light and orbits, or lensing, with playback.</summary>
public sealed class BlackHoleDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, BlackHoleViewModel hole)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public BlackHoleViewModel Hole { get; } = hole;

    public IChapterContent Content => Hole;
}

public partial class BlackHoleTextPageView : ChapterPageView
{
    public BlackHoleTextPageView()
    {
        InitializeComponent();
    }
}

public partial class BlackHoleDiagramPageView : ChapterPageView
{
    public BlackHoleDiagramPageView()
    {
        InitializeComponent();
    }
}
