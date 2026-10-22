using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Contents;

/// <summary>
/// Builds each chapter's two pages: the ported chapters' own page types, and placeholders for the rest. Porting a
/// chapter means adding its case here.
/// </summary>
public static class ChapterRegistry
{
    /// <summary>Slugs of the chapters that are fully in the book.</summary>
    public static IReadOnlySet<string> Ported { get; } = new HashSet<string>(StringComparer.Ordinal) { "Seasons" };

    public static (PageViewModel Text, PageViewModel Diagram) CreatePages(
        Chapter chapter, int number, int textPage, IBookNavigator navigator, BookServices services)
    {
        switch (chapter.Slug)
        {
            case "Seasons":
            {
                var seasons = new SeasonsViewModel(services.Narration, services.Settings);
                return (new SeasonsTextPageViewModel(textPage, navigator, chapter, number, seasons),
                    new SeasonsDiagramPageViewModel(textPage + 1, navigator, chapter, seasons));
            }

            default:
                return (new ChapterTextPageViewModel(textPage, navigator, chapter, number),
                    new ChapterDiagramPageViewModel(textPage + 1, navigator, chapter));
        }
    }
}
