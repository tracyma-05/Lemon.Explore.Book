using Avalonia;
using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Waves;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class WavesChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "dippera", "dipperb", "probe", "difference", "quiet", "loud", "tank", "laser", "slits", "spacing", "screen",
        "order", "tape", "source", "listener", "bunched", "stretched", "cone", "heard",
    ];

    /// <summary>The lines the walkthrough glows, which are not clickable.</summary>
    private static readonly string[] Lines = ["paths"];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(WavesDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(WavesDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(WaveMode.Ripple)]
    [InlineData(WaveMode.Slits)]
    [InlineData(WaveMode.Doppler)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(WaveMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new WavesViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([WavesLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): two sources out of step at the probe, the probe on a bright
        // stripe, the listener moved.
        RunToEnd(page.Lesson, narration, new HashSet<string>([.. TargetIds, .. Lines]), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.SecondSource = true;
            page.OneSlit = false;
            page.ProbeOnCommand.Execute("Quiet");
            page.ProbeOnStripeCommand.Execute("1");
            page.Listener = new Point(page.Listener.X + 20, page.Listener.Y);
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
            using var page = new WavesViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Waves_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Waves_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Waves_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Waves_", page.Subtitle + page.ScaleNote + page.DragHint + page.ClockText, StringComparison.Ordinal);
            }

            page.Mode = WaveMode.Ripple;
            foreach (var probe in new[] { "Quiet", "Loud" })
            {
                page.ProbeOnCommand.Execute(probe);
                Check();
            }

            page.Opposite = true;
            Check();

            page.Mode = WaveMode.Slits;
            foreach (var stripe in new[] { "0", "1", "0.5" })
            {
                page.ProbeOnStripeCommand.Execute(stripe);
                Check();
            }

            page.WhiteLight = true;
            Check();
            page.OneSlit = true;
            Check();

            page.Mode = WaveMode.Doppler;
            foreach (var mach in new[] { "0", "0.26", "1", "2" })
            {
                page.SetMachCommand.Execute(mach);
                Check();
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
            var (window, model) = OpenAt("Waves");
            var waves = ((WavesTextPageViewModel)model.Pages[TextPageOf("Waves")]).Waves;
            waves.IsPlaying = true;
            Frames(30);
            waves.IsPlaying = false;
            Snapshot(window, "waves-ripple-zh");

            waves.Mode = WaveMode.Slits;
            waves.IsPlaying = true;
            Frames(30);
            waves.IsPlaying = false;
            Snapshot(window, "waves-slits-zh");

            waves.Mode = WaveMode.Doppler;
            waves.SetMachCommand.Execute("0.8");
            waves.IsPlaying = true;
            Frames(40);
            waves.IsPlaying = false;
            Snapshot(window, "waves-doppler-zh");

            waves.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(waves.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
