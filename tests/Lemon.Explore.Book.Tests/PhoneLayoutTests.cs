using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// The book on a phone: one page at a time, about 370 pixels wide. The diagrams were drawn for a wider page, and a
/// layout that clamps between bounds that cross on a narrow one throws while rendering, which takes the whole web
/// edition down. With <c>BOOK_SNAPSHOT_DIR</c> set, every view is saved as phone-&lt;chapter&gt;-&lt;view&gt;-1/2.png.
/// </summary>
public class PhoneLayoutTests
{
    [AvaloniaFact]
    public void EveryViewOfEveryChapter_RendersOnAPhone()
    {
        var (window, model) = OpenPhone();
        var failures = new List<string>();
        foreach (var chapter in BookContents.Chapters)
        {
            var page = TextPageOf(chapter.Slug);
            model.GoToPage(page);
            Frames(5);

            // The chapter's view model (the text page's only property with a Mode enum) and each of its views.
            var text = model.Pages[page];
            var content = text.GetType().GetProperties().Select(p => p.GetValue(text))
                .FirstOrDefault(v => v?.GetType().GetProperty("Mode")?.PropertyType.IsEnum == true);
            var mode = content?.GetType().GetProperty("Mode");
            var views = mode is null ? [null] : Enum.GetValues(mode.PropertyType).Cast<object?>().ToArray();
            foreach (var view in views)
            {
                var name = $"{chapter.Slug}-{view}";
                try
                {
                    if (view is not null) mode!.SetValue(content, view);
                    model.GoToPage(page);
                    Frames(5);
                    Snapshot(window, $"phone-{name}-1");
                    model.GoToPage(page + 1);
                    Frames(15);
                    Snapshot(window, $"phone-{name}-2");
                }
                catch (Exception ex)
                {
                    failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        window.Close();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [AvaloniaFact]
    public void OnAPhone_TheToolbarShowsIcons_AndPagesHaveSlimMargins()
    {
        var (window, model) = OpenPhone();
        var main = (MainView)window.Content!;
        Assert.Contains("narrow", main.Classes);

        // Every toolbar button is on screen, Settings (the last) included.
        var settings = main.FindControl<Button>("SettingsButton")!;
        var right = settings.TranslatePoint(new Point(settings.Bounds.Width, 0), window)!.Value.X;
        Assert.InRange(right, 1, window.Width);

        model.GoToPage(TextPageOf("Lenses") + 1);
        Frames(10);
        var page = main.GetVisualDescendants().OfType<ChapterPageView>().First(v => v.IsEffectivelyVisible);
        var root = (Control)page.Content!;
        Assert.True(root.Margin.Left <= 12 && root.Margin.Right <= 12, root.Margin.ToString());

        // Wide again: the toolbar's words and the book's margins come back.
        window.Width = 1360;
        Frames(10);
        Assert.DoesNotContain("narrow", main.Classes);
        window.Close();
    }

    private static (Window Window, MainViewModel Model) OpenPhone()
    {
        var settings = new MemorySettingsStore();
        settings.Set(SettingKeys.AnimatesTurns, bool.FalseString);
        var model = new MainViewModel(settings);
        var window = new Window { Width = 390, Height = 800, Content = new MainView { DataContext = model } };
        window.Show();
        Frames(10);
        return (window, model);
    }
}
