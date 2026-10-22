using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// The aurora page's spoken walkthroughs, one per view. Each sets the wind, the particle or the oval and holds the cycle,
/// the particle or the night at what it talks about, so the picture matches the words; the words never name the viewer's
/// place, the time or a hemisphere (the sky view follows the shared location). Highlight ids are <see cref="AuroraDiagram"/>'s.
/// </summary>
internal static class AuroraLessons
{
    public static IReadOnlyList<LessonStep> For(AuroraViewModel page) => page.Mode switch
    {
        AuroraMode.Particles => Particles(page),
        AuroraMode.Sky => Sky(page),
        _ => Shield(page),
    };

    /// <summary>The view set up with labels at normal speed.</summary>
    private static void Setup(AuroraViewModel page, AuroraMode mode)
    {
        page.Mode = mode;
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
        page.ShowBelts = true;
        page.IsPlaying = true;
    }

    private static void TypicalWind(AuroraViewModel page)
    {
        page.NoField = false;
        page.WindChoice.Selected = string.Empty;
        page.WindChoice.Selected = "Typical";
    }

    private static IReadOnlyList<LessonStep> Shield(AuroraViewModel page) =>
    [
        new("Narr_Aurora_Shield_01")
        {
            Enter = () =>
            {
                Setup(page, AuroraMode.Shield);
                TypicalWind(page);
                page.HoldCycle(0.04);
            },
            Highlight = "wind",
        },
        new("Narr_Aurora_Shield_02") { Highlight = "bowshock" },
        new("Narr_Aurora_Shield_03") { Highlight = "magnetopause" },
        new("Narr_Aurora_Shield_04")
        {
            Choices = ["Aurora_Lesson_Shield_Choice1", "Aurora_Lesson_Shield_Choice2", "Aurora_Lesson_Shield_Choice3"],
            Answer = 0,
        },
        new("Narr_Aurora_Shield_05")
        {
            Enter = () =>
            {
                page.WindChoice.Selected = "Storm";
                page.HoldCycle(0.04);
            },
            Highlight = "magnetopause",
        },
        new("Narr_Aurora_Shield_06")
        {
            Enter = () =>
            {
                TypicalWind(page);
                page.HoldCycle(0.11);
            },
            Highlight = "imf",
        },
        new("Narr_Aurora_Shield_07")
        {
            Enter = () => page.HoldCycle(AuroraDiagram.StageOpen + 0.02),
            Highlight = "xday",
        },
        new("Narr_Aurora_Shield_08")
        {
            Enter = () => page.HoldCycle(AuroraDiagram.StageSnap + 0.02),
            Highlight = "xtail",
        },
        new("Narr_Aurora_Shield_09")
        {
            Enter = () => page.HoldCycle(AuroraDiagram.StageSnap + 0.11),
            Highlight = "aurora",
        },
        new("Narr_Aurora_Shield_10")
        {
            Enter = () => page.IsPlaying = true,
            TryIt = () => page.Bz > 0 || page.NoField,
        },
    ];

    private static IReadOnlyList<LessonStep> Particles(AuroraViewModel page)
    {
        // The task: the other species, or another pitch angle.
        var atTask = (Species: AuroraSpecies.Electron, Pitch: 30.0);
        return
        [
            new("Narr_Aurora_Particles_01")
            {
                Enter = () =>
                {
                    Setup(page, AuroraMode.Particles);
                    page.ShowRingCurrent = false;
                    page.Species = AuroraSpecies.Electron;
                    page.EnergyChoice.Selected = "100";
                    page.Shell = 4;
                    page.PitchAngle = 30;
                    page.RestartCommand.Execute(null);
                },
                Highlight = "particle",
            },
            new("Narr_Aurora_Particles_02") { Highlight = "spiral" },
            new("Narr_Aurora_Particles_03") { Highlight = "mirror" },
            new("Narr_Aurora_Particles_04") { Highlight = "shell" },
            new("Narr_Aurora_Particles_05")
            {
                Choices = ["Aurora_Lesson_Particles_Choice1", "Aurora_Lesson_Particles_Choice2", "Aurora_Lesson_Particles_Choice3"],
                Answer = 0,
            },
            new("Narr_Aurora_Particles_06")
            {
                Enter = () =>
                {
                    page.PitchAngle = 2;
                    page.RestartCommand.Execute(null);
                },
                Highlight = "cone",
            },
            new("Narr_Aurora_Particles_07") { Highlight = "foot" },
            new("Narr_Aurora_Particles_08")
            {
                Enter = () =>
                {
                    page.PitchAngle = 30;
                    page.ShowBelts = true;
                    page.RestartCommand.Execute(null);
                },
                Highlight = "outer",
            },
            new("Narr_Aurora_Particles_09")
            {
                Enter = () => atTask = (page.Species, page.PitchAngle),
                TryIt = () => page.Species != atTask.Species || Math.Abs(page.PitchAngle - atTask.Pitch) > 5,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Sky(AuroraViewModel page)
    {
        // The task: Kp moved, or the electrons' energy changed.
        var atTask = (Kp: 3.0, Energy: 5.0);
        return
        [
            new("Narr_Aurora_Sky_01")
            {
                Enter = () =>
                {
                    Setup(page, AuroraMode.Sky);
                    page.Kp = 3;
                    page.EnergyKeV = 5;
                    page.HoldHour(page.BestHourOfNight());
                },
                Highlight = "oval",
            },
            new("Narr_Aurora_Sky_02")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "night",
            },
            new("Narr_Aurora_Sky_03")
            {
                Enter = () => page.HoldHour(page.BestHourOfNight()),
                Highlight = "you",
            },
            new("Narr_Aurora_Sky_04")
            {
                Choices = ["Aurora_Lesson_Sky_Choice1", "Aurora_Lesson_Sky_Choice2", "Aurora_Lesson_Sky_Choice3"],
                Answer = 0,
            },
            new("Narr_Aurora_Sky_05")
            {
                Enter = () =>
                {
                    page.Kp = 7;
                    page.HoldHour(page.BestHourOfNight());
                },
                Highlight = "seen",
            },
            new("Narr_Aurora_Sky_06") { Highlight = "curtain" },
            new("Narr_Aurora_Sky_07")
            {
                Enter = () => page.EnergyKeV = 5,
                Highlight = "green",
            },
            new("Narr_Aurora_Sky_08")
            {
                Enter = () => page.EnergyKeV = 0.5,
                Highlight = "red",
            },
            new("Narr_Aurora_Sky_09")
            {
                Enter = () => atTask = (page.Kp, page.EnergyKeV),
                TryIt = () => Math.Abs(page.Kp - atTask.Kp) >= 1 || Math.Abs(Math.Log(page.EnergyKeV / atTask.Energy)) > 0.4,
            },
        ];
    }
}
