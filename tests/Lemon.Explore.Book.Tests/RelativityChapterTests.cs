using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Relativity;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class RelativityChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "shipclock", "groundclock", "flash", "zigzag", "triangle", "mirror", "ship", "ground", "counter", "frame", "gamma",
        "observer",
        "lamp", "origin", "rear", "front", "train", "platform", "watcher", "light", "trainlength", "platformlength",
        "trainclocks", "platformclock", "time",
        "lightline", "earthline", "shipline", "turn", "now", "earthmarks", "shipmarks", "shipaxes", "jump", "shipnow",
        "signals", "strip", "star", "earthtwin", "traveller", "younger", "grid",
    ];

    /// <summary>Every state each view's card can name (see ClockState, TrainState and TwinState).</summary>
    private static readonly (string View, string[] States)[] CardStates =
    [
        ("Clock", ["Still", "Ground", "Ship"]),
        ("Train", ["Before", "Flight", "RearHit", "Together", "Apart"]),
        ("Twin", ["Start", "Out", "Back", "Home"]),
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(RelativityDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(RelativityDiagram.ObjectClipKey(id)!, keys);
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
                    Assert.Contains($"Rel_{view}_{state}_Title", keys);
                    Assert.Contains($"Rel_{view}_{state}_Text", keys);
                }
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(RelativityMode.LightClock)]
    [InlineData(RelativityMode.Simultaneity)]
    [InlineData(RelativityMode.Twins)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(RelativityMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new RelativityViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([RelativityLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task: the speed, the point of view or the trip changed.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Beta = page.Beta > 0.5 ? 0.3 : 0.9;
            page.Distance += 1;
        });
    }

    [Fact]
    public void TrainSteps_HoldTheMomentTheyTalkAbout()
    {
        using var page = new RelativityViewModel(new LessonPlayerTests.FakeNarration()) { Mode = RelativityMode.Simultaneity };
        page.Beta = 0.6;
        page.Frame = RelativityFrame.Ground;
        var (rear, front) = page.HitTimes();
        Assert.True(rear < front);
        page.HoldTrain("Rear");
        Assert.InRange(page.TrainHold, rear, front);
        page.HoldTrain("Flight");
        Assert.InRange(page.TrainHold, 0, rear);
        page.Frame = RelativityFrame.Ship;
        var onTrain = page.HitTimes();
        Assert.Equal(onTrain.Rear, onTrain.Front);
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
            using var page = new RelativityViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Rel_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Rel_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Rel_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Rel_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote
                    + page.GroundFrameText + page.ShipFrameText + page.BetaText + page.DistanceText, StringComparison.Ordinal);
            }

            foreach (var mode in Enum.GetValues<RelativityMode>())
            {
                page.Mode = mode;
                Check();
            }

            page.Mode = RelativityMode.LightClock;
            foreach (var frame in Enum.GetValues<RelativityFrame>())
            {
                page.Frame = frame;
                foreach (var beta in new[] { 0.0, 0.6, 0.99 })
                {
                    page.Beta = beta;
                    Check();
                }
            }

            page.Mode = RelativityMode.Simultaneity;
            foreach (var frame in Enum.GetValues<RelativityFrame>())
            {
                page.Frame = frame;
                foreach (var phase in new[] { "Before", "Flight", "RearHit", "BothHit" })
                {
                    page.TrainPhase = phase;
                    Check();
                }
            }

            page.Mode = RelativityMode.Twins;
            page.ShowSignals = true;
            foreach (var moment in new[] { "Start", "Out", "Turn", "Back", "Home" })
            {
                page.HoldTrip(moment);
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
            var (window, model) = OpenAt("Relativity");
            var relativity = ((RelativityTextPageViewModel)model.Pages[TextPageOf("Relativity")]).Relativity;
            relativity.IsPlaying = true;
            relativity.Beta = 0.8;
            Frames(30);
            Snapshot(window, "relativity-clock-zh");

            relativity.Mode = RelativityMode.Simultaneity;
            relativity.HoldTrain("Rear");
            Frames(20);
            Snapshot(window, "relativity-train-zh");

            relativity.Mode = RelativityMode.Twins;
            relativity.HoldTrip("Back");
            Frames(20);
            relativity.IsPlaying = false;
            Snapshot(window, "relativity-twins-zh");

            relativity.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(relativity.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
