using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>
/// The acids and bases page's spoken walkthroughs, one per view. Each sets the beaker, the liquid or the titration so the
/// picture matches the words (a question is asked before the picture shows its answer), and the last step hands over to
/// the viewer. Highlight ids are <see cref="AcidBaseDiagram"/>'s.
/// </summary>
internal static class AcidBaseLessons
{
    public static IReadOnlyList<LessonStep> For(AcidBaseViewModel page) => page.Mode switch
    {
        AcidBaseMode.Scale => Scale(page),
        AcidBaseMode.Neutralize => Neutralize(page),
        _ => Particles(page),
    };

    /// <summary>A beaker of fresh water with only <paramref name="drops"/> drops of <paramref name="reagent"/> in it.</summary>
    private static void Fresh(AcidBaseViewModel page, Reagent reagent, int drops)
    {
        page.EmptyBeaker();
        if (drops > 0) page.AddDrops(reagent, drops);
    }

    private static IReadOnlyList<LessonStep> Particles(AcidBaseViewModel page) =>
    [
        new("Narr_Acid_Particles_01")
        {
            Enter = () =>
            {
                page.Mode = AcidBaseMode.Particles;
                page.ShowLabels = true;
                page.ShowParticles = true;
                page.BeakerIndicator = Indicator.None;
                page.EmptyBeaker();
            },
            Highlight = "meter",
        },
        new("Narr_Acid_Particles_02") { Highlight = "magnifier" },

        // Two drops of hydrochloric acid: 1 × 10⁻⁴ mol/L, pH 4.
        new("Narr_Acid_Particles_03")
        {
            Enter = () => Fresh(page, Reagent.StrongAcid, 2),
            Highlight = "hcl",
        },
        new("Narr_Acid_Particles_04") { Highlight = "p_hydronium" },
        new("Narr_Acid_Particles_05")
        {
            Choices = ["Acid_Lesson_Particles_Choice1", "Acid_Lesson_Particles_Choice2", "Acid_Lesson_Particles_Choice3"],
            Answer = 0,
        },

        // Twenty drops: ten times as much acid, pH 3.
        new("Narr_Acid_Particles_06")
        {
            Enter = () => Fresh(page, Reagent.StrongAcid, 20),
            Highlight = "phvalue",
        },

        // The same twenty drops of acetic acid: most of it stays as whole molecules, pH about 3.9.
        new("Narr_Acid_Particles_07")
        {
            Enter = () => Fresh(page, Reagent.WeakAcid, 20),
            Highlight = "p_acidmolecule",
        },

        // Twenty drops of sodium hydroxide: pH 11, far more OH⁻ than H₃O⁺.
        new("Narr_Acid_Particles_08")
        {
            Enter = () => Fresh(page, Reagent.StrongBase, 20),
            Highlight = "p_hydroxide",
        },

        // Then twenty drops of the acid on top: neutral salt water.
        new("Narr_Acid_Particles_09")
        {
            Enter = () =>
            {
                Fresh(page, Reagent.StrongBase, 20);
                page.AddDrops(Reagent.StrongAcid, 20);
            },
            Highlight = "p_sodium",
        },
        new("Narr_Acid_Particles_10")
        {
            Enter = () =>
            {
                page.BeakerIndicator = Indicator.Universal;
                Fresh(page, Reagent.WeakBase, 20);
            },
            Highlight = "beaker",
        },
        new("Narr_Acid_Particles_11")
        {
            Highlight = "hcl",
            TryIt = () => page.BeakerPh < 5,
        },
    ];

