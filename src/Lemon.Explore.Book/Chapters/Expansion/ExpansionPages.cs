using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>The Expansion chapter's left page: the view's settings, its chart, and what is happening.</summary>
public sealed class ExpansionTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, ExpansionViewModel expansion)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public ExpansionViewModel Expansion { get; } = expansion;

    public IChapterContent Content => Expansion;
}

/// <summary>The Expansion chapter's right page: Hubble's law, redshift, or the universe's history, with playback.</summary>
public sealed class ExpansionDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, ExpansionViewModel expansion)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public ExpansionViewModel Expansion { get; } = expansion;

    public IChapterContent Content => Expansion;
}

public partial class ExpansionTextPageView : ChapterPageView
{
    public ExpansionTextPageView()
    {
        InitializeComponent();
    }
}

public partial class ExpansionDiagramPageView : ChapterPageView
{
    public ExpansionDiagramPageView()
    {
        InitializeComponent();
    }
}
