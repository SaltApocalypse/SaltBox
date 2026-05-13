using Microsoft.UI.Xaml.Controls;
using SaltBox.ViewModels;

namespace SaltBox.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
