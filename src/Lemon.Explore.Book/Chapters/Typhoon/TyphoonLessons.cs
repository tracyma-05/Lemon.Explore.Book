using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>
/// The typhoon page's spoken walkthroughs, one per view. Each picks the storm's depth and options, the sea's warmth and
/// the hours after landfall, or the month, the high and the hour of the track it talks about (the air keeps moving), so
/// the picture matches the words. Highlight ids are <see cref="TyphoonDiagram"/>'s.
/// </summary>
internal static class TyphoonLessons
{
    public static IReadOnlyList<LessonStep> For(TyphoonViewModel page) => page.Mode switch
    {
        TyphoonMode.Engine => Engine(page),
        TyphoonMode.Track => Track(page),
        _ => Storm(page),
    };

    private static void Labels(TyphoonViewModel page)
    {
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
    }

    /// <summary>Puts the probe where the strongest winds are, east of the centre.</summary>
    private static void ProbeAtEyewall(TyphoonViewModel page)
    {
        page.ProbeX = Math.Round(StormModel.RadiusOfMaxWind(page.CentralPressure), 1);
        page.ProbeY = 0;
    }

    /// <summary>Holds the track at an hour.</summary>
    private static void HoldTrack(TyphoonViewModel page, double hour)
    {
        page.TrackHour = Math.Min(hour, page.TrackLength);
        page.IsPlaying = false;
    }

