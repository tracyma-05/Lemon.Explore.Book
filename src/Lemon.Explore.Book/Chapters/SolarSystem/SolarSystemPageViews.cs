using Avalonia;
using Avalonia.Interactivity;
using Lemon.Explore.Book.Controls.Explore;

namespace Lemon.Explore.Book.Chapters.SolarSystem;

public partial class SolarSystemTextPageView : ChapterPageView
{
    public SolarSystemTextPageView()
    {
        InitializeComponent();
    }
}

public partial class SolarSystemDiagramPageView : ChapterPageView
{
    private SolarSystemViewModel? _solar;

    public SolarSystemDiagramPageView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The camera belongs to the scene; the tour asks for the whole system through the view model.
        _solar = (DataContext as SolarSystemDiagramPageViewModel)?.Solar;
        if (_solar is not null)
        {
            _solar.ViewResetRequested += OnViewResetRequested;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_solar is not null)
        {
            _solar.ViewResetRequested -= OnViewResetRequested;
            _solar = null;
        }
    }

    private void OnViewResetRequested(object? sender, EventArgs e) => Scene.ResetView();

    private void OnResetViewClick(object? sender, RoutedEventArgs e) => Scene.ResetView();
}
