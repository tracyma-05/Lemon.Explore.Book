using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.ViewModels;

namespace Lemon.Explore.Book.Views;

public partial class MainView : UserControl
{
    private Window? _window;

    private NotesViewModel? _notes;

    public MainView()
    {
        InitializeComponent();

        // Page text is selectable: right-click (or long-press) it for the book's own menu.
        AddHandler(ContextRequestedEvent, OnContextRequested);

        // The book handles the keys itself while it has focus; this keeps them turning pages after a toolbar button
        // has taken focus. Flyouts (settings, notes) are popups, so their keys never bubble up here.
        AddHandler(KeyDownEvent, OnTurnKey);

        // On a phone the toolbar's words do not fit: below this width it shows only the icons (App.axaml, .narrow).
        SizeChanged += (_, e) => Classes.Set("narrow", e.NewSize.Width < NarrowWidth);
    }

    /// <summary>Narrower than this, the toolbar shows its buttons' icons only.</summary>
    internal const double NarrowWidth = 640;

    private void OnTurnKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is TextBox or Slider or ComboBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Right or Key.PageDown:
                Book.Next();
                break;
            case Key.Left or Key.PageUp:
                Book.Previous();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_notes is not null) _notes.OpenRequested -= OnOpenNotes;
        _notes = (DataContext as MainViewModel)?.Notes;
        if (_notes is not null) _notes.OpenRequested += OnOpenNotes;
    }

    /// <summary>"Add to notes" on selected text: open the notes panel, ready to write.</summary>
    private void OnOpenNotes(object? sender, EventArgs e)
    {
        NotesButton.Flyout?.ShowAt(NotesButton);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => NotesPanel.FocusDraft(), Avalonia.Threading.DispatcherPriority.Input);
    }

    /// <summary>
    /// Copy, select all, and add to notes, for text selected on a page. With nothing selected, copy and the note take
    /// the whole paragraph that was clicked.
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Source is not Control source || FindText(source) is not { } text || _notes is null)
        {
            return;
        }

        var localization = LocalizationService.Instance;
        string Chosen() => string.IsNullOrEmpty(text.SelectedText) ? text.Text ?? string.Empty : text.SelectedText;

        var copy = new MenuItem { Header = localization.GetString("Text_Copy"), InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) };
        copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(Chosen());
        };
        var selectAll = new MenuItem { Header = localization.GetString("Text_SelectAll") };
        selectAll.Click += (_, _) => text.SelectAll();
        var addToNotes = new MenuItem { Header = localization.GetString("Text_AddToNotes") };
        addToNotes.Click += (_, _) => _notes.AddQuote(Chosen());

        var menu = new MenuFlyout { Items = { copy, selectAll, new Separator(), addToNotes } };
        menu.ShowAt(text, showAtPointer: true);
        e.Handled = true;
    }

    private static SelectableTextBlock? FindText(Control control)
    {
        for (Visual? visual = control; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is SelectableTextBlock text) return text;
        }

        return null;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // The arrow keys turn pages from the start, without clicking the book first.
        Book.Focus();
    }

    /// <summary>
    /// Makes the toolbar the title bar of <paramref name="window"/>: on macOS the system's traffic lights sit at its
    /// left; elsewhere the book draws minimize, maximize and close at its right.
    /// </summary>
    internal void UseAsTitleBar(Window window)
    {
        _window = window;
        if (OperatingSystem.IsMacOS())
        {
            TitleText.Margin = new Thickness(70, 0, 0, 0);
        }
        else
        {
            CaptionButtons.IsVisible = true;
        }

        window.PropertyChanged += (_, change) =>
        {
            if (change.Property == Window.WindowStateProperty)
            {
                UpdateMaximizeIcon();
            }
        };
        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        var maximized = _window?.WindowState == WindowState.Maximized;
        if (this.TryFindResource(maximized ? "Icon.WindowRestore" : "Icon.WindowMaximize", out var icon) && icon is Geometry geometry)
        {
            MaximizeIcon.Data = geometry;
        }
    }

    // Turning one step depends on whether one or two pages are showing, which only the book knows.
    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Book.Previous();

    private void OnNextClick(object? sender, RoutedEventArgs e) => Book.Next();

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (_window is not null) _window.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _window?.Close();
}
