using Gtk;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using Ryujinx.Ui.App.Common;
using Ryujinx.Ui.Common.Configuration;
using Ryujinx.Ui.Widgets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GUI = Gtk.Builder.ObjectAttribute;
using SpanHelpers = LibHac.Common.SpanHelpers;

namespace Ryujinx.Ui.Windows
{
    public class TitleUpdateWindow : Window
    {
        private readonly MainWindow _parent;
        private readonly VirtualFileSystem _virtualFileSystem;
        private readonly ApplicationData _title;
        private readonly string _updateJsonPath;

        private TitleUpdateMetadata _titleUpdateWindowData;

        private readonly Dictionary<RadioButton, string> _radioButtonToPathDictionary;
        private static readonly TitleUpdateMetadataJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

#pragma warning disable CS0649, IDE0044 // Field is never assigned to, Add readonly modifier
        [GUI] Label _baseTitleInfoLabel;
        [GUI] Box _availableUpdatesBox;
        [GUI] RadioButton _noUpdateRadioButton;
#pragma warning restore CS0649, IDE0044

        public TitleUpdateWindow(MainWindow parent, VirtualFileSystem virtualFileSystem, ApplicationData applicationData) : this(new Builder("Ryujinx.Ui.Windows.TitleUpdateWindow.glade"), parent, virtualFileSystem, applicationData) { }

        private TitleUpdateWindow(Builder builder, MainWindow parent, VirtualFileSystem virtualFileSystem, ApplicationData applicationData) : base(builder.GetRawOwnedObject("_titleUpdateWindow"))
        {
            _parent = parent;

            builder.Autoconnect(this);

            _title = applicationData;
            _virtualFileSystem = virtualFileSystem;
            _updateJsonPath = System.IO.Path.Combine(AppDataManager.GamesDirPath, applicationData.IdString, "updates.json");
            _radioButtonToPathDictionary = new Dictionary<RadioButton, string>();

            try
            {
                _titleUpdateWindowData = JsonHelper.DeserializeFromFile(_updateJsonPath, _serializerContext.TitleUpdateMetadata);
            }
            catch
            {
                _titleUpdateWindowData = new TitleUpdateMetadata
                {
                    Selected = "",
                    Paths = new List<string>(),
                };
            }

            _baseTitleInfoLabel.Text = $"Updates Available for {applicationData.Name} [{applicationData.IdString}]";

            // Try to get updates from PFS first
            AddUpdate(_title.Path, true);

            foreach (string path in _titleUpdateWindowData.Paths)
            {
                AddUpdate(path);
            }

            if (_titleUpdateWindowData.Selected == "")
            {
                _noUpdateRadioButton.Active = true;
            }
            else
            {
                foreach ((RadioButton update, var _) in _radioButtonToPathDictionary.Where(keyValuePair => keyValuePair.Value == _titleUpdateWindowData.Selected))
                {
                    update.Active = true;
                }
            }
        }

        private void AddUpdate(string path, bool ignoreNotFound = false)
        {
            if (File.Exists(path))
            {
                IntegrityCheckLevel checkLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks
                    ? IntegrityCheckLevel.ErrorOnInvalid
                    : IntegrityCheckLevel.None;

                using FileStream file = new(path, FileMode.Open, FileAccess.Read);

                IFileSystem pfs;

                try
                {
                    if (System.IO.Path.GetExtension(path).ToLower() == ".xci")
                    {
                        pfs = new Xci(_virtualFileSystem.KeySet, file.AsStorage()).OpenPartition(XciPartitionType.Secure);
                    }
                    else
                    {
                        var pfsTemp = new PartitionFileSystem();
                        pfsTemp.Initialize(file.AsStorage()).ThrowIfFailure();
                        pfs = pfsTemp;
                    }

                    Dictionary<ulong, ContentCollection> updates = pfs.GetUpdateData(_virtualFileSystem, checkLevel);

                    Nca patchNca = null;
                    Nca controlNca = null;

                    if (updates.TryGetValue(_title.Id, out ContentCollection update))
                    {
                        patchNca = update.GetNcaByType(_virtualFileSystem.KeySet, LibHac.Ncm.ContentType.Program);
                        controlNca = update.GetNcaByType(_virtualFileSystem.KeySet, LibHac.Ncm.ContentType.Control);
                    }

                    if (controlNca != null && patchNca != null)
                    {
                        ApplicationControlProperty controlData = new();

                        using var nacpFile = new UniqueRef<IFile>();

                        controlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None).OpenFile(ref nacpFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();
                        nacpFile.Get.Read(out _, 0, SpanHelpers.AsByteSpan(ref controlData), ReadOption.None).ThrowIfFailure();

                        string radioLabel = $"Version {controlData.DisplayVersionString.ToString()} - {path}";

                        if (System.IO.Path.GetExtension(path).ToLower() == ".xci")
                        {
                            radioLabel = "Bundled: " + radioLabel;
                        }

                        RadioButton radioButton = new(radioLabel);
                        radioButton.JoinGroup(_noUpdateRadioButton);

                        _availableUpdatesBox.Add(radioButton);
                        _radioButtonToPathDictionary.Add(radioButton, path);

                        radioButton.Show();
                        radioButton.Active = true;
                    }
                    else
                    {
                        if (!ignoreNotFound)
                        {
                            GtkDialog.CreateErrorDialog("The specified file does not contain an update for the selected title!");
                        }
                    }
                }
                catch (Exception exception)
                {
                    GtkDialog.CreateErrorDialog($"{exception.Message}. Errored File: {path}");
                }
            }
        }

        private void RemoveUpdates(bool removeSelectedOnly = false)
        {
            foreach (RadioButton radioButton in _noUpdateRadioButton.Group)
            {
                if (radioButton.Label != "No Update" && (!removeSelectedOnly || radioButton.Active))
                {
                    _availableUpdatesBox.Remove(radioButton);
                    _radioButtonToPathDictionary.Remove(radioButton);
                    radioButton.Dispose();
                }
            }
        }

        private void AddButton_Clicked(object sender, EventArgs args)
        {
            using FileChooserNative fileChooser = new("Select update files", this, FileChooserAction.Open, "Add", "Cancel");

            fileChooser.SelectMultiple = true;

            FileFilter filter = new()
            {
                Name = "Switch Game Updates",
            };
            filter.AddPattern("*.nsp");

            fileChooser.AddFilter(filter);

            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                foreach (string path in fileChooser.Filenames)
                {
                    AddUpdate(path);
                }
            }
        }

        private void RemoveButton_Clicked(object sender, EventArgs args)
        {
            RemoveUpdates(true);
        }

        private void RemoveAllButton_Clicked(object sender, EventArgs args)
        {
            RemoveUpdates();
        }

        private void SaveButton_Clicked(object sender, EventArgs args)
        {
            _titleUpdateWindowData.Paths.Clear();
            _titleUpdateWindowData.Selected = "";

            foreach (string paths in _radioButtonToPathDictionary.Values)
            {
                _titleUpdateWindowData.Paths.Add(paths);
            }

            foreach (RadioButton radioButton in _noUpdateRadioButton.Group)
            {
                if (radioButton.Active)
                {
                    _titleUpdateWindowData.Selected = _radioButtonToPathDictionary.TryGetValue(radioButton, out string updatePath) ? updatePath : "";
                }
            }

            JsonHelper.SerializeToFile(_updateJsonPath, _titleUpdateWindowData, _serializerContext.TitleUpdateMetadata);

            _parent.UpdateGameTable();

            Dispose();
        }

        private void CancelButton_Clicked(object sender, EventArgs args)
        {
            Dispose();
        }
    }
}
