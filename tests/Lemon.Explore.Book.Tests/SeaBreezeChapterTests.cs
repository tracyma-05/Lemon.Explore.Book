using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.SeaBreeze;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class SeaBreezeChapterTests
{
    /// <summary>Every target id the diagram registers.</summary>
    private static readonly string[] TargetIds =
    [
        .. Enum.GetValues<MonsoonCity>().Select(c => "c-" + c),
        "s-Siberian", "s-AsianLow", "s-Mascarene", "s-Australia", "s-PacificRidge",
        "m-SW", "m-SE", "m-NW", "m-NE", "m-Australia",
        "sea", "land", "seabreeze", "landbreeze", "rising", "sinking", "return", "calm", "isobars", "high", "low", "sun",
        "cooling", "front", "flag", "smoke", "boat", "scale", "shower", "trough", "equator",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(BreezeDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(BreezeDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(BreezeMode.Coast)]
    [InlineData(BreezeMode.Cell)]
    [InlineData(BreezeMode.Monsoon)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(BreezeMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new SeaBreezeViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([SeaBreezeLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: move the clock, change a temperature, or click a city.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Hour = BreezeModel.Wrap(page.Hour + 4);
            page.LandTemperature = page.LandTemperature > 25 ? 15 : 35;
            page.Card.ShowQuietly("c-" + MonsoonCity.Mumbai);
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
            using var page = new SeaBreezeViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Breeze_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Breeze_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Breeze_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Breeze_", page.Subtitle + page.ScaleNote + page.Hint + page.ClockText, StringComparison.Ordinal);
            }

            page.Mode = BreezeMode.Coast;
            foreach (var sky in Enum.GetValues<BreezeSky>())
            {
                page.Sky = sky;
                foreach (var hour in new[] { 3.0, 8.5, 11, 15, 20.5 })
                {
                    page.Hour = hour;
                    Check();
                }
            }

            page.Mode = BreezeMode.Cell;
            foreach (var (land, sea) in new[] { (32.0, 24.0), (24.0, 24.0), (18.0, 26.0) })
            {
                page.LandTemperature = land;
                page.SeaTemperature = sea;
                foreach (var t in new[] { 0.0, 0.5, 0.99 })
                {
                    page.StepTime = page.CellDuration * t;
                    Check();
                }
            }

            page.Mode = BreezeMode.Monsoon;
            foreach (var city in Enum.GetValues<MonsoonCity>())
            {
                page.City = city;
                foreach (var month in new[] { 1.0, 4, 7, 10 })
                {
                    page.Month = month;
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
            var (window, model) = OpenAt("Sea-Breeze");
            var breeze = ((SeaBreezeTextPageViewModel)model.Pages[TextPageOf("Sea-Breeze")]).Breeze;
            breeze.IsPlaying = false;
            breeze.Hour = 14;
            Frames(10);
            Snapshot(window, "sea-breeze-coast-zh");

            breeze.Mode = BreezeMode.Cell;
            breeze.IsPlaying = false;
            breeze.StepTime = breeze.CellDuration * 0.8;
            Frames(10);
            Snapshot(window, "sea-breeze-cell-zh");

            breeze.Mode = BreezeMode.Monsoon;
            breeze.IsPlaying = false;
            breeze.Month = 7;
            Frames(10);
            Snapshot(window, "sea-breeze-monsoon-zh");

            breeze.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(breeze.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
