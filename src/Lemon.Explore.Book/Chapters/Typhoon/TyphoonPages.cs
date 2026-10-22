using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>The Typhoon chapter's left page: the view's settings, its charts, and what is happening.</summary>
public sealed class TyphoonTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, TyphoonViewModel typhoon)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public TyphoonViewModel Typhoon { get; } = typhoon;

    public IChapterContent Content => Typhoon;
}

/// <summary>The Typhoon chapter's right page: the storm from above, its heat engine, or its track, with playback.</summary>
public sealed class TyphoonDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, TyphoonViewModel typhoon)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public TyphoonViewModel Typhoon { get; } = typhoon;

    public IChapterContent Content => Typhoon;
}

public partial class TyphoonTextPageView : ChapterPageView
{
    public TyphoonTextPageView()
    {
        InitializeComponent();
    }
}

public partial class TyphoonDiagramPageView : ChapterPageView
{
    public TyphoonDiagramPageView()
    {
        InitializeComponent();
    }
}
