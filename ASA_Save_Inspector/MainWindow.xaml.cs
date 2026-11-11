using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ASA_Save_Inspector.Pages;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using WinUIEx;

namespace ASA_Save_Inspector
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public const string _appName = "ASA Save Inspector";
        public const string _appAcronym = "ASI";
        public AppWindow? _appWindow = null;
        public static MainWindow? _mainWindow = null;
        public NavigationView? _navView = null;
        public NavigationViewItem? _navBtnSettings = null;
        public NavigationViewItem? _navBtnItems = null;
        public NavigationViewItem? _navBtnPawns = null;
        public NavigationViewItem? _navBtnDinos = null;
        public NavigationViewItem? _navBtnStructures = null;
        public NavigationViewItem? _navBtnPlayersData = null;
        public NavigationViewItem? _navBtnTribesData = null;
        public NavigationViewItem? _navBtnAbout = null;
        public static Minimap? _minimap = null;
        private static List<MapPoint> _emptyPoints = new List<MapPoint>();

        public event PropertyChangedEventHandler? PropertyChanged;

        // Testing data
        private static readonly List<MapPoint> _points = new List<MapPoint>()
        {
            new MapPoint() { ID = "1", Name = "Bottom Left", Description = "Some description 1\nSome very very very very very very long description 2\nSome description 3", X = Minimap.X_MIN, Y = Minimap.Y_MIN },
            new MapPoint() { ID = "2", Name = "Bottom Right", Description = "Some description 4\nSome very very very very very very long description 5\nSome description 6", X = Minimap.X_MAX, Y = Minimap.Y_MIN },
            new MapPoint() { ID = "3", Name = "Top Left", Description = "Some description 7\nSome very very very very very very long description 8\nSome description 9", X = Minimap.X_MIN, Y = Minimap.Y_MAX },
            new MapPoint() { ID = "4", Name = "Top Right", Description = "Some description 10\nSome very very very very very very long description 11\nSome description 12", X = Minimap.X_MAX, Y = Minimap.Y_MAX }
        };

        // Testing data
        private static readonly List<MapPoint> _pointsB = new List<MapPoint>()
        {
            new MapPoint() { ID = "5", Name = "Bottom Left 2", Description = "Some description 1\nSome very very very very very very long description 2\nSome description 3", X = Minimap.X_MIN + 400.0d, Y = Minimap.Y_MIN + 400.0d },
            new MapPoint() { ID = "6", Name = "Bottom Right 2", Description = "Some description 4\nSome very very very very very very long description 5\nSome description 6", X = Minimap.X_MAX - 400.0d, Y = Minimap.Y_MIN + 400.0d },
            new MapPoint() { ID = "7", Name = "Top Left 2", Description = "Some description 7\nSome very very very very very very long description 8\nSome description 9", X = Minimap.X_MIN + 400.0d, Y = Minimap.Y_MAX - 400.0d },
            new MapPoint() { ID = "8", Name = "Top Right 2", Description = "Some description 10\nSome very very very very very very long description 11\nSome description 12", X = Minimap.X_MAX - 400.0d, Y = Minimap.Y_MAX - 400.0d }
        };

        public MainWindow()
        {
            InitializeComponent();

            ASILang.SwitchLanguage(!string.IsNullOrEmpty(SettingsPage._language) && ASILang._languages.ContainsKey(SettingsPage._language) ? SettingsPage._language : ASILang.DEFAULT_LANGUAGE_CODE);

            this._appWindow = this.AppWindow;
            this._navView = NavView;
            this._navBtnSettings = nvi_Settings;
            this._navBtnItems = nvi_Items;
            this._navBtnPawns = nvi_PlayerPawns;
            this._navBtnDinos = nvi_Dinos;
            this._navBtnStructures = nvi_Structures;
            this._navBtnPlayersData = nvi_Players;
            this._navBtnTribesData = nvi_Tribes;
            this._navBtnAbout = nvi_About;

            MainWindow._mainWindow = this;
            Activated += MainWindow_Activated;
            App.Current.UnhandledException += Current_UnhandledException;

            // Hide system title bar.
            ExtendsContentIntoTitleBar = true;
            if (ExtendsContentIntoTitleBar == true)
                this._appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            // Set title bar title.
            this.Title = $"{_appName} v{Utils.GetVersionStr()}";
            TitleBarTextBlock.Text = $"{_appName} v{Utils.GetVersionStr()}";

            // Set icon.
            if (_appWindow != null)
            {
                _appWindow.SetIcon(@"Assets\ASI.ico");
                _appWindow.SetTitleBarIcon(@"Assets\ASI.ico");
                _appWindow.SetTaskbarIcon(@"Assets\ASI.ico");
            }

            // Load maps data.
            LoadMapsInfo();

            // Init Python.
            PythonManager.InitHttpClient();
            PythonManager.GetPythonExePaths();
            PythonManager.CreateAsiExportScriptFile(Utils.AsiExportFastOrigFilePath(), Utils.AsiExportFastFilePath());
            PythonManager.CreateAsiExportScriptFile(Utils.AsiExportClusterDataOrigFilePath(), Utils.AsiExportClusterDataFilePath());

            // Load launch arguments.
            bool checkForUpdates = true;
            string[] args = Environment.GetCommandLineArgs();
            if (args != null)
            {
                if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[1]) && !string.IsNullOrWhiteSpace(args[2]))
                {
                    if (string.Compare(args[1], "-ExtractPreset", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        int timeout = 1800; // Default timeout: 30 minutes.
                        checkForUpdates = false;
                        if (args.Length > 4)
                            if (!string.IsNullOrWhiteSpace(args[3]) && !string.IsNullOrWhiteSpace(args[4]))
                                if (string.Compare(args[3], "-Timeout", StringComparison.InvariantCultureIgnoreCase) == 0)
                                    if (int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTimeout) && parsedTimeout >= 120 && parsedTimeout <= 10800) // Minimum timeout 2 minutes, maximum timeout 3 hours.
                                        timeout = parsedTimeout;
#pragma warning disable CS4014
                        CLI_ExtractPreset(args[2], timeout);
#pragma warning restore CS4014
                    }
                }
                else if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                {
                    if (string.Compare(args[1], "-CleanOldData", StringComparison.InvariantCultureIgnoreCase) == 0)
#pragma warning disable CS4014
                        CLI_CleanOldData();
#pragma warning restore CS4014
                }
            }

            // Check for updates.
            if (checkForUpdates)
