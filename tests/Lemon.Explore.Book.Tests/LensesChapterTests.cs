using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Lenses;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class LensesChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "lens", "axis", "focus", "twof", "centre", "object", "image", "virtual", "ray1", "ray2", "ray3", "screen",
        "screenview", "aberration", "chromatic",
        "cornea", "iris", "eyelens", "ciliary", "retina", "fovea", "blindspot", "retinalimage", "vitreous", "glasses",
        "nearpoint", "farpoint", "chart", "view",
        "camlens", "aperture", "shutter", "sensor", "blurcircle", "dof", "focusmark", "subject0", "subject1", "subject2",
        "subject3", "photo", "ball", "meter", "fov",
    ];

    /// <summary>Every state each view's card can name (see BenchState, EyeState and the camera's exposure).</summary>
    private static readonly (string View, string[] States)[] CardStates =
    [
        ("Bench", ["Smaller", "Same", "Larger", "None", "Magnifier", "Concave", "Sunlight"]),
        ("Eye", ["Sharp", "SharpNear", "Front", "TooClose", "Behind"]),
        ("Camera", ["Bright", "Dark", "Good"]),
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(LensDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(LensDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [Fact]
    public void EveryCardState_HasATitleAndAText_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var (view, states) in CardStates)
            {
                foreach (var state in states)
                {
                    Assert.Contains($"Lens_{view}_{state}_Title", keys);
                    Assert.Contains($"Lens_{view}_{state}_Text", keys);
                }
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(LensMode.Bench)]
    [InlineData(LensMode.Eye)]
    [InlineData(LensMode.Camera)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(LensMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new LensesViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([LensesLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): the candle moved, the eye older, the camera refocused.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.ObjectDistance = page.ObjectDistance > 30 ? page.ObjectDistance - 5 : page.ObjectDistance + 5;
            page.Age = page.Age > 40 ? page.Age - 10 : page.Age + 10;
            page.FocusDistance = page.FocusDistance > 5 ? page.FocusDistance / 2 : page.FocusDistance * 2;
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
            using var page = new LensesViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Lens_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Lens_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Lens_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Lens_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            page.Mode = LensMode.Bench;
            foreach (var preset in new[] { "Camera", "Copier", "Projector", "Magnifier" })
            {
                page.PresetCommand.Execute(preset);
                Check();
            }

            page.KindChoice.Selected = nameof(LensKind.Concave);
            Check();

            page.Mode = LensMode.Eye;
            foreach (var distance in new[] { "Inf", "6", "0.33", "0.1" })
            {
                page.SetDistanceCommand.Execute(distance);
                foreach (var error in new[] { "0", "-3", "2" })
                {
                    page.SetErrorCommand.Execute(error);
                    Check();
                }
            }

            page.Mode = LensMode.Camera;
            foreach (var shutter in new[] { 0, 6, 12 })
            {
                page.ShutterIndex = shutter;
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
            var (window, model) = OpenAt("Lenses");
            var lenses = ((LensesTextPageViewModel)model.Pages[TextPageOf("Lenses")]).Lenses;
            lenses.IsPlaying = true;
            Frames(30);
            Snapshot(window, "lenses-bench-zh");

            lenses.Mode = LensMode.Eye;
            lenses.SetErrorCommand.Execute("-3");
            Frames(20);
            Snapshot(window, "lenses-eye-zh");

            lenses.Mode = LensMode.Camera;
            Frames(30);
            lenses.IsPlaying = false;
            Snapshot(window, "lenses-camera-zh");

            lenses.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(lenses.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
