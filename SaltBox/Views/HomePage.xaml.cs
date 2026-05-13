using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private void OnToolsClick(object sender, RoutedEventArgs e)
    {
        // Tools section navigation will be added when modules exist
    }
}
