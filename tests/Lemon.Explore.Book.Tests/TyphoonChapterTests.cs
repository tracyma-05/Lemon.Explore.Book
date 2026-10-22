using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Typhoon;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class TyphoonChapterTests
{
    /// <summary>Every target id the diagram registers (the days along a track and the landfalls are numbered).</summary>
    private static readonly string[] TargetIds =
    [
        .. Enumerable.Range(0, 12).Select(TyphoonDiagram.DayId),
        .. Enumerable.Range(0, 4).Select(TyphoonDiagram.LandfallId),
        "eye", "low", "eyewall", "bands", "isobars", "probe", "push", "motion", "strongside", "turn", "scale", "outflow",
        "inflow", "warmcore", "sea", "wind", "coast", "showers", "start", "warmedge", "high", "edge588", "steering", "track",
        "storm", "place", "pacific", "scs",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(TyphoonDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(TyphoonDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(TyphoonMode.Storm)]
    [InlineData(TyphoonMode.Engine)]
    [InlineData(TyphoonMode.Track)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(TyphoonMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new TyphoonViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([TyphoonLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: deepen the storm, warm the sea, or move the subtropical high.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.CentralPressure = page.CentralPressure > 950 ? 920 : 980;
            page.SeaTemperature = page.SeaTemperature > 28 ? 26 : 30;
            page.RidgeWest = page.RidgeWest > 125 ? 110 : 140;
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
            using var page = new TyphoonViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Typhoon_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Typhoon_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Typhoon_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Typhoon_", page.Subtitle + page.ScaleNote + page.Hint + page.ClockText, StringComparison.Ordinal);
            }

            page.Mode = TyphoonMode.Storm;
            foreach (var pressure in new[] { 995.0, 980, 965, 945, 915 })
            {
                page.CentralPressure = pressure;
                Check();
            }

            page.Southern = true;
            page.Moving = true;
            Check();
            page.Spin = false;
            Check();

            page.Mode = TyphoonMode.Engine;
            foreach (var sea in new[] { 25.5, 27.5, 30 })
            {
                page.SeaTemperature = sea;
                Check();
            }

            page.Landfall = true;
            foreach (var hours in new[] { 0.0, 12, 48 })
            {
                page.LandfallHours = Math.Min(hours, page.LandfallSpan);
                Check();
            }

            page.Mode = TyphoonMode.Track;
            foreach (var start in new[] { "Guam", "Philippines", "SouthChinaSea" })
            {
                page.UseStartCommand.Execute(start);
                foreach (var t in new[] { 0.0, 0.5, 1 })
                {
                    page.TrackHour = page.TrackLength * t;
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
            var (window, model) = OpenAt("Typhoon");
            var typhoon = ((TyphoonTextPageViewModel)model.Pages[TextPageOf("Typhoon")]).Typhoon;
            typhoon.IsPlaying = true;
            Frames(30);
            typhoon.IsPlaying = false;
            Snapshot(window, "typhoon-storm-zh");

            typhoon.Mode = TyphoonMode.Engine;
            typhoon.IsPlaying = true;
            Frames(30);
            typhoon.IsPlaying = false;
            Snapshot(window, "typhoon-engine-zh");

            typhoon.Mode = TyphoonMode.Track;
            typhoon.IsPlaying = false;
            typhoon.TrackHour = typhoon.TrackLength * 0.6;
            Frames(10);
            Snapshot(window, "typhoon-track-zh");

            typhoon.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(typhoon.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
