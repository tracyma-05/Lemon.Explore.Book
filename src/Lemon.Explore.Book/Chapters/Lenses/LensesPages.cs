using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>The Lenses chapter's left page: the bench, the eye or the camera's settings, its chart, and what is happening.</summary>
public sealed class LensesTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, LensesViewModel lenses)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public LensesViewModel Lenses { get; } = lenses;

    public IChapterContent Content => Lenses;
}

/// <summary>The Lenses chapter's right page: a lens on an optical bench, an eye, or a camera, with playback.</summary>
public sealed class LensesDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, LensesViewModel lenses)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public LensesViewModel Lenses { get; } = lenses;

    public IChapterContent Content => Lenses;
}

public partial class LensesTextPageView : ChapterPageView
{
    public LensesTextPageView()
    {
        InitializeComponent();
    }
}

public partial class LensesDiagramPageView : ChapterPageView
{
    public LensesDiagramPageView()
    {
        InitializeComponent();
    }
}
