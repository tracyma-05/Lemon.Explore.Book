using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Projectile;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class ProjectileChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "cannon", "ball", "strobe", "velocity", "vx", "vy", "weight", "drag", "peak", "range", "airless", "groundshadow",
        "postshadow", "launcher", "farthest", "envelope", "landing", "dropped", "thrown", "oddnumbers", "monkey", "dart",
        "hunter", "aim", "fall",
    ];

    /// <summary>The lines the walkthrough glows, which are not clickable.</summary>
    private static readonly string[] Lines = ["path"];

    private static ProjectileViewModel NewPage(LessonPlayerTests.FakeNarration narration) =>
        new(new ExploreLocationService(new MemorySettingsStore()), narration);

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(ProjectileDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(ProjectileDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(ProjectileMode.Launch)]
    [InlineData(ProjectileMode.BestAngle)]
    [InlineData(ProjectileMode.Together)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(ProjectileMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = NewPage(narration);
        page.Mode = mode;
        var used = WalkthroughKeys([ProjectileLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): another shot, another speed, another dart speed.
        RunToEnd(page.Lesson, narration, new HashSet<string>([.. TargetIds, .. Lines]), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Angle = page.Angle > 45 ? page.Angle - 10 : page.Angle + 10;
            page.LaunchSpeed = page.LaunchSpeed > 30 ? page.LaunchSpeed - 5 : page.LaunchSpeed + 5;
            page.DartSpeed = page.DartSpeed > 30 ? page.DartSpeed - 5 : page.DartSpeed + 5;
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
            using var page = NewPage(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Proj_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Proj_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Proj_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Proj_", page.Subtitle + page.ScaleNote + page.DragHint + page.ClockText + page.EarthGravityNote, StringComparison.Ordinal);
            }

            // Every view, at the start, part-way and after landing, on each world, with and without air.
            foreach (var mode in Enum.GetValues<ProjectileMode>())
            {
                page.Mode = mode;
                foreach (var place in Enum.GetValues<ProjectilePlace>())
                {
                    page.Place = place;
                    foreach (var air in new[] { false, true })
                    {
                        page.AirDrag = air;
                        foreach (var fraction in new[] { 0, 0.3, 0.6, 1.2 })
                        {
                            page.Time = page.Duration * fraction;
                            Check();
                        }
                    }
                }
            }

            page.Mode = ProjectileMode.Together;
            foreach (var scene in Enum.GetValues<TogetherScene>())
            {
                page.Scene = scene;
                foreach (var letsGo in new[] { false, true })
                {
                    page.MonkeyLetsGo = letsGo;
                    page.Time = page.Duration;
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
            var (window, model) = OpenAt("Projectile");
            var projectile = ((ProjectileTextPageViewModel)model.Pages[TextPageOf("Projectile")]).Projectile;
            projectile.IsPlaying = true;
            Frames(40);
            projectile.IsPlaying = false;
            Snapshot(window, "projectile-launch-zh");

            projectile.Mode = ProjectileMode.BestAngle;
            projectile.IsPlaying = true;
            Frames(60);
            projectile.IsPlaying = false;
            Snapshot(window, "projectile-best-zh");

            projectile.Mode = ProjectileMode.Together;
            projectile.IsPlaying = true;
            Frames(40);
            projectile.IsPlaying = false;
            Snapshot(window, "projectile-drop-zh");

            projectile.Scene = TogetherScene.MonkeyHunter;
            projectile.IsPlaying = true;
            Frames(40);
            projectile.IsPlaying = false;
            Snapshot(window, "projectile-monkey-zh");

            projectile.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(projectile.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
