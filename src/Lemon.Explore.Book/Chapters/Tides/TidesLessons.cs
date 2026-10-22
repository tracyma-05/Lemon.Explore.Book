using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>
/// The tides page's spoken walkthroughs, one per view. The clips (<c>Narr_Tides_*</c>) never name the viewer's place or
/// the time shown, which the audio cannot know. Highlight ids are <see cref="TidesDiagram"/>'s.
/// </summary>
internal static class TidesLessons
{
    public static IReadOnlyList<LessonStep> For(TidesViewModel page) => page.Mode switch
    {
        TidesMode.SpringNeap => SpringNeap(page),
        TidesMode.Coast => Coast(page),
        _ => Bulges(page),
    };

    /// <summary>How far apart two moments of the model's month are, in days, going round the shorter way.</summary>
    private static double DaysBetween(double a, double b)
    {
        var d = Math.Abs(a - b) % MoonPhaseGeometry.SynodicMonth;
        return Math.Min(d, MoonPhaseGeometry.SynodicMonth - d);
    }

    private static IReadOnlyList<LessonStep> Bulges(TidesViewModel page)
    {
        // The task: drag the time on by a few hours.
        var dayAtTask = 0.0;
        return
        [
            new("Narr_Tides_Bulges_01")
            {
                Enter = () =>
                {
                    page.Mode = TidesMode.Bulges;
                    page.ShowForces = true;
                    page.IsPlaying = true;
                },
                Highlight = "bulges",
            },
            new("Narr_Tides_Bulges_02") { Highlight = "force" },
            new("Narr_Tides_Bulges_03") { Highlight = "highnear" },
            new("Narr_Tides_Bulges_04") { Highlight = "highfar" },
            new("Narr_Tides_Bulges_05")
            {
                Highlight = "spin",
                Choices = ["Tides_Lesson_Bulges_Choice1", "Tides_Lesson_Bulges_Choice2", "Tides_Lesson_Bulges_Choice3"],
                Answer = 0,
            },
            new("Narr_Tides_Bulges_06") { Highlight = "you" },
            new("Narr_Tides_Bulges_07") { Highlight = "moon" },
            new("Narr_Tides_Bulges_08")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    dayAtTask = page.Day;
                },
                Highlight = "you",
                TryIt = () => DaysBetween(page.Day, dayAtTask) > 0.1,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> SpringNeap(TidesViewModel page)
    {
        // The task: move the Moon a few days along its orbit.
        var dayAtTask = 0.0;
        return
        [
            new("Narr_Tides_Spring_01")
            {
                Enter = () =>
                {
                    page.Mode = TidesMode.SpringNeap;
                    page.IsPlaying = true;
                },
                Highlight = "sun",
            },
            new("Narr_Tides_Spring_02") { Highlight = "sunbulge" },
            new("Narr_Tides_Spring_03") { Highlight = "moonbulge" },
            new("Narr_Tides_Spring_04")
            {
                Enter = () => page.JumpToCommand.Execute(nameof(MoonPhase.New)),
                Highlight = "spring",
            },
            new("Narr_Tides_Spring_05")
            {
                Enter = () => page.JumpToCommand.Execute(nameof(MoonPhase.FirstQuarter)),
                Highlight = "moon",
                Choices = ["Tides_Lesson_Spring_Choice1", "Tides_Lesson_Spring_Choice2", "Tides_Lesson_Spring_Choice3"],
                Answer = 0,
            },
            new("Narr_Tides_Spring_06") { Highlight = "neap" },
            new("Narr_Tides_Spring_07")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "range",
            },
            new("Narr_Tides_Spring_08")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    dayAtTask = page.Day;
                },
                Highlight = "moon",
                TryIt = () => DaysBetween(page.Day, dayAtTask) > 3,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Coast(TidesViewModel page)
    {
        // The task: drag the time on by a few hours.
        var dayAtTask = 0.0;
        return
        [
            new("Narr_Tides_Coast_01")
            {
                Enter = () =>
                {
                    page.Mode = TidesMode.Coast;
                    page.IsPlaying = true;
                },
                Highlight = "water",
            },
            new("Narr_Tides_Coast_02") { Highlight = "staff" },
            new("Narr_Tides_Coast_03") { Highlight = "level" },
            new("Narr_Tides_Coast_04") { Highlight = "highwater" },
            new("Narr_Tides_Coast_05")
            {
                Choices = ["Tides_Lesson_Coast_Choice1", "Tides_Lesson_Coast_Choice2", "Tides_Lesson_Coast_Choice3"],
                Answer = 0,
            },
            new("Narr_Tides_Coast_06") { Highlight = "boat" },
            new("Narr_Tides_Coast_07")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    dayAtTask = page.Day;
                },
                Highlight = "level",
                TryIt = () => DaysBetween(page.Day, dayAtTask) > 0.1,
            },
            new("Narr_Tides_Coast_08")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "staff",
            },
        ];
    }
}
