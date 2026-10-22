using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>The Star life chapter's left page: the star's mass, each view's own pictures, and what is happening.</summary>
public sealed class StarLifeTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, StarLifeViewModel star)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public StarLifeViewModel Star { get; } = star;

    public IChapterContent Content => Star;
}

/// <summary>The Star life chapter's right page: the star's life, the H-R diagram, or the fates, with playback.</summary>
public sealed class StarLifeDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, StarLifeViewModel star)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public StarLifeViewModel Star { get; } = star;

    public IChapterContent Content => Star;
}

public partial class StarLifeTextPageView : ChapterPageView
{
    public StarLifeTextPageView()
    {
        InitializeComponent();
    }
}

public partial class StarLifeDiagramPageView : ChapterPageView
{
    public StarLifeDiagramPageView()
    {
        InitializeComponent();
    }
}