#pragma warning disable CS4014
                CheckForUpdateIfEnabled();
#pragma warning restore CS4014
        }

        public static bool SettingsPageFinishedLoading = false;
        public static bool PresetExtractionCompleted = false;
        public static bool PresetExtractionFailed = false;
        public static bool PresetExtractionAborted = false; // Set when the CLI gives up (timeout), so that no new extraction is started.
        public static bool ArkParseUpdateCompleted = false;

        private const int CLI_EXIT_OK = 0;
        private const int CLI_EXIT_EXTRACTION_FAILED = 1; // At least one extraction of the preset failed.
        private const int CLI_EXIT_PRESET_ERROR = 2;      // Preset not found or empty.
        private const int CLI_EXIT_TIMEOUT = 3;           // Extraction did not complete before timeout.
        private const int CLI_EXIT_UNEXPECTED_ERROR = 4;  // Unexpected error (ASI not ready, exception and other errors).

        private async Task CLI_ExtractPreset(string presetName, int timeout = 1800)
        {
            int exitCode = CLI_EXIT_UNEXPECTED_ERROR;
            try
            {
                exitCode = await CLI_DoExtractPreset(presetName, timeout);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"CLI: Unexpected exception caught in -ExtractPreset. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            finally
            {
                // Always close the app, otherwise the caller would wait forever.
                PythonManager.KillRunningProcess();
                Logger.Instance.Log($"CLI: -ExtractPreset exits with code {exitCode.ToString(CultureInfo.InvariantCulture)}.");
                await Task.Delay(1000);
                Environment.ExitCode = exitCode;
                Application.Current.Exit();
            }
        }

        private async Task<int> CLI_DoExtractPreset(string presetName, int timeout)
        {
            Utils.LockAllPages(true);
            Logger.Instance.Log($"CLI: Running -ExtractPreset CLI with preset name [{presetName}] and timeout[{timeout.ToString(CultureInfo.InvariantCulture)}].");
            await Task.Delay(250);
            if (MainWindow._mainWindow == null)
                return CLI_EXIT_UNEXPECTED_ERROR;

            Logger.Instance.Log("CLI: Loading settings and initializing ArkParse...");

            // Open settings page so that presets are loaded.
            SettingsPageFinishedLoading = false;
            ArkParseUpdateCompleted = false;
            if (MainWindow._mainWindow._navView != null)
                MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnSettings;
            MainWindow._mainWindow.NavView_Navigate(typeof(SettingsPage), new EntranceNavigationTransitionInfo());

            // Wait for settings page to finish opening (wait 3 minutes max).
            int i = 0;
            bool settingsPageLoaded = false;
            bool arkParseUpdated = false;
            while (i < 180 && (!SettingsPageFinishedLoading || !ArkParseUpdateCompleted))
            {
                await Task.Delay(1000);
                if (!settingsPageLoaded && SettingsPageFinishedLoading)
                {
                    settingsPageLoaded = true;
                    Logger.Instance.Log("CLI: Settings loaded.");
                    // Lock settings page.
                    if (SettingsPage._page != null)
                        SettingsPage._page.IsEnabled = false;
                }
                if (!arkParseUpdated && ArkParseUpdateCompleted)
                {
                    arkParseUpdated = true;
                    Logger.Instance.Log("CLI: ArkParse initialized.");
                }
                ++i;
            }

            // Lock settings page.
            if (SettingsPage._page != null)
                SettingsPage._page.IsEnabled = false;

            // Search requested preset.
            Logger.Instance.Log($"CLI: Searching extraction preset \"{presetName}\"...");
            JsonExportPreset? foundPreset = null;
            if (SettingsPage._jsonExportPresets != null && SettingsPage._jsonExportPresets.Count > 0)
                foreach (var preset in SettingsPage._jsonExportPresets)
                    if (preset != null && string.Compare(presetName, preset.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        foundPreset = preset;
                        break;
                    }

            // Extract preset (if it has been found and if is valid).
            if (foundPreset == null)
            {
                Logger.Instance.Log($"CLI: Preset \"{presetName}\" has not been found.", Logger.LogLevel.ERROR);
                return CLI_EXIT_PRESET_ERROR;
            }
            if (foundPreset.ExportProfiles == null || foundPreset.ExportProfiles.Count <= 0)
            {
                Logger.Instance.Log($"CLI: Preset \"{presetName}\" is empty.", Logger.LogLevel.ERROR);
                return CLI_EXIT_PRESET_ERROR;
            }
            if (SettingsPage._page == null)
            {
                Logger.Instance.Log("CLI: Settings page is not available.", Logger.LogLevel.ERROR);
                return CLI_EXIT_UNEXPECTED_ERROR;
            }

            // Reset flags BEFORE starting the extraction.
            PresetExtractionCompleted = false;
            PresetExtractionFailed = false;
            PresetExtractionAborted = false;
            bool queued = SettingsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                Logger.Instance.Log($"CLI: Extracting preset \"{presetName}\"...");
                await Task.Delay(250);
                SettingsPage._page.DoExtractPreset(foundPreset, null); // Extract preset.
            });
            if (!queued)
            {
                Logger.Instance.Log("CLI: Failed to start preset extraction (UI thread is not available).", Logger.LogLevel.ERROR);
                return CLI_EXIT_UNEXPECTED_ERROR;
            }

            // Wait for preset extraction to complete (wait "timeout" seconds max).
            int j = 0;
            while (!PresetExtractionCompleted && j < timeout)
            {
                await Task.Delay(1000);
                ++j;
            }

            if (!PresetExtractionCompleted)
            {
                Logger.Instance.Log($"CLI: Failed to extract preset \"{presetName}\" (timed out).", Logger.LogLevel.ERROR);
                PresetExtractionAborted = true; // Prevents the preset chain from starting a new extraction.
                return CLI_EXIT_TIMEOUT; // The running ArkParse process gets killed by the caller.
            }
            if (PresetExtractionFailed)
            {
                Logger.Instance.Log($"CLI: Preset \"{presetName}\" has been extracted with errors (see logs above).", Logger.LogLevel.ERROR);
                return CLI_EXIT_EXTRACTION_FAILED;
            }
            Logger.Instance.Log($"CLI: Preset \"{presetName}\" has been successfully extracted.");
            return CLI_EXIT_OK;
        }

        /*
        private async Task CLI_ExtractPreset(string presetName, int timeout = 1800)
        {
            Utils.LockAllPages(true);
            Logger.Instance.Log($"CLI: Running -ExtractPreset CLI with preset name [{presetName}] and timeout[{timeout.ToString(CultureInfo.InvariantCulture)}].");
            await Task.Delay(250);
            if (MainWindow._mainWindow != null)
            {
                Logger.Instance.Log("CLI: Loading settings and initializing ArkParse...");

                // Open settings page so that presets are loaded.
                SettingsPageFinishedLoading = false;
                ArkParseUpdateCompleted = false;
                if (MainWindow._mainWindow._navView != null)
                    MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnSettings;
                MainWindow._mainWindow.NavView_Navigate(typeof(SettingsPage), new EntranceNavigationTransitionInfo());

                // Wait for settings page to finish opening (wait 3 minutes max).
                int i = 0;
                bool settingsPageLoaded = false;
                bool arkParseUpdated = false;
                while (i < 180 && (!SettingsPageFinishedLoading || !ArkParseUpdateCompleted))
                {
                    await Task.Delay(1000);
                    if (!settingsPageLoaded && SettingsPageFinishedLoading)
                    {
                        settingsPageLoaded = true;
                        Logger.Instance.Log("CLI: Settings loaded.");
                        // Lock settings page.
                        if (SettingsPage._page != null)
                            SettingsPage._page.IsEnabled = false;
                    }
                    if (!arkParseUpdated && ArkParseUpdateCompleted)
                    {
                        arkParseUpdated = true;
                        Logger.Instance.Log("CLI: ArkParse initialized.");
                    }
                    ++i;
                }

                // Lock settings page.
                if (SettingsPage._page != null)
                    SettingsPage._page.IsEnabled = false;

                // Search requested preset.
                Logger.Instance.Log($"CLI: Searching extraction preset \"{presetName}\"...");
                JsonExportPreset? foundPreset = null;
                if (SettingsPage._jsonExportPresets != null && SettingsPage._jsonExportPresets.Count > 0)
                    foreach (var preset in SettingsPage._jsonExportPresets)
                        if (preset != null && string.Compare(presetName, preset.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            foundPreset = preset;
                            break;
                        }

                // Extract preset (if it has been found and if is valid).
                if (foundPreset == null)
                {
                    Logger.Instance.Log($"CLI: Preset \"{presetName}\" has not been found.", Logger.LogLevel.ERROR);
                    return;
                }
                if (foundPreset.ExportProfiles == null || foundPreset.ExportProfiles.Count <= 0)
                {
                    Logger.Instance.Log($"CLI: Preset \"{presetName}\" is empty.", Logger.LogLevel.ERROR);
                    return;
                }
                if (SettingsPage._page != null) // If SettingsPage is valid.
                    SettingsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                    {
                        Logger.Instance.Log($"CLI: Extracting preset \"{presetName}\"...");
                        await Task.Delay(250);
                        PresetExtractionCompleted = false;
                        SettingsPage._page.DoExtractPreset(foundPreset, null); // Extract preset.
                    });

                // Wait for preset extraction to complete (wait "timeout" seconds max).
                int j = 0;
                while (!PresetExtractionCompleted && j < timeout)
                {
                    await Task.Delay(1000);
                    ++j;
                }
                if (PresetExtractionCompleted)
                    Logger.Instance.Log($"CLI: Preset \"{presetName}\" has been successfully extracted.");
                else
                    Logger.Instance.Log($"CLI: Failed to extract preset \"{presetName}\" (timed out).", Logger.LogLevel.ERROR);

                // Close app.
                await Task.Delay(1000);
                Environment.ExitCode = 0;
                Application.Current.Exit();
            }
        }
        */

        private async Task CLI_CleanOldData()
        {
            Utils.LockAllPages(true);
            Logger.Instance.Log("CLI: Running -CleanOldData.");

            // Clean old data.
            OtherPage.DoRemoveOldJsonData(true);
            Logger.Instance.Log("CLI: Old data has been successfully cleaned up.");

            // Close app.
            await Task.Delay(1000);
            Environment.ExitCode = 0;
            Application.Current.Exit();
        }

        public void LanguageChanged()
        {
            tb_updateAvailableTitle.Text = ASILang.Get("UpdateAvailable");
            tb_updateAvailableDescription.Text = ASILang.Get("UpdateAvailableDescription");
            btn_InstallUpdate.Content = ASILang.Get("Yes");
            btn_DontUpdate.Content = ASILang.Get("No");

            nvi_Settings.Content = ASILang.Get("Settings");
            nvi_PlayerPawns.Content = ASILang.Get("Pawns");
            nvi_Dinos.Content = ASILang.Get("Dinos");
            nvi_Structures.Content = ASILang.Get("Structures");
            nvi_Items.Content = ASILang.Get("Items");
            nvi_Players.Content = ASILang.Get("Players");
            nvi_Tribes.Content = ASILang.Get("Tribes");
            nvi_Other.Content = ASILang.Get("Other");
            nvi_About.Content = ASILang.Get("About");
        }

        private void Current_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.Instance.Log($"An unhandled exception got caught. Exception=[{e.Exception}]", Logger.LogLevel.ERROR);
            if (SettingsPage.UsePythonVenv())
            {
                var t = PythonManager.DeactivatePythonVenv();
                if (t != null)
                    t.Wait(4000);
            }
            Logger.Instance.Log("ASA Save Inspector has stopped.");
            App.Current.Exit();
        }

        private bool MapsInfosAreEqual(ArkMapInfo? a, ArkMapInfo? b)
        {
            if (a == null)
                return (b == null);
            if (b == null)
                return (a == null);
            if (string.Compare(a.MapName, b.MapName, StringComparison.InvariantCultureIgnoreCase) != 0 ||
                string.Compare(a.MinimapFilename, b.MinimapFilename, StringComparison.InvariantCultureIgnoreCase) != 0 ||
                a.Bounds?.OriginMinX != b.Bounds?.OriginMinX ||
                a.Bounds?.OriginMinY != b.Bounds?.OriginMinY ||
                a.Bounds?.OriginMinZ != b.Bounds?.OriginMinZ ||
                a.Bounds?.OriginMaxX != b.Bounds?.OriginMaxX ||
                a.Bounds?.OriginMaxY != b.Bounds?.OriginMaxY ||
                a.Bounds?.OriginMaxZ != b.Bounds?.OriginMaxZ ||
                a.Bounds?.PlayableMinX != b.Bounds?.PlayableMinX ||
                a.Bounds?.PlayableMinY != b.Bounds?.PlayableMinY ||
                a.Bounds?.PlayableMinZ != b.Bounds?.PlayableMinZ ||
                a.Bounds?.PlayableMaxX != b.Bounds?.PlayableMaxX ||
                a.Bounds?.PlayableMaxY != b.Bounds?.PlayableMaxY ||
                a.Bounds?.PlayableMaxZ != b.Bounds?.PlayableMaxZ)
                return false;
            if (a.SubMinimaps == null && b.SubMinimaps != null)
                return false;
            if (a.SubMinimaps != null && b.SubMinimaps == null)
                return false;
            if (a.SubMinimaps != null && b.SubMinimaps != null)
            {
                if (a.SubMinimaps.Count != b.SubMinimaps.Count)
                    return false;
                if (a.SubMinimaps.Count > 0 && b.SubMinimaps.Count > 0)
                    foreach (var subMapA in a.SubMinimaps)
                        if (subMapA != null)
                        {
                            bool found = false;
                            foreach (var subMapB in b.SubMinimaps)
                                if (subMapB != null && MapsInfosAreEqual(subMapA, subMapB))
                                {
                                    found = true;
                                    break;
                                }
                            if (!found)
                                return false;
                        }
            }
            return true;
        }

        private bool UpdateMapsInfo()
        {
            Utils.EnsureDataFolderExist();
            try
            {
                string jsonString = JsonSerializer.Serialize<List<ArkMapInfo>>(Utils._allMaps, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.MapsInfoFilePath(), jsonString, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadMapsInfo. Exception=[{ex}]", Logger.LogLevel.ERROR);
                return false;
            }
        }

        private bool LoadMapsInfo()
        {
            string mapsInfoFilepath = Utils.MapsInfoFilePath();
            if (!File.Exists(mapsInfoFilepath))
                return UpdateMapsInfo();

            List<ArkMapInfo>? jsonMapsInfo = null;
            try
            {
                string mapsInfoJson = File.ReadAllText(mapsInfoFilepath, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(mapsInfoJson))
                    jsonMapsInfo = JsonSerializer.Deserialize<List<ArkMapInfo>>(mapsInfoJson);
            }
            catch (Exception ex)
            {
                jsonMapsInfo = null;
                Logger.Instance.Log($"Exception caught in LoadMapsInfo. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (jsonMapsInfo == null || jsonMapsInfo.Count <= 0)
                return UpdateMapsInfo();

            // Search if some maps config are missing or deprecated in currently stored maps info.
            bool requiresUpdate = false;
            foreach (var preconfig in Utils._allMaps)
                if (preconfig != null)
                {
                    bool found = false;
                    foreach (var newconfig in jsonMapsInfo)
                        if (newconfig != null && MapsInfosAreEqual(preconfig, newconfig))
                        {
                            found = true;
                            break;
                        }
                    if (!found) // If map config is missing or deprecated.
                    {
                        // Remove previously stored instance of the map info.
                        int toDel = -1;
                        try
                        {
                            for (int i = 0; i < jsonMapsInfo.Count; i++)
                                if (string.Compare(jsonMapsInfo[i].MapName, preconfig.MapName, StringComparison.InvariantCultureIgnoreCase) == 0)
                                {
                                    toDel = i;
                                    break;
                                }
                            if (toDel >= 0)
                                jsonMapsInfo.RemoveAt(toDel);
                        }
                        catch { }
                        // Add the new (or updated) map info.
                        if (toDel >= 0)
                            jsonMapsInfo.Insert(toDel, preconfig);
                        else
                            jsonMapsInfo.Add(preconfig);
                        requiresUpdate = true;
                        Logger.Instance.Log($"Map \"{preconfig.MapName}\" was missing or incorrectly formatted inside \"maps_info.json\". JSON file has been updated.", Logger.LogLevel.WARNING);
                    }
                }

            Utils._allMaps = jsonMapsInfo;
            return (requiresUpdate ? UpdateMapsInfo() : true);
        }

        private static bool LastDoubleTap(MapPoint? point)
        {
            Debug.WriteLine($"Point \"{(point?.Name ?? string.Empty)}\" has been double clicked.");
            return true;
        }

        public static void CloseMinimap()
        {
            if (_minimap == null)
                return;
            HideMinimap();
            _minimap.Close();
        }

        public static void OpenMinimap()
        {
            CloseMinimap();
            if (MainWindow._mainWindow != null)
            {
                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    await Task.Delay(250);
                    MainWindow.UpdateMinimap(_emptyPoints, LastDoubleTap);
                    MainWindow.ShowMinimap();
                });
            }
        }

        public static void UpdateMinimap(IEnumerable<MapPoint?> points, Func<MapPoint?, bool>? onDoubleTap)
        {
            if (_minimap == null)
            {
                _minimap = new Minimap();
                string mapName = SettingsPage._currentlyLoadedMapName != null ? SettingsPage._currentlyLoadedMapName : ASILang.Get("Unknown");
                ArkMapInfo? mapInfo = Utils.GetMapInfoFromName(mapName);

                List<string> allMapFilenames = new List<string>();
                if (mapInfo != null)
                {
                    allMapFilenames.Add(mapInfo.MinimapFilename);
                    if (mapInfo.SubMinimaps != null && mapInfo.SubMinimaps.Count > 0)
                        foreach (var subMap in mapInfo.SubMinimaps)
                            if (subMap != null && !string.IsNullOrWhiteSpace(subMap.MinimapFilename))
                                if (!allMapFilenames.Contains(subMap.MinimapFilename))
                                    allMapFilenames.Add(subMap.MinimapFilename);
                }
                else
                    allMapFilenames.Add("TheIsland_Minimap_Margin.jpg");
                Minimap.InitMap(points, onDoubleTap, allMapFilenames);
            }
            else
            {
                if (Minimap.PauseMinimapRefresh)
                    Minimap.UpdatePointsAndDoubleTapData(points, onDoubleTap);
                else
                    Minimap.ChangePoints(points, onDoubleTap);
            }
        }

        public static bool ShowMinimap() => (_minimap != null ? _minimap.Show() && _minimap.SetForegroundWindow() : false);

        public static bool HideMinimap() => (_minimap != null ? _minimap.Hide() : false);

        public static void TestAddPointsMinimap()
        {
            if (MainWindow._mainWindow == null)
                return;
            UpdateMinimap(_points, LastDoubleTap);
            MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(3000);
                UpdateMinimap(_pointsB, LastDoubleTap);
            });
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
                TitleBarTextBlock.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            else
                TitleBarTextBlock.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
        }

        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            Logger.Instance.Log($"Failed to load page \"{e.SourcePageType.FullName}\".", Logger.LogLevel.ERROR);
            throw new Exception($"Failed to load page \"{e.SourcePageType.FullName}\".");
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;
            // Load home page.
            NavView_Navigate(typeof(HomePage), new EntranceNavigationTransitionInfo());
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                string? btnTag = args.InvokedItemContainer.Tag.ToString();
                Type? navPageType = (btnTag != null ? Type.GetType(btnTag) : null);
                if (navPageType != null)
                    NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
            }
        }

        public void NavView_Navigate(Type navPageType, NavigationTransitionInfo transitionInfo)
        {
            // Get the page type before navigation so you can prevent duplicate entries in the backstack.
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (navPageType != null && !Type.Equals(preNavPageType, navPageType))
                ContentFrame.Navigate(navPageType, null, transitionInfo);
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            NavView.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType?.FullName != null)
            {
                // Select the nav view item that corresponds to the page being navigated to.
                if (ContentFrame.SourcePageType.FullName != "ASA_Save_Inspector.Pages.HomePage")
                {
                    var menuItems = NavView.MenuItems.OfType<NavigationViewItem>();
                    if (menuItems != null)
                    {
                        var footerItems = NavView.FooterMenuItems.OfType<NavigationViewItem>();
                        if (footerItems != null && footerItems.Count() > 0)
                            foreach (var footerItem in footerItems)
                                menuItems = menuItems.Append(footerItem);
                        if (menuItems.Count() > 0)
                            foreach (var menuItem in menuItems)
                                if (menuItem.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()))
                                {
                                    NavView.SelectedItem = menuItem;
                                    break;
                                }
                    }
                }
            }
        }

        public void ShowPopup(string msg)
        {
            if (!MainWindowPopup.IsOpen)
            {
                tb_popupTitle.Text = msg;
                tb_popupDetails.Text = "";
                MainWindowPopup.IsOpen = true;
            }
        }

        public void HidePopup()
        {
            if (MainWindowPopup.IsOpen)
                MainWindowPopup.IsOpen = false;
        }

        private Brush? _defaultTextColor = App.Current.Resources.ThemeDictionaries["SystemBaseMediumColor"] as SolidColorBrush;
        private Brush _fallbackTextColor = new SolidColorBrush(Colors.Gray);

        public void AddTextToPopupDetails(string txt, Brush? color = null)
        {
            Run run = new Run()
            {
                Text = txt + Environment.NewLine,
                Foreground = (color != null ? color : (_defaultTextColor ?? _fallbackTextColor)),
                FontSize = 16
            };
            tb_popupDetails.Inlines.Add(run);
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
            WindowWidth = Math.Max(1, Convert.ToInt32(Math.Round(this.Bounds.Width)));
            WindowHeight = Math.Max(1, Convert.ToInt32(Math.Round(this.Bounds.Height)) - 52);
        }

