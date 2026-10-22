using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>
/// The volcanoes and rocks page's spoken walkthroughs, one per view. Each sets the magma, the grain or the magnifier so
/// the picture matches the words (a question is asked before the picture shows its answer), and the last step hands over
/// to the viewer. Highlight ids are <see cref="VolcanoDiagram"/>'s.
/// </summary>
internal static class VolcanoLessons
{
    public static IReadOnlyList<LessonStep> For(VolcanoViewModel page) => page.Mode switch
    {
        VolcanoMode.Cycle => Cycle(page),
        VolcanoMode.Magnifier => Magnifier(page),
        _ => Eruption(page),
    };

    /// <summary>A moment in the round: filling (0.12, 0.22), rising (0.33), erupting (0.4–0.8).</summary>
    private const double Filling = 0.12;

    private const double Filled = 0.22;
    private const double Rising = 0.33;

    private static IReadOnlyList<LessonStep> Eruption(VolcanoViewModel page) =>
    [
        new("Narr_Volc_Eruption_01")
        {
            Enter = () =>
            {
                page.Mode = VolcanoMode.Eruption;
                page.ShowLabels = true;
                page.ShowLevels = true;
                page.UsePreset("Stromboli");
                page.HoldRound(Filling);
            },
            Highlight = "chamber",
        },
        new("Narr_Volc_Eruption_02")
        {
            Enter = () => page.HoldRound(Filled),
            Highlight = "gauge",
        },
        new("Narr_Volc_Eruption_03")
        {
            Enter = () => page.HoldRound(Rising),
            Highlight = "bubbles",
        },
        new("Narr_Volc_Eruption_04") { Highlight = "satlevel" },

        // Stromboli: runny basalt, its bubbles bursting at the top; and a lava flow.
        new("Narr_Volc_Eruption_05")
        {
            Enter = () => page.RunRound(VolcanoModel.EruptionStart + 0.04),
            Highlight = "bombs",
        },
        new("Narr_Volc_Eruption_06")
        {
            Enter = () => page.HoldRound(0.72),
            Highlight = "lavaflow",
        },
        new("Narr_Volc_Eruption_07")
        {
            Enter = () => page.HoldRound(Filled),
            Choices = ["Volc_Lesson_Eruption_Choice1", "Volc_Lesson_Eruption_Choice2", "Volc_Lesson_Eruption_Choice3"],
            Answer = 0,
        },

        // Pinatubo: sticky dacite full of water, a column over 30 km high.
        new("Narr_Volc_Eruption_08")
        {
            Enter = () =>
            {
                page.UsePreset("Pinatubo");
                page.HoldRound(0.5);
            },
            Highlight = "column",
        },
        new("Narr_Volc_Eruption_09") { Highlight = "fraglevel" },
        new("Narr_Volc_Eruption_10")
        {
            Enter = () => page.HoldRound(0.72),
            Highlight = "pyroflow",
        },

        // Merapi: sticky but with little gas left, a dome.
        new("Narr_Volc_Eruption_11")
        {
            Enter = () =>
            {
                page.UsePreset("Merapi");
                page.HoldRound(0.64);
            },
            Highlight = "dome",
        },
        new("Narr_Volc_Eruption_12")
        {
            Enter = () =>
            {
                page.UsePreset("Kilauea");
                page.RunRound(0);
            },
            Highlight = "vent",
            TryIt = () => VolcanoModel.IsExplosive(page.Style),
        },
    ];

    private static IReadOnlyList<LessonStep> Cycle(VolcanoViewModel page) =>
    [
        new("Narr_Volc_Cycle_01")
        {
            Enter = () =>
            {
                page.Mode = VolcanoMode.Cycle;
                page.ShowLabels = true;
                page.JourneyId = null;
            },
        },
        new("Narr_Volc_Cycle_02") { Highlight = "rock_magma" },
        new("Narr_Volc_Cycle_03") { Highlight = "proc_crystallization" },
        new("Narr_Volc_Cycle_04") { Highlight = "proc_weathering" },
        new("Narr_Volc_Cycle_05") { Highlight = "proc_erosion" },
        new("Narr_Volc_Cycle_06") { Highlight = "proc_deposition" },
        new("Narr_Volc_Cycle_07") { Highlight = "proc_lithification" },
        new("Narr_Volc_Cycle_08") { Highlight = "proc_metamorphism" },
        new("Narr_Volc_Cycle_09") { Highlight = "proc_melting" },
        new("Narr_Volc_Cycle_10")
        {
            Choices = ["Volc_Lesson_Cycle_Choice1", "Volc_Lesson_Cycle_Choice2"],
            Answer = 0,
        },

        // A grain of sand: granite, sand, sandstone, mountains, sand again, never melted.
        new("Narr_Volc_Cycle_11")
        {
            Enter = () => page.Follow("Sand"),
            Highlight = "grain",
        },
        new("Narr_Volc_Cycle_12")
        {
            Enter = () => page.HoldAtStop("Sand", 5),
            Highlight = "proc_uplift",
        },
        new("Narr_Volc_Cycle_13")
        {
            Enter = () => page.HoldAtStop("Sand", 6),
            Highlight = "grain",
            TryIt = () => page.JourneyId is not null && page.JourneyId != "Sand",
        },
    ];

