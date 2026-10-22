using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.StarLife;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class StarLifeChapterTests
{
    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        var ids = Enum.GetValues<StarStage>().Select(StarDiagram.StageTarget)
            .Concat(Enum.GetValues<StarClass>().Select(c => "class-" + c))
            .Concat(StarModel.KnownStars.Select(s => "known-" + s.Id))
            .Concat(["sun", "orbits", "mainsequence", "giants", "supergiants", "whitedwarfs"]);
        foreach (var keys in BothLanguages())
        {
            foreach (var id in ids)
            {
                Assert.Contains(StarDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(StarDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(StarMode.Life)]
    [InlineData(StarMode.Diagram)]
    [InlineData(StarMode.Fates)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(StarMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new StarLifeViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([StarLifeLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string>
        {
            "stage-Protostar", "stage-MainSequence", "orbits", "stage-RedGiant", "stage-PlanetaryNebula", "stage-WhiteDwarf", "strip",
            "mainsequence", "known-Sun", "giants", "whitedwarfs", "track", "class-RedDwarf", "class-SunLike", "stage-Supernova",
            "stage-NeutronStar", "stage-BlackHole",
        };

        // Each view's task: travel along the life, or pick a heavier star (another kind of star too).
        RunToEnd(page.Lesson, narration, known, () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Story = page.Story > page.StoryLength / 2 ? 0 : page.StoryLength * 0.9;
            page.Mass = page.Mass > 5 ? 1 : 12;
        });
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Star-Life");
            var star = ((StarLifeTextPageViewModel)model.Pages[TextPageOf("Star-Life")]).Star;
            star.IsPlaying = false;
            star.Story = star.StoryLength * 0.7;
            Frames(10);
            Snapshot(window, "star-life-life-zh");

            star.Mode = StarMode.Diagram;
            star.IsPlaying = false;
            Frames(10);
            Snapshot(window, "star-life-diagram-zh");

            star.Mode = StarMode.Fates;
            star.IsPlaying = false;
            star.Mass = 18;
            Frames(10);
            Snapshot(window, "star-life-fates-zh");

            star.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(star.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
