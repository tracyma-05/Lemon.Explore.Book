using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>The Spectra chapter's left page: the atom, the lamps or the star's settings, its chart, and what is happening.</summary>
public sealed class SpectraTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, SpectraViewModel spectra)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public SpectraViewModel Spectra { get; } = spectra;

    public IChapterContent Content => Spectra;
}

/// <summary>The Spectra chapter's right page: a hydrogen atom, a spectroscope, or a star's spectrum, with playback.</summary>
public sealed class SpectraDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, SpectraViewModel spectra)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public SpectraViewModel Spectra { get; } = spectra;

    public IChapterContent Content => Spectra;
}

public partial class SpectraTextPageView : ChapterPageView
{
    public SpectraTextPageView()
    {
        InitializeComponent();
    }
}

public partial class SpectraDiagramPageView : ChapterPageView
{
    public SpectraDiagramPageView()
    {
        InitializeComponent();
    }
}