    private static IReadOnlyList<LessonStep> Magnifier(VolcanoViewModel page) =>
    [
        // Granite: slow cooling deep down.
        new("Narr_Volc_Magnifier_01")
        {
            Enter = () =>
            {
                page.Mode = VolcanoMode.Magnifier;
                page.Process = MagnifierProcess.Cooling;
                page.ShowLabels = true;
                page.CoolMagma = MagmaKind.Felsic;
                page.Gas = false;
                page.TwoStage = false;
                page.CoolRate = -3;
                page.CoolTo(0.3);
            },
            Highlight = "thermo",
        },
        new("Narr_Volc_Magnifier_02")
        {
            Enter = () => page.CoolTo(0.55),
            Highlight = "min_plagioclase",
        },
        new("Narr_Volc_Magnifier_03")
        {
            Enter = () => page.CoolTo(1),
            Highlight = "min_quartz",
        },
        new("Narr_Volc_Magnifier_04")
        {
            Choices = ["Volc_Lesson_Magnifier_Choice1", "Volc_Lesson_Magnifier_Choice2", "Volc_Lesson_Magnifier_Choice3"],
            Answer = 0,
        },
        new("Narr_Volc_Magnifier_05")
        {
            Enter = () =>
            {
                page.CoolRate = 3.9;
                page.CoolTo(1);
            },
            Highlight = "min_kfeldspar",
        },
        new("Narr_Volc_Magnifier_06")
        {
            Enter = () =>
            {
                page.CoolRate = 7.2;
                page.CoolTo(1);
            },
            Highlight = "glass",
        },
        new("Narr_Volc_Magnifier_07")
        {
            Enter = () =>
            {
                page.Gas = true;
                page.CoolTo(1);
            },
            Highlight = "vesicle",
        },

        // Mud settling into graded layers, then buried and hardened.
        new("Narr_Volc_Magnifier_08")
        {
            Enter = () =>
            {
                page.Process = MagnifierProcess.Settling;
                page.EmptyTank();
                page.Pour();
            },
            Highlight = "river",
        },
        new("Narr_Volc_Magnifier_09") { Highlight = "g_clay" },
        new("Narr_Volc_Magnifier_10")
        {
            Enter = () =>
            {
                page.SettleNow();
                if (page.Tank.Pours < 3)
                {
                    page.Pour();
                    page.SettleNow();
                    page.Pour();
                    page.SettleNow();
                }

                page.BurialKm = 2.5;
            },
            Highlight = "overburden",
        },

        // Shale squeezed and heated: slate, schist, gneiss.
        new("Narr_Volc_Magnifier_11")
        {
            Enter = () =>
            {
                page.Process = MagnifierProcess.Squeezing;
                page.SqueezeParent = "Shale";
                page.SqueezeDepthKm = 4;
            },
            Highlight = "mica",
        },
        new("Narr_Volc_Magnifier_12")
        {
            Enter = () => page.SqueezeDepthKm = 11,
            Highlight = "press",
        },
        new("Narr_Volc_Magnifier_13")
        {
            Enter = () => page.SqueezeDepthKm = 21,
            Highlight = "garnet",
        },
        new("Narr_Volc_Magnifier_14")
        {
            Enter = () => page.SqueezeDepthKm = 25.5,
            Highlight = "bands",
        },
        new("Narr_Volc_Magnifier_15")
        {
            Enter = () =>
            {
                page.SqueezeParent = "Limestone";
                page.SqueezeDepthKm = 3;
            },
            Highlight = "press",
            TryIt = () => page.SqueezedRock == "Marble",
        },
    ];
}
