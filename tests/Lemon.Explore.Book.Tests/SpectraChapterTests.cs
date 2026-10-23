using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Spectra;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class SpectraChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "nucleus", "electron", "orbit", "level1", "level2", "level3", "level4", "level5", "level6", "continuum",
        "photonin", "photonout", "lamp", "hspectrum", "uv", "ir", "cloud", "energy",
        "gaslamp", "bulb", "sun", "coolgas", "slit", "prism", "line", "darkline", "trace", "ruler", "compare", "zoom",
        "mystery",
        "star", "peak", "curve", "visible", "starspectrum", "balmer", "calcium", "helium", "bands", "reference", "shift",
    ];

    /// <summary>Every state each view's card can name (see RefreshAtom, RefreshLamps and the star's class).</summary>
    private static readonly (string View, string[] States)[] CardStates =
    [
        ("Atom", ["Ground", "Excited", "Free"]),
        ("Lamps", ["Lamp", "Bulb", "Sun", "ThroughGas", "Mystery"]),
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(SpectraDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(SpectraDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [Fact]
    public void EveryCardState_HasATitleAndAText_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var (view, states) in CardStates)
            {
                foreach (var state in states)
                {
                    Assert.Contains($"Spec_{view}_{state}_Title", keys);
                    Assert.Contains($"Spec_{view}_{state}_Text", keys);
                }
            }

            foreach (var type in "OBAFGKM")
            {
                Assert.Contains($"Spec_Class_{type}_Text", keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(SpectraMode.Atom)]
    [InlineData(SpectraMode.Lamps)]
    [InlineData(SpectraMode.Stars)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(SpectraMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new SpectraViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([SpectraLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): a photon fired, a gas guessed, the star changed.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.FireCommand.Execute(null);
            page.GuessCommand.Execute(nameof(Gas.Neon));
            page.Kelvin = page.Kelvin > 10_000 ? 5000 : 20_000;
        });
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    public void TheCard_HasItsWords_InEachView(string culture)
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo(culture));
        try
        {
            using var page = new SpectraViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Spec_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Spec_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Spec_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Spec_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
                Assert.DoesNotContain("Spec_", page.GuessText, StringComparison.Ordinal);
            }

            page.Mode = SpectraMode.Atom;
            foreach (var level in new[] { 1, 3, 0 })
            {
                page.Level = level;
                foreach (var nm in new[] { "121.57", "656.28", "80" })
                {
                    page.SetPhotonCommand.Execute(nm);
                    Check();
                }
            }

            foreach (var lastEvent in new[] { "emit 3 2", "absorb 1 3", "free 2", "pass 640.0", "capture 4" })
            {
                page.LastEvent = lastEvent;
                Check();
            }

            page.Mode = SpectraMode.Lamps;
            foreach (var source in Enum.GetValues<LightSource>())
            {
                page.SourceChoice.Selected = source.ToString();
                page.SelectedLine = 656.28;
                Check();
            }

            page.StartMystery(Gas.Sodium);
            Check();
            page.GuessCommand.Execute(nameof(Gas.Neon));
            Check();
            page.GuessCommand.Execute(nameof(Gas.Sodium));
            Assert.False(page.Mystery);
            Check();

            page.Mode = SpectraMode.Stars;
            foreach (var kelvin in new[] { 3000.0, 4300, 5772, 6500, 9900, 22_000, 38_000 })
            {
                page.Kelvin = kelvin;
                foreach (var kms in new[] { "0", "-300", "15000" })
                {
                    page.SetVelocityCommand.Execute(kms);
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
            var (window, model) = OpenAt("Spectra");
            var spectra = ((SpectraTextPageViewModel)model.Pages[TextPageOf("Spectra")]).Spectra;
            spectra.IsPlaying = true;
            spectra.FireCommand.Execute(null);
            Frames(30);
            Snapshot(window, "spectra-atom-zh");

            spectra.Mode = SpectraMode.Lamps;
            spectra.SelectedLine = 656.28;
            Frames(20);
            Snapshot(window, "spectra-lamps-zh");

            spectra.Mode = SpectraMode.Stars;
            spectra.SetVelocityCommand.Execute("15000");
            Frames(20);
            spectra.IsPlaying = false;
            Snapshot(window, "spectra-stars-zh");

            spectra.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(spectra.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
