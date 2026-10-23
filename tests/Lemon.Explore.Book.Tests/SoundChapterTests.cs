using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Sound;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class SoundChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "speaker", "compression", "rarefaction", "particle", "wavelength", "pressure", "ear", "medium",
        "string", "bridge", "fret", "pluck", "node", "antinode", "body",
        "harmonic1", "harmonic2", "harmonic3", "harmonic4", "harmonic5", "harmonic6",
        "pipe", "openend", "closedend", "pnode", "pantinode", "pipemode1", "pipemode2", "pipemode3", "pipemode4",
        "waveform", "period", "timbre", "envelope", "fundamental", "overtone", "keyboard", "octave", "beat",
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(SoundDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(SoundDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(SoundMode.Wave)]
    [InlineData(SoundMode.Strings)]
    [InlineData(SoundMode.Timbre)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(SoundMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new SoundViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([SoundLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): another tone, a pluck, a note.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Frequency *= 2;
            page.PluckCommand.Execute(null);
            page.PlayNoteCommand.Execute(null);
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
            using var page = new SoundViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Sound_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Sound_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Sound_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Sound_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            page.Mode = SoundMode.Wave;
            foreach (var medium in Enum.GetValues<SoundMedium>())
            {
                page.Medium = medium;
                foreach (var (hertz, decibels) in new[] { (10.0, 60.0), (440.0, 60.0), (440.0, 0.0), (3000.0, 125.0), (17_000.0, 80.0), (25_000.0, 60.0) })
                {
                    page.Frequency = hertz;
                    page.Loudness = decibels;
                    Check();
                }
            }

            page.Mode = SoundMode.Strings;
            foreach (var point in new[] { 0.06, 0.2, 0.5 })
            {
                page.PluckPoint = point;
                Check();
            }

            page.SoloHarmonic = 1;
            Check();
            page.SoloHarmonic = 3;
            Check();
            foreach (var resonator in new[] { Resonator.OpenPipe, Resonator.ClosedPipe })
            {
                page.Resonator = resonator;
                Check();
            }

            page.Mode = SoundMode.Timbre;
            foreach (var instrument in Enum.GetValues<Instrument>())
            {
                page.Instrument = instrument;
                foreach (var second in Enum.GetValues<SecondNote>())
                {
                    page.SecondNote = second;
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
            var (window, model) = OpenAt("Sound");
            var sound = ((SoundTextPageViewModel)model.Pages[TextPageOf("Sound")]).Sound;
            sound.IsPlaying = true;
            Frames(30);
            sound.IsPlaying = false;
            Snapshot(window, "sound-wave-zh");

            sound.Mode = SoundMode.Strings;
            sound.PluckCommand.Execute(null);
            sound.IsPlaying = true;
            Frames(20);
            sound.IsPlaying = false;
            Snapshot(window, "sound-string-zh");

            sound.Resonator = Resonator.ClosedPipe;
            Frames(10);
            Snapshot(window, "sound-pipe-zh");

            sound.Mode = SoundMode.Timbre;
            sound.Instrument = Instrument.Violin;
            sound.SecondNote = SecondNote.Beats;
            Frames(10);
            Snapshot(window, "sound-timbre-zh");

            sound.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(sound.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
