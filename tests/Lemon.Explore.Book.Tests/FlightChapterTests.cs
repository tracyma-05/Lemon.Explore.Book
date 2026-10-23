using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Flight;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class FlightChapterTests
{
    /// <summary>Every target id the diagram registers, over its three views.</summary>
    private static readonly string[] TargetIds =
    [
        "wing", "leadingedge", "trailingedge", "chord", "aoa", "camber", "upper", "lower", "stagnation", "streamline",
        "wlift", "wdrag", "downwash", "timeline", "wake", "wind", "arrows",
        "plane", "lift", "weight", "thrust", "drag", "path", "planeaoa", "propeller", "balance", "throttle", "airspeed",
        "altimeter", "vario", "stallwarn", "runway",
        "aileron", "elevator", "rudder", "rollaxis", "pitchaxis", "yawaxis", "liftvector", "rearview", "centripetal",
        "loadfactor", "topview",
    ];

    /// <summary>Every state each view's card can name (see WingState, ForcesState and TurnState).</summary>
    private static readonly (string View, string[] States)[] CardStates =
    [
        ("Wing", ["Lift", "NearStall", "Stall", "NoLift", "Down"]),
        ("Forces", ["Parked", "Rolling", "Braking", "Stall", "Glide", "Climb", "Descend", "Level"]),
        ("Turn", ["Straight", "Turning", "Steep", "TurnStall", "Roll", "Pitch", "Yaw"]),
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(FlightDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(FlightDiagram.ObjectClipKey(id)!, keys);
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
                    Assert.Contains($"Flight_{view}_{state}_Title", keys);
                    Assert.Contains($"Flight_{view}_{state}_Text", keys);
                }
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(FlightMode.Wing)]
    [InlineData(FlightMode.Forces)]
    [InlineData(FlightMode.Turn)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(FlightMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new FlightViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([FlightLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): another angle, another throttle, another bank.
        RunToEnd(page.Lesson, narration, new HashSet<string>(TargetIds), () => Assert.Equal(mode, page.Mode), () =>
        {
            page.Alpha = page.Alpha > 5 ? page.Alpha - 3 : page.Alpha + 3;
            page.Throttle = page.Throttle > 0.5 ? page.Throttle - 0.2 : page.Throttle + 0.2;
            page.Bank = page.Bank > 20 ? page.Bank - 15 : page.Bank + 15;
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
            using var page = new FlightViewModel(new LessonPlayerTests.FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Flight_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Flight_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Flight_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Flight_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            page.Mode = FlightMode.Wing;
            foreach (var shape in Enum.GetValues<WingShape>())
            {
                page.Shape = shape;
                foreach (var alpha in new[] { -8.0, 0, 6, 14, 22 })
                {
                    page.Alpha = alpha;
                    Check();
                }
            }

            page.Inverted = true;
            Check();

            page.Mode = FlightMode.Forces;
            foreach (var scenario in new[] { "Takeoff", "Climb", "Cruise", "Glide", "Stall", "Landing" })
            {
                page.ApplyScenarioCommand.Execute(scenario);
                Check();
            }

            page.Mode = FlightMode.Turn;
            foreach (var demo in Enum.GetValues<TurnDemo>())
            {
                page.Demo = demo;
                foreach (var bank in new[] { 0.0, 30, 60 })
                {
                    page.Bank = bank;
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
            var (window, model) = OpenAt("Flight");
            var flight = ((FlightTextPageViewModel)model.Pages[TextPageOf("Flight")]).Flight;
            flight.IsPlaying = true;
            Frames(40);
            Snapshot(window, "flight-wing-zh");

            flight.ShowPressure = true;
            flight.Alpha = 12;
            Frames(10);
            Snapshot(window, "flight-wing-pressure-zh");

            flight.Mode = FlightMode.Forces;
            flight.ApplyScenarioCommand.Execute("Climb");
            Frames(40);
            Snapshot(window, "flight-forces-zh");

            flight.Mode = FlightMode.Turn;
            flight.Bank = 40;
            Frames(40);
            flight.IsPlaying = false;
            Snapshot(window, "flight-turn-zh");

            flight.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(flight.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
