using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class DayNightChapterTests
{
    private static readonly string[] TargetIds =
    [
        "sun", "sunriseline", "sunsetline", "noon", "midnight", "tropic", "polarcircle", "equator", "zone", "utc", "newday",
        "dateline", "sunlight", "axis", "angle", "polarday", "polarnight",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(DayNightDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(DayNightDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(DayNightMode.Map)]
    [InlineData(DayNightMode.TimeZones)]
    [InlineData(DayNightMode.Globe)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(DayNightMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new DayNightViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([DayNightLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string>
        {
            "terminator", "sun", "sunriseline", "noonline", "zone", "utc", "newday", "dateline", "sunlight", "axis", "angle", "polarday",
        };
        RunToEnd(page.Lesson, narration, known, () => Assert.Equal(mode, page.Mode),
            () => page.SelectCityCommand.Execute(WorldCities.All.First(c => c.Id != page.PlaceCity).Id));
    }

    [AvaloniaFact]
    public void ChoosingALocation_MovesThePlace_AndIsSaved()
    {
        var settings = new MemorySettingsStore();
        var location = new ExploreLocationService(settings);
        using var page = new DayNightViewModel(location, new LessonPlayerTests.FakeNarration());
        Assert.Equal("Beijing", page.PlaceCity);

        location.Select("Sydney");

        Assert.Equal("Sydney", page.PlaceCity);
        Assert.Equal(ExplorePlaces.Find("Sydney")!.Latitude, page.PlaceLatitude, 6);
        Assert.Equal("Sydney", new ExploreLocationService(settings).Current.Id);
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Day-Night");
            var day = ((DayNightTextPageViewModel)model.Pages[TextPageOf("Day-Night")]).Day;
            day.IsPlaying = false;
            day.JumpToCommand.Execute("JuneSolstice");
            day.IsPlaying = false;
            Frames(10);
            Snapshot(window, "day-night-map-zh");

            // The location picker opens over the page.
            var chip = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "Chip");
            chip.Flyout!.ShowAt(chip);
            Frames(5);
            Snapshot(window, "day-night-location-zh");
            chip.Flyout.Hide();

            day.Mode = DayNightMode.TimeZones;
            Frames(10);
            Snapshot(window, "day-night-zones-zh");

            day.Mode = DayNightMode.Globe;
            Frames(10);
            Snapshot(window, "day-night-globe-zh");

            day.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(day.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
