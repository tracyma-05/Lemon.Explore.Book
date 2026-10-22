using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>
/// The retrograde page's spoken walkthroughs, one per view. Each picks the planet, holds the date at the station, the
/// opposition or the line of sight it talks about, or turns the frame, so the picture matches the words; the words never
/// name dates (they depend on today) or which side of the strip east is on (it depends on the viewer's hemisphere).
/// Highlight ids are <see cref="RetroDiagram"/>'s.
/// </summary>
internal static class RetrogradeLessons
{
    public static IReadOnlyList<LessonStep> For(RetrogradeViewModel page) => page.Mode switch
    {
        RetroMode.Above => Above(page),
        RetroMode.Epicycle => Epicycle(page),
        _ => Sky(page),
    };

    /// <summary>The view and planet set up, with labels, at normal speed, the date at today's loop.</summary>
    private static void Setup(RetrogradeViewModel page, RetroMode mode, RetroPlanet planet)
    {
        page.Mode = mode;
        page.Planet = planet;
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
        page.ShowGuides = true;
        page.SunCentered = false;
        page.Day = Math.Round(RetroModel.Today, 3);
    }

    private static IReadOnlyList<LessonStep> Sky(RetrogradeViewModel page)
    {
        // The task: another planet, or the date moved by dragging.
        var atTask = (Planet: RetroPlanet.Mars, Day: 0.0);
        return
        [
            new("Narr_Retro_Sky_01")
            {
                Enter = () =>
                {
                    Setup(page, RetroMode.Sky, RetroPlanet.Mars);
                    page.ShowWholePath = false;
                    page.GoTo(page.Period);
                },
                Highlight = RetroDiagram.PlanetId(RetroPlanet.Mars),
            },
            new("Narr_Retro_Sky_02") { Highlight = "east" },
            new("Narr_Retro_Sky_03")
            {
                Enter = () => page.Hold(page.Period.Start),
                Highlight = "station-r",
            },
            new("Narr_Retro_Sky_04")
            {
                Choices = ["Retro_Lesson_Sky_Choice1", "Retro_Lesson_Sky_Choice2", "Retro_Lesson_Sky_Choice3"],
                Answer = 0,
            },
            new("Narr_Retro_Sky_05")
            {
                Enter = () => page.Hold((page.Period.Start + page.Period.Middle) / 2),
                Highlight = "retro",
            },
            new("Narr_Retro_Sky_06")
            {
                Enter = () => page.Hold(page.Period.Middle),
                Highlight = "opposition",
            },
            new("Narr_Retro_Sky_07")
            {
                Enter = () => page.Hold(page.Period.End + 2),
                Highlight = "station-d",
            },
            new("Narr_Retro_Sky_08")
            {
                Enter = () =>
                {
                    page.ShowWholePath = true;
                    page.Hold(page.WindowEnd);
                },
                Highlight = "ecliptic",
            },
            new("Narr_Retro_Sky_09")
            {
                Enter = () =>
                {
                    page.IsPlaying = true;
                    atTask = (page.Planet, page.Day);
                },
                TryIt = () => page.Planet != atTask.Planet || (!page.IsPlaying && Math.Abs(page.Day - atTask.Day) > 5),
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Above(RetrogradeViewModel page)
    {
        // The task: another planet, or Earth dragged round its orbit.
        var atTask = (Planet: RetroPlanet.Mars, Day: 0.0);
        IReadOnlyList<double> Marks() => RetroDiagram.SightDays(page.Period, page.WindowStart, page.WindowEnd);
        return
        [
            new("Narr_Retro_Above_01")
            {
                Enter = () =>
                {
                    Setup(page, RetroMode.Above, RetroPlanet.Mars);
                    page.Hold(Marks()[0] - 10);
                },
                Highlight = "sun",
            },
            new("Narr_Retro_Above_02") { Highlight = "earthorbit" },
            new("Narr_Retro_Above_03") { Highlight = "sightline" },
            new("Narr_Retro_Above_04")
            {
                Enter = () => page.Hold(Marks()[1] + 1),
                Highlight = "fan",
            },
            new("Narr_Retro_Above_05")
            {
                Enter = () => page.Hold(Marks()[4] + 1),
                Highlight = "earth",
            },
            new("Narr_Retro_Above_06")
            {
                Choices = ["Retro_Lesson_Above_Choice1", "Retro_Lesson_Above_Choice2", "Retro_Lesson_Above_Choice3"],
                Answer = 0,
            },
            new("Narr_Retro_Above_07")
            {
                Enter = () => page.Hold(Marks()[6] + 1),
                Highlight = "speed",
            },
            new("Narr_Retro_Above_08")
            {
                Enter = () =>
                {
                    page.Planet = RetroPlanet.Venus;
                    page.Day = Math.Round(RetroModel.Today, 3);
                    page.Hold(Marks()[4] + 1);
                },
                Highlight = RetroDiagram.PlanetId(RetroPlanet.Venus),
            },
            new("Narr_Retro_Above_09")
            {
                Enter = () => atTask = (page.Planet, page.Day),
                TryIt = () => page.Planet != atTask.Planet || Math.Abs(page.Day - atTask.Day) > 10,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Epicycle(RetrogradeViewModel page)
    {
        // The task: the other frame, or another planet.
        var atTask = (Planet: RetroPlanet.Mars, Sun: false);
        return
        [
            new("Narr_Retro_Epicycle_01")
            {
                Enter = () =>
                {
                    Setup(page, RetroMode.Epicycle, RetroPlanet.Mars);
                    page.TrailStart = page.Day;
                    page.IsPlaying = true;
                },
                Highlight = "earth",
            },
            new("Narr_Retro_Epicycle_02")
            {
                Enter = () =>
                {
                    page.WholePatternCommand.Execute(null);
                    page.IsPlaying = true;
                },
                Highlight = "loop",
            },
            new("Narr_Retro_Epicycle_03")
            {
                Enter = () => page.IsPlaying = false,
                Highlight = "deferent",
            },
            new("Narr_Retro_Epicycle_04") { Highlight = "epicycle" },
            new("Narr_Retro_Epicycle_05")
            {
                Choices = ["Retro_Lesson_Epicycle_Choice1", "Retro_Lesson_Epicycle_Choice2", "Retro_Lesson_Epicycle_Choice3"],
                Answer = 0,
            },
            new("Narr_Retro_Epicycle_06")
            {
                Enter = () =>
                {
                    page.SunCentered = true;
                    page.IsPlaying = true;
                },
                Highlight = "frame",
            },
            new("Narr_Retro_Epicycle_07")
            {
                Enter = () =>
                {
                    page.SunCentered = false;
                    page.Planet = RetroPlanet.Venus;
                    page.WholePatternCommand.Execute(null);
                    page.IsPlaying = true;
                },
                Highlight = RetroDiagram.PlanetId(RetroPlanet.Venus),
            },
            new("Narr_Retro_Epicycle_08")
            {
                Enter = () => atTask = (page.Planet, page.SunCentered),
                TryIt = () => page.Planet != atTask.Planet || page.SunCentered != atTask.Sun,
            },
        ];
    }
}
