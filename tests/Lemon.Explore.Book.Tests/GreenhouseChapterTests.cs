using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Greenhouse;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class GreenhouseChapterTests
{
    /// <summary>Every target id the diagram registers.</summary>
    private static readonly string[] TargetIds =
    [
        .. Enum.GetValues<Gas>().Where(g => g != Gas.O3).Select(g => "gas-" + g),
        .. new[] { "Stretch", "SymmetricStretch", "AsymmetricStretch", "Bend", "SymmetricBend", "AsymmetricBend", "Spin" }.Select(v => "vib-" + v),
        .. new[] { "In", "Surface", "Reflected", "AirSun", "Glow", "Back", "Window", "FromAir", "Rising" }.Select(GreenhouseDiagram.BudgetId),
        "sun", "space", "air", "clouds", "ground", "emission", "profile", "sunlight", "infrared", "dipole", "groundrays",
        "spectrum", "window", "greenhouse", "vents", "car", "carwindow", "outside", "pane", "floor", "warmair",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(GreenhouseDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(GreenhouseDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(GreenhouseMode.Planet)]
    [InlineData(GreenhouseMode.Molecules)]
    [InlineData(GreenhouseMode.GlassHouse)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(GreenhouseMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new GreenhouseViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([GreenhouseLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: change the CO₂, pick another gas, or open the vents.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Co2 = page.Co2 > 500 ? 280 : 1120;
            page.Gas = page.Gas == Gas.CO2 ? Gas.H2O : Gas.CO2;
            page.VentsOpen = !page.VentsOpen;
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
            using var page = new GreenhouseViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Green_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Green_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Green_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Green_", page.Subtitle + page.ScaleNote + page.Hint, StringComparison.Ordinal);
            }

            page.Mode = GreenhouseMode.Planet;
            foreach (var co2 in new[] { 180.0, 280, 425, 1120 })
            {
                page.Co2 = co2;
                Check();
            }

            page.NoGases = true;
            Check();
            page.NoGases = false;

            page.Mode = GreenhouseMode.Molecules;
            foreach (var gas in Enum.GetValues<Gas>().Where(g => g != Gas.O3))
            {
                page.Gas = gas;
                Check();
                foreach (var vibration in page.Vibrations)
                {
                    vibration.IsSelected = true;
                    Check();
                }
            }

            page.Mode = GreenhouseMode.GlassHouse;
            foreach (var season in Enum.GetValues<Season>())
            {
                page.Season = season;
                foreach (var hour in new[] { 5.0, 10, 14, 22 })
                {
                    page.Hour = hour;
                    Check();
                }
            }

            page.VentsOpen = true;
            page.CarWindowOpen = true;
            page.SaltPane = true;
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
            var (window, model) = OpenAt("Greenhouse");
            var green = ((GreenhouseTextPageViewModel)model.Pages[TextPageOf("Greenhouse")]).Green;
            green.IsPlaying = true;
            Frames(30);
            green.IsPlaying = false;
            Snapshot(window, "greenhouse-planet-zh");

            green.Mode = GreenhouseMode.Molecules;
            green.Gas = Gas.CO2;
            green.IsPlaying = true;
            Frames(30);
            green.IsPlaying = false;
            Snapshot(window, "greenhouse-molecules-zh");

            green.Mode = GreenhouseMode.GlassHouse;
            green.IsPlaying = false;
            green.Hour = 14;
            Frames(10);
            Snapshot(window, "greenhouse-glasshouse-zh");

            green.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(green.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
