using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaltBox.Services;
using SaltBox.Services.ExplorerIntegration;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SaltBox.Modules.CustomExplorerActions;

public partial class CustomExplorerActionsViewModel : ObservableObject
{
    private readonly ExplorerActionManager _actionManager;
    private readonly MainWindow _mainWindow;
    private readonly CultureService _lang;
    public CultureService Lang { get; }

    [ObservableProperty]
    private bool _isEnabled;

    public ObservableCollection<ActionItemViewModel> Actions { get; } = new();

    public CustomExplorerActionsViewModel(
        CultureService lang,
        ExplorerActionManager actionManager,
        MainWindow mainWindow)
    {
        _lang = lang;
        Lang = lang;
        _actionManager = actionManager;
        _mainWindow = mainWindow;

        _isEnabled = _actionManager.IsCustomActionsEnabled;
        LoadFromConfig();
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _actionManager.SetCustomActionsEnabled(value);
    }

    [RelayCommand]
    private async Task AddAction()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(_mainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null)
            return;

        var appName = System.IO.Path.GetFileNameWithoutExtension(file.Path);
        var item = new ExplorerActionItem
        {
            Id = $"SaltBox.BB.{Guid.NewGuid():N}",
            DisplayName = appName,
            CommandPath = file.Path,
            Target = ExplorerTarget.File,
            IsEnabled = true,
            SortOrder = Actions.Count,
        };

        _actionManager.AddCustomAction(item);
        Actions.Add(new ActionItemViewModel(item, this, _lang));
    }

    public void RemoveAction(ActionItemViewModel vm)
    {
        _actionManager.RemoveCustomAction(vm.Id);
        Actions.Remove(vm);
    }

    public void OnActionChanged(ActionItemViewModel vm)
    {
        vm.SyncToItem();
        _actionManager.RefreshCustomAction(vm.Item);
    }

    private void LoadFromConfig()
    {
        var actions = _actionManager.GetCustomActions();
        foreach (var item in actions.OrderBy(a => a.SortOrder))
            Actions.Add(new ActionItemViewModel(item, this, _lang));
    }
}

public partial class ActionItemViewModel : ObservableObject
{
    private readonly CustomExplorerActionsViewModel _parent;

    [ObservableProperty]
    private string _alias;

    [ObservableProperty]
    private bool _isEnabled;

    public ExplorerActionItem Item { get; }
    public CultureService Lang { get; }

    public string Id => Item.Id;
    public string CommandPath => Item.CommandPath;
    public string AppName => System.IO.Path.GetFileNameWithoutExtension(Item.CommandPath);

    public string DisplayHeader =>
        string.IsNullOrEmpty(Alias) ? AppName : $"{Alias} ({AppName})";

    public ActionItemViewModel(ExplorerActionItem item, CustomExplorerActionsViewModel parent, CultureService lang)
    {
        Item = item;
        _parent = parent;
        Lang = lang;
        _alias = item.DisplayName != AppName ? item.DisplayName : "";
        _isEnabled = item.IsEnabled;
    }

    [RelayCommand]
    private void Delete()
    {
        _parent.RemoveAction(this);
    }

    partial void OnAliasChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayHeader));
        SyncToItem();
        _parent.OnActionChanged(this);
    }

    partial void OnIsEnabledChanged(bool value)
    {
        SyncToItem();
        _parent.OnActionChanged(this);
    }

    public void SyncToItem()
    {
        Item.DisplayName = string.IsNullOrWhiteSpace(Alias) ? AppName : Alias;
        Item.IsEnabled = IsEnabled;
    }
}
