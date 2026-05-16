using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SaltBox.Controls;

public sealed partial class KeyCap : UserControl
{
    public static readonly DependencyProperty KeyTextProperty =
        DependencyProperty.Register(nameof(KeyText), typeof(string), typeof(KeyCap), new PropertyMetadata(""));

    public string KeyText
    {
        get => (string)GetValue(KeyTextProperty);
        set => SetValue(KeyTextProperty, value);
    }

    public KeyCap()
    {
        InitializeComponent();
    }
}
