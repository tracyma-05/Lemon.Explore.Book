using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Expansion;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class ExpansionChapterTests
{
    /// <summary>Every target id the diagram registers.</summary>
    private static readonly string[] TargetIds =
    [
        "home", "galaxy", "arrow", "ruler", "ghost", "grid", "law", "clock",
        "source", "us", "light", "space", "distnow", "distthen", "travel", "drift", "rclock", "lab", "arriving", "line", "visible", "infrared", "jwst",
        "size", "cursor", "patch", "axis", "bigbang", "protons", "nuclei", "cmb", "firststars", "jades", "noon", "speedup", "sun", "now",
        "andromeda", "turnaround", "crunch",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(ExpansionDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(ExpansionDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(ExpansionMode.Hubble)]
    [InlineData(ExpansionMode.Redshift)]
    [InlineData(ExpansionMode.History)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(ExpansionMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new ExpansionViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([ExpansionLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: home moved, another galaxy's light, or the moment moved.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Home = new GalaxyCell(page.Home.I + 1, page.Home.J);
            page.ChooseSource(page.SourceId == "Coma" ? "Virgo" : "Coma");
            page.CosmicTime += 1;
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
            using var page = new ExpansionViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Expand_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Expand_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Expand_", fact, StringComparison.Ordinal);
            }

            foreach (var mode in Enum.GetValues<ExpansionMode>())
            {
                page.Mode = mode;
                Check();
            }

            page.Mode = ExpansionMode.Hubble;
            page.ShowOverlay = true;
            Check();
            page.Home = new GalaxyCell(2, 2);
            Check();

            page.Mode = ExpansionMode.Redshift;
            foreach (var source in LightSource.All)
            {
                page.ChooseSource(source.Id);
                Check();
            }

            page.Mode = ExpansionMode.History;
            foreach (var kind in Enum.GetValues<UniverseKind>())
            {
                page.UniverseKind = kind;
                foreach (var t in new[] { 1e-16, 1e-12, 1e-4, 0.05, 0.5, 4, 10, 13.8, 18, 25, 29.9 })
                {
                    page.CosmicTime = t;
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
            var (window, model) = OpenAt("Expansion");
            var expansion = ((ExpansionTextPageViewModel)model.Pages[TextPageOf("Expansion")]).Expansion;
            expansion.IsPlaying = false;
            expansion.HubbleProgress = 0.6;
            Frames(10);
            Snapshot(window, "expansion-hubble-zh");

            expansion.Mode = ExpansionMode.Redshift;
            expansion.IsPlaying = false;
            expansion.Journey = 0.6;
            Frames(10);
            Snapshot(window, "expansion-redshift-zh");

            expansion.Mode = ExpansionMode.History;
            expansion.IsPlaying = false;
            expansion.CosmicTime = 4;
            Frames(10);
            Snapshot(window, "expansion-history-zh");

            expansion.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(expansion.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
