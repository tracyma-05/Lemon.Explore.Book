using Lemon.Book.Shell.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The states of matter page's spoken walkthroughs, one per view. Each sets the temperature, pressure or heating so the
/// picture matches the words (a question is asked before the picture shows its answer), and the last step hands over to
/// the viewer. Highlight ids are <see cref="MatterDiagram"/>'s.
/// </summary>
internal static class MatterLessons
{
    public static IReadOnlyList<LessonStep> For(MatterViewModel page) => page.Mode switch
    {
        MatterMode.Heating => Heating(page),
        MatterMode.Diagram => Diagram(page),
        _ => Particles(page),
    };

    private static IReadOnlyList<LessonStep> Particles(MatterViewModel page) =>
    [
        // Ice at −20 °C and one atmosphere.
        new("Narr_Matter_Particles_01")
        {
            Enter = () =>
            {
                page.Mode = MatterMode.Particles;
                page.Substance = MatterSubstance.Water;
                page.ShowLabels = true;
                page.ShowBonds = true;
                page.SetState(-20, MatterModel.Atmosphere);
            },
            Highlight = "cylinder",
        },
        new("Narr_Matter_Particles_02") { Highlight = "molecule" },

        // Warmed past 0 °C: the lattice falls apart into a liquid.
        new("Narr_Matter_Particles_03")
        {
            Enter = () => page.SetState(25, MatterModel.Atmosphere),
            Highlight = "thermometer",
        },
        new("Narr_Matter_Particles_04")
        {
            Enter = () => page.SetState(95, MatterModel.Atmosphere),
            Choices = ["Matter_Lesson_Particles_Choice1", "Matter_Lesson_Particles_Choice2", "Matter_Lesson_Particles_Choice3"],
            Answer = 0,
        },

        // 120 °C: a gas, which pushes the piston up.
        new("Narr_Matter_Particles_05")
        {
            Enter = () => page.SetState(120, MatterModel.Atmosphere),
            Highlight = "piston",
        },

        // Still 120 °C, squeezed to 20 atmospheres: liquid again.
        new("Narr_Matter_Particles_06")
        {
            Enter = () => page.SetState(120, 2e6),
            Highlight = "gauge",
        },
        new("Narr_Matter_Particles_07")
        {
            Enter = () => page.SetState(120, MatterModel.Atmosphere),
            Highlight = "state",
        },
        new("Narr_Matter_Particles_08")
        {
            Enter = () => page.SetState(40, MatterModel.Atmosphere),
            Highlight = "thermometer",
            TryIt = () => page.State == MatterState.Solid,
        },
    ];

    private static IReadOnlyList<LessonStep> Heating(MatterViewModel page) =>
    [
        new("Narr_Matter_Heating_01")
        {
            Enter = () =>
            {
                page.Mode = MatterMode.Heating;
                page.Substance = MatterSubstance.Water;
                page.Place = HeatingPlace.SeaLevel;
                page.ShowLabels = true;
                page.Restart();
            },
            Highlight = "beaker",
        },

        // The burner lit: the ice warms, the curve climbs.
        new("Narr_Matter_Heating_02")
        {
            Enter = () => page.RunFrom(0, HeatSetting.High),
            Highlight = "curve",
        },
        new("Narr_Matter_Heating_03")
        {
            Enter = () => page.RunFrom(page.KjInto("Melt", 0.05), HeatSetting.Low),
            Choices = ["Matter_Lesson_Heating_Choice1", "Matter_Lesson_Heating_Choice2", "Matter_Lesson_Heating_Choice3"],
            Answer = 0,
        },
        new("Narr_Matter_Heating_04")
        {
            Enter = () => page.HoldAt(page.KjInto("Melt", 0.6)),
            Highlight = "melt",
        },
        new("Narr_Matter_Heating_05")
        {
            Enter = () => page.HoldAt(page.KjInto("WarmLiquid", 0.6)),
            Highlight = "window",
        },
        new("Narr_Matter_Heating_06")
        {
            Enter = () => page.RunFrom(page.KjInto("Boil", 0.15), HeatSetting.High),
            Highlight = "boil",
        },

        // Everest, then a pressure cooker: the same boiling, at other temperatures.
        new("Narr_Matter_Heating_07")
        {
            Enter = () =>
            {
                page.Place = HeatingPlace.Everest;
                page.HoldAt(page.KjInto("Boil", 0.2));
            },
            Highlight = "place",
        },
        new("Narr_Matter_Heating_08")
        {
            Enter = () =>
            {
                page.Place = HeatingPlace.Cooker;
                page.HoldAt(page.KjInto("Boil", 0.2));
            },
            Highlight = "boil",
        },

        // Dry ice at ordinary pressure: no liquid at all.
        new("Narr_Matter_Heating_09")
        {
            Enter = () =>
            {
                page.Substance = MatterSubstance.CarbonDioxide;
                page.Place = HeatingPlace.SeaLevel;
                page.HoldAt(page.KjInto("Sublime", 0.3));
            },
            Highlight = "sublime",
        },
        new("Narr_Matter_Heating_10")
        {
            Enter = () =>
            {
                page.Substance = MatterSubstance.Water;
                page.Place = HeatingPlace.SeaLevel;
                page.Restart();
            },
            Highlight = "flame",
            TryIt = () => page.Substance == MatterSubstance.Water && page.HeatKj >= page.KjInto("Melt", 1),
        },
    ];

