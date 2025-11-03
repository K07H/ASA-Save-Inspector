using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class OtherPage : Page, INotifyPropertyChanged
    {
        private bool _initialized = false;

        public static OtherPage? _page = null;

        public OtherPage()
        {
            InitializeComponent();

            _page = this;

            // Calculate page center.
            AdjustToSizeChange();

            cb_AppTheme.IsChecked = (SettingsPage._darkTheme != null && SettingsPage._darkTheme.HasValue && !SettingsPage._darkTheme.Value ? false : true);
            cb_DebugLogging.IsChecked = (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value);

            SettingsPage.LoadCustomBlueprints();
            RefreshRegisteredBlueprints();

            Task.Run(() => ComputeJsonExportsFolderSize());
            Task.Run(() => ComputeASIDataFolderTotalSize());
            Task.Run(() => CheckForPreviousInstallsToRemove());

            _initialized = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private string? _asiDataFolderTotalSize = null;
        public string ASIDataFolderTotalSize
        {
            get { return _asiDataFolderTotalSize ?? ASILang.Get("Computing"); }
            set { _asiDataFolderTotalSize = value; }
        }

        private void UpdateASIDataFolderTotalSize(string folderSizeStr)
        {
            try
            {
                _asiDataFolderTotalSize = folderSizeStr;
                if (PropertyChanged != null)
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ASIDataFolderTotalSize)));
            }
            catch { }
        }

        private void ComputeASIDataFolderTotalSize()
        {
            try
            {
                string? folderPath = GetASIDataFolderPath();
                if (folderPath != null)
                {
                    double folderSize = Utils.GetDirectorySize(folderPath);
                    string folderSizeStr = Utils.BytesSizeToReadableString(folderSize);
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => UpdateASIDataFolderTotalSize(folderSizeStr));
                }
            }
            catch { }
        }

        private string? _jsonExportsFolderSize = null;
        public string JsonExportsFolderSize
        {
            get { return _jsonExportsFolderSize ?? ASILang.Get("Computing"); }
            set { _jsonExportsFolderSize = value; }
        }

        private void UpdateJsonExportsFolderSize(string folderSizeStr)
        {
            try
            {
                _jsonExportsFolderSize = folderSizeStr;
                if (PropertyChanged != null)
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(JsonExportsFolderSize)));
            }
            catch { }
        }

        private void ComputeJsonExportsFolderSize()
        {
            try
            {
                string? folderPath = GetJsonExportsFolderPath();
                if (folderPath != null)
                {
                    double folderSize = Utils.GetDirectorySize(folderPath);
                    if (folderSize > (20.0d * 1024.0d * 1024.0d * 1024.0d)) // If greater than 20 GB
                        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => sp_RemoveJsonData.Visibility = Visibility.Visible);
                    string folderSizeStr = Utils.BytesSizeToReadableString(folderSize);
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => UpdateJsonExportsFolderSize(folderSizeStr));
                }
            }
            catch { }
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

#pragma warning disable CS8625
        public void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
#pragma warning restore CS8625

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

        private void page_SizeChanged(object sender, SizeChangedEventArgs e) => AdjustToSizeChange();

        private string? GetASIDataFolderPath()
        {
            string folderPath = Utils.GetDataDir();
            if (Directory.Exists(folderPath))
                return folderPath;
            folderPath = Utils.GetBaseDir();
            if (Directory.Exists(folderPath))
                return folderPath;
            return null;
        }

        private string? GetJsonExportsFolderPath()
        {
            string folderPath = Utils.JsonExportsFolder();
            if (Directory.Exists(folderPath))
                return folderPath;
            return null;
        }

        private void btn_OpenAppDataFolder_Click(object sender, RoutedEventArgs e)
        {
            string? folderPath = GetASIDataFolderPath();
            if (folderPath != null)
            {
                Logger.Instance.Log($"AppDataFolderPath=[{folderPath}]");
                Process.Start(Environment.GetEnvironmentVariable("WINDIR") + @"\explorer.exe", $"\"{folderPath}\"");
            }
            else
                MainWindow.ShowToast(ASILang.Get("CannotFindASIDataFolder"), BackgroundColor.ERROR);
        }

        private void btn_OpenMinimap_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.OpenMinimap();
#if DEBUG
            MainWindow.TestAddPointsMinimap();
#endif
        }

