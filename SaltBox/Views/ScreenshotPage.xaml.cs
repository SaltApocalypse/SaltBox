using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SaltBox.Services;
using SaltBox.ViewModels;
using Windows.System;

namespace SaltBox.Views;

public sealed partial class ScreenshotPage : Page
{
    public ScreenshotViewModel ViewModel { get; }
    private readonly ShortcutRegistry _shortcutRegistry;

    public ScreenshotPage(ScreenshotViewModel viewModel, ShortcutRegistry shortcutRegistry)
    {
        ViewModel = viewModel;
        _shortcutRegistry = shortcutRegistry;
        InitializeComponent();
    }

    private void OnSavePathKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            var binding = ((TextBox)sender).GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
        }
    }

    private async void OnModifyShortcut(object sender, RoutedEventArgs e)
    {
        var dialog = new KeyRecorderDialog();
        dialog.SetLanguage(ViewModel.Lang);
        dialog.SetShortcutRegistry(_shortcutRegistry);
        dialog.SetToolName("Screenshot");
        dialog.XamlRoot = XamlRoot;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.UpdateShortcut(dialog.SelectedModifier, dialog.SelectedKey);
        }
    }

    private void OnResetShortcut(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetShortcut();
    }

}
