using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>The Flight chapter's left page: the wing, the plane's controls or the turn, its chart, and what is happening.</summary>
public sealed class FlightTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, FlightViewModel flight)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public FlightViewModel Flight { get; } = flight;

    public IChapterContent Content => Flight;
}

/// <summary>The Flight chapter's right page: a wing in a wind tunnel, a plane flying, or a plane turning, with playback.</summary>
public sealed class FlightDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, FlightViewModel flight)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public FlightViewModel Flight { get; } = flight;

    public IChapterContent Content => Flight;
}

public partial class FlightTextPageView : ChapterPageView
{
    public FlightTextPageView()
    {
        InitializeComponent();
    }
}

public partial class FlightDiagramPageView : ChapterPageView
{
    public FlightDiagramPageView()
    {
        InitializeComponent();
    }
}
