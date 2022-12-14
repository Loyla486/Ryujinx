using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Ryujinx.Ava.Ui.Controls.Settings;

public partial class SettingsInputView : UserControl
{
    public SettingsInputView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}