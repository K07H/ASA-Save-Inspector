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
    public abstract class StructuresPageBase : DataPageBase<Structure> { }

    public sealed partial class StructuresPage : StructuresPageBase
    {
        #region Page specific members (see DataPageBase)

        public static StructuresPage? _page => (_currentPage as StructuresPage);

        public static bool ApplyCurrentQuery() => ApplyCurrentQueryCore("Structures");

        public StructuresPage()
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
            InitStructuresQuickFilter();
        }

        protected override SearchType PageSearchType => SearchType.STRUCTURES;
        protected override List<Structure>? AllData => SettingsPage._structuresData;
        protected override string PageLabel => "Structures";
        protected override IReadOnlyList<string> DefaultSelectedColumns => StructureUtils.DefaultSelectedColumns;
        protected override IReadOnlyList<string> DefaultColumnsOrder => StructureUtils.DefaultColumnsOrder;
        protected override string? GetCleanNameFromPropertyName(string? propertyName) => StructureUtils.GetCleanNameFromPropertyName(propertyName);
        protected override string? GetPropertyNameFromCleanName(string? cleanName) => StructureUtils.GetPropertyNameFromCleanName(cleanName);
        protected override string ColumnsOrderFilePath => Utils.StructureColumnsOrderFilePath();
        protected override string ColumnsPresetsFilePath => Utils.StructureColumnsPresetsFilePath();

        protected override string? DefaultColumnsPresetName
        {
            get => SettingsPage._defaultColumnsPreset_Structures;
            set => SettingsPage._defaultColumnsPreset_Structures = value;
        }

        protected override string? GetObjectID(Structure structure) => (structure.StructureID != null && structure.StructureID.HasValue ? $"{structure.StructureID.Value.ToString(CultureInfo.InvariantCulture)}" : null);

        protected override void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        protected override void RefreshMinimap(IEnumerable<Structure> structures)
        {
            var structuresPoints = structures.Select(d =>
            {
                string structureIDStr = (d.StructureID != null && d.StructureID.HasValue ? d.StructureID.Value.ToString(CultureInfo.InvariantCulture) : "0");
                var minimapCoords = d.GetASIMinimapCoords();
                string? subMapName = d.GetSubMapName();
                return new MapPoint()
                {
                    ID = structureIDStr,
                    Name = d.ShortName,
                    Description = $"{(structureIDStr != "0" ? $"{ASILang.Get("ID")}: {structureIDStr}\n" : string.Empty)}{(d.TargetingTeam != null && d.TargetingTeam.HasValue ? $"{ASILang.Get("TribeID")}: {d.TargetingTeam.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty)}",
                    X = minimapCoords.Value,
                    Y = minimapCoords.Key,
                    SubMapName = subMapName
                };
            });
            var structuresFiltered = structuresPoints.Where(d => string.Compare(d.SubMapName, SettingsPage._currentlyLoadedSubMapName, StringComparison.InvariantCultureIgnoreCase) == 0);
            MainWindow.UpdateMinimap(structuresFiltered, OnMinimapDoubleTap);
        }

        protected override bool OnMinimapDoubleTap(MapPoint? point)
        {
            if (point == null || _lastDisplayedVM == null || string.IsNullOrWhiteSpace(point.ID) || point.ID == "00")
                return false;

            Structure? structure = _lastDisplayedVM.FirstOrDefault(d => (string.Compare(point.ID, d?.StructureID?.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture) == 0), null);
            if (structure != null)
            {
                dg_Data.SelectedItem = structure;
                dg_Data.UpdateLayout();
                dg_Data.ScrollIntoView(dg_Data.SelectedItem, null);
                return true;
            }
            return false;
        }

        protected override void OnDataGridPointerReleased(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Visibility goToInventoryItemsVisibility = Visibility.Collapsed;
            DataGridRow? row = Utils.FindParent<DataGridRow>((UIElement)e.OriginalSource);
            if (row != null)
            {
                Structure? s = row.DataContext as Structure;
                if (s != null && !string.IsNullOrEmpty(s.InventoryUUID))
                    goToInventoryItemsVisibility = Visibility.Visible;
            }
            mfi_contextMenuGoToInventoryItems.Visibility = goToInventoryItemsVisibility;
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

        private string _quickFilter_Structure_Label = ASILang.Get("Initializing");
        public string QuickFilter_Structure_Label
        {
            get { return _quickFilter_Structure_Label; }
            set { _quickFilter_Structure_Label = value; OnPropertyChanged(); }
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
                    _page.QuickFilter_Structure_Label = ASILang.Get("ClickHere");
                    _page._quickFilter_SelectedTribe = null;
                    _page._quickFilter_SelectedStructure = null;
                });
            SearchBuilder._query = new SearchQuery();
        }

        public bool GoToStructure(int? structureID)
        {
            if (structureID != null && structureID.HasValue && _lastDisplayedVM != null)
            {
                Structure? structure = _lastDisplayedVM.FirstOrDefault(d => (d?.StructureID == structureID), null);
                if (structure == null) // Structure not found. That's probably due to current filters.
                {
                    ClearPageFilters(); // Remove current filtering.
                    ApplyFiltersAndSort(); // Refresh.
                    structure = _lastDisplayedVM.FirstOrDefault(d => (d?.StructureID == structureID), null);
                }
                if (structure != null)
                {
                    dg_Data.SelectedItem = structure;
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
                Structure? s = row.DataContext as Structure;
                if (s != null)
                {
                    string structureIDStr = (s.StructureID != null && s.StructureID.HasValue ? s.StructureID.Value.ToString(CultureInfo.InvariantCulture) : "0");
                    var minimapCoords = s.GetASIMinimapCoords();
                    double x = minimapCoords.Value;
                    double y = minimapCoords.Key;
                    string? subMapName = s.GetSubMapName();
                    Minimap.ShowCallout(structureIDStr, x, y, subMapName);
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
                    Structure? structure = (mfi.DataContext as Structure);
                    if (structure != null && structure.Location != null)
                        clipboardStr = structure.Location;
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

        private void mfi_contextMenuGoToInventoryItems_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Structure? structure = (mfi.DataContext as Structure);
                    if (structure != null && !string.IsNullOrEmpty(structure.InventoryUUID))
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
                                        if (!ItemsPage._page.FilterByInventoryUUID(structure.InventoryUUID))
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
            if (SettingsPage._allTribesForStructuresInitialized)
            {
                _quickFilter_allTribes.Clear();
                _quickFilter_allTribes.Add(ASILang.Get("All").ToUpper());
                if (SettingsPage._allTribesForStructuresSorted != null && SettingsPage._allTribesForStructuresSorted.Count > 0)
                    foreach (string tribeName in SettingsPage._allTribesForStructuresSorted)
                        if (tribeName != null)
                            _quickFilter_allTribes.Add(tribeName);
                QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
            }
            else
                QuickFilter_Tribe_Label = ASILang.Get("Initializing");
        }

        public void InitStructuresQuickFilter()
        {
            if (SettingsPage._allShortNamesForStructuresInitialized)
            {
                _quickFilter_allShortNames.Clear();
                _quickFilter_allShortNames.Add(ASILang.Get("All").ToUpper());
                if (SettingsPage._allShortNamesForStructuresSorted != null && SettingsPage._allShortNamesForStructuresSorted.Count > 0)
                    foreach (string? shortName in SettingsPage._allShortNamesForStructuresSorted)
                        if (shortName != null)
                            _quickFilter_allShortNames.Add(shortName);
                QuickFilter_Structure_Label = ASILang.Get("ClickHere");
            }
            else
                QuickFilter_Structure_Label = ASILang.Get("Initializing");
        }

        private static PropertyInfo? _targetingTeamProp = Utils.GetProperty(typeof(Structure), nameof(Structure.TargetingTeam));

        private string? _quickFilter_SelectedTribe = null;
        private string? _quickFilter_SelectedStructure = null;

        private void SetupQuickFilter()
        {
            SearchBuilder._query = new SearchQuery() { Parts = new List<SearchQueryPart>() };

            if (_quickFilter_SelectedTribe != null && string.Compare(_quickFilter_SelectedTribe, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) != 0 && SettingsPage._allTribesForStructures.ContainsKey(_quickFilter_SelectedTribe))
            {
                SearchBuilder._query.Parts.Add(new SearchQueryPart()
                {
                    Type = SearchType.STRUCTURES,
                    LogicalOperator = LogicalOperator.AND,
                    Operator = SearchOperator.MATCHING,
                    PropertyName = "TargetingTeam",
                    PropertyCleanName = StructureUtils.GetCleanNameFromPropertyName("TargetingTeam"),
                    Value = SettingsPage._allTribesForStructures[_quickFilter_SelectedTribe].ToString(CultureInfo.InvariantCulture)
                });
                QuickFilter_Tribe_Label = _quickFilter_SelectedTribe;
            }
            if (_quickFilter_SelectedStructure != null && string.Compare(_quickFilter_SelectedStructure, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) != 0 && SettingsPage._allShortNamesForStructuresSorted.Contains(_quickFilter_SelectedStructure))
            {
                SearchBuilder._query.Parts.Add(new SearchQueryPart()
                {
                    Type = SearchType.STRUCTURES,
                    LogicalOperator = LogicalOperator.AND,
                    Operator = SearchOperator.MATCHING,
                    PropertyName = "ShortName",
                    PropertyCleanName = StructureUtils.GetCleanNameFromPropertyName("ShortName"),
                    Value = _quickFilter_SelectedStructure
                });
                QuickFilter_Structure_Label = _quickFilter_SelectedStructure;
            }
        }

        private void QuickFilterTribeSelect(string tribeName)
        {
            if (_targetingTeamProp == null)
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

        private static PropertyInfo? _shortNameProp = Utils.GetProperty(typeof(Structure), nameof(Structure.ShortName));

        private void QuickFilterStructureSelect(string shortName)
        {
            if (_shortNameProp == null)
                return;

            _quickFilter_SelectedStructure = string.Compare(shortName, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) == 0 ? null : shortName;
            SetupQuickFilter();
            ApplyFiltersAndSort();
        }

        private void cbb_QuickFilter_Structure_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string? selected = e.AddedItems[0] as string;
                if (selected != null)
                    QuickFilterStructureSelect(selected);
            }
        }

        #endregion
    }
}
