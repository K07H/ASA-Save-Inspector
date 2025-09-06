using ASA_Save_Inspector.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ASA_Save_Inspector.Pages
{
    public class JsonExportProfile
    {
        public int ID { get; set; } = -1;
        public string SaveFilePath { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public bool ExtractedDinos { get; set; } = false;
        public bool ExtractedPlayerPawns { get; set; } = false;
        public bool ExtractedItems { get; set; } = false;
        public bool ExtractedStructures { get; set; } = false;
        public bool ExtractedPlayers { get; set; } = false;
        public bool ExtractedTribes { get; set; } = false;
        public bool FastExtract { get; set; } = false;

        public string GetLabel() => $"{ID.ToString(CultureInfo.InvariantCulture)} - {MapName} ({CreationDate.ToString("yyyy-MM-dd HH\\hmm\\mss\\s")})";
        public string GetExportFolderName() => (Directory.Exists(SaveFilePath) ? $"{SaveFilePath}" : $"{ID.ToString(CultureInfo.InvariantCulture)}_{MapName}");
        public override string ToString() => $"Map: {(MapName ?? "")}, FilePath: \"{(SaveFilePath ?? "")}\", Extract: {(ExtractedDinos ? "dinos," : "")}{(ExtractedPlayerPawns ? "pawns," : "")}{(ExtractedItems ? "items," : "")}{(ExtractedStructures ? "structures," : "")}{(ExtractedPlayers ? "players," : "")}{(ExtractedTribes ? "tribes," : "")}";
    }

    public class JsonExportPreset
    {
        public string Name { get; set; } = string.Empty;
        public List<JsonExportProfile> ExportProfiles { get; set; } = new List<JsonExportProfile>();
    }

    public class JsonSettings
    {
        public SettingsPage.ASILanguage Language { get; set; } = SettingsPage.ASILanguage.ENGLISH;
        public string? PythonExeFilePath { get; set; } = string.Empty;
        public bool? DarkTheme { get; set; } = true;
    }

    public class MapBounds
    {
        public double OriginMinX { get; set; } = 0.0d;
        public double OriginMinY { get; set; } = 0.0d;
        public double OriginMinZ { get; set; } = 0.0d;

        public double OriginMaxX { get; set; } = 0.0d;
        public double OriginMaxY { get; set; } = 0.0d;
        public double OriginMaxZ { get; set; } = 0.0d;

        public double PlayableMinX { get; set; } = 0.0d;
        public double PlayableMinY { get; set; } = 0.0d;
        public double PlayableMinZ { get; set; } = 0.0d;

        public double PlayableMaxX { get; set; } = 0.0d;
        public double PlayableMaxY { get; set; } = 0.0d;
        public double PlayableMaxZ { get; set; } = 0.0d;
    }

    public class ArkMapInfo
    {
        public string MapName { get; set; } = "Unknown";
        public string MinimapFilename { get; set; } = "TheIsland_Minimap_Margin.jpg";
        public MapBounds Bounds { get; set; } = new MapBounds()
        {
            OriginMinX = -342900.0,
            OriginMinY = -342900.0,
            OriginMinZ = -15000.0,
            OriginMaxX = 342900.0,
            OriginMaxY = 342900.0,
            OriginMaxZ = 54695.0,
            PlayableMinX = -342900.0,
            PlayableMinY = -342900.0,
            PlayableMinZ = -15000.0,
            PlayableMaxX = 342900.0,
            PlayableMaxY = 342900.0,
            PlayableMaxZ = 54695.0
        };
    }

    public class SaveFileInfo
    {
        public string? MapName { get; set; } = null;
        public double? GameTime { get; set; } = null;
        public int? CurrentDay { get; set; } = null;
        public double? CurrentTime { get; set; } = null;
        public DateTime? SaveDateTime { get; set; } = null;
    }

    public class CustomBlueprints
    {
        public List<string> Dinos { get; set; } = new List<string>();
        public List<string> Items { get; set; } = new List<string>();
        public List<string> Structures { get; set; } = new List<string>();
    }

    public sealed partial class SettingsPage : Page, INotifyPropertyChanged
    {
        public enum ASILanguage
        {
            ENGLISH = 0,
            FRENCH = 1
        }

        private static readonly object lockObject = new object();

        public static SettingsPage? _page = null;
        public static bool? _darkTheme = true;

        public static ASILanguage? _language = null;
        public static string? _pythonExePath = null;
        public static string? _jsonDataFolderPath = null;
        public static string? _asaSaveFilePath = null;
        public static string? _mapName = null;
        //private static bool _bindedWindowResize = false;

        public static List<JsonExportProfile> _jsonExportProfiles = new List<JsonExportProfile>();
        public static JsonExportProfile? _selectedJsonExportProfile = null;

        public static List<JsonExportPreset> _jsonExportPresets = new List<JsonExportPreset>();
        
        public static List<Dino>? _dinosData = null;
        public static List<PlayerPawn>? _playerPawnsData = null;
        public static List<Item>? _itemsData = null;
        public static List<Structure>? _structuresData = null;
        public static List<Player>? _playersData = null;
        public static List<Tribe>? _tribesData = null;
        public static SaveFileInfo? _saveFileData = null;

        public static string? _currentlyLoadedMapName = null;

        public static CustomBlueprints _customBlueprints = new CustomBlueprints();

        private BitmapImage _flagEnglish = new BitmapImage(new Uri("ms-appx:///Assets/FlagGBIcon96.png", UriKind.Absolute));
        private BitmapImage _flagFrench = new BitmapImage(new Uri("ms-appx:///Assets/FlagFRIcon96.png", UriKind.Absolute));

        private SolidColorBrush _errorColor = new SolidColorBrush(Colors.Red);
        private SolidColorBrush _warningColor = new SolidColorBrush(Colors.Orange);
        private SolidColorBrush _successColor = new SolidColorBrush(Colors.Green);

        public event PropertyChangedEventHandler? PropertyChanged;

        public SettingsPage()
        {
            InitializeComponent();

            // Ensure previous page is garbage collected then bind current page to _page variable (thread-safe).
            lock (lockObject)
            {
                if (_page != null)
                    _page = null;
                _page = this;
            }

            // Calculate page center.
            AdjustToSizeChange();

            tb_ExtractJsonResult.Visibility = Visibility.Collapsed;
            RefreshExtractProfilePresetsPopup();

            List<string>? pythonPaths = PythonManager.GetPythonExePaths();
            if (pythonPaths != null && pythonPaths.Count > 0)
                foreach (string pythonPath in pythonPaths)
                    if (!string.IsNullOrWhiteSpace(pythonPath))
                        mf_PythonSelect.Items.Add(new MenuFlyoutItem
                        {
                            Text = pythonPath,
                            Command = PythonSelectPathCommand,
                            CommandParameter = pythonPath
                        });

            mf_PythonSelect.Items.Add(new MenuFlyoutItem
            {
                Text = "Manual selection",
                Command = PythonSelectPathCommand,
                CommandParameter = "MANUAL"
            });

            foreach (var mapInfo in Utils._allMaps)
                if (mapInfo != null && !string.IsNullOrWhiteSpace(mapInfo.MapName))
                {
                    var newMenuItem = new MenuFlyoutItem();
                    newMenuItem.Text = mapInfo.MapName;
                    newMenuItem.Click += (s, e1) =>
                    {
                        _mapName = mapInfo.MapName;
                        MapNameChanged();
                    };
                    mf_MapNameSlect.Items.Add(newMenuItem);

                    var newFolderMenuItem = new MenuFlyoutItem();
                    newFolderMenuItem.Text = mapInfo.MapName;
                    newFolderMenuItem.Click += (s, e1) =>
                    {
                        _mapName = mapInfo.MapName;
                        FolderMapNameChanged();
                    };
                    mf_FolderMapNameSlect.Items.Add(newFolderMenuItem);
                }

            LoadJsonExportProfiles();
            if (_jsonExportProfiles != null && _jsonExportProfiles.Count > 0)
                foreach (var jsonProfile in _jsonExportProfiles)
                    if (jsonProfile != null)
                        AddJsonExportProfileToDropDown(jsonProfile);

            LoadSettings();
            LoadJsonExportPresets();
            LoadCustomBlueprints();
            AsaSaveFilePathChanged();
            MapNameChanged();
            if (_selectedJsonExportProfile != null)
                JsonExportProfileSelected(_selectedJsonExportProfile);

            /*
            if (!_bindedWindowResize && App._window != null)
            {
                _bindedWindowResize = true;
                App._window.SizeChanged += _window_SizeChanged;
            }
            */
        }

        private int _windowWidth = 0;
        public int WindowWidth
        {
            get { return _windowWidth; }
            set { _windowWidth = value; OnPropertyChanged(); }
        }
        private int _windowHeight = 0;
        public int WindowHeight
        {
            get { return _windowHeight; }
            set { _windowHeight = value; OnPropertyChanged(); }
        }

        private void AdjustToSizeChange()
        {
            if (MainWindow._mainWindow != null)
            {
                NavigationViewDisplayMode displayMode = NavigationViewDisplayMode.Expanded;
                if (MainWindow._mainWindow._navView != null)
                    displayMode = MainWindow._mainWindow._navView.DisplayMode;
                WindowWidth = Math.Max(1, Convert.ToInt32(Math.Round(MainWindow._mainWindow.Bounds.Width)) - (displayMode == NavigationViewDisplayMode.Minimal ? 2 : (displayMode == NavigationViewDisplayMode.Compact ? 52 : 164)));
                WindowHeight = Math.Max(1, Convert.ToInt32(Math.Round(MainWindow._mainWindow.Bounds.Height)) - 52);
            }
        }

        private void page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustToSizeChange();
        }

#pragma warning disable CS8625
        public void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
#pragma warning restore CS8625

        private ASILanguage GetLanguageFromID(int? id)
        {
            if (id == null || !id.HasValue)
                return ASILanguage.ENGLISH;
            if (id.Value == (int)ASILanguage.FRENCH)
                return ASILanguage.FRENCH;
            return ASILanguage.ENGLISH;
        }

        public void LoadSettings()
        {
            string settingsPath = Utils.SettingsFilePath();
            if (!File.Exists(settingsPath))
                return;

            try
            {
                string settingsJson = File.ReadAllText(settingsPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(settingsJson))
                    return;

                JsonSettings? jsonSettings = JsonSerializer.Deserialize<JsonSettings>(settingsJson);
                if (jsonSettings != null)
                {
                    _pythonExePath = jsonSettings.PythonExeFilePath;
                    _language = jsonSettings.Language;
                    _darkTheme = jsonSettings.DarkTheme;
                    PythonPathChanged();
                    LanguageChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadSettings. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public static void LoadSettingsStatic()
        {
            string settingsPath = Utils.SettingsFilePath();
            if (!File.Exists(settingsPath))
                return;

            try
            {
                string settingsJson = File.ReadAllText(settingsPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(settingsJson))
                    return;

                JsonSettings? jsonSettings = JsonSerializer.Deserialize<JsonSettings>(settingsJson);
                if (jsonSettings != null)
                {
                    _pythonExePath = jsonSettings.PythonExeFilePath;
                    _language = jsonSettings.Language;
                    _darkTheme = jsonSettings.DarkTheme;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadSettingsStatic. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public static void SaveSettings()
        {
            try
            {
                JsonSettings settings = new JsonSettings()
                {
                    Language = (_language != null && _language.HasValue ? _language.Value : ASILanguage.ENGLISH),
                    PythonExeFilePath = _pythonExePath,
                    DarkTheme = _darkTheme
                };
                string jsonString = JsonSerializer.Serialize<JsonSettings>(settings, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.SettingsFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SaveSettings. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public static void LoadCustomBlueprints()
        {
            string customBlueprintsFilepath = Utils.CustomBlueprintsFilePath();
            if (!File.Exists(customBlueprintsFilepath))
                return;

            try
            {
                string customBlueprintsJson = File.ReadAllText(customBlueprintsFilepath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(customBlueprintsJson))
                    return;

                CustomBlueprints? customBlueprints = JsonSerializer.Deserialize<CustomBlueprints>(customBlueprintsJson);
                if (customBlueprints != null)
                    _customBlueprints = customBlueprints;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadCustomBlueprints. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public static void SaveCustomBlueprints()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize<CustomBlueprints>(_customBlueprints, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.CustomBlueprintsFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SaveCustomBlueprints. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public static string GetCustomBlueprintsB64()
        {
            string ret = string.Empty;

            if (SettingsPage._customBlueprints?.Dinos != null && SettingsPage._customBlueprints.Dinos.Count > 0)
            {
                ret += "Dinos=";
                foreach (string str in SettingsPage._customBlueprints.Dinos)
                    if (!string.IsNullOrWhiteSpace(str))
                        ret += $"{str};";
            }
            if (SettingsPage._customBlueprints?.Items != null && SettingsPage._customBlueprints.Items.Count > 0)
            {
                if (ret.Length > 0)
                    ret += "|";
                ret += "Items=";
                foreach (string str in SettingsPage._customBlueprints.Items)
                    if (!string.IsNullOrWhiteSpace(str))
                        ret += $"{str};";
            }
            if (SettingsPage._customBlueprints?.Structures != null && SettingsPage._customBlueprints.Structures.Count > 0)
            {
                if (ret.Length > 0)
                    ret += "|";
                ret += "Structures=";
                foreach (string str in SettingsPage._customBlueprints.Structures)
                    if (!string.IsNullOrWhiteSpace(str))
                        ret += $"{str};";
            }

            if (ret.Length > 0)
                ret = Convert.ToBase64String(Encoding.UTF8.GetBytes(ret), Base64FormattingOptions.None).Replace("=", ",", StringComparison.InvariantCulture).Replace("+", "-", StringComparison.InvariantCulture).Replace("/", "_", StringComparison.InvariantCulture);
            
            return ret;
        }

        public void RemoveJsonExportProfileFromDropDown(JsonExportProfile jsonProfile)
        {
            if (jsonProfile == null || mf_JsonDataSelect?.Items == null || mf_JsonDataSelect.Items.Count <= 0)
                return;
            int toDel = -1;
            string jsonProfileLabel = jsonProfile.GetLabel();
            for (int i = 0; i < mf_JsonDataSelect.Items.Count; i++)
            {
                MenuFlyoutItem? mfi = mf_JsonDataSelect.Items[i] as MenuFlyoutItem;
                if (mfi != null && string.Compare(mfi.Text, jsonProfileLabel, StringComparison.InvariantCulture) == 0)
                {
                    toDel = i;
                    break;
                }
            }
            if (toDel >= 0)
                mf_JsonDataSelect.Items.RemoveAt(toDel);
        }

        public void AddJsonExportProfileToDropDown(JsonExportProfile jsonProfile)
        {
            if (jsonProfile == null)
                return;
            var newMenuItem = new MenuFlyoutItem();
            newMenuItem.Text = jsonProfile.GetLabel();
            newMenuItem.Click += (s, e1) =>
            {
                JsonExportProfileSelected(jsonProfile);
            };
            mf_JsonDataSelect.Items.Add(newMenuItem);
        }

        public void JsonExportProfileSelected(JsonExportProfile? jsonProfile)
        {
            _selectedJsonExportProfile = jsonProfile;
            if (_selectedJsonExportProfile == null)
            {
                btn_LoadJsonData.Visibility = Visibility.Collapsed;
                btn_RemoveJsonData.Visibility = Visibility.Collapsed;
                return;
            }

            _asaSaveFilePath = _selectedJsonExportProfile.SaveFilePath;
            _mapName = _selectedJsonExportProfile.MapName;
            AsaSaveFilePathChanged();
            MapNameChanged();
            DataTypesSelectionChanged(_selectedJsonExportProfile);
            tb_JsonDataSelect.Text = _selectedJsonExportProfile.GetLabel();

            btn_RemoveJsonData.Visibility = Visibility.Visible;
            btn_LoadJsonData.Visibility = Visibility.Visible;
        }

        public void DataTypesSelectionChanged(JsonExportProfile? jsonProfile)
        {
            if (jsonProfile == null)
                return;

            cb_extractDinos.IsChecked = jsonProfile.ExtractedDinos;
            cb_extractPlayerPawns.IsChecked = jsonProfile.ExtractedPlayerPawns;
            cb_extractItems.IsChecked = jsonProfile.ExtractedItems;
            cb_extractStructures.IsChecked = jsonProfile.ExtractedStructures;
            cb_extractPlayers.IsChecked = jsonProfile.ExtractedPlayers;
            cb_extractTribes.IsChecked = jsonProfile.ExtractedTribes;
        }

        public static void LoadJsonExportProfiles()
        {
            string exportProfilesPath = Utils.ExportProfilesFilePath();
            if (!File.Exists(exportProfilesPath))
                return;

            string exportProfilesJson = File.ReadAllText(exportProfilesPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(exportProfilesJson))
                return;

            try
            {
                List<JsonExportProfile>? exportProfiles = JsonSerializer.Deserialize<List<JsonExportProfile>>(exportProfilesJson);
                if (exportProfiles != null)
                    _jsonExportProfiles = exportProfiles;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadJsonExportProfiles. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public static void SaveJsonExportProfiles()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize<List<JsonExportProfile>>(_jsonExportProfiles, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.ExportProfilesFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SaveJsonExportProfiles. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public static JsonExportProfile? FormatNewJsonExportProfile(string? saveFilePath, string? mapName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract)
        {
            if (string.IsNullOrWhiteSpace(saveFilePath) || string.IsNullOrWhiteSpace(mapName))
                return null;

            int highestId = 0;
            if (_jsonExportProfiles != null && _jsonExportProfiles.Count > 0)
                foreach (var jsonProfile in _jsonExportProfiles)
                    if (jsonProfile != null && jsonProfile.ID > highestId)
                        highestId = jsonProfile.ID;

            return new JsonExportProfile()
            {
                ID = (highestId + 1),
                SaveFilePath = saveFilePath,
                MapName = mapName,
                CreationDate = DateTime.Now,
                ExtractedDinos = extractDinos,
                ExtractedPlayerPawns = extractPlayerPawns,
                ExtractedItems = extractItems,
                ExtractedStructures = extractStructures,
                ExtractedPlayers = extractPlayers,
                ExtractedTribes = extractTribes,
                FastExtract = fastExtract,
            };
        }

        public static JsonExportProfile? AddNewJsonExportProfile(string? saveFilePath, string? mapName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract)
        {
            JsonExportProfile? p = FormatNewJsonExportProfile(saveFilePath, mapName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, fastExtract);
            if (p == null)
                return null;

            if (_jsonExportProfiles == null)
                return null;

            _jsonExportProfiles.Add(p);
            SettingsPage.SaveJsonExportProfiles();
            return p;
        }

        /*
        private void _window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (StandardPopup.IsOpen)
            {
                b_extractJsonPopup.Width = gr_SettingsPage.ActualWidth;
                b_extractJsonPopup.Height = gr_SettingsPage.ActualHeight;
            }
        }
        */

        [RelayCommand]
        private void PythonSelectPath(string path)
        {
            if (path == "MANUAL")
#pragma warning disable CS4014 // Don't wait on purpose
                OpenPythonFilePicker();
#pragma warning restore CS4014
            else
            {
                _pythonExePath = path;
                PythonPathChanged();
            }
        }

        private async Task OpenPythonFilePicker()
        {
            if (App._window == null)
                return;
            StorageFile? pythonFile = null;
            try
            {
                var filePicker = new FileOpenPicker();

                // Configure the FileOpenPicker
                filePicker.ViewMode = PickerViewMode.List;
                filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                filePicker.FileTypeFilter.Add(".exe");

                // HWND initialization (needed for WinUI3)
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App._window);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

                // Open FileOpenPicker window
                pythonFile = await filePicker.PickSingleFileAsync();
            }
            catch (Exception ex)
            {
                pythonFile = null;
                Logger.Instance.Log($"Exception caught in OpenPythonFilePicker. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (pythonFile != null && !string.IsNullOrWhiteSpace(pythonFile.Path) && File.Exists(pythonFile.Path))
            {
                _pythonExePath = pythonFile.Path;
                PythonPathChanged();
            }
        }

        private static readonly List<string> _validJsonFileNames = new List<string>()
        {
            "dinos.json",
            "player_pawns.json",
            "items.json",
            "structures.json",
            "players.json",
            "tribes.json"
        };

        private async Task OpenJSONDataFolderPicker()
        {
            if (App._window == null)
                return;
            StorageFolder? jsonDataFolder = null;
            try
            {
                var folderPicker = new FolderPicker();

                // Configure the FolderPicker
                folderPicker.ViewMode = PickerViewMode.List;
                folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add("*");

                // HWND initialization (needed for WinUI3)
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App._window);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                // Open FolderPicker window
                jsonDataFolder = await folderPicker.PickSingleFolderAsync();
            }
            catch (Exception ex)
            {
                jsonDataFolder = null;
                Logger.Instance.Log($"Exception caught in OpenJSONDataFolderPicker. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (jsonDataFolder != null && !string.IsNullOrWhiteSpace(jsonDataFolder.Path) && Directory.Exists(jsonDataFolder.Path))
            {
                //Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", jsonDataFolder);
                Dictionary<string, bool> foundFiles = new Dictionary<string, bool>();
                foreach (string validFile in _validJsonFileNames)
                    foundFiles.Add(validFile, false);

                bool foundValidFiles = false;
                string[] files = Directory.GetFiles(jsonDataFolder.Path);
                foreach (string filePath in files)
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        string fileName = Path.GetFileName(filePath);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            foreach (var found in foundFiles)
                                if (string.Compare(fileName, found.Key, StringComparison.InvariantCulture) == 0)
                                {
                                    foundFiles[found.Key] = true;
                                    foundValidFiles = true;
                                    break;
                                }
                        }
                    }

                if (foundValidFiles)
                {
                    _jsonDataFolderPath = jsonDataFolder.Path;
                    JsonDataFolderPathChanged(foundFiles);
                }
                else
                    MainWindow.ShowToast("No valid JSON files were found in the folder.", BackgroundColor.WARNING);
            }
        }

        public void MapNameChanged()
        {
            if (!string.IsNullOrWhiteSpace(_mapName))
                tb_MapNameSelect.Text = _mapName;
        }

        public void AsaSaveFilePathChanged()
        {
            if (!string.IsNullOrWhiteSpace(_asaSaveFilePath))
            {
                tb_ASASaveFilePath.Text = _asaSaveFilePath;
                sp_extractJsonPopup.Visibility = Visibility.Visible;
                btn_CancelBottom.Visibility = Visibility.Collapsed;
            }
            else
            {
                sp_extractJsonPopup.Visibility = Visibility.Collapsed;
                btn_CancelBottom.Visibility = Visibility.Visible;
            }
        }

        public void FolderMapNameChanged()
        {
            if (!string.IsNullOrWhiteSpace(_mapName))
                tb_FolderMapNameSelect.Text = _mapName;
        }

        public void JsonDataFolderPathChanged(Dictionary<string, bool> foundFiles)
        {
            cb_foundDinos.IsChecked = false;
            cb_foundPlayerPawns.IsChecked = false;
            cb_foundItems.IsChecked = false;
            cb_foundStructures.IsChecked = false;
            cb_foundPlayers.IsChecked = false;
            cb_foundTribes.IsChecked = false;
            foreach (var found in foundFiles)
                if (found.Value)
                {
                    if (found.Key == "dinos.json")
                        cb_foundDinos.IsChecked = true;
                    else if (found.Key == "player_pawns.json")
                        cb_foundPlayerPawns.IsChecked = true;
                    else if (found.Key == "items.json")
                        cb_foundItems.IsChecked = true;
                    else if (found.Key == "structures.json")
                        cb_foundStructures.IsChecked = true;
                    else if (found.Key == "players.json")
                        cb_foundPlayers.IsChecked = true;
                    else if (found.Key == "tribes.json")
                        cb_foundTribes.IsChecked = true;
                }
            tb_FolderMapNameSelect.Text = "Click here...";
            if (!JsonFolderSelectedPopup.IsOpen)
                JsonFolderSelectedPopup.IsOpen = true;
        }

        private void ValidateJsonFolderSelectedClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_mapName) || tb_FolderMapNameSelect.Text == "Click here...")
            {
                Logger.Instance.Log("Cannot get JSON data: Incorrect ASA map name.", Logger.LogLevel.WARNING);
                MainWindow.ShowToast("Cannot get JSON data: Incorrect ASA map name.", BackgroundColor.WARNING);
                return;
            }

            bool extractDinos = (cb_foundDinos.IsChecked != null && cb_foundDinos.IsChecked.HasValue ? cb_foundDinos.IsChecked.Value : false);
            bool extractPlayerPawns = (cb_foundPlayerPawns.IsChecked != null && cb_foundPlayerPawns.IsChecked.HasValue ? cb_foundPlayerPawns.IsChecked.Value : false);
            bool extractItems = (cb_foundItems.IsChecked != null && cb_foundItems.IsChecked.HasValue ? cb_foundItems.IsChecked.Value : false);
            bool extractStructures = (cb_foundStructures.IsChecked != null && cb_foundStructures.IsChecked.HasValue ? cb_foundStructures.IsChecked.Value : false);
            bool extractPlayers = (cb_foundPlayers.IsChecked != null && cb_foundPlayers.IsChecked.HasValue ? cb_foundPlayers.IsChecked.Value : false);
            bool extractTribes = (cb_foundTribes.IsChecked != null && cb_foundTribes.IsChecked.HasValue ? cb_foundTribes.IsChecked.Value : false);

            if (!extractDinos && !extractPlayerPawns && !extractItems && !extractStructures && !extractPlayers && !extractTribes)
            {
                Logger.Instance.Log("Cannot get JSON data: No valid JSON file name found.", Logger.LogLevel.WARNING);
                MainWindow.ShowToast("Cannot get JSON data: No valid JSON file name found.", BackgroundColor.WARNING);
                return;
            }

            JsonExportProfile? jep = AddNewJsonExportProfile(_jsonDataFolderPath, _mapName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, true);
            if (jep == null)
            {
                Logger.Instance.Log("Failed to create new JSON export profile.", Logger.LogLevel.ERROR);
                MainWindow.ShowToast("Failed to create new JSON export profile.", BackgroundColor.ERROR);
                return;
            }
            AddJsonExportProfileToDropDown(jep);
            JsonExportProfileSelected(jep);
            if (JsonFolderSelectedPopup.IsOpen)
                JsonFolderSelectedPopup.IsOpen = false;
        }

        public void CloseJsonFolderSelectedPopupClicked(object sender, RoutedEventArgs e)
        {
            if (JsonFolderSelectedPopup.IsOpen)
                JsonFolderSelectedPopup.IsOpen = false;
        }

        public void PythonPathChanged()
        {
            if (!string.IsNullOrWhiteSpace(_pythonExePath))
            {
                tb_PythonSelect.Text = _pythonExePath;
                SaveSettings();
                PythonManager.InstallArkParse();
            }
        }

        public void LanguageChanged()
        {
            if (_language != null && _language.HasValue)
            {
                switch (_language.Value)
                {
                    case ASILanguage.FRENCH:
                        r_LanguageSelect.Text = "Français";
                        i_LanguageSelect.Source = _flagFrench;
                        break;

                    default:
                        r_LanguageSelect.Text = "English";
                        i_LanguageSelect.Source = _flagEnglish;
                        break;
                }
                SaveSettings();
            }
        }

        private async Task OpenAsaSaveFilePicker()
        {
            if (App._window == null)
                return;
            StorageFile? saveFile = null;
            try
            {
                var filePicker = new FileOpenPicker();

                // Configure the FileOpenPicker
                filePicker.ViewMode = PickerViewMode.List;
                filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                filePicker.FileTypeFilter.Add(".ark");

                // HWND initialization (needed for WinUI3)
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App._window);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

                // Open FileOpenPicker window
                saveFile = await filePicker.PickSingleFileAsync();
            }
            catch (Exception ex)
            {
                saveFile = null;
                Logger.Instance.Log($"Exception caught in OpenAsaSaveFilePicker. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (saveFile != null && !string.IsNullOrWhiteSpace(saveFile.Path) && File.Exists(saveFile.Path))
            {
                _asaSaveFilePath = saveFile.Path;
                AsaSaveFilePathChanged();
            }
        }

#pragma warning disable CS4014 // Don't wait on purpose
        private void SelectASASaveFileClicked(object sender, RoutedEventArgs e) => OpenAsaSaveFilePicker();
#pragma warning restore CS4014

        private bool _isExtracting = false;
        private async Task DoExtract(bool useJsonResultTb, string? saveFilePath, string? mapName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract, List<KeyValuePair<JsonExportProfile, bool>>? extractions = null, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
        {
            if (string.IsNullOrWhiteSpace(saveFilePath) || !File.Exists(saveFilePath))
            {
                Logger.Instance.Log("Cannot extract JSON data: Incorrect ASA save file path.", Logger.LogLevel.WARNING);
                if (useJsonResultTb)
                {
                    tb_ExtractJsonResult.Text = "Cannot extract JSON data: Incorrect ASA save file path.";
                    tb_ExtractJsonResult.Foreground = _errorColor;
                    tb_ExtractJsonResult.Visibility = Visibility.Visible;
                }
                else
                    MainWindow.ShowToast("Incorrect ASA save file.", BackgroundColor.WARNING);
                return;
            }

            if (string.IsNullOrWhiteSpace(mapName))
            {
                Logger.Instance.Log("Cannot extract JSON data: Incorrect ASA map name.", Logger.LogLevel.WARNING);
                if (useJsonResultTb)
                {
                    tb_ExtractJsonResult.Text = "Cannot extract JSON data: Please select a map name.";
                    tb_ExtractJsonResult.Foreground = _errorColor;
                    tb_ExtractJsonResult.Visibility = Visibility.Visible;
                }
                else
                    MainWindow.ShowToast("Incorrect map name.", BackgroundColor.WARNING);
                return;
            }

            if (!extractDinos && !extractPlayerPawns && !extractItems && !extractStructures && !extractPlayers && !extractTribes)
            {
                Logger.Instance.Log("Cannot extract JSON data: No data type selected.", Logger.LogLevel.WARNING);
                if (useJsonResultTb)
                {
                    tb_ExtractJsonResult.Text = "Cannot extract JSON data: No data type selected.";
                    tb_ExtractJsonResult.Foreground = _errorColor;
                    tb_ExtractJsonResult.Visibility = Visibility.Visible;
                }
                else
                    MainWindow.ShowToast("No data type selected.", BackgroundColor.WARNING);
                return;
            }

            if (useJsonResultTb)
                tb_ExtractJsonResult.Visibility = Visibility.Collapsed;
            bool ret = false;
            if (!_isExtracting)
            {
                _isExtracting = true;
                ret = await PythonManager.RunArkParse(saveFilePath,
                    mapName,
                    extractDinos,
                    extractPlayerPawns,
                    extractItems,
                    extractStructures,
                    extractPlayers,
                    extractTribes,
                    fastExtract,
                    extractions,
                    callback);
                _isExtracting = false;
            }
            if (!ret)
            {
                Logger.Instance.Log("JSON data extraction failed.", Logger.LogLevel.ERROR);
                if (useJsonResultTb)
                {
                    tb_ExtractJsonResult.Text = "JSON data extraction failed.";
                    tb_ExtractJsonResult.Foreground = _errorColor;
                    tb_ExtractJsonResult.Visibility = Visibility.Visible;
                }
                else
                    MainWindow.ShowToast("JSON data extraction failed.", BackgroundColor.WARNING);
            }
        }

        private async void ExtractASASaveFileClicked(object sender, RoutedEventArgs e)
        {
            bool extractDinos = (cb_extractDinos.IsChecked != null && cb_extractDinos.IsChecked.HasValue ? cb_extractDinos.IsChecked.Value : false);
            bool extractPlayerPawns = (cb_extractPlayerPawns.IsChecked != null && cb_extractPlayerPawns.IsChecked.HasValue ? cb_extractPlayerPawns.IsChecked.Value : false);
            bool extractItems = (cb_extractItems.IsChecked != null && cb_extractItems.IsChecked.HasValue ? cb_extractItems.IsChecked.Value : false);
            bool extractStructures = (cb_extractStructures.IsChecked != null && cb_extractStructures.IsChecked.HasValue ? cb_extractStructures.IsChecked.Value : false);
            bool extractPlayers = (cb_extractPlayers.IsChecked != null && cb_extractPlayers.IsChecked.HasValue ? cb_extractPlayers.IsChecked.Value : false);
            bool extractTribes = (cb_extractTribes.IsChecked != null && cb_extractTribes.IsChecked.HasValue ? cb_extractTribes.IsChecked.Value : false);

            bool fastExtract = string.Compare(tb_ExtractionType.Text, "Legacy extraction", StringComparison.InvariantCulture) != 0;

            await DoExtract(true, _asaSaveFilePath, _mapName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, fastExtract);
        }

        public void CloseArkParserPopupClicked(object sender, RoutedEventArgs e)
        {
            if (StandardPopup.IsOpen)
                StandardPopup.IsOpen = false;
        }

        private void ShowArkParserPopupOffsetClicked(object sender, RoutedEventArgs e)
        {
            if (!StandardPopup.IsOpen)
                StandardPopup.IsOpen = true;
        }

        private void mfi_LanguageEnglish_Click(object sender, RoutedEventArgs e)
        {
            _language = ASILanguage.ENGLISH;
            LanguageChanged();
        }

        private void mfi_LanguageFrench_Click(object sender, RoutedEventArgs e)
        {
            _language = ASILanguage.FRENCH;
            LanguageChanged();
        }

        private void mfi_JsonDataArkParse_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_pythonExePath) || !File.Exists(_pythonExePath) || !File.Exists(Utils.PythonFilePathFromVenv()))
            {
                tb_CannotExtractWithArkParse.Visibility = Visibility.Visible;
                return;
            }

            tb_CannotExtractWithArkParse.Visibility = Visibility.Collapsed;
#pragma warning disable CS8625
            ShowArkParserPopupOffsetClicked(null, null);
#pragma warning restore CS8625
        }

#pragma warning disable CS4014 // Don't wait on purpose
        private void mfi_JsonDataManual_Click(object sender, RoutedEventArgs e) => OpenJSONDataFolderPicker();
#pragma warning restore CS4014

        private void btn_LoadJsonData_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedJsonExportProfile == null)
            {
                Logger.Instance.Log("Failed to load JSON data (no export profile selected).", Logger.LogLevel.ERROR);
                tb_JsonDataLoaded.Text = "Failed to load JSON data (no export profile selected).";
                tb_JsonDataLoaded.Foreground = _errorColor;
                tb_JsonDataLoaded.Visibility = Visibility.Visible;
                return;
            }

            string folderPath = _selectedJsonExportProfile.GetExportFolderName();
            if (!Directory.Exists(folderPath))
                folderPath = Path.Combine(Utils.JsonExportsFolder(), _selectedJsonExportProfile.GetExportFolderName());
            if (!Directory.Exists(folderPath))
            {
                Logger.Instance.Log("Failed to load JSON data (export folder not found).", Logger.LogLevel.ERROR);
                tb_JsonDataLoaded.Text = "Failed to load JSON data (export folder not found).";
                tb_JsonDataLoaded.Foreground = _errorColor;
                tb_JsonDataLoaded.Visibility = Visibility.Visible;
                return;
            }

            Utils.ClearAllPagesFiltersAndGroups();

            bool hasErrors = false;
            bool refreshMinimap = (string.Compare(_currentlyLoadedMapName, _selectedJsonExportProfile.MapName, StringComparison.InvariantCulture) != 0);
            _currentlyLoadedMapName = _selectedJsonExportProfile.MapName;
            if (refreshMinimap && MainWindow._minimap != null)
                MainWindow.OpenMinimap();

            string saveInfoFilePath = Path.Combine(folderPath, "save_info.json");
            if (!File.Exists(saveInfoFilePath))
            {
                Logger.Instance.Log($"Could not load savefile info JSON data (file not found at \"{saveInfoFilePath}\").", Logger.LogLevel.WARNING);
                hasErrors = true;
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(saveInfoFilePath, Encoding.UTF8);
                    _saveFileData = JsonSerializer.Deserialize<SaveFileInfo>(json);
                    if (_saveFileData != null)
                    {
                        DateTime dt = DateTime.Now;
                        if (File.Exists(_selectedJsonExportProfile.SaveFilePath))
                            dt = new FileInfo(_selectedJsonExportProfile.SaveFilePath).LastWriteTime;
                        _saveFileData.SaveDateTime = dt;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Failed to load savefile info JSON data. Exception=[{ex}]", Logger.LogLevel.ERROR);
                    hasErrors = true;
                }
            }

            if (_selectedJsonExportProfile.ExtractedDinos)
            {
                string dinosFilePath = Path.Combine(folderPath, "dinos.json");
                if (!File.Exists(dinosFilePath))
                {
                    Logger.Instance.Log($"Could not load dinos JSON data (file not found at \"{dinosFilePath}\").", Logger.LogLevel.WARNING);
                    hasErrors = true;
                }
                else
                {
                    try
                    {
                        _dinosData = JsonSerializer.Deserialize<List<Dino>>(File.ReadAllText(dinosFilePath, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Failed to load dinos JSON data. Exception=[{ex}]", Logger.LogLevel.ERROR);
                        hasErrors = true;
                    }
                }
            }

            if (_selectedJsonExportProfile.ExtractedPlayerPawns)
            {
                string playerPawnsFilePath = Path.Combine(folderPath, "player_pawns.json");
                if (!File.Exists(playerPawnsFilePath))
                {
                    Logger.Instance.Log($"Could not load player pawns JSON data (file not found at \"{playerPawnsFilePath}\").", Logger.LogLevel.WARNING);
                    hasErrors = true;
                }
                else
                {
                    try
                    {
                        _playerPawnsData = JsonSerializer.Deserialize<List<PlayerPawn>>(File.ReadAllText(playerPawnsFilePath, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Failed to load player pawns JSON data. Exception=[{ex}]", Logger.LogLevel.ERROR);
                        hasErrors = true;
                    }
                }
            }

            if (_selectedJsonExportProfile.ExtractedItems)
            {
                string itemsFilePath = Path.Combine(folderPath, "items.json");
                if (!File.Exists(itemsFilePath))
                {
                    Logger.Instance.Log($"Could not load items JSON data (file not found at \"{itemsFilePath}\").", Logger.LogLevel.WARNING);
                    hasErrors = true;
                }
                else
                {
                    try
                    {
                        _itemsData = JsonSerializer.Deserialize<List<Item>>(File.ReadAllText(itemsFilePath, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Failed to load items JSON data. Exception=[{ex}]", Logger.LogLevel.ERROR);
                        hasErrors = true;
                    }
                }
            }

            if (_selectedJsonExportProfile.ExtractedItems)
            {
                string structuresFilePath = Path.Combine(folderPath, "structures.json");
                if (!File.Exists(structuresFilePath))
                {
                    Logger.Instance.Log($"Could not load structures JSON data (file not found at \"{structuresFilePath}\").", Logger.LogLevel.WARNING);
                    hasErrors = true;
                }
                else
                {
                    try
                    {
                        _structuresData = JsonSerializer.Deserialize<List<Structure>>(File.ReadAllText(structuresFilePath, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Failed to load structures JSON data. Exception=[{ex}]", Logger.LogLevel.ERROR);
                        hasErrors = true;
                    }
                }
            }

            if (_selectedJsonExportProfile.ExtractedPlayers)
            {
                string playersFilePath = Path.Combine(folderPath, "players.json");
                if (!File.Exists(playersFilePath))
                {
                    Logger.Instance.Log($"Could not load players JSON data (file not found at \"{playersFilePath}\").", Logger.LogLevel.WARNING);
                    hasErrors = true;
                }
                else
                {
                    try
                    {
                        _playersData = JsonSerializer.Deserialize<List<Player>>(File.ReadAllText(playersFilePath, Encoding.UTF8));
                        //if (_playersData != null && _playersData.Count > 0)
                        //    foreach (Player p in _playersData)
                        //        Utils.ReplaceUnicodeEscapedStrInObject(p, typeof(Player));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Failed to load players JSON data. Exception=[{ex}]", Logger.LogLevel.ERROR);
                        hasErrors = true;
                    }
                }
            }

            if (_selectedJsonExportProfile.ExtractedTribes)
            {
                string tribesFilePath = Path.Combine(folderPath, "tribes.json");
                if (!File.Exists(tribesFilePath))
                {
                    Logger.Instance.Log($"Could not load tribes JSON data (file not found at \"{tribesFilePath}\").", Logger.LogLevel.WARNING);
                    hasErrors = true;
                }
                else
                {
                    try
                    {
                        _tribesData = JsonSerializer.Deserialize<List<Tribe>>(File.ReadAllText(tribesFilePath, Encoding.UTF8));
                        //if (_tribesData != null && _tribesData.Count > 0)
                        //    foreach (Tribe t in _tribesData)
                        //        Utils.ReplaceUnicodeEscapedStrInObject(t, typeof(Tribe));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Failed to load tribes JSON data. Exception=[{ex}]", Logger.LogLevel.ERROR);
                        hasErrors = true;
                    }
                }
            }

            if (_dinosData != null && _dinosData.Count > 0)
                foreach (var dinoData in _dinosData)
                    dinoData.InitStats();

            if (hasErrors)
            {
                tb_JsonDataLoaded.Text = "JSON data was partially loaded (see logs for more info).";
                tb_JsonDataLoaded.Foreground = _warningColor;
            }
            else
            {
                tb_JsonDataLoaded.Text = "JSON data successfully loaded.";
                tb_JsonDataLoaded.Foreground = _successColor;
            }
            tb_JsonDataLoaded.Visibility = Visibility.Visible;
        }

        private void btn_RemoveJsonData_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedJsonExportProfile != null && !ConfirmJsonExportRemovalPopup.IsOpen)
            {
                string folderPath = _selectedJsonExportProfile.GetExportFolderName();
                if (!Directory.Exists(folderPath))
                    folderPath = Path.Combine(Utils.JsonExportsFolder(), _selectedJsonExportProfile.GetExportFolderName());

                tb_confirmJsonFolderRemoval.Text = $"Are you sure that you want to delete folder \"{folderPath}\"?";
                ConfirmJsonExportRemovalPopup.IsOpen = true;
            }
        }

        private void RemoveJsonExportProfile(JsonExportProfile jep)
        {
            RemoveJsonExportProfileFromDropDown(jep);
            if (_jsonExportProfiles != null && _jsonExportProfiles.Count > 0)
            {
                int toDel = -1;
                for (int i = 0; i < _jsonExportProfiles.Count; i++)
                    if (_jsonExportProfiles[i] != null && _jsonExportProfiles[i].ID == jep.ID)
                    {
                        toDel = i;
                        break;
                    }
                if (toDel >= 0)
                    _jsonExportProfiles.RemoveAt(toDel);
            }
        }

        public void ValidateJsonFolderRemovalClicked(object sender, RoutedEventArgs e)
        {
            if (ConfirmJsonExportRemovalPopup.IsOpen)
                ConfirmJsonExportRemovalPopup.IsOpen = false;

            if (_selectedJsonExportProfile == null)
            {
                Logger.Instance.Log("Failed to remove JSON data (no export profile selected).", Logger.LogLevel.ERROR);
                tb_JsonDataLoaded.Text = "Failed to remove JSON data (no export profile selected).";
                tb_JsonDataLoaded.Foreground = _errorColor;
                tb_JsonDataLoaded.Visibility = Visibility.Visible;
                return;
            }

            string folderPath = _selectedJsonExportProfile.GetExportFolderName();
            if (!Directory.Exists(folderPath))
                folderPath = Path.Combine(Utils.JsonExportsFolder(), _selectedJsonExportProfile.GetExportFolderName());

            if (!Directory.Exists(folderPath))
                Logger.Instance.Log($"JSON data has already been removed (folder not found at \"{folderPath}\").", Logger.LogLevel.ERROR);

            List<JsonExportProfile> toRemove = new List<JsonExportProfile>();
            if (_jsonExportProfiles != null && _jsonExportProfiles.Count > 0)
                foreach (var jsonExportProfile in _jsonExportProfiles)
                {
                    string currentFolderPath = jsonExportProfile.GetExportFolderName();
                    if (!Directory.Exists(currentFolderPath))
                        currentFolderPath = Path.Combine(Utils.JsonExportsFolder(), jsonExportProfile.GetExportFolderName());
                    if (jsonExportProfile != null && string.Compare(folderPath, currentFolderPath, StringComparison.InvariantCulture) == 0)
                        toRemove.Add(jsonExportProfile);
                }

            if (Directory.Exists(folderPath))
                try { Directory.Delete(folderPath, true); }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Failed to remove JSON data. Exception=[{ex}]", Logger.LogLevel.ERROR);
                    tb_JsonDataLoaded.Text = "Failed to remove JSON data (exception caught).";
                    tb_JsonDataLoaded.Foreground = _errorColor;
                    tb_JsonDataLoaded.Visibility = Visibility.Visible;
                    return;
                }

            if (toRemove.Count > 0)
            {
                foreach (var jepToRemove in toRemove)
                    RemoveJsonExportProfile(jepToRemove);
                SaveJsonExportProfiles();
            }

            _selectedJsonExportProfile = null;
            tb_JsonDataSelect.Text = "Click here...";
        }

        public void CloseJsonFolderRemovalPopupClicked(object sender, RoutedEventArgs e)
        {
            if (ConfirmJsonExportRemovalPopup.IsOpen)
                ConfirmJsonExportRemovalPopup.IsOpen = false;
        }

        private void mfi_ExtractFast_Click(object sender, RoutedEventArgs e) => tb_ExtractionType.Text = "Fast extraction";

        private void mfi_ExtractLegacy_Click(object sender, RoutedEventArgs e) => tb_ExtractionType.Text = "Legacy extraction";

        #region Extraction presets

        public static void LoadJsonExportPresets()
        {
            string jsonExportPresetsPath = Utils.ExportPresetsFilePath();
            if (!File.Exists(jsonExportPresetsPath))
                return;

            try
            {
                string presetsJson = File.ReadAllText(jsonExportPresetsPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(presetsJson))
                    return;

                List<JsonExportPreset>? jsonExportPresets = JsonSerializer.Deserialize<List<JsonExportPreset>>(presetsJson);
                _jsonExportPresets.Clear();
                if (jsonExportPresets != null)
                    _jsonExportPresets = jsonExportPresets;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadJsonExportPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public static void SaveJsonExportPresets()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize<List<JsonExportPreset>>(_jsonExportPresets, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.ExportPresetsFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SaveJsonExportPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void JsonExportPresetSelected_AddTo(string presetName)
        {
            tb_ExistingExtractionPresets_AddTo.Text = presetName;
            btn_ExistingExtractionPresets_AddTo.IsEnabled = true;
        }

        private void JsonExportPresetSelected_Remove(string presetName)
        {
            tb_ExistingExtractionPresets_Remove.Text = presetName;
            btn_RemoveExistingExtractionPreset.IsEnabled = true;
        }

        private void JsonExportPresetSelected_Details(string presetName)
        {
            tb_ExistingExtractionPresets_Details.Text = presetName;
            sp_ExistingExtractionPresetDetails.Children.Clear();

            if (_jsonExportPresets != null && _jsonExportPresets.Count > 0)
                foreach (JsonExportPreset preset in _jsonExportPresets)
                    if (preset != null && string.Compare(presetName, preset.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        if (preset.ExportProfiles != null && preset.ExportProfiles.Count > 0)
                            foreach (JsonExportProfile jep in preset.ExportProfiles)
                                if (jep != null)
                                {
                                    TextBox tb = new TextBox()
                                    {
                                        FontSize = 12.0d,
                                        TextWrapping = TextWrapping.NoWrap,
                                        AcceptsReturn = false,
                                        IsReadOnly = true,
                                        MaxWidth = 1200.0d,
                                        Text = jep.ToString(), //$"Map: {(jep.MapName ?? "")}, FilePath: \"{(jep.SaveFilePath ?? "")}\", Extract: {(jep.ExtractedDinos ? "dinos," : "")}{(jep.ExtractedPlayerPawns ? "pawns," : "")}{(jep.ExtractedItems ? "items," : "")}{(jep.ExtractedStructures ? "structures," : "")}{(jep.ExtractedPlayers ? "players," : "")}{(jep.ExtractedTribes ? "tribes," : "")}",
                                        VerticalAlignment = VerticalAlignment.Center,
                                        HorizontalAlignment = HorizontalAlignment.Left,
                                        Margin = new Thickness(0.0d, 5.0d, 0.0d, 0.0d)
                                    };
                                    sp_ExistingExtractionPresetDetails.Children.Add(tb);
                                }
                        break;
                    }
        }

        private void RefreshExtractProfilePresetsPopup()
        {
            tb_ExistingExtractionPresets_Details.Text = "Click here...";
            sp_ExistingExtractionPresetDetails.Children.Clear();
            mf_ExistingExtractionPresets_Details.Items.Clear();

            tb_ExistingExtractionPresets_AddTo.Text = "Click here...";
            btn_ExistingExtractionPresets_AddTo.IsEnabled = false;
            mf_ExistingExtractionPresets_AddTo.Items.Clear();

            tb_ExistingExtractionPresets_Remove.Text = "Click here...";
            btn_RemoveExistingExtractionPreset.IsEnabled = false;
            mf_ExistingExtractionPresets_Remove.Items.Clear();

            tb_CreateExtractionPreset.Text = "";
            btn_CreateExtractionPreset.IsEnabled = false;

            if (_jsonExportPresets == null || _jsonExportPresets.Count <= 0)
                return;

            foreach (JsonExportPreset preset in _jsonExportPresets)
                if (preset != null && !string.IsNullOrWhiteSpace(preset.Name))
                {
                    MenuFlyoutItem mfiDetails = new MenuFlyoutItem();
                    mfiDetails.Text = preset.Name;
                    mfiDetails.Click += (s, e1) => { JsonExportPresetSelected_Details(preset.Name); };
                    mf_ExistingExtractionPresets_Details.Items.Add(mfiDetails);

                    MenuFlyoutItem mfiAddTo = new MenuFlyoutItem();
                    mfiAddTo.Text = preset.Name;
                    mfiAddTo.Click += (s, e1) => { JsonExportPresetSelected_AddTo(preset.Name); };
                    mf_ExistingExtractionPresets_AddTo.Items.Add(mfiAddTo);

                    MenuFlyoutItem mfiRemove = new MenuFlyoutItem();
                    mfiRemove.Text = preset.Name;
                    mfiRemove.Click += (s, e1) => { JsonExportPresetSelected_Remove(preset.Name); };
                    mf_ExistingExtractionPresets_Remove.Items.Add(mfiRemove);
                }
        }

        private void tb_CreateExtractionPreset_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_CreateExtractionPreset.IsEnabled = (!string.IsNullOrWhiteSpace(tb_CreateExtractionPreset.Text) && tb_CreateExtractionPreset.Text.Length > 0);
        }

        private void btn_CreateExtractionPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonExportPresets == null)
                return;

            if (string.IsNullOrWhiteSpace(tb_CreateExtractionPreset.Text))
            {
                MainWindow.ShowToast("An extraction preset requires a name.", BackgroundColor.WARNING);
                return;
            }

            if (_jsonExportPresets.Count > 0)
                foreach (JsonExportPreset preset in _jsonExportPresets)
                    if (preset != null && string.Compare(tb_CreateExtractionPreset.Text, preset.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        MainWindow.ShowToast("This preset name already exists.", BackgroundColor.WARNING);
                        return;
                    }

            _jsonExportPresets.Add(new JsonExportPreset() { Name = tb_CreateExtractionPreset.Text });
            SaveJsonExportPresets();
            RefreshExtractProfilePresetsPopup();
            MainWindow.ShowToast("Extraction preset created.", BackgroundColor.SUCCESS);
        }

        private void btn_RemoveExistingExtractionPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonExportPresets == null)
                return;

            if (string.IsNullOrWhiteSpace(tb_ExistingExtractionPresets_Remove.Text) || string.Compare(tb_ExistingExtractionPresets_Remove.Text, "Click here...", StringComparison.InvariantCulture) == 0)
            {
                MainWindow.ShowToast("No extraction preset selected.", BackgroundColor.WARNING);
                return;

            }
            if (_jsonExportPresets.Count > 0)
            {
                int toDel = -1;
                for (int i = 0; i < _jsonExportPresets.Count; i++)
                    if (_jsonExportPresets[i] != null && string.Compare(tb_ExistingExtractionPresets_Remove.Text, _jsonExportPresets[i].Name, StringComparison.InvariantCulture) == 0)
                    {
                        toDel = i;
                        break;
                    }
                if (toDel >= 0)
                {
                    _jsonExportPresets.RemoveAt(toDel);
                    SaveJsonExportPresets();
                    RefreshExtractProfilePresetsPopup();
                    MainWindow.ShowToast("Preset removed.", BackgroundColor.SUCCESS);
                    return;
                }
            }

            MainWindow.ShowToast($"Preset \"{tb_ExistingExtractionPresets_Remove.Text}\" not found.", BackgroundColor.WARNING);
        }

        private void btn_ExistingExtractionPresets_AddTo_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonExportPresets == null)
                return;

            if (string.IsNullOrWhiteSpace(tb_ExistingExtractionPresets_AddTo.Text) || string.Compare(tb_ExistingExtractionPresets_AddTo.Text, "Click here...", StringComparison.InvariantCulture) == 0)
            {
                MainWindow.ShowToast("No extraction preset selected.", BackgroundColor.WARNING);
                return;
            }

            bool extractDinos = (cb_extractDinos.IsChecked != null && cb_extractDinos.IsChecked.HasValue ? cb_extractDinos.IsChecked.Value : false);
            bool extractPlayerPawns = (cb_extractPlayerPawns.IsChecked != null && cb_extractPlayerPawns.IsChecked.HasValue ? cb_extractPlayerPawns.IsChecked.Value : false);
            bool extractItems = (cb_extractItems.IsChecked != null && cb_extractItems.IsChecked.HasValue ? cb_extractItems.IsChecked.Value : false);
            bool extractStructures = (cb_extractStructures.IsChecked != null && cb_extractStructures.IsChecked.HasValue ? cb_extractStructures.IsChecked.Value : false);
            bool extractPlayers = (cb_extractPlayers.IsChecked != null && cb_extractPlayers.IsChecked.HasValue ? cb_extractPlayers.IsChecked.Value : false);
            bool extractTribes = (cb_extractTribes.IsChecked != null && cb_extractTribes.IsChecked.HasValue ? cb_extractTribes.IsChecked.Value : false);

            if (!extractDinos && !extractPlayerPawns && !extractItems && !extractStructures && !extractPlayers && !extractTribes)
            {
                Logger.Instance.Log("Cannot add current extraction settings to preset: No data type selected.", Logger.LogLevel.WARNING);
                MainWindow.ShowToast("No data type selected.", BackgroundColor.WARNING);
                return;
            }

            JsonExportProfile? p = FormatNewJsonExportProfile(_asaSaveFilePath, _mapName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, true);
            if (p == null)
            {
                MainWindow.ShowToast("Failed to format extraction settings.", BackgroundColor.WARNING);
                return;
            }

            if (_jsonExportPresets.Count > 0)
                foreach (JsonExportPreset preset in _jsonExportPresets)
                    if (preset != null && string.Compare(tb_ExistingExtractionPresets_AddTo.Text, preset.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        preset.ExportProfiles.Add(p);
                        SaveJsonExportPresets();
                        RefreshExtractProfilePresetsPopup();
                        MainWindow.ShowToast("Extraction settings added to preset.", BackgroundColor.SUCCESS);
                        return;
                    }

            MainWindow.ShowToast($"Preset \"{tb_ExistingExtractionPresets_AddTo.Text}\" not found.", BackgroundColor.WARNING);
        }

        private void btn_CloseExtractProfilePresetsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ExtractProfilePresetsPopup.IsOpen)
                ExtractProfilePresetsPopup.IsOpen = false;
        }

        private void AddExtractProfileToPresetClicked(object sender, RoutedEventArgs e)
        {
            if (!ExtractProfilePresetsPopup.IsOpen)
            {

                if (string.IsNullOrWhiteSpace(_asaSaveFilePath) || !File.Exists(_asaSaveFilePath))
                {
                    Logger.Instance.Log("Cannot add to preset: Incorrect ASA save file.", Logger.LogLevel.WARNING);
                    MainWindow.ShowToast("Incorrect ASA save file.", BackgroundColor.WARNING);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_mapName))
                {
                    Logger.Instance.Log("Cannot add to preset: Incorrect map name.", Logger.LogLevel.WARNING);
                    MainWindow.ShowToast("Incorrect map name.", BackgroundColor.WARNING);
                    return;
                }

                bool extractDinos = (cb_extractDinos.IsChecked != null && cb_extractDinos.IsChecked.HasValue ? cb_extractDinos.IsChecked.Value : false);
                bool extractPlayerPawns = (cb_extractPlayerPawns.IsChecked != null && cb_extractPlayerPawns.IsChecked.HasValue ? cb_extractPlayerPawns.IsChecked.Value : false);
                bool extractItems = (cb_extractItems.IsChecked != null && cb_extractItems.IsChecked.HasValue ? cb_extractItems.IsChecked.Value : false);
                bool extractStructures = (cb_extractStructures.IsChecked != null && cb_extractStructures.IsChecked.HasValue ? cb_extractStructures.IsChecked.Value : false);
                bool extractPlayers = (cb_extractPlayers.IsChecked != null && cb_extractPlayers.IsChecked.HasValue ? cb_extractPlayers.IsChecked.Value : false);
                bool extractTribes = (cb_extractTribes.IsChecked != null && cb_extractTribes.IsChecked.HasValue ? cb_extractTribes.IsChecked.Value : false);

                if (!extractDinos && !extractPlayerPawns && !extractItems && !extractStructures && !extractPlayers && !extractTribes)
                {
                    Logger.Instance.Log("Cannot add to preset: No data type selected.", Logger.LogLevel.WARNING);
                    MainWindow.ShowToast("No data type selected.", BackgroundColor.WARNING);
                    return;
                }

                RefreshExtractProfilePresetsPopup();
                ExtractProfilePresetsPopup.IsOpen = true;
            }
        }

        private void JsonExportPresetSelected_Extract(string presetName)
        {
            tb_ExportPresetExtract.Text = presetName;
            if (!string.IsNullOrWhiteSpace(presetName) && string.Compare(presetName, "Click here...", StringComparison.InvariantCulture) != 0)
            {
                sp_ExportPresetExtract.Children.Clear();
                if (_jsonExportPresets == null || _jsonExportPresets.Count <= 0)
                    return;
                foreach (JsonExportPreset preset in _jsonExportPresets)
                    if (preset != null && !string.IsNullOrWhiteSpace(preset.Name) && string.Compare(presetName, preset.Name, StringComparison.InvariantCulture) == 0)
                    {
                        if (preset.ExportProfiles != null && preset.ExportProfiles.Count > 0)
                            foreach (var jep in preset.ExportProfiles)
                                if (jep != null)
                                {
                                    Grid grd = new Grid()
                                    {
                                        VerticalAlignment = VerticalAlignment.Top,
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        Margin = new Thickness(0.0d, 5.0d, 0.0d, 0.0d)
                                    };
                                    grd.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(30.0d, GridUnitType.Pixel) });
                                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                                    TextBlock tb1 = new TextBlock()
                                    {
                                        FontSize = 14.0d,
                                        Text = "Extract:",
                                        Margin = new Thickness(0.0d, 7.0d, 0.0d, 0.0d)
                                    };
                                    TextBox tb2 = new TextBox()
                                    {
                                        FontSize = 12.0d,
                                        TextWrapping = TextWrapping.NoWrap,
                                        AcceptsReturn = false,
                                        IsReadOnly = true,
                                        MaxWidth = 1200.0d,
                                        Text = jep.ToString(), //$"Map: {(jep.MapName ?? "")}, FilePath: \"{(jep.SaveFilePath ?? "")}\", Extract: {(jep.ExtractedDinos ? "dinos," : "")}{(jep.ExtractedPlayerPawns ? "pawns," : "")}{(jep.ExtractedItems ? "items," : "")}{(jep.ExtractedStructures ? "structures," : "")}{(jep.ExtractedPlayers ? "players," : "")}{(jep.ExtractedTribes ? "tribes," : "")}",
                                        VerticalAlignment = VerticalAlignment.Center,
                                        HorizontalAlignment = HorizontalAlignment.Left,
                                        Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
                                    };
                                    CheckBox cb = new CheckBox()
                                    {
                                        FontSize = 12.0d,
                                        IsChecked = true,
                                        Content = "",
                                        HorizontalAlignment = HorizontalAlignment.Left,
                                        Width = 30.0d,
                                        MaxWidth = 30.0d,
                                        Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
                                    };
                                    grd.Children.Add(tb1);
                                    Grid.SetRow(tb1, 0);
                                    Grid.SetColumn(tb1, 0);
                                    grd.Children.Add(cb);
                                    Grid.SetRow(cb, 0);
                                    Grid.SetColumn(cb, 1);
                                    grd.Children.Add(tb2);
                                    Grid.SetRow(tb2, 0);
                                    Grid.SetColumn(tb2, 2);

                                    sp_ExportPresetExtract.Children.Add(grd);
                                }
                        break;
                    }
            }
        }

        private void RefreshSelectExportPresetPopup()
        {
            tb_ExportPresetExtract.Text = "Click here...";
            mf_ExportPresetExtract.Items.Clear();
            sp_ExportPresetExtract.Children.Clear();

            if (_jsonExportPresets == null || _jsonExportPresets.Count <= 0)
                return;

            foreach (JsonExportPreset preset in _jsonExportPresets)
                if (preset != null && !string.IsNullOrWhiteSpace(preset.Name))
                {
                    MenuFlyoutItem mfiPreset = new MenuFlyoutItem();
                    mfiPreset.Text = preset.Name;
                    mfiPreset.Click += (s, e1) => { JsonExportPresetSelected_Extract(preset.Name); };
                    mf_ExportPresetExtract.Items.Add(mfiPreset);
                }
        }

        private async void OnCurrentExtractionComplete(List<KeyValuePair<JsonExportProfile, bool>>? extractions)
        {
            if (extractions == null || extractions.Count <= 0)
                return;

            for (int i = 0; i < extractions.Count; i++)
                if (!extractions[i].Value)
                {
                    extractions[i] = new KeyValuePair<JsonExportProfile, bool>(extractions[i].Key, true);
                    await Task.Delay(1000);
                    await DoExtract(false,
                        extractions[i].Key.SaveFilePath,
                        extractions[i].Key.MapName,
                        extractions[i].Key.ExtractedDinos,
                        extractions[i].Key.ExtractedPlayerPawns,
                        extractions[i].Key.ExtractedItems,
                        extractions[i].Key.ExtractedStructures,
                        extractions[i].Key.ExtractedPlayers,
                        extractions[i].Key.ExtractedTribes,
                        extractions[i].Key.FastExtract, 
                        extractions, 
                        OnCurrentExtractionComplete);
                    return;
                }
        }

        private void btn_ExportPresetExtract_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tb_ExportPresetExtract.Text) || string.Compare(tb_ExportPresetExtract.Text, "Click here...", StringComparison.InvariantCulture) == 0)
            {
                MainWindow.ShowToast("No preset selected.", BackgroundColor.WARNING);
                return;
            }

            if (_jsonExportPresets == null || _jsonExportPresets.Count <= 0)
                return;

            foreach (JsonExportPreset preset in _jsonExportPresets)
                if (preset != null && !string.IsNullOrWhiteSpace(preset.Name) && string.Compare(tb_ExportPresetExtract.Text, preset.Name, StringComparison.InvariantCulture) == 0)
                {
                    if (preset.ExportProfiles != null && preset.ExportProfiles.Count > 0)
                    {
                        Dictionary<string, bool> selected = new Dictionary<string, bool>();
                        if (sp_ExportPresetExtract.Children != null && sp_ExportPresetExtract.Children.Count > 0)
                            foreach (var obj in sp_ExportPresetExtract.Children)
                            {
                                Grid? grd = obj as Grid;
                                if (grd != null)
                                {
                                    CheckBox? cb = grd.Children[1] as CheckBox;
                                    TextBox? tb = grd.Children[2] as TextBox;
                                    if (cb != null && tb != null)
                                        selected.Add(tb.Text, (cb.IsChecked != null && cb.IsChecked.HasValue && cb.IsChecked.Value));
                                }
                            }
                        if (selected.Count <= 0)
                        {
                            MainWindow.ShowToast("No export profile selected.", BackgroundColor.WARNING);
                            return;
                        }
                        List<KeyValuePair<JsonExportProfile, bool>> toExtract = new List<KeyValuePair<JsonExportProfile, bool>>();
                        foreach (JsonExportProfile jep in preset.ExportProfiles)
                            if (jep != null)
                            {
                                bool doAdd = false;
                                string jepDescription = jep.ToString();
                                foreach (var selection in selected)
                                    if (string.Compare(jepDescription, selection.Key, StringComparison.InvariantCulture) == 0)
                                    {
                                        doAdd = selection.Value;
                                        break;
                                    }
                                if (doAdd)
                                    toExtract.Add(new KeyValuePair<JsonExportProfile, bool>(jep, false));
                            }
                        if (toExtract.Count > 0)
                        {
                            if (SelectExportPresetPopup.IsOpen)
                                SelectExportPresetPopup.IsOpen = false;
                            if (SettingsPage._page != null)
                                SettingsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                {
                                    await Task.Delay(250);
                                    OnCurrentExtractionComplete(toExtract);
                                });
                            MainWindow.ShowToast("Extraction started. Please wait.", BackgroundColor.SUCCESS);
                        }
                        else
                            MainWindow.ShowToast("Preset is empty.", BackgroundColor.WARNING);
                    }
                    else
                        MainWindow.ShowToast("Preset is empty.", BackgroundColor.WARNING);
                    return;
                }

            MainWindow.ShowToast($"Preset \"{tb_ExportPresetExtract.Text}\" not found.", BackgroundColor.WARNING);
        }

        private void mfi_JsonDataArkParseWithPreset_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectExportPresetPopup.IsOpen)
            {
                RefreshSelectExportPresetPopup();
                SelectExportPresetPopup.IsOpen = true;
            }
        }

        private void btn_CloseSelectExportPresetPopup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectExportPresetPopup.IsOpen)
                SelectExportPresetPopup.IsOpen = false;
        }

        #endregion
    }
}
