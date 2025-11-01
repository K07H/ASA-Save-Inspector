using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.ObjectModelUtils;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ASA_Save_Inspector.Pages
{
    public class JsonExportProfile : INotifyPropertyChanged
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        public int ID { get; set; } = -1;
        public string ExtractName { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public string LabelCreationDate => this.CreationDate.ToString(ASILang.Get("MicrosoftDateTimeFormat"), ASILang.GetCultureInfo());
        public bool ExtractedPlayerPawns { get; set; } = false;
        public bool ExtractedDinos { get; set; } = false;
        public bool ExtractedStructures { get; set; } = false;
        public bool ExtractedItems { get; set; } = false;
        public bool ExtractedPlayers { get; set; } = false;
        public bool ExtractedTribes { get; set; } = false;
        public bool FastExtract { get; set; } = false;
        public string SaveFilePath { get; set; } = string.Empty;

        public string LabelLoad => ASILang.Get("Load");
        public string LabelRemove => ASILang.Get("Remove");

        public string GetLabel() => $"{ID.ToString(CultureInfo.InvariantCulture)}{(string.IsNullOrWhiteSpace(ExtractName) ? string.Empty : $" - {ExtractName}")} - {MapName} ({CreationDate.ToString("yyyy-MM-dd HH\\hmm\\mss\\s")})";
        public string GetExportFolderName() => (Directory.Exists(SaveFilePath) ? $"{SaveFilePath}" : $"{ID.ToString(CultureInfo.InvariantCulture)}_{MapName}");
        public override string ToString() => $"{ASILang.Get("Name")}: {(ExtractName ?? "")}, {ASILang.Get("Map")}: {(MapName ?? "")}, {ASILang.Get("Path")}: \"{(SaveFilePath ?? "")}\", {ASILang.Get("Extracts")}: {(ExtractedDinos ? $"{ASILang.Get("Dinos")}," : "")}{(ExtractedPlayerPawns ? $"{ASILang.Get("Pawns")}," : "")}{(ExtractedItems ? $"{ASILang.Get("Items")}," : "")}{(ExtractedStructures ? $"{ASILang.Get("Structures")}," : "")}{(ExtractedPlayers ? $"{ASILang.Get("Players")}," : "")}{(ExtractedTribes ? $"{ASILang.Get("Tribes")}," : "")}";
    }

    public class JsonExportPreset
    {
        public string Name { get; set; } = string.Empty;
        public List<JsonExportProfile> ExportProfiles { get; set; } = new List<JsonExportProfile>();
    }

    public class JsonSettings
    {
        public int Language { get; set; } = -1; // Deprecated.
        public string? LanguageCode { get; set; } = ASILang.DEFAULT_LANGUAGE_CODE;
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
        public string MapName { get; set; } = ASILang.Get("Unknown");
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

    public class ButtonLabels
    {
        public string LabelLoad { get; set; } = ASILang.Get("Load");
        public string LabelRemove { get; set; } = ASILang.Get("Remove");
    }

    public sealed partial class SettingsPage : Page, INotifyPropertyChanged
    {
        private const double _scrollBarWidth = 24.0d;
        private const double _marginLeft = 10.0d;

        private static readonly object lockObject = new object();

        public static SettingsPage? _page = null;
        public static bool? _darkTheme = true;

        public static string? _language = ASILang.DEFAULT_LANGUAGE_CODE;
        public static string? _pythonExePath = null;
        public static string? _jsonDataFolderPath = null;
        public static string? _asaSaveFilePath = null;
        public static string? _mapName = null;

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

        public static Dictionary<int, string>? _tribeNames = null;

        public static Dictionary<string, int> _allTribesForDinos = new Dictionary<string, int>();
        public static List<string> _allTribesForDinosSorted = new List<string>();
        public static bool _allTribesForDinosInitialized = false;
        public static List<string?> _allShortNamesForDinosSorted = new List<string?>();
        public static bool _allShortNamesForDinosInitialized = false;

        public static Dictionary<string, int> _allTribesForStructures = new Dictionary<string, int>();
        public static List<string> _allTribesForStructuresSorted = new List<string>();
        public static bool _allTribesForStructuresInitialized = false;
        public static List<string?> _allShortNamesForStructuresSorted = new List<string?>();
        public static bool _allShortNamesForStructuresInitialized = false;

        public static Dictionary<string, int> _allTribesForItems = new Dictionary<string, int>();
        public static List<string> _allTribesForItemsSorted = new List<string>();
        public static bool _allTribesForItemsInitialized = false;
        public static List<string?> _allShortNamesForItemsSorted = new List<string?>();
        public static bool _allShortNamesForItemsInitialized = false;

        public static string? _currentlyLoadedMapName = null;

        public static CustomBlueprints _customBlueprints = new CustomBlueprints();

        private static SolidColorBrush _errorColor = new SolidColorBrush(Colors.Red);
        private static SolidColorBrush _warningColor = new SolidColorBrush(Colors.Orange);
        private static SolidColorBrush _successColor = new SolidColorBrush(Colors.Green);
        private static SolidColorBrush _defaultColorDarkTheme = new SolidColorBrush(Colors.White);
        private static SolidColorBrush _defaultColorLightTheme = new SolidColorBrush(Colors.Black);

        public IEnumerable<GroupInfoCollection<string, JsonExportProfile>>? _lastDisplayedVM = null;

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

            if (ASILang._languages.Count > 0)
                foreach (var lang in ASILang._languages)
                    if (!string.IsNullOrEmpty(lang.Key) && lang.Value != null)
                    {
                        string flagFilepath = (lang.Value.FlagFilepath.StartsWith("/Assets/", StringComparison.InvariantCulture) ? $"ms-appx://{lang.Value.FlagFilepath}" : lang.Value.FlagFilepath);
                        BitmapImage? flagImage = ASILang.GetFlagImage(flagFilepath);

                        MenuFlyoutItem mfi = new MenuFlyoutItem();
                        mfi.Text = lang.Value.Name;
                        if (flagImage != null)
                            mfi.Icon = new ImageIcon() { Source = flagImage };
                        mfi.Click += (s, e1) =>
                        {
                            _language = lang.Value.Code;
                            r_LanguageSelect.Text = lang.Value.Name;
                            i_LanguageSelect.Source = flagImage;
                            LanguageChanged();

#pragma warning disable CS1998
                            if (MainWindow._mainWindow != null)
                            {
                                MainWindow._mainWindow.LanguageChanged();
                                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                {
                                    if (MainWindow._mainWindow != null)
                                    {
                                        if (MainWindow._mainWindow._navView != null)
                                            MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnAbout;
                                        MainWindow._mainWindow.NavView_Navigate(typeof(AboutPage), new EntranceNavigationTransitionInfo());
                                    }
                                    await Task.Delay(250);
                                    if (MainWindow._mainWindow != null)
                                    {
                                        if (MainWindow._mainWindow._navView != null)
                                            MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnSettings;
                                        MainWindow._mainWindow.NavView_Navigate(typeof(SettingsPage), new EntranceNavigationTransitionInfo());
                                    }
                                });
                            }
#pragma warning restore CS1998
                        };

                        mf_LanguageSlect.Items.Add(mfi);
                    }

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
                Text = ASILang.Get("ManualSelection"),
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
                    mf_MapNameSelect.Items.Add(newMenuItem);
                }

            LoadJsonExportProfiles();

            LoadSettings();
            LoadJsonExportPresets();
            LoadCustomBlueprints();
            AsaSaveFilePathChanged();
            MapNameChanged();
            if (_selectedJsonExportProfile != null)
                JsonExportProfileSelected(_selectedJsonExportProfile);

            InitDatagrid(false, false);
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
            InitDatagrid(true, false);
        }

