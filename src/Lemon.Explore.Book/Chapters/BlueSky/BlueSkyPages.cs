using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>The Blue sky chapter's left page: the view's settings, its charts, and what is happening.</summary>
public sealed class BlueSkyTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, BlueSkyViewModel sky)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public BlueSkyViewModel Sky { get; } = sky;

    public IChapterContent Content => Sky;
}

/// <summary>The Blue sky chapter's right page: the sky over the sea, light scattering in a box of air, or sunlight's path through the air, with the time of day.</summary>
public sealed class BlueSkyDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, BlueSkyViewModel sky)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public BlueSkyViewModel Sky { get; } = sky;

    public IChapterContent Content => Sky;
}

public partial class BlueSkyTextPageView : ChapterPageView
{
    public BlueSkyTextPageView()
    {
        InitializeComponent();
    }
}

public partial class BlueSkyDiagramPageView : ChapterPageView
{
    public BlueSkyDiagramPageView()
    {
        InitializeComponent();
    }
}
