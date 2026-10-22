using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.SolarSystem;

/// <summary>The Solar System chapter's left page: the date, the bodies to pick, and the chosen body's story.</summary>
public sealed class SolarSystemTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, SolarSystemViewModel solar)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public SolarSystemViewModel Solar { get; } = solar;

    public IChapterContent Content => Solar;
}

/// <summary>The Solar System chapter's right page: the animated system with its tour and playback.</summary>
public sealed class SolarSystemDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, SolarSystemViewModel solar)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public SolarSystemViewModel Solar { get; } = solar;

    public IChapterContent Content => Solar;
}