#pragma warning disable CS8625
        public void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
#pragma warning restore CS8625

        private void w_MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            AdjustToSizeChange();
        }

        private static readonly Brush _greenNotificationBackground = new SolidColorBrush(Colors.DarkGreen);
        private static readonly Brush _orangeNotificationBackground = new SolidColorBrush(Colors.DarkOrange);
        private static readonly Brush _redNotificationBackground = new SolidColorBrush(Colors.DarkRed);
        private static readonly Brush _grayNotificationBackground = new SolidColorBrush(Colors.DarkGray);

        private void ShowNotificationMsg(string msg, BackgroundColor color = BackgroundColor.DEFAULT, int duration = 3000)
        {
            if (!PopupNotification.IsOpen)
            {
                switch (color)
                {
                    case BackgroundColor.DEFAULT:
                        b_innerPopupNotification.Background = _grayNotificationBackground;
                        break;
                    case BackgroundColor.SUCCESS:
                        b_innerPopupNotification.Background = _greenNotificationBackground;
                        break;
                    case BackgroundColor.WARNING:
                        b_innerPopupNotification.Background = _orangeNotificationBackground;
                        break;
                    case BackgroundColor.ERROR:
                        b_innerPopupNotification.Background = _redNotificationBackground;
                        break;
                    default:
                        b_innerPopupNotification.Background = _grayNotificationBackground;
                        break;
                }
                tb_popupNotificationMsg.Text = msg;
                PopupNotification.IsOpen = true;

                FadeInStoryboard.Begin();
                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    await Task.Delay(duration - 600);
                    FadeOutStoryboard.Begin();
                    await Task.Delay(600);
                    if (PopupNotification.IsOpen)
                    {
                        b_popupNotification.Opacity = 0;
                        PopupNotification.IsOpen = false;
                    }
                });
            }
        }

        public static void ShowToast(string msg, BackgroundColor color = BackgroundColor.DEFAULT, int duration = 4000)
        {
            if (duration < 1100)
                duration = 1100;
#pragma warning disable CS1998
            if (MainWindow._mainWindow != null)
                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    MainWindow._mainWindow.ShowNotificationMsg(msg, color, duration);
                });
