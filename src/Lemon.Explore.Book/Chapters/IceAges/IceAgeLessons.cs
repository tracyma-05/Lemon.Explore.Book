using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The ice-age page's spoken walkthroughs, one per view. Each sets the time and what is drawn so the picture matches the
/// words (a question is asked before the picture shows its answer), and the last step hands over to the viewer.
/// Highlight ids are <see cref="IceAgeDiagram"/>'s.
/// </summary>
internal static class IceAgeLessons
{
    public static IReadOnlyList<LessonStep> For(IceAgeViewModel page) => page.Mode switch
    {
        IceAgeMode.Sunshine => Sunshine(page),
        IceAgeMode.Sheet => Sheet(page),
        _ => Orbit(page),
    };

    /// <summary>The view set up with labels, held at <paramref name="kyr"/>.</summary>
    private static void Setup(IceAgeViewModel page, IceAgeMode mode, double kyr)
    {
        page.Mode = mode;
        page.ShowLabels = true;
        page.Hold(kyr);
    }

    private static IReadOnlyList<LessonStep> Orbit(IceAgeViewModel page)
    {
        // The task: the time moved by hand.
        var atTask = 0.0;
        return
        [
            new("Narr_Ice_Orbit_01")
            {
                Enter = () =>
                {
                    Setup(page, IceAgeMode.Orbit, 0);
                    page.Cycle = IceAgeCycle.All;
                    page.Exaggerate = true;
                },
                Highlight = "earth",
            },
            new("Narr_Ice_Orbit_02") { Highlight = "orbit" },
            new("Narr_Ice_Orbit_03")
            {
                Enter = () =>
                {
                    page.Cycle = IceAgeCycle.Eccentricity;
                    page.PlayFrom(-420, 20);
                },
                Highlight = "eccentricity",
            },
            new("Narr_Ice_Orbit_04")
            {
                Enter = () =>
                {
                    page.Cycle = IceAgeCycle.Tilt;
                    page.PlayFrom(-90, 5);
                },
                Highlight = "tilt",
            },
            new("Narr_Ice_Orbit_05")
            {
                Enter = () => page.Hold(-10),
                Choices = ["Ice_Lesson_Orbit_Choice1", "Ice_Lesson_Orbit_Choice2", "Ice_Lesson_Orbit_Choice3"],
                Answer = 0,
            },
            new("Narr_Ice_Orbit_06")
            {
                Enter = () =>
                {
                    page.Cycle = IceAgeCycle.Precession;
                    page.Hold(0);
                },
                Highlight = "perihelion",
            },
            new("Narr_Ice_Orbit_07")
            {
                Enter = () => page.Hold(-11),
                Highlight = "perihelion",
            },
            new("Narr_Ice_Orbit_08")
            {
                Enter = () => page.PlayFrom(-26, 2),
                Highlight = "polestar",
            },
            new("Narr_Ice_Orbit_09")
            {
                Enter = () =>
                {
                    page.Cycle = IceAgeCycle.All;
                    page.Hold(0);
                },
                Highlight = "summerhalf",
            },
            new("Narr_Ice_Orbit_10")
            {
                Enter = () => page.Hold(-115),
                Highlight = "strip",
            },
            new("Narr_Ice_Orbit_11")
            {
                Enter = () => atTask = page.TimeKa,
                Highlight = "strip",
                TryIt = () => Math.Abs(page.TimeKa - atTask) > 5,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Sunshine(IceAgeViewModel page)
    {
        var atTask = 0.0;
        return
        [
            new("Narr_Ice_Sunshine_01")
            {
                Enter = () =>
                {
                    Setup(page, IceAgeMode.Sunshine, 0);
                    page.ShowTerminations = false;
                },
                Highlight = "ring65",
            },
            new("Narr_Ice_Sunshine_02") { Highlight = "sunshinechart" },
            new("Narr_Ice_Sunshine_03")
            {
                Enter = () => page.Hold(-115),
                Highlight = "sunmeter",
            },
            new("Narr_Ice_Sunshine_04")
            {
                Choices = ["Ice_Lesson_Sunshine_Choice1", "Ice_Lesson_Sunshine_Choice2", "Ice_Lesson_Sunshine_Choice3"],
                Answer = 0,
            },
            new("Narr_Ice_Sunshine_05")
            {
                Enter = () => page.PlayFrom(-115, 6),
                Highlight = "laurentide",
            },
            new("Narr_Ice_Sunshine_06")
            {
                Enter = () => page.Hold(-21),
                Highlight = "icechart",
            },
            new("Narr_Ice_Sunshine_07")
            {
                Enter = () =>
                {
                    page.ShowTerminations = true;
                    page.PlayFrom(-21, 1.5);
                },
                Highlight = "terminations",
            },
            new("Narr_Ice_Sunshine_08")
            {
                Enter = () => page.Hold(-430),
                Highlight = "icechart",
            },
            new("Narr_Ice_Sunshine_09")
            {
                Enter = () => page.Hold(0),
                Highlight = "now",
            },
            new("Narr_Ice_Sunshine_10")
            {
                Enter = () => page.Hold(55),
                Highlight = "future",
            },
            new("Narr_Ice_Sunshine_11")
            {
                Enter = () => atTask = page.TimeKa,
                Highlight = "sunshinechart",
                TryIt = () => Math.Abs(page.TimeKa - atTask) > 5,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Sheet(IceAgeViewModel page)
    {
        var atTask = 0.0;
        return
        [
            new("Narr_Ice_Sheet_01")
            {
                Enter = () =>
                {
                    Setup(page, IceAgeMode.Sheet, -21);
                    page.ShowArrows = true;
                    page.ShowGround = true;
                },
                Highlight = "sheet",
            },
            new("Narr_Ice_Sheet_02") { Highlight = "snowline" },
            new("Narr_Ice_Sheet_03") { Highlight = "snowfall" },
            new("Narr_Ice_Sheet_04") { Highlight = "reflect" },
            new("Narr_Ice_Sheet_05")
            {
                Choices = ["Ice_Lesson_Sheet_Choice1", "Ice_Lesson_Sheet_Choice2", "Ice_Lesson_Sheet_Choice3"],
                Answer = 0,
            },
            new("Narr_Ice_Sheet_06") { Highlight = "ground" },
            new("Narr_Ice_Sheet_07") { Highlight = "coast" },
            new("Narr_Ice_Sheet_08")
            {
                Enter = () => page.Hold(-13),
                Highlight = "melt",
            },
            new("Narr_Ice_Sheet_09")
            {
                Enter = () => page.Hold(-9),
                Highlight = "ground",
            },
            new("Narr_Ice_Sheet_10")
            {
                Enter = () => page.Hold(-21),
                Highlight = "co2",
            },
            new("Narr_Ice_Sheet_11")
            {
                Enter = () => atTask = page.TimeKa,
                Highlight = "strip",
                TryIt = () => Math.Abs(page.TimeKa - atTask) > 5,
            },
        ];
    }
}
