using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class EclipsesChapterTests
{
    private static readonly string[] TargetIds =
        ["sun", "earth", "moon", "umbra", "penumbra", "antumbra", "observer", "newmoon", "fullmoon", "ecliptic", "season", "nodeline", "moonorbit"];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(EclipseDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(EclipseDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(EclipseMode.Solar)]
    [InlineData(EclipseMode.Lunar)]
    [InlineData(EclipseMode.Seasons)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(EclipseMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new EclipseViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([EclipseLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string>
        {
            "sun", "newmoon", "umbra", "penumbra", "observer", "antumbra", "moon", "fullmoon", "earth", "moonorbit", "nodeline",
            "season",
        };

        // Each mode's task: drag the Moon, pick another path through Earth's shadow, move through the year.
        RunToEnd(page.Lesson, narration, known, () => Assert.Equal(mode, page.Mode), () =>
        {
            page.MoonAngle = (page.MoonAngle + 40) % 360;
            page.LunarPath = page.LunarPath == LunarPath.Central ? LunarPath.OffCentre : LunarPath.Central;
            page.SeasonDay += 40;
        });
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("en"));
        try
        {
            var (window, model) = OpenAt("Eclipses");
            var eclipse = ((EclipseTextPageViewModel)model.Pages[TextPageOf("Eclipses")]).Eclipse;

            eclipse.IsPlaying = false;
            eclipse.MoonAngle = 2;
            Frames(10);
            Snapshot(window, "eclipses-solar");

            eclipse.Mode = EclipseMode.Lunar;
            eclipse.IsPlaying = false;
            eclipse.MoonAngle = 180;
            Frames(10);
            Snapshot(window, "eclipses-lunar");

            eclipse.Mode = EclipseMode.Seasons;
            Frames(10);
            Snapshot(window, "eclipses-seasons");

            eclipse.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(eclipse.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
