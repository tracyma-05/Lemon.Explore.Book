using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.HeatEngines;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class HeatEnginesChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "intakevalve", "exhaustvalve", "camshaft", "coolant", "rod", "crank", "sparkplug", "piston", "oil", "intake",
        "exhaust", "gas", "dial", "spark",
        "compressor", "condenser", "capillary", "evaporator", "insulation", "door", "heatout", "freezer", "fridge", "room",
        "hot", "cold", "machine", "hotflow", "coldflow", "work", "loop", "isotherm",
    ];

    /// <summary>Every state each view's card can name (see RefreshEngine and RefreshFridge).</summary>
    private static readonly (string View, string[] States)[] CardStates =
    [
        ("Engine", ["Intake", "Compression", "Burning", "Power", "Exhaust"]),
        ("Fridge", ["Running", "Hot", "DoorOpen"]),
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(HeatDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(HeatDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [Fact]
    public void EveryCardState_HasATitleAndADescription_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var (view, states) in CardStates)
            {
                foreach (var state in states)
                {
                    Assert.Contains($"Heat_{view}_{state}_Title", keys);
                    Assert.Contains($"Heat_{view}_{state}_Description", keys);
                }
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(HeatMode.Engine)]
    [InlineData(HeatMode.Fridge)]
    [InlineData(HeatMode.Cycle)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(HeatMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new HeatEnginesViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([HeatEnginesLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): the crank turned, the door opened, a temperature changed.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Angle = EngineModel.Wrap(page.Angle + 90);
            page.DoorOpen = !page.DoorOpen;
            page.Hot = page.Hot > 600 ? page.Hot - 50 : page.Hot + 50;
        });
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
            using var page = new HeatEnginesViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Heat_", page.PhaseTitle + page.PhaseDescription + page.ClockText, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Heat_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Heat_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Heat_", page.Subtitle + page.ScaleNote + page.Hint + page.AltitudeNote, StringComparison.Ordinal);
            }

            page.Mode = HeatMode.Engine;
            foreach (var stroke in new[] { "Intake", "Compression", "Spark", "Power", "Exhaust" })
            {
                page.JumpToStrokeCommand.Execute(stroke);
                Check();
            }

            page.Mode = HeatMode.Fridge;
            foreach (var room in new[] { "15", "25", "35" })
            {
                page.SetRoomCommand.Execute(room);
                Check();
            }

            page.DoorOpen = true;
            Check();

            page.Mode = HeatMode.Cycle;
            foreach (var preset in new[] { "PowerStation", "CarEngine", "Fridge", "HeatPump" })
            {
                page.UsePresetCommand.Execute(preset);
                foreach (var phase in new[] { 0.5, 1.5, 2.5, 3.5 })
                {
                    page.Phase = phase;
                    Check();
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
            var (window, model) = OpenAt("Heat-Engines");
            var heat = ((HeatEnginesTextPageViewModel)model.Pages[TextPageOf("Heat-Engines")]).Heat;
            heat.IsPlaying = true;
            Frames(30);
            Snapshot(window, "heat-engine-zh");

            heat.Mode = HeatMode.Fridge;
            Frames(20);
            Snapshot(window, "heat-fridge-zh");

            heat.Mode = HeatMode.Cycle;
            Frames(30);
            heat.IsPlaying = false;
            Snapshot(window, "heat-cycle-zh");

            heat.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(heat.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
