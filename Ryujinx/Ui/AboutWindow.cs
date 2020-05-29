﻿using Gtk;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

using GUI = Gtk.Builder.ObjectAttribute;

namespace Ryujinx.Ui
{
    public class AboutWindow : Window
    {
#pragma warning disable CS0649
#pragma warning disable IDE0044
        [GUI] Label  _versionText;
        [GUI] Image  _ryujinxLogo;
        [GUI] Image  _patreonLogo;
        [GUI] Image  _gitHubLogo;
        [GUI] Image  _discordLogo;
        [GUI] Image  _twitterLogo;
#pragma warning restore CS0649
#pragma warning restore IDE0044

        public AboutWindow() : this(new Builder("Ryujinx.Ui.AboutWindow.glade")) { }

        private AboutWindow(Builder builder) : base(builder.GetObject("_aboutWin").Handle)
        {
            builder.Autoconnect(this);

            this.Icon           = new Gdk.Pixbuf(Assembly.GetExecutingAssembly(), "Ryujinx.Ui.assets.Icon.png");
            _ryujinxLogo.Pixbuf = new Gdk.Pixbuf(Assembly.GetExecutingAssembly(), "Ryujinx.Ui.assets.Icon.png"       , 100, 100);
            _patreonLogo.Pixbuf = new Gdk.Pixbuf(Assembly.GetExecutingAssembly(), "Ryujinx.Ui.assets.PatreonLogo.png", 30 , 30 );
            _gitHubLogo.Pixbuf  = new Gdk.Pixbuf(Assembly.GetExecutingAssembly(), "Ryujinx.Ui.assets.GitHubLogo.png" , 30 , 30 );
            _discordLogo.Pixbuf = new Gdk.Pixbuf(Assembly.GetExecutingAssembly(), "Ryujinx.Ui.assets.DiscordLogo.png", 30 , 30 );
            _twitterLogo.Pixbuf = new Gdk.Pixbuf(Assembly.GetExecutingAssembly(), "Ryujinx.Ui.assets.TwitterLogo.png", 30 , 30 );

            _versionText.Text = Program.Version;
        }

        private static void OpenUrl(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }

        //Events
        private void RyujinxButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenUrl("https://ryujinx.org");
        }

        private void PatreonButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenUrl("https://www.patreon.com/ryujinx");
        }

        private void GitHubButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenUrl("https://github.com/Ryujinx/Ryujinx");
        }

        private void DiscordButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenUrl("https://discordapp.com/invite/N2FmfVc");
        }

        private void TwitterButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenUrl("https://twitter.com/RyujinxEmu");
        }

        private void ContributorsButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenUrl("https://github.com/Ryujinx/Ryujinx/graphs/contributors?type=a");
        }

        private void CloseToggle_Activated(object sender, EventArgs args)
        {
            Dispose();
        }
    }
}
