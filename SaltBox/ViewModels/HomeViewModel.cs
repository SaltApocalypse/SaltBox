using CommunityToolkit.Mvvm.ComponentModel;
using SaltBox.Services;

namespace SaltBox.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    public CultureService Lang { get; }

    public HomeViewModel(CultureService lang)
    {
        Lang = lang;
    }
}
