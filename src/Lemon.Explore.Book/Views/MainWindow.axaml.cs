using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.Views;

/// <summary>Where the window was and how big, saved when it closes.</summary>
public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool Maximized);

public partial class MainWindow : Window
{
    internal const string PlacementSetting = "window";

    private readonly ISettingsStore _settings;

    // The last size and place while neither maximized nor full screen: what "restore" goes back to next time.
    private PixelPoint _normalPosition;
    private Size _normalSize;

    public MainWindow()
        : this(App.Settings)
    {
    }

    public MainWindow(ISettingsStore settings)
    {
        _settings = settings;
        InitializeComponent();
        View.UseAsTitleBar(this);
        Restore();

        PositionChanged += (_, _) => RememberNormalBounds();
        Resized += (_, _) => RememberNormalBounds();
        Opened += OnOpened;
    }

    /// <summary>
    /// After an update, says how it went; then checks for a newer version quietly. Neither waits on the network before
    /// the window shows, and being offline shows nothing.
    /// </summary>
    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel book)
        {
            return;
        }

        await book.Update.ReportLastUpdateAsync();
        await book.Update.CheckOnStartupAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e is { Handled: false, Key: Key.F11 })
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            e.Handled = true;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        RememberNormalBounds();
        if (_normalSize.Width > 0)
        {
            var placement = new WindowPlacement(
                _normalPosition.X, _normalPosition.Y, _normalSize.Width, _normalSize.Height,
                WindowState is WindowState.Maximized or WindowState.FullScreen);
            _settings.Set(PlacementSetting, JsonSerializer.Serialize(placement, BookJson.Default.WindowPlacement));
        }
    }

    private void RememberNormalBounds()
    {
        if (WindowState == WindowState.Normal && IsVisible)
        {
            _normalPosition = Position;
            _normalSize = ClientSize;
        }
    }

    /// <summary>Back where it was last time, as long as that is still on a screen (a monitor may have gone).</summary>
    private void Restore()
    {
        WindowPlacement? placement;
        try
        {
            placement = _settings.Get(PlacementSetting) is { } json
                ? JsonSerializer.Deserialize(json, BookJson.Default.WindowPlacement)
                : null;
        }
        catch (JsonException)
        {
            placement = null;
        }

        if (placement is null || placement.Width < MinWidth || placement.Height < MinHeight)
        {
            return;
        }

        Width = placement.Width;
        Height = placement.Height;
        var position = new PixelPoint(placement.X, placement.Y);
        var onScreen = Screens.All.Any(s => s.WorkingArea.Contains(position + new PixelPoint(40, 20)));
        if (onScreen)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = position;
        }

        if (placement.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }
}
