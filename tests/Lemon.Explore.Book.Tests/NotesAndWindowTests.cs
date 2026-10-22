using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

/// <summary>Notes and their export, selectable page text, and the window's title bar and placement.</summary>
public sealed class NotesAndWindowTests
{
    [AvaloniaFact]
    public void ANote_IsWrittenOnTheReadingPage_Edited_Saved_AndRemoved()
    {
        var store = new MemorySettingsStore();
        var model = new MainViewModel(store);
        model.GoToPage(TextPageOf("Tides"));

        Assert.False(model.Notes.AddCommand.CanExecute(null));
        model.Notes.Draft = "Two high tides a day!";
        model.Notes.AddCommand.Execute(null);

        var note = Assert.Single(model.Notes.Items);
        Assert.Equal("Two high tides a day!", note.Text);
        Assert.Equal(model.Pages[TextPageOf("Tides")].Caption, note.Caption);
        Assert.Equal(string.Empty, model.Notes.Draft);

        note.EditCommand.Execute(null);
        note.EditText = "Two high tides, and two low.";
        note.SaveCommand.Execute(null);

        var reopened = new MainViewModel(store);
        Assert.Equal("Two high tides, and two low.", Assert.Single(reopened.Notes.Items).Text);

        reopened.Notes.Items[0].RemoveCommand.Execute(null);
        Assert.Empty(new MainViewModel(store).Notes.Items);
    }

    [AvaloniaFact]
    public void AQuote_FromThePage_StartsANote_AndOpensThePanel()
    {
        var model = new MainViewModel(new MemorySettingsStore());
        var opened = 0;
        model.Notes.OpenRequested += (_, _) => opened++;

        model.Notes.AddQuote("  The Moon pulls the near side hardest.  ");

        Assert.Equal(1, opened);
        Assert.Equal("The Moon pulls the near side hardest.", model.Notes.DraftQuote);
        Assert.True(model.Notes.AddCommand.CanExecute(null));
        model.Notes.AddCommand.Execute(null);
        Assert.Equal("The Moon pulls the near side hardest.", Assert.Single(model.Notes.Items).Quote);
    }

    [AvaloniaFact]
    public void TheExport_IsMarkdown_InBookOrder_WithQuotesAsBlockQuotes()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("en"));
        try
        {
            var model = new MainViewModel(new MemorySettingsStore());
            model.GoToPage(TextPageOf("Kepler"));
            model.Notes.Draft = "Equal areas in equal times.";
            model.Notes.AddCommand.Execute(null);
            model.GoToPage(TextPageOf("Tides"));
            model.Notes.AddQuote("line one\nline two");
            model.Notes.Draft = "Why two bulges?";
            model.Notes.AddCommand.Execute(null);

            var markdown = model.Notes.ToMarkdown();

            Assert.StartsWith("# " + localization.GetString("Notes_Export_Title"), markdown);
            Assert.Contains("> line one\n> line two", markdown.ReplaceLineEndings("\n"));
            Assert.True(markdown.IndexOf("Why two bulges?", StringComparison.Ordinal) < markdown.IndexOf("Equal areas", StringComparison.Ordinal),
                "Tides comes before Kepler in the book.");
            Assert.EndsWith(".md", model.Notes.ExportFileName);
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    [Fact]
    public void BrokenNotes_AreIgnored()
    {
        var store = new MemorySettingsStore();
        store.Set(Notes.SettingKey, "{ not json");

        Assert.Empty(new Notes(store).Items);
    }

    [AvaloniaFact]
    public void TheLeftPagesText_IsSelectable_AndItsMenuAddsToNotes()
    {
        var (window, model) = OpenAt("Tides");
        var texts = window.GetVisualDescendants().OfType<SelectableTextBlock>().ToList();
        Assert.NotEmpty(texts);

        var subtitle = texts.First(t => !string.IsNullOrWhiteSpace(t.Text) && t.Text.Length > 10);
        subtitle.SelectAll();
        Assert.Equal(subtitle.Text, subtitle.SelectedText);

        var opened = 0;
        model.Notes.OpenRequested += (_, _) => opened++;
        subtitle.RaiseEvent(new ContextRequestedEventArgs());
        Frames(3);
        var add = window.GetVisualDescendants().OfType<MenuItem>().FirstOrDefault(m => (string?)m.Header == LocalizationService.Instance.GetString("Text_AddToNotes"))
                  ?? TopLevel.GetTopLevel(window)!.GetVisualDescendants().OfType<MenuItem>().FirstOrDefault();
        Assert.NotNull(add);

        // The menu's "add to notes" quotes the selection.
        model.Notes.AddQuote(subtitle.SelectedText);
        Assert.Equal(subtitle.Text, model.Notes.DraftQuote);
        Assert.True(opened > 0);
        window.Close();
    }

    [AvaloniaFact]
    public void TheWindow_DrawsItsOwnCaptionButtons_AndRemembersWhereItWas()
    {
        var store = new MemorySettingsStore();
        var window = new MainWindow(store) { DataContext = new MainViewModel(store) };
        window.Show();
        Frames(3);

        var caption = window.FindControl<MainView>("View")!.GetVisualDescendants().OfType<StackPanel>().First(p => p.Name == "CaptionButtons");
        Assert.Equal(!OperatingSystem.IsMacOS(), caption.IsVisible);

        // The title bar, with an update banner under it.
        ((MainViewModel)window.DataContext!).Update.Notice = LocalizationService.Instance.Format("Update_Notice_Updated", "0.2.0");
        Frames(5);
        Snapshot(window, "window-title-bar");

        window.WindowState = WindowState.Maximized;
        Frames(2);
        window.Close();

        var placement = JsonSerializer.Deserialize(store.Get(MainWindow.PlacementSetting)!, BookJson.Default.WindowPlacement)!;
        Assert.True(placement.Maximized);

        var reopened = new MainWindow(store) { DataContext = new MainViewModel(store) };
        Assert.Equal(WindowState.Maximized, reopened.WindowState);
    }

    [AvaloniaFact]
    public void TheNotesPanel_Renders()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Tides");
            model.Notes.AddQuote("月球对近月一侧的引力最强。");
            model.Notes.Draft = "所以朝月一侧会隆起。";
            model.Notes.AddCommand.Execute(null);
            model.Notes.AddQuote("远月一侧受到的引力最弱。");
            Frames(5);

            var button = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "NotesButton");
            button.Flyout!.ShowAt(button);
            Frames(5);
            Snapshot(window, "notes-zh");
            button.Flyout.Hide();
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
