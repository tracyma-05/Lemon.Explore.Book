using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>What the ported-chapter tests share: the resx keys, the book open at a chapter, frames and snapshots.</summary>
internal static class ChapterTestKit
{
    private static readonly string Resources =
        Path.Combine(LocalizationCoverageTests.FindRepositoryRoot(), "src", "Lemon.Explore.Book", "Resources");

    /// <summary>The keys of each language's resx file.</summary>
    public static IEnumerable<HashSet<string>> BothLanguages() =>
        new[] { "AppStrings.resx", "AppStrings.zh-CN.resx" }.Select(file =>
            XDocument.Load(Path.Combine(Resources, file)).Root!.Elements("data")
                .Select(d => (string)d.Attribute("name")!).ToHashSet(StringComparer.Ordinal));

    /// <summary>Every key a walkthrough speaks or offers as an answer, plus the shared feedback clips.</summary>
    public static HashSet<string> WalkthroughKeys(IEnumerable<IReadOnlyList<LessonStep>> walkthroughs)
    {
        var used = new HashSet<string>(StringComparer.Ordinal)
        {
            LessonPlayer.RightKey, LessonPlayer.WrongKey, LessonPlayer.NiceKey, LessonPlayer.RevealKey, LessonPlayer.MoveOnKey,
        };
        foreach (var step in walkthroughs.SelectMany(w => w))
        {
            used.Add(step.Key);
            used.UnionWith(step.Choices ?? []);
        }

        return used;
    }

    public static int TextPageOf(string slug) =>
        BookContents.ChapterPage(BookContents.Chapters.ToList().FindIndex(c => c.Slug == slug));

    /// <summary>A wide window with the book open at the chapter's spread.</summary>
    public static (Window Window, MainViewModel Model) OpenAt(string slug)
    {
        var settings = new MemorySettingsStore();
        settings.Set(SettingKeys.AnimatesTurns, bool.FalseString);
        var model = new MainViewModel(settings);
        model.GoToPage(TextPageOf(slug));
        var window = new Window { Width = 1360, Height = 860, Content = new MainView { DataContext = model } };
        window.Show();
        Frames(10);
        return (window, model);
    }

    /// <summary>Runs a walkthrough to its end, answering with the first choice and doing each task.</summary>
    public static void RunToEnd(LessonPlayer lesson, LessonPlayerTests.FakeNarration narration, ISet<string> known, Action check, Action doTask)
    {
        lesson.GapSeconds = 0;
        lesson.Start();
        for (var guard = 0; guard < 200 && lesson.State != LessonState.Done; guard++)
        {
            Assert.True(lesson.Highlight is null || known.Contains(lesson.Highlight), lesson.Highlight);
            check();
            if (lesson.State == LessonState.Quiz)
            {
                lesson.AnswerCommand.Execute(lesson.Choices[0]);
            }
            else if (lesson.State == LessonState.TryIt)
            {
                doTask();
            }

            narration.Finish();
            lesson.Tick(0.1);
        }

        Assert.Equal(LessonState.Done, lesson.State);
        lesson.Stop();
    }

    public static void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Captures a frame; with <c>BOOK_SNAPSHOT_DIR</c> set, also saves it as a PNG.</summary>
    public static void Snapshot(Window window, string name)
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
}
