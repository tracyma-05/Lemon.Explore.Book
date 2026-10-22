using Lemon.Explore.Book.Chapters.BlackHole;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Chapters.Kepler;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Retrograde;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Chapters.SolarSystem;
using Lemon.Explore.Book.Chapters.StarLife;
using Lemon.Explore.Book.Chapters.StarTrails;
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
    public static IReadOnlySet<string> Ported { get; } = new HashSet<string>(StringComparer.Ordinal) { "Solar-System", "Eclipses", "Seasons", "Moon-Phases", "Day-Night", "Tides", "Kepler", "Star-Trails", "Retrograde", "Star-Life", "Black-Hole" };

    public static (PageViewModel Text, PageViewModel Diagram) CreatePages(
        Chapter chapter, int number, int textPage, IBookNavigator navigator, BookServices services)
    {
        switch (chapter.Slug)
        {
            case "Solar-System":
            {
                var solar = new SolarSystemViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, solar.Lesson, () => "Tour", ["Tour"]);
                return (new SolarSystemTextPageViewModel(textPage, navigator, chapter, number, solar),
                    new SolarSystemDiagramPageViewModel(textPage + 1, navigator, chapter, solar));
            }

            case "Eclipses":
            {
                var eclipse = new EclipseViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, eclipse.Lesson, () => eclipse.Mode.ToString(), Enum.GetNames<EclipseMode>());
                return (new EclipseTextPageViewModel(textPage, navigator, chapter, number, eclipse),
                    new EclipseDiagramPageViewModel(textPage + 1, navigator, chapter, eclipse));
            }

            case "Seasons":
            {
                var seasons = new SeasonsViewModel(services.Narration, services.Settings);
                services.Progress.Track(chapter.Slug, seasons.Lesson, () => seasons.Mode.ToString(), Enum.GetNames<SeasonsMode>());
                return (new SeasonsTextPageViewModel(textPage, navigator, chapter, number, seasons),
                    new SeasonsDiagramPageViewModel(textPage + 1, navigator, chapter, seasons));
            }

            case "Moon-Phases":
            {
                var moon = new MoonPhasesViewModel(services.Narration, navigator);
                services.Progress.Track(chapter.Slug, moon.Lesson, () => moon.Mode.ToString(), Enum.GetNames<MoonPhasesMode>());
                return (new MoonPhasesTextPageViewModel(textPage, navigator, chapter, number, moon),
                    new MoonPhasesDiagramPageViewModel(textPage + 1, navigator, chapter, moon));
            }

            case "Day-Night":
            {
                var day = new DayNightViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, day.Lesson, () => day.Mode.ToString(), Enum.GetNames<DayNightMode>());
                return (new DayNightTextPageViewModel(textPage, navigator, chapter, number, day),
                    new DayNightDiagramPageViewModel(textPage + 1, navigator, chapter, day));
            }

            case "Tides":
            {
                var tides = new TidesViewModel(navigator, services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, tides.Lesson, () => tides.Mode.ToString(), Enum.GetNames<TidesMode>());
                return (new TidesTextPageViewModel(textPage, navigator, chapter, number, tides),
                    new TidesDiagramPageViewModel(textPage + 1, navigator, chapter, tides));
            }

            case "Kepler":
            {
                var kepler = new KeplerViewModel(navigator, services.Narration);
                services.Progress.Track(chapter.Slug, kepler.Lesson, () => kepler.Mode.ToString(), Enum.GetNames<KeplerMode>());
                return (new KeplerTextPageViewModel(textPage, navigator, chapter, number, kepler),
                    new KeplerDiagramPageViewModel(textPage + 1, navigator, chapter, kepler));
            }

            case "Star-Trails":
            {
                var trails = new StarTrailsViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, trails.Lesson, () => trails.Mode.ToString(), Enum.GetNames<TrailsMode>());
                return (new StarTrailsTextPageViewModel(textPage, navigator, chapter, number, trails),
                    new StarTrailsDiagramPageViewModel(textPage + 1, navigator, chapter, trails));
            }

            case "Retrograde":
            {
                var retro = new RetrogradeViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, retro.Lesson, () => retro.Mode.ToString(), Enum.GetNames<RetroMode>());
                return (new RetrogradeTextPageViewModel(textPage, navigator, chapter, number, retro),
                    new RetrogradeDiagramPageViewModel(textPage + 1, navigator, chapter, retro));
            }

            case "Star-Life":
            {
                var star = new StarLifeViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, star.Lesson, () => star.Mode.ToString(), Enum.GetNames<StarMode>());
                return (new StarLifeTextPageViewModel(textPage, navigator, chapter, number, star),
                    new StarLifeDiagramPageViewModel(textPage + 1, navigator, chapter, star));
            }

            case "Black-Hole":
            {
                var hole = new BlackHoleViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, hole.Lesson, () => hole.Mode.ToString(), Enum.GetNames<BlackHoleMode>());
                return (new BlackHoleTextPageViewModel(textPage, navigator, chapter, number, hole),
                    new BlackHoleDiagramPageViewModel(textPage + 1, navigator, chapter, hole));
            }

            default:
                return (new ChapterTextPageViewModel(textPage, navigator, chapter, number),
                    new ChapterDiagramPageViewModel(textPage + 1, navigator, chapter));
        }
    }
}
