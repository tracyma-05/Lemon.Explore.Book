using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>
/// The greenhouse page's spoken walkthroughs, one per view. Each picks the CO₂, the gas and its wiggle, or the hour and
/// the greenhouse's options it talks about (the light keeps moving), so the picture matches the words. Highlight ids are
/// <see cref="GreenhouseDiagram"/>'s.
/// </summary>
internal static class GreenhouseLessons
{
    public static IReadOnlyList<LessonStep> For(GreenhouseViewModel page) => page.Mode switch
    {
        GreenhouseMode.Molecules => Molecules(page),
        GreenhouseMode.GlassHouse => GlassHouse(page),
        _ => Planet(page),
    };

    private static void Labels(GreenhouseViewModel page)
    {
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
    }

    private static IReadOnlyList<LessonStep> Planet(GreenhouseViewModel page)
    {
        // The task: other air.
        var atTask = (Co2: 0.0, NoGases: false);
        return
        [
            new("Narr_Green_Planet_01")
            {
                Enter = () =>
                {
                    page.Mode = GreenhouseMode.Planet;
                    Labels(page);
                    page.IsPlaying = true;
                    page.ShowBudget = false;
                    page.SetCo2Command.Execute("425");
                },
                Highlight = "sun",
            },
            new("Narr_Green_Planet_02") { Highlight = "ground" },
            new("Narr_Green_Planet_03") { Highlight = GreenhouseDiagram.GasId(Gas.CO2) },
            new("Narr_Green_Planet_04")
            {
                Choices = ["Green_Lesson_Planet_Choice1", "Green_Lesson_Planet_Choice2", "Green_Lesson_Planet_Choice3"],
                Answer = 0,
            },
            new("Narr_Green_Planet_05")
            {
                Enter = () => page.ShowBudget = true,
                Highlight = GreenhouseDiagram.BudgetId("Back"),
            },
            new("Narr_Green_Planet_06")
            {
                Enter = () =>
                {
                    page.ShowBudget = false;
                    page.NoGases = true;
                },
                Highlight = "ground",
            },
            new("Narr_Green_Planet_07")
            {
                Enter = () => page.SetCo2Command.Execute("280"),
                Highlight = "ground",
            },
            new("Narr_Green_Planet_08")
            {
                Enter = () => page.SetCo2Command.Execute("560"),
                Highlight = "emission",
            },
            new("Narr_Green_Planet_09") { Highlight = "profile" },
            new("Narr_Green_Planet_10")
            {
                Enter = () =>
                {
                    page.SetCo2Command.Execute("425");
                    atTask = (page.Co2, page.NoGases);
                },
                TryIt = () => page.NoGases != atTask.NoGases || Math.Abs(page.Co2 - atTask.Co2) >= 20,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Molecules(GreenhouseViewModel page)
    {
        // The task: another gas or another wiggle.
        var atTask = (Gas: Gas.CO2, Vibration: string.Empty);
        return
        [
            new("Narr_Green_Molecules_01")
            {
                Enter = () =>
                {
                    page.Mode = GreenhouseMode.Molecules;
                    Labels(page);
                    page.IsPlaying = true;
                    page.Gas = Gas.N2;
                },
                Highlight = GreenhouseDiagram.GasId(Gas.N2),
            },
            new("Narr_Green_Molecules_02")
            {
                Enter = () =>
                {
                    page.Gas = Gas.CO2;
                    page.VibrationId = "SymmetricStretch";
                },
                Highlight = GreenhouseDiagram.VibrationTargetId("SymmetricStretch"),
            },
            new("Narr_Green_Molecules_03")
            {
                Enter = () => page.VibrationId = "Bend",
                Highlight = "dipole",
            },
            new("Narr_Green_Molecules_04")
            {
                Choices = ["Green_Lesson_Molecules_Choice1", "Green_Lesson_Molecules_Choice2", "Green_Lesson_Molecules_Choice3"],
                Answer = 0,
            },
            new("Narr_Green_Molecules_05") { Highlight = GreenhouseDiagram.GasId(Gas.CO2) },
            new("Narr_Green_Molecules_06") { Highlight = "spectrum" },
            new("Narr_Green_Molecules_07") { Highlight = "window" },
            new("Narr_Green_Molecules_08")
            {
                Enter = () => page.Gas = Gas.H2O,
                Highlight = GreenhouseDiagram.GasId(Gas.H2O),
            },
            new("Narr_Green_Molecules_09")
            {
                Enter = () => atTask = (page.Gas, page.VibrationId),
                TryIt = () => page.Gas != atTask.Gas || page.VibrationId != atTask.Vibration,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> GlassHouse(GreenhouseViewModel page)
    {
        // The task: another hour, or the vents or the window.
        var atTask = (Hour: 0.0, Vents: false, Window: false);
        return
        [
            new("Narr_Green_Glass_01")
            {
                Enter = () =>
                {
                    page.Mode = GreenhouseMode.GlassHouse;
                    Labels(page);
                    page.Season = Season.Summer;
                    page.VentsOpen = false;
                    page.SaltPane = false;
                    page.CarWindowOpen = false;
                    page.JumpToHourCommand.Execute("14");
                },
                Highlight = "greenhouse",
            },
            new("Narr_Green_Glass_02") { Highlight = "pane" },
            new("Narr_Green_Glass_03") { Highlight = "floor" },
            new("Narr_Green_Glass_04")
            {
                Choices = ["Green_Lesson_Glass_Choice1", "Green_Lesson_Glass_Choice2", "Green_Lesson_Glass_Choice3"],
                Answer = 0,
            },
            new("Narr_Green_Glass_05") { Highlight = "warmair" },
            new("Narr_Green_Glass_06")
            {
                Enter = () => page.VentsOpen = true,
                Highlight = "vents",
            },
            new("Narr_Green_Glass_07")
            {
                Enter = () =>
                {
                    page.VentsOpen = false;
                    page.SaltPane = true;
                },
                Highlight = "pane",
            },
            new("Narr_Green_Glass_08")
            {
                Enter = () => page.SaltPane = false,
                Highlight = "car",
            },
            new("Narr_Green_Glass_09")
            {
                Enter = () => atTask = (page.Hour, page.VentsOpen, page.CarWindowOpen),
                TryIt = () => page.VentsOpen != atTask.Vents || page.CarWindowOpen != atTask.Window ||
                              Math.Abs(page.Hour - atTask.Hour) >= 1,
            },
        ];
    }
}
