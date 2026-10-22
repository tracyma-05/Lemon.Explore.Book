using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class BlueSkyChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
        ["sun", "earthshadow", "beltofvenus", "sunbehind", "box", "sideeye", "endeye", "you", "overhead", "ray", "air"];

    /// <summary>The lines the walkthrough glows, which are not clickable.</summary>
    private static readonly string[] Lines = ["sunpath", "beam"];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(SkyDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(SkyDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(SkyMode.Sky)]
    [InlineData(SkyMode.Scattering)]
    [InlineData(SkyMode.Path)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(SkyMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new BlueSkyViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([BlueSkyLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: move the time of day, or change the haze.
        RunToEnd(page.Lesson, narration, new HashSet<string>([.. TargetIds, .. Lines]), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Hour = page.Hour > 12 ? page.FirstHour + 1 : page.LastHour - 1;
            page.Haze = page.Haze > 0.5 ? 0.05 : 0.85;
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
            using var page = new BlueSkyViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Sky_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Sky_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Sky_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Sky_", page.Subtitle + page.ScaleNote + page.DragHint + page.ClockText + page.HazeText, StringComparison.Ordinal);
            }

            foreach (var mode in Enum.GetValues<SkyMode>())
            {
                page.Mode = mode;
                foreach (var moment in new[] { "Sunrise", "Noon", "Afternoon", "Golden", "Sunset", "Twilight" })
                {
                    page.SetTimeCommand.Execute(moment);
                    foreach (var haze in new[] { 0.05, 0.3, 0.85 })
                    {
                        page.Haze = haze;
                        Check();
                    }
                }
            }

            page.Mode = SkyMode.Sky;
            page.LookAway = true;
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
            var (window, model) = OpenAt("Blue-Sky");
            var sky = ((BlueSkyTextPageViewModel)model.Pages[TextPageOf("Blue-Sky")]).Sky;
            sky.IsPlaying = false;
            sky.SetTimeCommand.Execute("Golden");
            Frames(10);
            Snapshot(window, "bluesky-sky-zh");

            sky.Mode = SkyMode.Scattering;
            sky.IsPlaying = true;
            Frames(30);
            sky.IsPlaying = false;
            Snapshot(window, "bluesky-scattering-zh");

            sky.Mode = SkyMode.Path;
            sky.SetTimeCommand.Execute("Sunset");
            Frames(10);
            Snapshot(window, "bluesky-path-zh");

            sky.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(sky.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
