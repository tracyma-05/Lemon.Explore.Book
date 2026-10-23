using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>The Acids and bases chapter's left page: the particles, the pH scale or the titration's settings, their charts, and what is happening.</summary>
public sealed class AcidsBasesTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, AcidBaseViewModel acids)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public AcidBaseViewModel Acids { get; } = acids;

    public IChapterContent Content => Acids;
}

/// <summary>The Acids and bases chapter's right page: particles in water, the pH scale, or a neutralisation.</summary>
public sealed class AcidsBasesDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, AcidBaseViewModel acids)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public AcidBaseViewModel Acids { get; } = acids;

    public IChapterContent Content => Acids;
}

public partial class AcidsBasesTextPageView : ChapterPageView
{
    public AcidsBasesTextPageView()
    {
        InitializeComponent();
    }
}

public partial class AcidsBasesDiagramPageView : ChapterPageView
{
    public AcidsBasesDiagramPageView()
    {
        InitializeComponent();
    }
}
