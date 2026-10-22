using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// The book in a real (headless) window. Set <c>BOOK_SNAPSHOT_DIR</c> to also save each captured frame as a
/// PNG, which is how the page-turn drawing gets looked at during development.
/// </summary>
public class BookViewTests
{
    [AvaloniaFact]
    public void WideWindow_ShowsSpreads_NarrowWindow_ShowsOnePage()
    {
        var (window, book, _) = Open(1280, 800);
        Assert.True(book.IsSpread);

        window.Width = 420;
        window.Height = 800;
        Pump();
        Assert.False(book.IsSpread);

        window.Close();
    }

    [AvaloniaFact]
    public void Next_TurnsOneSpread_AndUpdatesTheReadingPage()
    {
        var (window, book, model) = Open(1280, 800, animate: false);

        book.Next();
        Assert.Equal(1, model.PageIndex);

        book.Next();
        Assert.Equal(3, model.PageIndex);

        book.Previous();
        Assert.Equal(1, model.PageIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void ContentsLink_JumpsStraightToTheChapter()
    {
        var (window, book, model) = Open(1280, 800, animate: false);

        model.GoToPage(BookContents.ChapterPage(5));
        Pump();

        Assert.Equal(BookContents.ChapterPage(5), model.PageIndex);
        Assert.False(book.IsTurning);
        Snapshot(window, "chapter-spread");

        window.Close();
    }

    [AvaloniaFact]
    public void DraggingThePageEdge_TurnsThePage_AndLettingGoFinishesIt()
    {
        var (window, book, model) = Open(1280, 800);
        model.GoToPage(BookContents.FirstContentsPage);
        Pump();
        WaitForTurn(book);
        Snapshot(window, "contents-spread");

        var layout = BookView.BookLayout.For(book.Bounds.Size, spread: true);
        var offset = book.TranslatePoint(default, window)!.Value;
        var start = offset + new Point(layout.RightPage.Right - 10, layout.RightPage.Bottom - 20);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(-60, -10));
        window.MouseMove(start + new Point(-380, -120));
        Pump();
        Assert.True(book.IsTurning);
        Snapshot(window, "turn-midway");

        window.MouseMove(start + new Point(-760, -60));
        Pump();
        Snapshot(window, "turn-late");

        window.MouseUp(start + new Point(-760, -60), MouseButton.Left);
        WaitForTurn(book);

        Assert.Equal(BookContents.FirstContentsPage + 2, model.PageIndex);
        window.Close();
    }

    [AvaloniaFact]
    public void Cover_AndChineseText_Render()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        try
        {
            localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
            var (window, _, _) = Open(1280, 800);
            Snapshot(window, "cover-zh");

            window.Width = 420;
            Pump();
            Snapshot(window, "cover-zh-phone");
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    private static (Window Window, BookView Book, MainViewModel Model) Open(double width, double height, bool animate = true)
    {
        var settings = new MemorySettingsStore();
        settings.Set(SettingKeys.AnimatesTurns, animate.ToString());
        var model = new MainViewModel(settings);
        var view = new MainView { DataContext = model };
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Pump();

        return (window, view.FindControl<BookView>("Book")!, model);
    }

    private static void WaitForTurn(BookView book)
    {
        // Headless time does not pass by itself; each tick runs one animation frame.
        for (var i = 0; i < 400 && book.IsTurning; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.False(book.IsTurning);
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
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
}
