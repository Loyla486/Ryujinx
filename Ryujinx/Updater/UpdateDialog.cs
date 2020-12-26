using Gdk;
using Gtk;
using Mono.Unix;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ryujinx.Ui
{
    public class UpdateDialog : Gtk.Window
    {
#pragma warning disable CS0649, IDE0044
        [Builder.Object] public Label MainText;
        [Builder.Object] public Label SecondaryText;
        [Builder.Object] public LevelBar ProgressBar;
        [Builder.Object] public Button YesButton;
        [Builder.Object] public Button NoButton;
#pragma warning restore CS0649, IDE0044

        private readonly MainWindow _mainWindow;
        private readonly string _buildUrl;
        private bool _restartQuery;

        public UpdateDialog(MainWindow mainWindow, Version newVersion, string buildUrl) : this(new Builder("Ryujinx.Updater.UpdateDialog.glade"), mainWindow, newVersion, buildUrl) { }

        private UpdateDialog(Builder builder, MainWindow mainWindow, Version newVersion, string buildUrl) : base(builder.GetObject("UpdateDialog").Handle)
        {
            builder.Autoconnect(this);

            _mainWindow = mainWindow;
            _buildUrl   = buildUrl;

            MainText.Text      = "Do you want to update Ryujinx to the latest version?";
            SecondaryText.Text = $"{Program.Version} -> {newVersion}";

            ProgressBar.Hide();

            YesButton.Clicked += YesButton_Pressed;
            NoButton.Clicked  += NoButton_Pressed;
        }
        
        private void YesButton_Pressed(object sender, EventArgs args)
        {
            if (_restartQuery)
            {
                string ryuName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Ryujinx.exe" : "Ryujinx";
                string ryuExe  = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ryuName);
                string ryuArg = String.Join(" ", Environment.GetCommandLineArgs().AsEnumerable().Skip(1).ToArray());

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    UnixFileInfo unixFileInfo = new UnixFileInfo(ryuExe);
                    unixFileInfo.FileAccessPermissions |= FileAccessPermissions.UserExecute;
                }

                Process.Start(ryuExe, ryuArg);

                Environment.Exit(0);
            }
            else
            {
                this.Window.Functions = _mainWindow.Window.Functions = WMFunction.All & WMFunction.Close;
                _mainWindow.ExitMenuItem.Sensitive = false;

                YesButton.Hide();
                NoButton.Hide();
                ProgressBar.Show();

                SecondaryText.Text = "";
                _restartQuery      = true;

                _ = Updater.UpdateRyujinx(this, _buildUrl);
            }
        }

        private void NoButton_Pressed(object sender, EventArgs args)
        {
            Updater.Running = false;
            _mainWindow.Window.Functions = WMFunction.All;

            _mainWindow.ExitMenuItem.Sensitive   = true;
            _mainWindow.UpdateMenuItem.Sensitive = true;

            this.Dispose();
        }
    }
}
