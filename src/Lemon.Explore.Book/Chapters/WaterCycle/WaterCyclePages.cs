using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>The Water cycle chapter's left page: the view's settings, its chart, and what is happening.</summary>
public sealed class WaterCycleTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, WaterCycleViewModel water)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public WaterCycleViewModel Water { get; } = water;

    public IChapterContent Content => Water;
}

/// <summary>The Water cycle chapter's right page: the cycle, a cloud forming, or where the water is, with playback.</summary>
public sealed class WaterCycleDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, WaterCycleViewModel water)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public WaterCycleViewModel Water { get; } = water;

    public IChapterContent Content => Water;
}

public partial class WaterCycleTextPageView : ChapterPageView
{
    public WaterCycleTextPageView()
    {
        InitializeComponent();
    }
}

public partial class WaterCycleDiagramPageView : ChapterPageView
{
    public WaterCycleDiagramPageView()
    {
        InitializeComponent();
    }
}
