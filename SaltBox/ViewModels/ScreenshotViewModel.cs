using CommunityToolkit.Mvvm.ComponentModel;
using SaltBox.Services;

namespace SaltBox.ViewModels;

public partial class ScreenshotViewModel : ObservableObject
{
    public CultureService Lang { get; }

    public ScreenshotViewModel(CultureService lang)
    {
        Lang = lang;
    }
}
