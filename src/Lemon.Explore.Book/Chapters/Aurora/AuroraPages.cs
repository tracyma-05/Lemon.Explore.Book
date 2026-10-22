using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>The Aurora chapter's left page: the view's settings, its chart, and what is happening.</summary>
public sealed class AuroraTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, AuroraViewModel aurora)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public AuroraViewModel Aurora { get; } = aurora;

    public IChapterContent Content => Aurora;
}

/// <summary>The Aurora chapter's right page: the solar wind on Earth's shield, a trapped particle, or the aurora in the night sky, with playback.</summary>
public sealed class AuroraDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, AuroraViewModel aurora)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public AuroraViewModel Aurora { get; } = aurora;

    public IChapterContent Content => Aurora;
}

public partial class AuroraTextPageView : ChapterPageView
{
    public AuroraTextPageView()
    {
        InitializeComponent();
    }
}

public partial class AuroraDiagramPageView : ChapterPageView
{
    public AuroraDiagramPageView()
    {
        InitializeComponent();
    }
}
