using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>The eclipses chapter's left page: the view from Earth, the options of each view, and what is happening.</summary>
public sealed class EclipseTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, EclipseViewModel eclipse)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public EclipseViewModel Eclipse { get; } = eclipse;

    public IChapterContent Content => Eclipse;
}

/// <summary>The eclipses chapter's right page: the shadows of Earth and the Moon, with the walkthrough and playback.</summary>
public sealed class EclipseDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, EclipseViewModel eclipse)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public EclipseViewModel Eclipse { get; } = eclipse;

    public IChapterContent Content => Eclipse;
}

public partial class EclipseTextPageView : ChapterPageView
{
    public EclipseTextPageView()
    {
        InitializeComponent();
    }
}

public partial class EclipseDiagramPageView : ChapterPageView
{
    public EclipseDiagramPageView()
    {
        InitializeComponent();
    }
}