    private static IReadOnlyList<LessonStep> Storm(TyphoonViewModel page)
    {
        // The task: another depth, another option, or the probe somewhere else.
        var atTask = (Pressure: 0.0, Spin: true, Southern: false, Moving: false, X: 0.0, Y: 0.0);
        return
        [
            new("Narr_Typhoon_Storm_01")
            {
                Enter = () =>
                {
                    page.Mode = TyphoonMode.Storm;
                    Labels(page);
                    page.IsPlaying = true;
                    page.CentralPressure = 950;
                    page.Spin = true;
                    page.Southern = false;
                    page.Moving = false;
                    page.ShowIsobars = true;
                    page.ProbeX = 160;
                    page.ProbeY = 0;
                },
                Highlight = "eye",
            },
            new("Narr_Typhoon_Storm_02") { Highlight = "isobars" },
            new("Narr_Typhoon_Storm_03") { Highlight = "probe" },
            new("Narr_Typhoon_Storm_04")
            {
                Choices = ["Typhoon_Lesson_Storm_Choice1", "Typhoon_Lesson_Storm_Choice2", "Typhoon_Lesson_Storm_Choice3"],
                Answer = 0,
            },
            new("Narr_Typhoon_Storm_05")
            {
                Enter = () => page.Spin = false,
                Highlight = "turn",
            },
            new("Narr_Typhoon_Storm_06")
            {
                Enter = () =>
                {
                    page.Spin = true;
                    ProbeAtEyewall(page);
                },
                Highlight = "probe",
            },
            new("Narr_Typhoon_Storm_07")
            {
                Enter = () =>
                {
                    page.SetPressureCommand.Execute("915");
                    ProbeAtEyewall(page);
                },
                Highlight = "isobars",
            },
            new("Narr_Typhoon_Storm_08")
            {
                Enter = () => page.Southern = true,
                Highlight = "turn",
            },
            new("Narr_Typhoon_Storm_09")
            {
                Enter = () =>
                {
                    page.Southern = false;
                    page.Moving = true;
                },
                Highlight = "strongside",
            },
            new("Narr_Typhoon_Storm_10")
            {
                Enter = () => atTask = (page.CentralPressure, page.Spin, page.Southern, page.Moving, page.ProbeX, page.ProbeY),
                TryIt = () => Math.Abs(page.CentralPressure - atTask.Pressure) >= 5 || page.Spin != atTask.Spin ||
                              page.Southern != atTask.Southern || page.Moving != atTask.Moving ||
                              Math.Abs(page.ProbeX - atTask.X) + Math.Abs(page.ProbeY - atTask.Y) >= 30,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Engine(TyphoonViewModel page)
    {
        // The task: another sea, landfall, or other hours ashore.
        var atTask = (Sea: 0.0, Landfall: false, Hours: 0.0);
        return
        [
            new("Narr_Typhoon_Engine_01")
            {
                Enter = () =>
                {
                    page.Mode = TyphoonMode.Engine;
                    Labels(page);
                    page.Landfall = false;
                    page.SetSeaCommand.Execute("29.5");
                    page.IsPlaying = true;
                },
                Highlight = "sea",
            },
            new("Narr_Typhoon_Engine_02") { Highlight = "inflow" },
            new("Narr_Typhoon_Engine_03") { Highlight = "eyewall" },
            new("Narr_Typhoon_Engine_04")
            {
                Choices = ["Typhoon_Lesson_Engine_Choice1", "Typhoon_Lesson_Engine_Choice2", "Typhoon_Lesson_Engine_Choice3"],
                Answer = 0,
            },
            new("Narr_Typhoon_Engine_05") { Highlight = "warmcore" },
            new("Narr_Typhoon_Engine_06") { Highlight = "outflow" },
            new("Narr_Typhoon_Engine_07")
            {
                Enter = () => page.SetSeaCommand.Execute("25.5"),
                Highlight = "showers",
            },
            new("Narr_Typhoon_Engine_08")
            {
                Enter = () =>
                {
                    page.SetSeaCommand.Execute("29.5");
                    page.Landfall = true;
                    page.LandfallHours = 12;
                    page.IsPlaying = false;
                },
                Highlight = "wind",
            },
            new("Narr_Typhoon_Engine_09")
            {
                Enter = () => atTask = (page.SeaTemperature, page.Landfall, page.LandfallHours),
                TryIt = () => Math.Abs(page.SeaTemperature - atTask.Sea) >= 0.5 || page.Landfall != atTask.Landfall ||
                              Math.Abs(page.LandfallHours - atTask.Hours) >= 3,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Track(TyphoonViewModel page)
    {
        // The task: another month, another reach of the high, or a storm somewhere else.
        var atTask = (Month: 0, Ridge: 0.0, Latitude: 0.0, Longitude: 0.0);
        return
        [
            new("Narr_Typhoon_Track_01")
            {
                Enter = () =>
                {
                    page.Mode = TyphoonMode.Track;
                    Labels(page);
                    page.Month = 8;
                    page.RidgeWest = 125;
                    page.UseStartCommand.Execute("Guam");
                    HoldTrack(page, 0);
                },
                Highlight = "start",
            },
            new("Narr_Typhoon_Track_02") { Highlight = "warmedge" },
            new("Narr_Typhoon_Track_03") { Highlight = "high" },
            new("Narr_Typhoon_Track_04")
            {
                Enter = () => HoldTrack(page, 96),
                Highlight = "storm",
            },
            new("Narr_Typhoon_Track_05")
            {
                Choices = ["Typhoon_Lesson_Track_Choice1", "Typhoon_Lesson_Track_Choice2", "Typhoon_Lesson_Track_Choice3"],
                Answer = 0,
            },
            new("Narr_Typhoon_Track_06")
            {
                Enter = () =>
                {
                    page.RidgeWest = 110;
                    HoldTrack(page, 160);
                },
                Highlight = TyphoonDiagram.LandfallId(0),
            },
            new("Narr_Typhoon_Track_07")
            {
                Enter = () =>
                {
                    page.RidgeWest = 145;
                    HoldTrack(page, page.TrackLength);
                },
                Highlight = "track",
            },
            new("Narr_Typhoon_Track_08") { Highlight = "place" },
            new("Narr_Typhoon_Track_09")
            {
                Enter = () => atTask = (page.Month, page.RidgeWest, page.StartLatitude, page.StartLongitude),
                TryIt = () => page.Month != atTask.Month || Math.Abs(page.RidgeWest - atTask.Ridge) >= 3 ||
                              Math.Abs(page.StartLatitude - atTask.Latitude) + Math.Abs(page.StartLongitude - atTask.Longitude) >= 1,
            },
        ];
    }
}
