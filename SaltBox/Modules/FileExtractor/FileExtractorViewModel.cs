using CommunityToolkit.Mvvm.ComponentModel;
using SaltBox.Services;

namespace SaltBox.Modules.FileExtractor;

public partial class FileExtractorViewModel : ObservableObject
{
    private readonly FileExtractorService _fileExtractorService;

    public CultureService Lang { get; }

    [ObservableProperty]
    private bool _isEnabled;

    public FileExtractorViewModel(CultureService lang, FileExtractorService fileExtractorService)
    {
        Lang = lang;
        _fileExtractorService = fileExtractorService;

        _isEnabled = _fileExtractorService.IsEnabled;
        OnPropertyChanged(nameof(IsEnabled));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _fileExtractorService.IsEnabled = value;
    }
}
