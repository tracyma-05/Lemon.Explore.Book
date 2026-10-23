using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>
/// The engine-and-fridge page's spoken walkthroughs, one per view. Each holds the stroke or the step of the cycle it talks
/// about (the gas keeps moving), picks the room, the door or the temperatures, so the picture matches the words. Highlight
/// ids are <see cref="HeatDiagram"/>'s.
/// </summary>
internal static class HeatEnginesLessons
{
    public static IReadOnlyList<LessonStep> For(HeatEnginesViewModel page) => page.Mode switch
    {
        HeatMode.Fridge => Fridge(page),
        HeatMode.Cycle => Cycle(page),
        _ => Engine(page),
    };

    private static void Labels(HeatEnginesViewModel page)
    {
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
    }

    /// <summary>The cycle held in the middle of a step (0…3, in the engine's order).</summary>
    private static void HoldStep(HeatEnginesViewModel page, int step)
    {
        page.Phase = step + 0.5;
        page.IsPlaying = false;
    }

    private static IReadOnlyList<LessonStep> Engine(HeatEnginesViewModel page)
    {
        // The task: turn the crank by hand, or jump to a stroke.
        var angleAtTask = 0.0;
        return
        [
            new("Narr_Heat_Engine_01")
            {
                Enter = () =>
                {
                    page.Mode = HeatMode.Engine;
                    Labels(page);
                    page.CompressionRatio = EngineModel.DefaultCompressionRatio;
                    page.RpmChoice.Selected = "3000";
                    page.JumpToStrokeCommand.Execute(nameof(EngineStroke.Intake));
                },
                Highlight = "intakevalve",
            },
            new("Narr_Heat_Engine_02") { Highlight = "camshaft" },
            new("Narr_Heat_Engine_03")
            {
                Enter = () => page.JumpToStrokeCommand.Execute(nameof(EngineStroke.Compression)),
                Highlight = "gas",
            },
            new("Narr_Heat_Engine_04")
            {
                Choices = ["Heat_Lesson_Engine_Choice1", "Heat_Lesson_Engine_Choice2", "Heat_Lesson_Engine_Choice3"],
                Answer = 0,
            },
            new("Narr_Heat_Engine_05")
            {
                Enter = () => page.JumpToStrokeCommand.Execute("Spark"),
                Highlight = "sparkplug",
            },
            new("Narr_Heat_Engine_06")
            {
                Enter = () => page.JumpToStrokeCommand.Execute(nameof(EngineStroke.Power)),
                Highlight = "piston",
            },
            new("Narr_Heat_Engine_07") { Highlight = "crank" },
            new("Narr_Heat_Engine_08")
            {
                Enter = () => page.JumpToStrokeCommand.Execute(nameof(EngineStroke.Exhaust)),
                Highlight = "exhaustvalve",
            },
            new("Narr_Heat_Engine_09") { Highlight = "coolant" },
            new("Narr_Heat_Engine_10")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "dial",
            },
            new("Narr_Heat_Engine_11")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    angleAtTask = page.Angle;
                },
                TryIt = () =>
                {
                    var moved = Math.Abs(EngineModel.Wrap(page.Angle - angleAtTask));
                    return Math.Min(moved, 720 - moved) >= 30;
                },
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Fridge(HeatEnginesViewModel page)
    {
        // The task: open or shut the door, or another room.
        var atTask = (Door: false, Room: 0.0);
        return
        [
            new("Narr_Heat_Fridge_01")
            {
                Enter = () =>
                {
                    page.Mode = HeatMode.Fridge;
                    Labels(page);
                    page.IsPlaying = true;
                    page.DoorOpen = false;
                    page.Room = FridgeModel.DefaultRoom;
                },
                Highlight = "evaporator",
            },
            new("Narr_Heat_Fridge_02") { Highlight = "compressor" },
            new("Narr_Heat_Fridge_03")
            {
                Choices = ["Heat_Lesson_Fridge_Choice1", "Heat_Lesson_Fridge_Choice2", "Heat_Lesson_Fridge_Choice3"],
                Answer = 0,
            },
            new("Narr_Heat_Fridge_04") { Highlight = "condenser" },
            new("Narr_Heat_Fridge_05") { Highlight = "capillary" },
            new("Narr_Heat_Fridge_06") { Highlight = "heatout" },
            new("Narr_Heat_Fridge_07")
            {
                Enter = () => page.DoorOpen = true,
                Highlight = "door",
            },
            new("Narr_Heat_Fridge_08")
            {
                Enter = () =>
                {
                    page.DoorOpen = false;
                    page.Room = 35;
                },
                Highlight = "room",
            },
            new("Narr_Heat_Fridge_09")
            {
                Enter = () =>
                {
                    page.Room = FridgeModel.DefaultRoom;
                    atTask = (page.DoorOpen, page.Room);
                },
                TryIt = () => page.DoorOpen != atTask.Door || Math.Abs(page.Room - atTask.Room) >= 3,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Cycle(HeatEnginesViewModel page)
    {
        // The task: other temperatures.
        var atTask = (Hot: 0.0, Cold: 0.0);
        return
        [
            new("Narr_Heat_Cycle_01")
            {
                Enter = () =>
                {
                    page.Mode = HeatMode.Cycle;
                    Labels(page);
                    page.Direction = CycleDirection.Engine;
                    page.Hot = 500;
                    page.Cold = 300;
                    page.IsPlaying = true;
                },
                Highlight = "hot",
            },
            new("Narr_Heat_Cycle_02")
            {
                Enter = () => HoldStep(page, 0),
                Highlight = "machine",
            },
            new("Narr_Heat_Cycle_03")
            {
                Enter = () => HoldStep(page, 2),
                Highlight = "coldflow",
            },
            new("Narr_Heat_Cycle_04")
            {
                Choices = ["Heat_Lesson_Cycle_Choice1", "Heat_Lesson_Cycle_Choice2", "Heat_Lesson_Cycle_Choice3"],
                Answer = 0,
            },
            new("Narr_Heat_Cycle_05")
            {
                Enter = () =>
                {
                    page.UsePresetCommand.Execute("PowerStation");
                    page.IsPlaying = true;
                },
                Highlight = "work",
            },
            new("Narr_Heat_Cycle_06") { Highlight = "loop" },
            new("Narr_Heat_Cycle_07")
            {
                Enter = () => page.UsePresetCommand.Execute("Fridge"),
                Highlight = "coldflow",
            },
            new("Narr_Heat_Cycle_08")
            {
                Enter = () => page.UsePresetCommand.Execute("HeatPump"),
                Highlight = "hotflow",
            },
            new("Narr_Heat_Cycle_09")
            {
                Enter = () =>
                {
                    page.Direction = CycleDirection.Engine;
                    page.Hot = 500;
                    page.Cold = 300;
                    atTask = (page.Hot, page.Cold);
                },
                TryIt = () => Math.Abs(page.Hot - atTask.Hot) >= 10 || Math.Abs(page.Cold - atTask.Cold) >= 10,
            },
        ];
    }
}
