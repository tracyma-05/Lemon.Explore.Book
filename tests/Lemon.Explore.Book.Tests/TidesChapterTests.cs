using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Tides;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels.Pages;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class TidesChapterTests
{
    private static readonly string[] TargetIds =
    [
        "earth", "moon", "sun", "highnear", "highfar", "low", "force", "you", "spin", "spring", "neap", "range", "staff", "level",
        "highwater", "lowwater", "boat",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(TidesDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(TidesDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(TidesMode.Bulges)]
    [InlineData(TidesMode.SpringNeap)]
    [InlineData(TidesMode.Coast)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(TidesMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new TidesViewModel(new Navigator(), new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([TidesLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string>
        {
            "bulges", "force", "highnear", "highfar", "spin", "you", "moon", "sun", "sunbulge", "moonbulge", "spring", "neap",
            "range", "water", "staff", "level", "highwater", "boat",
        };

        // Each view's task: move the time on by a few days (more than enough for the hours the daily views ask for).
        RunToEnd(page.Lesson, narration, known, () => Assert.Equal(mode, page.Mode),
            () => page.Day = (page.Day + 5) % MoonPhaseGeometry.SynodicMonth);
    }

    [AvaloniaFact]
    public void TheCoast_FollowsTheObserverLocation()
    {
        var location = new ExploreLocationService(new MemorySettingsStore());
        using var page = new TidesViewModel(new Navigator(), location, new LessonPlayerTests.FakeNarration());

        location.Select("Sydney");

        Assert.Equal(ExplorePlaces.Find("Sydney")!.Latitude, page.CoastLatitude, 6);
    }

    [AvaloniaFact]
    public void TheMoonPhasesLink_TurnsToTheMoonPhasesChapter()
    {
        var navigator = new Navigator();
        using var page = new TidesViewModel(navigator, new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());

        page.OpenMoonPhasesCommand.Execute(null);

        Assert.Equal(TextPageOf("Moon-Phases"), navigator.Page);
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Tides");
            var tides = ((TidesTextPageViewModel)model.Pages[TextPageOf("Tides")]).Tides;
            tides.IsPlaying = false;
            tides.Day = 3.3;
            Frames(10);
            Snapshot(window, "tides-bulges-zh");

            tides.Mode = TidesMode.SpringNeap;
            tides.IsPlaying = false;
            Frames(10);
            Snapshot(window, "tides-springneap-zh");

            tides.Mode = TidesMode.Coast;
            tides.IsPlaying = false;
            tides.Day = 14.2;
            Frames(10);
            Snapshot(window, "tides-coast-zh");

            tides.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(tides.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    private sealed class Navigator : IBookNavigator
    {
        public int? Page { get; private set; }

        public void GoToPage(int pageIndex) => Page = pageIndex;
    }
}
