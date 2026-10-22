using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.IceAges;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class IceAgeChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "sun", "earth", "orbit", "perihelion", "aphelion", "axis", "summerhalf", "eccentricity", "tilt", "polestar",
        "strip", "globe", "ring65", "laurentide", "fennoscandian", "greenland", "seaice", "sunmeter",
        "sunshinechart", "icechart", "terminations", "now", "future", "sheet", "snowline", "snowfall", "melt",
        "sunrays", "reflect", "ground", "coast", "hudson", "co2",
    ];

    /// <summary>The lines the walkthrough glows, which are not clickable.</summary>
    private static readonly string[] Lines = [];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(IceAgeDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(IceAgeDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(IceAgeMode.Orbit)]
    [InlineData(IceAgeMode.Sunshine)]
    [InlineData(IceAgeMode.Sheet)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(IceAgeMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new IceAgeViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([IceAgeLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: move through time.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.TimeKa = page.TimeKa > -100 ? -400 : 0;
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
            using var page = new IceAgeViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Ice_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Ice_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Ice_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Ice_", page.Subtitle + page.ScaleNote + page.Hint + page.TimeText + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            foreach (var mode in Enum.GetValues<IceAgeMode>())
            {
                page.Mode = mode;
                foreach (var ka in new[] { -800.0, -430, -125, -21, 0, 55, 100 })
                {
                    page.TimeKa = ka;
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
            var (window, model) = OpenAt("Ice-Ages");
            var ice = ((IceAgeTextPageViewModel)model.Pages[TextPageOf("Ice-Ages")]).Ice;
            ice.IsPlaying = false;
            ice.JumpToCommand.Execute("-21");
            Frames(10);
            Snapshot(window, "ice-ages-orbit-zh");

            ice.Mode = IceAgeMode.Sunshine;
            Frames(10);
            Snapshot(window, "ice-ages-sunshine-zh");

            ice.Mode = IceAgeMode.Sheet;
            Frames(10);
            Snapshot(window, "ice-ages-sheet-zh");

            ice.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(ice.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