#pragma warning disable CS1998
        private void btn_ForceReinstallArkParse_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(Utils.ArkParseFolder()))
            {
                try
                {
                    Directory.Delete(Utils.ArkParseFolder(), true);
                    if (MainWindow._mainWindow != null)
                        MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                        {
                            if (MainWindow._mainWindow != null)
                            {
                                if (MainWindow._mainWindow._navView != null)
                                    MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnSettings;
                                MainWindow._mainWindow.NavView_Navigate(typeof(SettingsPage), new EntranceNavigationTransitionInfo());
                            }
                        });
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Failed to delete ArkParse folder. Exception=[{ex}]", Logger.LogLevel.ERROR);
                    MainWindow.ShowToast(ASILang.Get("ReinstallArkParseFailed"));
                }
            }
        }
#pragma warning restore CS1998

        private void ResetCustomBlueprintPopup()
        {
            tb_BlueprintType.Text = ASILang.Get("ClickHere");
            tb_BlueprintClass.Text = "";
            sp_BlueprintClass.Visibility = Visibility.Collapsed;
            tb_RegisteredBlueprintsType.Text = ASILang.Get("ClickHere");
            sp_RegisteredDinoBlueprintsBlock.Visibility = Visibility.Collapsed;
            sp_RegisteredItemBlueprintsBlock.Visibility = Visibility.Collapsed;
            sp_RegisteredStructureBlueprintsBlock.Visibility = Visibility.Collapsed;
        }

        private void BlueprintTypeSelected(string? blueprintType)
        {
            if (!string.IsNullOrEmpty(blueprintType))
            {
                tb_BlueprintType.Text = blueprintType;
                sp_BlueprintClass.Visibility = Visibility.Visible;
            }
        }

        private string? GetSelectedMenuFlyoutItem(object sender)
        {
            MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
            if (mfi != null)
                return mfi.Text;
            return null;
        }

        private Grid? GetUIElemForBlueprint(string blueprint, RoutedEventHandler clickEventFunc)
        {
            if (string.IsNullOrWhiteSpace(blueprint))
                return null;

            Grid grd = new Grid()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0.0d, 5.0d, 0.0d, 0.0d)
            };
            grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            TextBox tb = new TextBox()
            {
                FontSize = 12.0d,
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = false,
                Text = blueprint,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 500.0d,
                IsReadOnly = true
            };
            Button btn = new Button()
            {
                Width = 120.0d,
                Content = ASILang.Get("Remove"),
                FontSize = 12.0d,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
            };
            btn.Click += clickEventFunc;
            grd.Children.Add(btn);
            Grid.SetColumn(btn, 0);
            grd.Children.Add(tb);
            Grid.SetColumn(tb, 1);

            return grd;
        }

        private void RefreshRegisteredBlueprints()
        {
            sp_RegisteredDinoBlueprints.Children.Clear();
            sp_RegisteredItemBlueprints.Children.Clear();
            sp_RegisteredStructureBlueprints.Children.Clear();

            if (SettingsPage._customBlueprints?.Dinos != null && SettingsPage._customBlueprints.Dinos.Count > 0)
                foreach (string dinoBP in SettingsPage._customBlueprints.Dinos)
                    if (!string.IsNullOrWhiteSpace(dinoBP))
                    {
                        Grid? grd = GetUIElemForBlueprint(dinoBP, btn_RemoveCustomDinoBlueprint_Click);
                        if (grd != null)
                            sp_RegisteredDinoBlueprints.Children.Add(grd);
                    }

            if (SettingsPage._customBlueprints?.Items != null && SettingsPage._customBlueprints.Items.Count > 0)
                foreach (string itemBP in SettingsPage._customBlueprints.Items)
                    if (!string.IsNullOrWhiteSpace(itemBP))
                    {
                        Grid? grd = GetUIElemForBlueprint(itemBP, btn_RemoveCustomItemBlueprint_Click);
                        if (grd != null)
                            sp_RegisteredItemBlueprints.Children.Add(grd);
                    }

            if (SettingsPage._customBlueprints?.Structures != null && SettingsPage._customBlueprints.Structures.Count > 0)
                foreach (string structureBP in SettingsPage._customBlueprints.Structures)
                    if (!string.IsNullOrWhiteSpace(structureBP))
                    {
                        Grid? grd = GetUIElemForBlueprint(structureBP, btn_RemoveCustomStructureBlueprint_Click);
                        if (grd != null)
                            sp_RegisteredStructureBlueprints.Children.Add(grd);
                    }
        }

        private void btn_RemoveCustomDinoBlueprint_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn == null)
                return;
            Grid? grd = btn.Parent as Grid;
            if (grd == null)
                return;
            TextBox? tb = grd.Children[1] as TextBox;
            if (tb == null)
                return;
            if (SettingsPage._customBlueprints?.Dinos == null || SettingsPage._customBlueprints.Dinos.Count <= 0)
                return;
            string? blueprint = tb.Text;
            if (string.IsNullOrWhiteSpace(blueprint))
                return;

            int toDel = -1;
            for (int i = 0; i < SettingsPage._customBlueprints.Dinos.Count; i++)
                if (string.Compare(blueprint, SettingsPage._customBlueprints.Dinos[i], StringComparison.InvariantCulture) == 0)
                {
                    toDel = i;
                    break;
                }

            if (toDel >= 0)
            {
                SettingsPage._customBlueprints.Dinos.RemoveAt(toDel);
                SettingsPage.SaveCustomBlueprints();
                RefreshRegisteredBlueprints();
                MainWindow.ShowToast(ASILang.Get("CustomDinoBlueprintUnregistered"), BackgroundColor.SUCCESS);
            }
        }

        private void btn_RemoveCustomItemBlueprint_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn == null)
                return;
            Grid? grd = btn.Parent as Grid;
            if (grd == null)
                return;
            TextBox? tb = grd.Children[1] as TextBox;
            if (tb == null)
                return;
            if (SettingsPage._customBlueprints?.Items == null || SettingsPage._customBlueprints.Items.Count <= 0)
                return;
            string? blueprint = tb.Text;
            if (string.IsNullOrWhiteSpace(blueprint))
                return;

            int toDel = -1;
            for (int i = 0; i < SettingsPage._customBlueprints.Items.Count; i++)
                if (string.Compare(blueprint, SettingsPage._customBlueprints.Items[i], StringComparison.InvariantCulture) == 0)
                {
                    toDel = i;
                    break;
                }

            if (toDel >= 0)
            {
                SettingsPage._customBlueprints.Items.RemoveAt(toDel);
                SettingsPage.SaveCustomBlueprints();
                RefreshRegisteredBlueprints();
                MainWindow.ShowToast(ASILang.Get("CustomItemBlueprintUnregistered"), BackgroundColor.SUCCESS);
            }
        }

        private void btn_RemoveCustomStructureBlueprint_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn == null)
                return;
            Grid? grd = btn.Parent as Grid;
            if (grd == null)
                return;
            TextBox? tb = grd.Children[1] as TextBox;
            if (tb == null)
                return;
            if (SettingsPage._customBlueprints?.Structures == null || SettingsPage._customBlueprints.Structures.Count <= 0)
                return;
            string? blueprint = tb.Text;
            if (string.IsNullOrWhiteSpace(blueprint))
                return;

            int toDel = -1;
            for (int i = 0; i < SettingsPage._customBlueprints.Structures.Count; i++)
                if (string.Compare(blueprint, SettingsPage._customBlueprints.Structures[i], StringComparison.InvariantCulture) == 0)
                {
                    toDel = i;
                    break;
                }

            if (toDel >= 0)
            {
                SettingsPage._customBlueprints.Structures.RemoveAt(toDel);
                SettingsPage.SaveCustomBlueprints();
                RefreshRegisteredBlueprints();
                MainWindow.ShowToast(ASILang.Get("CustomStructureBlueprintUnregistered"), BackgroundColor.SUCCESS);
            }
        }

        private void btn_ConfigureCustomBlueprints_Click(object sender, RoutedEventArgs e)
        {
            if (AddCustomBlueprintsPopup.IsOpen)
                return;

            ResetCustomBlueprintPopup();
            AddCustomBlueprintsPopup.IsOpen = true;
        }

        private void mfi_BlueprintTypeDino_Click(object sender, RoutedEventArgs e) => BlueprintTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void mfi_BlueprintTypeItem_Click(object sender, RoutedEventArgs e) => BlueprintTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void mfi_BlueprintTypeStructure_Click(object sender, RoutedEventArgs e) => BlueprintTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void btn_CloseAddCustomBlueprintsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddCustomBlueprintsPopup.IsOpen)
            {
                ResetCustomBlueprintPopup();
                AddCustomBlueprintsPopup.IsOpen = false;
            }
        }

        private void btn_AddCustomBlueprint_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tb_BlueprintClass.Text))
            {
                MainWindow.ShowToast(ASILang.Get("CustomBlueprintNeedsClassName"), BackgroundColor.WARNING);
                return;
            }

            bool isDinoBP = (string.Compare(tb_BlueprintType.Text, ASILang.Get("DinoBlueprint"), StringComparison.InvariantCulture) == 0);
            bool isItemBP = (string.Compare(tb_BlueprintType.Text, ASILang.Get("ItemBlueprint"), StringComparison.InvariantCulture) == 0);
            bool isStructureBP = (string.Compare(tb_BlueprintType.Text, ASILang.Get("StructureBlueprint"), StringComparison.InvariantCulture) == 0);
            if (!isDinoBP && !isItemBP && !isStructureBP)
            {
                MainWindow.ShowToast(ASILang.Get("BadBlueprintTypeSelected"), BackgroundColor.WARNING);
                return;
            }

            if (isDinoBP)
            {
                if (SettingsPage._customBlueprints?.Dinos != null)
                {
                    if (SettingsPage._customBlueprints.Dinos.Count > 0)
                        foreach (string str in SettingsPage._customBlueprints.Dinos)
                            if (string.Compare(str, tb_BlueprintClass.Text, StringComparison.InvariantCulture) == 0)
                            {
                                MainWindow.ShowToast(ASILang.Get("CustomDinoBlueprintAlreadyRegistered"), BackgroundColor.WARNING);
                                return;
                            }
                    SettingsPage._customBlueprints.Dinos.Add(tb_BlueprintClass.Text);
                    MainWindow.ShowToast(ASILang.Get("CustomDinoBlueprintAdded"), BackgroundColor.SUCCESS);
                }
            }
            else if (isItemBP)
            {
                if (SettingsPage._customBlueprints?.Items != null)
                {
                    if (SettingsPage._customBlueprints.Items.Count > 0)
                        foreach (string str in SettingsPage._customBlueprints.Items)
                            if (string.Compare(str, tb_BlueprintClass.Text, StringComparison.InvariantCulture) == 0)
                            {
                                MainWindow.ShowToast(ASILang.Get("CustomItemBlueprintAlreadyRegistered"), BackgroundColor.WARNING);
                                return;
                            }
                    SettingsPage._customBlueprints.Items.Add(tb_BlueprintClass.Text);
                    MainWindow.ShowToast(ASILang.Get("CustomItemBlueprintAdded"), BackgroundColor.SUCCESS);
                }
            }
            else if (isStructureBP)
            {
                if (SettingsPage._customBlueprints?.Structures != null)
                {
                    if (SettingsPage._customBlueprints.Structures.Count > 0)
                        foreach (string str in SettingsPage._customBlueprints.Structures)
                            if (string.Compare(str, tb_BlueprintClass.Text, StringComparison.InvariantCulture) == 0)
                            {
                                MainWindow.ShowToast(ASILang.Get("CustomStructureBlueprintAlreadyRegistered"), BackgroundColor.WARNING);
                                return;
                            }
                    SettingsPage._customBlueprints.Structures.Add(tb_BlueprintClass.Text);
                    MainWindow.ShowToast(ASILang.Get("CustomStructureBlueprintAdded"), BackgroundColor.SUCCESS);
                }
            }
            SettingsPage.SaveCustomBlueprints();
            RefreshRegisteredBlueprints();
        }

        private void tb_BlueprintClass_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_AddCustomBlueprint.IsEnabled = (!string.IsNullOrWhiteSpace(tb_BlueprintClass.Text) && tb_BlueprintClass.Text.Length > 0);
        }

        private void RegisteredBlueprintsTypeSelected(string? blueprintType)
        {
            sp_RegisteredDinoBlueprintsBlock.Visibility = Visibility.Collapsed;
            sp_RegisteredItemBlueprintsBlock.Visibility = Visibility.Collapsed;
            sp_RegisteredStructureBlueprintsBlock.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(blueprintType))
                return;

            tb_RegisteredBlueprintsType.Text = blueprintType;

            if (string.Compare(blueprintType, ASILang.Get("DinoBlueprints"), StringComparison.InvariantCulture) == 0)
                sp_RegisteredDinoBlueprintsBlock.Visibility = Visibility.Visible;
            else if (string.Compare(blueprintType, ASILang.Get("ItemBlueprints"), StringComparison.InvariantCulture) == 0)
                sp_RegisteredItemBlueprintsBlock.Visibility = Visibility.Visible;
            else if (string.Compare(blueprintType, ASILang.Get("StructureBlueprints"), StringComparison.InvariantCulture) == 0)
                sp_RegisteredStructureBlueprintsBlock.Visibility = Visibility.Visible;
        }

        private void mfi_RegisteredBlueprintTypeDinos_Click(object sender, RoutedEventArgs e) => RegisteredBlueprintsTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void mfi_RegisteredBlueprintTypeItems_Click(object sender, RoutedEventArgs e) => RegisteredBlueprintsTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void mfi_RegisteredBlueprintTypeStructures_Click(object sender, RoutedEventArgs e) => RegisteredBlueprintsTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void SwitchAppTheme(bool darkTheme, bool showToast)
        {
            string themeLabel = (darkTheme ? ASILang.Get("ApplicationTheme_Dark") : ASILang.Get("ApplicationTheme_Light"));
            cb_AppTheme.Content = themeLabel;
            SettingsPage._darkTheme = darkTheme;
            SettingsPage.SaveSettings();
            if (showToast)
                MainWindow.ShowToast($"{ASILang.Get("ThemeSwitched_RestartAppToApplyChanges").Replace("#THEME_NAME#", $"{themeLabel}", StringComparison.InvariantCulture)}", BackgroundColor.SUCCESS, 4000);
        }

        private void cb_AppTheme_Checked(object sender, RoutedEventArgs e) => SwitchAppTheme(true, _initialized);

        private void cb_AppTheme_Unchecked(object sender, RoutedEventArgs e) => SwitchAppTheme(false, _initialized);

        private void RemovePreviousInstalls()
        {
            IEnumerable<string>? previousFolders = Utils.GetPreviousASIFolders();
            if (previousFolders != null && previousFolders.Count() > 0)
                foreach (string folder in previousFolders)
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        try { Directory.Delete(folder, true); }
                        catch (Exception ex)
                        {
                            Logger.Instance.Log($"Failed to delete previous ASI installation located at [{folder}]. Exception=[{ex}]", Logger.LogLevel.WARNING);
                        }
                    }
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                tb_RemovingPreviousInstalls.Visibility = Visibility.Collapsed;
                sp_RemovePreviousInstalls.Visibility = Visibility.Collapsed;
                btn_RemovePreviousInstalls.IsEnabled = true;
            });
        }

        private void btn_RemovePreviousInstalls_Click(object sender, RoutedEventArgs e)
        {
            btn_RemovePreviousInstalls.IsEnabled = false;
            tb_RemovingPreviousInstalls.Visibility = Visibility.Visible;
            Task.Run(() => RemovePreviousInstalls());
        }

        private void CheckForPreviousInstallsToRemove()
        {
            IEnumerable<string>? previousFolders = Utils.GetPreviousASIFolders();
            if (previousFolders != null && previousFolders.Count() > 0)
            {
                double totalSize = 0.0d;
                foreach (string folder in previousFolders)
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                        totalSize += Utils.GetDirectorySize(folder);
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    run_RemovePreviousInstalls.Text = ASILang.Get("PrivousInstallsFound_Description").Replace("#STORAGE_SIZE#", Utils.BytesSizeToReadableString(totalSize), StringComparison.InvariantCulture);
                    sp_RemovePreviousInstalls.Visibility = Visibility.Visible;
                });
            }
            else
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => sp_RemovePreviousInstalls.Visibility = Visibility.Collapsed);
        }

        private void RemoveOldJsonData()
        {
            if (SettingsPage._jsonExportProfiles == null || SettingsPage._jsonExportProfiles.Count <= 0)
                SettingsPage.LoadJsonExportProfiles();
            if (SettingsPage._jsonExportProfiles != null && SettingsPage._jsonExportProfiles.Count > 0)
            {
                var groupedProfiles = SettingsPage._jsonExportProfiles.OrderByDescending(o => o.ID).ToList().GroupBy(o => o.SaveFilePath, (key, list) => new GroupInfoCollection<string, JsonExportProfile>(key, list));
                if (groupedProfiles != null && groupedProfiles.Count() > 0)
                {
                    foreach (var group in groupedProfiles)
                        if (group != null && group.Count() > 1)
                        {
                            bool isFirst = true;
                            foreach (var jep in group)
                                if (jep != null)
                                {
                                    if (isFirst)
                                        isFirst = false;
                                    else
                                        try { SettingsPage.DeleteJsonExportProfile(jep); }
                                        catch { }
                                }
                        }
                    Task.Run(() => ComputeJsonExportsFolderSize());
                    Task.Run(() => ComputeASIDataFolderTotalSize());
                }
            }
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                tb_RemovingJsonData.Visibility = Visibility.Collapsed;
                sp_RemoveJsonData.Visibility = Visibility.Collapsed;
                btn_RemoveJsonData.IsEnabled = true;
            });
        }

        private void btn_RemoveJsonData_Click(object sender, RoutedEventArgs e)
        {
            btn_RemoveJsonData.IsEnabled = false;
            tb_RemovingJsonData.Visibility = Visibility.Visible;
            Task.Run(() => RemoveOldJsonData());
        }

        private void cb_DebugLogging_Checked(object sender, RoutedEventArgs e)
        {
            SettingsPage._debugLogging = true;
            SettingsPage.SaveSettings();
        }

        private void cb_DebugLogging_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingsPage._debugLogging = false;
            SettingsPage.SaveSettings();
        }
    }
}
