using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Retrograde;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class RetrogradeChapterTests
{
    private static readonly string[] Parts =
    [
        "sun", "earth", "ecliptic", "station-r", "station-d", "opposition", "conjunction", "retro", "zodiac", "scale", "east",
        "sightline", "stars", "earthorbit", "planetorbit", "marks", "fan", "speed", "loop", "deferent", "epicycle", "frame",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        var ids = Parts.Concat(Enum.GetValues<RetroPlanet>().Select(RetroDiagram.PlanetId));
        foreach (var keys in BothLanguages())
        {
            foreach (var id in ids)
            {
                Assert.Contains(RetroDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(RetroDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(RetroMode.Sky)]
    [InlineData(RetroMode.Above)]
    [InlineData(RetroMode.Epicycle)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(RetroMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new RetrogradeViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([RetrogradeLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string>
        {
            RetroDiagram.PlanetId(RetroPlanet.Mars), RetroDiagram.PlanetId(RetroPlanet.Venus),
            "east", "station-r", "station-d", "retro", "opposition", "ecliptic",
            "sun", "earthorbit", "sightline", "fan", "earth", "speed",
            "loop", "deferent", "epicycle", "frame",
        };

        // Each view's task: another planet (or the frame turned round).
        RunToEnd(page.Lesson, narration, known, () => Assert.Equal(mode, page.Mode), () =>
        {
            page.IsPlaying = false;
            page.Planet = page.Planet == RetroPlanet.Jupiter ? RetroPlanet.Saturn : RetroPlanet.Jupiter;
            page.SunCentered = !page.SunCentered;
        });
    }

    [AvaloniaFact]
    public void TheSkyStrip_TurnsRound_InTheSouthernHemisphere()
    {
        var location = new ExploreLocationService(new MemorySettingsStore());
        using var page = new RetrogradeViewModel(location, new LessonPlayerTests.FakeNarration());
        Assert.True(page.EastLeft);

        location.Select("Sydney");

        Assert.False(page.EastLeft);
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Retrograde");
            var retro = ((RetrogradeTextPageViewModel)model.Pages[TextPageOf("Retrograde")]).Retro;
            retro.IsPlaying = false;
            retro.LoopProgress = 0.6;
            Frames(10);
            Snapshot(window, "retrograde-sky-zh");

            retro.Mode = RetroMode.Above;
            retro.IsPlaying = false;
            Frames(10);
            Snapshot(window, "retrograde-above-zh");

            retro.Mode = RetroMode.Epicycle;
            retro.IsPlaying = false;
            Frames(10);
            Snapshot(window, "retrograde-epicycle-zh");

            retro.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(retro.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