    private static IReadOnlyList<LessonStep> Diagram(MatterViewModel page)
    {
        // The task: the dot dragged into the gas region from where it was left.
        return
        [
            new("Narr_Matter_Diagram_01")
            {
                Enter = () =>
                {
                    page.Mode = MatterMode.Diagram;
                    page.Substance = MatterSubstance.Water;
                    page.ShowLabels = true;
                    page.SetState(25, MatterModel.Atmosphere);
                },
                Highlight = "plot",
            },
            new("Narr_Matter_Diagram_02") { Highlight = "liquid" },
            new("Narr_Matter_Diagram_03") { Highlight = "atm" },
            new("Narr_Matter_Diagram_04") { Highlight = "boilline" },
            new("Narr_Matter_Diagram_05")
            {
                Enter = () => page.SetState(MatterLandmarks.EverestBoilingC, MatterModel.PressureAt(HeatingPlace.Everest)),
                Highlight = "lm_everest",
            },
            new("Narr_Matter_Diagram_06")
            {
                Enter = () => page.SetState(0.01, MatterModel.Water.TripleP),
                Highlight = "triple",
            },
            new("Narr_Matter_Diagram_07")
            {
                Enter = () => page.SetState(-10, MatterModel.Atmosphere),
                Highlight = "meltline",
                Choices = ["Matter_Lesson_Diagram_Choice1", "Matter_Lesson_Diagram_Choice2", "Matter_Lesson_Diagram_Choice3"],
                Answer = 0,
            },
            new("Narr_Matter_Diagram_08")
            {
                Enter = () => page.SetState(400, 3e7),
                Highlight = "critical",
            },

            // Carbon dioxide: its triple point is above one atmosphere.
            new("Narr_Matter_Diagram_09")
            {
                Enter = () =>
                {
                    page.Substance = MatterSubstance.CarbonDioxide;
                    page.SetState(-80, MatterModel.Atmosphere);
                },
                Highlight = "lm_dryice",
            },
            new("Narr_Matter_Diagram_10")
            {
                Enter = () =>
                {
                    page.Substance = MatterSubstance.Water;
                    page.SetState(25, MatterModel.Atmosphere);
                },
                Highlight = "dot",
                TryIt = () => page.Substance == MatterSubstance.Water && page.State == MatterState.Gas,
            },
        ];
    }
}

/// <summary>Numbers the walkthrough and the tests share.</summary>
internal static class MatterLandmarks
{
    /// <summary>Water's boiling point at the top of Everest, °C (about 70).</summary>
    public static double EverestBoilingC =>
        Math.Round(MatterModel.BoilingTemperature(MatterModel.Water, MatterModel.PressureAt(HeatingPlace.Everest)) - MatterModel.ZeroCelsius, 1);
}
