using ASA_Save_Inspector.ObjectModel;
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
using System.Collections.Generic;
using System.ComponentModel;
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
        public string GetExportFolderName() => (IsDeprecatedFormat() ? $"{SaveFilePath}" : $"{ID.ToString(CultureInfo.InvariantCulture)}_{MapName}");

        /// <summary>True for profiles using the deprecated format, where SaveFilePath is the (absolute) path of a folder.</summary>
        public bool IsDeprecatedFormat() => !string.IsNullOrWhiteSpace(SaveFilePath) && Path.IsPathRooted(SaveFilePath) && Directory.Exists(SaveFilePath);

        /// <summary>Absolute path of the JSON export folder.</summary>
        public string GetExportFolderPath() => Path.Combine(Utils.JsonExportsFolder(), GetExportFolderName());

        public bool IsExtractionComplete()
        {
            try
            {
                if (IsDeprecatedFormat())
                    return false;
                string folderPath = GetExportFolderPath();
                if (!Directory.Exists(folderPath))
                    return false;
                bool FileOk(bool extracted, string fileName) => !extracted || File.Exists(Path.Combine(folderPath, fileName));
                return FileOk(ExtractedDinos, "dinos.json") &&
                    FileOk(ExtractedPlayerPawns, "player_pawns.json") &&
                    FileOk(ExtractedItems, "items.json") &&
                    FileOk(ExtractedStructures, "structures.json") &&
                    FileOk(ExtractedPlayers, "players.json") &&
                    FileOk(ExtractedTribes, "tribes.json");
            }
            catch
            {
                return false;
            }
        }

        public override string ToString() => $"{ASILang.Get("Name")}: {(ExtractName ?? string.Empty)}, {ASILang.Get("Map")}: {(MapName ?? string.Empty)}, {ASILang.Get("Path")}: '{(SaveFilePath ?? string.Empty)}', {ASILang.Get("Extracts")}: {(ExtractedDinos ? $"{ASILang.Get("Dinos")}," : string.Empty)}{(ExtractedPlayerPawns ? $"{ASILang.Get("Pawns")}," : string.Empty)}{(ExtractedItems ? $"{ASILang.Get("Items")}," : string.Empty)}{(ExtractedStructures ? $"{ASILang.Get("Structures")}," : string.Empty)}{(ExtractedPlayers ? $"{ASILang.Get("Players")}," : string.Empty)}{(ExtractedTribes ? $"{ASILang.Get("Tribes")}," : string.Empty)}";
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
        public bool? DebugLogging { get; set; } = false;
        public bool? NoPythonVenv { get; set; } = false;
        public string? DefaultColumnsPreset_PlayerPawns { get; set; } = null;
        public string? DefaultColumnsPreset_Dinos { get; set; } = null;
        public string? DefaultColumnsPreset_Items { get; set; } = null;
        public string? DefaultColumnsPreset_Structures { get; set; } = null;
        public string? DefaultColumnsPreset_Players { get; set; } = null;
        public string? DefaultColumnsPreset_Tribes { get; set; } = null;
        public string? ClusterDataFolderPath { get; set; } = null;
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

        public string? SubMapName { get; set; } = null;
        public List<ArkMapInfo>? SubMinimaps { get; set; } = null;

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
        private const double _scrollBarWidth = 24.0d;
        private const double _marginLeft = 10.0d;

        private static readonly object lockObject = new object();

        public static SettingsPage? _page = null;
        public static bool? _darkTheme = true;
        public static bool? _noPythonVenv = false;
        public static bool? _debugLogging = false;

        public static string? _defaultColumnsPreset_PlayerPawns = null;
        public static string? _defaultColumnsPreset_Dinos = null;
        public static string? _defaultColumnsPreset_Items = null;
        public static string? _defaultColumnsPreset_Structures = null;
        public static string? _defaultColumnsPreset_Players = null;
        public static string? _defaultColumnsPreset_Tribes = null;

        public static string? _language = ASILang.DEFAULT_LANGUAGE_CODE;
        public static string? _pythonExePath = null;
        public static string? _asaSaveFilePath = null;
        public static string? _mapName = null;

        public static List<JsonExportProfile> _jsonExportProfiles = new List<JsonExportProfile>();

        public static readonly object _jsonExportProfilesLock = new object();

        public static List<JsonExportProfile> GetJsonExportProfilesSnapshot()
        {
            lock (_jsonExportProfilesLock)
                return (_jsonExportProfiles != null ? new List<JsonExportProfile>(_jsonExportProfiles) : new List<JsonExportProfile>());
        }

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
        public static List<string?> _allCleanNamesForDinosSorted = new List<string?>();
        public static bool _allCleanNamesForDinosInitialized = false;

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
        public static string? _currentlyLoadedSubMapName = null;

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

                        mf_LanguageSelect.Items.Add(mfi);
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

            MainWindow.SettingsPageFinishedLoading = true;
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
                    _debugLogging = jsonSettings.DebugLogging;
                    _noPythonVenv = jsonSettings.NoPythonVenv;
                    _defaultColumnsPreset_PlayerPawns = jsonSettings.DefaultColumnsPreset_PlayerPawns;
                    _defaultColumnsPreset_Dinos = jsonSettings.DefaultColumnsPreset_Dinos;
                    _defaultColumnsPreset_Items = jsonSettings.DefaultColumnsPreset_Items;
                    _defaultColumnsPreset_Structures = jsonSettings.DefaultColumnsPreset_Structures;
                    _defaultColumnsPreset_Players = jsonSettings.DefaultColumnsPreset_Players;
                    _defaultColumnsPreset_Tribes = jsonSettings.DefaultColumnsPreset_Tribes;
                    SettingsChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadSettings. Exception=[{ex}]", Logger.LogLevel.ERROR);
                // Invalid JSON: keep the file aside, otherwise the next save would overwrite it (and its content would be lost).
                if (ex is JsonException)
                    Utils.RenameInvalidUserFile(settingsPath, ex);
            }
        }

        /// <summary>Loads settings statically.</summary>
        /// <param name="pythonOrLanguageRefresh">Set this to true if Python path changed, if language changed, or if Python vEnv needs to be activated/updated.</param>
        public static void LoadSettingsStatic(bool pythonOrLanguageRefresh = true)
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
                    _debugLogging = jsonSettings.DebugLogging;
                    _noPythonVenv = jsonSettings.NoPythonVenv;
                    _defaultColumnsPreset_PlayerPawns = jsonSettings.DefaultColumnsPreset_PlayerPawns;
                    _defaultColumnsPreset_Dinos = jsonSettings.DefaultColumnsPreset_Dinos;
                    _defaultColumnsPreset_Items = jsonSettings.DefaultColumnsPreset_Items;
                    _defaultColumnsPreset_Structures = jsonSettings.DefaultColumnsPreset_Structures;
                    _defaultColumnsPreset_Players = jsonSettings.DefaultColumnsPreset_Players;
                    _defaultColumnsPreset_Tribes = jsonSettings.DefaultColumnsPreset_Tribes;
                    ClusterDataPage.SelectedClusterDataFolderPath = jsonSettings.ClusterDataFolderPath;
                    if (pythonOrLanguageRefresh)
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
                // Invalid JSON: keep the file aside, otherwise the next save would overwrite it (and its content would be lost).
                if (ex is JsonException)
                    Utils.RenameInvalidUserFile(settingsPath, ex);
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
                    DarkTheme = _darkTheme,
                    DebugLogging = _debugLogging,
                    NoPythonVenv = _noPythonVenv,
                    DefaultColumnsPreset_PlayerPawns = _defaultColumnsPreset_PlayerPawns,
                    DefaultColumnsPreset_Dinos = _defaultColumnsPreset_Dinos,
                    DefaultColumnsPreset_Items = _defaultColumnsPreset_Items,
                    DefaultColumnsPreset_Structures = _defaultColumnsPreset_Structures,
                    DefaultColumnsPreset_Players = _defaultColumnsPreset_Players,
                    DefaultColumnsPreset_Tribes = _defaultColumnsPreset_Tribes,
                    ClusterDataFolderPath = ClusterDataPage.SelectedClusterDataFolderPath
                };
                string jsonString = JsonSerializer.Serialize<JsonSettings>(settings, new JsonSerializerOptions() { WriteIndented = true });
                Utils.WriteAllTextAtomic(Utils.SettingsFilePath(), jsonString);
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
                // Invalid JSON: keep the file aside, otherwise the next save would overwrite it (and its content would be lost).
                if (ex is JsonException)
                    Utils.RenameInvalidUserFile(customBlueprintsFilepath, ex);
            }
        }

        public static void SaveCustomBlueprints()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize<CustomBlueprints>(_customBlueprints, new JsonSerializerOptions() { WriteIndented = true });
                Utils.WriteAllTextAtomic(Utils.CustomBlueprintsFilePath(), jsonString);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SaveCustomBlueprints. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        /// <summary>
        /// Writes the custom blueprints in a JSON file that is given to the export script, and returns its path (or an empty string if there is no custom blueprint).
        /// They used to be given as a base64 command line argument: it could exceed the command line length limit of cmd.exe (8191 characters),
        /// and blueprints containing the ";" or "|" separators were split.
        /// </summary>
        public static string WriteCustomBlueprintsForExport()
        {
            string filePath = Utils.CustomBlueprintsForExportFilePath();
            CustomBlueprints toExport = new CustomBlueprints()
            {
                Dinos = SettingsPage._customBlueprints?.Dinos?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>(),
                Items = SettingsPage._customBlueprints?.Items?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>(),
                Structures = SettingsPage._customBlueprints?.Structures?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>()
            };

            try
            {
                if (toExport.Dinos.Count <= 0 && toExport.Items.Count <= 0 && toExport.Structures.Count <= 0)
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                    return string.Empty;
                }
                Utils.WriteAllTextAtomic(filePath, JsonSerializer.Serialize<CustomBlueprints>(toExport, new JsonSerializerOptions() { WriteIndented = true }));
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in WriteCustomBlueprintsForExport. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            return string.Empty;
        }

        public static bool UsePythonVenv() => (SettingsPage._noPythonVenv == null || !SettingsPage._noPythonVenv.HasValue || !SettingsPage._noPythonVenv.Value);

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

            try
            {
                string exportProfilesJson = File.ReadAllText(exportProfilesPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(exportProfilesJson))
                    return;

                List<JsonExportProfile>? exportProfiles = JsonSerializer.Deserialize<List<JsonExportProfile>>(exportProfilesJson);
                if (exportProfiles != null)
                    lock (_jsonExportProfilesLock)
                        _jsonExportProfiles = exportProfiles;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadJsonExportProfiles. Exception=[{ex}]", Logger.LogLevel.ERROR);
                // Invalid JSON: keep the file aside, otherwise the next save would overwrite it (and its content would be lost).
                if (ex is JsonException)
                    Utils.RenameInvalidUserFile(exportProfilesPath, ex);
            }
        }

        public static void SaveJsonExportProfiles()
        {
            try
            {
                // Serialized and written under the lock: the list cannot change during serialization, and writes happen in order.
                lock (_jsonExportProfilesLock)
                {
                    string jsonString = JsonSerializer.Serialize<List<JsonExportProfile>>(_jsonExportProfiles, new JsonSerializerOptions() { WriteIndented = true });
                    Utils.WriteAllTextAtomic(Utils.ExportProfilesFilePath(), jsonString);
                }
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
            lock (_jsonExportProfilesLock)
            {
                if (_jsonExportProfiles != null && _jsonExportProfiles.Count > 0)
                    foreach (var jsonProfile in _jsonExportProfiles)
                        if (jsonProfile != null && jsonProfile.ID > highestId)
                            highestId = jsonProfile.ID;
            }
            // Also skip the IDs of the existing export folders ("[ID]_[MapName]"): if export_profiles.json has been lost or renamed,
            // a new extraction must not reuse (and overwrite) the folder of a previous one.
            try
            {
                string exportsFolder = Utils.JsonExportsFolder();
                if (Directory.Exists(exportsFolder))
                    foreach (string dir in Directory.GetDirectories(exportsFolder))
                    {
                        string dirName = Path.GetFileName(dir);
                        int sep = dirName.IndexOf('_', StringComparison.Ordinal);
                        if (sep > 0 && int.TryParse(dirName.AsSpan(0, sep), NumberStyles.None, CultureInfo.InvariantCulture, out int dirId) && dirId > highestId)
                            highestId = dirId;
                    }
            }
            catch (Exception ignoredEx) { Utils.LogIgnoredException("SettingsPage.FormatNewJsonExportProfile", ignoredEx); }

            return new JsonExportProfile()
            {
                ID = (highestId + 1),
                SaveFilePath = saveFilePath,
                MapName = mapName,
                ExtractName = (string.IsNullOrWhiteSpace(extractName) ? string.Empty : extractName),
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
            JsonExportProfile? p = null;
            // ID computation and insertion are done under the same lock, so that two extractions cannot get the same ID.
            lock (_jsonExportProfilesLock)
            {
                p = FormatNewJsonExportProfile(saveFilePath, mapName, extractName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, fastExtract);
                if (p == null)
                    return null;

                if (_jsonExportProfiles == null)
                    return null;

                _jsonExportProfiles.Add(p);
                SettingsPage.SaveJsonExportProfiles();
            }
            return p;
        }

        [RelayCommand]
        private void PythonSelectPath(string path)
        {
            if (path == "MANUAL")
                OpenPythonFilePicker().FireAndForget(nameof(OpenPythonFilePicker)); // Don't wait on purpose.
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
                PythonManager.InstallPythonVenv();
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

        private void SelectASASaveFileClicked(object sender, RoutedEventArgs e) => OpenAsaSaveFilePicker().FireAndForget(nameof(OpenAsaSaveFilePicker)); // Don't wait on purpose.

        private bool _isExtracting = false;
        private async Task DoExtract(bool useJsonResultTb, string? saveFilePath, string? mapName, string? extractName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract, List<KeyValuePair<JsonExportProfile, bool>>? extractions = null, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
        {
            bool started = await DoExtractCore(useJsonResultTb, saveFilePath, mapName, extractName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, fastExtract, extractions, callback);
            // When an extraction cannot be started, PythonManager never calls the "extraction complete" callback.
            // Call it here, otherwise the rest of the preset is never extracted and the CLI waits until timeout.
            if (!started && extractions != null && callback != null)
            {
                MainWindow.PresetExtractionFailed = true;
                callback(extractions);
            }
        }

        private async Task<bool> DoExtractCore(bool useJsonResultTb, string? saveFilePath, string? mapName, string? extractName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract, List<KeyValuePair<JsonExportProfile, bool>>? extractions = null, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
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
                return false;
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
                return false;
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
                return false;
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
            return ret;
        }

        private async void ExtractASASaveFileClicked(object sender, RoutedEventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SettingsPage.ExtractASASaveFileClicked. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
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
            if (UsePythonVenv())
            {
                if (string.IsNullOrWhiteSpace(_pythonExePath) || !File.Exists(_pythonExePath) || !File.Exists(Utils.PythonFilePathFromVenv()))
                {
                    tb_CannotExtractWithArkParse.Visibility = Visibility.Visible;
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_pythonExePath) || !File.Exists(_pythonExePath))
                {
                    tb_CannotExtractWithArkParse.Visibility = Visibility.Visible;
                    return;
                }
            }

            tb_CannotExtractWithArkParse.Visibility = Visibility.Collapsed;
#pragma warning disable CS8625
            ShowArkParserPopupOffsetClicked(null, null);
#pragma warning restore CS8625
        }

        private static async Task<List<T>?> DeserializeJsonObjectsAsync<T>(string filepath)
        {
            if (!File.Exists(filepath))
            {
                Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_FileNotFound").Replace("#FILEPATH#", filepath ?? string.Empty, StringComparison.InvariantCulture)}", Logger.LogLevel.WARNING);
                return null;
            }

            try
            {
                List<T> result = new List<T>();
                using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await foreach (JsonElement element in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(fs))
                    {
                        try
                        {
                            T? obj = element.Deserialize<T>(_jsonReadOptions);
                            if (obj != null)
                                result.Add(obj);
                        }
                        catch (Exception ex)
                        {
                            string content = element.GetRawText();
                            if (content.Length > 2000)
                                content = content.Substring(0, 2000) + "...";
                            Logger.Instance.Log($"{ASILang.Get("DeserializeJsonFailed")} Content=[{content}] Exception=[{ex}]", Logger.LogLevel.ERROR);
                        }
                    }
                }
                return result;
            }
            catch (JsonException ex)
            {
                Logger.Instance.Log($"{ASILang.Get("FileIsNotValidJson").Replace("#FILE_PATH#", filepath ?? string.Empty, StringComparison.InvariantCulture)} Exception=[{ex.Message}]", Logger.LogLevel.WARNING);
                return filepath != null ? DeserializeJsonObjectsLegacy<T>(filepath) : null;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_FileParsingError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            return null;
        }

        private static List<T>? DeserializeJsonObjectsLegacy<T>(string filepath)
        {
            try
            {
                List<T> result = new List<T>();
                const string objBegin = "    {";
                const string objEnd = "    }";
                StringBuilder? currentObj = null;
                using (StreamReader sr = new StreamReader(new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, FileOptions.SequentialScan)))
                {
                    string? line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line == objBegin)
                            currentObj = new StringBuilder("{");
                        else if (line.StartsWith(objEnd, StringComparison.Ordinal))
                        {
                            if (currentObj != null)
                            {
                                string json = currentObj.Append('}').ToString();
                                try
                                {
                                    T? obj = JsonSerializer.Deserialize<T>(json, _legacyJsonOptions);
                                    if (obj != null)
                                        result.Add(obj);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Instance.Log($"{ASILang.Get("DeserializeJsonFailed")} Content=[{(json.Length > 2000 ? json.Substring(0, 2000) + "..." : json)}] Exception=[{ex}]", Logger.LogLevel.ERROR);
                                }
                            }
                            currentObj = null;
                        }
                        else
                            currentObj?.Append(line);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_FileParsingError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            return null;
        }

        private static readonly JsonSerializerOptions _legacyJsonOptions = new JsonSerializerOptions() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

        /// <summary>
        /// Options of the main JSON loader. ASI's Python scripts write NaN and +/-Infinity as the strings "NaN", "Infinity" and "-Infinity"
        /// (bare NaN / Infinity tokens are not valid JSON): this reads them back into float / double properties.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonReadOptions = new JsonSerializerOptions() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

        private static void RemoveJsonExportProfile(JsonExportProfile jep)
        {
            lock (_jsonExportProfilesLock)
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
        }

        public static int DeleteJsonExportProfile(JsonExportProfile? jep)
        {
            if (jep == null)
                return 0;

            bool deleteFolder = true;
            if (jep.IsDeprecatedFormat())
            {
                deleteFolder = false;
                Logger.Instance.Log($"{ASILang.Get("DeprecatedExportProfileFormat").Replace("#JSON_EXPORT_PROFILE_NAME#", jep.GetLabel() ?? string.Empty, StringComparison.InvariantCulture)}", Logger.LogLevel.WARNING);
            }
            string folderPath = jep.GetExportFolderPath();
            if (!Directory.Exists(folderPath))
                Logger.Instance.Log($"{ASILang.Get("RemoveJsonDataFailed_AlreadyRemoved").Replace("#DIRECTORY_PATH#", folderPath ?? string.Empty, StringComparison.InvariantCulture)}", Logger.LogLevel.WARNING);

            List<JsonExportProfile> toRemove = new List<JsonExportProfile>();
            foreach (var jsonExportProfile in GetJsonExportProfilesSnapshot())
            {
                if (jsonExportProfile == null)
                    continue;
                if (string.Equals(folderPath, jsonExportProfile.GetExportFolderPath(), StringComparison.OrdinalIgnoreCase)) // Windows paths are case-insensitive.
                    toRemove.Add(jsonExportProfile);
            }

            if (deleteFolder && Directory.Exists(folderPath))
                try
                {
                    Directory.Delete(folderPath, true);
                    Logger.Instance.Log(ASILang.Get("FolderRemoved").Replace("#FOLDER_PATH#", folderPath ?? string.Empty, StringComparison.InvariantCulture));
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("RemoveJsonDataFailed")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return -1;
                }

            if (toRemove.Count > 0)
            {
                lock (_jsonExportProfilesLock)
                {
                    foreach (var jepToRemove in toRemove)
                        RemoveJsonExportProfile(jepToRemove);
                    SaveJsonExportProfiles();
                }
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
                tb_JsonDataLoaded.Text = $"{ASILang.Get("RemoveJsonDataFailed_Details")} {ASILang.Get("SeeLogsForDetails")}";
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
                // Invalid JSON: keep the file aside, otherwise the next save would overwrite it (and its content would be lost).
                if (ex is JsonException)
                    Utils.RenameInvalidUserFile(jsonExportPresetsPath, ex);
            }
        }

        public static void SaveJsonExportPresets()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize<List<JsonExportPreset>>(_jsonExportPresets, new JsonSerializerOptions() { WriteIndented = true });
                Utils.WriteAllTextAtomic(Utils.ExportPresetsFilePath(), jsonString);
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

            MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", tb_ExistingExtractionPresets_Remove.Text ?? string.Empty, StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
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

            MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", tb_ExistingExtractionPresets_AddTo.Text ?? string.Empty, StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
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
            try
            {
                if (extractions == null || extractions.Count <= 0)
                    return;

                // CLI timeout reached: do not start any new extraction.
                if (MainWindow.PresetExtractionAborted)
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

                // Notify that preset extraction is complete (CLI "-ExtractPreset" case).
                MainWindow.PresetExtractionCompleted = true;
            }
            catch (Exception ex)
            {
                // async void: an exception would otherwise be lost (or crash the app). Log it instead.
                Logger.Instance.Log($"Exception caught in SettingsPage.OnCurrentExtractionComplete. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public void DoExtractPreset(JsonExportPreset? preset, List<bool>? selected)
        {
            if (preset == null)
                return;
            // Refuse the whole preset upfront (otherwise each of its extractions would be refused one by one, and counted as failed).
            if (PythonManager.IsBusy)
            {
                PythonManager.NotifyBusy();
                return;
            }

            List<KeyValuePair<JsonExportProfile, bool>> toExtract = new List<KeyValuePair<JsonExportProfile, bool>>();
            int profileIndex = 0;
            foreach (JsonExportProfile jep in preset.ExportProfiles)
                if (jep != null)
                {
                    bool doAdd = (selected == null || (profileIndex < selected.Count && selected[profileIndex]));
                    profileIndex++;
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
                        List<bool> selected = new List<bool>();
                        if (sp_ExportPresetExtract.Children != null && sp_ExportPresetExtract.Children.Count > 0)
                            foreach (var obj in sp_ExportPresetExtract.Children)
                            {
                                Grid? grd = obj as Grid;
                                if (grd != null)
                                {
                                    CheckBox? cb = grd.Children[1] as CheckBox;
                                    TextBox? tb = grd.Children[2] as TextBox;
                                    if (cb != null && tb != null)
                                        selected.Add((cb.IsChecked != null && cb.IsChecked.HasValue && cb.IsChecked.Value));
                                }
                            }
                        if (selected.Count <= 0)
                        {
                            MainWindow.ShowToast(ASILang.Get("NoExportProfileSelected"), BackgroundColor.WARNING);
                            return;
                        }

                        DoExtractPreset(preset, selected);
                    }
                    else
                        MainWindow.ShowToast(ASILang.Get("PresetIsEmpty"), BackgroundColor.WARNING);
                    return;
                }

            MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", tb_ExportPresetExtract.Text ?? string.Empty, StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
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
            List<JsonExportProfile> profiles = GetJsonExportProfilesSnapshot(); // The list can be modified by a background thread meanwhile.
            if (currentColumn == "ID")
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.ID).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.ID).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Name"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.ExtractName).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.ExtractName).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Path"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.SaveFilePath).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.SaveFilePath).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("MapName"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.MapName).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.MapName).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("CreationDate"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.CreationDate).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.CreationDate).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Dinos"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.ExtractedDinos).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.ExtractedDinos).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Pawns"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.ExtractedPlayerPawns).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.ExtractedPlayerPawns).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Items"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.ExtractedItems).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.ExtractedItems).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Structures"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.ExtractedStructures).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.ExtractedStructures).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Players"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.ExtractedPlayers).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.ExtractedPlayers).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                sorted = true;
            }
            else if (currentColumn == ASILang.Get("Tribes"))
            {
                if (updateSortDir)
                    AdjustSortDirection(currentColumn);
                if (_sortAscending)
                    _lastDisplayedVM = profiles.OrderBy(o => o.ExtractedTribes).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>((list.Count() > 0 ? list.First().SaveFilePath : key), list));
                else
                    _lastDisplayedVM = profiles.OrderByDescending(o => o.ExtractedTribes).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
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

        private static string GetTribeNameOrId(Dino d)
        {
            if (SettingsPage._tribeNames != null && SettingsPage._tribeNames.ContainsKey(d.OwningTribeID))
                return SettingsPage._tribeNames[d.OwningTribeID];
            if (!string.IsNullOrEmpty(d.TribeName))
                return d.TribeName;
            return d.OwningTribeID.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetTribeNameOrId_Fallback(Structure s) => !string.IsNullOrEmpty(s.OwnerName) ? s.OwnerName : (!string.IsNullOrEmpty(s.OwningPlayerName) ? s.OwningPlayerName : (s.TargetingTeam != null && s.TargetingTeam.HasValue ? s.TargetingTeam.Value.ToString(CultureInfo.InvariantCulture) : string.Empty));
        private static string GetTribeNameOrId(Structure s)
        {
            if (s.TargetingTeam != null && s.TargetingTeam.HasValue && SettingsPage._tribeNames != null && SettingsPage._tribeNames.ContainsKey(s.TargetingTeam.Value))
                return SettingsPage._tribeNames[s.TargetingTeam.Value];
            return GetTribeNameOrId_Fallback(s);
        }

        private static string GetTribeNameOrId(Item i) => i.ContainerTribeID != null && i.ContainerTribeID.HasValue && SettingsPage._tribeNames != null && SettingsPage._tribeNames.ContainsKey(i.ContainerTribeID.Value) ? SettingsPage._tribeNames[i.ContainerTribeID.Value] : (i.ContainerTribeID != null && i.ContainerTribeID.HasValue ? i.ContainerTribeID.Value.ToString(CultureInfo.InvariantCulture) : "0");

        /// <summary>Quick filters values, computed on a background thread and then applied on the UI thread.</summary>
        private sealed class QuickFiltersData
        {
            public Dictionary<string, int> TribesForDinos = new Dictionary<string, int>();
            public List<string> TribesForDinosSorted = new List<string>();
            public List<string?> CleanNamesForDinosSorted = new List<string?>();
            public Dictionary<string, int> TribesForStructures = new Dictionary<string, int>();
            public List<string> TribesForStructuresSorted = new List<string>();
            public List<string?> ShortNamesForStructuresSorted = new List<string?>();
            public Dictionary<string, int> TribesForItems = new Dictionary<string, int>();
            public List<string> TribesForItemsSorted = new List<string>();
            public List<string?> ShortNamesForItemsSorted = new List<string?>();
        }

        private static List<string> SortedKeys(Dictionary<string, int> dict)
        {
            List<string> result = dict.Keys.ToList();
            result.Sort();
            return result;
        }

        private static List<string?> SortedDistinct(IEnumerable<string?> values)
        {
            List<string?> result = values.Where(v => v != null).Distinct().ToList();
            result.Sort();
            return result;
        }

        private static QuickFiltersData ComputeQuickFilters(List<Dino>? dinos, List<Structure>? structures, List<Item>? items)
        {
            QuickFiltersData q = new QuickFiltersData();

            if (dinos != null && dinos.Count > 0)
            {
                q.TribesForDinos = dinos.DistinctBy(d => d.OwningTribeID).Select(d => new KeyValuePair<string, int>(GetTribeNameOrId(d), d.OwningTribeID)).DistinctBy(e => e.Key).ToDictionary();
                q.TribesForDinosSorted = SortedKeys(q.TribesForDinos);
                q.CleanNamesForDinosSorted = SortedDistinct(dinos.Select(d => d.CleanName));
            }

            if (structures != null && structures.Count > 0)
            {
                q.TribesForStructures = structures.Where(s => s.TargetingTeam != null && s.TargetingTeam.HasValue).DistinctBy(s => s.TargetingTeam).Select(s => new KeyValuePair<string, int>(GetTribeNameOrId(s), s.TargetingTeam!.Value)).DistinctBy(e => e.Key).ToDictionary();
                q.TribesForStructuresSorted = SortedKeys(q.TribesForStructures);
                q.ShortNamesForStructuresSorted = SortedDistinct(structures.Select(s => s.ShortName));
            }

            if (items != null && items.Count > 0)
            {
                q.TribesForItems = items.Where(i => i.ContainerTribeID != null && i.ContainerTribeID.HasValue).DistinctBy(i => i.ContainerTribeID).Select(i => new KeyValuePair<string, int>(GetTribeNameOrId(i), i.ContainerTribeID!.Value)).DistinctBy(e => e.Key).ToDictionary();
                q.TribesForItemsSorted = SortedKeys(q.TribesForItems);
                q.ShortNamesForItemsSorted = SortedDistinct(items.Select(i => i.ShortName));
            }

            return q;
        }

        private static void ApplyQuickFilters(QuickFiltersData q)
        {
            _allTribesForDinos = q.TribesForDinos;
            _allTribesForDinosSorted = q.TribesForDinosSorted;
            _allCleanNamesForDinosSorted = q.CleanNamesForDinosSorted;
            _allTribesForDinosInitialized = true;
            _allCleanNamesForDinosInitialized = true;

            _allTribesForStructures = q.TribesForStructures;
            _allTribesForStructuresSorted = q.TribesForStructuresSorted;
            _allShortNamesForStructuresSorted = q.ShortNamesForStructuresSorted;
            _allTribesForStructuresInitialized = true;
            _allShortNamesForStructuresInitialized = true;

            _allTribesForItems = q.TribesForItems;
            _allTribesForItemsSorted = q.TribesForItemsSorted;
            _allShortNamesForItemsSorted = q.ShortNamesForItemsSorted;
            _allTribesForItemsInitialized = true;
            _allShortNamesForItemsInitialized = true;

            if (DinosPage._page != null)
                DinosPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    DinosPage._page.InitTribesQuickFilter();
                    DinosPage._page.InitDinosQuickFilter();
                });
            if (StructuresPage._page != null)
                StructuresPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    StructuresPage._page.InitTribesQuickFilter();
                    StructuresPage._page.InitStructuresQuickFilter();
                });
            if (ItemsPage._page != null)
                ItemsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    ItemsPage._page.InitTribesQuickFilter();
                    ItemsPage._page.InitItemsQuickFilter();
                });
        }

        private static void ResetQuickFilters()
        {
            _allTribesForDinosInitialized = false;
            _allCleanNamesForDinosInitialized = false;
            _allTribesForStructuresInitialized = false;
            _allShortNamesForStructuresInitialized = false;
            _allTribesForItemsInitialized = false;
            _allShortNamesForItemsInitialized = false;
            _allTribesForDinos = new Dictionary<string, int>();
            _allTribesForDinosSorted = new List<string>();
            _allCleanNamesForDinosSorted = new List<string?>();
            _allTribesForStructures = new Dictionary<string, int>();
            _allTribesForStructuresSorted = new List<string>();
            _allShortNamesForStructuresSorted = new List<string?>();
            _allTribesForItems = new Dictionary<string, int>();
            _allTribesForItemsSorted = new List<string>();
            _allShortNamesForItemsSorted = new List<string?>();
        }

        private void AllTasksCompleted(bool hasErrors)
        {
            if (hasErrors)
            {
                tb_JsonDataLoaded.Text = $"{ASILang.Get("LoadJsonData_PartiallyLoaded")} {ASILang.Get("SeeLogsForDetails")}";
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

        // Incremented for each load. If a new load starts while a previous one is still running, the results of the previous ones are ignored.
        private static int _loadGeneration = 0;

        private static Task<List<T>?> LoadIfExtracted<T>(bool extracted, string filepath) => extracted ? Task.Run(() => DeserializeJsonObjectsAsync<T>(filepath)) : Task.FromResult<List<T>?>(null);

        private async Task DeserializeAllDataAsync(string folderPath, JsonExportProfile profile, bool hasErrors)
        {
            int generation = ++_loadGeneration;
            try
            {
                var dinosTask = LoadIfExtracted<Dino>(profile.ExtractedDinos, Path.Combine(folderPath, "dinos.json"));
                var pawnsTask = LoadIfExtracted<PlayerPawn>(profile.ExtractedPlayerPawns, Path.Combine(folderPath, "player_pawns.json"));
                var itemsTask = LoadIfExtracted<Item>(profile.ExtractedItems, Path.Combine(folderPath, "items.json"));
                var structuresTask = LoadIfExtracted<Structure>(profile.ExtractedStructures, Path.Combine(folderPath, "structures.json"));
                var playersTask = LoadIfExtracted<Player>(profile.ExtractedPlayers, Path.Combine(folderPath, "players.json"));
                var tribesTask = LoadIfExtracted<Tribe>(profile.ExtractedTribes, Path.Combine(folderPath, "tribes.json"));
                await Task.WhenAll(dinosTask, pawnsTask, itemsTask, structuresTask, playersTask, tribesTask);

                List<Dino>? loadedDinos = dinosTask.Result;
                if (loadedDinos != null && loadedDinos.Count > 0)
                    await Task.Run(() =>
                    {
                        foreach (var dinoData in loadedDinos)
                            dinoData.InitStats();
                    });

                if (generation != _loadGeneration)
                    return; // A more recent load has been started.

                if (profile.ExtractedDinos) { _dinosData = dinosTask.Result; hasErrors |= (_dinosData == null); }
                if (profile.ExtractedPlayerPawns) { _playerPawnsData = pawnsTask.Result; hasErrors |= (_playerPawnsData == null); }
                if (profile.ExtractedItems) { _itemsData = itemsTask.Result; hasErrors |= (_itemsData == null); }
                if (profile.ExtractedStructures) { _structuresData = structuresTask.Result; hasErrors |= (_structuresData == null); }
                if (profile.ExtractedPlayers) { _playersData = playersTask.Result; hasErrors |= (_playersData == null); }
                if (profile.ExtractedTribes)
                {
                    _tribesData = tribesTask.Result;
                    hasErrors |= (_tribesData == null);
                    if (_tribesData != null && _tribesData.Count > 0)
                        _tribeNames = _tribesData.DistinctBy(t => t.TribeID).Select(t => new KeyValuePair<int, string>(t.TribeID != null && t.TribeID.HasValue ? t.TribeID.Value : -1, t.TribeName ?? string.Empty)).Where(s => s.Key != -1 && !string.IsNullOrEmpty(s.Value)).ToDictionary();
                }
                if (_tribeNames == null)
                    _tribeNames = new Dictionary<int, string>();

                List<Dino>? dinos = _dinosData;
                List<Structure>? structures = _structuresData;
                List<Item>? items = _itemsData;
                QuickFiltersData quickFilters = await Task.Run(() => ComputeQuickFilters(dinos, structures, items));

                if (generation != _loadGeneration)
                    return;

                ApplyQuickFilters(quickFilters);
                AllTasksCompleted(hasErrors);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_FileParsingError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                if (generation == _loadGeneration)
                    AllTasksCompleted(true);
            }
        }

        private async void LoadJsonExportProfile_Click(object sender, RoutedEventArgs e)
        {
            try
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

                if (_selectedJsonExportProfile.IsDeprecatedFormat())
                    Logger.Instance.Log($"{ASILang.Get("DeprecatedExportProfileFormat").Replace("#JSON_EXPORT_PROFILE_NAME#", jep.GetLabel() ?? string.Empty, StringComparison.InvariantCulture)}", Logger.LogLevel.WARNING);
                string folderPath = _selectedJsonExportProfile.GetExportFolderPath();

                if (!Directory.Exists(folderPath))
                {
                    Logger.Instance.Log(ASILang.Get("LoadJsonFailed_ExportFolderNotFound"), Logger.LogLevel.ERROR);
                    tb_JsonDataLoaded.Text = ASILang.Get("LoadJsonFailed_ExportFolderNotFound");
                    tb_JsonDataLoaded.Foreground = _errorColor;
                    tb_JsonDataLoaded.Visibility = Visibility.Visible;
                    return;
                }

                Utils.ClearAllPagesFilters();
                ResetQuickFilters();
                bool hasErrors = false;

                bool refreshMinimap = (string.Compare(_currentlyLoadedMapName, _selectedJsonExportProfile.MapName, StringComparison.InvariantCulture) != 0);
                _currentlyLoadedMapName = _selectedJsonExportProfile.MapName;
                _currentlyLoadedSubMapName = null;
                if (!string.IsNullOrWhiteSpace(_currentlyLoadedMapName))
                {
                    var mapInfo = Utils.GetMapInfoFromName(_currentlyLoadedMapName);
                    if (mapInfo != null)
                        _currentlyLoadedSubMapName = mapInfo.SubMapName;
                }
                if (refreshMinimap && MainWindow._minimap != null)
                    MainWindow.OpenMinimap();

                string saveInfoFilePath = Path.Combine(folderPath, "save_info.json");
                if (!File.Exists(saveInfoFilePath))
                {
                    Logger.Instance.Log($"{ASILang.Get("LoadJsonFailed_SaveFileInfoNotFound").Replace("#FILEPATH#", saveInfoFilePath ?? string.Empty, StringComparison.InvariantCulture)}", Logger.LogLevel.WARNING);
                    hasErrors = true;
                }
                else
                {
                    try
                    {
                        string json = File.ReadAllText(saveInfoFilePath, Encoding.UTF8);
                        _saveFileData = JsonSerializer.Deserialize<SaveFileInfo>(json, _jsonReadOptions);
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
                        hasErrors = true;
                    }
                }

                Utils.LockAllPages(true);
                tb_JsonDataLoaded.Text = $"{ASILang.Get("LoadingJsonData")} {ASILang.Get("PleaseWait")}";
                tb_JsonDataLoaded.Foreground = _darkTheme != null && _darkTheme.HasValue && _darkTheme.Value == false ? _defaultColorLightTheme : _defaultColorDarkTheme;
                tb_JsonDataLoaded.Visibility = Visibility.Visible;

                await DeserializeAllDataAsync(folderPath, _selectedJsonExportProfile, hasErrors);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SettingsPage.LoadJsonExportProfile_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void RemoveJsonExportProfile_Click(object sender, RoutedEventArgs e)
        {
            JsonExportProfile? jep = GetJEPFromButton(e);
            if (jep == null)
                return;

            JsonExportProfileSelected(jep);

            if (_selectedJsonExportProfile != null && !ConfirmJsonExportRemovalPopup.IsOpen)
            {
                string folderPath = _selectedJsonExportProfile.GetExportFolderPath();

                tb_confirmJsonFolderRemoval.Text = $"{ASILang.Get("JsonDataRemovalConfirm_Description").Replace("#DIRECTORY_PATH#", folderPath ?? string.Empty, StringComparison.InvariantCulture)}";
                ConfirmJsonExportRemovalPopup.IsOpen = true;
            }
        }

        #endregion
    }
}
