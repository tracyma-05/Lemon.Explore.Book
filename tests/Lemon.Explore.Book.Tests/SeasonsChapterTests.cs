using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// The first ported chapter, as the reference for the others: every clickable thing has words, every walkthrough key
/// exists in both languages, and the spread renders and responds in the book. Set <c>BOOK_SNAPSHOT_DIR</c> to save
/// the frames.
/// </summary>
public sealed class SeasonsChapterTests
{
    private static readonly string Resources = Path.Combine(LocalizationCoverageTests.FindRepositoryRoot(), "src", "Lemon.Explore.Book", "Resources");

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in SeasonsDiagram.TargetIds)
            {
                Assert.Contains(SeasonsDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(SeasonsDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [AvaloniaFact]
    public void EveryWalkthroughKey_Exists_InBothLanguages()
    {
        using var page = new SeasonsViewModel(new LessonPlayerTests.FakeNarration(), new MemorySettingsStore());
        var used = new HashSet<string>(StringComparer.Ordinal)
        {
            LessonPlayer.RightKey, LessonPlayer.WrongKey, LessonPlayer.NiceKey, LessonPlayer.RevealKey, LessonPlayer.MoveOnKey,
        };

        foreach (var mode in Enum.GetValues<SeasonsMode>())
        {
            page.Mode = mode;
            foreach (var step in SeasonsLessons.For(page))
            {
                used.Add(step.Key);
                used.UnionWith(step.Choices ?? []);
            }
        }

        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }
    }

    [Fact]
    public void TheChapter_IsInTheBook_AsOneSpread()
    {
        var pages = MainViewModel.BuildPages(new NoNavigation(), BookServices.Create(new MemorySettingsStore()));
        var index = BookContents.Chapters.ToList().FindIndex(c => c.Slug == "Seasons");
        var text = Assert.IsType<SeasonsTextPageViewModel>(pages[BookContents.ChapterPage(index)]);
        var diagram = Assert.IsType<SeasonsDiagramPageViewModel>(pages[BookContents.ChapterPage(index) + 1]);

        Assert.Same(text.Seasons, diagram.Seasons);
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView_AndClickingTheSunExplainsIt()
    {
        var model = new MainViewModel(new MemorySettingsStore());
        model.GoToPage(SeasonsPage());
        var window = new Window { Width = 1360, Height = 860, Content = new MainView { DataContext = model } };
        window.Show();
        Frames(10);

        var seasons = ((SeasonsTextPageViewModel)model.Pages[SeasonsPage()]).Seasons;
        seasons.JumpToCommand.Execute(nameof(SeasonEvent.JuneSolstice));
        Frames(5);
        Snapshot(window, "seasons-tilt");

        // The Sun sits in the middle of the orbit view, a little below the centre.
        var diagram = window.GetVisualDescendants().OfType<SeasonsDiagram>().Single();
        var centre = diagram.TranslatePoint(new Point(diagram.Bounds.Width / 2, (diagram.Bounds.Height / 2) + 5), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Frames(3);
        Assert.Equal("sun", seasons.Card.Id);
        Snapshot(window, "seasons-sun-card");

        seasons.Mode = SeasonsMode.SunPath;
        Frames(5);
        Snapshot(window, "seasons-sunpath");

        seasons.Mode = SeasonsMode.Distance;
        seasons.Lesson.Start();
        Frames(5);
        Snapshot(window, "seasons-distance-lesson");

        // Turning away stops the walkthrough.
        window.FindDescendantOfType<BookView>()!.Next();
        for (var i = 0; i < 300 && window.FindDescendantOfType<BookView>()!.IsTurning; i++)
        {
            Frames(1);
        }

        Assert.False(seasons.Lesson.IsActive);
        window.Close();
    }

    private static int SeasonsPage() =>
        BookContents.ChapterPage(BookContents.Chapters.ToList().FindIndex(c => c.Slug == "Seasons"));

    private static IEnumerable<HashSet<string>> BothLanguages() =>
        new[] { "AppStrings.resx", "AppStrings.zh-CN.resx" }.Select(file =>
            XDocument.Load(Path.Combine(Resources, file)).Root!.Elements("data")
                .Select(d => (string)d.Attribute("name")!).ToHashSet(StringComparer.Ordinal));

    private static void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Snapshot(Window window, string name)
    {
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var directory = Environment.GetEnvironmentVariable("BOOK_SNAPSHOT_DIR");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
        }
    }

    private sealed class NoNavigation : ViewModels.Pages.IBookNavigator
    {
        public void GoToPage(int pageIndex)
        {
        }
    }
}
