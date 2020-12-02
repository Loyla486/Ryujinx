using Gtk;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using GUI        = Gtk.Builder.ObjectAttribute;
using JsonHelper = Ryujinx.Common.Utilities.JsonHelper;

namespace Ryujinx.Ui
{
    public class DlcWindow : Window
    {
        private readonly VirtualFileSystem  _virtualFileSystem;
        private readonly string             _titleId;
        private readonly string             _dlcJsonPath;
        private readonly List<DlcContainer> _dlcContainerList;

#pragma warning disable CS0649, IDE0044
        [GUI] Label         _baseTitleInfoLabel;
        [GUI] TreeView      _dlcTreeView;
        [GUI] TreeSelection _dlcTreeSelection;
#pragma warning restore CS0649, IDE0044

        public DlcWindow(string titleId, string titleName, VirtualFileSystem virtualFileSystem) : this(new Builder("Ryujinx.Ui.DlcWindow.glade"), titleId, titleName, virtualFileSystem) { }

        private DlcWindow(Builder builder, string titleId, string titleName, VirtualFileSystem virtualFileSystem) : base(builder.GetObject("_dlcWindow").Handle)
        {
            builder.Autoconnect(this);

            _titleId                 = titleId;
            _virtualFileSystem       = virtualFileSystem;
            _dlcJsonPath             = System.IO.Path.Combine(AppDataManager.GamesDirPath, _titleId, "dlc.json");
            _baseTitleInfoLabel.Text = $"DLC Available for {titleName} [{titleId.ToUpper()}]";

            try
            {
                _dlcContainerList = JsonHelper.DeserializeFromFile<List<DlcContainer>>(_dlcJsonPath);
            }
            catch
            {
                _dlcContainerList = new List<DlcContainer>();
            }
            
            _dlcTreeView.Model = new TreeStore(
                typeof(bool),
                typeof(string),
                typeof(string));

            CellRendererToggle enableToggle = new CellRendererToggle();
            enableToggle.Toggled += (sender, args) =>
            {
                _dlcTreeView.Model.GetIter(out TreeIter treeIter, new TreePath(args.Path));
                bool newValue = !(bool)_dlcTreeView.Model.GetValue(treeIter, 0);
                _dlcTreeView.Model.SetValue(treeIter, 0, newValue);

                if (_dlcTreeView.Model.IterChildren(out TreeIter childIter, treeIter))
                {
                    do
                    {
                        _dlcTreeView.Model.SetValue(childIter, 0, newValue);
                    }
                    while (_dlcTreeView.Model.IterNext(ref childIter));
                }
            };

            _dlcTreeView.AppendColumn("Enabled", enableToggle,           "active", 0);
            _dlcTreeView.AppendColumn("TitleId", new CellRendererText(), "text",   1);
            _dlcTreeView.AppendColumn("Path",    new CellRendererText(), "text",   2);

            foreach (DlcContainer dlcContainer in _dlcContainerList)
            {
                TreeIter parentIter = ((TreeStore)_dlcTreeView.Model).AppendValues(false, "", dlcContainer.Path);

                using FileStream containerFile = File.OpenRead(dlcContainer.Path);
                PartitionFileSystem pfs = new PartitionFileSystem(containerFile.AsStorage());
                _virtualFileSystem.ImportTickets(pfs);

                foreach (DlcNca dlcNca in dlcContainer.DlcNcaList)
                {
                    pfs.OpenFile(out IFile ncaFile, dlcNca.Path.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                    Nca nca = TryCreateNca(ncaFile.AsStorage(), dlcContainer.Path);
                    
                    if (nca != null)
                    {
                        ((TreeStore)_dlcTreeView.Model).AppendValues(parentIter, dlcNca.Enabled, nca.Header.TitleId.ToString("X16"), dlcNca.Path);
                    }
                }
            }
        }

        private Nca TryCreateNca(IStorage ncaStorage, string containerPath)
        {
            try
            {
                return new Nca(_virtualFileSystem.KeySet, ncaStorage);
            }
            catch (InvalidDataException exception)
            {
                Logger.Error?.Print(LogClass.Application, $"{exception.Message}. Errored File: {containerPath}");

                GtkDialog.CreateInfoDialog("Ryujinx - Error", "Add DLC Failed!", "The NCA header content type check has failed. This is usually because the header key is incorrect or missing.");
            }
            catch (MissingKeyException exception)
            {
                Logger.Error?.Print(LogClass.Application, $"Your key set is missing a key with the name: {exception.Name}. Errored File: {containerPath}");

                GtkDialog.CreateInfoDialog("Ryujinx - Error", "Add DLC Failed!", $"Your key set is missing a key with the name: {exception.Name}");
            }

            return null;
        }

        private void AddButton_Clicked(object sender, EventArgs args)
        {
            FileChooserDialog fileChooser = new FileChooserDialog("Select DLC files", this, FileChooserAction.Open, "Cancel", ResponseType.Cancel, "Add", ResponseType.Accept)
            {
                SelectMultiple = true,
                Filter         = new FileFilter()
            };
            fileChooser.SetPosition(WindowPosition.Center);
            fileChooser.Filter.AddPattern("*.nsp");

            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                foreach (string containerPath in fileChooser.Filenames)
                {
                    if (!File.Exists(containerPath))
                    {
                        return;
                    }

                    using (FileStream containerFile = File.OpenRead(containerPath))
                    {
                        PartitionFileSystem pfs = new PartitionFileSystem(containerFile.AsStorage());
                        bool containsDlc = false;

                        _virtualFileSystem.ImportTickets(pfs);

                        TreeIter? parentIter = null;

                        foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*.nca"))
                        {
                            pfs.OpenFile(out IFile ncaFile, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                            Nca nca = TryCreateNca(ncaFile.AsStorage(), containerPath);

                            if (nca == null) continue;

                            if (nca.Header.ContentType == NcaContentType.PublicData)
                            {
                                if ((nca.Header.TitleId & 0xFFFFFFFFFFFFE000).ToString("x16") != _titleId)
                                {
                                    break;
                                }

                                parentIter ??= ((TreeStore)_dlcTreeView.Model).AppendValues(true, "", containerPath);

                                ((TreeStore)_dlcTreeView.Model).AppendValues(parentIter.Value, true, nca.Header.TitleId.ToString("X16"), fileEntry.FullPath);
                                containsDlc = true;
                            }
                        }

                        if (!containsDlc)
                        {
                            GtkDialog.CreateErrorDialog("The specified file does not contain a DLC for the selected title!");
                        }
                    }
                }
            }

            fileChooser.Dispose();
        }

        private void RemoveButton_Clicked(object sender, EventArgs args)
        {
            if (_dlcTreeSelection.GetSelected(out ITreeModel treeModel, out TreeIter treeIter))
            {
                if (_dlcTreeView.Model.IterParent(out TreeIter parentIter, treeIter) && _dlcTreeView.Model.IterNChildren(parentIter) <= 1)
                {
                    ((TreeStore)treeModel).Remove(ref parentIter);
                }
                else
                {
                    ((TreeStore)treeModel).Remove(ref treeIter);
                }
            }
        }
        
        private void RemoveAllButton_Clicked(object sender, EventArgs args)
        {
            List<TreeIter> toRemove = new List<TreeIter>();

            if (_dlcTreeView.Model.GetIterFirst(out TreeIter iter))
            {
                do
                {
                    toRemove.Add(iter);
                } 
                while (_dlcTreeView.Model.IterNext(ref iter));
            }

            foreach (TreeIter i in toRemove)
            {
                TreeIter j = i;
                ((TreeStore)_dlcTreeView.Model).Remove(ref j);
            }
        }

        private void SaveButton_Clicked(object sender, EventArgs args)
        {
            _dlcContainerList.Clear();

            if (_dlcTreeView.Model.GetIterFirst(out TreeIter parentIter))
            {
                do
                {
                    if (_dlcTreeView.Model.IterChildren(out TreeIter childIter, parentIter))
                    {
                        DlcContainer dlcContainer = new DlcContainer
                        {
                            Path       = (string)_dlcTreeView.Model.GetValue(parentIter, 2),
                            DlcNcaList = new List<DlcNca>()
                        };

                        do
                        {
                            dlcContainer.DlcNcaList.Add(new DlcNca
                            {
                                Enabled = (bool)_dlcTreeView.Model.GetValue(childIter, 0),
                                TitleId = Convert.ToUInt64(_dlcTreeView.Model.GetValue(childIter, 1).ToString(), 16),
                                Path    = (string)_dlcTreeView.Model.GetValue(childIter, 2)
                            });
                        }
                        while (_dlcTreeView.Model.IterNext(ref childIter));

                        _dlcContainerList.Add(dlcContainer);
                    }
                }
                while (_dlcTreeView.Model.IterNext(ref parentIter));
            }

            using (FileStream dlcJsonStream = File.Create(_dlcJsonPath, 4096, FileOptions.WriteThrough))
            {
                dlcJsonStream.Write(Encoding.UTF8.GetBytes(JsonHelper.Serialize(_dlcContainerList, true)));
            }

            Dispose();
        }

        private void CancelButton_Clicked(object sender, EventArgs args)
        {
            Dispose();
        }
    }
}