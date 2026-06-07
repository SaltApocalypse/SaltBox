using Microsoft.UI.Xaml.Controls;

namespace SaltBox.Modules.FileExtractor;

public sealed partial class FileExtractorPage : Page
{
    public FileExtractorViewModel ViewModel { get; }

    public FileExtractorPage(FileExtractorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
