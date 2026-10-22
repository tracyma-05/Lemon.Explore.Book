using Lemon.Explore.Book.Chapters.Aurora;
using Lemon.Explore.Book.Chapters.BlackHole;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Chapters.Earthquakes;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Chapters.Expansion;
using Lemon.Explore.Book.Chapters.Greenhouse;
using Lemon.Explore.Book.Chapters.IceAges;
using Lemon.Explore.Book.Chapters.Kepler;
using Lemon.Explore.Book.Chapters.Lightning;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Oscillations;
using Lemon.Explore.Book.Chapters.Rainbow;
using Lemon.Explore.Book.Chapters.Retrograde;
using Lemon.Explore.Book.Chapters.SeaBreeze;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Chapters.SolarSystem;
using Lemon.Explore.Book.Chapters.StarLife;
using Lemon.Explore.Book.Chapters.StarTrails;
using Lemon.Explore.Book.Chapters.Tides;
using Lemon.Explore.Book.Chapters.Typhoon;
using Lemon.Explore.Book.Chapters.Volcanoes;
using Lemon.Explore.Book.Chapters.WaterCycle;
using Lemon.Explore.Book.Chapters.Waves;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Contents;

/// <summary>
/// Builds each chapter's two pages: the ported chapters' own page types, and placeholders for the rest. Porting a
/// chapter means adding its case here.
/// </summary>
public static class ChapterRegistry
{
    /// <summary>Slugs of the chapters that are fully in the book.</summary>
    public static IReadOnlySet<string> Ported { get; } = new HashSet<string>(StringComparer.Ordinal) { "Solar-System", "Eclipses", "Seasons", "Moon-Phases", "Day-Night", "Tides", "Kepler", "Star-Trails", "Retrograde", "Star-Life", "Black-Hole", "Expansion", "Water-Cycle", "Sea-Breeze", "Greenhouse", "Typhoon", "Lightning", "Blue-Sky", "Rainbow", "Aurora", "Earthquakes", "Volcanoes", "Ice-Ages", "Oscillations", "Waves" };

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

            case "Expansion":
            {
                var expansion = new ExpansionViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, expansion.Lesson, () => expansion.Mode.ToString(), Enum.GetNames<ExpansionMode>());
                return (new ExpansionTextPageViewModel(textPage, navigator, chapter, number, expansion),
                    new ExpansionDiagramPageViewModel(textPage + 1, navigator, chapter, expansion));
            }

            case "Water-Cycle":
            {
                var water = new WaterCycleViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, water.Lesson, () => water.Mode.ToString(), Enum.GetNames<WaterMode>());
                return (new WaterCycleTextPageViewModel(textPage, navigator, chapter, number, water),
                    new WaterCycleDiagramPageViewModel(textPage + 1, navigator, chapter, water));
            }

            case "Sea-Breeze":
            {
                var breeze = new SeaBreezeViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, breeze.Lesson, () => breeze.Mode.ToString(), Enum.GetNames<BreezeMode>());
                return (new SeaBreezeTextPageViewModel(textPage, navigator, chapter, number, breeze),
                    new SeaBreezeDiagramPageViewModel(textPage + 1, navigator, chapter, breeze));
            }

            case "Greenhouse":
            {
                var green = new GreenhouseViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, green.Lesson, () => green.Mode.ToString(), Enum.GetNames<GreenhouseMode>());
                return (new GreenhouseTextPageViewModel(textPage, navigator, chapter, number, green),
                    new GreenhouseDiagramPageViewModel(textPage + 1, navigator, chapter, green));
            }

            case "Typhoon":
            {
                var typhoon = new TyphoonViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, typhoon.Lesson, () => typhoon.Mode.ToString(), Enum.GetNames<TyphoonMode>());
                return (new TyphoonTextPageViewModel(textPage, navigator, chapter, number, typhoon),
                    new TyphoonDiagramPageViewModel(textPage + 1, navigator, chapter, typhoon));
            }

            case "Lightning":
            {
                var lightning = new LightningViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, lightning.Lesson, () => lightning.Mode.ToString(), Enum.GetNames<LightningMode>());
                return (new LightningTextPageViewModel(textPage, navigator, chapter, number, lightning),
                    new LightningDiagramPageViewModel(textPage + 1, navigator, chapter, lightning));
            }

            case "Blue-Sky":
            {
                var sky = new BlueSkyViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, sky.Lesson, () => sky.Mode.ToString(), Enum.GetNames<SkyMode>());
                return (new BlueSkyTextPageViewModel(textPage, navigator, chapter, number, sky),
                    new BlueSkyDiagramPageViewModel(textPage + 1, navigator, chapter, sky));
            }

            case "Rainbow":
            {
                var rainbow = new RainbowViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, rainbow.Lesson, () => rainbow.Mode.ToString(), Enum.GetNames<RainbowMode>());
                return (new RainbowTextPageViewModel(textPage, navigator, chapter, number, rainbow),
                    new RainbowDiagramPageViewModel(textPage + 1, navigator, chapter, rainbow));
            }

            case "Aurora":
            {
                var aurora = new AuroraViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, aurora.Lesson, () => aurora.Mode.ToString(), Enum.GetNames<AuroraMode>());
                return (new AuroraTextPageViewModel(textPage, navigator, chapter, number, aurora),
                    new AuroraDiagramPageViewModel(textPage + 1, navigator, chapter, aurora));
            }

            case "Earthquakes":
            {
                var quake = new QuakeViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, quake.Lesson, () => quake.Mode.ToString(), Enum.GetNames<QuakeMode>());
                return (new QuakeTextPageViewModel(textPage, navigator, chapter, number, quake),
                    new QuakeDiagramPageViewModel(textPage + 1, navigator, chapter, quake));
            }

            case "Volcanoes":
            {
                var volcano = new VolcanoViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, volcano.Lesson, () => volcano.Mode.ToString(), Enum.GetNames<VolcanoMode>());
                return (new VolcanoTextPageViewModel(textPage, navigator, chapter, number, volcano),
                    new VolcanoDiagramPageViewModel(textPage + 1, navigator, chapter, volcano));
            }

            case "Ice-Ages":
            {
                var ice = new IceAgeViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, ice.Lesson, () => ice.Mode.ToString(), Enum.GetNames<IceAgeMode>());
                return (new IceAgeTextPageViewModel(textPage, navigator, chapter, number, ice),
                    new IceAgeDiagramPageViewModel(textPage + 1, navigator, chapter, ice));
            }

            case "Oscillations":
            {
                var osc = new OscillationsViewModel(services.Location, services.Narration);
                services.Progress.Track(chapter.Slug, osc.Lesson, () => osc.Mode.ToString(), Enum.GetNames<OscillationMode>());
                return (new OscillationsTextPageViewModel(textPage, navigator, chapter, number, osc),
                    new OscillationsDiagramPageViewModel(textPage + 1, navigator, chapter, osc));
            }

            case "Waves":
            {
                var waves = new WavesViewModel(services.Narration);
                services.Progress.Track(chapter.Slug, waves.Lesson, () => waves.Mode.ToString(), Enum.GetNames<WaveMode>());
                return (new WavesTextPageViewModel(textPage, navigator, chapter, number, waves),
                    new WavesDiagramPageViewModel(textPage + 1, navigator, chapter, waves));
            }

            default:
                return (new ChapterTextPageViewModel(textPage, navigator, chapter, number),
                    new ChapterDiagramPageViewModel(textPage + 1, navigator, chapter));
        }
    }
}
