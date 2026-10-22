using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Earthquakes;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class QuakeChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "ridge", "trench", "collision", "transform", "rift", "diffuse", "depth", "hawaii", "place", "chile1960",
        "alaska1964", "sumatra2004", "tohoku2011", "bolivia1994", "sanfrancisco1906", "wenchuan2008", "nepal2015",
        "turkey2023", "tangshan1976", "axis", "magma", "smoker", "stripes", "oldfloor", "lithosphere",
        "asthenosphere", "field", "oceanplate", "upperplate", "slab", "benioff", "arcvolcano", "melt", "wedge",
        "locked", "tsunami", "india", "asia", "tethys", "himalaya", "tibet", "suture", "slabbreak", "gangdese",
        "fault", "road", "epicentre", "farfield", "stress", "quake", "station", "pwave", "swave", "surfacewave",
        "shadowp", "shadows", "crust", "mantle", "outercore", "innercore", "gutenberg", "moho", "pcurve", "scurve",
        .. PlateModel.Plates.Select(plate => "plate:" + plate.Id),
        .. PlateModel.Plates.Select(plate => "group:" + plate.Group).Distinct(),
    ];

    /// <summary>The lines the walkthrough glows, which are not clickable.</summary>
    private static readonly string[] Lines = ["fence"];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(QuakeDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(QuakeDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(QuakeMode.Plates)]
    [InlineData(QuakeMode.Boundaries)]
    [InlineData(QuakeMode.Inside)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(QuakeMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new QuakeViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([QuakeLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: pick something on the map, set off a quake, or move the quake or the station.
        RunToEnd(page.Lesson, narration, new HashSet<string>([.. TargetIds, .. Lines]), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.PickFamousCommand.Execute("Tohoku2011");
            page.QuakeNowCommand.Execute(null);
            page.StationDistance = page.StationDistance > 90 ? 40 : 140;
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
            using var page = new QuakeViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Quake_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Quake_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Quake_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Quake_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            page.Mode = QuakeMode.Plates;
            Check();
            foreach (var famous in new[] { "Chile1960", "SanFrancisco1906", "Wenchuan2008", "Tangshan1976" })
            {
                page.PickFamousCommand.Execute(famous);
                Check();
            }

            page.Mode = QuakeMode.Boundaries;
            foreach (var section in Enum.GetValues<SectionKind>())
            {
                page.Section = section;
                foreach (var t in new[] { 0.0, 0.5, 1 })
                {
                    page.TimeFraction = t;
                    Check();
                }
            }

            page.Mode = QuakeMode.Inside;
            foreach (var distance in new[] { 20.0, 100, 150 })
            {
                page.StationDistance = distance;
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
            var (window, model) = OpenAt("Earthquakes");
            var quake = ((QuakeTextPageViewModel)model.Pages[TextPageOf("Earthquakes")]).Quake;
            quake.IsPlaying = true;
            Frames(20);
            quake.IsPlaying = false;
            Snapshot(window, "earthquakes-plates-zh");

            quake.Mode = QuakeMode.Boundaries;
            quake.IsPlaying = true;
            Frames(40);
            quake.IsPlaying = false;
            Snapshot(window, "earthquakes-boundaries-zh");

            quake.Mode = QuakeMode.Inside;
            quake.IsPlaying = true;
            Frames(40);
            quake.IsPlaying = false;
            Snapshot(window, "earthquakes-inside-zh");

            quake.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(quake.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
