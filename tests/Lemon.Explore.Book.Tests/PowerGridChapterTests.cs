using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.PowerGrid;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class PowerGridChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "north", "south", "field", "coil", "rings", "commutator", "brushes", "lamp", "lamps", "scope", "crank", "pushback",
        "rotor", "stator",
        "station", "stepup", "line", "pylon", "heat", "substation", "pole", "town", "core", "primary", "secondary",
        "supply", "nuclear", "coal", "gas", "hydro", "pumped", "wind", "solar", "import", "bus", "dial", "flywheel", "city",
        "trace",
    ];

    /// <summary>Every state each view's card can name (see RefreshGenerator, RefreshTransmission and RefreshBalance).</summary>
    private static readonly (string View, string[] States)[] CardStates =
    [
        ("Gen", ["Facing", "Along", "Between"]),
        ("Line", ["Good", "High", "Impossible", "Dc"]),
        ("Bal", ["Night", "Day", "Sunny", "Peak", "Falling", "Caught", "Recovering"]),
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(GridDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(GridDiagram.ObjectClipKey(id)!, keys);
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
                    Assert.Contains($"Grid_{view}_{state}_Title", keys);
                    Assert.Contains($"Grid_{view}_{state}_Description", keys);
                }
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(GridMode.Generator)]
    [InlineData(GridMode.Transmission)]
    [InlineData(GridMode.Balance)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(GridMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new PowerGridViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([PowerGridLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): the coil sped up, the line's voltage changed, an event set off.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.CoilSpeed += 0.5;
            page.LineKv = page.LineKv > 200 ? page.LineKv / 2 : page.LineKv * 2;
            page.TriggerEventCommand.Execute(nameof(GridEvent.Trip));
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
            using var page = new PowerGridViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Grid_", page.PhaseTitle + page.PhaseDescription + page.ClockText, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Grid_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Grid_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Grid_", page.Subtitle + page.ScaleNote + page.Hint, StringComparison.Ordinal);
            }

            page.Mode = GridMode.Generator;
            foreach (var kind in Enum.GetValues<GeneratorKind>())
            {
                page.Kind = kind;
                foreach (var degrees in new[] { "0", "45", "90" })
                {
                    page.HoldAngleCommand.Execute(degrees);
                    Check();
                }
            }

            page.Mode = GridMode.Transmission;
            foreach (var kv in new[] { 0.4, 10, 220, 500, 1000 })
            {
                page.LineKv = kv;
                Check();
            }

            page.FeedDc = true;
            Check();

            page.Mode = GridMode.Balance;
            foreach (var hour in new[] { "4", "9", "12", "20" })
            {
                page.JumpToHourCommand.Execute(hour);
                Check();
            }

            foreach (var kind in new[] { GridEvent.Trip, GridEvent.Link, GridEvent.Surge })
            {
                page.TriggerEventCommand.Execute(kind.ToString());
                Check();
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
            var (window, model) = OpenAt("Power-Grid");
            var grid = ((PowerGridTextPageViewModel)model.Pages[TextPageOf("Power-Grid")]).Grid;
            grid.IsPlaying = true;
            Frames(30);
            Snapshot(window, "grid-generator-zh");

            grid.Mode = GridMode.Transmission;
            Frames(20);
            Snapshot(window, "grid-transmission-zh");

            grid.Mode = GridMode.Balance;
            grid.TriggerEventCommand.Execute(nameof(GridEvent.Trip));
            Frames(30);
            grid.IsPlaying = false;
            Snapshot(window, "grid-balance-zh");

            grid.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(grid.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
