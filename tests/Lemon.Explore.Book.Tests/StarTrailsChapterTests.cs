using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.StarTrails;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class StarTrailsChapterTests
{
    private static readonly string[] Parts =
    [
        "pole", "arc", "horizon", "compass", "shutter", "observer", "ncp", "scp", "zenith", "equator", "circumpolar", "never",
        "risers", "sun", "earth", "night", "sidereal", "panorama",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        var ids = Parts
            .Concat(StarCatalog.Named.Select(s => "s-" + s.Id))
            .Concat(StarCatalog.Figures.Select(f => TrailsDiagram.FigureId(f.Id)));
        foreach (var keys in BothLanguages())
        {
            foreach (var id in ids)
            {
                Assert.Contains(TrailsDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(TrailsDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(TrailsMode.Camera)]
    [InlineData(TrailsMode.Sphere)]
    [InlineData(TrailsMode.Year)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(TrailsMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new StarTrailsViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([StarTrailsLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string>
        {
            "shutter", "pole", "arc", "horizon", "compass",
            "observer", "ncp", "scp", "equator", "circumpolar", "never", "zenith",
            "night", TrailsDiagram.FigureId("Orion"), TrailsDiagram.FigureId("Scorpius"), "sidereal", "panorama", "earth",
        };

        // Each view's task: turn the camera, travel, another date.
        RunToEnd(page.Lesson, narration, known, () => Assert.Equal(mode, page.Mode), () =>
        {
            page.ViewAzimuth += 40;
            page.Latitude = page.Latitude > 0 ? page.Latitude - 20 : page.Latitude + 20;
            page.Day = TrailsDiagram.WrapDay(page.Day + 30);
        });
    }

    [AvaloniaFact]
    public void TheSky_IsSeenFromTheObserverLocation_UntilTheReaderTravels()
    {
        var location = new ExploreLocationService(new MemorySettingsStore());
        using var page = new StarTrailsViewModel(location, new LessonPlayerTests.FakeNarration());
        Assert.Equal(location.Current.Latitude, page.Latitude, 1);

        location.Select("Sydney");
        Assert.Equal(ExplorePlaces.Find("Sydney")!.Latitude, page.Latitude, 1);

        page.SetLatitudeCommand.Execute("90");
        Assert.Equal(90, page.Latitude, 1);
        page.UseMyLatitudeCommand.Execute(null);
        Assert.Equal(ExplorePlaces.Find("Sydney")!.Latitude, page.Latitude, 1);
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Star-Trails");
            var trails = ((StarTrailsTextPageViewModel)model.Pages[TextPageOf("Star-Trails")]).Trails;
            trails.IsPlaying = false;
            trails.Exposure = 3;
            Frames(10);
            Snapshot(window, "star-trails-camera-zh");

            trails.Mode = TrailsMode.Sphere;
            trails.IsPlaying = false;
            Frames(10);
            Snapshot(window, "star-trails-sphere-zh");

            trails.Mode = TrailsMode.Year;
            trails.IsPlaying = false;
            Frames(10);
            Snapshot(window, "star-trails-year-zh");

            trails.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(trails.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
