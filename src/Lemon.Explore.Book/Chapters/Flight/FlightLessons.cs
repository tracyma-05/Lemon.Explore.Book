using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>
/// The flight page's spoken walkthroughs, one per view. Each sets the wing, the flight or the turn so the picture matches
/// the words, and the last step hands over to the viewer. Highlight ids are <see cref="FlightDiagram"/>'s.
/// </summary>
internal static class FlightLessons
{
    public static IReadOnlyList<LessonStep> For(FlightViewModel page) => page.Mode switch
    {
        FlightMode.Forces => Forces(page),
        FlightMode.Turn => Turn(page),
        _ => Wing(page),
    };

    /// <summary>The view set up with labels, playing.</summary>
    private static void Setup(FlightViewModel page, FlightMode mode)
    {
        page.Mode = mode;
        page.ShowLabels = true;
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Wing(FlightViewModel page)
    {
        // The task: the angle, the shape or upside down changed.
        var atTask = (Alpha: 0.0, Shape: WingShape.Cambered, Inverted: false);
        return
        [
            new("Narr_Flight_Wing_01")
            {
                Enter = () =>
                {
                    Setup(page, FlightMode.Wing);
                    page.Shape = WingShape.Cambered;
                    page.Inverted = false;
                    page.Alpha = 4;
                    page.AirSpeed = 200;
                    page.ShowPressure = true;
                    page.ShowStreamlines = true;
                    page.ShowSmoke = true;
                    page.ShowTimelines = false;
                    page.ShowArrows = false;
                },
                Highlight = "wing",
            },
            new("Narr_Flight_Wing_02") { Highlight = "stagnation" },
            new("Narr_Flight_Wing_03") { Highlight = "streamline" },
            new("Narr_Flight_Wing_04") { Highlight = "upper" },
            new("Narr_Flight_Wing_05")
            {
                Enter = () => page.ShowArrows = true,
                Highlight = "lower",
            },
            new("Narr_Flight_Wing_06") { Highlight = "wlift" },
            new("Narr_Flight_Wing_07")
            {
                Enter = () => page.ShowArrows = false,
                Choices = ["Flight_Lesson_Wing_Choice1", "Flight_Lesson_Wing_Choice2", "Flight_Lesson_Wing_Choice3"],
                Answer = 0,
            },
            new("Narr_Flight_Wing_08")
            {
                Enter = () => page.ShowTimelines = true,
                Highlight = "timeline",
            },
            new("Narr_Flight_Wing_09") { Highlight = "downwash" },
            new("Narr_Flight_Wing_10")
            {
                Enter = () => page.Alpha = 10,
                Highlight = "aoa",
            },
            new("Narr_Flight_Wing_11")
            {
                Enter = () => page.Alpha = 19,
                Highlight = "wake",
            },
            new("Narr_Flight_Wing_12")
            {
                Enter = () =>
                {
                    page.Alpha = 5;
                    atTask = (page.Alpha, page.Shape, page.Inverted);
                },
                Highlight = "wing",
                TryIt = () => page.Alpha != atTask.Alpha || page.Shape != atTask.Shape || page.Inverted != atTask.Inverted,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Forces(FlightViewModel page)
    {
        // The task: the nose raised or lowered, or the throttle moved.
        var atTask = (Alpha: 0.0, Throttle: 0.0);
        return
        [
            new("Narr_Flight_Forces_01")
            {
                Enter = () =>
                {
                    Setup(page, FlightMode.Forces);
                    page.Mass = 1000;
                    page.ApplyScenario("Cruise");
                },
                Highlight = "plane",
            },
            new("Narr_Flight_Forces_02") { Highlight = "lift" },
            new("Narr_Flight_Forces_03") { Highlight = "weight" },
            new("Narr_Flight_Forces_04") { Highlight = "thrust" },
            new("Narr_Flight_Forces_05") { Highlight = "drag" },
            new("Narr_Flight_Forces_06") { Highlight = "balance" },
            new("Narr_Flight_Forces_07")
            {
                Choices = ["Flight_Lesson_Forces_Choice1", "Flight_Lesson_Forces_Choice2", "Flight_Lesson_Forces_Choice3"],
                Answer = 0,
            },
            new("Narr_Flight_Forces_08")
            {
                Enter = () => page.ApplyScenario("Climb"),
                Highlight = "thrust",
            },
            new("Narr_Flight_Forces_09")
            {
                Enter = () => page.ApplyScenario("Glide"),
                Highlight = "path",
            },
            new("Narr_Flight_Forces_10")
            {
                Enter = () => page.ApplyScenario("Stall"),
                Highlight = "stallwarn",
            },
            new("Narr_Flight_Forces_11")
            {
                Enter = () =>
                {
                    page.ApplyScenario("Takeoff");
                    atTask = (page.PlaneAlpha, page.Throttle);
                },
                Highlight = "runway",
                TryIt = () => page.PlaneAlpha != atTask.Alpha || page.Throttle != atTask.Throttle || page.Altitude > 2,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Turn(FlightViewModel page)
    {
        // The task: the bank or the speed changed.
        var atTask = (Bank: 0.0, Speed: 0.0);
        return
        [
            new("Narr_Flight_Turn_01")
            {
                Enter = () =>
                {
                    Setup(page, FlightMode.Turn);
                    page.TurnSpeed = 180;
                    page.Bank = 0;
                    page.Demo = TurnDemo.Roll;
                },
                Highlight = "rollaxis",
            },
            new("Narr_Flight_Turn_02") { Highlight = "aileron" },
            new("Narr_Flight_Turn_03")
            {
                Enter = () => page.Demo = TurnDemo.Pitch,
                Highlight = "elevator",
            },
            new("Narr_Flight_Turn_04")
            {
                Enter = () => page.Demo = TurnDemo.Yaw,
                Highlight = "rudder",
            },
            new("Narr_Flight_Turn_05")
            {
                Enter = () =>
                {
                    page.Demo = TurnDemo.Turn;
                    page.Bank = 0;
                },
                Choices = ["Flight_Lesson_Turn_Choice1", "Flight_Lesson_Turn_Choice2", "Flight_Lesson_Turn_Choice3"],
                Answer = 0,
            },
            new("Narr_Flight_Turn_06")
            {
                Enter = () => page.Bank = 30,
                Highlight = "liftvector",
            },
            new("Narr_Flight_Turn_07") { Highlight = "centripetal" },
            new("Narr_Flight_Turn_08") { Highlight = "rearview" },
            new("Narr_Flight_Turn_09")
            {
                Enter = () => page.Bank = 60,
                Highlight = "loadfactor",
            },
            new("Narr_Flight_Turn_10") { Highlight = "topview" },
            new("Narr_Flight_Turn_11")
            {
                Enter = () =>
                {
                    page.Bank = 30;
                    atTask = (page.Bank, page.TurnSpeed);
                },
                Highlight = "liftvector",
                TryIt = () => page.Bank != atTask.Bank || page.TurnSpeed != atTask.Speed,
            },
        ];
    }
}
