using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Volcanoes;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class VolcanoChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
        [.. VolcanoDiagram.TargetIds];

    /// <summary>The lines the walkthrough glows, which are not clickable.</summary>
    private static readonly string[] Lines = [];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(VolcanoDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(VolcanoDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(VolcanoMode.Eruption)]
    [InlineData(VolcanoMode.Cycle)]
    [InlineData(VolcanoMode.Magnifier)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(VolcanoMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new VolcanoViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([VolcanoLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (the original's): an explosive volcano, another grain to follow, rock squeezed deep.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.UsePreset("Pinatubo");
            page.Follow("Shell");
            page.SqueezeDepthKm = 20;
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
            using var page = new VolcanoViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Volc_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Volc_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Volc_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Volc_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            page.Mode = VolcanoMode.Eruption;
            foreach (var preset in new[] { "Kilauea", "Stromboli", "Merapi", "Pinatubo" })
            {
                page.UsePreset(preset);
                foreach (var progress in new[] { 0.0, 0.4, 0.9 })
                {
                    page.Progress = progress;
                    Check();
                }
            }

            page.Mode = VolcanoMode.Cycle;
            foreach (var journey in new[] { "Sand", "Mud", "Shell", "Magma" })
            {
                page.Follow(journey);
                foreach (var progress in new[] { 0.0, 0.5, 1 })
                {
                    page.Progress = progress;
                    Check();
                }
            }

            page.Mode = VolcanoMode.Magnifier;
            foreach (var depth in new[] { 0.0, 10, 25 })
            {
                page.SqueezeDepthKm = depth;
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
            var (window, model) = OpenAt("Volcanoes");
            var volcano = ((VolcanoTextPageViewModel)model.Pages[TextPageOf("Volcanoes")]).Volcano;
            volcano.IsPlaying = true;
            Frames(40);
            volcano.IsPlaying = false;
            Snapshot(window, "volcanoes-eruption-zh");

            volcano.Mode = VolcanoMode.Cycle;
            volcano.Follow("Shell");
            volcano.Progress = 0.4;
            Frames(10);
            Snapshot(window, "volcanoes-cycle-zh");

            volcano.Mode = VolcanoMode.Magnifier;
            volcano.IsPlaying = false;
            volcano.Progress = 0.6;
            Frames(10);
            Snapshot(window, "volcanoes-magnifier-zh");

            volcano.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(volcano.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
