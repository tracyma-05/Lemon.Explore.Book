using Lemon.Book.Shell.Controls.Explore;
using Lemon.Book.Shell.ViewModels.Pages;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>The Atoms and the periodic table chapter's left page: the atom's particles or the table's colouring, the chart, and what is happening.</summary>
public sealed class PeriodicTableTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, AtomViewModel atoms)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public AtomViewModel Atoms { get; } = atoms;

    public IChapterContent Content => Atoms;
}

/// <summary>The Atoms and the periodic table chapter's right page: an atom being built, or the periodic table.</summary>
public sealed class PeriodicTableDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, AtomViewModel atoms)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public AtomViewModel Atoms { get; } = atoms;

    public IChapterContent Content => Atoms;
}

public partial class PeriodicTableTextPageView : ChapterPageView
{
    public PeriodicTableTextPageView()
    {
        InitializeComponent();
    }
}

public partial class PeriodicTableDiagramPageView : ChapterPageView
{
    public PeriodicTableDiagramPageView()
    {
        InitializeComponent();
    }
}
