using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.WaterCycle;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class WaterCycleChapterTests
{
    /// <summary>Every target id the diagram registers.</summary>
    private static readonly string[] TargetIds =
    [
        .. Enum.GetValues<WaterProcess>().Where(p => p != WaterProcess.None).Select(p => "p-" + p),
        "b-All", "b-FreshLiquid", "b-LakesRivers",
        "f-OceanEvaporation", "f-OceanPrecipitation", "f-VapourToLand", "f-LandPrecipitation", "f-LandEvaporation", "f-Runoff",
        "sun", "freezing", "snowline", "rainshadow", "drop", "thermal", "parcel", "cloudbase", "wind", "leeward", "front",
        "earth", "atmosphere", "oceans", "land",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(WaterCycleDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(WaterCycleDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(WaterMode.Cycle)]
    [InlineData(WaterMode.Cloud)]
    [InlineData(WaterMode.Stores)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(WaterMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new WaterCycleViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([WaterCycleLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: light up a way of moving, change the air, or click something.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.SelectedProcess = page.SelectedProcess == WaterProcess.Evaporation ? WaterProcess.Runoff : WaterProcess.Evaporation;
            page.GroundTemperature = page.GroundTemperature > 20 ? 10 : 30;
            page.Card.ShowQuietly("oceans");
        });
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    public void EveryCardState_HasItsWords(string culture)
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo(culture));
        try
        {
            using var page = new WaterCycleViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Water_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Water_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Water_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Water_", page.Subtitle + page.ScaleNote + page.Hint, StringComparison.Ordinal);
            }

            page.Mode = WaterMode.Cycle;
            foreach (var process in Enum.GetValues<WaterProcess>())
            {
                page.SelectedProcess = process;
                Check();
            }

            page.Mode = WaterMode.Cloud;
            foreach (var lift in Enum.GetValues<CloudLift>())
            {
                page.Lift = lift;
                foreach (var t in new[] { 0.0, 0.3, 0.6, 0.95 })
                {
                    page.Time = page.Duration * t;
                    Check();
                }
            }

            page.Mode = WaterMode.Stores;
            foreach (var view in Enum.GetValues<StoresView>())
            {
                page.StoresView = view;
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
            var (window, model) = OpenAt("Water-Cycle");
            var water = ((WaterCycleTextPageViewModel)model.Pages[TextPageOf("Water-Cycle")]).Water;
            water.IsPlaying = true;
            Frames(20);
            water.IsPlaying = false;
            Snapshot(window, "water-cycle-cycle-zh");

            water.Mode = WaterMode.Cloud;
            water.IsPlaying = false;
            water.Time = water.Duration * 0.7;
            Frames(10);
            Snapshot(window, "water-cycle-cloud-zh");

            // The balls grow in while it plays.
            water.Mode = WaterMode.Stores;
            water.IsPlaying = true;
            Frames(60);
            water.IsPlaying = false;
            Snapshot(window, "water-cycle-stores-zh");

            water.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(water.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
