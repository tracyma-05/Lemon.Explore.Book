using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Kepler;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.ViewModels.Pages;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class KeplerChapterTests
{
    private static readonly string[] TargetIds =
    [
        "sun", "otherfocus", "planet", "center", "perihelion", "aphelion", "string", "majoraxis", "circle", "focal", "velocity",
        "equalareas", "you", "scale", "p-Mercury", "p-Venus", "p-Earth", "p-Mars", "p-Jupiter", "p-Saturn",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(KeplerDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(KeplerDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(KeplerMode.Ellipse)]
    [InlineData(KeplerMode.Areas)]
    [InlineData(KeplerMode.Periods)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(KeplerMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new KeplerViewModel(new Navigator(), narration) { Mode = mode };
        var used = WalkthroughKeys([KeplerLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string>
        {
            "orbit", "sun", "otherfocus", "string", "majoraxis", "radius", "equalareas", "sectors", "velocity", "perihelion",
            "planet", "p-Earth", "p-Mercury", "you", "yourorbit",
        };

        // Each view's task: reshape the orbit, drag the planet along it, move your planet.
        RunToEnd(page.Lesson, narration, known, () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Eccentricity = page.Eccentricity > 0.3 ? 0.1 : 0.6;
            page.Phase = (page.Phase + 0.25) % 1;
            page.YourAxis = page.YourAxis > 5 ? 2 : 7;
        });
    }

    [AvaloniaFact]
    public void TheSolarSystemLink_TurnsToTheSolarSystemChapter()
    {
        var navigator = new Navigator();
        using var page = new KeplerViewModel(navigator, new LessonPlayerTests.FakeNarration());

        page.OpenSolarSystemCommand.Execute(null);

        Assert.Equal(TextPageOf("Solar-System"), navigator.Page);
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView_AndTheBodyChipsFollowTheChoice()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Kepler");
            var kepler = ((KeplerTextPageViewModel)model.Pages[TextPageOf("Kepler")]).Kepler;
            kepler.IsPlaying = false;
            kepler.Body.Selected = "Halley";
            kepler.Phase = 0.15;
            Frames(10);
            var checkedBodies = window.GetVisualDescendants().OfType<RadioButton>()
                .Where(r => r.GroupName == "KeplerBody" && r.IsChecked == true).ToList();
            Assert.Equal(localization.GetString("Kepler_Body_Halley"), Assert.Single(checkedBodies).Content);
            Snapshot(window, "kepler-ellipse-zh");

            kepler.Mode = KeplerMode.Areas;
            kepler.IsPlaying = false;
            Frames(10);
            Snapshot(window, "kepler-areas-zh");

            kepler.Mode = KeplerMode.Periods;
            kepler.IsPlaying = false;
            Frames(10);
            Snapshot(window, "kepler-periods-zh");

            kepler.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(kepler.Lesson.IsActive);
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
