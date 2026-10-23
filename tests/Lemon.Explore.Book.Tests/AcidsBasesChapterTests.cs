using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.AcidsBases;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class AcidsBasesChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "beaker", "meter", "phvalue", "phstrip", "magnifier", "swap", "hcl", "acetic", "naoh", "ammonia", "jug",
        "p_water", "p_hydronium", "p_hydroxide", "p_chloride", "p_sodium", "p_acidmolecule", "p_acidion", "p_basemolecule",
        "p_baseion",
        "scale", "neutral", "steps", "tube", "dilute",
        "ind_universal", "ind_cabbage", "ind_litmus", "ind_phenol", "ind_methyl", "ind_bromothymol",
        "burette", "tap", "flask", "indicator", "thermometer", "reaction", "curve", "band", "equivalence", "jump", "halfway",
        .. AcidBaseModel.Substances.Select(s => "sub_" + s.Id),
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(AcidBaseDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(AcidBaseDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [Fact]
    public void EveryParticleKind_HasATarget()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var kind in Enum.GetValues<ParticleKind>())
            {
                Assert.Contains(AcidBaseDiagram.ParticleTargetId(kind), TargetIds);
                Assert.Contains(AcidBaseDiagram.ParticleNameKey(kind), keys);
            }
        }

        foreach (var reagent in Enum.GetValues<Reagent>()) Assert.Equal(reagent, AcidBaseDiagram.ReagentOf(AcidBaseDiagram.BottleId(reagent)));
        foreach (var dye in AcidBaseDiagram.Dyes) Assert.Equal(dye, AcidBaseDiagram.IndicatorOf(AcidBaseDiagram.IndicatorId(dye)));
    }

    [AvaloniaTheory]
    [InlineData(AcidBaseMode.Particles)]
    [InlineData(AcidBaseMode.Scale)]
    [InlineData(AcidBaseMode.Neutralize)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(AcidBaseMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new AcidBaseViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([AcidBaseLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): acid dropped in, another substance picked, the burette run to the end point.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            switch (mode)
            {
                case AcidBaseMode.Particles:
                    page.AddDrops(Reagent.StrongAcid, 60);
                    break;

                case AcidBaseMode.Scale:
                    page.SubstanceId = page.SubstanceId == "Soap" ? "Milk" : "Soap";
                    break;

                default:
                    page.HoldAt(AcidBaseModel.EquivalenceMl);
                    break;
            }
        });
    }

    [AvaloniaFact]
    public void ParticleSteps_ShowWhatTheySay()
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new AcidBaseViewModel(narration);
        var seen = new Dictionary<string, double>();
        page.Lesson.GapSeconds = 0;
        page.Lesson.Start();
        for (var guard = 0; guard < 100 && page.Lesson.State != LessonState.Done; guard++)
        {
            if (narration.CurrentKey is { } key) seen[key] = page.BeakerPh;
            if (page.Lesson.State == LessonState.Quiz) page.Lesson.AnswerCommand.Execute(page.Lesson.Choices[0]);
            else if (page.Lesson.State == LessonState.TryIt) page.AddDrops(Reagent.StrongAcid, 60);
            narration.Finish();
            page.Lesson.Tick(0.1);
        }

        // Pure water 7; two drops of HCl 4; twenty 3; twenty of acetic acid about 3.9; NaOH 11; then neutral; ammonia basic.
        Assert.Equal(7.0, seen["Narr_Acid_Particles_01"], 1);
        Assert.Equal(4.0, seen["Narr_Acid_Particles_03"], 1);
        Assert.Equal(3.0, seen["Narr_Acid_Particles_06"], 1);
        Assert.InRange(seen["Narr_Acid_Particles_07"], 3.8, 4.0);
        Assert.Equal(11.0, seen["Narr_Acid_Particles_08"], 1);
        Assert.Equal(7.0, seen["Narr_Acid_Particles_09"], 1);
        Assert.InRange(seen["Narr_Acid_Particles_10"], 9.5, 10.5);
    }

    [AvaloniaFact]
    public void ScaleAndTitrationSteps_ShowWhatTheySay()
    {
        using var page = new AcidBaseViewModel(new LessonPlayerTests.FakeNarration()) { Mode = AcidBaseMode.Scale };

        // Lemon about 50,000× water's H₃O⁺; cola ~300× coffee's; stomach acid once watered: 2.5; a million times: just under 7.
        page.SubstanceId = "Lemon";
        Assert.InRange(Math.Pow(10, 7 - page.ScalePh), 40_000, 60_000);
        Assert.InRange(Math.Pow(10, AcidBaseModel.Substance("Coffee").Ph - AcidBaseModel.Substance("Cola").Ph), 250, 350);
        page.SubstanceId = "Stomach";
        page.Dilutions = 1;
        Assert.Equal(2.5, page.ScalePh, 1);
        page.Dilutions = AcidBaseDiagram.MaxDilutions;
        Assert.InRange(page.ScalePh, 6.8, 7.0);
        page.SubstanceId = "Soap";
        Assert.Equal(0, page.Dilutions);
        Assert.Equal("Acid_Colour_Pink", AcidBaseViewModel.ColourKey(Indicator.Phenolphthalein, page.ScalePh));

        // Titration: 24 mL acidic near 3, 26 mL near 11, colourless then pink; under a degree warmer.
        Assert.InRange(AcidBaseModel.TitrationPh(TitrationAcid.Strong, 24), 2.5, 3.0);
        Assert.InRange(AcidBaseModel.TitrationPh(TitrationAcid.Strong, 26), 11.0, 11.5);
        Assert.Equal("Acid_Colour_Colourless", AcidBaseViewModel.ColourKey(Indicator.Phenolphthalein, AcidBaseModel.TitrationPh(TitrationAcid.Strong, 24.9)));
        Assert.Equal("Acid_Colour_Pink", AcidBaseViewModel.ColourKey(Indicator.Phenolphthalein, AcidBaseModel.TitrationPh(TitrationAcid.Strong, 25.1)));
        Assert.InRange(AcidBaseModel.TemperatureRise(TitrationAcid.Strong, 25), 0.5, 1.0);

        // Methyl orange has finished changing long before a weak acid's end.
        Assert.True(AcidBaseModel.TitrationPh(TitrationAcid.Weak, 15) > AcidBaseModel.ChangeRange(Indicator.MethylOrange)!.Value.High);
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    public void TheCard_HasItsWords_InEachView(string culture)
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo(culture));
        try
        {
            using var page = new AcidBaseViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Acid_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Acid_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Acid_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Acid_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote + page.BaseMlText, StringComparison.Ordinal);
            }

            page.Mode = AcidBaseMode.Particles;
            var states = new HashSet<string>();
            foreach (var (reagent, drops) in new[] { (Reagent.Water, 0), (Reagent.StrongAcid, 5), (Reagent.WeakBase, 5) })
            {
                page.EmptyBeaker();
                if (drops > 0) page.AddDrops(reagent, drops);
                Check();
                states.Add(page.BeakerStateName);
            }

            page.EmptyBeaker();
            page.AddDrops(Reagent.StrongBase, 20);
            page.AddDrops(Reagent.StrongAcid, 20);
            Check();
            states.Add(page.BeakerStateName);
            Assert.Equal(new HashSet<string> { "Water", "Acidic", "Basic", "Neutral" }, states);

            page.Mode = AcidBaseMode.Scale;
            foreach (var substance in AcidBaseModel.Substances)
            {
                page.SubstanceId = substance.Id;
                Check();
                page.Dilutions = 3;
                Check();
            }

            page.Mode = AcidBaseMode.Neutralize;
            foreach (var acid in Enum.GetValues<TitrationAcid>())
            {
                page.Acid = acid;
                foreach (var indicator in new[] { Indicator.Phenolphthalein, Indicator.Bromothymol, Indicator.MethylOrange, Indicator.Universal })
                {
                    page.FlaskIndicator = indicator;
                    for (var ml = 0.0; ml <= 50; ml += 2.5)
                    {
                        page.HoldAt(ml);
                        Check();
                    }
                }
            }
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    [AvaloniaTheory]
    [InlineData("en", 50_118, "50,000")]
    [InlineData("en", 3_162_278, "3.2 million")]
    [InlineData("zh-CN", 50_118, "5万")]
    [InlineData("zh-CN", 316, "320")]
    public void LargeFactors_ReadNaturally(string culture, double times, string expected)
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo(culture));
        try
        {
            Assert.Equal(expected, AcidBaseDiagram.TimesText(times));
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Acids-Bases");
            var acids = ((AcidsBasesTextPageViewModel)model.Pages[TextPageOf("Acids-Bases")]).Acids;
            acids.AddDrops(Reagent.StrongAcid, 5);
            Frames(30);
            Snapshot(window, "acids-particles-zh");

            acids.Mode = AcidBaseMode.Scale;
            acids.SubstanceId = "Lemon";
            Frames(20);
            Snapshot(window, "acids-scale-zh");

            acids.Mode = AcidBaseMode.Neutralize;
            acids.HoldAt(24);
            Frames(20);
            Snapshot(window, "acids-neutralize-zh");

            acids.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(acids.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
