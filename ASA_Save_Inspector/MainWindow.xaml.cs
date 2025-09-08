using ASA_Save_Inspector.Pages;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
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
            this.Title = _appName;
            TitleBarTextBlock.Text = _appName;

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
            PythonManager.CreateAsiExportScriptFile(Utils.AsiExportAllOrigFilePath(), Utils.AsiExportAllFilePath());
            PythonManager.CreateAsiExportScriptFile(Utils.AsiExportFastOrigFilePath(), Utils.AsiExportFastFilePath());
            //PythonManager.InstallArkParse();

            CheckForUpdate();

            /*
            AcrylicBrush myBrush = new AcrylicBrush();
            myBrush.TintColor = Color.FromArgb(255, 202, 24, 37);
            myBrush.FallbackColor = Color.FromArgb(255, 202, 24, 37);
            myBrush.TintOpacity = 0.6;
            */
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
            }
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

        private async void CheckForUpdate()
        {
            string projectFileContent = await PythonManager._client.GetStringAsync(Utils.ASIVersionFileUrl);
            if (string.IsNullOrWhiteSpace(projectFileContent))
            {
                Logger.Instance.Log($"{ASILang.Get("CheckUpdateFailed")} Response=[{(projectFileContent ?? "NULL")}]", Logger.LogLevel.WARNING);
                return;
            }
            string latestVersion = projectFileContent.Replace("\r\n", "", StringComparison.InvariantCulture).Replace("\n", "", StringComparison.InvariantCulture);
            if (latestVersion.Length < 3 || latestVersion.Length > 7 || !latestVersion.Contains('.', StringComparison.InvariantCulture))
            {
                Logger.Instance.Log($"{ASILang.Get("CheckUpdateFailed")} Response=[{latestVersion}]", Logger.LogLevel.WARNING);
                return;
            }
            string currentVersion = Utils.GetVersionStr();
            if (string.Compare(latestVersion, currentVersion, StringComparison.InvariantCulture) == 0)
            {
                Logger.Instance.Log(ASILang.Get("ASIIsUpToDate"), Logger.LogLevel.INFO);
                return;
            }
            _latestVersion = latestVersion;
            if (!UpdateAvailablePopup.IsOpen)
            {
                tb_updateAvailableDescription.Text = $"{ASILang.Get("UpdateAvailableDescription").Replace("#NEW_VERSION#", $"{latestVersion}", StringComparison.InvariantCulture).Replace("#MY_VERSION#", $"{currentVersion}", StringComparison.InvariantCulture)}";
                UpdateAvailablePopup.IsOpen = true;
            }
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

        private void btn_CloseUpdateAvailablePopup_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateAvailablePopup.IsOpen)
                UpdateAvailablePopup.IsOpen = false;
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
