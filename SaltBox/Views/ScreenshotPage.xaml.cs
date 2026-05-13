using Microsoft.UI.Xaml.Controls;
using SaltBox.ViewModels;

namespace SaltBox.Views;

public sealed partial class ScreenshotPage : Page
{
    public ScreenshotViewModel ViewModel { get; }

    public ScreenshotPage(ScreenshotViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
