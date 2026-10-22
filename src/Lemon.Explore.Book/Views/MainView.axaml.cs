using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Lemon.Explore.Book.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // The arrow keys turn pages from the start, without clicking the book first.
        Book.Focus();
    }

    // Turning one step depends on whether one or two pages are showing, which only the book knows.
    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Book.Previous();

    private void OnNextClick(object? sender, RoutedEventArgs e) => Book.Next();
}
