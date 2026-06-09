using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace SaltBox.Modules.DeveloperMode;

public sealed partial class DeveloperModePage : Page
{
    public DeveloperModeViewModel ViewModel { get; }

    public DeveloperModePage(DeveloperModeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.ScrollToBottomRequested = ScrollToBottom;
        LogScrollViewer.ViewChanged += OnScrollViewerChanged;
    }

    private void OnScrollViewerChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (e.IsIntermediate) return;
        double threshold = 40;
        bool atBottom = LogScrollViewer.ScrollableHeight - LogScrollViewer.VerticalOffset <= threshold;
        ViewModel.IsAtBottom = atBottom;
    }

    private void ScrollToBottom()
    {
        LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.StopRefresh();
    }
}
