using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>
/// The blue sky page's spoken walkthroughs, one per view. Each sets the time of day it talks about (noon, a low Sun,
/// twilight) and holds it there, so the picture matches the words. Highlight ids are <see cref="SkyDiagram"/>'s.
/// </summary>
internal static class BlueSkyLessons
{
    public static IReadOnlyList<LessonStep> For(BlueSkyViewModel page) => page.Mode switch
    {
        SkyMode.Scattering => Scattering(page),
        SkyMode.Path => Path(page),
        _ => Sky(page),
    };

    /// <summary>The usual amount of haze, which the walkthroughs start from.</summary>
    private const double NormalHaze = 0.3;

    private static IReadOnlyList<LessonStep> Sky(BlueSkyViewModel page)
    {
        // The task: move the Sun by dragging (or the timeline) to another time of day.
        var hourAtTask = 0.0;
        return
        [
            new("Narr_Sky_Sky_01")
            {
                Enter = () =>
                {
                    page.Mode = SkyMode.Sky;
                    page.LookAway = false;
                    page.Haze = NormalHaze;
                    page.SetTimeCommand.Execute("Afternoon");
                },
                Highlight = "sun",
            },
            new("Narr_Sky_Sky_02"),
            new("Narr_Sky_Sky_03")
            {
                Choices = ["Sky_Lesson_Sky_Choice1", "Sky_Lesson_Sky_Choice2", "Sky_Lesson_Sky_Choice3"],
                Answer = 0,
            },
            new("Narr_Sky_Sky_04") { Highlight = "sun" },
            new("Narr_Sky_Sky_05")
            {
                Enter = () =>
                {
                    page.SetTimeCommand.Execute("Golden");
                    page.IsPlaying = true;
                },
                Highlight = "sunpath",
            },
            new("Narr_Sky_Sky_06")
            {
                Enter = () =>
                {
                    page.LookAway = true;
                    page.SetTimeCommand.Execute("Twilight");
                },
                Highlight = "earthshadow",
            },
            new("Narr_Sky_Sky_07")
            {
                Enter = () =>
                {
                    page.LookAway = false;
                    page.SetTimeCommand.Execute("Afternoon");
                    hourAtTask = page.Hour;
                },
                Highlight = "sun",
                TryIt = () => Math.Abs(page.Hour - hourAtTask) > 0.75,
            },
            new("Narr_Sky_Sky_08"),
        ];
    }

    private static IReadOnlyList<LessonStep> Scattering(BlueSkyViewModel page)
    {
        // The task: change the haze noticeably.
        var hazeAtTask = 0.0;
        return
        [
            new("Narr_Sky_Scatter_01")
            {
                Enter = () =>
                {
                    page.Mode = SkyMode.Scattering;
                    page.Haze = NormalHaze;
                    page.SetTimeCommand.Execute("Noon");
                },
                Highlight = "beam",
            },
            new("Narr_Sky_Scatter_02") { Highlight = "box" },
            new("Narr_Sky_Scatter_03") { Highlight = "box" },
            new("Narr_Sky_Scatter_04") { Highlight = "sideeye" },
            new("Narr_Sky_Scatter_05") { Highlight = "endeye" },
            new("Narr_Sky_Scatter_06")
            {
                Choices = ["Sky_Lesson_Scatter_Choice1", "Sky_Lesson_Scatter_Choice2", "Sky_Lesson_Scatter_Choice3"],
                Answer = 0,
            },
            new("Narr_Sky_Scatter_07")
            {
                Enter = () => page.SetTimeCommand.Execute("Golden"),
                Highlight = "endeye",
            },
            new("Narr_Sky_Scatter_08")
            {
                Enter = () => hazeAtTask = page.Haze,
                TryIt = () => Math.Abs(page.Haze - hazeAtTask) > 0.25,
            },
            new("Narr_Sky_Scatter_09") { Highlight = "box" },
        ];
    }

    private static IReadOnlyList<LessonStep> Path(BlueSkyViewModel page)
    {
        // The task: move the Sun round the observer to another time of day.
        var hourAtTask = 0.0;
        return
        [
            new("Narr_Sky_Path_01")
            {
                Enter = () =>
                {
                    page.Mode = SkyMode.Path;
                    page.Haze = NormalHaze;
                    page.SetTimeCommand.Execute("Noon");
                },
                Highlight = "you",
            },
            new("Narr_Sky_Path_02") { Highlight = "ray" },
            new("Narr_Sky_Path_03") { Highlight = "overhead" },
            new("Narr_Sky_Path_04")
            {
                Choices = ["Sky_Lesson_Path_Choice1", "Sky_Lesson_Path_Choice2", "Sky_Lesson_Path_Choice3"],
                Answer = 0,
            },
            new("Narr_Sky_Path_05")
            {
                Enter = () => page.SetTimeCommand.Execute("Sunset"),
                Highlight = "ray",
            },
            new("Narr_Sky_Path_06")
            {
                Enter = () => page.SetTimeCommand.Execute("Twilight"),
                Highlight = "ray",
            },
            new("Narr_Sky_Path_07")
            {
                Enter = () => hourAtTask = page.Hour,
                Highlight = "you",
                TryIt = () => Math.Abs(page.Hour - hourAtTask) > 0.75,
            },
        ];
    }
}
