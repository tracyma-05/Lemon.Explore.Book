using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.SolarSystem;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class SolarSystemChapterTests
{
    [Fact]
    public void EveryBody_HasANameStoryAndExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var body in SolarSystemCatalog.AllBodies)
            {
                Assert.Contains(SolarSystemViewModel.BodyTitleKey(body.Id)!, keys);
                Assert.Contains(SolarSystemViewModel.BodyClipKey(body.Id)!, keys);
                Assert.Contains($"SolarSystem_{body.Id}_Tagline", keys);
                Assert.Contains($"SolarSystem_{body.Id}_Description", keys);
            }
        }
    }

    [AvaloniaFact]
    public void TheTour_UsesKeysInBothLanguages_AndRunsToTheEnd()
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new SolarSystemViewModel(narration);
        var used = WalkthroughKeys([SolarSystemLessons.Tour(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string> { "Sun", "Earth", "belt", "Jupiter", "Saturn", "Neptune", "Moon" };
        RunToEnd(page.Lesson, narration, known, () => { },
            () => page.SelectedBody = SolarSystemCatalog.Bodies.First(b => b.Id != "Sun" && b != page.SelectedBody));

        // Flying to Earth on the tour, and choosing a body during the task, do not read the body out over the tour.
        Assert.DoesNotContain(narration.Played, k => k.StartsWith("Narr_SolarSystem_Obj_", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void ChoosingABody_ReadsItAloud_AndClickingItAgainRepeats()
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new SolarSystemViewModel(narration);
        var mars = SolarSystemCatalog.Body("Mars");

        page.SelectedBody = mars;
        page.ReselectedCommand.Execute(mars);

        Assert.Equal(["Narr_SolarSystem_Obj_Mars", "Narr_SolarSystem_Obj_Mars"], narration.Played);
        Assert.Equal("Mars", page.Card.Id);
    }

    [AvaloniaFact]
    public void TheSpread_Renders_AndPickingSaturnShowsItsStory()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("en"));
        try
        {
            var (window, model) = OpenAt("Solar-System");
            var solar = ((SolarSystemTextPageViewModel)model.Pages[TextPageOf("Solar-System")]).Solar;
            Frames(20);
            Snapshot(window, "solar-system");

            solar.SelectedBody = SolarSystemCatalog.Body("Saturn");
            Frames(40);
            Assert.Equal("Saturn", solar.Card.Id);
            Snapshot(window, "solar-system-saturn");

            solar.IsRealScale = true;
            solar.SelectedBody = null;
            Frames(40);
            Snapshot(window, "solar-system-real-scale");

            // Turning away stops the tour.
            solar.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(solar.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
