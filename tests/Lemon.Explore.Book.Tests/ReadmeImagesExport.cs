using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.Infrastructures.Timing;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// Not a check: saves the README's screenshots, in English and Chinese, into <c>BOOK_README_DIR</c>/&lt;culture&gt;
/// (<c>en</c>, <c>zh-CN</c>). Runs only with that set; <c>scripts/readme/make-images.py</c> runs it and writes the
/// committed JPEGs under <c>docs/images</c>. Time is stepped, as for the videos, so animations are caught mid-way.
/// </summary>
public sealed class ReadmeImagesExport
{
    /// <summary>Chapters shown as a spread, with the view to show (null: the chapter's first).</summary>
    private static readonly (string Slug, string? View)[] Spreads =
    [
        ("Solar-System", null),
        ("Rainbow", null),
        ("Black-Hole", null),
        ("Acids-Bases", null),
    ];

    [AvaloniaFact]
    public void SavesTheReadmeImages_WhenAskedTo()
    {
        var directory = Environment.GetEnvironmentVariable("BOOK_README_DIR");
        if (string.IsNullOrEmpty(directory)) return;

        var previous = LocalizationService.Instance.CurrentCulture;
        BookClock.BeginStepping();
        try
        {
            foreach (var culture in new[] { "en", "zh-CN" })
            {
                LocalizationService.Instance.SetCulture(CultureInfo.GetCultureInfo(culture));
                Save(Path.Combine(directory, culture));
            }
        }
        finally
        {
            BookClock.EndStepping();
            LocalizationService.Instance.SetCulture(previous);
        }
    }

    private static void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        var settings = new MemorySettingsStore();
        settings.Set(SettingKeys.AnimatesTurns, bool.FalseString);
        var model = new MainViewModel(settings);
        var window = new Window { Width = 1280, Height = 800, Content = new MainView { DataContext = model } };
        window.Show();

        model.GoToPage(0);
        Run(2);
        Capture(window, directory, "cover");

        model.GoToPage(BookContents.FirstContentsPage);
        Run(2);
        Capture(window, directory, "contents");

        foreach (var (slug, _) in Spreads)
        {
            model.GoToPage(BookContents.ChapterPageOf(slug));
            Run(3);
            Capture(window, directory, slug.ToLowerInvariant());
        }

        // A walkthrough under way: the subtitle strip and the highlighted thing in the picture.
        var seasons = BookContents.ChapterPageOf("Seasons");
        model.GoToPage(seasons);
        Run(1);
        var lesson = LessonOf(model.Pages[seasons]);
        lesson.Start();
        Run(16);
        Capture(window, directory, "walkthrough");
        lesson.Stop();

        // The same book on a phone: one page at a time.
        window.Width = 420;
        window.Height = 860;
        model.GoToPage(BookContents.ChapterPageOf("Eclipses") + 1);
        Run(3);
        Capture(window, directory, "phone");

        window.Close();
    }

    private static LessonPlayer LessonOf(object page)
    {
        var content = page.GetType().GetProperties().Select(p => p.GetValue(page))
            .First(v => v?.GetType().GetProperty("Lesson")?.PropertyType == typeof(LessonPlayer))!;
        return (LessonPlayer)content.GetType().GetProperty("Lesson")!.GetValue(content)!;
    }

    /// <summary>Lets <paramref name="seconds"/> of book time pass, 30 frames a second.</summary>
    private static void Run(double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 30)
        {
            BookClock.Advance(1.0 / 30);
            Frames(1);
        }
    }

    private static void Capture(Window window, string directory, string name)
    {
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
}
