using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>
/// The day-and-night page's spoken walkthroughs, one per view. The clips (<c>Narr_DayNight_*</c>) never name the
/// viewer's own place or the date shown, which the audio cannot know. Highlight ids are <see cref="DayNightDiagram"/>'s.
/// </summary>
internal static class DayNightLessons
{
    public static IReadOnlyList<LessonStep> For(DayNightViewModel page) => page.Mode switch
    {
        DayNightMode.TimeZones => TimeZones(page),
        DayNightMode.Globe => Globe(page),
        _ => Map(page),
    };

    private static IReadOnlyList<LessonStep> Map(DayNightViewModel page)
    {
        // The task: pick any city other than the one chosen when the task starts.
        var cityAtTask = string.Empty;
        return
        [
            new("Narr_DayNight_Map_01")
            {
                Enter = () =>
                {
                    page.Mode = DayNightMode.Map;
                    page.AnimatesYear = false;
                    page.IsPlaying = true;
                },
                Highlight = "terminator",
            },
            new("Narr_DayNight_Map_02") { Highlight = "sun" },
            new("Narr_DayNight_Map_03") { Highlight = "sunriseline" },
            new("Narr_DayNight_Map_04") { Highlight = "noonline" },
            new("Narr_DayNight_Map_05")
            {
                Highlight = "sunriseline",
                Choices = ["DayNight_Lesson_Map_Choice1", "DayNight_Lesson_Map_Choice2", "DayNight_Lesson_Map_Choice3"],
                Answer = 0,
            },
            new("Narr_DayNight_Map_06")
            {
                Enter = () =>
                {
                    page.AnimatesYear = true;
                    page.IsPlaying = true;
                },
                Highlight = "terminator",
            },
            new("Narr_DayNight_Map_07")
            {
                Enter = () =>
                {
                    page.AnimatesYear = false;
                    cityAtTask = page.PlaceCity;
                },
                TryIt = () => page.PlaceCity.Length > 0 && page.PlaceCity != cityAtTask,
            },
            new("Narr_DayNight_Map_08"),
        ];
    }

    private static IReadOnlyList<LessonStep> TimeZones(DayNightViewModel page) =>
    [
        new("Narr_DayNight_Zones_01")
        {
            Enter = () =>
            {
                page.Mode = DayNightMode.TimeZones;
                page.AnimatesYear = false;
                page.IsPlaying = true;
            },
            Highlight = "zone",
        },
        new("Narr_DayNight_Zones_02") { Highlight = "utc" },
        new("Narr_DayNight_Zones_03") { Highlight = "newday" },
        new("Narr_DayNight_Zones_04")
        {
            Choices = ["DayNight_Lesson_Zones_Choice1", "DayNight_Lesson_Zones_Choice2", "DayNight_Lesson_Zones_Choice3"],
            Answer = 0,
        },
        new("Narr_DayNight_Zones_05") { Highlight = "dateline" },
        new("Narr_DayNight_Zones_06"),
    ];

    private static IReadOnlyList<LessonStep> Globe(DayNightViewModel page) =>
    [
        new("Narr_DayNight_Globe_01")
        {
            Enter = () =>
            {
                page.Mode = DayNightMode.Globe;
                page.AnimatesYear = false;
                page.ResetViewCommand.Execute(null);
                page.IsPlaying = true;
            },
            Highlight = "sunlight",
        },
        new("Narr_DayNight_Globe_02") { Highlight = "terminator" },
        new("Narr_DayNight_Globe_03") { Highlight = "axis" },
        new("Narr_DayNight_Globe_04")
        {
            Enter = () => page.GoToEvent(SeasonEvent.JuneSolstice),
            Highlight = "angle",
        },
        new("Narr_DayNight_Globe_05") { Highlight = "polarday" },
        new("Narr_DayNight_Globe_06")
        {
            Choices = ["DayNight_Lesson_Globe_Choice1", "DayNight_Lesson_Globe_Choice2", "DayNight_Lesson_Globe_Choice3"],
            Answer = 0,
        },
        new("Narr_DayNight_Globe_07")
        {
            Enter = () => page.GoToEvent(SeasonEvent.DecemberSolstice),
            Highlight = "terminator",
        },
    ];
}
