using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DynamicData;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS;
using System.IO;
using System.Linq;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class ModManagerViewModel : BaseModel
    {
        private readonly string _modJsonPath;

        private AvaloniaList<ModModel> _mods = new();
        private AvaloniaList<ModModel> _views = new();
        private AvaloniaList<ModModel> _selectedMods = new();

        private string _search;
        private readonly ulong _titleId;
        private readonly IStorageProvider _storageProvider;

        private static readonly ModMetadataJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public AvaloniaList<ModModel> Mods
        {
            get => _mods;
            set
            {
                _mods = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModCount));
                Sort();
            }
        }

        public AvaloniaList<ModModel> Views
        {
            get => _views;
            set
            {
                _views = value;
                OnPropertyChanged();
            }
        }

        public AvaloniaList<ModModel> SelectedMods
        {
            get => _selectedMods;
            set
            {
                _selectedMods = value;
                OnPropertyChanged();
            }
        }

        public string Search
        {
            get => _search;
            set
            {
                _search = value;
                OnPropertyChanged();
                Sort();
            }
        }

        public string ModCount
        {
            get => string.Format(LocaleManager.Instance[LocaleKeys.ModWindowHeading], Mods.Count);
        }

        public ModManagerViewModel(ulong titleId)
        {
            _titleId = titleId;

            _modJsonPath = Path.Combine(AppDataManager.GamesDirPath, titleId.ToString("x16"), "mods.json");

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _storageProvider = desktop.MainWindow.StorageProvider;
            }

            LoadMods(titleId);
        }

        private void LoadMods(ulong titleId)
        {
            Mods.Clear();
            SelectedMods.Clear();

            string modsBasePath = ModLoader.GetModsBasePath();

            var modCache = new ModLoader.ModCache();
            ModLoader.QueryContentsDir(modCache, new DirectoryInfo(Path.Combine(modsBasePath, "contents")), titleId);

            foreach (var mod in modCache.RomfsDirs)
            {
                var modModel = new ModModel(mod.Path.Parent.FullName, mod.Name, mod.Enabled);
                if (Mods.All(x => x.Path != mod.Path.Parent.FullName))
                {
                    Mods.Add(modModel);
                }
            }

            foreach (var mod in modCache.RomfsContainers)
            {
                Mods.Add(new ModModel(mod.Path.FullName, mod.Name, mod.Enabled));
            }

            foreach (var mod in modCache.ExefsDirs)
            {
                var modModel = new ModModel(mod.Path.Parent.FullName, mod.Name, mod.Enabled);
                if (Mods.All(x => x.Path != mod.Path.Parent.FullName))
                {
                    Mods.Add(modModel);
                }
            }

            foreach (var mod in modCache.ExefsContainers)
            {
                Mods.Add(new ModModel(mod.Path.FullName, mod.Name, mod.Enabled));
            }

            SelectedMods = new(Mods.Where(x => x.Enabled));

            Sort();
        }

        public void Sort()
        {
            Mods.AsObservableChangeSet()
                .Filter(Filter)
                .Bind(out var view).AsObservableList();

            _views.Clear();
            _views.AddRange(view);
            OnPropertyChanged(nameof(ModCount));
        }

        private bool Filter(object arg)
        {
            if (arg is ModModel content)
            {
                return string.IsNullOrWhiteSpace(_search) || content.Name.ToLower().Contains(_search.ToLower());
            }

            return false;
        }

        public void Save()
        {
            ModMetadata modData = new();

            foreach (ModModel mod in SelectedMods)
            {
                modData.Mods.Add(new Mod
                {
                    Name = mod.Name,
                    Path = mod.Path,
                    Enabled = mod.Enabled,
                });
            }

            JsonHelper.SerializeToFile(_modJsonPath, modData, _serializerContext.ModMetadata);
        }

        public void Delete(ModModel model)
        {
            Directory.Delete(model.Path, true);

            Mods.Remove(model);
            OnPropertyChanged(nameof(ModCount));
            Sort();
        }

        private void AddMod(DirectoryInfo directory)
        {
            var directories = Directory.GetDirectories(directory.ToString(), "*", SearchOption.AllDirectories);
            var destinationDir = ModLoader.GetTitleDir(ModLoader.GetModsBasePath(), _titleId.ToString("x16"));

            foreach (var dir in directories)
            {
                string dirToCreate = dir.Replace(directory.Parent.ToString(), destinationDir);

                // Mod already exists
                if (Directory.Exists(dirToCreate))
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogLoadModErrorMessage, "Director", dirToCreate));
                    });

                    return;
                }

                Directory.CreateDirectory(dirToCreate);
            }

            var files = Directory.GetFiles(directory.ToString(), "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                File.Copy(file, file.Replace(directory.Parent.ToString(), destinationDir), true);
            }

            LoadMods(_titleId);
        }

        public async void Add()
        {
            var result = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocaleManager.Instance[LocaleKeys.SelectModDialogTitle]
            });

            foreach (var folder in result)
            {
                AddMod(new DirectoryInfo(folder.Path.LocalPath));
            }
        }

        public void DeleteAll()
        {
            foreach (var mod in Mods)
            {
                Directory.Delete(mod.Path, true);
            }

            Mods.Clear();
            OnPropertyChanged(nameof(ModCount));
            Sort();
        }

        public void EnableAll()
        {
            SelectedMods = new(Mods);
        }

        public void DisableAll()
        {
            SelectedMods.Clear();
        }
    }
}
