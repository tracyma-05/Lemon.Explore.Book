using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>
/// The moon-phases page's spoken walkthroughs, one per view. The clips (<c>Narr_MoonPhases_*</c>) stay true in both
/// hemispheres (no "south" or "left/right is lit") and never name the viewer's place or date. Highlight ids are
/// <see cref="MoonPhasesDiagram"/>'s.
/// </summary>
internal static class MoonPhasesLessons
{
    public static IReadOnlyList<LessonStep> For(MoonPhasesViewModel page) => page.Mode switch
    {
        MoonPhasesMode.Sky => Sky(page),
        MoonPhasesMode.Month => Month(page),
        _ => Phases(page),
    };

    private static IReadOnlyList<LessonStep> Phases(MoonPhasesViewModel page) =>
    [
        new("Narr_MoonPhases_Phases_01")
        {
            Enter = () =>
            {
                page.Mode = MoonPhasesMode.Phases;
                page.Age = MoonPhaseGeometry.AgeOf(MoonPhase.WaxingCrescent);
                page.IsPlaying = true;
            },
            Highlight = "moon",
        },
        new("Narr_MoonPhases_Phases_02") { Highlight = "sunlight" },
        new("Narr_MoonPhases_Phases_03") { Highlight = "halves" },
        new("Narr_MoonPhases_Phases_04")
        {
            Enter = () =>
            {
                page.Age = MoonPhaseGeometry.AgeOf(MoonPhase.New);
                page.IsPlaying = false;
            },
            Highlight = "moon",
        },
        new("Narr_MoonPhases_Phases_05")
        {
            Highlight = "orbit",
            Choices = ["MoonPhases_Lesson_Phases_Choice1", "MoonPhases_Lesson_Phases_Choice2", "MoonPhases_Lesson_Phases_Choice3"],
            Answer = 0,
        },
        new("Narr_MoonPhases_Phases_06")
        {
            Enter = () =>
            {
                page.Age = MoonPhaseGeometry.AgeOf(MoonPhase.Full);
                page.IsPlaying = false;
            },
            Highlight = "moon",
        },
        new("Narr_MoonPhases_Phases_07")
        {
            Highlight = "ring-FirstQuarter",
            TryIt = () => MoonPhaseGeometry.PhaseOf(MoonPhaseGeometry.Wrap(page.Age)) == MoonPhase.FirstQuarter,
        },
        new("Narr_MoonPhases_Phases_08") { Enter = () => page.IsPlaying = true },
        new("Narr_MoonPhases_Phases_09"),
    ];

    private static IReadOnlyList<LessonStep> Sky(MoonPhasesViewModel page) =>
    [
        new("Narr_MoonPhases_Sky_01")
        {
            Enter = () =>
            {
                page.Mode = MoonPhasesMode.Sky;
                page.AnimatesMonth = false;
                page.Age = MoonPhaseGeometry.AgeOf(MoonPhase.FirstQuarter);
                page.TimeOfDay = 8;
                page.IsPlaying = true;
            },
            Highlight = "path",
        },
        new("Narr_MoonPhases_Sky_02") { Highlight = "skymoon" },
        new("Narr_MoonPhases_Sky_03")
        {
            Enter = () =>
            {
                page.Age = MoonPhaseGeometry.AgeOf(MoonPhase.Full);
                page.TimeOfDay = 17.5;
                page.IsPlaying = true;
            },
            Highlight = "moonrise",
        },
        new("Narr_MoonPhases_Sky_04")
        {
            Choices = ["MoonPhases_Lesson_Sky_Choice1", "MoonPhases_Lesson_Sky_Choice2", "MoonPhases_Lesson_Sky_Choice3"],
            Answer = 0,
        },
        new("Narr_MoonPhases_Sky_05")
        {
            Enter = () =>
            {
                page.Age = 0.6;
                page.TimeOfDay = 11;
                page.IsPlaying = false;
            },
            Highlight = "skysun",
        },
        new("Narr_MoonPhases_Sky_06")
        {
            Enter = () =>
            {
                page.AnimatesMonth = true;
                page.TimeOfDay = 19;
                page.Age = 1.5;
                page.IsPlaying = true;
            },
            Highlight = "skymoon",
        },
        new("Narr_MoonPhases_Sky_07") { Enter = () => page.AnimatesMonth = false },
    ];

    private static IReadOnlyList<LessonStep> Month(MoonPhasesViewModel page) =>
    [
        new("Narr_MoonPhases_Month_01")
        {
            Enter = () =>
            {
                page.Mode = MoonPhasesMode.Month;
                page.Age = 0;
                page.IsPlaying = true;
            },
            Highlight = "month-moon",
        },
        new("Narr_MoonPhases_Month_02") { Highlight = "star" },
        new("Narr_MoonPhases_Month_03")
        {
            Enter = () =>
            {
                page.Age = MoonPhaseGeometry.SiderealMonth;
                page.IsPlaying = false;
            },
            Highlight = "sidereal",
        },
        new("Narr_MoonPhases_Month_04")
        {
            Highlight = "month-earth",
            Choices = ["MoonPhases_Lesson_Month_Choice1", "MoonPhases_Lesson_Month_Choice2", "MoonPhases_Lesson_Month_Choice3"],
            Answer = 0,
        },
        new("Narr_MoonPhases_Month_05") { Highlight = "angle" },
        new("Narr_MoonPhases_Month_06")
        {
            Enter = () =>
            {
                page.Age = MoonPhaseGeometry.SynodicMonth - 0.01;
                page.IsPlaying = false;
            },
            Highlight = "synodic",
        },
    ];
}
