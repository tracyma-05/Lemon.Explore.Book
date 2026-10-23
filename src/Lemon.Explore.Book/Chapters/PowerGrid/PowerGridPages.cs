using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>The Power grid chapter's left page: the generator, the line or the day's settings, their charts, and what is happening.</summary>
public sealed class PowerGridTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, PowerGridViewModel grid)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public PowerGridViewModel Grid { get; } = grid;

    public IChapterContent Content => Grid;
}

/// <summary>The Power grid chapter's right page: a generator, a transmission line, or a day of supply and demand, with playback.</summary>
public sealed class PowerGridDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, PowerGridViewModel grid)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public PowerGridViewModel Grid { get; } = grid;

    public IChapterContent Content => Grid;
}

public partial class PowerGridTextPageView : ChapterPageView
{
    public PowerGridTextPageView()
    {
        InitializeComponent();
    }
}

public partial class PowerGridDiagramPageView : ChapterPageView
{
    public PowerGridDiagramPageView()
    {
        InitializeComponent();
    }
}
