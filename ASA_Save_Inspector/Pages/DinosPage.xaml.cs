using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.ObjectModelUtils;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ASA_Save_Inspector.Pages
{
    public abstract class DinosPageBase : DataPageBase<Dino> { }

    public sealed partial class DinosPage : DinosPageBase
    {
        #region Page specific members (see DataPageBase)

        public static DinosPage? _page => (_currentPage as DinosPage);

        public static bool ApplyCurrentQuery() => ApplyCurrentQueryCore("Dinos");

        public DinosPage()
        {
            InitializeComponent();
            InitializePage(new DataPageControls()
            {
                MainGrid = gr_Main,
                Title = tb_Title,
                SortAndFilters = sp_SortAndFilters,
                EditFilters = sp_EditFilters,
                EditFiltersGroup = sp_EditFiltersGroup,
                EditColumns = sp_EditColumns,
                CurrentSort = sp_CurrentSort,
                ExistingColumns = sp_ExistingColumns,
                PrimarySort = tb_PrimarySort,
                SecondarySort = tb_SecondarySort,
                PrimarySortRun = run_PrimarySort,
                SecondarySortRun = run_SecondarySort,
                NbLinesSelectedRun = run_NbLinesSelected,
                DataGrid = dg_Data,
                GetAllJsonMenuItem = mfi_contextMenuGetAllJson,
                ModifyColumnsPopup = ModifyColumnsPopup,
                ColumnsPresetsPopup = ColumnsPresetsPopup,
                ColumnsPresetName = tb_ColumnsPresetName,
                SaveCurrentColumns = btn_SaveCurrentColumns,
                ExistingColumnsPreset = tb_ExistingColumnsPreset,
                ExistingColumnsPresets = mf_ExistingColumnsPresets,
                LoadColumnsPreset = btn_LoadColumnsPreset,
                RemoveColumnsPreset = btn_RemoveColumnsPreset,
                SetDefaultColumnsPreset = btn_SetDefaultColumnsPreset,
                ExistingQueries = cbb_ExistingQueries,
                LoadQuery = btn_LoadQuery,
                EditQuery = btn_EditQuery,
                DeleteQuery = btn_DeleteQuery,
                QuickSearch = tb_QuickSearch
            });
            InitTribesQuickFilter();
            InitDinosQuickFilter();
        }

        protected override SearchType PageSearchType => SearchType.DINOS;
        protected override List<Dino>? AllData => SettingsPage._dinosData;
        protected override string PageLabel => "Dinos";
        protected override IReadOnlyList<string> DefaultSelectedColumns => DinoUtils.DefaultSelectedColumns;
        protected override IReadOnlyList<string> DefaultColumnsOrder => DinoUtils.DefaultColumnsOrder;
        protected override string? GetCleanNameFromPropertyName(string? propertyName) => DinoUtils.GetCleanNameFromPropertyName(propertyName);
        protected override string? GetPropertyNameFromCleanName(string? cleanName) => DinoUtils.GetPropertyNameFromCleanName(cleanName);
        protected override string ColumnsOrderFilePath => Utils.DinoColumnsOrderFilePath();
        protected override string ColumnsPresetsFilePath => Utils.DinoColumnsPresetsFilePath();

        protected override string? DefaultColumnsPresetName
        {
            get => SettingsPage._defaultColumnsPreset_Dinos;
            set => SettingsPage._defaultColumnsPreset_Dinos = value;
        }

        protected override string? GetObjectID(Dino dino) => (dino.DinoID1 != null && dino.DinoID1.HasValue && dino.DinoID2 != null && dino.DinoID2.HasValue ? dino.GetDinoID() : null);

        protected override void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        protected override void RefreshMinimap(IEnumerable<Dino> dinos)
        {
            var dinosPoints = dinos.Select(d =>
            {
                string dinoIDStr = d.GetDinoID();
                KeyValuePair<double, double> minimapCoords = new KeyValuePair<double, double>(0.0d, 0.0d);
                string? subMapName = null;
                if (d.bIsCryopodded != null && d.bIsCryopodded.HasValue && d.bIsCryopodded.Value)
                {
                    var cryopodPos = Utils.GetCryopodCoordsByUUID(d.CryopodUUID);
                    var coords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, cryopodPos.Item1, cryopodPos.Item2, cryopodPos.Item3);
                    if (coords != null)
                    {
                        minimapCoords = new KeyValuePair<double, double>(coords.Item1, coords.Item2);
                        subMapName = coords.Item3;
                    }
                }
                else
                {
                    minimapCoords = d.GetASIMinimapCoords();
                    subMapName = d.GetSubMapName();
                }
                return new MapPoint()
                {
                    ID = dinoIDStr,
                    Name = d.CleanName,
                    Description = $"{(dinoIDStr != "00" ? $"{ASILang.Get("ID")}: {dinoIDStr}\n" : string.Empty)}{(!string.IsNullOrWhiteSpace(d.TamedName) ? $"{ASILang.Get("Name")}: {d.TamedName}\n" : string.Empty)}{ASILang.Get("Level")}: {(d.CurrentLevel != null && d.CurrentLevel.HasValue ? d.CurrentLevel.Value.ToString(CultureInfo.InvariantCulture) : "0")}",
                    X = minimapCoords.Value,
                    Y = minimapCoords.Key,
                    SubMapName = subMapName
                };
            });
            var dinosFiltered = dinosPoints.Where(d => string.Compare(d.SubMapName, SettingsPage._currentlyLoadedSubMapName, StringComparison.InvariantCultureIgnoreCase) == 0);
            MainWindow.UpdateMinimap(dinosFiltered, OnMinimapDoubleTap);
        }

        protected override bool OnMinimapDoubleTap(MapPoint? point)
        {
            if (point == null || _lastDisplayedVM == null || string.IsNullOrWhiteSpace(point.ID) || point.ID == "00")
                return false;

            Dino? dino = _lastDisplayedVM.FirstOrDefault(d => (string.Compare(point.ID, d?.GetDinoID(), StringComparison.InvariantCulture) == 0), null);
            if (dino != null)
            {
                dg_Data.SelectedItem = dino;
                dg_Data.UpdateLayout();
                dg_Data.ScrollIntoView(dg_Data.SelectedItem, null);
                return true;
            }
            return false;
        }

        protected override void OnDataGridPointerReleased(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Visibility goToInventoryItemsVisibility = Visibility.Collapsed;
            Visibility ueCoords = Visibility.Visible;
            Visibility cryopodEntries = Visibility.Collapsed;
            DataGridRow? row = Utils.FindParent<DataGridRow>((UIElement)e.OriginalSource);
            if (row != null)
            {
                Dino? d = row.DataContext as Dino;
                if (d != null && d.bIsCryopodded != null && d.bIsCryopodded.HasValue && d.bIsCryopodded.Value)
                {
                    ueCoords = Visibility.Collapsed;
                    cryopodEntries = Visibility.Visible;
                }
                if (d != null && !string.IsNullOrEmpty(d.InventoryUUID))
                    goToInventoryItemsVisibility = Visibility.Visible;
            }
            mfi_contextMenuGoToInventoryItems.Visibility = goToInventoryItemsVisibility;
            mfi_contextMenuGetCoords.Visibility = ueCoords;
            mfi_contextMenuGoToCryopod.Visibility = cryopodEntries;
            mfi_contextMenuGetCryopodCoords.Visibility = cryopodEntries;
            mfi_contextMenuGetCryopodMapCoords.Visibility = cryopodEntries;
        }

        #endregion

        #region Statics

        // Map name, save file datetime and in-game datetime.
        public static string MapName => (SettingsPage._currentlyLoadedMapName ?? ASILang.Get("Unknown"));
        public static string SaveGameDatetime => (Utils.GetSaveFileDateTimeStr() ?? ASILang.Get("UnknownDate"));
        public static string InGameDatetime => Utils.GetInGameDateTimeStr();

        #endregion

        #region Properties

        private ObservableCollection<string> _quickFilter_allTribes = new ObservableCollection<string>() { ASILang.Get("ClickHere") };
        private ObservableCollection<string> _quickFilter_allCleanNames = new ObservableCollection<string>() { ASILang.Get("ClickHere") };

        private string _quickFilter_Tribe_Label = ASILang.Get("Initializing");
        public string QuickFilter_Tribe_Label
        {
            get { return _quickFilter_Tribe_Label; }
            set { _quickFilter_Tribe_Label = value; OnPropertyChanged(); }
        }

        private string _quickFilter_Dino_Label = ASILang.Get("Initializing");
        public string QuickFilter_Dino_Label
        {
            get { return _quickFilter_Dino_Label; }
            set { _quickFilter_Dino_Label = value; OnPropertyChanged(); }
        }

        #endregion

        #region UI

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

        public static void ClearPageFilters()
        {
            if (_page != null)
                _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    _page.QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
                    _page.QuickFilter_Dino_Label = ASILang.Get("ClickHere");
                    _page._quickFilter_SelectedTribe = null;
                    _page._quickFilter_SelectedDino = null;
                });
            SearchBuilder._query = new SearchQuery();
        }

        public bool GoToDino(int? id1, int? id2)
        {
            if (id1 != null && id1.HasValue && id2 != null && id2.HasValue && _lastDisplayedVM != null)
            {
                Dino? dino = _lastDisplayedVM.FirstOrDefault(d => (d?.DinoID1 == id1 && d?.DinoID2 == id2), null);
                if (dino == null) // Dino not found. That's probably due to current filters.
                {
                    ClearPageFilters(); // Remove current filtering.
                    ApplyFiltersAndSort(); // Refresh.
                    dino = _lastDisplayedVM.FirstOrDefault(d => (d?.DinoID1 == id1 && d?.DinoID2 == id2), null);
                }
                if (dino != null)
                {
                    dg_Data.SelectedItem = dino;
                    dg_Data.UpdateLayout();
                    dg_Data.ScrollIntoView(dg_Data.SelectedItem, null);
                    return true;
                }
            }
            return false;
        }

        private void dg_Data_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (MainWindow._minimap == null)
                return;

            DataGridRow? row = Utils.FindParent<DataGridRow>((UIElement)e.OriginalSource);
            if (row != null)
            {
                Dino? d = row.DataContext as Dino;
                if (d != null)
                {
                    string dinoIDStr = d.GetDinoID();
                    KeyValuePair<double, double> minimapCoords = new KeyValuePair<double, double>(0.0d, 0.0d);
                    string? subMapName = null;
                    if (d.bIsCryopodded != null && d.bIsCryopodded.HasValue && d.bIsCryopodded.Value)
                    {
                        var cryopodPos = Utils.GetCryopodCoordsByUUID(d.CryopodUUID);
                        var coords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, cryopodPos.Item1, cryopodPos.Item2, cryopodPos.Item3);
                        if (coords != null)
                        {
                            minimapCoords = new KeyValuePair<double, double>(coords.Item1, coords.Item2);
                            subMapName = coords.Item3;
                        }
                    }
                    else
                    {
                        minimapCoords = d.GetASIMinimapCoords();
                        subMapName = d.GetSubMapName();
                    }
                    double x = minimapCoords.Value;
                    double y = minimapCoords.Key;
                    Minimap.ShowCallout(dinoIDStr, x, y, subMapName);
                }
            }
        }

        #endregion

        #region DataGrid context menu

        private void mfi_contextMenuGetCoords_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null && dino.Location != null)
                        clipboardStr = dino.Location;
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetCoords_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
                Utils.AddToClipboard(clipboardStr, false);
            }
        }

        private void mfi_contextMenuGetCryopodCoords_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null && dino.Cryopod != null && !string.IsNullOrEmpty(dino.Cryopod.Location))
                        clipboardStr = dino.Cryopod.Location;
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetCryopodCoords_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
                Utils.AddToClipboard(clipboardStr, false);
            }
        }

        private void mfi_contextMenuGetCryopodMapCoords_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null && dino.Cryopod != null && !string.IsNullOrEmpty(dino.Cryopod.MapCoords))
                        clipboardStr = dino.Cryopod.MapCoords;
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetCryopodMapCoords_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
                Utils.AddToClipboard(clipboardStr, false);
            }
        }

        private void mfi_contextMenuGoToCryopod_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null)
                    {
                        if (dino.Cryopod != null && dino.Cryopod.ItemID != null)
                        {
#pragma warning disable CS1998
                            if (MainWindow._mainWindow != null)
                            {
                                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                {
                                    if (MainWindow._mainWindow != null)
                                    {
                                        if (MainWindow._mainWindow._navView != null)
                                            MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnItems;
                                        MainWindow._mainWindow.NavView_Navigate(typeof(ItemsPage), new EntranceNavigationTransitionInfo());
                                    }
                                    await Task.Delay(250);
                                    if (ItemsPage._page != null)
                                        ItemsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                        {
                                            if (!ItemsPage._page.GoToItem(dino.Cryopod.ItemID.ItemID1, dino.Cryopod.ItemID.ItemID2))
                                                MainWindow.ShowToast($"{ASILang.Get("CryopodNotFound")} {ASILang.Get("CheckFilters")}", BackgroundColor.WARNING);
                                        });
                                });
                            }
