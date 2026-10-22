using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>The Day and night chapter's left page: where you are, the date, each view's own pictures, the clocks.</summary>
public sealed class DayNightTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, DayNightViewModel day)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public DayNightViewModel Day { get; } = day;

    public IChapterContent Content => Day;
}

/// <summary>The Day and night chapter's right page: the map, the time zones or the globe, with playback.</summary>
public sealed class DayNightDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, DayNightViewModel day)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public DayNightViewModel Day { get; } = day;

    public IChapterContent Content => Day;
}

public partial class DayNightTextPageView : ChapterPageView
{
    public DayNightTextPageView()
    {
        InitializeComponent();
    }
}

public partial class DayNightDiagramPageView : ChapterPageView
{
    public DayNightDiagramPageView()
    {
        InitializeComponent();
    }
}
