using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Aurora;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class AuroraChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
        
    [
        "sun", "wind", "bowshock", "sheath", "magnetopause", "tail", "plasmasheet", "lobe", "belts", "cusp", "earth", "aurora", "imf", "xday", "xtail", "openline", "closedline", "stripped", "particle", "spiral", "mirror", "line", "shell", "foot", "inner", "outer", "ring", "cone", "oval", "you", "pole", "meridian", "curtain", "green", "red", "violet", "horizon", "sight", "view", "night", "seen",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(AuroraDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(AuroraDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(AuroraMode.Shield)]
    [InlineData(AuroraMode.Particles)]
    [InlineData(AuroraMode.Sky)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(AuroraMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new AuroraViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([AuroraLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: turn the solar wind's field, change the particle, or the storm.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Bz = page.Bz > 0 ? -10 : 5;
            page.PitchAngle = page.PitchAngle > 45 ? 20 : 70;
            page.Kp = page.Kp > 5 ? 2 : 8;
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
            using var page = new AuroraViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Aurora_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Aurora_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Aurora_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Aurora_", page.Subtitle + page.ScaleNote + page.Hint + page.ClockText + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            foreach (var mode in Enum.GetValues<AuroraMode>())
            {
                page.Mode = mode;
                foreach (var (speed, bz) in new[] { (300.0, 2.0), (450, -3), (800, -20) })
                {
                    page.WindSpeed = speed;
                    page.Bz = bz;
                    Check();
                }

                foreach (var species in Enum.GetValues<AuroraSpecies>())
                {
                    page.Species = species;
                    Check();
                }

                foreach (var kp in new[] { 1.0, 5, 9 })
                {
                    page.Kp = kp;
                    Check();
                }
            }

            page.Mode = AuroraMode.Shield;
            page.NoField = true;
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
            var (window, model) = OpenAt("Aurora");
            var aurora = ((AuroraTextPageViewModel)model.Pages[TextPageOf("Aurora")]).Aurora;
            aurora.IsPlaying = true;
            Frames(30);
            aurora.IsPlaying = false;
            Snapshot(window, "aurora-shield-zh");

            aurora.Mode = AuroraMode.Particles;
            aurora.IsPlaying = true;
            Frames(60);
            aurora.IsPlaying = false;
            Snapshot(window, "aurora-particles-zh");

            aurora.Mode = AuroraMode.Sky;
            aurora.Kp = 7;
            Frames(3);
            aurora.IsPlaying = false;
            aurora.BestHourCommand.Execute(null);
            Frames(10);
            Snapshot(window, "aurora-sky-zh");

            aurora.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(aurora.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
