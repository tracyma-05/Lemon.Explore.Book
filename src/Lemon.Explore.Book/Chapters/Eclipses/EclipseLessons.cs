using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>
/// The eclipse chapter's spoken walkthroughs, one per mode. The steps that talk about a shadow hold the Moon where that
/// shadow is drawn (the Moon's callouts show only near new moon). Highlight ids are <see cref="EclipseDiagram"/>'s.
/// </summary>
internal static class EclipseLessons
{
    public static IReadOnlyList<LessonStep> For(EclipseViewModel page) => page.Mode switch
    {
        EclipseMode.Lunar => Lunar(page),
        EclipseMode.Seasons => Seasons(page),
        _ => Solar(page),
    };

    /// <summary>The angle between two places on the Moon's orbit, 0…180°.</summary>
    private static double AngleBetween(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return Math.Min(d, 360 - d);
    }

    private static IReadOnlyList<LessonStep> Solar(EclipseViewModel page)
    {
        var angleAtTask = 0.0;
        return
        [
            new("Narr_Eclipse_Solar_01")
            {
                Enter = () =>
                {
                    page.Mode = EclipseMode.Solar;
                    page.IsMoonNear = true;
                    page.ObserverOffset = 0;
                    page.ReplayCommand.Execute(null);
                },
                Highlight = "sun",
            },
            new("Narr_Eclipse_Solar_02") { Highlight = "newmoon" },
            new("Narr_Eclipse_Solar_03")
            {
                Enter = () => page.HoldMoonAt(350),
                Highlight = "umbra",
            },
            new("Narr_Eclipse_Solar_04") { Highlight = "penumbra" },
            new("Narr_Eclipse_Solar_05")
            {
                Enter = () => page.HoldMoonAt(0),
                Highlight = "observer",
                Choices = ["Eclipse_Lesson_Solar_Choice1", "Eclipse_Lesson_Solar_Choice2", "Eclipse_Lesson_Solar_Choice3"],
                Answer = 0,
            },
            new("Narr_Eclipse_Solar_06") { Highlight = "umbra" },
            new("Narr_Eclipse_Solar_07")
            {
                Enter = () =>
                {
                    page.IsMoonNear = false;
                    page.HoldMoonAt(0);
                },
                Highlight = "antumbra",
            },
            new("Narr_Eclipse_Solar_08")
            {
                Enter = () =>
                {
                    page.IsMoonNear = true;
                    angleAtTask = page.MoonAngle;
                },
                Highlight = "moon",
                TryIt = () => AngleBetween(page.MoonAngle, angleAtTask) > 15,
            },
            new("Narr_Eclipse_Solar_09")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "fullmoon",
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Lunar(EclipseViewModel page)
    {
        var pathAtTask = LunarPath.Central;
        return
        [
            new("Narr_Eclipse_Lunar_01")
            {
                Enter = () =>
                {
                    page.Mode = EclipseMode.Lunar;
                    page.LunarPath = LunarPath.Central;
                    page.ReplayCommand.Execute(null);
                },
                Highlight = "earth",
            },
            new("Narr_Eclipse_Lunar_02") { Highlight = "fullmoon" },
            new("Narr_Eclipse_Lunar_03") { Highlight = "umbra" },
            new("Narr_Eclipse_Lunar_04")
            {
                Enter = () => page.HoldMoonAt(180),
                Highlight = "moon",
                Choices = ["Eclipse_Lesson_Lunar_Choice1", "Eclipse_Lesson_Lunar_Choice2", "Eclipse_Lesson_Lunar_Choice3"],
                Answer = 0,
            },
            new("Narr_Eclipse_Lunar_05") { Highlight = "moon" },
            new("Narr_Eclipse_Lunar_06")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "earth",
            },
            new("Narr_Eclipse_Lunar_07")
            {
                Enter = () =>
                {
                    pathAtTask = page.LunarPath;
                    page.ReplayCommand.Execute(null);
                },
                TryIt = () => page.LunarPath != pathAtTask,
            },
            new("Narr_Eclipse_Lunar_08")
            {
                Enter = () =>
                {
                    page.LunarPath = LunarPath.Grazing;
                    page.ReplayCommand.Execute(null);
                },
                Highlight = "penumbra",
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Seasons(EclipseViewModel page)
    {
        var dayAtTask = 0.0;
        return
        [
            new("Narr_Eclipse_Seasons_01")
            {
                Enter = () =>
                {
                    page.Mode = EclipseMode.Seasons;
                    page.IsPlaying = true;
                },
                Highlight = "earth",
            },
            new("Narr_Eclipse_Seasons_02") { Highlight = "moonorbit" },
            new("Narr_Eclipse_Seasons_03") { Highlight = "nodeline" },
            new("Narr_Eclipse_Seasons_04")
            {
                Choices = ["Eclipse_Lesson_Seasons_Choice1", "Eclipse_Lesson_Seasons_Choice2", "Eclipse_Lesson_Seasons_Choice3"],
                Answer = 0,
            },
            new("Narr_Eclipse_Seasons_05") { Highlight = "moon" },
            new("Narr_Eclipse_Seasons_06")
            {
                // Skips ahead to just before the next eclipse season.
                Enter = () => page.ReplayCommand.Execute(null),
                Highlight = "season",
            },
            new("Narr_Eclipse_Seasons_07") { Highlight = "nodeline" },
            new("Narr_Eclipse_Seasons_08")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    dayAtTask = page.SeasonDay;
                },
                TryIt = () => Math.Abs(page.SeasonDay - dayAtTask) > 20,
            },
            new("Narr_Eclipse_Seasons_09")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "season",
            },
        ];
    }
}
