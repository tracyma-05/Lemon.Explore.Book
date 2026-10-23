using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>
/// The generator-and-grid page's spoken walkthroughs, one per view. Each holds the coil at the angle it talks about, picks
/// the voltage it compares, or sets the hour and starts the event it describes, so the picture matches the words.
/// Highlight ids are <see cref="GridDiagram"/>'s.
/// </summary>
internal static class PowerGridLessons
{
    public static IReadOnlyList<LessonStep> For(PowerGridViewModel page) => page.Mode switch
    {
        GridMode.Transmission => Transmission(page),
        GridMode.Balance => Balance(page),
        _ => Generator(page),
    };

    private static void Labels(PowerGridViewModel page)
    {
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
    }

    /// <summary>The classroom generator as the walkthrough starts it: slip rings, the lamp on, the usual coil and magnet.</summary>
    private static void Classroom(PowerGridViewModel page)
    {
        page.Kind = GeneratorKind.Ac;
        page.LampOn = true;
        page.Turns = GeneratorModel.DefaultTurns;
        page.Field = GeneratorModel.DefaultField;
        page.CoilSpeed = GeneratorModel.DefaultSpeed;
    }

    /// <summary>The event the step talks about, started if it is not already running, moved on to at least <paramref name="time"/> s.</summary>
    private static void HoldEvent(PowerGridViewModel page, GridEvent kind, double time)
    {
        if (page.EventKind != kind) page.TriggerEventCommand.Execute(kind.ToString());
        page.EventTime = Math.Max(page.EventTime, time);
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Generator(PowerGridViewModel page)
    {
        // The task: turn it faster or slower, change the coil or the magnet, or switch to another machine.
        var atTask = (Speed: 0.0, Turns: 0, Field: 0.0, Kind: GeneratorKind.Ac);
        return
        [
            new("Narr_Grid_Generator_01")
            {
                Enter = () =>
                {
                    page.Mode = GridMode.Generator;
                    Labels(page);
                    Classroom(page);
                    page.IsPlaying = true;
                },
                Highlight = "coil",
            },
            new("Narr_Grid_Generator_02") { Highlight = "field" },
            new("Narr_Grid_Generator_03")
            {
                Enter = () => page.HoldAngleCommand.Execute("0"),
                Highlight = "coil",
            },
            new("Narr_Grid_Generator_04")
            {
                Enter = () => page.HoldAngleCommand.Execute("90"),
                Highlight = "scope",
            },
            new("Narr_Grid_Generator_05")
            {
                Choices = ["Grid_Lesson_Generator_Choice1", "Grid_Lesson_Generator_Choice2", "Grid_Lesson_Generator_Choice3"],
                Answer = 0,
            },
            new("Narr_Grid_Generator_06")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "rings",
            },
            new("Narr_Grid_Generator_07")
            {
                Enter = () => page.Kind = GeneratorKind.Dc,
                Highlight = "commutator",
            },
            new("Narr_Grid_Generator_08")
            {
                Enter = () =>
                {
                    page.Kind = GeneratorKind.Ac;
                    page.LampOn = true;
                    page.HoldAngleCommand.Execute("90");
                },
                Highlight = "pushback",
            },
            new("Narr_Grid_Generator_09")
            {
                Enter = () =>
                {
                    page.Kind = GeneratorKind.ThreePhase;
                    page.PolePairs = 1;
                    page.Turns = 500;
                    page.CoilSpeed = 0.5;
                    page.IsPlaying = true;
                },
                Highlight = "rotor",
            },
            new("Narr_Grid_Generator_10") { Highlight = "scope" },
            new("Narr_Grid_Generator_11")
            {
                Enter = () =>
                {
                    Classroom(page);
                    page.IsPlaying = true;
                    atTask = (page.CoilSpeed, page.Turns, page.Field, page.Kind);
                },
                TryIt = () => Math.Abs(page.CoilSpeed - atTask.Speed) >= 0.2 || page.Turns != atTask.Turns ||
                              Math.Abs(page.Field - atTask.Field) > 0.01 || page.Kind != atTask.Kind,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Transmission(PowerGridViewModel page)
    {
        // The task: another voltage, or a longer or shorter line.
        var atTask = (Kv: 0.0, Distance: 0.0, Power: 0.0);
        return
        [
            new("Narr_Grid_Transmission_01")
            {
                Enter = () =>
                {
                    page.Mode = GridMode.Transmission;
                    Labels(page);
                    page.FeedDc = false;
                    page.LineKv = 500;
                    page.Power = 1000;
                    page.Distance = 300;
                    page.IsPlaying = true;
                },
                Highlight = "station",
            },
            new("Narr_Grid_Transmission_02") { Highlight = "line" },
            new("Narr_Grid_Transmission_03") { Highlight = "stepup" },
            new("Narr_Grid_Transmission_04")
            {
                Enter = () => page.LineKv = 110,
                Highlight = "heat",
            },
            new("Narr_Grid_Transmission_05")
            {
                Choices = ["Grid_Lesson_Transmission_Choice1", "Grid_Lesson_Transmission_Choice2", "Grid_Lesson_Transmission_Choice3"],
                Answer = 0,
            },
            new("Narr_Grid_Transmission_06")
            {
                Enter = () => page.LineKv = 500,
                Highlight = "core",
            },
            new("Narr_Grid_Transmission_07") { Highlight = "secondary" },
            new("Narr_Grid_Transmission_08")
            {
                Enter = () => page.FeedDc = true,
                Highlight = "supply",
            },
            new("Narr_Grid_Transmission_09")
            {
                Enter = () => page.FeedDc = false,
                Highlight = "substation",
            },
            new("Narr_Grid_Transmission_10")
            {
                Enter = () => atTask = (page.LineKv, page.Distance, page.Power),
                TryIt = () => Math.Abs(page.LineKv - atTask.Kv) > 0.5 || Math.Abs(page.Distance - atTask.Distance) >= 50 ||
                              Math.Abs(page.Power - atTask.Power) >= 100,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Balance(PowerGridViewModel page)
    {
        // The task: start an event (at any hour).
        var events = 0;
        return
        [
            new("Narr_Grid_Balance_01")
            {
                Enter = () =>
                {
                    page.Mode = GridMode.Balance;
                    Labels(page);
                    page.Season = GridSeason.Summer;
                    page.SolarCapacity = GridDayModel.DefaultSolar;
                    page.Hour = 7;
                    page.IsPlaying = true;
                },
                Highlight = "city",
            },
            new("Narr_Grid_Balance_02")
            {
                Enter = () =>
                {
                    page.Hour = 20;
                    page.IsPlaying = false;
                },
                Highlight = "coal",
            },
            new("Narr_Grid_Balance_03")
            {
                Enter = () =>
                {
                    page.SolarCapacity = 24;
                    page.Hour = 12;
                    page.IsPlaying = false;
                },
                Highlight = "pumped",
            },
            new("Narr_Grid_Balance_04")
            {
                Enter = () =>
                {
                    page.SolarCapacity = GridDayModel.DefaultSolar;
                    page.Hour = 19;
                    page.IsPlaying = false;
                },
                Highlight = "dial",
            },
            new("Narr_Grid_Balance_05")
            {
                Enter = () => HoldEvent(page, GridEvent.Trip, 0),
                Highlight = "flywheel",
            },
            new("Narr_Grid_Balance_06")
            {
                Enter = () => HoldEvent(page, GridEvent.Trip, 12),
                Highlight = "hydro",
            },
            new("Narr_Grid_Balance_07")
            {
                Enter = () => HoldEvent(page, GridEvent.Trip, 60),
                Highlight = "trace",
            },
            new("Narr_Grid_Balance_08")
            {
                Choices = ["Grid_Lesson_Balance_Choice1", "Grid_Lesson_Balance_Choice2", "Grid_Lesson_Balance_Choice3"],
                Answer = 0,
            },
            new("Narr_Grid_Balance_09")
            {
                Enter = () =>
                {
                    page.SolarCapacity = GridDayModel.MaxSolar;
                    page.Hour = 12;
                    HoldEvent(page, GridEvent.Trip, 0);
                },
                Highlight = "flywheel",
            },
            new("Narr_Grid_Balance_10")
            {
                Enter = () =>
                {
                    page.SolarCapacity = GridDayModel.DefaultSolar;
                    events = page.EventCount;
                },
                TryIt = () => page.EventCount > events,
            },
        ];
    }
}
