using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Book.Shell.Controls;
using Lemon.Book.Shell.Explore.Lessons;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Explore.Book.Chapters.StatesOfMatter;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class StatesOfMatterChapterTests
{
    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in MatterDiagram.TargetIds)
            {
                Assert.Contains(MatterDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(MatterDiagram.ObjectClipKey(id)!, keys);
            }

            foreach (var substance in Enum.GetValues<MatterSubstance>())
            {
                foreach (var id in MatterDiagram.Landmarks(substance)) Assert.Contains(id, MatterDiagram.TargetIds);
                foreach (var state in Enum.GetValues<MatterState>())
                {
                    Assert.Contains(MatterViewModel.StateNameKey(substance, state), keys);
                    Assert.Contains($"Matter_State_{state}", keys);
                    Assert.Contains($"Matter_State_{state}_Text", keys);
                    Assert.Contains($"Matter_Motion_{state}", keys);
                }

                foreach (var kind in new[] { "WarmSolid", "Melt", "WarmLiquid", "Boil", "Sublime", "WarmGas" })
                {
                    Assert.Contains($"Matter_Stretch_{kind}_{substance}", keys);
                    Assert.Contains($"Matter_Stretch_{kind}_Title", keys);
                    Assert.Contains($"Matter_Short_{kind}", keys);
                }
            }

            foreach (var place in Enum.GetValues<HeatingPlace>()) Assert.Contains($"Matter_Place_{place}", keys);
            foreach (var heat in Enum.GetValues<HeatSetting>()) Assert.Contains($"Matter_Heat_{heat}", keys);
        }
    }

    [AvaloniaTheory]
    [InlineData(MatterMode.Particles)]
    [InlineData(MatterMode.Heating)]
    [InlineData(MatterMode.Diagram)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(MatterMode mode)
    {
        var narration = new FakeNarration();
        using var page = new MatterViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([MatterLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): the water frozen, the ice melted, the dot moved into the gas.
        RunToEnd(page.Lesson, narration, new HashSet<string>(MatterDiagram.TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            switch (mode)
            {
                case MatterMode.Particles:
                    page.Celsius = -5;
                    break;

                case MatterMode.Heating:
                    page.HeatKj = page.KjInto("WarmLiquid", 0.1);
                    break;

                default:
                    page.Celsius = 150;
                    break;
            }
        });
    }

    [AvaloniaFact]
    public void Steps_ShowWhatTheySay()
    {
        var narration = new FakeNarration();
        using var page = new MatterViewModel(narration);
        var seen = new Dictionary<string, (MatterState State, string Stretch, double Celsius, MatterSubstance Substance)>();
        foreach (var mode in Enum.GetValues<MatterMode>())
        {
            page.Mode = mode;
            page.Lesson.GapSeconds = 0;
            page.Lesson.Start();
            for (var guard = 0; guard < 100 && page.Lesson.State != LessonState.Done; guard++)
            {
                if (narration.CurrentKey is { } key)
                {
                    seen[key] = (page.State, page.Stretch.Kind, mode == MatterMode.Heating ? page.HeatCelsius : page.Celsius, page.Substance);
                }

                if (page.Lesson.State == LessonState.Quiz) page.Lesson.AnswerCommand.Execute(page.Lesson.Choices[0]);
                else if (page.Lesson.State == LessonState.TryIt) page.Lesson.Stop();
                narration.Finish();
                page.Lesson.Tick(0.1);
            }
        }

        // The cylinder: ice, water, steam, water again when squeezed.
        Assert.Equal(MatterState.Solid, seen["Narr_Matter_Particles_01"].State);
        Assert.Equal(MatterState.Liquid, seen["Narr_Matter_Particles_03"].State);
        Assert.Equal(MatterState.Liquid, seen["Narr_Matter_Particles_04"].State);
        Assert.Equal(MatterState.Gas, seen["Narr_Matter_Particles_05"].State);
        Assert.Equal(MatterState.Liquid, seen["Narr_Matter_Particles_06"].State);

        // The heating: melting at 0, boiling at 100, about 70 on Everest, 120 in a cooker, dry ice at −78.5.
        Assert.Equal("Melt", seen["Narr_Matter_Heating_04"].Stretch);
        Assert.Equal(0, seen["Narr_Matter_Heating_04"].Celsius, 1);
        Assert.Equal("Boil", seen["Narr_Matter_Heating_06"].Stretch);
        Assert.Equal(100, seen["Narr_Matter_Heating_06"].Celsius, 0);
        Assert.InRange(seen["Narr_Matter_Heating_07"].Celsius, 69.5, 71.5);
        Assert.Equal(120, seen["Narr_Matter_Heating_08"].Celsius, 0);
        Assert.Equal("Sublime", seen["Narr_Matter_Heating_09"].Stretch);
        Assert.Equal(MatterSubstance.CarbonDioxide, seen["Narr_Matter_Heating_09"].Substance);

        // The map: water at 25 °C, Everest on the boiling line, supercritical past the critical point, dry ice solid.
        Assert.Equal(MatterState.Liquid, seen["Narr_Matter_Diagram_02"].State);
        Assert.Equal(MatterState.Solid, seen["Narr_Matter_Diagram_07"].State);
        Assert.Equal(MatterState.Supercritical, seen["Narr_Matter_Diagram_08"].State);
        Assert.Equal(MatterState.Solid, seen["Narr_Matter_Diagram_09"].State);
        Assert.Equal(MatterSubstance.CarbonDioxide, seen["Narr_Matter_Diagram_09"].Substance);
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
            using var page = new MatterViewModel(new FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Matter_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Matter_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Matter_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Matter_", page.Subtitle + page.ModeNote + page.Hint + page.ChartCaption + page.ChartNote
                    + page.CelsiusText + page.PressureText + page.HeatKjText, StringComparison.Ordinal);
            }

            foreach (var substance in Enum.GetValues<MatterSubstance>())
            {
                page.Substance = substance;
                page.Mode = MatterMode.Particles;
                var states = new HashSet<MatterState>();
                for (var c = page.MinCelsius; c <= page.MaxCelsius; c += 15)
                {
                    foreach (var log in new[] { 0.5, 2.0, 5.0, 6.5, 7.5 })
                    {
                        page.SetState(c, Math.Pow(10, log));
                        Check();
                        states.Add(page.State);
                    }
                }

                Assert.Equal(Enum.GetValues<MatterState>().Length, states.Count);

                page.Mode = MatterMode.Diagram;
                Check();

                page.Mode = MatterMode.Heating;
                foreach (var place in Enum.GetValues<HeatingPlace>())
                {
                    page.Place = place;
                    for (var kj = 0.0; kj <= page.TotalKj; kj += page.TotalKj / 40)
                    {
                        page.HoldAt(kj);
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

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("States-of-Matter");
            var matter = ((StatesOfMatterTextPageViewModel)model.Pages[TextPageOf("States-of-Matter")]).Matter;
            matter.SetState(-20, 101_325);
            Frames(30);
            Snapshot(window, "matter-particles-ice-zh");
            matter.SetState(140, 101_325);
            Frames(60);
            Snapshot(window, "matter-particles-steam-zh");

            matter.Mode = MatterMode.Heating;
            matter.HoldAt(matter.KjInto("Boil", 0.3));
            Frames(30);
            Snapshot(window, "matter-heating-zh");

            matter.Mode = MatterMode.Diagram;
            matter.SetState(25, 101_325);
            Frames(20);
            Snapshot(window, "matter-diagram-zh");

            matter.Substance = MatterSubstance.CarbonDioxide;
            Frames(20);
            Snapshot(window, "matter-diagram-co2-zh");

            matter.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(matter.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    [AvaloniaFact]
    public void TheBurner_RunsTheHeatUp_AndGoesOutAtTheEnd()
    {
        var (window, model) = OpenAt("States-of-Matter");
        var matter = ((StatesOfMatterTextPageViewModel)model.Pages[TextPageOf("States-of-Matter")]).Matter;
        matter.Mode = MatterMode.Heating;
        matter.Substance = MatterSubstance.CarbonDioxide;
        matter.RunFrom(matter.TotalKj - 0.2, HeatSetting.High);
        Frames(30);
        Assert.Equal(matter.TotalKj, matter.HeatKj, 6);
        Assert.Equal(HeatSetting.Off, matter.Heat);
        window.Close();
    }
}
