using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>
/// The sea-and-land-breeze page's spoken walkthroughs, one per view. Each holds the time of day, the step of the cell or
/// the month it talks about (the air keeps moving), so the picture matches the words. Highlight ids are
/// <see cref="BreezeDiagram"/>'s.
/// </summary>
internal static class SeaBreezeLessons
{
    public static IReadOnlyList<LessonStep> For(SeaBreezeViewModel page) => page.Mode switch
    {
        BreezeMode.Cell => Cell(page),
        BreezeMode.Monsoon => Monsoon(page),
        _ => Coast(page),
    };

    /// <summary>Everything drawn, at the normal pace.</summary>
    private static void ShowAll(SeaBreezeViewModel page, bool pressure)
    {
        page.SpeedChoice.Selected = "1";
        page.ShowFlow = true;
        page.ShowLabels = true;
        page.ShowPressure = pressure;
    }

    /// <summary>The cell held at the end of a step, everything in it drawn.</summary>
    private static void Hold(SeaBreezeViewModel page, CellStage stage)
    {
        page.StepTime = ((int)stage + 0.7) * BreezeModel.StageSeconds;
        page.IsPlaying = false;
    }

    /// <summary>The cell by day (land 32 °C, sea 24 °C) or by night (land 18 °C).</summary>
    private static void CellAir(SeaBreezeViewModel page, CellTime time)
    {
        page.CellTime = time;
        page.LandTemperature = time == CellTime.Day ? 32 : 18;
        page.SeaTemperature = 24;
    }

