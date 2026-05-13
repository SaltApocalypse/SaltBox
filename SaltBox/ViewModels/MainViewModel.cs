using CommunityToolkit.Mvvm.ComponentModel;

namespace SaltBox.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _selectedPageTag = "Home";
}
