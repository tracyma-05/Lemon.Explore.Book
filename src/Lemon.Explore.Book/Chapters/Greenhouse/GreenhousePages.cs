using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>The Greenhouse chapter's left page: the view's settings, its charts, and what is happening.</summary>
public sealed class GreenhouseTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, GreenhouseViewModel green)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public GreenhouseViewModel Green { get; } = green;

    public IChapterContent Content => Green;
}

/// <summary>The Greenhouse chapter's right page: the planet, the molecules, or a glass house, with playback.</summary>
public sealed class GreenhouseDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, GreenhouseViewModel green)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public GreenhouseViewModel Green { get; } = green;

    public IChapterContent Content => Green;
}

public partial class GreenhouseTextPageView : ChapterPageView
{
    public GreenhouseTextPageView()
    {
        InitializeComponent();
    }
}

public partial class GreenhouseDiagramPageView : ChapterPageView
{
    public GreenhouseDiagramPageView()
    {
        InitializeComponent();
    }
}
