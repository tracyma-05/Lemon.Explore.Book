using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>
/// The relativity page's spoken walkthroughs, one per view. Each sets the speed, the point of view and the moment so the
/// picture matches the words (a question is asked before the picture shows its answer), and the last step hands over to
/// the viewer. Highlight ids are <see cref="RelativityDiagram"/>'s.
/// </summary>
internal static class RelativityLessons
{
    public static IReadOnlyList<LessonStep> For(RelativityViewModel page) => page.Mode switch
    {
        RelativityMode.Simultaneity => Train(page),
        RelativityMode.Twins => Twins(page),
        _ => LightClock(page),
    };

    /// <summary>The view set up with labels, playing.</summary>
    private static void Setup(RelativityViewModel page, RelativityMode mode)
    {
        page.Mode = mode;
        page.ShowLabels = true;
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> LightClock(RelativityViewModel page)
    {
        // The task: the speed changed by hand.
        var atTask = 0.0;
        return
        [
            new("Narr_Rel_LightClock_01")
            {
                Enter = () =>
                {
                    Setup(page, RelativityMode.LightClock);
                    page.Frame = RelativityFrame.Ground;
                    page.Beta = 0.6;
                    page.ShowTriangle = false;
                    page.ShowTrail = true;
                    page.ResetClocks();
                },
                Highlight = "groundclock",
            },
            new("Narr_Rel_LightClock_02") { Highlight = "shipclock" },
            new("Narr_Rel_LightClock_03") { Highlight = "zigzag" },
            new("Narr_Rel_LightClock_04")
            {
                Choices = ["Rel_Lesson_LightClock_Choice1", "Rel_Lesson_LightClock_Choice2", "Rel_Lesson_LightClock_Choice3"],
                Answer = 0,
            },
            new("Narr_Rel_LightClock_05") { Highlight = "counter" },
            new("Narr_Rel_LightClock_06")
            {
                Enter = () => page.ShowTriangle = true,
                Highlight = "triangle",
            },
            new("Narr_Rel_LightClock_07")
            {
                Enter = () => page.Beta = 0.9,
                Highlight = "gamma",
            },
            new("Narr_Rel_LightClock_08")
            {
                Enter = () => page.Frame = RelativityFrame.Ship,
                Highlight = "frame",
            },
            new("Narr_Rel_LightClock_09")
            {
                Enter = () => page.Frame = RelativityFrame.Ground,
                Highlight = "flash",
            },
            new("Narr_Rel_LightClock_10")
            {
                Enter = () => atTask = page.Beta,
                Highlight = "gamma",
                TryIt = () => Math.Abs(page.Beta - atTask) > 0.15,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Train(RelativityViewModel page)
    {
        // The task: the speed or the point of view changed by hand.
        var atTask = (Beta: 0.0, Frame: RelativityFrame.Ground);
        return
        [
            new("Narr_Rel_Simultaneity_01")
            {
                Enter = () =>
                {
                    Setup(page, RelativityMode.Simultaneity);
                    page.Frame = RelativityFrame.Ship;
                    page.Beta = 0.6;
                    page.ShowLengths = true;
                    page.ShowClocks = false;
                    page.HoldTrain("Before");
                },
                Highlight = "lamp",
            },
            new("Narr_Rel_Simultaneity_02")
            {
                Enter = () => page.HoldTrain("Flight"),
                Highlight = "light",
            },
            new("Narr_Rel_Simultaneity_03")
            {
                Enter = () => page.HoldTrain("Both"),
                Highlight = "rear",
            },
            new("Narr_Rel_Simultaneity_04")
            {
                Enter = () =>
                {
                    page.Frame = RelativityFrame.Ground;
                    page.HoldTrain("Before");
                },
                Highlight = "watcher",
            },
            new("Narr_Rel_Simultaneity_05")
            {
                Enter = () => page.HoldTrain("Flight"),
                Highlight = "origin",
            },
            new("Narr_Rel_Simultaneity_06")
            {
                Choices = ["Rel_Lesson_Simultaneity_Choice1", "Rel_Lesson_Simultaneity_Choice2", "Rel_Lesson_Simultaneity_Choice3"],
                Answer = 0,
            },
            new("Narr_Rel_Simultaneity_07")
            {
                Enter = () => page.HoldTrain("Rear"),
                Highlight = "rear",
            },
            new("Narr_Rel_Simultaneity_08")
            {
                Enter = () => page.HoldTrain("Both"),
                Highlight = "trainlength",
            },
            new("Narr_Rel_Simultaneity_09")
            {
                Enter = () => page.ShowClocks = true,
                Highlight = "trainclocks",
            },
            new("Narr_Rel_Simultaneity_10")
            {
                Enter = () =>
                {
                    page.FlashAgain();
                    atTask = (page.Beta, page.Frame);
                },
                Highlight = "frame",
                TryIt = () => Math.Abs(page.Beta - atTask.Beta) > 0.1 || page.Frame != atTask.Frame,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Twins(RelativityViewModel page)
    {
        // The task: the trip changed by hand.
        var atTask = (Beta: 0.0, Distance: 0.0);
        return
        [
            new("Narr_Rel_Twins_01")
            {
                Enter = () =>
                {
                    Setup(page, RelativityMode.Twins);
                    page.Beta = 0.8;
                    page.Distance = 4;
                    page.ShowSignals = false;
                    page.ShowNowLines = false;
                    page.ShowAxes = false;
                    page.HoldTrip("Start");
                },
                Highlight = "grid",
            },
            new("Narr_Rel_Twins_02") { Highlight = "earthline" },
            new("Narr_Rel_Twins_03") { Highlight = "lightline" },
            new("Narr_Rel_Twins_04")
            {
                Enter = page.RestartTrip,
                Highlight = "shipline",
            },
            new("Narr_Rel_Twins_05")
            {
                Enter = () => page.HoldTrip("Start"),
                Choices = ["Rel_Lesson_Twins_Choice1", "Rel_Lesson_Twins_Choice2", "Rel_Lesson_Twins_Choice3"],
                Answer = 0,
            },
            new("Narr_Rel_Twins_06")
            {
                Enter = () => page.HoldTrip("Home"),
                Highlight = "traveller",
            },
            new("Narr_Rel_Twins_07") { Highlight = "turn" },
            new("Narr_Rel_Twins_08")
            {
                Enter = () =>
                {
                    page.ShowNowLines = true;
                    page.HoldTrip("Turn");
                },
                Highlight = "jump",
            },
            new("Narr_Rel_Twins_09")
            {
                Enter = () =>
                {
                    page.ShowNowLines = false;
                    page.ShowSignals = true;
                    page.RestartTrip();
                },
                Highlight = "signals",
            },
            new("Narr_Rel_Twins_10")
            {
                Enter = () => atTask = (page.Beta, page.Distance),
                Highlight = "turn",
                TryIt = () => Math.Abs(page.Beta - atTask.Beta) > 0.04 || Math.Abs(page.Distance - atTask.Distance) > 0.4,
            },
        ];
    }
}