    private static IReadOnlyList<LessonStep> Coast(SeaBreezeViewModel page)
    {
        // The task: another time of day.
        var hourAtTask = 0.0;
        return
        [
            new("Narr_Breeze_Coast_01")
            {
                Enter = () =>
                {
                    page.Mode = BreezeMode.Coast;
                    ShowAll(page, pressure: false);
                    page.Sky = BreezeSky.Clear;
                    page.JumpToHourCommand.Execute("14");
                },
                Highlight = "land",
            },
            new("Narr_Breeze_Coast_02") { Highlight = "sea" },
            new("Narr_Breeze_Coast_03") { Highlight = "rising" },
            new("Narr_Breeze_Coast_04")
            {
                Choices = ["Breeze_Lesson_Coast_Choice1", "Breeze_Lesson_Coast_Choice2", "Breeze_Lesson_Coast_Choice3"],
                Answer = 0,
            },
            new("Narr_Breeze_Coast_05") { Highlight = "seabreeze" },
            new("Narr_Breeze_Coast_06")
            {
                Enter = () => page.ShowPressure = true,
                Highlight = "low",
            },
            new("Narr_Breeze_Coast_07")
            {
                Enter = () => page.ShowPressure = false,
                Highlight = "front",
            },
            new("Narr_Breeze_Coast_08")
            {
                Enter = () => page.JumpToHourCommand.Execute("3"),
                Highlight = "landbreeze",
            },
            new("Narr_Breeze_Coast_09")
            {
                Enter = () =>
                {
                    page.Sky = BreezeSky.Overcast;
                    page.JumpToHourCommand.Execute("14");
                },
                Highlight = "calm",
            },
            new("Narr_Breeze_Coast_10")
            {
                Enter = () =>
                {
                    page.Sky = BreezeSky.Clear;
                    page.JumpToHourCommand.Execute("8.5");
                    hourAtTask = page.Hour;
                },
                TryIt = () =>
                {
                    var moved = Math.Abs(BreezeModel.Wrap(page.Hour - hourAtTask));
                    return Math.Min(moved, 24 - moved) >= 2;
                },
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Cell(SeaBreezeViewModel page)
    {
        // The task: other temperatures.
        var airAtTask = (Land: 0.0, Sea: 0.0);
        return
        [
            new("Narr_Breeze_Cell_01")
            {
                Enter = () =>
                {
                    page.Mode = BreezeMode.Cell;
                    ShowAll(page, pressure: false);
                    CellAir(page, CellTime.Day);
                    Hold(page, CellStage.Even);
                },
                Highlight = "isobars",
            },
            new("Narr_Breeze_Cell_02")
            {
                Enter = () => Hold(page, CellStage.Heating),
                Highlight = "land",
            },
            new("Narr_Breeze_Cell_03")
            {
                Highlight = "isobars",
                Choices = ["Breeze_Lesson_Cell_Choice1", "Breeze_Lesson_Cell_Choice2", "Breeze_Lesson_Cell_Choice3"],
                Answer = 0,
            },
            new("Narr_Breeze_Cell_04")
            {
                Enter = () => Hold(page, CellStage.AloftFlow),
                Highlight = "return",
            },
            new("Narr_Breeze_Cell_05")
            {
                Enter = () => Hold(page, CellStage.SurfacePressure),
                Highlight = "low",
            },
            new("Narr_Breeze_Cell_06")
            {
                Enter = () => Hold(page, CellStage.SurfaceWind),
                Highlight = "seabreeze",
            },
            new("Narr_Breeze_Cell_07")
            {
                Enter = () => Hold(page, CellStage.Circulation),
                Highlight = "rising",
            },
            new("Narr_Breeze_Cell_08")
            {
                Enter = () =>
                {
                    CellAir(page, CellTime.Night);
                    Hold(page, CellStage.Circulation);
                },
                Highlight = "landbreeze",
            },
            new("Narr_Breeze_Cell_09")
            {
                Enter = () =>
                {
                    CellAir(page, CellTime.Day);
                    Hold(page, CellStage.Circulation);
                    airAtTask = (page.LandTemperature, page.SeaTemperature);
                },
                TryIt = () => Math.Abs(page.LandTemperature - airAtTask.Land) >= 2 ||
                              Math.Abs(page.SeaTemperature - airAtTask.Sea) >= 2,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Monsoon(SeaBreezeViewModel page) =>
    [
        new("Narr_Breeze_Monsoon_01")
        {
            Enter = () =>
            {
                page.Mode = BreezeMode.Monsoon;
                ShowAll(page, pressure: true);
                page.JumpToMonthCommand.Execute("1");
            },
            Highlight = BreezeDiagram.SystemId("Siberian"),
        },
        new("Narr_Breeze_Monsoon_02") { Highlight = BreezeDiagram.MonsoonId("NW") },
        new("Narr_Breeze_Monsoon_03")
        {
            Choices = ["Breeze_Lesson_Monsoon_Choice1", "Breeze_Lesson_Monsoon_Choice2", "Breeze_Lesson_Monsoon_Choice3"],
            Answer = 0,
        },
        new("Narr_Breeze_Monsoon_04")
        {
            Enter = () => page.JumpToMonthCommand.Execute("7"),
            Highlight = BreezeDiagram.SystemId("AsianLow"),
        },
        new("Narr_Breeze_Monsoon_05") { Highlight = BreezeDiagram.MonsoonId("SW") },
        new("Narr_Breeze_Monsoon_06") { Highlight = "trough" },
        new("Narr_Breeze_Monsoon_07")
        {
            Enter = () => page.City = MonsoonCity.Mumbai,
            Highlight = BreezeDiagram.CityId(MonsoonCity.Mumbai),
        },
        new("Narr_Breeze_Monsoon_08")
        {
            Enter = () =>
            {
                page.JumpToMonthCommand.Execute("1");
                page.City = MonsoonCity.Darwin;
            },
            Highlight = BreezeDiagram.CityId(MonsoonCity.Darwin),
        },
        new("Narr_Breeze_Monsoon_09")
        {
            // The task: click a city on the map.
            Enter = () => page.Card.Id = null,
            TryIt = () => page.Card.Id?.StartsWith("c-", StringComparison.Ordinal) == true,
        },
    ];
}
