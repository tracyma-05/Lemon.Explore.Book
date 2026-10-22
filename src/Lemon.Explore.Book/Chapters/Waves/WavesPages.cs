using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Waves;

/// <summary>The Waves chapter's left page: the view's settings, its charts, and what is happening.</summary>
public sealed class WavesTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, WavesViewModel waves)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public WavesViewModel Waves { get; } = waves;

    public IChapterContent Content => Waves;
}

/// <summary>The Waves chapter's right page: ripples meeting, light through two slits, or a moving source, with playback.</summary>
public sealed class WavesDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, WavesViewModel waves)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public WavesViewModel Waves { get; } = waves;

    public IChapterContent Content => Waves;
}

public partial class WavesTextPageView : ChapterPageView
{
    public WavesTextPageView()
    {
        InitializeComponent();
    }
}

public partial class WavesDiagramPageView : ChapterPageView
{
    public WavesDiagramPageView()
    {
        InitializeComponent();
    }
}
