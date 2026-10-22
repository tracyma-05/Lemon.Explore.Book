using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>
/// The Kepler's laws page's spoken walkthroughs, one per law. Each sets up the orbit it talks about (the example orbit,
/// Earth's, Halley's), so the clips (<c>Narr_Kepler_*</c>) can describe what is on screen. Highlight ids are
/// <see cref="KeplerDiagram"/>'s.
/// </summary>
internal static class KeplerLessons
{
    /// <summary>The example orbit's shape in the walkthroughs: stretched enough to see both foci and the speed change.</summary>
    private const double ExampleEccentricity = 0.5;

    public static IReadOnlyList<LessonStep> For(KeplerViewModel page) => page.Mode switch
    {
        KeplerMode.Areas => Areas(page),
        KeplerMode.Periods => Periods(page),
        _ => Ellipse(page),
    };

    private static IReadOnlyList<LessonStep> Ellipse(KeplerViewModel page)
    {
        // The task: change the orbit's shape noticeably from what it was when the task starts.
        var shapeAtTask = 0.0;
        return
        [
            new("Narr_Kepler_Ellipse_01")
            {
                Enter = () =>
                {
                    page.Mode = KeplerMode.Ellipse;
                    page.ShowExampleOrbit(ExampleEccentricity);
                    page.ShowString = true;
                    page.ShowCircle = false;
                    page.IsPlaying = true;
                },
                Highlight = "orbit",
            },
            new("Narr_Kepler_Ellipse_02") { Highlight = "sun" },
            new("Narr_Kepler_Ellipse_03") { Highlight = "otherfocus" },
            new("Narr_Kepler_Ellipse_04") { Highlight = "string" },
            new("Narr_Kepler_Ellipse_05")
            {
                Enter = () =>
                {
                    page.Phase = 0;
                    page.IsPlaying = false;
                },
                Highlight = "string",
                Choices = ["Kepler_Lesson_Ellipse_Choice1", "Kepler_Lesson_Ellipse_Choice2", "Kepler_Lesson_Ellipse_Choice3"],
                Answer = 0,
            },
            new("Narr_Kepler_Ellipse_06")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "majoraxis",
            },
            new("Narr_Kepler_Ellipse_07")
            {
                Enter = () => shapeAtTask = page.Eccentricity,
                TryIt = () => Math.Abs(page.Eccentricity - shapeAtTask) > 0.1,
            },
            new("Narr_Kepler_Ellipse_08")
            {
                Enter = () =>
                {
                    page.Body.Selected = "Earth";
                    page.IsPlaying = true;
                },
                Highlight = "orbit",
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Areas(KeplerViewModel page)
    {
        // The task: drag the planet some way along its orbit (playback is paused, so only the viewer moves it).
        var phaseAtTask = 0.0;
        return
        [
            new("Narr_Kepler_Areas_01")
            {
                Enter = () =>
                {
                    page.Mode = KeplerMode.Areas;
                    page.ShowExampleOrbit(ExampleEccentricity);
                    page.SectorChoice.Selected = "12";
                    page.ShowVelocity = true;
                    page.Phase = 0;
                    page.IsPlaying = true;
                },
                Highlight = "radius",
            },
            new("Narr_Kepler_Areas_02") { Highlight = "equalareas" },
            new("Narr_Kepler_Areas_03") { Highlight = "sectors" },
            new("Narr_Kepler_Areas_04") { Highlight = "velocity" },
            new("Narr_Kepler_Areas_05")
            {
                Choices = ["Kepler_Lesson_Areas_Choice1", "Kepler_Lesson_Areas_Choice2", "Kepler_Lesson_Areas_Choice3"],
                Answer = 0,
            },
            new("Narr_Kepler_Areas_06") { Highlight = "perihelion" },
            new("Narr_Kepler_Areas_07")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    phaseAtTask = page.Phase;
                },
                Highlight = "planet",
                TryIt = () =>
                {
                    var moved = Math.Abs(page.Phase - phaseAtTask);
                    return Math.Min(moved, 1 - moved) > 0.08;
                },
            },
            new("Narr_Kepler_Areas_08")
            {
                Enter = () =>
                {
                    page.Body.Selected = "Halley";
                    page.IsPlaying = true;
                },
                Highlight = "orbit",
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Periods(KeplerViewModel page)
    {
        // The task: move your planet noticeably nearer or farther than where it is when the task starts.
        var axisAtTask = 0.0;
        return
        [
            new("Narr_Kepler_Periods_01")
            {
                Enter = () =>
                {
                    page.Mode = KeplerMode.Periods;
                    page.YourAxis = 2.5;
                    page.Years = 0;
                    page.IsPlaying = true;
                },
                Highlight = "p-Earth",
            },
            new("Narr_Kepler_Periods_02") { Highlight = "p-Mercury" },
            new("Narr_Kepler_Periods_03") { Highlight = "you" },
            new("Narr_Kepler_Periods_04")
            {
                Choices = ["Kepler_Lesson_Periods_Choice1", "Kepler_Lesson_Periods_Choice2", "Kepler_Lesson_Periods_Choice3"],
                Answer = 0,
            },
            new("Narr_Kepler_Periods_05")
            {
                Enter = () => page.YourAxis = 4,
                Highlight = "you",
            },
            new("Narr_Kepler_Periods_06")
            {
                Enter = () => axisAtTask = page.YourAxis,
                Highlight = "yourorbit",
                TryIt = () => Math.Abs(page.YourAxis - axisAtTask) > 0.5,
            },
            new("Narr_Kepler_Periods_07"),
        ];
    }
}