    private static IReadOnlyList<LessonStep> Scale(AcidBaseViewModel page)
    {
        // The task: another liquid picked.
        var atTask = string.Empty;
        return
        [
            new("Narr_Acid_Scale_01")
            {
                Enter = () =>
                {
                    page.Mode = AcidBaseMode.Scale;
                    page.ShowLabels = true;
                    page.ScaleIndicator = Indicator.Universal;
                    page.SubstanceId = "Water";
                },
                Highlight = "scale",
            },
            new("Narr_Acid_Scale_02") { Highlight = "steps" },
            new("Narr_Acid_Scale_03")
            {
                Enter = () => page.SubstanceId = "Lemon",
                Highlight = "sub_Lemon",
            },
            new("Narr_Acid_Scale_04")
            {
                Enter = () => page.SubstanceId = "Cola",
                Choices = ["Acid_Lesson_Scale_Choice1", "Acid_Lesson_Scale_Choice2", "Acid_Lesson_Scale_Choice3"],
                Answer = 0,
            },
            new("Narr_Acid_Scale_05")
            {
                Enter = () => page.SubstanceId = "Coffee",
                Highlight = "sub_Coffee",
            },
            new("Narr_Acid_Scale_06")
            {
                Enter = () => page.SubstanceId = "Bleach",
                Highlight = "sub_Bleach",
            },
            new("Narr_Acid_Scale_07")
            {
                Enter = () =>
                {
                    page.SubstanceId = "Vinegar";
                    page.ScaleIndicator = Indicator.Cabbage;
                },
                Highlight = "ind_cabbage",
            },
            new("Narr_Acid_Scale_08")
            {
                Enter = () =>
                {
                    page.SubstanceId = "Soap";
                    page.ScaleIndicator = Indicator.Phenolphthalein;
                },
                Highlight = "ind_phenol",
            },
            new("Narr_Acid_Scale_09")
            {
                Enter = () =>
                {
                    page.ScaleIndicator = Indicator.Universal;
                    page.SubstanceId = "Stomach";
                },
                Highlight = "dilute",
                Choices = ["Acid_Lesson_Scale2_Choice1", "Acid_Lesson_Scale2_Choice2", "Acid_Lesson_Scale2_Choice3"],
                Answer = 0,
            },
            new("Narr_Acid_Scale_10")
            {
                Enter = () => page.Dilutions = 1,
                Highlight = "tube",
            },
            new("Narr_Acid_Scale_11")
            {
                Enter = () => page.Dilutions = AcidBaseDiagram.MaxDilutions,
                Highlight = "neutral",
            },
            new("Narr_Acid_Scale_12")
            {
                Enter = () => atTask = page.SubstanceId,
                Highlight = "scale",
                TryIt = () => page.SubstanceId != atTask,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Neutralize(AcidBaseViewModel page) =>
    [
        new("Narr_Acid_Neutralize_01")
        {
            Enter = () =>
            {
                page.Mode = AcidBaseMode.Neutralize;
                page.ShowLabels = true;
                page.ShowParticles = true;
                page.ShowCompare = false;
                page.Acid = TitrationAcid.Strong;
                page.FlaskIndicator = Indicator.Phenolphthalein;
                page.Molar = AcidBaseModel.BottleMolar;
                page.HoldAt(0);
            },
            Highlight = "flask",
        },
        new("Narr_Acid_Neutralize_02") { Highlight = "burette" },

        // The tap opened a drop at a time from 8 mL: the curve creeps up.
        new("Narr_Acid_Neutralize_03")
        {
            Enter = () => page.RunFrom(8, TapState.Drip),
            Highlight = "curve",
        },
        new("Narr_Acid_Neutralize_04")
        {
            Enter = () => page.HoldAt(15),
            Highlight = "reaction",
        },
        new("Narr_Acid_Neutralize_05")
        {
            Enter = () => page.HoldAt(24),
            Choices = ["Acid_Lesson_Neutralize_Choice1", "Acid_Lesson_Neutralize_Choice2", "Acid_Lesson_Neutralize_Choice3"],
            Answer = 0,
        },
        new("Narr_Acid_Neutralize_06")
        {
            Enter = () => page.RunFrom(24, TapState.Drip),
            Highlight = "jump",
        },
        new("Narr_Acid_Neutralize_07")
        {
            Enter = () => page.HoldAt(25),
            Highlight = "equivalence",
        },
        new("Narr_Acid_Neutralize_08")
        {
            Enter = () => page.HoldAt(25),
            Highlight = "thermometer",
        },

        // Vinegar's acid: a curve that starts higher, levels off halfway and jumps less.
        new("Narr_Acid_Neutralize_09")
        {
            Enter = () =>
            {
                page.Acid = TitrationAcid.Weak;
                page.ShowCompare = true;
                page.HoldAt(32);
            },
            Highlight = "halfway",
        },
        new("Narr_Acid_Neutralize_10")
        {
            Enter = () => page.FlaskIndicator = Indicator.MethylOrange,
            Highlight = "band",
        },
        new("Narr_Acid_Neutralize_11")
        {
            Enter = () =>
            {
                page.Acid = TitrationAcid.Strong;
                page.ShowCompare = false;
                page.FlaskIndicator = Indicator.Phenolphthalein;
                page.Refill();
            },
            Highlight = "tap",
            TryIt = () => page.BaseMl >= AcidBaseModel.EquivalenceMl - 0.1 && page.Tap == TapState.Closed,
        },
    ];
}
