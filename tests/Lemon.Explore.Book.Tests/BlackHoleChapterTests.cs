using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.BlackHole;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class BlackHoleChapterTests
{
    /// <summary>Every target id the diagram registers.</summary>
    private static readonly string[] TargetIds =
    [
        "shadow", "horizon", "photonring", "hat", "below", "coming", "going", "inner", "stars", "scale",
        "photonsphere", "isco", "probe", "trail", "newton", "start", "arrow", "caught", "bent", "critical", "clocks",
        "lens", "ring", "true", "image1", "image2", "arc", "track", "sideview",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(BlackHoleDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(BlackHoleDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(BlackHoleMode.Shadow)]
    [InlineData(BlackHoleMode.Orbits)]
    [InlineData(BlackHoleMode.Lensing)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(BlackHoleMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new BlackHoleViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([BlackHoleLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: tilted, launched by hand, or the hole moved.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Inclination = page.Inclination > 45 ? 10 : 85;
            page.StartRadialSpeed += 0.05;
            page.LensX += 0.5;
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
            using var page = new BlackHoleViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Hole_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Hole_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Hole_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Hole_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            foreach (var mode in Enum.GetValues<BlackHoleMode>())
            {
                page.Mode = mode;
                Check();
            }

            page.Mode = BlackHoleMode.Shadow;
            foreach (var example in BlackHoleExample.All)
            {
                page.ExampleId = example.Id;
                foreach (var tilt in new[] { 5.0, 50, 85 })
                {
                    page.Inclination = tilt;
                    Check();
                }
            }

            page.Doppler = false;
            Check();
            page.ShowDisk = false;
            Check();
            page.BendLight = false;
            Check();

            page.Mode = BlackHoleMode.Orbits;
            foreach (var launch in Enum.GetValues<ProbeLaunch>())
            {
                if (launch != ProbeLaunch.Custom) page.ChooseLaunch(launch);
                else page.StartRadialSpeed = 0.1;
                Check();
            }

            page.ProbeState = "Lost";
            page.FarTime = double.PositiveInfinity;
            Check();
            page.ProbeState = "Escaped";
            Check();

            page.Mode = BlackHoleMode.Lensing;
            foreach (var lens in LensExample.All)
            {
                page.LensId = lens.Id;
                foreach (var x in new[] { -2.0, -0.5, -0.1, 0 })
                {
                    page.LensX = x;
                    page.LensY = x == 0 ? 0 : BlackHoleDiagram.TrackY;
                    Check();
                }
            }

            page.LensOn = false;
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
            var (window, model) = OpenAt("Black-Hole");
            var hole = ((BlackHoleTextPageViewModel)model.Pages[TextPageOf("Black-Hole")]).Hole;
            hole.IsPlaying = false;
            Frames(10);
            Snapshot(window, "black-hole-shadow-zh");

            hole.Mode = BlackHoleMode.Orbits;
            hole.ChooseLaunch(ProbeLaunch.Rosette);
            hole.IsPlaying = true;
            Frames(30);
            hole.IsPlaying = false;
            Snapshot(window, "black-hole-orbits-zh");

            hole.Mode = BlackHoleMode.Lensing;
            hole.IsPlaying = false;
            hole.LensX = -0.4;
            Frames(10);
            Snapshot(window, "black-hole-lensing-zh");

            hole.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(hole.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
