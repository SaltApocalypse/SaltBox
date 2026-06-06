using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SaltBox.ViewModels;

namespace SaltBox.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage(HomeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void OnUpdateBannerTapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.NavigateToTool?.Invoke("Settings");
    }
}
