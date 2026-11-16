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
    public abstract class ItemsPageBase : DataPageBase<Item> { }

    public sealed partial class ItemsPage : ItemsPageBase
    {
        #region Page specific members (see DataPageBase)

        public static ItemsPage? _page => (_currentPage as ItemsPage);

        public static bool ApplyCurrentQuery() => ApplyCurrentQueryCore("Items");

        public ItemsPage()
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
            InitItemsQuickFilter();
        }

        protected override SearchType PageSearchType => SearchType.ITEMS;
        protected override List<Item>? AllData => SettingsPage._itemsData;
        protected override string PageLabel => "Items";
        protected override IReadOnlyList<string> DefaultSelectedColumns => ItemUtils.DefaultSelectedColumns;
        protected override IReadOnlyList<string> DefaultColumnsOrder => ItemUtils.DefaultColumnsOrder;
        protected override string? GetCleanNameFromPropertyName(string? propertyName) => ItemUtils.GetCleanNameFromPropertyName(propertyName);
        protected override string? GetPropertyNameFromCleanName(string? cleanName) => ItemUtils.GetPropertyNameFromCleanName(cleanName);
        protected override string ColumnsOrderFilePath => Utils.ItemColumnsOrderFilePath();
        protected override string ColumnsPresetsFilePath => Utils.ItemColumnsPresetsFilePath();

        protected override string? DefaultColumnsPresetName
        {
            get => SettingsPage._defaultColumnsPreset_Items;
            set => SettingsPage._defaultColumnsPreset_Items = value;
        }

        protected override string? GetObjectID(Item item) => (item.ItemID != null && item.ItemID.ItemID1 != null && item.ItemID.ItemID1.HasValue && item.ItemID.ItemID2 != null && item.ItemID.ItemID2.HasValue ? $"{item.ItemID.ItemID1.Value.ToString(CultureInfo.InvariantCulture)}{item.ItemID.ItemID2.Value.ToString(CultureInfo.InvariantCulture)}" : null);

        protected override void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        protected override void RefreshMinimap(IEnumerable<Item> items)
        {
            var itemsPoints = items.Select(d =>
            {
                if (d != null && d.UECoords != null && d.UECoords.Item1 != null && d.UECoords.Item1.HasValue && d.UECoords.Item2 != null && d.UECoords.Item2.HasValue && d.UECoords.Item3 != null && d.UECoords.Item3.HasValue)
                {
                    string? itemIDStr = d.GetItemID();
                    var minimapCoords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, d.UECoords.Item1.Value, d.UECoords.Item2.Value, d.UECoords.Item3.Value);
                    return new MapPoint()
                    {
                        ID = itemIDStr,
                        Name = d.ShortName,
                        Description = $"{(itemIDStr != "00" ? $"{ASILang.Get("ID")}: {itemIDStr}\n" : string.Empty)}{(!string.IsNullOrWhiteSpace(d.ShortName) ? $"{ASILang.Get("Name")}: {d.ShortName}\n" : string.Empty)}",
                        X = minimapCoords.Item2,
                        Y = minimapCoords.Item1,
                        SubMapName = minimapCoords.Item3
                    };
                }
                return null;
            });
            var itemsFiltered = itemsPoints.Where(d => d != null && string.Compare(d.SubMapName, SettingsPage._currentlyLoadedSubMapName, StringComparison.InvariantCultureIgnoreCase) == 0);
            MainWindow.UpdateMinimap(itemsFiltered, OnMinimapDoubleTap);
        }

        protected override bool OnMinimapDoubleTap(MapPoint? point)
        {
            if (point == null || _lastDisplayedVM == null || string.IsNullOrWhiteSpace(point.ID) || point.ID == "00")
                return false;

            Item? item = _lastDisplayedVM.FirstOrDefault(d => (string.Compare(point.ID, d?.GetItemID(), StringComparison.InvariantCulture) == 0), null);
            if (item != null)
            {
                dg_Data.SelectedItem = item;
                dg_Data.UpdateLayout();
                dg_Data.ScrollIntoView(dg_Data.SelectedItem, null);
                return true;
            }
            return false;
        }

        protected override void OnDataGridPointerReleased(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            mfi_contextMenuGetCoords.Visibility = Visibility.Visible;
            Visibility goToContainerVisibility = Visibility.Collapsed;

            DataGridRow? row = Utils.FindParent<DataGridRow>((UIElement)e.OriginalSource);
            if (row != null)
            {
                Item? d = row.DataContext as Item;
                if (d != null)
                {
                    var owner = d.Owner();
                    if (owner == null || !owner.HasValue || owner.Value.Value == null)
                        if (!string.IsNullOrEmpty(d.OwnerCryopodUUID))
                        {
                            Item? cryopod = d.OwnerCryopod();
                            if (cryopod != null)
                                owner = cryopod.Owner();
                        }
                    if (owner != null && owner.HasValue && owner.Value.Key != ArkObjectType.UNKNOWN && owner.Value.Value != null)
                        goToContainerVisibility = Visibility.Visible;
                }
            }
            mfi_contextMenuGoToContainer.Visibility = goToContainerVisibility;
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
        private ObservableCollection<string> _quickFilter_allShortNames = new ObservableCollection<string>() { ASILang.Get("ClickHere") };

        private string _quickFilter_Tribe_Label = ASILang.Get("Initializing");
        public string QuickFilter_Tribe_Label
        {
            get { return _quickFilter_Tribe_Label; }
            set { _quickFilter_Tribe_Label = value; OnPropertyChanged(); }
        }

        private string _quickFilter_Item_Label = ASILang.Get("Initializing");
        public string QuickFilter_Item_Label
        {
            get { return _quickFilter_Item_Label; }
            set { _quickFilter_Item_Label = value; OnPropertyChanged(); }
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
                    _page.QuickFilter_Item_Label = ASILang.Get("ClickHere");
                    _page._quickFilter_SelectedTribe = null;
                    _page._quickFilter_SelectedItem = null;
                });
            SearchBuilder._query = new SearchQuery();
        }

        public bool GoToItem(int? id1, int? id2)
        {
            if (id1 != null && id1.HasValue && id2 != null && id2.HasValue && _lastDisplayedVM != null)
            {
                Item? item = _lastDisplayedVM.FirstOrDefault(d => (d?.ItemID?.ItemID1 == id1 && d?.ItemID.ItemID2 == id2), null);
                if (item == null) // Item not found. That's probably due to current filters.
                {
                    ClearPageFilters(); // Remove current filtering.
                    ApplyFiltersAndSort(); // Refresh.
                    item = _lastDisplayedVM.FirstOrDefault(d => (d?.ItemID?.ItemID1 == id1 && d?.ItemID.ItemID2 == id2), null);
                }
                if (item != null)
                {
                    dg_Data.SelectedItem = item;
                    dg_Data.UpdateLayout();
                    dg_Data.ScrollIntoView(dg_Data.SelectedItem, null);
                    return true;
                }
            }
            return false;
        }

        public bool FilterByInventoryUUID(string? inventoryUUID)
        {
            if (string.IsNullOrEmpty(inventoryUUID))
                return false;

            PropertyInfo? prop = Utils.GetProperty(typeof(Item), "OwnerInventoryUUID");
            if (prop != null)
            {
                SearchBuilder._query = new SearchQuery()
                {
                    Parts = new List<SearchQueryPart>() {
                    new SearchQueryPart()
                    {
                        Type = SearchType.ITEMS,
                        LogicalOperator = LogicalOperator.AND,
                        Operator = SearchOperator.MATCHING,
                        PropertyName = "OwnerInventoryUUID",
                        PropertyCleanName = ItemUtils.GetCleanNameFromPropertyName("OwnerInventoryUUID"),
                        Value = inventoryUUID
                    }
                }
                };
                ApplyFiltersAndSort();
            }
            return true;
        }

        private void dg_Data_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (MainWindow._minimap == null)
                return;

            DataGridRow? row = Utils.FindParent<DataGridRow>((UIElement)e.OriginalSource);
            if (row != null)
            {
                Item? i = row.DataContext as Item;
                if (i != null && i.UECoords?.Item1 != null && i.UECoords?.Item2 != null && i.UECoords?.Item3 != null && i.UECoords.Item1.HasValue && i.UECoords.Item2.HasValue && i.UECoords.Item3.HasValue)
                {
                    string? itemIDStr = i.GetItemID();
                    var minimapCoords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, i.UECoords.Item1.Value, i.UECoords.Item2.Value, i.UECoords.Item3.Value);
                    double x = minimapCoords.Item2;
                    double y = minimapCoords.Item1;
                    string? subMapName = minimapCoords.Item3;
                    Minimap.ShowCallout(itemIDStr, x, y, subMapName);
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
                    Item? item = (mfi.DataContext as Item);
                    if (item != null && item.Location != null)
                        clipboardStr = item.Location;
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

        private void mfi_contextMenuGoToContainer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Item? item = (mfi.DataContext as Item);
                    if (item != null)
                    {
                        var owner = item.Owner();
                        // Handle case of items stored inside cryopod.
                        if (owner == null || !owner.HasValue || owner.Value.Value == null)
                            if (!string.IsNullOrEmpty(item.OwnerCryopodUUID))
                            {
                                Item? cryopod = item.OwnerCryopod();
                                if (cryopod != null && cryopod.ItemID != null)
                                {
                                    if (GoToItem(cryopod.ItemID.ItemID1, cryopod.ItemID.ItemID2))
                                        return;
                                    else
                                    {
                                        MainWindow.ShowToast($"{ASILang.Get("CryopodNotFound")} {ASILang.Get("CheckFilters")}", BackgroundColor.WARNING);
                                        // If cryopod item not found try to fallback redirect to the container of the cryopod.
                                        owner = cryopod.Owner();
                                    }
                                }
                            }
                        // Go to container.
                        if (owner != null && owner.HasValue && owner.Value.Value != null &&
                            (owner.Value.Key == ArkObjectType.PLAYER_PAWN || owner.Value.Key == ArkObjectType.DINO || owner.Value.Key == ArkObjectType.STRUCTURE))
                        {
#pragma warning disable CS1998
                            if (MainWindow._mainWindow != null)
                            {
                                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                {
                                    if (MainWindow._mainWindow != null && MainWindow._mainWindow._navView != null)
                                    {
                                        if (owner.Value.Key == ArkObjectType.PLAYER_PAWN)
                                        {
                                            MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnPawns;
                                            MainWindow._mainWindow.NavView_Navigate(typeof(PlayerPawnsPage), new EntranceNavigationTransitionInfo());
                                            PlayerPawn? pp = owner.Value.Value as PlayerPawn;
                                            await Task.Delay(250);
                                            if (PlayerPawnsPage._page != null && pp != null && pp.LinkedPlayerDataID != null && pp.LinkedPlayerDataID.HasValue)
                                            {
                                                PlayerPawnsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                                {
                                                    if (!PlayerPawnsPage._page.GoToPlayerPawn(pp.LinkedPlayerDataID))
                                                        MainWindow.ShowToast($"{ASILang.Get("PlayerPawnNotFound")} {ASILang.Get("CheckFilters")}", BackgroundColor.WARNING);
                                                });
                                            }
                                            else
                                                MainWindow.ShowToast(ASILang.Get("NoValidID_PlayerPawn"), BackgroundColor.WARNING);
                                        }
                                        else if (owner.Value.Key == ArkObjectType.DINO)
                                        {
                                            MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnDinos;
                                            MainWindow._mainWindow.NavView_Navigate(typeof(DinosPage), new EntranceNavigationTransitionInfo());
                                            Dino? d = owner.Value.Value as Dino;
                                            await Task.Delay(250);
                                            if (DinosPage._page != null && d != null && d.DinoID1 != null && d.DinoID1.HasValue && d.DinoID2 != null && d.DinoID2.HasValue)
                                            {
                                                DinosPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                                {
                                                    if (!DinosPage._page.GoToDino(d.DinoID1, d.DinoID2))
                                                        MainWindow.ShowToast($"{ASILang.Get("DinoNotFound")} {ASILang.Get("CheckFilters")}", BackgroundColor.WARNING);
                                                });
                                            }
                                            else
                                                MainWindow.ShowToast(ASILang.Get("NoValidID_Dino"), BackgroundColor.WARNING);
                                        }
                                        else if (owner.Value.Key == ArkObjectType.STRUCTURE)
                                        {
                                            MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnStructures;
                                            MainWindow._mainWindow.NavView_Navigate(typeof(StructuresPage), new EntranceNavigationTransitionInfo());
                                            Structure? s = owner.Value.Value as Structure;
                                            await Task.Delay(250);
                                            if (StructuresPage._page != null && s != null && s.StructureID != null && s.StructureID.HasValue)
                                            {
                                                StructuresPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                                {
                                                    if (!StructuresPage._page.GoToStructure(s.StructureID))
                                                        MainWindow.ShowToast($"{ASILang.Get("StructureNotFound")} {ASILang.Get("CheckFilters")}", BackgroundColor.WARNING);
                                                });
                                            }
                                            else
                                                MainWindow.ShowToast(ASILang.Get("NoValidID_Structure"), BackgroundColor.WARNING);
                                        }
                                        else
                                            MainWindow.ShowToast(ASILang.Get("UnknownContainerType"), BackgroundColor.WARNING);
                                    }
                                });
                            }