#pragma warning disable CS8625
        public void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
#pragma warning restore CS8625

        public void SettingsChanged()
        {
            PythonPathChanged();
            LanguageChanged();
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
                    _language = ASILang.GetLanguageCode(jsonSettings.LanguageCode);
                    _darkTheme = jsonSettings.DarkTheme;
                    SettingsChanged();
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
                    _language = ASILang.GetLanguageCode(jsonSettings.LanguageCode);
                    _darkTheme = jsonSettings.DarkTheme;
                    if (_page != null)
                        _ = _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                        {
                            await Task.Delay(250);
                            _page.SettingsChanged();
                        });
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
                    LanguageCode = ASILang.GetLanguageCode(_language),
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

        public void JsonExportProfileSelected(JsonExportProfile? jsonProfile)
        {
            _selectedJsonExportProfile = jsonProfile;
            if (_selectedJsonExportProfile == null)
                return;

            _asaSaveFilePath = _selectedJsonExportProfile.SaveFilePath;
            _mapName = _selectedJsonExportProfile.MapName;
            AsaSaveFilePathChanged();
            MapNameChanged();
            DataTypesSelectionChanged(_selectedJsonExportProfile);
            tb_ExtractionName.Text = _selectedJsonExportProfile.ExtractName;
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

        public static JsonExportProfile? FormatNewJsonExportProfile(string? saveFilePath, string? mapName, string? extractName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract)
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
                ExtractName = (string.IsNullOrWhiteSpace(extractName) ? string.Empty :  extractName),
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

        public static JsonExportProfile? AddNewJsonExportProfile(string? saveFilePath, string? mapName, string? extractName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract)
        {
            JsonExportProfile? p = FormatNewJsonExportProfile(saveFilePath, mapName, extractName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, fastExtract);
            if (p == null)
                return null;

            if (_jsonExportProfiles == null)
                return null;

            _jsonExportProfiles.Add(p);
            SettingsPage.SaveJsonExportProfiles();
            return p;
        }

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

        public void PythonPathChanged()
        {
            if (!string.IsNullOrWhiteSpace(_pythonExePath))
            {
                tb_PythonSelect.Text = _pythonExePath;
                SaveSettings();
                PythonManager.InstallArkParse();
                if (_page != null)
                    _ = _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                    {
                        await Task.Delay(250);
                        _page.tb_PythonSelect.Text = _pythonExePath;
                    });
            }
        }

        public void LanguageChanged()
        {
            if (_language != null)
            {
                ASILangFile? lang = (ASILang._languages.ContainsKey(_language) ? ASILang._languages[_language] : null);
                if (lang != null)
                {
                    string flagFilepath = (lang.FlagFilepath.StartsWith("/Assets/", StringComparison.InvariantCulture) ? $"ms-appx://{lang.FlagFilepath}" : lang.FlagFilepath);
                    var flagImage = new BitmapImage(new Uri(flagFilepath, UriKind.Absolute));

                    r_LanguageSelect.Text = lang.Name;
                    i_LanguageSelect.Source = flagImage;
                    ASILang.SwitchLanguage(lang.Code);
                    SaveSettings();
                }
                else
                    Logger.Instance.Log($"Language {_language} was not found.", Logger.LogLevel.ERROR);
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
        private async Task DoExtract(bool useJsonResultTb, string? saveFilePath, string? mapName, string? extractName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract, List<KeyValuePair<JsonExportProfile, bool>>? extractions = null, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
        {
            if (string.IsNullOrWhiteSpace(saveFilePath) || !File.Exists(saveFilePath))
            {
                Logger.Instance.Log(ASILang.Get("CannotExtractJsonData_IncorrectSaveFilePath"), Logger.LogLevel.WARNING);
                if (useJsonResultTb)
                {
                    tb_ExtractJsonResult.Text = ASILang.Get("CannotExtractJsonData_IncorrectSaveFilePath");
                    tb_ExtractJsonResult.Foreground = _errorColor;
                    tb_ExtractJsonResult.Visibility = Visibility.Visible;
                }
                else
                    MainWindow.ShowToast(ASILang.Get("IncorrectASASaveFile"), BackgroundColor.WARNING);
                return;
            }

            if (string.IsNullOrWhiteSpace(mapName))
            {
                Logger.Instance.Log(ASILang.Get("CannotExtractJsonData_IncorrectMapName"), Logger.LogLevel.WARNING);
                if (useJsonResultTb)
                {
                    tb_ExtractJsonResult.Text = ASILang.Get("CannotExtractJsonData_IncorrectMapName");
                    tb_ExtractJsonResult.Foreground = _errorColor;
                    tb_ExtractJsonResult.Visibility = Visibility.Visible;
                }
                else
                    MainWindow.ShowToast(ASILang.Get("IncorrectMapName"), BackgroundColor.WARNING);
                return;
            }

            if (!extractDinos && !extractPlayerPawns && !extractItems && !extractStructures && !extractPlayers && !extractTribes)
            {
                Logger.Instance.Log(ASILang.Get("CannotExtractJsonData_NoDataTypeSelected"), Logger.LogLevel.WARNING);
                if (useJsonResultTb)
                {
                    tb_ExtractJsonResult.Text = ASILang.Get("CannotExtractJsonData_NoDataTypeSelected");
                    tb_ExtractJsonResult.Foreground = _errorColor;
                    tb_ExtractJsonResult.Visibility = Visibility.Visible;
                }
                else
                    MainWindow.ShowToast(ASILang.Get("NoDataTypeSelected"), BackgroundColor.WARNING);
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
                    extractName,
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
                Logger.Instance.Log(ASILang.Get("JsonDataExtractionFailed"), Logger.LogLevel.ERROR);
                if (useJsonResultTb)
                {
                    tb_ExtractJsonResult.Text = ASILang.Get("JsonDataExtractionFailed");
                    tb_ExtractJsonResult.Foreground = _errorColor;
                    tb_ExtractJsonResult.Visibility = Visibility.Visible;
                }
                else
                    MainWindow.ShowToast(ASILang.Get("JsonDataExtractionFailed"), BackgroundColor.WARNING);
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

            bool fastExtract = true;

            await DoExtract(true, _asaSaveFilePath, _mapName, tb_ExtractionName.Text, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, fastExtract);
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

        private void btn_ExtractWithSaveFile_Click(object sender, RoutedEventArgs e)
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

        private List<T>? DeserializeJsonObjects<T>(string filepath)
        {
            if (!File.Exists(filepath))
                Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_FileNotFound").Replace("#FILEPATH#", $"\"{filepath}\"", StringComparison.InvariantCulture)}", Logger.LogLevel.WARNING);
            else
            {
                try
                {
                    List<T> result = new List<T>();
                    string objBegin = "    {";
                    string objEnd = "    }";
                    string currentObj = string.Empty;
                    using (FileStream fs = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (BufferedStream bs = new BufferedStream(fs))
                        {
                            using (StreamReader sr = new StreamReader(bs))
                            {
                                string? line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    if (line == objBegin)
                                        currentObj = "{\r\n";
                                    else if (line.StartsWith(objEnd))
                                    {
                                        if (currentObj.Length > 0)
                                            try
                                            {
                                                T? obj = JsonSerializer.Deserialize<T>(currentObj + "\r\n}");
                                                if (obj != null)
                                                    result.Add(obj);
                                            }
                                            catch { }
                                        currentObj = string.Empty;
                                    }
                                    else if (currentObj.Length > 0)
                                        currentObj += line;
                                }
                            }
                        }
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_FileParsingError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                }
            }
            return null;
        }

        /*
        private async Task<List<T>?> DeserializeJsonObjectsAsync<T>(string filepath)
        {
            if (!File.Exists(filepath))
                Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_FileNotFound").Replace("#FILEPATH#", $"\"{filepath}\"", StringComparison.InvariantCulture)}", Logger.LogLevel.WARNING);
            else
            {
                try
                {
                    using (FileStream fs = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        return await JsonSerializer.DeserializeAsync<List<T>?>(fs);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_FileParsingError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                }
            }
            return null;
        }
        */

        private static void RemoveJsonExportProfile(JsonExportProfile jep)
        {
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

        public static int DeleteJsonExportProfile(JsonExportProfile? jep)
        {
            if (jep == null)
                return 0;

            string folderPath = jep.GetExportFolderName();
            if (!Directory.Exists(folderPath))
                folderPath = Path.Combine(Utils.JsonExportsFolder(), jep.GetExportFolderName());

            if (!Directory.Exists(folderPath))
                Logger.Instance.Log($"{ASILang.Get("RemoveJsonDataFailed_AlreadyRemoved").Replace("#DIRECTORY_PATH#", $"\"{folderPath}\"", StringComparison.InvariantCulture)}", Logger.LogLevel.ERROR);

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
                    Logger.Instance.Log($"{ASILang.Get("RemoveJsonDataFailed")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return -1;
                }

            if (toRemove.Count > 0)
            {
                foreach (var jepToRemove in toRemove)
                    RemoveJsonExportProfile(jepToRemove);
                SaveJsonExportProfiles();
                return 1;
            }
            return 0;
        }

        public void ValidateJsonFolderRemovalClicked(object sender, RoutedEventArgs e)
        {
            if (ConfirmJsonExportRemovalPopup.IsOpen)
                ConfirmJsonExportRemovalPopup.IsOpen = false;

            if (_selectedJsonExportProfile == null)
            {
                Logger.Instance.Log(ASILang.Get("RemoveJsonDataFailed_NoExportProfileSelected"), Logger.LogLevel.ERROR);
                tb_JsonDataLoaded.Text = ASILang.Get("RemoveJsonDataFailed_NoExportProfileSelected");
                tb_JsonDataLoaded.Foreground = _errorColor;
                tb_JsonDataLoaded.Visibility = Visibility.Visible;
                return;
            }

            int ret = DeleteJsonExportProfile(_selectedJsonExportProfile);
            if (ret == -1)
            {
                tb_JsonDataLoaded.Text = ASILang.Get("RemoveJsonDataFailed_Details");
                tb_JsonDataLoaded.Foreground = _errorColor;
                tb_JsonDataLoaded.Visibility = Visibility.Visible;
                return;
            }
            else if (ret == 1)
                InitDatagrid(false, false);

            _selectedJsonExportProfile = null;
        }

        public void CloseJsonFolderRemovalPopupClicked(object sender, RoutedEventArgs e)
        {
            if (ConfirmJsonExportRemovalPopup.IsOpen)
                ConfirmJsonExportRemovalPopup.IsOpen = false;
        }

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

        private List<TextBox> GetAllJEPAsTextBoxesForPreset(string presetName)
        {
            List<TextBox> result = new List<TextBox>();
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
                                        Text = jep.ToString(),
                                        VerticalAlignment = VerticalAlignment.Center,
                                        HorizontalAlignment = HorizontalAlignment.Left,
                                        Margin = new Thickness(0.0d, 5.0d, 0.0d, 0.0d)
                                    };
                                    result.Add(tb);
                                }
                        break;
                    }
            return result;
        }

        private void JsonExportPresetSelected_Details(string presetName)
        {
            tb_ExistingExtractionPresets_Details.Text = presetName;
            sp_ExistingExtractionPresetDetails.Children.Clear();
            List<TextBox> jeps = GetAllJEPAsTextBoxesForPreset(presetName);
            if (jeps != null && jeps.Count > 0)
                foreach (TextBox tb in jeps)
                    sp_ExistingExtractionPresetDetails.Children.Add(tb);
        }

        private void JsonExportPresetSelected_Details_B(string presetName)
        {
            tb_ExistingExtractionPresets_Details_B.Text = presetName;
            sp_ExistingExtractionPresetDetails_B.Children.Clear();
            List<TextBox> jeps = GetAllJEPAsTextBoxesForPreset(presetName);
            if (jeps != null && jeps.Count > 0)
                foreach (TextBox tb in jeps)
                    sp_ExistingExtractionPresetDetails_B.Children.Add(tb);
        }

        private void RefreshExtractProfilePresetsPopup()
        {
            tb_ExistingExtractionPresets_Details.Text = ASILang.Get("ClickHere");
            sp_ExistingExtractionPresetDetails.Children.Clear();
            mf_ExistingExtractionPresets_Details.Items.Clear();

            tb_ExistingExtractionPresets_AddTo.Text = ASILang.Get("ClickHere");
            btn_ExistingExtractionPresets_AddTo.IsEnabled = false;
            mf_ExistingExtractionPresets_AddTo.Items.Clear();

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
                }
        }

        private void RefreshManageJEPPresetsPopup()
        {
            tb_ExistingExtractionPresets_Details_B.Text = ASILang.Get("ClickHere");
            sp_ExistingExtractionPresetDetails_B.Children.Clear();
            mf_ExistingExtractionPresets_Details_B.Items.Clear();

            tb_ExistingExtractionPresets_Remove.Text = ASILang.Get("ClickHere");
            btn_RemoveExistingExtractionPreset.IsEnabled = false;
            mf_ExistingExtractionPresets_Remove.Items.Clear();

            tb_CreateExtractionPreset_B.Text = "";
            btn_CreateExtractionPreset_B.IsEnabled = false;

            if (_jsonExportPresets == null || _jsonExportPresets.Count <= 0)
                return;

            foreach (JsonExportPreset preset in _jsonExportPresets)
                if (preset != null && !string.IsNullOrWhiteSpace(preset.Name))
                {
                    MenuFlyoutItem mfiDetails = new MenuFlyoutItem();
                    mfiDetails.Text = preset.Name;
                    mfiDetails.Click += (s, e1) => { JsonExportPresetSelected_Details_B(preset.Name); };
                    mf_ExistingExtractionPresets_Details_B.Items.Add(mfiDetails);

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

        private void tb_CreateExtractionPreset_B_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_CreateExtractionPreset_B.IsEnabled = (!string.IsNullOrWhiteSpace(tb_CreateExtractionPreset_B.Text) && tb_CreateExtractionPreset_B.Text.Length > 0);
        }

        private void CreateExtractionPreset(string? presetName)
        {
            if (_jsonExportPresets == null)
                return;

            if (string.IsNullOrWhiteSpace(presetName))
            {
                MainWindow.ShowToast(ASILang.Get("ExtractPreset_NameRequired"), BackgroundColor.WARNING);
                return;
            }

            if (_jsonExportPresets.Count > 0)
                foreach (JsonExportPreset preset in _jsonExportPresets)
                    if (preset != null && string.Compare(presetName, preset.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        MainWindow.ShowToast(ASILang.Get("PresetNameAlreadyExists"), BackgroundColor.WARNING);
                        return;
                    }

            _jsonExportPresets.Add(new JsonExportPreset() { Name = presetName });
            SaveJsonExportPresets();
            MainWindow.ShowToast(ASILang.Get("ExtractPreset_Created"), BackgroundColor.SUCCESS);
        }

        private void btn_CreateExtractionPreset_Click(object sender, RoutedEventArgs e)
        {
            CreateExtractionPreset(tb_CreateExtractionPreset.Text);
            RefreshExtractProfilePresetsPopup();
        }

        private void btn_CreateExtractionPreset_B_Click(object sender, RoutedEventArgs e)
        {
            CreateExtractionPreset(tb_CreateExtractionPreset_B.Text);
            RefreshManageJEPPresetsPopup();
        }

        private void btn_RemoveExistingExtractionPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonExportPresets == null)
                return;

            if (string.IsNullOrWhiteSpace(tb_ExistingExtractionPresets_Remove.Text) || string.Compare(tb_ExistingExtractionPresets_Remove.Text, ASILang.Get("ClickHere"), StringComparison.InvariantCulture) == 0)
            {
                MainWindow.ShowToast(ASILang.Get("ExtractPreset_NoPresetSelected"), BackgroundColor.WARNING);
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
                    RefreshManageJEPPresetsPopup();
                    MainWindow.ShowToast(ASILang.Get("ExtractPreset_Removed"), BackgroundColor.SUCCESS);
                    return;
                }
            }

            MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", $"\"{tb_ExistingExtractionPresets_Remove.Text}\"", StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
        }

        private void btn_ExistingExtractionPresets_AddTo_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonExportPresets == null)
                return;

            if (string.IsNullOrWhiteSpace(tb_ExistingExtractionPresets_AddTo.Text) || string.Compare(tb_ExistingExtractionPresets_AddTo.Text, ASILang.Get("ClickHere"), StringComparison.InvariantCulture) == 0)
            {
                MainWindow.ShowToast(ASILang.Get("ExtractPreset_NoPresetSelected"), BackgroundColor.WARNING);
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
                Logger.Instance.Log(ASILang.Get("ExtractPreset_CannotAddExtractProfile_NoDataTypeSelected"), Logger.LogLevel.WARNING);
                MainWindow.ShowToast(ASILang.Get("ExtractPreset_NoDataTypeSelected"), BackgroundColor.WARNING);
                return;
            }

            JsonExportProfile? p = FormatNewJsonExportProfile(_asaSaveFilePath, _mapName, tb_ExtractionName.Text, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, true);
            if (p == null)
            {
                MainWindow.ShowToast(ASILang.Get("ExtractPreset_FailedToFormatExtractProfile"), BackgroundColor.WARNING);
                return;
            }

            if (_jsonExportPresets.Count > 0)
                foreach (JsonExportPreset preset in _jsonExportPresets)
                    if (preset != null && string.Compare(tb_ExistingExtractionPresets_AddTo.Text, preset.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        preset.ExportProfiles.Add(p);
                        SaveJsonExportPresets();
                        RefreshExtractProfilePresetsPopup();
                        MainWindow.ShowToast(ASILang.Get("ExtractPreset_ExtractProfileAdded"), BackgroundColor.SUCCESS);
                        return;
                    }

            MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", $"\"{tb_ExistingExtractionPresets_AddTo.Text}\"", StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
        }

        private void btn_CloseExtractProfilePresetsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ExtractProfilePresetsPopup.IsOpen)
                ExtractProfilePresetsPopup.IsOpen = false;
        }

        private void btn_CloseManageJEPPresetsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ManageJEPPresetsPopup.IsOpen)
                ManageJEPPresetsPopup.IsOpen = false;
        }

        private void btn_ManagePresets_Click(object sender, RoutedEventArgs e)
        {
            if (!ManageJEPPresetsPopup.IsOpen)
            {
                RefreshManageJEPPresetsPopup();
                ManageJEPPresetsPopup.IsOpen = true;
            }
        }

        private void AddExtractProfileToPresetClicked(object sender, RoutedEventArgs e)
        {
            if (!ExtractProfilePresetsPopup.IsOpen)
            {
                if (string.IsNullOrWhiteSpace(_asaSaveFilePath) || !File.Exists(_asaSaveFilePath))
                {
                    Logger.Instance.Log($"{ASILang.Get("CannotAddToPreset")} {ASILang.Get("IncorrectSaveFile")}", Logger.LogLevel.WARNING);
                    MainWindow.ShowToast(ASILang.Get("IncorrectSaveFile"), BackgroundColor.WARNING);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_mapName))
                {
                    Logger.Instance.Log($"{ASILang.Get("CannotAddToPreset")} {ASILang.Get("IncorrectMapName")}", Logger.LogLevel.WARNING);
                    MainWindow.ShowToast(ASILang.Get("IncorrectMapName"), BackgroundColor.WARNING);
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
                    Logger.Instance.Log($"{ASILang.Get("CannotAddToPreset")} {ASILang.Get("NoDataTypeSelected")}", Logger.LogLevel.WARNING);
                    MainWindow.ShowToast(ASILang.Get("NoDataTypeSelected"), BackgroundColor.WARNING);
                    return;
                }

                RefreshExtractProfilePresetsPopup();
                ExtractProfilePresetsPopup.IsOpen = true;
            }
        }

        private void JsonExportPresetSelected_Extract(string presetName)
        {
            tb_ExportPresetExtract.Text = presetName;
            if (!string.IsNullOrWhiteSpace(presetName) && string.Compare(presetName, ASILang.Get("ClickHere"), StringComparison.InvariantCulture) != 0)
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
                                        Text = $"{ASILang.Get("Extract")}:",
                                        Margin = new Thickness(0.0d, 7.0d, 0.0d, 0.0d)
                                    };
                                    TextBox tb2 = new TextBox()
                                    {
                                        FontSize = 12.0d,
                                        TextWrapping = TextWrapping.NoWrap,
                                        AcceptsReturn = false,
                                        IsReadOnly = true,
                                        MaxWidth = 1200.0d,
                                        Text = jep.ToString(),
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
            tb_ExportPresetExtract.Text = ASILang.Get("ClickHere");
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
                        extractions[i].Key.ExtractName,
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
            if (string.IsNullOrWhiteSpace(tb_ExportPresetExtract.Text) || string.Compare(tb_ExportPresetExtract.Text, ASILang.Get("ClickHere"), StringComparison.InvariantCulture) == 0)
            {
                MainWindow.ShowToast(ASILang.Get("NoPresetSelected"), BackgroundColor.WARNING);
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
                            MainWindow.ShowToast(ASILang.Get("NoExportProfileSelected"), BackgroundColor.WARNING);
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
                                {
                                    if (string.IsNullOrWhiteSpace(jep.ExtractName) && !string.IsNullOrWhiteSpace(preset.Name))
                                        jep.ExtractName = preset.Name;
                                    toExtract.Add(new KeyValuePair<JsonExportProfile, bool>(jep, false));
                                }
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
                            MainWindow.ShowToast($"{ASILang.Get("ExtractionStarted")} {ASILang.Get("PleaseWait")}", BackgroundColor.SUCCESS);
                        }
                        else
                            MainWindow.ShowToast(ASILang.Get("PresetIsEmpty"), BackgroundColor.WARNING);
                    }
                    else
                        MainWindow.ShowToast(ASILang.Get("PresetIsEmpty"), BackgroundColor.WARNING);
                    return;
                }

            MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", $"\"{tb_ExportPresetExtract.Text}\"", StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
        }

        private void btn_ExtractWithPreset_Click(object sender, RoutedEventArgs e)
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

        #region JEP datagrid

        private string _prevSort = "ID";
        private bool _sortAscending = false;

        public void InitDatagrid(bool onlyRefresh, bool updateSortDir)
        {
            dg_JEP.MaxHeight = 300.0d;
            dg_JEP.MaxWidth = Math.Max(1.0d, gr_SettingsPage.ActualWidth - (_scrollBarWidth + _marginLeft + 4.0d));
            if (!onlyRefresh)
                GroupAndSortItems("ID", updateSortDir);
        }

        private void dg_JEP_AutoGeneratingColumn(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e?.Column?.Header != null)
            {
                string? currentColumn = e.Column.Header.ToString();
                if (currentColumn == null)
                    return;
                if (currentColumn == "ExtractName")
                    e.Column.Header = ASILang.Get("Name");
                else if (currentColumn == "SaveFilePath")
                    e.Column.Header = ASILang.Get("Path");
                else if (currentColumn == "MapName")
                    e.Column.Header = ASILang.Get("MapName");
                else if (currentColumn == "LabelCreationDate")
                    e.Column.Header = ASILang.Get("CreationDate");
                else if (currentColumn == "ExtractedDinos")
                    e.Column.Header = ASILang.Get("Dinos");
                else if (currentColumn == "ExtractedPlayerPawns")
                    e.Column.Header = ASILang.Get("Pawns");
                else if (currentColumn == "ExtractedItems")
                    e.Column.Header = ASILang.Get("Items");
                else if (currentColumn == "ExtractedStructures")
                    e.Column.Header = ASILang.Get("Structures");
                else if (currentColumn == "ExtractedPlayers")
                    e.Column.Header = ASILang.Get("Players");
                else if (currentColumn == "ExtractedTribes")
                    e.Column.Header = ASILang.Get("Tribes");
                else if (currentColumn == "CreationDate" || currentColumn == "FastExtract" || currentColumn == "LabelLoad" || currentColumn == "LabelRemove")
                    e.Cancel = true;
            }
        }

        private void dg_JEP_Sorting(object sender, DataGridColumnEventArgs e)
        {
            if (e?.Column?.Header == null)
                return;

            string? currentColumn = e.Column.Header.ToString();
            if (currentColumn == null)
                return;

            GroupAndSortItems(currentColumn, true);
        }

        private void dg_JEP_LoadingRowGroup(object sender, DataGridRowGroupHeaderEventArgs e)
        {
            if (e?.RowGroupHeader == null)
                return;

            ICollectionViewGroup? group = e.RowGroupHeader.CollectionViewGroup;
            if (group == null || group.GroupItems == null || group.GroupItems.Count <= 0)
                return;

            JsonExportProfile? item = group.GroupItems[0] as JsonExportProfile;
            if (item == null)
                return;

            e.RowGroupHeader.PropertyName = ASILang.Get("File");
            e.RowGroupHeader.PropertyValue = $"{item.SaveFilePath} ({group.GroupItems.Count.ToString(CultureInfo.InvariantCulture)} {ASILang.Get("RowsGroupItems")})";
            e.RowGroupHeader.ItemCountVisibility = Visibility.Collapsed;
        }

        private void AdjustSortDirection(string currentColumn)
        {
            if (_prevSort == currentColumn)
                _sortAscending = !_sortAscending;
            else
                _sortAscending = true;
        }

        private void GroupAndSortItems(string currentColumn, bool updateSortDir)
        {
            bool sorted = false;
            if (currentColumn == "ID")
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.ID).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.ID).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Name"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.ExtractName).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.ExtractName).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Path"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.SaveFilePath).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.SaveFilePath).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("MapName"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.MapName).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.MapName).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("CreationDate"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.CreationDate).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.CreationDate).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Dinos"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.ExtractedDinos).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.ExtractedDinos).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Pawns"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.ExtractedPlayerPawns).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.ExtractedPlayerPawns).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Items"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.ExtractedItems).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.ExtractedItems).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Structures"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.ExtractedStructures).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.ExtractedStructures).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Players"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.ExtractedPlayers).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.ExtractedPlayers).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Tribes"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = _jsonExportProfiles.OrderBy(o => o.ExtractedTribes).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>((list.Count() > 0 ? list.First().SaveFilePath : key), list));
                else
                    _lastDisplayedVM = _jsonExportProfiles.OrderByDescending(o => o.ExtractedTribes).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }

            if (sorted)
            {
                CollectionViewSource groupedItems = new()
                {
                    IsSourceGrouped = true,
                    Source = _lastDisplayedVM
                };
                dg_JEP.ItemsSource = groupedItems.View;
                if (updateSortDir)
                    _prevSort = currentColumn;
            }
        }

        private JsonExportProfile? GetJEPFromButton(RoutedEventArgs e)
        {
            if (e == null || e.OriginalSource == null)
                return null;
            Button? btn = e.OriginalSource as Button;
            if (btn == null)
                return null;
            StackPanel? sp = btn.Parent as StackPanel;
            if (sp == null)
                return null;
            DataGridCell? dgc = sp.Parent as DataGridCell;
            if (dgc == null)
                return null;
            JsonExportProfile? jep = dgc.DataContext as JsonExportProfile;
            if (jep == null)
                return null;
            return jep;
        }

        private const int NB_PROCESSES = 8;
        private static int _nbProcessedDinosSubsets = 0;
        private static List<Dino> _processedDinosSubsets = new List<Dino>();
        private static int _nbProcessedStructuresSubsets = 0;
        private static List<Structure> _processedStructuresSubsets = new List<Structure>();
        private static int _nbProcessedItemsSubsets = 0;
        private static List<Item> _processedItemsSubsets = new List<Item>();

        private static string GetTribeNameOrId(Dino d) => SettingsPage._tribeNames.ContainsKey(d.OwningTribeID) ? SettingsPage._tribeNames[d.OwningTribeID] : (!string.IsNullOrEmpty(d.TribeName) ? d.TribeName : d.OwningTribeID.ToString(CultureInfo.InvariantCulture));

        private static void GotAllTribeIdsForDinos(List<Dino> dinos)
        {
            _allTribesForDinos = dinos.DistinctBy(d => d.OwningTribeID).Select(d => new KeyValuePair<string, int>(GetTribeNameOrId(d), d.OwningTribeID)).DistinctBy(e => e.Key).ToDictionary();
            if (_allTribesForDinos != null && _allTribesForDinos.Count > 0)
            {
                _allTribesForDinosSorted = _allTribesForDinos.Select(t => t.Key).ToList();
                if (_allTribesForDinosSorted != null && _allTribesForDinosSorted.Count > 0)
                    _allTribesForDinosSorted.Sort();
            }
            _allTribesForDinosInitialized = true;
            if (DinosPage._page != null)
                DinosPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => DinosPage._page.InitTribesQuickFilter());
        }

        private static void DinosSubsetProcessed(List<Dino> dinos)
        {
            if (dinos != null && dinos.Count > 0)
                foreach (var d in dinos)
                    _processedDinosSubsets.Add(d);
            ++_nbProcessedDinosSubsets;
            if (_nbProcessedDinosSubsets == NB_PROCESSES)
                GotAllTribeIdsForDinos(_processedDinosSubsets);
        }

        private static void ProcessDinosSubset(List<Dino> dinos)
        {
            Task.Run(() =>
            {
                return dinos.DistinctBy(d => d.OwningTribeID).ToList();
            }).ContinueWith((result) =>
            {
                DinosSubsetProcessed(result.Result);
            });
        }

        private static void ComputeAllTribesForDinos()
        {
            if (_dinosData != null && _dinosData.Count > 0)
            {
                _nbProcessedDinosSubsets = 0;
                _processedDinosSubsets.Clear();
                int nbElems = _dinosData.Count / NB_PROCESSES;
                int remaining = _dinosData.Count % NB_PROCESSES;
                for (int i = 0; i < NB_PROCESSES; i++)
                {
                    int toSkip = (i * nbElems);
                    int toTake = (i == (NB_PROCESSES - 1) ? nbElems + remaining : nbElems);
                    ProcessDinosSubset(_dinosData.Skip(toSkip).Take(toTake).ToList());
                }
            }
        }

        private static void ComputeAllShortNamesForDinos()
        {
            if (_dinosData != null && _dinosData.Count > 0)
            {
                _allShortNamesForDinosSorted = _dinosData.DistinctBy(d => d.ShortName).Where(d => d.ShortName != null).Select(d => d.ShortName).ToList();
                _allShortNamesForDinosSorted.Sort();
            }
        }

        private static void InitializeDinosQuickFilters()
        {
            _allTribesForDinosInitialized = false;
            _allTribesForDinos.Clear();
            _allTribesForDinosSorted.Clear();
            _allShortNamesForDinosInitialized = false;
            _allShortNamesForDinosSorted.Clear();
            Task.Run(() => ComputeAllTribesForDinos());
            Task.Run(() => ComputeAllShortNamesForDinos()).ContinueWith((result) =>
            {
                _allShortNamesForDinosInitialized = true;
                if (DinosPage._page != null)
                    DinosPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => DinosPage._page.InitDinosQuickFilter());
            });
        }

        private static string GetTribeNameOrId_Fallback(Structure s) => !string.IsNullOrEmpty(s.OwnerName) ? s.OwnerName : (!string.IsNullOrEmpty(s.OwningPlayerName) ? s.OwningPlayerName : (s.TargetingTeam != null && s.TargetingTeam.HasValue ? s.TargetingTeam.Value.ToString(CultureInfo.InvariantCulture) : string.Empty));
        private static string GetTribeNameOrId(Structure s) => s.TargetingTeam != null && s.TargetingTeam.HasValue && SettingsPage._tribeNames.ContainsKey(s.TargetingTeam.Value) ? SettingsPage._tribeNames[s.TargetingTeam.Value] : GetTribeNameOrId_Fallback(s);

        private static void GotAllTribeIdsForStructures(List<Structure> structures)
        {
            _allTribesForStructures = structures.DistinctBy(d => d.TargetingTeam).Where(s => s.TargetingTeam != null && s.TargetingTeam.HasValue).Select(s => new KeyValuePair<string, int>(GetTribeNameOrId(s), s.TargetingTeam.Value)).DistinctBy(e => e.Key).ToDictionary();
            if (_allTribesForStructures != null && _allTribesForStructures.Count > 0)
            {
                _allTribesForStructuresSorted = _allTribesForStructures.Select(t => t.Key).ToList();
                if (_allTribesForStructuresSorted != null && _allTribesForStructuresSorted.Count > 0)
                    _allTribesForStructuresSorted.Sort();
            }
            _allTribesForStructuresInitialized = true;
            if (StructuresPage._page != null)
                StructuresPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => StructuresPage._page.InitTribesQuickFilter());
        }

        private static void StructuresSubsetProcessed(List<Structure> structures)
        {
            if (structures != null && structures.Count > 0)
                foreach (var s in structures)
                    _processedStructuresSubsets.Add(s);
            ++_nbProcessedStructuresSubsets;
            if (_nbProcessedStructuresSubsets == NB_PROCESSES)
                GotAllTribeIdsForStructures(_processedStructuresSubsets);
        }

        private static void ProcessStructuresSubset(List<Structure> structures)
        {
            Task.Run(() =>
            {
                return structures.DistinctBy(s => s.TargetingTeam).Where(s => s.TargetingTeam != null && s.TargetingTeam.HasValue).ToList();
            }).ContinueWith((result) =>
            {
                StructuresSubsetProcessed(result.Result);
            });
        }

        private static void ComputeAllTribesForStructures()
        {
            if (_structuresData != null && _structuresData.Count > 0)
            {
                _nbProcessedStructuresSubsets = 0;
                _processedStructuresSubsets.Clear();
                int nbElems = _structuresData.Count / NB_PROCESSES;
                int remaining = _structuresData.Count % NB_PROCESSES;
                for (int i = 0; i < NB_PROCESSES; i++)
                {
                    int toSkip = (i * nbElems);
                    int toTake = (i == (NB_PROCESSES - 1) ? nbElems + remaining : nbElems);
                    ProcessStructuresSubset(_structuresData.Skip(toSkip).Take(toTake).ToList());
                }
            }
        }

        private static void ComputeAllShortNamesForStructures()
        {
            if (_structuresData != null && _structuresData.Count > 0)
            {
                _allShortNamesForStructuresSorted = _structuresData.DistinctBy(s => s.ShortName).Where(s => s.ShortName != null).Select(s => s.ShortName).ToList();
                _allShortNamesForStructuresSorted.Sort();
            }
        }

        private static void InitializeStructuresQuickFilters()
        {
            _allTribesForStructuresInitialized = false;
            _allTribesForStructures.Clear();
            _allTribesForStructuresSorted.Clear();
            _allShortNamesForStructuresInitialized = false;
            _allShortNamesForStructuresSorted.Clear();
            Task.Run(() => ComputeAllTribesForStructures());
            Task.Run(() => ComputeAllShortNamesForStructures()).ContinueWith((result) =>
            {
                _allShortNamesForStructuresInitialized = true;
                if (StructuresPage._page != null)
                    StructuresPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => StructuresPage._page.InitStructuresQuickFilter());
            });
        }

        private static string GetTribeNameOrId(Item i) => i.ContainerTribeID != null && i.ContainerTribeID.HasValue && SettingsPage._tribeNames.ContainsKey(i.ContainerTribeID.Value) ? SettingsPage._tribeNames[i.ContainerTribeID.Value] : (i.ContainerTribeID != null && i.ContainerTribeID.HasValue ? i.ContainerTribeID.Value.ToString(CultureInfo.InvariantCulture) : "0");

        private static void GotAllTribeIdsForItems(List<Item> items)
        {
            _allTribesForItems = items.Where(i => i.ContainerTribeID != null && i.ContainerTribeID.HasValue).DistinctBy(i => i.ContainerTribeID).Select(i => new KeyValuePair<string, int>(GetTribeNameOrId(i), i.ContainerTribeID.Value)).DistinctBy(e => e.Key).ToDictionary();
            if (_allTribesForItems != null && _allTribesForItems.Count > 0)
            {
                _allTribesForItemsSorted = _allTribesForItems.Select(t => t.Key).ToList();
                if (_allTribesForItemsSorted != null && _allTribesForItemsSorted.Count > 0)
                    _allTribesForItemsSorted.Sort();
            }
            _allTribesForItemsInitialized = true;
            if (ItemsPage._page != null)
                ItemsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => ItemsPage._page.InitTribesQuickFilter());
        }

        private static void ItemsSubsetProcessed(List<Item> items)
        {
            if (items != null && items.Count > 0)
                foreach (var i in items)
                    _processedItemsSubsets.Add(i);
            ++_nbProcessedItemsSubsets;
            if (_nbProcessedItemsSubsets == NB_PROCESSES)
                GotAllTribeIdsForItems(_processedItemsSubsets);
        }

        private static void ProcessItemsSubset(List<Item> items)
        {
            Task.Run(() =>
            {
                return items.DistinctBy(d => d.ContainerTribeID).ToList();
            }).ContinueWith((result) =>
            {
                ItemsSubsetProcessed(result.Result);
            });
        }

        private static void ComputeAllTribesForItems()
        {
            if (_itemsData != null && _itemsData.Count > 0)
            {
                _nbProcessedItemsSubsets = 0;
                _processedItemsSubsets.Clear();
                int nbElems = _itemsData.Count / NB_PROCESSES;
                int remaining = _itemsData.Count % NB_PROCESSES;
                for (int i = 0; i < NB_PROCESSES; i++)
                {
                    int toSkip = (i * nbElems);
                    int toTake = (i == (NB_PROCESSES - 1) ? nbElems + remaining : nbElems);
                    ProcessItemsSubset(_itemsData.Skip(toSkip).Take(toTake).ToList());
                }
            }
        }

        private static void ComputeAllShortNamesForItems()
        {
            if (_itemsData != null && _itemsData.Count > 0)
            {
                _allShortNamesForItemsSorted = _itemsData.DistinctBy(i => i.ShortName).Where(i => i.ShortName != null).Select(i => i.ShortName).ToList();
                _allShortNamesForItemsSorted.Sort();
            }
        }

        private static void InitializeItemsQuickFilters()
        {
            _allTribesForItemsInitialized = false;
            _allTribesForItems.Clear();
            _allTribesForItemsSorted.Clear();
            _allShortNamesForItemsInitialized = false;
            _allShortNamesForItemsSorted.Clear();
            Task.Run(() => ComputeAllTribesForItems());
            Task.Run(() => ComputeAllShortNamesForItems()).ContinueWith((result) =>
            {
                _allShortNamesForItemsInitialized = true;
                if (ItemsPage._page != null)
                    ItemsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => ItemsPage._page.InitItemsQuickFilter());
            });
        }

        private static Dictionary<string, bool> _deserializationTasks = new Dictionary<string, bool>()
        {
            { "dinos", false },
            { "player_pawns", false },
            { "items", false },
            { "structures", false },
            { "players", false },
            { "tribes", false },
        };

        private static bool _deserializationHasErrors = false;

        private static void ResetDeserializationTasks()
        {
            _deserializationTasks["dinos"] = false;
            _deserializationTasks["player_pawns"] = false;
            _deserializationTasks["items"] = false;
            _deserializationTasks["structures"] = false;
            _deserializationTasks["players"] = false;
            _deserializationTasks["tribes"] = false;
            _deserializationHasErrors = false;
        }

        private void CheckForTasksCompletion()
        {
            if (_deserializationTasks["dinos"] == false ||
                _deserializationTasks["player_pawns"] == false ||
                _deserializationTasks["items"] == false ||
                _deserializationTasks["structures"] == false ||
                _deserializationTasks["players"] == false ||
                _deserializationTasks["tribes"] == false)
                return;

            // All tasks completed.

            InitializeDinosQuickFilters();
            InitializeStructuresQuickFilters();
            InitializeItemsQuickFilters();

            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => AllTasksCompleted());
        }

        private void AllTasksCompleted()
        {
            if (_deserializationHasErrors)
            {
                tb_JsonDataLoaded.Text = ASILang.Get("LoadJsonData_PartiallyLoaded");
                tb_JsonDataLoaded.Foreground = _warningColor;
            }
            else
            {
                tb_JsonDataLoaded.Text = ASILang.Get("LoadJsonData_Success");
                tb_JsonDataLoaded.Foreground = _successColor;
            }
            tb_JsonDataLoaded.Visibility = Visibility.Visible;
            Utils.LockAllPages(false);
        }

        private void DeserializeAllData_WithBufferedStream(string folderPath)
        {
            if (_selectedJsonExportProfile == null)
                return;

            if (_selectedJsonExportProfile.ExtractedDinos)
                Task.Run(() => DeserializeJsonObjects<Dino>(Path.Combine(folderPath, "dinos.json"))).ContinueWith((result) =>
                {
                    _deserializationTasks["dinos"] = true;
                    _dinosData = result.Result;
                    if (_dinosData == null)
                        _deserializationHasErrors = true;
                    else if (_dinosData.Count > 0)
                        foreach (var dinoData in _dinosData)
                            dinoData.InitStats();
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["dinos"] = true;

            if (_selectedJsonExportProfile.ExtractedPlayerPawns)
                Task.Run(() => DeserializeJsonObjects<PlayerPawn>(Path.Combine(folderPath, "player_pawns.json"))).ContinueWith((result) =>
                {
                    _deserializationTasks["player_pawns"] = true;
                    _playerPawnsData = result.Result;
                    if (_playerPawnsData == null)
                        _deserializationHasErrors = true;
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["player_pawns"] = true;

            if (_selectedJsonExportProfile.ExtractedItems)
                Task.Run(() => DeserializeJsonObjects<Item>(Path.Combine(folderPath, "items.json"))).ContinueWith((result) =>
                {
                    _deserializationTasks["items"] = true;
                    _itemsData = result.Result;
                    if (_itemsData == null)
                        _deserializationHasErrors = true;
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["items"] = true;

            if (_selectedJsonExportProfile.ExtractedStructures)
                Task.Run(() => DeserializeJsonObjects<Structure>(Path.Combine(folderPath, "structures.json"))).ContinueWith((result) =>
                {
                    _deserializationTasks["structures"] = true;
                    _structuresData = result.Result;
                    if (_structuresData == null)
                        _deserializationHasErrors = true;
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["structures"] = true;

            if (_selectedJsonExportProfile.ExtractedPlayers)
                Task.Run(() => DeserializeJsonObjects<Player>(Path.Combine(folderPath, "players.json"))).ContinueWith((result) =>
                {
                    _deserializationTasks["players"] = true;
                    _playersData = result.Result;
                    if (_playersData == null)
                        _deserializationHasErrors = true;
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["players"] = true;

            if (_selectedJsonExportProfile.ExtractedTribes)
                Task.Run(() => DeserializeJsonObjects<Tribe>(Path.Combine(folderPath, "tribes.json"))).ContinueWith((result) =>
                {
                    _deserializationTasks["tribes"] = true;
                    _tribesData = result.Result;
                    if (_tribesData == null)
                        _deserializationHasErrors = true;
                    if (_tribesData != null && _tribesData.Count > 0)
                        _tribeNames = _tribesData.DistinctBy(t => t.TribeID).Select(t => new KeyValuePair<int, string>(t.TribeID != null && t.TribeID.HasValue ? t.TribeID.Value : -1, t.TribeName ?? string.Empty)).Where(s => s.Key != -1 && !string.IsNullOrEmpty(s.Value)).ToDictionary();
                    if (_tribeNames == null)
                        _tribeNames = new Dictionary<int, string>();
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["tribes"] = true;
        }

        /*
        private void DeserializeAllData(string folderPath)
        {
            if (_selectedJsonExportProfile == null)
                return;

            if (_selectedJsonExportProfile.ExtractedDinos)
                DeserializeJsonObjectsAsync<Dino>(Path.Combine(folderPath, "dinos.json")).ContinueWith((result) =>
                {
                    _deserializationTasks["dinos"] = true;
                    _dinosData = result.Result;
                    if (_dinosData == null)
                        _deserializationHasErrors = true;
                    else if (_dinosData.Count > 0)
                        foreach (var dinoData in _dinosData)
                            dinoData.InitStats();
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["dinos"] = true;

            if (_selectedJsonExportProfile.ExtractedPlayerPawns)
                DeserializeJsonObjectsAsync<PlayerPawn>(Path.Combine(folderPath, "player_pawns.json")).ContinueWith((result) =>
                {
                    _deserializationTasks["player_pawns"] = true;
                    _playerPawnsData = result.Result;
                    if (_playerPawnsData == null)
                        _deserializationHasErrors = true;
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["player_pawns"] = true;

            if (_selectedJsonExportProfile.ExtractedItems)
                DeserializeJsonObjectsAsync<Item>(Path.Combine(folderPath, "items.json")).ContinueWith((result) =>
                {
                    _deserializationTasks["items"] = true;
                    _itemsData = result.Result;
                    if (_itemsData == null)
                        _deserializationHasErrors = true;
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["items"] = true;

            if (_selectedJsonExportProfile.ExtractedStructures)
                DeserializeJsonObjectsAsync<Structure>(Path.Combine(folderPath, "structures.json")).ContinueWith((result) =>
                {
                    _deserializationTasks["structures"] = true;
                    _structuresData = result.Result;
                    if (_structuresData == null)
                        _deserializationHasErrors = true;
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["structures"] = true;

            if (_selectedJsonExportProfile.ExtractedPlayers)
                DeserializeJsonObjectsAsync<Player>(Path.Combine(folderPath, "players.json")).ContinueWith((result) =>
                {
                    _deserializationTasks["players"] = true;
                    _playersData = result.Result;
                    if (_playersData == null)
                        _deserializationHasErrors = true;
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["players"] = true;

            if (_selectedJsonExportProfile.ExtractedTribes)
                DeserializeJsonObjectsAsync<Tribe>(Path.Combine(folderPath, "tribes.json")).ContinueWith((result) =>
                {
                    _deserializationTasks["tribes"] = true;
                    _tribesData = result.Result;
                    if (_tribesData == null)
                        _deserializationHasErrors = true;
                    if (_tribesData != null && _tribesData.Count > 0)
                        _tribeNames = _tribesData.DistinctBy(t => t.TribeID).Select(t => new KeyValuePair<int, string>(t.TribeID != null && t.TribeID.HasValue ? t.TribeID.Value : -1, t.TribeName ?? string.Empty)).Where(s => s.Key != -1 && !string.IsNullOrEmpty(s.Value)).ToDictionary();
                    if (_tribeNames == null)
                        _tribeNames = new Dictionary<int, string>();
                    CheckForTasksCompletion();
                });
            else
                _deserializationTasks["tribes"] = true;
        }
        */

        private void LoadJsonExportProfile_Click(object sender, RoutedEventArgs e)
        {
            JsonExportProfile? jep = GetJEPFromButton(e);
            if (jep == null)
                return;

            JsonExportProfileSelected(jep);

            if (_selectedJsonExportProfile == null)
            {
                Logger.Instance.Log(ASILang.Get("LoadJsonFailed_NoExportProfileSelected"), Logger.LogLevel.ERROR);
                tb_JsonDataLoaded.Text = ASILang.Get("LoadJsonFailed_NoExportProfileSelected");
                tb_JsonDataLoaded.Foreground = _errorColor;
                tb_JsonDataLoaded.Visibility = Visibility.Visible;
                return;
            }

            string folderPath = _selectedJsonExportProfile.GetExportFolderName();
            if (!Directory.Exists(folderPath))
                folderPath = Path.Combine(Utils.JsonExportsFolder(), _selectedJsonExportProfile.GetExportFolderName());
            if (!Directory.Exists(folderPath))
            {
                Logger.Instance.Log(ASILang.Get("LoadJsonFailed_ExportFolderNotFound"), Logger.LogLevel.ERROR);
                tb_JsonDataLoaded.Text = ASILang.Get("LoadJsonFailed_ExportFolderNotFound");
                tb_JsonDataLoaded.Foreground = _errorColor;
                tb_JsonDataLoaded.Visibility = Visibility.Visible;
                return;
            }

            Utils.ClearAllPagesFiltersAndGroups();
            _allTribesForDinosSorted.Clear();
            _allTribesForDinos.Clear();

            bool refreshMinimap = (string.Compare(_currentlyLoadedMapName, _selectedJsonExportProfile.MapName, StringComparison.InvariantCulture) != 0);
            _currentlyLoadedMapName = _selectedJsonExportProfile.MapName;
            if (refreshMinimap && MainWindow._minimap != null)
                MainWindow.OpenMinimap();

            string saveInfoFilePath = Path.Combine(folderPath, "save_info.json");
            if (!File.Exists(saveInfoFilePath))
            {
                Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_SaveFileInfoNotFound").Replace("#FILEPATH#", $"\"{saveInfoFilePath}\"", StringComparison.InvariantCulture)}", Logger.LogLevel.WARNING);
                _deserializationHasErrors = true;
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
                    Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_SaveFileInfoParsingError")}. Exception=[{ex}]", Logger.LogLevel.ERROR);
                    _deserializationHasErrors = true;
                }
            }

            Utils.LockAllPages(true);
            ResetDeserializationTasks();
            //DeserializeAllData(folderPath);
            DeserializeAllData_WithBufferedStream(folderPath);
            CheckForTasksCompletion();

            tb_JsonDataLoaded.Text = $"{ASILang.Get("LoadingJsonData")} {ASILang.Get("PleaseWait")}";
            tb_JsonDataLoaded.Foreground = _darkTheme != null && _darkTheme.HasValue && _darkTheme.Value == false ? _defaultColorLightTheme : _defaultColorDarkTheme;
            tb_JsonDataLoaded.Visibility = Visibility.Visible;
        }

        private void RemoveJsonExportProfile_Click(object sender, RoutedEventArgs e)
        {
            JsonExportProfile? jep = GetJEPFromButton(e);
            if (jep == null)
                return;

            JsonExportProfileSelected(jep);

            if (_selectedJsonExportProfile != null && !ConfirmJsonExportRemovalPopup.IsOpen)
            {
                string folderPath = _selectedJsonExportProfile.GetExportFolderName();
                if (!Directory.Exists(folderPath))
                    folderPath = Path.Combine(Utils.JsonExportsFolder(), _selectedJsonExportProfile.GetExportFolderName());

                tb_confirmJsonFolderRemoval.Text = $"{ASILang.Get("JsonDataRemovalConfirm_Description").Replace("#DIRECTORY_PATH#", $"\"{folderPath}\"", StringComparison.InvariantCulture)}";
                ConfirmJsonExportRemovalPopup.IsOpen = true;
            }
        }

        #endregion
    }
}
