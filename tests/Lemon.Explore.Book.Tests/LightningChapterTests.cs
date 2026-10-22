using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Lightning;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class LightningChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "cloth", "paper", "water", "ball", "earth", "rod", "humidity", "spark", "updraft", "anvil", "top", "graupel", "ice",
        "rain", "freezing", "reversal", "glaciation", "upper", "main", "lower", "meter", "corona", "field", "tree", "house",
        "lrod", "person", "cloudbase", "slow", "leader", "streamer", "stroke", "later", "dart", "hit", "zone", "sphere",
        "strip", "squeezed",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(LightningDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(LightningDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(LightningMode.Static)]
    [InlineData(LightningMode.Cloud)]
    [InlineData(LightningMode.Strike)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(LightningMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new LightningViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([LightningLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: change the materials or the air, grow the cloud, or make a different strike.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Humidity = page.Humidity > 50 ? 20 : 80;
            page.CloudTop = page.CloudTop > 12 ? 9 : 15;
            page.PeakCurrent = page.PeakCurrent > 50 ? 10 : 100;
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
            using var page = new LightningViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Lightning_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Lightning_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Lightning_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Lightning_", page.Subtitle + page.ScaleNote + page.Hint + page.ClockText, StringComparison.Ordinal);
            }

            page.Mode = LightningMode.Static;
            foreach (var rod in Enum.GetValues<Material>())
            {
                page.Rod = rod;
                foreach (var humidity in new[] { 20.0, 85 })
                {
                    page.Humidity = humidity;
                    Check();
                }
            }

            foreach (var charge in new[] { -250e-9, 0, 250e-9 })
            {
                page.Charge = charge;
                Check();
            }

            page.Mode = LightningMode.Cloud;
            foreach (var top in new[] { 9.0, 12, 16 })
            {
                page.CloudTop = top;
                Check();
            }

            page.Mode = LightningMode.Strike;
            foreach (var current in new[] { 8.0, 30, 100 })
            {
                page.PeakCurrent = current;
                foreach (var t in new[] { 0.0, 0.3, 0.7, 1 })
                {
                    page.StrikeTime = page.StrikeLength * t;
                    Check();
                }
            }

            page.LightningRod = !page.LightningRod;
            Check();
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
            var (window, model) = OpenAt("Lightning");
            var lightning = ((LightningTextPageViewModel)model.Pages[TextPageOf("Lightning")]).Lightning;
            lightning.IsPlaying = true;
            Frames(30);
            lightning.IsPlaying = false;
            Snapshot(window, "lightning-static-zh");

            lightning.Mode = LightningMode.Cloud;
            lightning.IsPlaying = true;
            Frames(30);
            lightning.IsPlaying = false;
            Snapshot(window, "lightning-cloud-zh");

            lightning.Mode = LightningMode.Strike;
            lightning.IsPlaying = false;
            lightning.StrikeTime = lightning.StrikeLength * 0.3;
            Frames(10);
            Snapshot(window, "lightning-strike-zh");

            lightning.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(lightning.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
