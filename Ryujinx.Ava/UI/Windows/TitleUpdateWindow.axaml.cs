using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class TitleUpdateWindow : UserControl
    {
        public TitleUpdateViewModel ViewModel;

        public TitleUpdateWindow()
        {
            DataContext = this;

            InitializeComponent();
        }

        public TitleUpdateWindow(VirtualFileSystem virtualFileSystem, ulong titleId, string titleName)
        {
            DataContext = ViewModel = new TitleUpdateViewModel(virtualFileSystem, titleId, titleName);

            InitializeComponent();
        }

        public static async Task Show(VirtualFileSystem virtualFileSystem, ulong titleId, string titleName)
        {
            ContentDialog contentDialog = new()
            {
                PrimaryButtonText   = "",
                SecondaryButtonText = "",
                CloseButtonText     = "",
                Content             = new TitleUpdateWindow(virtualFileSystem, titleId, titleName),
                Title               = string.Format(LocaleManager.Instance[LocaleKeys.GameUpdateWindowHeading], titleName, titleId.ToString("X16"))
            };

            Style bottomBorder = new(x => x.OfType<Grid>().Name("DialogSpace").Child().OfType<Border>());
            bottomBorder.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(bottomBorder);

            await ContentDialogHelper.ShowAsync(contentDialog);
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            ((ContentDialog)Parent).Hide();
        }

        public void Save(object sender, RoutedEventArgs e)
        {
            ViewModel._titleUpdateWindowData.Paths.Clear();

            ViewModel._titleUpdateWindowData.Selected = "";

            foreach (TitleUpdateModel update in ViewModel.TitleUpdates)
            {
                ViewModel._titleUpdateWindowData.Paths.Add(update.Path);

                if (update == ViewModel.SelectedUpdate)
                {
                    ViewModel._titleUpdateWindowData.Selected = update.Path;
                }
            }

            using (FileStream titleUpdateJsonStream = File.Create(ViewModel._titleUpdateJsonPath, 4096, FileOptions.WriteThrough))
            {
                titleUpdateJsonStream.Write(Encoding.UTF8.GetBytes(JsonHelper.Serialize(ViewModel._titleUpdateWindowData, true)));
            }

            if (VisualRoot is MainWindow window)
            {
                window.ViewModel.LoadApplications();
            }

            ((ContentDialog)Parent).Hide();
        }
    }
}