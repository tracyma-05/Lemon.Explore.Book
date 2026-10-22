using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>The Lightning chapter's left page: the view's settings, its charts, and what is happening.</summary>
public sealed class LightningTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, LightningViewModel lightning)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public LightningViewModel Lightning { get; } = lightning;

    public IChapterContent Content => Lightning;
}

/// <summary>The Lightning chapter's right page: static electricity, the storm cloud, or the strike, with playback.</summary>
public sealed class LightningDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, LightningViewModel lightning)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public LightningViewModel Lightning { get; } = lightning;

    public IChapterContent Content => Lightning;
}

public partial class LightningTextPageView : ChapterPageView
{
    public LightningTextPageView()
    {
        InitializeComponent();
    }
}

public partial class LightningDiagramPageView : ChapterPageView
{
    public LightningDiagramPageView()
    {
        InitializeComponent();
    }
}
