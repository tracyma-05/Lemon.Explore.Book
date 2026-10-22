using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>
/// The water-cycle page's spoken walkthroughs, one per view. Each lights up the way of moving, picks the lift and the
/// air, or the stores view it talks about, so the picture matches the words. Highlight ids are
/// <see cref="WaterCycleDiagram"/>'s.
/// </summary>
internal static class WaterCycleLessons
{
    public static IReadOnlyList<LessonStep> For(WaterCycleViewModel page) => page.Mode switch
    {
        WaterMode.Cloud => Cloud(page),
        WaterMode.Stores => Stores(page),
        _ => Cycle(page),
    };

    private static string P(WaterProcess process) => WaterCycleDiagram.ProcessId(process);

    /// <summary>Running at its own pace.</summary>
    private static void Play(WaterCycleViewModel page)
    {
        page.SpeedChoice.Selected = "1";
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Cycle(WaterCycleViewModel page) =>
    [
        new("Narr_Water_Cycle_01")
        {
            Enter = () =>
            {
                page.Mode = WaterMode.Cycle;
                Play(page);
                page.Warming = 0;
                page.ShowLabels = true;
                page.FollowDrop = false;
                page.SelectedProcess = WaterProcess.None;
            },
            Highlight = "sun",
        },
        new("Narr_Water_Cycle_02")
        {
            Enter = () => page.SelectedProcess = WaterProcess.Evaporation,
            Highlight = P(WaterProcess.Evaporation),
        },
        new("Narr_Water_Cycle_03")
        {
            Enter = () => page.SelectedProcess = WaterProcess.Transpiration,
            Highlight = P(WaterProcess.Transpiration),
        },
        new("Narr_Water_Cycle_04")
        {
            Enter = () => page.SelectedProcess = WaterProcess.Condensation,
            Highlight = P(WaterProcess.Condensation),
        },
        new("Narr_Water_Cycle_05")
        {
            Enter = () => page.SelectedProcess = WaterProcess.Transport,
            Choices = ["Water_Lesson_Cycle_Choice1", "Water_Lesson_Cycle_Choice2", "Water_Lesson_Cycle_Choice3"],
            Answer = 0,
        },
        new("Narr_Water_Cycle_06")
        {
            Enter = () => page.SelectedProcess = WaterProcess.Precipitation,
            Highlight = P(WaterProcess.Precipitation),
        },
        new("Narr_Water_Cycle_07")
        {
            Enter = () => page.SelectedProcess = WaterProcess.Runoff,
            Highlight = P(WaterProcess.Runoff),
        },
        new("Narr_Water_Cycle_08")
        {
            Enter = () => page.SelectedProcess = WaterProcess.None,
            Highlight = "rainshadow",
        },
        new("Narr_Water_Cycle_09")
        {
            Enter = () =>
            {
                page.Route = DropRoute.River;
                page.FollowDrop = true;
                page.RestartCommand.Execute(null);
            },
            Highlight = "drop",
        },
        new("Narr_Water_Cycle_10")
        {
            // The task: light up a way of moving by clicking its label.
            Enter = () =>
            {
                page.FollowDrop = false;
                page.SelectedProcess = WaterProcess.None;
            },
            TryIt = () => page.SelectedProcess != WaterProcess.None,
        },
    ];

    private static IReadOnlyList<LessonStep> Cloud(WaterCycleViewModel page)
    {
        // The task: other air.
        var airAtTask = (Temperature: 0.0, Humidity: 0.0);
        return
        [
            new("Narr_Water_Cloud_01")
            {
                Enter = () =>
                {
                    page.Mode = WaterMode.Cloud;
                    Play(page);
                    page.Lift = CloudLift.Thermal;
                    page.GroundTemperature = 24;
                    page.HumidityPercent = 60;
                    page.RestartCommand.Execute(null);
                },
                Highlight = "thermal",
            },
            new("Narr_Water_Cloud_02") { Highlight = "parcel" },
            new("Narr_Water_Cloud_03")
            {
                Choices = ["Water_Lesson_Cloud_Choice1", "Water_Lesson_Cloud_Choice2", "Water_Lesson_Cloud_Choice3"],
                Answer = 0,
            },
            new("Narr_Water_Cloud_04") { Highlight = "cloudbase" },
            new("Narr_Water_Cloud_05")
            {
                Enter = () => page.HumidityPercent = 85,
                Highlight = "cloudbase",
            },
            new("Narr_Water_Cloud_06")
            {
                Enter = () =>
                {
                    page.Lift = CloudLift.Mountain;
                    page.HumidityPercent = 70;
                },
                Highlight = "leeward",
            },
            new("Narr_Water_Cloud_07")
            {
                Enter = () => page.Lift = CloudLift.Front,
                Highlight = "front",
            },
            new("Narr_Water_Cloud_08")
            {
                Enter = () =>
                {
                    page.Lift = CloudLift.Thermal;
                    page.HumidityPercent = 60;
                    airAtTask = (page.GroundTemperature, page.HumidityPercent);
                },
                Highlight = "cloudbase",
                TryIt = () => Math.Abs(page.GroundTemperature - airAtTask.Temperature) >= 3 ||
                              Math.Abs(page.HumidityPercent - airAtTask.Humidity) >= 5,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Stores(WaterCycleViewModel page) =>
    [
        new("Narr_Water_Stores_01")
        {
            Enter = () =>
            {
                page.Mode = WaterMode.Stores;
                Play(page);
                page.StoresView = StoresView.Spheres;
            },
            Highlight = "earth",
        },
        new("Narr_Water_Stores_02") { Highlight = "b-All" },
        new("Narr_Water_Stores_03") { Highlight = "b-FreshLiquid" },
        new("Narr_Water_Stores_04") { Highlight = "b-LakesRivers" },
        new("Narr_Water_Stores_05")
        {
            Choices = ["Water_Lesson_Stores_Choice1", "Water_Lesson_Stores_Choice2", "Water_Lesson_Stores_Choice3"],
            Answer = 0,
        },
        new("Narr_Water_Stores_06")
        {
            Enter = () => page.StoresView = StoresView.Flows,
            Highlight = "f-OceanEvaporation",
        },
        new("Narr_Water_Stores_07") { Highlight = "f-VapourToLand" },
        new("Narr_Water_Stores_08") { Highlight = "f-Runoff" },
        new("Narr_Water_Stores_09") { Highlight = "atmosphere" },
        new("Narr_Water_Stores_10")
        {
            // The task: click something in the picture.
            Enter = () => page.Card.Id = null,
            TryIt = () => page.Card.Id is not null,
        },
    ];
}
