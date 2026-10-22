using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.ViewModels.Pages;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class MoonPhasesChapterTests
{
    private static readonly string[] TargetIds =
    [
        "earth", "moon", "sunlight", "skymoon", "skysun", "moonrise", "moonset", "sun", "month-earth", "month-moon", "star",
        "start", "sidereal", "synodic", .. Enum.GetValues<MoonPhase>().Select(p => "ring-" + p),
    ];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(MoonPhasesDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(MoonPhasesDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(MoonPhasesMode.Phases)]
    [InlineData(MoonPhasesMode.Sky)]
    [InlineData(MoonPhasesMode.Month)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(MoonPhasesMode mode)
    {
        var narration = new LessonPlayerTests.FakeNarration();
        using var page = new MoonPhasesViewModel(narration, new Navigator()) { Mode = mode };
        var used = WalkthroughKeys([MoonPhasesLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        var known = new HashSet<string>
        {
            "moon", "sunlight", "halves", "orbit", "ring-FirstQuarter", "path", "skymoon", "moonrise", "skysun",
            "month-moon", "star", "sidereal", "month-earth", "angle", "synodic",
        };
        RunToEnd(page.Lesson, narration, known, () => Assert.Equal(mode, page.Mode),
            () => page.Age = MoonPhaseGeometry.AgeOf(MoonPhase.FirstQuarter));
    }

    [AvaloniaFact]
    public void TheEclipsesLink_TurnsToTheEclipsesChapter()
    {
        var navigator = new Navigator();
        using var page = new MoonPhasesViewModel(new LessonPlayerTests.FakeNarration(), navigator);

        page.OpenEclipsesCommand.Execute(null);

        Assert.Equal(TextPageOf("Eclipses"), navigator.Page);
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Moon-Phases");
            var moon = ((MoonPhasesTextPageViewModel)model.Pages[TextPageOf("Moon-Phases")]).Moon;
            moon.IsPlaying = false;
            moon.Age = MoonPhaseGeometry.AgeOf(MoonPhase.WaxingGibbous);
            Frames(10);
            Snapshot(window, "moon-phases-zh");

            moon.Mode = MoonPhasesMode.Sky;
            moon.IsPlaying = false;
            moon.TimeOfDay = 21;
            Frames(10);
            Snapshot(window, "moon-sky-zh");

            moon.Mode = MoonPhasesMode.Month;
            Frames(10);
            Snapshot(window, "moon-month-zh");

            moon.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(moon.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    private sealed class Navigator : IBookNavigator
    {
        public int? Page { get; private set; }

        public void GoToPage(int pageIndex) => Page = pageIndex;
    }
}
