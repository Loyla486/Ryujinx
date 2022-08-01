using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.ViewModels;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Input;
using Ryujinx.Input.Assigner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TimeZone = Ryujinx.Ava.Ui.Models.TimeZone;

namespace Ryujinx.Ava.Ui.Windows
{
    public partial class SettingsWindow : StyleableWindow
    {
        private ButtonKeyAssigner _currentAssigner;

        internal SettingsViewModel ViewModel { get; set; }

        public SettingsWindow(VirtualFileSystem virtualFileSystem, ContentManager contentManager)
        {
            Title = $"Ryujinx {Program.Version} - {LocaleManager.Instance["Settings"]}";

            ViewModel = new SettingsViewModel(virtualFileSystem, contentManager, this);
            DataContext = ViewModel;

            InitializeComponent();
            Load();

            FuncMultiValueConverter<string, string> converter = new(parts => string.Format("{0}  {1}   {2}", parts.ToArray()));
            MultiBinding tzMultiBinding = new() { Converter = converter };
            tzMultiBinding.Bindings.Add(new Binding("UtcDifference"));
            tzMultiBinding.Bindings.Add(new Binding("Location"));
            tzMultiBinding.Bindings.Add(new Binding("Abbreviation"));

            TimeZoneBox.ValueMemberBinding = tzMultiBinding;
        }

        public SettingsWindow()
        {
            ViewModel = new SettingsViewModel();
            DataContext = ViewModel;

            InitializeComponent();
            Load();
        }

        private void Load()
        {
            Pages.Children.Clear();
            NavPanel.SelectionChanged += NavPanelOnSelectionChanged;
            NavPanel.SelectedItem = NavPanel.MenuItems.ElementAt(0);
        }

        private void Button_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                if (_currentAssigner != null && button == _currentAssigner.ToggledButton)
                {
                    return;
                }

                if (_currentAssigner == null && (bool)button.IsChecked)
                {
                    _currentAssigner = new ButtonKeyAssigner(button);

                    FocusManager.Instance.Focus(this, NavigationMethod.Pointer);

                    PointerPressed += MouseClick;

                    IKeyboard keyboard = (IKeyboard)ViewModel.AvaloniaKeyboardDriver.GetGamepad(ViewModel.AvaloniaKeyboardDriver.GamepadsIds[0]);
                    IButtonAssigner assigner = new KeyboardKeyAssigner(keyboard);

                    _currentAssigner.GetInputAndAssign(assigner);
                }
                else
                {
                    if (_currentAssigner != null)
                    {
                        ToggleButton oldButton = _currentAssigner.ToggledButton;

                        _currentAssigner.Cancel();
                        _currentAssigner = null;
                        button.IsChecked = false;
                    }
                }
            }
        }

        private void Button_Unchecked(object sender, RoutedEventArgs e)
        {
            _currentAssigner?.Cancel();
            _currentAssigner = null;
        }

        private void MouseClick(object sender, PointerPressedEventArgs e)
        {
            bool shouldUnbind = false;

            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                shouldUnbind = true;
            }

            _currentAssigner?.Cancel(shouldUnbind);

            PointerPressed -= MouseClick;
        }

        private void NavPanelOnSelectionChanged(object sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (e.SelectedItem is NavigationViewItem navitem)
            {
                switch (navitem.Tag.ToString())
                {
                    case "UiPage":
                        NavPanel.Content = UiPage;
                        break;
                    case "InputPage":
                        NavPanel.Content = InputPage;
                        break;
                    case "HotkeysPage":
                        NavPanel.Content = HotkeysPage;
                        break;
                    case "SystemPage":
                        NavPanel.Content = SystemPage;
                        break;
                    case "CpuPage":
                        NavPanel.Content = CpuPage;
                        break;
                    case "GraphicsPage":
                        NavPanel.Content = GraphicsPage;
                        break;
                    case "AudioPage":
                        NavPanel.Content = AudioPage;
                        break;
                    case "NetworkPage":
                        NavPanel.Content = NetworkPage;
                        break;
                    case "LoggingPage":
                        NavPanel.Content = LoggingPage;
                        break;
                }
            }
        }

        private async void AddButton_OnClick(object sender, RoutedEventArgs e)
        {
            string path = PathBox.Text;

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && !ViewModel.GameDirectories.Contains(path))
            {
                ViewModel.GameDirectories.Add(path);
            }
            else
            {
                path = await new OpenFolderDialog().ShowAsync(this);

                if (!string.IsNullOrWhiteSpace(path))
                {
                    ViewModel.GameDirectories.Add(path);
                }
            }
        }

        private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
        {
            List<string> selected = new(GameList.SelectedItems.Cast<string>());

            foreach (string path in selected)
            {
                ViewModel.GameDirectories.Remove(path);
            }
        }

        private void TimeZoneBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is TimeZone timeZone)
                {
                    e.Handled = true;

                    ViewModel.ValidateAndSetTimeZone(timeZone.Location);
                }
            }
        }

        private void TimeZoneBox_OnTextChanged(object sender, EventArgs e)
        {
            if (sender is AutoCompleteBox box)
            {
                if (box.SelectedItem != null && box.SelectedItem is TimeZone timeZone)
                {
                    ViewModel.ValidateAndSetTimeZone(timeZone.Location);
                }
            }
        }

        private async void SaveButton_Clicked(object sender, RoutedEventArgs e)
        {
            await SaveSettings();

            Close();
        }

        private void CloseButton_Clicked(object sender, RoutedEventArgs e)
        {
            ViewModel.RevertIfNotSaved();
            Close();
        }

        private async void ApplyButton_Clicked(object sender, RoutedEventArgs e)
        {
            await SaveSettings();
        }

        private async Task SaveSettings()
        {
            await ViewModel.SaveSettings();

            ControllerSettings?.SaveCurrentProfile();

            if (Owner is MainWindow window)
            {
                window.ViewModel.LoadApplications();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            ControllerSettings.Dispose();
            _currentAssigner?.Cancel();
            _currentAssigner = null;
            base.OnClosed(e);
        }
    }
}