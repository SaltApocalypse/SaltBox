using Microsoft.UI.Xaml.Controls;

namespace SaltBox.Modules.CustomExplorerActions;

public sealed partial class CustomExplorerActionsPage : Page
{
    public CustomExplorerActionsViewModel ViewModel { get; }

    public CustomExplorerActionsPage(CustomExplorerActionsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
