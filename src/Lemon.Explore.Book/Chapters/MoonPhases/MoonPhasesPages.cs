using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>The Moon phases chapter's left page: the Moon from Earth, the options of each view, tonight's Moon.</summary>
public sealed class MoonPhasesTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, MoonPhasesViewModel moon)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public MoonPhasesViewModel Moon { get; } = moon;

    public IChapterContent Content => Moon;
}

/// <summary>The Moon phases chapter's right page: the Moon going round Earth, the sky, or the month, with playback.</summary>
public sealed class MoonPhasesDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, MoonPhasesViewModel moon)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public MoonPhasesViewModel Moon { get; } = moon;

    public IChapterContent Content => Moon;
}

public partial class MoonPhasesTextPageView : ChapterPageView
{
    public MoonPhasesTextPageView()
    {
        InitializeComponent();
    }
}

public partial class MoonPhasesDiagramPageView : ChapterPageView
{
    public MoonPhasesDiagramPageView()
    {
        InitializeComponent();
    }
}