#pragma warning restore CS1998
                        }
                        else
                            MainWindow.ShowToast(ASILang.Get("OwningContainerNotFound"), BackgroundColor.WARNING);
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGoToContainer_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        #endregion

        #region Quick filters

        public void InitTribesQuickFilter()
        {
            if (SettingsPage._allTribesForItemsInitialized)
            {
                _quickFilter_allTribes.Clear();
                _quickFilter_allTribes.Add(ASILang.Get("All").ToUpper());
                if (SettingsPage._allTribesForItemsSorted != null && SettingsPage._allTribesForItemsSorted.Count > 0)
                    foreach (string tribeName in SettingsPage._allTribesForItemsSorted)
                        if (tribeName != null)
                            _quickFilter_allTribes.Add(tribeName);
                QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
            }
            else
                QuickFilter_Tribe_Label = ASILang.Get("Initializing");
        }

        public void InitItemsQuickFilter()
        {
            if (SettingsPage._allShortNamesForItemsInitialized)
            {
                _quickFilter_allShortNames.Clear();
                _quickFilter_allShortNames.Add(ASILang.Get("All").ToUpper());
                if (SettingsPage._allShortNamesForItemsSorted != null && SettingsPage._allShortNamesForItemsSorted.Count > 0)
                    foreach (string? shortName in SettingsPage._allShortNamesForItemsSorted)
                        if (shortName != null)
                            _quickFilter_allShortNames.Add(shortName);
                QuickFilter_Item_Label = ASILang.Get("ClickHere");
            }
            else
                QuickFilter_Item_Label = ASILang.Get("Initializing");
        }

        private static PropertyInfo? _containerTribeIdProp = Utils.GetProperty(typeof(Item), nameof(Item.ContainerTribeID));

        private string? _quickFilter_SelectedTribe = null;
        private string? _quickFilter_SelectedItem = null;

        private void SetupQuickFilter()
        {
            SearchBuilder._query = new SearchQuery() { Parts = new List<SearchQueryPart>() };

            if (_quickFilter_SelectedTribe != null && string.Compare(_quickFilter_SelectedTribe, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) != 0 && SettingsPage._allTribesForItems.ContainsKey(_quickFilter_SelectedTribe))
            {
                SearchBuilder._query.Parts.Add(new SearchQueryPart()
                {
                    Type = SearchType.ITEMS,
                    LogicalOperator = LogicalOperator.AND,
                    Operator = SearchOperator.MATCHING,
                    PropertyName = "ContainerTribeID",
                    PropertyCleanName = ItemUtils.GetCleanNameFromPropertyName("ContainerTribeID"),
                    Value = SettingsPage._allTribesForItems[_quickFilter_SelectedTribe].ToString(CultureInfo.InvariantCulture)
                });
                QuickFilter_Tribe_Label = _quickFilter_SelectedTribe;
            }
            if (_quickFilter_SelectedItem != null && string.Compare(_quickFilter_SelectedItem, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) != 0 && SettingsPage._allShortNamesForItemsSorted.Contains(_quickFilter_SelectedItem))
            {
                SearchBuilder._query.Parts.Add(new SearchQueryPart()
                {
                    Type = SearchType.ITEMS,
                    LogicalOperator = LogicalOperator.AND,
                    Operator = SearchOperator.MATCHING,
                    PropertyName = "ShortName",
                    PropertyCleanName = ItemUtils.GetCleanNameFromPropertyName("ShortName"),
                    Value = _quickFilter_SelectedItem
                });
                QuickFilter_Item_Label = _quickFilter_SelectedItem;
            }
        }

        private void QuickFilterTribeSelect(string tribeName)
        {
            if (_containerTribeIdProp == null)
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

        private static PropertyInfo? _shortNameProp = Utils.GetProperty(typeof(Item), nameof(Item.ShortName));

        private void QuickFilterItemSelect(string shortName)
        {
            if (_shortNameProp == null)
                return;

            _quickFilter_SelectedItem = string.Compare(shortName, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) == 0 ? null : shortName;
            SetupQuickFilter();
            ApplyFiltersAndSort();
        }

        private void cbb_QuickFilter_Item_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string? selected = e.AddedItems[0] as string;
                if (selected != null)
                    QuickFilterItemSelect(selected);
            }
        }

        #endregion
    }
}
