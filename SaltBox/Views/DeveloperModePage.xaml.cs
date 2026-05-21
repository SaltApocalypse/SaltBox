using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SaltBox.ViewModels;

namespace SaltBox.Views;

public sealed partial class DeveloperModePage : Page
{
    public DeveloperModeViewModel ViewModel { get; }

    public DeveloperModePage(DeveloperModeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.StopRefresh();
    }
}
