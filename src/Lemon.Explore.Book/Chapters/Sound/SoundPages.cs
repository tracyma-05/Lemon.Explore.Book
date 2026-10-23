using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>The Sound chapter's left page: the view's settings, its chart, and what is happening.</summary>
public sealed class SoundTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, SoundViewModel sound)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public SoundViewModel Sound { get; } = sound;

    public IChapterContent Content => Sound;
}

/// <summary>The Sound chapter's right page: a loudspeaker and the air, a string or pipe, or a keyboard, with playback.</summary>
public sealed class SoundDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, SoundViewModel sound)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public SoundViewModel Sound { get; } = sound;

    public IChapterContent Content => Sound;
}

public partial class SoundTextPageView : ChapterPageView
{
    public SoundTextPageView()
    {
        InitializeComponent();
    }
}

public partial class SoundDiagramPageView : ChapterPageView
{
    public SoundDiagramPageView()
    {
        InitializeComponent();
    }
}
