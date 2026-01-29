using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
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
        private KeyValuePair<string?, List<string>?>? _previousData = null;

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

            LoadMapsInfo();

            PythonManager.InitHttpClient();
            PythonManager.GetPythonExePaths();
            PythonManager.CreateAsiExportScriptFile(Utils.AsiExportFastOrigFilePath(), Utils.AsiExportFastFilePath());

#pragma warning disable CS4014
            CheckForUpdateAndPreviousData();
#pragma warning restore CS4014
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
            var t = PythonManager.DeactivatePythonVenv();
            if (t != null)
                t.Wait(5000);
            Logger.Instance.Log("ASA Save Inspector has stopped.");
            App.Current.Exit();
        }

        private void LoadMapsInfo()
        {
#if !DEBUG
            string mapsInfoFilepath = Utils.MapsInfoFilePath();
            if (File.Exists(mapsInfoFilepath))
            {
                try
                {
                    string mapsInfoJson = File.ReadAllText(mapsInfoFilepath, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(mapsInfoJson))
                        return;

                    List<ArkMapInfo>? jsonMapsInfo = JsonSerializer.Deserialize<List<ArkMapInfo>>(mapsInfoJson);
                    if (jsonMapsInfo != null)
                        Utils._allMaps = jsonMapsInfo;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Exception caught in LoadMapsInfo. Exception=[{ex}]", Logger.LogLevel.ERROR);
                }
            }
            else
            {
#endif
            Utils.EnsureDataFolderExist();
            try
            {
                string jsonString = JsonSerializer.Serialize<List<ArkMapInfo>>(Utils._allMaps, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.MapsInfoFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in LoadMapsInfo. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
#if !DEBUG
            }
#endif
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
                Minimap.InitMap(points, (mapInfo != null ? mapInfo.MinimapFilename : "TheIsland_Minimap_Margin.jpg"), onDoubleTap);
            }
            else
                Minimap.ChangePoints(points, onDoubleTap);
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
            var t = PythonManager.DeactivatePythonVenv();
            if (t != null)
                t.Wait(4000);
            Logger.Instance.Log(ASILang.Get("ASIStopped"));
            App.Current.Exit();
        }

        private void tb_popupDetails_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try { sv_popupDetails.ChangeView(null, sv_popupDetails.ScrollableHeight, null); }
            catch { }
        }

        private string? _latestVersion = null;

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
            _latestVersion = latestVersion;
            if (!UpdateAvailablePopup.IsOpen)
            {
                tb_updateAvailableDescription.Text = $"{ASILang.Get("UpdateAvailableDescription").Replace("#NEW_VERSION#", $"{latestVersion}", StringComparison.InvariantCulture).Replace("#MY_VERSION#", $"{currentVersion}", StringComparison.InvariantCulture)}";
                UpdateAvailablePopup.IsOpen = true;
            }
            return true;
        }

        private async Task CheckForUpdateAndPreviousData()
        {
            if (!File.Exists(Utils.DontCheckForUpdateFilePath()))
                await CheckForUpdate();

            // DEPRECATED: Checking for previous ASI data is not required anymore.
            //if (!File.Exists(Utils.DontReimportPreviousDataFilePath()))
            //    CheckPreviousData();
        }

        private KeyValuePair<string?, List<string>?> HasASIData(string dir)
        {
            bool foundExports = false;
            string jsonExportsPath = Path.Combine(dir, "data", "json_exports");
            try
            {
                if (Directory.Exists(jsonExportsPath))
                {
                    var subDirs = Directory.GetDirectories(jsonExportsPath);
                    if (subDirs != null && subDirs.Length > 0)
                        foundExports = true;
                }
            }
            catch { foundExports = false; }

            List<string> configFilesFound = new List<string>();
            foreach (var configElem in Utils.ConfigFiles)
                try
                {
                    string configFilePath = Path.Combine(dir, "data", configElem.Key);
                    if (File.Exists(configFilePath))
                        configFilesFound.Add(configFilePath);
                }
                catch { }

            return new KeyValuePair<string?, List<string>?>((foundExports ? jsonExportsPath : null), (configFilesFound.Count > 0 ? configFilesFound : null));
        }

        private KeyValuePair<string?, List<string>?> SearchPreviousData()
        {
            KeyValuePair<string?, List<string>?> emptyResult = new KeyValuePair<string?, List<string>?>(null, null);
            IEnumerable<string>? previousASIFolders = Utils.GetPreviousASIFolders();
            if (previousASIFolders == null)
                return emptyResult;

            foreach (string? sortedDir in previousASIFolders)
                if (!string.IsNullOrEmpty(sortedDir))
                {
                    KeyValuePair<string?, List<string>?> ASIData = HasASIData(sortedDir);
                    if (!string.IsNullOrEmpty(ASIData.Key) || (ASIData.Value != null && ASIData.Value.Count > 0))
                        return ASIData;
                }

            return emptyResult;
        }

        /*
        public async Task<bool> DownloadASI()
        {
            if (string.IsNullOrEmpty(_latestVersion))
                return false;
            try
            {
                Utils.EnsureDataFolderExist();
                string fileToDownload = Utils.ASIArchiveUrl.Replace("VERSIONSTR", _latestVersion, StringComparison.InvariantCulture);
                string filePath = Utils.ASIArchiveFilePath().Replace("VERSIONSTR", _latestVersion, StringComparison.InvariantCulture);
                using var downloadStream = await PythonManager._client.GetStreamAsync(fileToDownload);
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                await downloadStream.CopyToAsync(fileStream);
                await fileStream.FlushAsync();
                fileStream.Close();
                downloadStream.Close();
                if (File.Exists(filePath))
                    return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in DownloadArkParse. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            return false;
        }
        */

        private void OpenRestorePreviousDataPopup()
        {
            tb_PreviousDataFolderPath.Text = string.Empty;
            sp_restorePreviousData.Children.Clear();
            if (_previousData != null && _previousData.HasValue)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_previousData.Value.Key))
                        tb_PreviousDataFolderPath.Text = Path.GetFullPath(Path.Combine(_previousData.Value.Key, ".."));
                    else if (_previousData.Value.Value != null && _previousData.Value.Value.Count > 0)
                    {
                        string? prevConfigFile = _previousData.Value.Value.FirstOrDefault(string.Empty);
                        if (!string.IsNullOrEmpty(prevConfigFile))
                        {
                            string? prevDataDir = Path.GetDirectoryName(prevConfigFile);
                            if (!string.IsNullOrEmpty(prevDataDir))
                                tb_PreviousDataFolderPath.Text = prevDataDir;
                        }
                    }
                }
                catch (Exception ex)
                {
                    tb_PreviousDataFolderPath.Text = string.Empty;
                    Logger.Instance.Log($"Exception caught in OpenRestorePreviousDataPopup. Exception=[{ex}]", Logger.LogLevel.ERROR);
                }
                if (_previousData.Value.Value != null && _previousData.Value.Value.Count > 0)
                    foreach (string configFile in _previousData.Value.Value)
                        if (!string.IsNullOrEmpty(configFile))
                        {
                            string? fileName = null;
                            try { fileName = Path.GetFileName(configFile); }
                            catch { fileName = null; }
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                TextBlock tb = new TextBlock()
                                {
                                    FontSize = 14.0d,
                                    TextWrapping = TextWrapping.Wrap,
                                    Text = (Utils.ConfigFiles.ContainsKey(fileName) ? ASILang.Get(Utils.ConfigFiles[fileName]) : fileName),
                                    VerticalAlignment = VerticalAlignment.Top,
                                    HorizontalAlignment = HorizontalAlignment.Left
                                };
                                sp_restorePreviousData.Children.Add(tb);
                            }
                        }
                if (!string.IsNullOrEmpty(_previousData.Value.Key))
                {
                    TextBlock tb = new TextBlock()
                    {
                        FontSize = 14.0d,
                        TextWrapping = TextWrapping.Wrap,
                        Text = ASILang.Get("JsonData"),
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    sp_restorePreviousData.Children.Add(tb);
                }
            }
            RestorePreviousDataPopup.IsOpen = true;
        }

        private void CheckPreviousData()
        {
            KeyValuePair<string?, List<string>?> previousData = SearchPreviousData();
            if (!string.IsNullOrEmpty(previousData.Key) || (previousData.Value != null && previousData.Value.Count > 0))
            {
                _previousData = previousData;
                if (!UpdateAvailablePopup.IsOpen) // Don't open "Update available" and "Restore previous data" popups at the same time.
                    if (!RestorePreviousDataPopup.IsOpen)
                        OpenRestorePreviousDataPopup();
            }
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

            if (_previousData != null)
                if (!RestorePreviousDataPopup.IsOpen)
                    OpenRestorePreviousDataPopup();
        }

        private void CloseUpdateAvailablePopup()
        {
            if (UpdateAvailablePopup.IsOpen)
                UpdateAvailablePopup.IsOpen = false;

            if (_previousData != null)
                if (!RestorePreviousDataPopup.IsOpen)
                    OpenRestorePreviousDataPopup();
        }

        private void btn_CloseUpdateAvailablePopup_Click(object sender, RoutedEventArgs e) => CloseUpdateAvailablePopup();

        private void btn_NeverUpdate_Click(object sender, RoutedEventArgs e)
        {
            try { File.WriteAllText(Utils.DontCheckForUpdateFilePath(), "1", Encoding.UTF8); }
            catch { }
            CloseUpdateAvailablePopup();
        }

        private void CloseReimportPreviousDataPopup()
        {
            _previousData = null;
            if (RestorePreviousDataPopup.IsOpen)
                RestorePreviousDataPopup.IsOpen = false;
        }

        private string GetRestartArgs()
        {
            /*
            // Using Environment.GetCommandLineArgs() to retrieve the command line arguments
            string[] commandLineArguments = Environment.GetCommandLineArgs();
            if (commandLineArguments.Length > 1)
            {
                commandLineArguments = commandLineArguments.Skip(1).ToArray();
                return String.Join(",", commandLineArguments);
            }
            */

            // Using AppInstance.GetActivatedEventArgs() to retrieve the command line arguments
            AppActivationArguments activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            ExtendedActivationKind kind = activatedArgs.Kind;

            if (kind == ExtendedActivationKind.Launch)
            {
                if (activatedArgs.Data is ILaunchActivatedEventArgs launchArgs)
                {
                    string argString = launchArgs.Arguments;
                    string[] argStrings = argString.Split();

                    if (argStrings.Length > 1)
                    {
                        argStrings = argStrings.Skip(1).ToArray();
                        return String.Join(",", argStrings.Where(s => !string.IsNullOrEmpty(s)));
                    }
                }
            }

            return string.Empty;
        }

        private bool RestartASI()
        {
            string restartArgs = GetRestartArgs();
            AppRestartFailureReason restartError = AppInstance.Restart(restartArgs);

            bool restartFailed = false;
            switch (restartError)
            {
                case AppRestartFailureReason.RestartPending:
                    restartFailed = true;
                    break;
                case AppRestartFailureReason.NotInForeground:
                    restartFailed = true;
                    break;
                case AppRestartFailureReason.InvalidUser:
                    restartFailed = true;
                    break;
                case AppRestartFailureReason.Other:
                    restartFailed = true;
                    break;
            }
            return restartFailed;
        }

        private void ReplaceFolderPathInExportProfilesFile(string sourceFilePath, string destFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath) || string.IsNullOrEmpty(destFilePath))
                return;

            string? fileContent = null;
            try { fileContent = File.ReadAllText(destFilePath, Encoding.UTF8); }
            catch { fileContent = null; }
            if (!string.IsNullOrEmpty(fileContent))
            {
                string? sourceDir = null;
                string? destDir = null;
                try
                {
                    sourceDir = Path.GetDirectoryName(sourceFilePath);
                    destDir = Path.GetDirectoryName(destFilePath);
                    sourceDir = JsonSerializer.Serialize(sourceDir);
                    destDir = JsonSerializer.Serialize(destDir);
                    // Remove quotes
                    if (sourceDir.Length > 2)
                        sourceDir = sourceDir.Substring(1, sourceDir.Length - 2);
                    if (destDir.Length > 2)
                        destDir = destDir.Substring(1, destDir.Length - 2);
                }
                catch
                {
                    sourceDir = null;
                    destDir = null;
                }
                if (!string.IsNullOrEmpty(sourceDir) && !string.IsNullOrEmpty(destDir))
                {

                    fileContent = fileContent.Replace(sourceDir, destDir, StringComparison.InvariantCulture);
                    try { File.WriteAllText(destFilePath, fileContent, Encoding.UTF8); }
                    catch { }
                }
            }
        }

        private void btn_ReimportPreviousData_Click(object sender, RoutedEventArgs e)
        {
            if (_previousData != null && _previousData.HasValue)
            {
                if (!string.IsNullOrEmpty(_previousData.Value.Key) && Directory.Exists(_previousData.Value.Key))
                    Utils.MoveDirectory(_previousData.Value.Key, Utils.JsonExportsFolder());
                if (_previousData.Value.Value != null && _previousData.Value.Value.Count > 0)
                    foreach (string configFile in _previousData.Value.Value)
                        if (!string.IsNullOrEmpty(configFile) && File.Exists(configFile))
                        {
                            string? destFilePath = null;
                            try { destFilePath = Path.Combine(Utils.GetDataDir(), Path.GetFileName(configFile)); }
                            catch (Exception ex1)
                            {
                                destFilePath = null;
                                Logger.Instance.Log($"Exception caught in btn_ReimportPreviousData_Click. Could not determine path for file \"{configFile}\" with directory \"{Utils.GetDataDir()}\". Exception=[{ex1}]", Logger.LogLevel.ERROR);
                            }
                            if (destFilePath != null)
                            {
                                try { File.Copy(configFile, destFilePath, true); }
                                catch (Exception ex2) { Logger.Instance.Log($"Exception caught in btn_ReimportPreviousData_Click. Could not copy file \"{configFile}\" to \"{destFilePath}\". Exception=[{ex2}]", Logger.LogLevel.ERROR); }
                                if (File.Exists(destFilePath) && string.Compare(Path.GetFileName(destFilePath).ToLowerInvariant(), "export_profiles.json", StringComparison.InvariantCulture) == 0)
                                    ReplaceFolderPathInExportProfilesFile(configFile, destFilePath);
                            }
                        }

                bool restartFailed = false;
                try { restartFailed = RestartASI(); }
                catch { restartFailed = true; }
                if (restartFailed)
                {
                    // Try shutdown app if restart failed.
                    try { App.Current.Exit(); }
                    catch { MainWindow.ShowToast($"{ASILang.Get("UnableToRestartASI")} {ASILang.Get("PleaseRestartASIManually")}", BackgroundColor.ERROR, 6000); }
                }
            }
            try { File.WriteAllText(Utils.DontReimportPreviousDataFilePath(), "1", Encoding.UTF8); }
            catch { }
            CloseReimportPreviousDataPopup();
        }

        private void btn_DontReimportPreviousData_Click(object sender, RoutedEventArgs e) => CloseReimportPreviousDataPopup();

        private void btn_NeverReimportPreviousData_Click(object sender, RoutedEventArgs e)
        {
            try { File.WriteAllText(Utils.DontReimportPreviousDataFilePath(), "1", Encoding.UTF8); }
            catch { }
            CloseReimportPreviousDataPopup();
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
