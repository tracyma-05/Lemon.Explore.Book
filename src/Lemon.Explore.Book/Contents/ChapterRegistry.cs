using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Chapters.Kepler;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Chapters.SolarSystem;
using Lemon.Explore.Book.Chapters.Tides;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Contents;

/// <summary>
/// Builds each chapter's two pages: the ported chapters' own page types, and placeholders for the rest. Porting a
/// chapter means adding its case here.
/// </summary>
public static class ChapterRegistry
{
    /// <summary>Slugs of the chapters that are fully in the book.</summary>
    public static IReadOnlySet<string> Ported { get; } = new HashSet<string>(StringComparer.Ordinal) { "Solar-System", "Eclipses", "Seasons", "Moon-Phases", "Day-Night", "Tides", "Kepler" };

    public static (PageViewModel Text, PageViewModel Diagram) CreatePages(
        Chapter chapter, int number, int textPage, IBookNavigator navigator, BookServices services)
    {
        switch (chapter.Slug)
        {
            case "Solar-System":
            {
                var solar = new SolarSystemViewModel(services.Narration);
                return (new SolarSystemTextPageViewModel(textPage, navigator, chapter, number, solar),
                    new SolarSystemDiagramPageViewModel(textPage + 1, navigator, chapter, solar));
            }

            case "Eclipses":
            {
                var eclipse = new EclipseViewModel(services.Narration);
                return (new EclipseTextPageViewModel(textPage, navigator, chapter, number, eclipse),
                    new EclipseDiagramPageViewModel(textPage + 1, navigator, chapter, eclipse));
            }

            case "Seasons":
            {
                var seasons = new SeasonsViewModel(services.Narration, services.Settings);
                return (new SeasonsTextPageViewModel(textPage, navigator, chapter, number, seasons),
                    new SeasonsDiagramPageViewModel(textPage + 1, navigator, chapter, seasons));
            }

            case "Moon-Phases":
            {
                var moon = new MoonPhasesViewModel(services.Narration, navigator);
                return (new MoonPhasesTextPageViewModel(textPage, navigator, chapter, number, moon),
                    new MoonPhasesDiagramPageViewModel(textPage + 1, navigator, chapter, moon));
            }

            case "Day-Night":
            {
                var day = new DayNightViewModel(services.Location, services.Narration);
                return (new DayNightTextPageViewModel(textPage, navigator, chapter, number, day),
                    new DayNightDiagramPageViewModel(textPage + 1, navigator, chapter, day));
            }

            case "Tides":
            {
                var tides = new TidesViewModel(navigator, services.Location, services.Narration);
                return (new TidesTextPageViewModel(textPage, navigator, chapter, number, tides),
                    new TidesDiagramPageViewModel(textPage + 1, navigator, chapter, tides));
            }

            case "Kepler":
            {
                var kepler = new KeplerViewModel(navigator, services.Narration);
                return (new KeplerTextPageViewModel(textPage, navigator, chapter, number, kepler),
                    new KeplerDiagramPageViewModel(textPage + 1, navigator, chapter, kepler));
            }

            default:
                return (new ChapterTextPageViewModel(textPage, navigator, chapter, number),
                    new ChapterDiagramPageViewModel(textPage + 1, navigator, chapter));
        }
    }
}