#pragma warning restore CS1998
        }

        private void w_MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (SettingsPage.UsePythonVenv())
            {
                var t = PythonManager.DeactivatePythonVenv();
                if (t != null)
                    t.Wait(4000);
            }
            Logger.Instance.Log(ASILang.Get("ASIStopped"));
            App.Current.Exit();
        }

        private void tb_popupDetails_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try { sv_popupDetails.ChangeView(null, sv_popupDetails.ScrollableHeight, null); }
            catch { }
        }

        private async Task<bool> CheckForUpdate()
        {
            string projectFileContent = await PythonManager._client.GetStringAsync(Utils.ASIVersionFileUrl);
            if (string.IsNullOrWhiteSpace(projectFileContent))
            {
                Logger.Instance.Log($"{ASILang.Get("CheckUpdateFailed")} Response=[{(projectFileContent ?? "NULL")}]", Logger.LogLevel.WARNING);
                return false;
            }
            string latestVersion = projectFileContent.Replace("\r\n", "", StringComparison.InvariantCulture).Replace("\n", "", StringComparison.InvariantCulture);
            if (latestVersion.Length < 3 || latestVersion.Length > 7 || !latestVersion.Contains('.', StringComparison.InvariantCulture))
            {
                Logger.Instance.Log($"{ASILang.Get("CheckUpdateFailed")} Response=[{latestVersion}]", Logger.LogLevel.WARNING);
                return false;
            }
            string currentVersion = Utils.GetVersionStr();
            if (string.Compare(latestVersion, currentVersion, StringComparison.InvariantCulture) == 0)
            {
                Logger.Instance.Log(ASILang.Get("ASIIsUpToDate"), Logger.LogLevel.INFO);
                return false;
            }
            if (!UpdateAvailablePopup.IsOpen)
            {
                tb_updateAvailableDescription.Text = $"{ASILang.Get("UpdateAvailableDescription").Replace("#NEW_VERSION#", $"{latestVersion}", StringComparison.InvariantCulture).Replace("#MY_VERSION#", $"{currentVersion}", StringComparison.InvariantCulture)}";
                UpdateAvailablePopup.IsOpen = true;
            }
            return true;
        }

        private async Task CheckForUpdateIfEnabled()
        {
            if (!File.Exists(Utils.DontCheckForUpdateFilePath()))
                await CheckForUpdate();
        }

        private async void btn_InstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateAvailablePopup.IsOpen)
                UpdateAvailablePopup.IsOpen = false;
            try
            {
                _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(Utils.ASILatestVersionUrl));
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("OpenURLFailed").Replace("#URL#", $"{Utils.ASILatestVersionUrl}", StringComparison.InvariantCulture)} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void CloseUpdateAvailablePopup()
        {
            if (UpdateAvailablePopup.IsOpen)
                UpdateAvailablePopup.IsOpen = false;
        }

        private void btn_CloseUpdateAvailablePopup_Click(object sender, RoutedEventArgs e) => CloseUpdateAvailablePopup();

        private void btn_NeverUpdate_Click(object sender, RoutedEventArgs e)
        {
            try { File.WriteAllText(Utils.DontCheckForUpdateFilePath(), "1", Encoding.UTF8); }
            catch { }
            CloseUpdateAvailablePopup();
        }
    }

    public enum BackgroundColor
    {
        DEFAULT = 0,
        SUCCESS = 1,
        WARNING = 2,
        ERROR = 3
    }
}
