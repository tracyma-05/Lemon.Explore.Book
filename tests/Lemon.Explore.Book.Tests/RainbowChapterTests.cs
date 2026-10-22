using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Rainbow;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class RainbowChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
        
    [
        "lamp", "normal", "index", "crests", "incidence", "refracted", "reflected", "through", "total", "critical", "prism", "spectrum", "turn", "drop", "entry", "exitangle", "crowd", "out", "losses", "antisolar", "bowangle", "primary", "secondary", "darkband", "brightinside",
    ];

    /// <summary>The lines the walkthrough glows, which are not clickable.</summary>
    private static readonly string[] Lines = ["bowray"];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(RainbowDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(RainbowDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(RainbowMode.Refraction)]
    [InlineData(RainbowMode.Drop)]
    [InlineData(RainbowMode.Sky)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(RainbowMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new RainbowViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([RainbowLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: change the angle, where the ray enters, or the Sun's height.
        RunToEnd(page.Lesson, narration, new HashSet<string>([.. TargetIds, .. Lines]), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Angle = page.Angle > 40 ? 10 : 70;
            page.Impact = page.Impact > 0.5 ? 0.2 : 0.9;
            page.SunAltitude = page.SunAltitude > 20 ? 5 : 35;
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
            using var page = new RainbowViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Rain_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Rain_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Rain_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Rain_", page.Subtitle + page.ScaleNote + page.DragHint + page.AngleText + page.ImpactText + page.SunAltitudeText, StringComparison.Ordinal);
            }

            page.Mode = RainbowMode.Refraction;
            foreach (var scene in Enum.GetValues<RefractionScene>())
            {
                page.Scene = scene;
                foreach (var medium in Enum.GetValues<OpticalMedium>())
                {
                    page.Medium = medium;
                    foreach (var colour in Enum.GetValues<LightColour>())
                    {
                        page.Colour = colour;
                        foreach (var angle in new[] { 0.0, 40, 85 })
                        {
                            page.Angle = angle;
                            Check();
                        }
                    }
                }
            }

            page.Scene = RefractionScene.Surface;
            page.FromInside = true;
            foreach (var angle in new[] { 20.0, 48.5, 70 })
            {
                page.Angle = angle;
                Check();
            }

            page.Mode = RainbowMode.Drop;
            foreach (var order in Enum.GetValues<BowOrder>())
            {
                page.Order = order;
                foreach (var impact in new[] { 0.0, 0.86, 0.99 })
                {
                    page.Impact = impact;
                    Check();
                }
            }

            page.Mode = RainbowMode.Sky;
            foreach (var size in Enum.GetValues<DropSize>())
            {
                page.DropSize = size;
                foreach (var sun in new[] { 0.0, 25, 45, 55 })
                {
                    page.SunAltitude = sun;
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
            var (window, model) = OpenAt("Rainbow");
            var rainbow = ((RainbowTextPageViewModel)model.Pages[TextPageOf("Rainbow")]).Rainbow;
            rainbow.IsPlaying = false;
            Frames(10);
            Snapshot(window, "rainbow-refraction-zh");

            rainbow.Mode = RainbowMode.Drop;
            Frames(10);
            Snapshot(window, "rainbow-drop-zh");

            rainbow.Mode = RainbowMode.Sky;
            rainbow.SunAltitude = 20;
            Frames(10);
            Snapshot(window, "rainbow-sky-zh");

            rainbow.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(rainbow.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