#pragma warning restore CS1998
                        }
                        else
                            MainWindow.ShowToast(ASILang.Get("CryopodNotFound"), BackgroundColor.WARNING);
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGoToCryopod_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void mfi_contextMenuGoToInventoryItems_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null && !string.IsNullOrEmpty(dino.InventoryUUID))
                    {
#pragma warning disable CS1998
                        if (MainWindow._mainWindow != null)
                        {
                            MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                            {
                                if (MainWindow._mainWindow != null)
                                {
                                    if (MainWindow._mainWindow._navView != null)
                                        MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnItems;
                                    MainWindow._mainWindow.NavView_Navigate(typeof(ItemsPage), new EntranceNavigationTransitionInfo());
                                }
                                await Task.Delay(250);
                                if (ItemsPage._page != null)
                                    ItemsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                    {
                                        if (!ItemsPage._page.FilterByInventoryUUID(dino.InventoryUUID))
                                            MainWindow.ShowToast(ASILang.Get("FilteringByInventoryIDFailed"), BackgroundColor.WARNING);
                                    });
                            });
                        }
#pragma warning restore CS1998
                    }
                    else
                        MainWindow.ShowToast(ASILang.Get("InventoryIDNotFound"), BackgroundColor.WARNING);
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGoToInventoryItems_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        #endregion

        #region Quick filters

        public void InitTribesQuickFilter()
        {
            if (SettingsPage._allTribesForDinosInitialized)
            {
                _quickFilter_allTribes.Clear();
                _quickFilter_allTribes.Add(ASILang.Get("All").ToUpper());
                if (SettingsPage._allTribesForDinosSorted != null && SettingsPage._allTribesForDinosSorted.Count > 0)
                    foreach (string tribeName in SettingsPage._allTribesForDinosSorted)
                        if (tribeName != null)
                            _quickFilter_allTribes.Add(tribeName);
                QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
            }
            else
                QuickFilter_Tribe_Label = ASILang.Get("Initializing");
        }

        public void InitDinosQuickFilter()
        {
            if (SettingsPage._allCleanNamesForDinosInitialized)
            {
                _quickFilter_allCleanNames.Clear();
                _quickFilter_allCleanNames.Add(ASILang.Get("All").ToUpper());
                if (SettingsPage._allCleanNamesForDinosSorted != null && SettingsPage._allCleanNamesForDinosSorted.Count > 0)
                    foreach (string? cleanName in SettingsPage._allCleanNamesForDinosSorted)
                        if (cleanName != null)
                            _quickFilter_allCleanNames.Add(cleanName);
                QuickFilter_Dino_Label = ASILang.Get("ClickHere");
            }
            else
                QuickFilter_Dino_Label = ASILang.Get("Initializing");
        }

        private static PropertyInfo? _owningTribeIdProp = Utils.GetProperty(typeof(Dino), nameof(Dino.OwningTribeID));

        private string? _quickFilter_SelectedTribe = null;
        private string? _quickFilter_SelectedDino = null;

        private void SetupQuickFilter()
        {
            SearchBuilder._query = new SearchQuery() { Parts = new List<SearchQueryPart>() };

            if (_quickFilter_SelectedTribe != null && string.Compare(_quickFilter_SelectedTribe, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) != 0 && SettingsPage._allTribesForDinos.ContainsKey(_quickFilter_SelectedTribe))
            {
                SearchBuilder._query.Parts.Add(new SearchQueryPart()
                {
                    Type = SearchType.DINOS,
                    LogicalOperator = LogicalOperator.AND,
                    Operator = SearchOperator.MATCHING,
                    PropertyName = "OwningTribeID",
                    PropertyCleanName = DinoUtils.GetCleanNameFromPropertyName("OwningTribeID"),
                    Value = SettingsPage._allTribesForDinos[_quickFilter_SelectedTribe].ToString(CultureInfo.InvariantCulture)
                });
                QuickFilter_Tribe_Label = _quickFilter_SelectedTribe;
            }
            if (_quickFilter_SelectedDino != null && string.Compare(_quickFilter_SelectedDino, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) != 0 && SettingsPage._allCleanNamesForDinosSorted.Contains(_quickFilter_SelectedDino))
            {
                SearchBuilder._query.Parts.Add(new SearchQueryPart()
                {
                    Type = SearchType.DINOS,
                    LogicalOperator = LogicalOperator.AND,
                    Operator = SearchOperator.MATCHING,
                    PropertyName = "CleanName",
                    PropertyCleanName = DinoUtils.GetCleanNameFromPropertyName("CleanName"),
                    Value = _quickFilter_SelectedDino
                });
                QuickFilter_Dino_Label = _quickFilter_SelectedDino;
            }
        }

        private void QuickFilterTribeSelect(string tribeName)
        {
            if (_owningTribeIdProp == null)
                return;

            _quickFilter_SelectedTribe = string.Compare(tribeName, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) == 0 ? null : tribeName;
            SetupQuickFilter();
            ApplyFiltersAndSort();
        }

        private void cbb_QuickFilter_Tribe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string? selected = e.AddedItems[0] as string;
                if (selected != null)
                    QuickFilterTribeSelect(selected);
            }
        }

        private static PropertyInfo? _cleanNameProp = Utils.GetProperty(typeof(Dino), nameof(Dino.CleanName));

        private void QuickFilterDinoSelect(string cleanName)
        {
            if (_cleanNameProp == null)
                return;

            _quickFilter_SelectedDino = string.Compare(cleanName, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) == 0 ? null : cleanName;
            SetupQuickFilter();
            ApplyFiltersAndSort();
        }

        private void cbb_QuickFilter_Dino_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string? selected = e.AddedItems[0] as string;
                if (selected != null)
                    QuickFilterDinoSelect(selected);
            }
        }

        #endregion
    }
}
