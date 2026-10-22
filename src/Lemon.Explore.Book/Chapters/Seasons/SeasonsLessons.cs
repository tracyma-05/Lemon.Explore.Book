using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// The seasons chapter's spoken walkthroughs, one per view. The clips (<c>Narr_Seasons_*</c>) never mention the viewer's
/// own place or date, which the audio cannot know; they point at the card instead. Highlight ids are
/// <see cref="SeasonsDiagram"/>'s.
/// </summary>
internal static class SeasonsLessons
{
    /// <summary>A step is "December" once Earth is within this many days of the December solstice.</summary>
    private const double Near = 12;

    public static IReadOnlyList<LessonStep> For(SeasonsViewModel page) => page.Mode switch
    {
        SeasonsMode.SunPath => SunPath(page),
        SeasonsMode.Distance => Distance(page),
        _ => Tilt(page),
    };

    private static IReadOnlyList<LessonStep> Tilt(SeasonsViewModel page)
    {
        var june = SeasonsGeometry.EventDay(SeasonEvent.JuneSolstice);
        var december = SeasonsGeometry.EventDay(SeasonEvent.DecemberSolstice);
        return
        [
            new("Narr_Seasons_Tilt_01")
            {
                Enter = () =>
                {
                    page.Mode = SeasonsMode.Tilt;
                    page.Tilt = SeasonsGeometry.EarthTilt;
                    page.IsPlaying = true;
                },
                Highlight = "earth",
            },
            new("Narr_Seasons_Tilt_02") { Highlight = "orbit" },
            new("Narr_Seasons_Tilt_03") { Highlight = "axis" },
            new("Narr_Seasons_Tilt_04") { Highlight = "axis" },
            new("Narr_Seasons_Tilt_05")
            {
                Enter = () =>
                {
                    page.Day = june;
                    page.IsPlaying = false;
                },

                // Earth sits on the June mark now, so point at Earth itself.
                Highlight = "earth",
            },
            new("Narr_Seasons_Tilt_06")
            {
                Highlight = "december",
                TryIt = () => DaysApart(page.Day, december) < Near,
            },
            new("Narr_Seasons_Tilt_07")
            {
                Enter = () =>
                {
                    page.Day = december;
                    page.IsPlaying = false;
                },
                Highlight = "earth",
            },
            new("Narr_Seasons_Tilt_08")
            {
                Highlight = "axis",
                Choices = ["Seasons_Lesson_Tilt_Choice1", "Seasons_Lesson_Tilt_Choice2", "Seasons_Lesson_Tilt_Choice3"],
                Answer = 1,
            },
            new("Narr_Seasons_Tilt_09")
            {
                Enter = () =>
                {
                    page.Tilt = 0;
                    page.IsPlaying = true;
                },
                Highlight = "earth",
            },
            new("Narr_Seasons_Tilt_10") { Enter = () => page.Tilt = SeasonsGeometry.EarthTilt },
        ];
    }

    private static IReadOnlyList<LessonStep> SunPath(SeasonsViewModel page) =>
    [
        new("Narr_Seasons_SunPath_01")
        {
            Enter = () =>
            {
                page.Mode = SeasonsMode.SunPath;
                page.AnimatesYear = false;
                page.TimeOfDay = 5;
                page.IsPlaying = true;
            },
            Highlight = "pole",
        },
        new("Narr_Seasons_SunPath_02") { Highlight = "skysun" },
        new("Narr_Seasons_SunPath_03")
        {
            Enter = () =>
            {
                page.TimeOfDay = 12;
                page.IsPlaying = false;
            },
            Highlight = "noon",
        },
        new("Narr_Seasons_SunPath_04")
        {
            Enter = () =>
            {
                page.TimeOfDay = 9;
                page.IsPlaying = true;
            },
            Highlight = "pole",
        },
        new("Narr_Seasons_SunPath_05")
        {
            Enter = () =>
            {
                page.AnimatesYear = true;
                page.TimeOfDay = 12;
                page.IsPlaying = true;
            },
            Highlight = "paths",
        },
        new("Narr_Seasons_SunPath_06")
        {
            Choices = ["Seasons_Lesson_SunPath_Choice1", "Seasons_Lesson_SunPath_Choice2", "Seasons_Lesson_SunPath_Choice3"],
            Answer = 0,
        },
        new("Narr_Seasons_SunPath_07")
        {
            Enter = () =>
            {
                page.AnimatesYear = false;
                page.IsPlaying = true;
            },
            Highlight = "todaypath",
        },
    ];

    private static IReadOnlyList<LessonStep> Distance(SeasonsViewModel page) =>
    [
        new("Narr_Seasons_Distance_01")
        {
            Enter = () =>
            {
                page.Mode = SeasonsMode.Distance;
                page.IsPlaying = true;
            },
            Highlight = "orbit",
        },
        new("Narr_Seasons_Distance_02")
        {
            Choices = ["Seasons_Lesson_Distance_Choice1", "Seasons_Lesson_Distance_Choice2", "Seasons_Lesson_Distance_Choice3"],
            Answer = 0,
        },
        new("Narr_Seasons_Distance_03")
        {
            Enter = () =>
            {
                page.Day = SeasonsGeometry.PerihelionDay();
                page.IsPlaying = false;
            },
            Highlight = "perihelion",
        },
        new("Narr_Seasons_Distance_04")
        {
            Enter = () => page.Day = SeasonsGeometry.AphelionDay(),
            Highlight = "aphelion",
        },
        new("Narr_Seasons_Distance_05") { Enter = () => page.IsPlaying = true },
    ];

    private static double DaysApart(double a, double b)
    {
        var d = Math.Abs(a - b) % SeasonsGeometry.Year;
        return Math.Min(d, SeasonsGeometry.Year - d);
    }
}
