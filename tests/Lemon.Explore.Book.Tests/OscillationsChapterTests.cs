using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Oscillations;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class OscillationsChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "bob", "length", "angle", "paper", "place", "formula", "mass", "spring", "rest", "unstretched", "stretch",
        "circle", "motor", "longest", "shortest", "topview",
    ];

    /// <summary>The lines the walkthrough glows, which are not clickable.</summary>
    private static readonly string[] Lines = ["trace", "snake"];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(OscillatorDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(OscillatorDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(OscillationMode.Pendulum)]
    [InlineData(OscillationMode.Spring)]
    [InlineData(OscillationMode.Wave)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(OscillationMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new OscillationsViewModel(new ExploreLocationService(new MemorySettingsStore()), narration) { Mode = mode };
        var used = WalkthroughKeys([OscillationsLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: a bigger or smaller swing, a harder or softer pull, a later moment of the wave.
        RunToEnd(page.Lesson, narration, new HashSet<string>([.. TargetIds, .. Lines]), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Amplitude = page.Amplitude > 30 ? 10 : 60;
            page.Pull = page.Pull > 0.1 ? 0.05 : 0.2;
            page.WaveTime = page.WaveTime > 10 ? 1 : 20;
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
            using var page = new OscillationsViewModel(new ExploreLocationService(new MemorySettingsStore()), new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Osc_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Osc_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Osc_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Osc_", page.Subtitle + page.ScaleNote + page.DragHint + page.ClockText + page.EarthGravityNote, StringComparison.Ordinal);
            }

            foreach (var mode in Enum.GetValues<OscillationMode>())
            {
                page.Mode = mode;
                foreach (var place in new[] { "Earth", "Moon", "Jupiter" })
                {
                    page.PlaceChoice[place] = true;
                    Check();
                }
            }

            page.Mode = OscillationMode.Pendulum;
            foreach (var amplitude in new[] { 5.0, 45, 90 })
            {
                page.Amplitude = amplitude;
                Check();
            }

            page.AirDrag = true;
            Check();

            page.Mode = OscillationMode.Spring;
            page.Shake = true;
            page.ShakeAtOwnFrequencyCommand.Execute(null);
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
            var (window, model) = OpenAt("Oscillations");
            var osc = ((OscillationsTextPageViewModel)model.Pages[TextPageOf("Oscillations")]).Osc;
            osc.IsPlaying = true;
            Frames(30);
            osc.IsPlaying = false;
            Snapshot(window, "oscillations-pendulum-zh");

            osc.Mode = OscillationMode.Spring;
            osc.IsPlaying = true;
            Frames(30);
            osc.IsPlaying = false;
            Snapshot(window, "oscillations-spring-zh");

            osc.Mode = OscillationMode.Wave;
            osc.IsPlaying = false;
            osc.JumpToCommand.Execute("0.2");
            Frames(10);
            Snapshot(window, "oscillations-wave-zh");

            osc.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(osc.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
