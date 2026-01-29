using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.ObjectModelUtils;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinUIEx;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class StructuresPage : Page, INotifyPropertyChanged
    {
        #region Constants

        private const double _scrollBarWidth = 24.0d;
        private const int MAX_PROPERTY_VALUES = 300;

        #endregion

        #region Statics

        private static readonly object lockObject = new object();

        public static StructuresPage? _page = null;

        private static string? _currentSort = null;
        private static bool _ascendingSort = true;
        private static string? _secondaryCurrentSort = null;
        private static bool _secondaryAscendingSort = true;

        //private static bool _addedDefaultFilters = false;
        private static List<KeyValuePair<PropertyInfo, Filter>> _filters = new List<KeyValuePair<PropertyInfo, Filter>>();

        private static bool _setDefaultSelectedColumns = false;
        private static List<string> _selectedColumns = new List<string>();

        private static List<KeyValuePair<FilterOperator, JsonFiltersPreset>> _group = new List<KeyValuePair<FilterOperator, JsonFiltersPreset>>();

        private static JsonColumnsPreset _defaultColumnsPreset = new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>() };

        private static JsonFiltersPreset _defaultFiltersPreset = new JsonFiltersPreset() { Name = ASILang.Get("DefaultPreset"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_OilVeins = new JsonFiltersPreset() { Name = ASILang.Get("OilVeins"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_WaterVeins = new JsonFiltersPreset() { Name = ASILang.Get("WaterVeins"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_GasVeins = new JsonFiltersPreset() { Name = ASILang.Get("GasVeins"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_PowerNodes = new JsonFiltersPreset() { Name = ASILang.Get("PowerNodes"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_BeaverDams = new JsonFiltersPreset() { Name = ASILang.Get("BeaverDams"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_ZPlants = new JsonFiltersPreset() { Name = ASILang.Get("ZPlants"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_WyvernNests = new JsonFiltersPreset() { Name = ASILang.Get("WyvernNests"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_GigantoraptorNests = new JsonFiltersPreset() { Name = ASILang.Get("GigantoraptorNests"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_ArtifactCrates = new JsonFiltersPreset() { Name = ASILang.Get("ArtifactCrates"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_HordeCrates = new JsonFiltersPreset() { Name = ASILang.Get("HordeCrates"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_TributeTerminals = new JsonFiltersPreset() { Name = ASILang.Get("TributeTerminals"), Filters = new List<JsonFilter>() };

        private static JsonFiltersPreset _defaultFiltersPreset_CityTerminals = new JsonFiltersPreset() { Name = ASILang.Get("CityTerminals"), Filters = new List<JsonFilter>() };

        // Map name, save file datetime and in-game datetime.
        public static string MapName => (SettingsPage._currentlyLoadedMapName ?? ASILang.Get("Unknown"));
        public static string SaveGameDatetime => (Utils.GetSaveFileDateTimeStr() ?? ASILang.Get("UnknownDate"));
        public static string InGameDatetime => Utils.GetInGameDateTimeStr();

        #endregion

        #region Properties

        public IEnumerable<Structure>? _lastDisplayed = null;
        public ObservableCollection<Structure>? _lastDisplayedVM = null;
        private string? _selectedStructureFilter_Name = null;
        private List<string>? _selectedStructureFilter_Values = new List<string>();

        private List<JsonFiltersPreset> _filtersPresets = new List<JsonFiltersPreset>();
        private JsonFiltersPreset? _selectedFiltersPreset = null;

        private List<JsonColumnsPreset> _columnsPresets = new List<JsonColumnsPreset>();
        private JsonColumnsPreset? _selectedColumnsPreset = null;

        private List<JsonGroupPreset> _groupPresets = new List<JsonGroupPreset>();
        private JsonGroupPreset? _selectedGroupPreset = null;

        private List<string> _propertiesWithManyValues = new List<string>(StructureUtils.DoNotCheckPropertyValuesAmount);

        private ObservableCollection<string> _quickFilter_allTribes = new ObservableCollection<string>() { ASILang.Get("ClickHere") };
        private ObservableCollection<string> _quickFilter_allShortNames = new ObservableCollection<string>() { ASILang.Get("ClickHere") };

        public event PropertyChangedEventHandler? PropertyChanged;

        private string? CurrentSort
        {
            get => _currentSort;
            set
            {
                _currentSort = value;
                if (_currentSort != null)
                {
                    RefreshPrimarySortLabel();
                    tb_PrimarySort.Visibility = Visibility.Visible;
                }
                else
                    tb_PrimarySort.Visibility = Visibility.Collapsed;
            }
        }

        private bool AscendingSort
        {
            get => _ascendingSort;
            set
            {
                _ascendingSort = value;
                RefreshPrimarySortLabel();
            }
        }

        private string? SecondaryCurrentSort
        {
            get => _secondaryCurrentSort;
            set
            {
                _secondaryCurrentSort = value;
                if (_secondaryCurrentSort != null)
                {
                    RefreshSecondarySortLabel();
                    tb_SecondarySort.Visibility = Visibility.Visible;
                }
                else
                    tb_SecondarySort.Visibility = Visibility.Collapsed;
            }
        }

        private bool SecondaryAscendingSort
        {
            get => _secondaryAscendingSort;
            set
            {
                _secondaryAscendingSort = value;
                RefreshSecondarySortLabel();
            }
        }

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

        #region Constructor/Destructor

        public StructuresPage()
        {
            InitializeComponent();

            // Ensure previous page is garbage collected then bind current page to _page variable (thread-safe).
            lock (lockObject)
            {
                DestroyPage();
                _page = this;
            }

            // Calculate page center.
            AdjustToSizeChange();

            // Close previously opened windows.
            if (SearchBuilder._searchBuilder != null)
                SearchBuilder._searchBuilder.Close();
            if (EditSearchQuery._editSearchQuery != null)
                EditSearchQuery._editSearchQuery.Close();

            // Init search.
            if (SettingsPage._legacySearch != null && SettingsPage._legacySearch.HasValue && SettingsPage._legacySearch.Value)
            {
                tb_Filters.Visibility = Visibility.Visible;
                btn_AddFilter.Visibility = Visibility.Visible;
                btn_EditFilters.Visibility = Visibility.Visible;
                btn_FiltersPresets.Visibility = Visibility.Visible;
                tb_FiltersGroup.Visibility = Visibility.Visible;
                btn_AddFiltersGroup.Visibility = Visibility.Visible;
                btn_EditFiltersGroup.Visibility = Visibility.Visible;
                btn_FiltersGroupPresets.Visibility = Visibility.Visible;

                tb_CreateFilter.Visibility = Visibility.Collapsed;
                btn_CreateFilter.Visibility = Visibility.Collapsed;
                tb_SavedQueries.Visibility = Visibility.Collapsed;
                cbb_ExistingQueries.Visibility = Visibility.Collapsed;
                btn_LoadQuery.Visibility = Visibility.Collapsed;
                btn_DeleteQuery.Visibility = Visibility.Collapsed;
            }
            else
            {
                tb_Filters.Visibility = Visibility.Collapsed;
                btn_AddFilter.Visibility = Visibility.Collapsed;
                btn_EditFilters.Visibility = Visibility.Collapsed;
                btn_FiltersPresets.Visibility = Visibility.Collapsed;
                tb_FiltersGroup.Visibility = Visibility.Collapsed;
                btn_AddFiltersGroup.Visibility = Visibility.Collapsed;
                btn_EditFiltersGroup.Visibility = Visibility.Collapsed;
                btn_FiltersGroupPresets.Visibility = Visibility.Collapsed;

                tb_CreateFilter.Visibility = Visibility.Visible;
                btn_CreateFilter.Visibility = Visibility.Visible;
                tb_SavedQueries.Visibility = Visibility.Visible;
                cbb_ExistingQueries.Visibility = Visibility.Visible;
                btn_LoadQuery.Visibility = Visibility.Visible;
                btn_DeleteQuery.Visibility = Visibility.Visible;
            }
            SearchBuilder.InitSearch(SearchType.STRUCTURES);

            // Init default presets.
            InitDefaultPresets();

            // Set default selected columns.
            if (!_setDefaultSelectedColumns)
            {
                if (_selectedColumns != null && StructureUtils.DefaultSelectedColumns != null && StructureUtils.DefaultSelectedColumns.Count > 0)
                    foreach (string c in StructureUtils.DefaultSelectedColumns)
                        _selectedColumns.Add(c);
                _setDefaultSelectedColumns = true;
            }

            // Grab structures data from settings if not set.
            if (_lastDisplayed == null)
                _lastDisplayed = SettingsPage._structuresData;

            InitTribesQuickFilter();
            InitStructuresQuickFilter();

            // Apply filters, sort and reorder columns.
            bool isQueued = this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(250);
                ApplyFiltersAndSort();
            });
        }

        private void DestroyPage()
        {
            if (_page != null)
            {
                if (_page.dg_Structures != null)
                {
                    if (_page.dg_Structures.ItemsSource != null)
                        _page.dg_Structures.ItemsSource = null;
                    if (gr_Main != null)
                        gr_Main.Children.Remove(_page.dg_Structures);
                }
                if (_page._selectedStructureFilter_Values != null)
                {
                    _page._selectedStructureFilter_Values.Clear();
                    _page._selectedStructureFilter_Values = null;
                }
                if (_page._lastDisplayedVM != null)
                {
                    _page._lastDisplayedVM.Clear();
                    _page._lastDisplayedVM = null;
                }
                _page._lastDisplayed = null;
                _page = null;
            }
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

#pragma warning disable CS8625
        public void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
#pragma warning restore CS8625

        private void RefreshPrimarySortLabel() => run_PrimarySort.Text = $"{_currentSort} {(AscendingSort ? ASILang.Get("SortAscending") : ASILang.Get("SortDescending"))}";
        private void RefreshSecondarySortLabel() => run_SecondarySort.Text = $"{_secondaryCurrentSort} {(SecondaryAscendingSort ? ASILang.Get("SortAscending") : ASILang.Get("SortDescending"))}";

        private static void InitDefaultPresets()
        {
            _defaultColumnsPreset = new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>() };

            //_defaultFiltersPreset = new JsonFiltersPreset() { Name = ASILang.Get("DefaultPreset"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_OilVeins = new JsonFiltersPreset() { Name = ASILang.Get("OilVeins"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_WaterVeins = new JsonFiltersPreset() { Name = ASILang.Get("WaterVeins"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_GasVeins = new JsonFiltersPreset() { Name = ASILang.Get("GasVeins"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_PowerNodes = new JsonFiltersPreset() { Name = ASILang.Get("PowerNodes"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_BeaverDams = new JsonFiltersPreset() { Name = ASILang.Get("BeaverDams"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_ZPlants = new JsonFiltersPreset() { Name = ASILang.Get("ZPlants"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_WyvernNests = new JsonFiltersPreset() { Name = ASILang.Get("WyvernNests"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_GigantoraptorNests = new JsonFiltersPreset() { Name = ASILang.Get("GigantoraptorNests"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_ArtifactCrates = new JsonFiltersPreset() { Name = ASILang.Get("ArtifactCrates"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_HordeCrates = new JsonFiltersPreset() { Name = ASILang.Get("HordeCrates"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_TributeTerminals = new JsonFiltersPreset() { Name = ASILang.Get("TributeTerminals"), Filters = new List<JsonFilter>() };
            _defaultFiltersPreset_CityTerminals = new JsonFiltersPreset() { Name = ASILang.Get("CityTerminals"), Filters = new List<JsonFilter>() };

            /*
            // Add "TribeID > 50000" to default filters preset.
            PropertyInfo? targetingTeam = typeof(Structure).GetProperty("TargetingTeam", BindingFlags.Instance | BindingFlags.Public);
            Filter defaultFilter = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.GREATER_THAN, FilterValue = "50000" };
            if (targetingTeam != null && _defaultFiltersPreset?.Filters != null)
            {
                _defaultFiltersPreset.Filters.Add(new JsonFilter()
                {
                    PropertyName = targetingTeam.Name,
                    Filter = defaultFilter
                });
            }
            */

            // Add special structures to their respective default filters preset.
            PropertyInfo? itemArchetype = typeof(Structure).GetProperty("ItemArchetype", BindingFlags.Instance | BindingFlags.Public);
            if (itemArchetype != null)
            {
                // Add "Oil Veins".
                Filter defaultFilter_OilVeins = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "OilVein" };
                if (_defaultFiltersPreset_OilVeins?.Filters != null)
                    _defaultFiltersPreset_OilVeins.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_OilVeins });
                // Add "Water Veins".
                Filter defaultFilter_WaterVeins = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "WaterVein" };
                if (_defaultFiltersPreset_WaterVeins?.Filters != null)
                    _defaultFiltersPreset_WaterVeins.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_WaterVeins });
                // Add "Gas Veins".
                Filter defaultFilter_GasVeins = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "GasVein" };
                if (_defaultFiltersPreset_GasVeins?.Filters != null)
                    _defaultFiltersPreset_GasVeins.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_GasVeins });
                // Add "Power Nodes".
                Filter defaultFilter_PowerNodes = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "PrimalStructurePowerNode" };
                if (_defaultFiltersPreset_PowerNodes?.Filters != null)
                    _defaultFiltersPreset_PowerNodes.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_PowerNodes });
                // Add "Beaver Dams".
                Filter defaultFilter_BeaverDams = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "BeaverDam" };
                if (_defaultFiltersPreset_BeaverDams?.Filters != null)
                    _defaultFiltersPreset_BeaverDams.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_BeaverDams });
                // Add "Z Plants".
                Filter defaultFilter_ZPlants = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "Structure_PlantSpeciesZ_Wild" };
                if (_defaultFiltersPreset_ZPlants?.Filters != null)
                    _defaultFiltersPreset_ZPlants.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_ZPlants });
                // Add "Wyvern Nests".
                Filter defaultFilter_WyvernNests = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "WyvernNest" };
                if (_defaultFiltersPreset_WyvernNests?.Filters != null)
                    _defaultFiltersPreset_WyvernNests.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_WyvernNests });
                // Add "Gigantoraptor Nests".
                Filter defaultFilter_GigantoraptorNests = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "GigantoraptorNest" };
                if (_defaultFiltersPreset_GigantoraptorNests?.Filters != null)
                    _defaultFiltersPreset_GigantoraptorNests.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_GigantoraptorNests });
                // Add "Artifact Crates".
                Filter defaultFilter_ArtifactCrates = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "ArtifactCrate" };
                if (_defaultFiltersPreset_ArtifactCrates?.Filters != null)
                    _defaultFiltersPreset_ArtifactCrates.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_ArtifactCrates });
                // Add "Horde Crates".
                Filter defaultFilter_HordeCrates = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "HordeCrates" };
                if (_defaultFiltersPreset_HordeCrates?.Filters != null)
                    _defaultFiltersPreset_HordeCrates.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_HordeCrates });
                // Add "Tribute Terminals".
                Filter defaultFilter_TributeTerminals = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "TributeTerminal" };
                if (_defaultFiltersPreset_TributeTerminals?.Filters != null)
                    _defaultFiltersPreset_TributeTerminals.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_TributeTerminals });
                // Add "City Terminals".
                Filter defaultFilter_CityTerminals = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = "CityTerminal" };
                if (_defaultFiltersPreset_CityTerminals?.Filters != null)
                    _defaultFiltersPreset_CityTerminals.Filters.Add(new JsonFilter() { PropertyName = itemArchetype.Name, Filter = defaultFilter_CityTerminals });
            }

            /*
            if (!_addedDefaultFilters && _filters != null && targetingTeam != null)
            {
                _addedDefaultFilters = true;
                _filters.Add(new KeyValuePair<PropertyInfo, Filter>(targetingTeam, defaultFilter));
            }
            */
        }

        public bool GoToStructure(int? structureID)
        {
            if (structureID != null && structureID.HasValue && _lastDisplayedVM != null)
            {
                Structure? structure = _lastDisplayedVM.FirstOrDefault(d => (d?.StructureID == structureID), null);
                if (structure == null) // Structure not found. That's probably due to current filters.
                {
                    ClearPageFiltersAndGroups(); // Remove current filtering & grouping.
                    ApplyFiltersAndSort(); // Refresh.
                    structure = _lastDisplayedVM.FirstOrDefault(d => (d?.StructureID == structureID), null);
                }
                if (structure != null)
                {
                    dg_Structures.SelectedItem = structure;
                    dg_Structures.UpdateLayout();
                    dg_Structures.ScrollIntoView(dg_Structures.SelectedItem, null);
                    return true;
                }
            }
            return false;
        }

        private bool LastStructureDoubleTap(MapPoint? point)
        {
            if (point == null || _lastDisplayedVM == null || string.IsNullOrWhiteSpace(point.ID) || point.ID == "00")
                return false;

            Structure? structure = _lastDisplayedVM.FirstOrDefault(d => (string.Compare(point.ID, d?.StructureID?.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture) == 0), null);
            if (structure != null)
            {
                dg_Structures.SelectedItem = structure;
                dg_Structures.UpdateLayout();
                dg_Structures.ScrollIntoView(dg_Structures.SelectedItem, null);
                return true;
            }
            return false;
        }

        private void Init(ref IEnumerable<Structure>? structures, bool onlyRefresh)
        {
            dg_Structures.MaxHeight = Math.Max(1.0d, gr_Main.ActualHeight - (tb_Title.ActualHeight + sp_SortAndFilters.ActualHeight + _scrollBarWidth + 4.0d));
            dg_Structures.MaxWidth = Math.Max(1.0d, gr_Main.ActualWidth - (_scrollBarWidth + 4.0d));
            if (!onlyRefresh && structures != null)
            {
                if (_lastDisplayedVM != null)
                {
                    _lastDisplayedVM.Clear();
                    _lastDisplayedVM = null;
                }
                _lastDisplayedVM = new ObservableCollection<Structure>(structures);
            }
            dg_Structures.ItemsSource = _lastDisplayedVM;
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(250);
                ReorderColumns();
            });
            if (structures != null && MainWindow._minimap != null)
                MainWindow.UpdateMinimap(structures.Select(d =>
                {
                    string structureIDStr = (d.StructureID != null && d.StructureID.HasValue ? d.StructureID.Value.ToString(CultureInfo.InvariantCulture) : "0");
                    var minimapCoords = d.GetASIMinimapCoords();
                    return new MapPoint()
                    {
                        ID = structureIDStr,
                        Name = d.ShortName,
                        Description = $"{(structureIDStr != "0" ? $"{ASILang.Get("ID")}: {structureIDStr}\n" : string.Empty)}{(d.TargetingTeam != null && d.TargetingTeam.HasValue ? $"{ASILang.Get("TribeID")}: {d.TargetingTeam.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty)}",
                        X = minimapCoords.Value,
                        Y = minimapCoords.Key
                    };
                }), LastStructureDoubleTap);
        }

        private void ReorderColumns()
        {
            if (dg_Structures?.Columns != null && dg_Structures.Columns.Count > 0)
            {
                List<string?> orders = new List<string?>();
                foreach (DataGridColumn col in dg_Structures.Columns)
                    if (col != null)
                        orders.Add(col.Header.ToString());
                orders.Sort();

                List<string?> defaultOrders = new List<string?>();

                // Add saved order.
                List<ColumnOrder>? savedOrder = LoadColumnsOrder();
                if (savedOrder != null && savedOrder.Count > 0)
                    foreach (var saved in savedOrder)
                        if (saved != null)
                            defaultOrders.Add(saved.HeaderName);

                // Add default order.
                foreach (string defaultOrder in StructureUtils.DefaultColumnsOrder)
                    if (orders.Contains(defaultOrder) && !defaultOrders.Contains(defaultOrder))
                        defaultOrders.Add(defaultOrder);

                // Add remaining unspecified order.
                foreach (string? otherOrder in orders)
                    if (!defaultOrders.Contains(otherOrder))
                        defaultOrders.Add(otherOrder);

                int j = 0;
                for (int i = 0; i < defaultOrders.Count; i++)
                {
                    foreach (DataGridColumn col in dg_Structures.Columns)
                        if (col != null && string.Compare(defaultOrders[i], col.Header.ToString(), StringComparison.InvariantCulture) == 0)
                        {
                            col.DisplayIndex = j;
                            j++;
                            break;
                        }
                }
            }
        }

        private void SaveColumnsOrder()
        {
            if (dg_Structures?.Columns == null || dg_Structures.Columns.Count <= 0)
                return;
            List<ColumnOrder> order = new List<ColumnOrder>();
            foreach (DataGridColumn col in dg_Structures.Columns)
                if (col != null)
                {
                    string? colName = col.Header.ToString();
                    if (colName != null && col.DisplayIndex >= 0)
                        order.Add(new ColumnOrder() { HeaderName = colName, DisplayIndex = col.DisplayIndex });
                }
            if (order.Count <= 0)
                return;

            try
            {
                order = order.OrderBy(o => o.DisplayIndex).ToList();
                string jsonString = JsonSerializer.Serialize<List<ColumnOrder>>(order, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.StructureColumnsOrderFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in SaveColumnsOrder. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private List<ColumnOrder>? LoadColumnsOrder()
        {
            string columnsOrderFilepath = Utils.StructureColumnsOrderFilePath();
            if (string.IsNullOrWhiteSpace(columnsOrderFilepath) || !File.Exists(columnsOrderFilepath))
                return null;

            string? columnsOrderJson = null;
            try { columnsOrderJson = File.ReadAllText(columnsOrderFilepath, Encoding.UTF8); }
            catch (Exception ex)
            {
                columnsOrderJson = null;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in LoadColumnsOrder. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (string.IsNullOrWhiteSpace(columnsOrderJson))
                return null;
            try
            {
                List<ColumnOrder>? columnsOrder = JsonSerializer.Deserialize<List<ColumnOrder>>(columnsOrderJson);
                if (columnsOrder != null && columnsOrder.Count > 0)
                    return columnsOrder;
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in LoadColumnsOrder. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            return null;
        }

        private void RefreshDisplayedColumns()
        {
            if (MainWindow._mainWindow == null)
                return;
#pragma warning disable CS1998
            MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                MainWindow._mainWindow.NavView_Navigate(typeof(HomePage), new EntranceNavigationTransitionInfo());
                MainWindow._mainWindow.NavView_Navigate(typeof(StructuresPage), new EntranceNavigationTransitionInfo());
            });
#pragma warning restore CS1998
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<Structure>? filtered = null;
            if (SettingsPage._legacySearch != null && SettingsPage._legacySearch.HasValue && SettingsPage._legacySearch.Value)
            {
                if (_group != null && _group.Count > 0)
                    filtered = ApplyGroupFiltering();
                else
                    filtered = ApplyFiltering(SettingsPage._structuresData, _filters);
            }
            else
            {
                if (SearchBuilder._query == null || SearchBuilder._query.Parts == null || SearchBuilder._query.Parts.Count <= 0)
                    filtered = SettingsPage._structuresData;
                else
                    filtered = SearchBuilder.DoSearchQuery(SearchType.STRUCTURES, true) as IEnumerable<Structure>;
            }
            if (filtered != null)
                ApplySorting(ref filtered);
            Init(ref filtered, false);
        }

        private readonly Thickness _defaultMarginLeftSortAndFilter = new Thickness(50.0d, 0.0d, 0.0d, 0.0d);
        private readonly Thickness _defaultMarginRightSortAndFilter = new Thickness(50.0d, 5.0d, 0.0d, 0.0d);
        private readonly Thickness _compactMarginSortAndFilter = new Thickness(0.0d, 5.0d, 0.0d, 0.0d);

        private void AdjustToSizeChange()
        {
            if (MainWindow._mainWindow != null)
            {
                NavigationViewDisplayMode displayMode = NavigationViewDisplayMode.Expanded;
                if (MainWindow._mainWindow._navView != null)
                    displayMode = MainWindow._mainWindow._navView.DisplayMode;
                WindowWidth = Math.Max(1, Convert.ToInt32(Math.Round(MainWindow._mainWindow.Bounds.Width)) - (displayMode == NavigationViewDisplayMode.Minimal ? 2 : (displayMode == NavigationViewDisplayMode.Compact ? 52 : 164)));
                WindowHeight = Math.Max(1, Convert.ToInt32(Math.Round(MainWindow._mainWindow.Bounds.Height)) - 52);
                if (WindowWidth < 950)
                {
                    sp_EditColumns.Margin = _compactMarginSortAndFilter;
                    sp_CurrentSort.Margin = _compactMarginSortAndFilter;

                    Grid.SetColumn(sp_EditFilters, 0);
                    Grid.SetColumn(sp_EditFiltersGroup, 0);
                    Grid.SetColumn(sp_EditColumns, 0);
                    Grid.SetColumn(sp_CurrentSort, 0);

                    Grid.SetRow(sp_EditFilters, 0);
                    Grid.SetRow(sp_EditFiltersGroup, 1);
                    Grid.SetRow(sp_EditColumns, 2);
                    Grid.SetRow(sp_CurrentSort, 3);
                }
                else
                {
                    sp_EditColumns.Margin = _defaultMarginLeftSortAndFilter;
                    sp_CurrentSort.Margin = _defaultMarginRightSortAndFilter;

                    Grid.SetColumn(sp_EditFilters, 0);
                    Grid.SetRow(sp_EditFilters, 0);

                    Grid.SetColumn(sp_EditFiltersGroup, 0);
                    Grid.SetRow(sp_EditFiltersGroup, 1);

                    Grid.SetColumn(sp_EditColumns, 1);
                    Grid.SetRow(sp_EditColumns, 0);

                    Grid.SetColumn(sp_CurrentSort, 1);
                    Grid.SetRow(sp_CurrentSort, 1);
                }
            }
        }

        private void page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustToSizeChange();
            Init(ref _lastDisplayed, true);
        }

        //private void dg_Structures_LayoutUpdated(object sender, object e) => AdjustColumnSizes();

        private void dg_Structures_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            run_NbLinesSelected.Text = (dg_Structures.SelectedItems != null ? dg_Structures.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) : "0");

            Visibility goToInventoryItemsVisibility = Visibility.Collapsed;
            DataGridRow? row = Utils.FindParent<DataGridRow>((UIElement)e.OriginalSource);
            if (row != null)
            {
                Structure? s = row.DataContext as Structure;
                if (s != null && !string.IsNullOrEmpty(s.InventoryUUID))
                    goToInventoryItemsVisibility = Visibility.Visible;
            }
            mfi_contextMenuGoToInventoryItems.Visibility = goToInventoryItemsVisibility;

            mfi_contextMenuGetAllJson.Visibility = (dg_Structures.SelectedItems != null && dg_Structures.SelectedItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed);
        }

        private void dg_Structures_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            run_NbLinesSelected.Text = (dg_Structures.SelectedItems != null ? dg_Structures.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) : "0");
        }

        private void dg_Structures_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (!_selectedColumns.Contains((string)e.Column.Header))
                e.Cancel = true; //e.Column.Visibility = Visibility.Collapsed;
            e.Column.Header = StructureUtils.GetCleanNameFromPropertyName(e.PropertyName);
        }

        private void dg_Structures_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
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
                    Minimap.ShowCallout(structureIDStr, x, y);
                }
            }
        }

        private void dg_Structures_ColumnReordered(object sender, DataGridColumnEventArgs e) => SaveColumnsOrder();

        #endregion

        #region Sorting

        private object? GetStructurePropertyValueByName(string cleanName, Structure d)
        {
            PropertyInfo? prop = Utils.GetProperty(typeof(Structure), StructureUtils.GetPropertyNameFromCleanName(cleanName));
            if (prop == null)
                return null;
            return prop.GetValue(d);
        }

        private void SimpleSort(ref IEnumerable<Structure> structures, string cleanName)
        {
            if (AscendingSort)
                structures = structures.OrderBy(o => GetStructurePropertyValueByName(cleanName, o));
            else
                structures = structures.OrderByDescending(o => GetStructurePropertyValueByName(cleanName, o));
        }

#pragma warning disable CS8604
        private void DoubleSort(ref IEnumerable<Structure> structures, string cleanName)
        {
            if (AscendingSort)
            {
                if (SecondaryAscendingSort)
                    structures = structures.OrderBy(o => GetStructurePropertyValueByName(cleanName, o)).ThenBy(o => GetStructurePropertyValueByName(SecondaryCurrentSort, o));
                else
                    structures = structures.OrderBy(o => GetStructurePropertyValueByName(cleanName, o)).ThenByDescending(o => GetStructurePropertyValueByName(SecondaryCurrentSort, o));
            }
            else
            {
                if (SecondaryAscendingSort)
                    structures = structures.OrderByDescending(o => GetStructurePropertyValueByName(cleanName, o)).ThenBy(o => GetStructurePropertyValueByName(SecondaryCurrentSort, o));
                else
                    structures = structures.OrderByDescending(o => GetStructurePropertyValueByName(cleanName, o)).ThenByDescending(o => GetStructurePropertyValueByName(SecondaryCurrentSort, o));
            }
        }
#pragma warning restore CS8604

        private void SortDataGrid(ref IEnumerable<Structure> structures, string cleanName)
        {
            if (structures == null)
                return;

            if (!string.IsNullOrWhiteSpace(CurrentSort) && SecondaryCurrentSort != null)
                DoubleSort(ref structures, cleanName);
            else
                SimpleSort(ref structures, cleanName);
        }

        private void SetSorting(string cleanName)
        {
            bool hasPreviousSort = !string.IsNullOrWhiteSpace(CurrentSort);
            bool sameColumnSort = false;
            if (hasPreviousSort)
                sameColumnSort = (string.Compare(cleanName, CurrentSort, StringComparison.InvariantCulture) == 0);
            if (hasPreviousSort)
            {
                if (sameColumnSort)
                    AscendingSort = !AscendingSort;
                else
                {
                    if (!(string.Compare(CurrentSort, SecondaryCurrentSort, StringComparison.InvariantCulture) == 0))
                        SecondaryCurrentSort = CurrentSort;
                    SecondaryAscendingSort = AscendingSort;
                    CurrentSort = cleanName;
                    AscendingSort = true;
                }
            }
            else
            {
                if (hasPreviousSort && sameColumnSort)
                    AscendingSort = !AscendingSort;
                CurrentSort = cleanName;
            }
        }

        private void ApplySorting(ref IEnumerable<Structure> structures)
        {
            if (CurrentSort != null)
            {
                SortDataGrid(ref structures, CurrentSort);
                RefreshPrimarySortLabel();
                tb_PrimarySort.Visibility = Visibility.Visible;
                if (SecondaryCurrentSort != null)
                {
                    RefreshSecondarySortLabel();
                    tb_SecondarySort.Visibility = Visibility.Visible;
                }
            }
        }

        private void dg_Structures_Sorting(object sender, DataGridColumnEventArgs e)
        {
            if (e?.Column?.Header != null)
            {
                string? cleanName = e.Column.Header.ToString();
                if (SettingsPage._structuresData != null && cleanName != null)
                {
                    SetSorting(cleanName);
                    ApplyFiltersAndSort();
                }
            }
        }

        #endregion

        #region Filtering

        private void RefreshSelectedStructureFilterValues()
        {
            tb_structureFilterValues.Text = (_selectedStructureFilter_Values != null ? string.Join(", ", _selectedStructureFilter_Values) : string.Empty);
            b_structureFilterValues.Visibility = (tb_structureFilterValues.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed);
        }

        [RelayCommand]
        private void StructureFilterValueSelect(string filterValue)
        {
            if (!string.IsNullOrWhiteSpace(filterValue) && _selectedStructureFilter_Values != null)
            {
                if (_selectedStructureFilter_Values.Contains(filterValue))
                    _selectedStructureFilter_Values.Remove(filterValue);
                else
                    _selectedStructureFilter_Values.Add(filterValue);
                RefreshSelectedStructureFilterValues();
                btn_AddToStructureFilters.IsEnabled = (_selectedStructureFilter_Values.Count > 0);
            }
        }

        [RelayCommand]
        private void StructureFilterSelect(string propName)
        {
            if (!string.IsNullOrWhiteSpace(propName))
            {
                _selectedStructureFilter_Name = propName;
                tb_StructureFilterName.Text = StructureUtils.GetCleanNameFromPropertyName(_selectedStructureFilter_Name);

                tb_StructureFilterType.Text = ASILang.Get("ClickHere");
                sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
                sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
                sp_FilterByOther.Visibility = Visibility.Collapsed;

                ResetFiltersValues();

                PropertyInfo? foundProperty = Utils.GetProperty(typeof(Structure), propName);
                if (foundProperty != null)
                {
                    string propType = foundProperty.PropertyType.ToString();
                    if (Utils.FilterBooleanTypes.Contains(propType))
                    {
                        mfi_FilterByExactMatch.Visibility = Visibility.Visible;
                        mfi_FilterByStartingWith.Visibility = Visibility.Collapsed;
                        mfi_FilterByEndingWith.Visibility = Visibility.Collapsed;
                        mfi_FilterByContaining.Visibility = Visibility.Collapsed;
                        mfi_FilterByNotContaining.Visibility = Visibility.Collapsed;
                        mfi_FilterByLowerThan.Visibility = Visibility.Collapsed;
                        mfi_FilterByGreaterThan.Visibility = Visibility.Collapsed;
                    }
                    else if (Utils.FilterNumberTypes.Contains(propType))
                    {
                        mfi_FilterByExactMatch.Visibility = Visibility.Visible;
                        mfi_FilterByStartingWith.Visibility = Visibility.Visible;
                        mfi_FilterByEndingWith.Visibility = Visibility.Visible;
                        mfi_FilterByContaining.Visibility = Visibility.Visible;
                        mfi_FilterByNotContaining.Visibility = Visibility.Visible;
                        mfi_FilterByLowerThan.Visibility = Visibility.Visible;
                        mfi_FilterByGreaterThan.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        mfi_FilterByExactMatch.Visibility = Visibility.Visible;
                        mfi_FilterByStartingWith.Visibility = Visibility.Visible;
                        mfi_FilterByEndingWith.Visibility = Visibility.Visible;
                        mfi_FilterByContaining.Visibility = Visibility.Visible;
                        mfi_FilterByNotContaining.Visibility = Visibility.Visible;
                        mfi_FilterByLowerThan.Visibility = Visibility.Collapsed;
                        mfi_FilterByGreaterThan.Visibility = Visibility.Collapsed;
                    }
                    if (_propertiesWithManyValues.Contains(propName))
                        mfi_FilterByExactMatch.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ResetFiltersValues()
        {
            btn_AddToStructureFilters.IsEnabled = false;

            tb_FilterByOther.Text = "";

            if (_selectedStructureFilter_Values != null)
                _selectedStructureFilter_Values.Clear();
            RefreshSelectedStructureFilterValues();
        }

        private void ResetFilters()
        {
            mf_StructureFilterName.Items.Clear();
            _selectedStructureFilter_Name = null;
            tb_StructureFilterOperator.Text = ASILang.Get("ClickHere");
            tb_StructureFilterName.Text = ASILang.Get("ClickHere");
            tb_StructureFilterType.Text = ASILang.Get("ClickHere");

            ResetFiltersValues();
        }

        private void FillPropertiesDropDown()
        {
            var structureProperties = typeof(Structure).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (structureProperties != null && structureProperties.Count() > 0)
            {
                Dictionary<string, string> propNames = new Dictionary<string, string>();
                List<string> toAdd = new List<string>();
                foreach (var structureProperty in structureProperties)
                {
                    // Check amount of values for current property (to know if ExactMatch filter can be used or not).
                    if (Utils.DoCheckForPropertyValuesAmount)
                        if (!StructureUtils.DoNotCheckPropertyValuesAmount.Contains(structureProperty.Name))
                        {
                            if (Utils.PropertyHasMoreValuesThan(SettingsPage._structuresData, structureProperty, MAX_PROPERTY_VALUES))
                            {
#if DEBUG
                                Logger.Instance.Log($"Found property with many values: {structureProperty.Name}", Logger.LogLevel.DEBUG);
#endif
                                if (!_propertiesWithManyValues.Contains(structureProperty.Name))
                                    _propertiesWithManyValues.Add(structureProperty.Name);
                            }
#if DEBUG
                            else if (structureProperty.Name.Contains("Time", StringComparison.InvariantCultureIgnoreCase))
                                Logger.Instance.Log($"Found property with \"time\": {structureProperty.Name}", Logger.LogLevel.DEBUG);
#endif
                        }
                    // Add current property.
                    string propName = structureProperty.Name;
                    if (propName != null)
                    {
                        string? cleanName = StructureUtils.GetCleanNameFromPropertyName(propName);
                        if (cleanName != null)
                        {
                            toAdd.Add(cleanName);
                            propNames[cleanName] = propName;
                        }
                    }
                }
                if (toAdd.Count > 0)
                {
                    toAdd.Sort();
                    foreach (string cleanName in toAdd)
                        if (cleanName != null)
                        {
                            mf_StructureFilterName.Items.Add(new MenuFlyoutItem
                            {
                                Text = cleanName,
                                Command = StructureFilterSelectCommand,
                                CommandParameter = propNames[cleanName]
                            });
                        }
                }
                propNames.Clear();
                toAdd.Clear();
            }
        }

        private bool CheckMatchFilter(Structure d, List<KeyValuePair<PropertyInfo, Filter>> filters)
        {
            if (filters == null || filters.Count <= 0)
                return true;

            foreach (var filter in filters)
                if (filter.Key != null && filter.Value != null && filter.Value.FilterOperator == FilterOperator.AND)
                {
                    if (filter.Value.FilterType == FilterType.EXACT_MATCH)
                    {
                        if (filter.Value.FilterValues != null && filter.Value.FilterValues.Count > 0)
                        {
                            string? propValue = Utils.GetPropertyValueForObject(filter.Key, d);
                            if (propValue == null || !filter.Value.FilterValues.Contains(propValue))
                                return false;
                        }
                    }
                    else if (filter.Value.FilterType == FilterType.STARTING_WITH)
                    {
                        if (filter.Value.FilterValue != null)
                        {
                            string? propValue = Utils.GetPropertyValueForObject(filter.Key, d);
                            if (propValue == null || !propValue.StartsWith(filter.Value.FilterValue))
                                return false;
                        }
                    }
                    else if (filter.Value.FilterType == FilterType.ENDING_WITH)
                    {
                        if (filter.Value.FilterValue != null)
                        {
                            string? propValue = Utils.GetPropertyValueForObject(filter.Key, d);
                            if (propValue == null || !propValue.EndsWith(filter.Value.FilterValue))
                                return false;
                        }
                    }
                    else if (filter.Value.FilterType == FilterType.CONTAINING)
                    {
                        if (filter.Value.FilterValue != null)
                        {
                            string? propValue = Utils.GetPropertyValueForObject(filter.Key, d);
                            if (propValue == null || !propValue.Contains(filter.Value.FilterValue))
                                return false;
                        }
                    }
                    else if (filter.Value.FilterType == FilterType.NOT_CONTAINING)
                    {
                        if (filter.Value.FilterValue != null)
                        {
                            string? propValue = Utils.GetPropertyValueForObject(filter.Key, d);
                            if (propValue != null && propValue.Contains(filter.Value.FilterValue))
                                return false;
                        }
                    }
                    else if (filter.Value.FilterType == FilterType.LOWER_THAN)
                    {
                        if (filter.Value.FilterValue != null && double.TryParse(filter.Value.FilterValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double filterVal))
                        {
                            string? propValue = Utils.GetPropertyValueForObject(filter.Key, d);
                            if (propValue != null && double.TryParse(propValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                            {
                                if (parsed >= filterVal)
                                    return false;
                            }
                            else
                                return false;
                        }
                    }
                    else if (filter.Value.FilterType == FilterType.GREATER_THAN)
                    {
                        if (filter.Value.FilterValue != null && double.TryParse(filter.Value.FilterValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double filterVal))
                        {
                            string? propValue = Utils.GetPropertyValueForObject(filter.Key, d);
                            if (propValue != null && double.TryParse(propValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                            {
                                if (parsed <= filterVal)
                                    return false;
                            }
                            else
                                return false;
                        }
                    }
                }
            return true;
        }

        private bool IsValidDouble(string? str) => !string.IsNullOrEmpty(str) && double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double d);

        private void AddOrFilters(ref List<Expression<Func<Structure, bool>>> orFilters, List<KeyValuePair<PropertyInfo, Filter>> filters)
        {
            if (filters == null || filters.Count <= 0)
                return;

            foreach (var filter in filters)
                if (filter.Key != null && filter.Value != null && filter.Value.FilterOperator == FilterOperator.OR)
                {
                    try
                    {
#pragma warning disable CS8604, CS8602
                        if (filter.Value.FilterType == FilterType.EXACT_MATCH)
                        {
                            if (filter.Value.FilterValues != null && filter.Value.FilterValues.Count > 0)
                                orFilters.Add(d => (Utils.GetPropertyValueForObject(filter.Key, d) != null && filter.Value.FilterValues.Contains(Utils.GetPropertyValueForObject(filter.Key, d))));
                        }
                        else if (filter.Value.FilterType == FilterType.STARTING_WITH)
                        {
                            if (filter.Value.FilterValue != null)
                                orFilters.Add(d => (Utils.GetPropertyValueForObject(filter.Key, d) != null && Utils.GetPropertyValueForObject(filter.Key, d).StartsWith(filter.Value.FilterValue)));
                        }
                        else if (filter.Value.FilterType == FilterType.ENDING_WITH)
                        {
                            if (filter.Value.FilterValue != null)
                                orFilters.Add(d => (Utils.GetPropertyValueForObject(filter.Key, d) != null && Utils.GetPropertyValueForObject(filter.Key, d).EndsWith(filter.Value.FilterValue)));
                        }
                        else if (filter.Value.FilterType == FilterType.CONTAINING)
                        {
                            if (filter.Value.FilterValue != null)
                                orFilters.Add(d => (Utils.GetPropertyValueForObject(filter.Key, d) != null && Utils.GetPropertyValueForObject(filter.Key, d).Contains(filter.Value.FilterValue)));
                        }
                        else if (filter.Value.FilterType == FilterType.NOT_CONTAINING)
                        {
                            if (filter.Value.FilterValue != null)
                                orFilters.Add(d => (Utils.GetPropertyValueForObject(filter.Key, d) == null || !Utils.GetPropertyValueForObject(filter.Key, d).Contains(filter.Value.FilterValue)));
                        }
                        else if (filter.Value.FilterType == FilterType.LOWER_THAN)
                        {
                            if (filter.Value.FilterValue != null && double.TryParse(filter.Value.FilterValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double filterVal))
                                orFilters.Add(d => (Utils.GetPropertyValueForObject(filter.Key, d) != null && IsValidDouble(Utils.GetPropertyValueForObject(filter.Key, d)) && double.Parse(Utils.GetPropertyValueForObject(filter.Key, d), NumberStyles.Any, CultureInfo.InvariantCulture) < filterVal));
                        }
                        else if (filter.Value.FilterType == FilterType.GREATER_THAN)
                        {
                            if (filter.Value.FilterValue != null && double.TryParse(filter.Value.FilterValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double filterVal))
                                orFilters.Add(d => (Utils.GetPropertyValueForObject(filter.Key, d) != null && IsValidDouble(Utils.GetPropertyValueForObject(filter.Key, d)) && double.Parse(Utils.GetPropertyValueForObject(filter.Key, d), NumberStyles.Any, CultureInfo.InvariantCulture) > filterVal));
                        }
#pragma warning restore CS8604, CS8602
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Exception caught in AddOrFilters. Exception=[{ex}]", Logger.LogLevel.ERROR);
                    }
                }
        }

        static Expression<Func<T, bool>> AnyOf<T>(params Expression<Func<T, bool>>[] expressions)
        {
            // Always include result if there is no "OR" filter
            if (expressions == null || expressions.Length == 0) return x => true;
            if (expressions.Length == 1) return expressions[0];

            var body = expressions[0].Body;
            var param = expressions[0].Parameters.Single();
            for (int i = 1; i < expressions.Length; i++)
            {
                var expr = expressions[i];
                var swappedParam = new SwapVisitor(expr.Parameters.Single(), param).Visit(expr.Body);
                body = Expression.OrElse(body, swappedParam);
            }
            return Expression.Lambda<Func<T, bool>>(body, param);
        }

        private IEnumerable<Structure>? DoApplyGroupFiltering(IEnumerable<Structure>? filtered, JsonFiltersPreset preset)
        {
            if (filtered == null)
                return null;

            IEnumerable<Structure>? ret = null;
            if (preset.Filters != null && preset.Filters.Count > 0)
            {
                List<KeyValuePair<PropertyInfo, Filter>> currentFilters = new List<KeyValuePair<PropertyInfo, Filter>>();
                Type type = typeof(Structure);
                foreach (var f in preset.Filters)
                    if (f != null && f.Filter != null)
                    {
                        PropertyInfo? prop = Utils.GetProperty(type, f.PropertyName);
                        if (prop != null)
                            currentFilters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, f.Filter));
                    }
                ret = ApplyFiltering(filtered, currentFilters);
            }
            return ret;
        }

        private IEnumerable<Structure>? ApplyGroupFiltering()
        {
            IEnumerable<Structure>? andFiltered = null;
            for (int i = 0; i < _group.Count; i++)
                if (_group[i].Key == FilterOperator.AND && _group[i].Value != null)
                {
                    if (andFiltered == null)
                        andFiltered = DoApplyGroupFiltering(SettingsPage._structuresData, _group[i].Value);
                    else
                        andFiltered = DoApplyGroupFiltering(andFiltered, _group[i].Value);
                }

            IEnumerable<Structure>? orFiltered = null;
            List<IEnumerable<Structure>?> orFiltereds = new List<IEnumerable<Structure>?>();
            for (int j = 0; j < _group.Count; j++)
                if (_group[j].Key == FilterOperator.OR && _group[j].Value != null)
                    orFiltereds.Add(DoApplyGroupFiltering(SettingsPage._structuresData, _group[j].Value));

            if (orFiltereds.Count > 0)
                foreach (var curr in orFiltereds)
                    if (curr != null)
                    {
                        if (orFiltered == null)
                            orFiltered = curr;
                        else
                            orFiltered = orFiltered.Concat(curr).Distinct();
                    }

            if (andFiltered != null && orFiltered != null)
                return andFiltered.Concat(orFiltered).Distinct();
            else if (andFiltered != null)
                return andFiltered;
            else if (orFiltered != null)
                return orFiltered;
            else
                return null;
        }

        private IEnumerable<Structure>? ApplyFiltering(IEnumerable<Structure>? structures, List<KeyValuePair<PropertyInfo, Filter>> filters)
        {
            if (structures == null)
                return null;

            var orFilters = new List<Expression<Func<Structure, bool>>>();
            AddOrFilters(ref orFilters, filters);
            var lambda = AnyOf(orFilters.ToArray());

            return structures.Where(lambda.Compile()).Where(a => CheckMatchFilter(a, filters));
        }

        public static void ClearPageFiltersAndGroups()
        {
            if (_page != null)
                _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    _page.sp_StructureFiltersPresetsInGroup.Children.Clear();
                    _page.sp_ExistingStructureFilters.Children.Clear();
                    _page.QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
                    _page.QuickFilter_Structure_Label = ASILang.Get("ClickHere");
                    _page._quickFilter_SelectedTribe = null;
                    _page._quickFilter_SelectedStructure = null;
                });
            SearchBuilder._query = new SearchQuery();
            _group.Clear();
            _filters.Clear();
            //_addedDefaultFilters = false;
            InitDefaultPresets();

            // TODO: Remove FillEditStructureFiltersPopup()?
            if (_page != null)
                _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => _page.FillEditStructureFiltersPopup());
        }

        private void FillEditStructureFiltersPopup()
        {
            sp_ExistingStructureFilters.Children.Clear();
            if (_filters == null || _filters.Count <= 0)
                return;

            Brush? brush = this.TryFindResource("AcrylicInAppFillColorDefaultBrush") as Brush;
            if (brush == null)
                brush = new SolidColorBrush(Colors.Gray);
            foreach (var filter in _filters)
                if (filter.Key != null && filter.Value != null)
                {
                    Grid grd = new Grid()
                    {
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Margin = new Thickness(0.0d, 5.0d, 0.0d, 0.0d)
                    };
                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                    TextBlock tb0 = new TextBlock()
                    {
                        FontSize = 16.0d,
                        TextWrapping = TextWrapping.Wrap,
                        Text = filter.Value.FilterOperator == FilterOperator.OR ? ASILang.Get("OperatorOR") : ASILang.Get("OperatorAND"),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
                    };
                    TextBlock tb1 = new TextBlock()
                    {
                        FontSize = 16.0d,
                        TextWrapping = TextWrapping.Wrap,
                        Text = ASILang.Get("FilterBy"),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
                    };
                    TextBlock tb2 = new TextBlock()
                    {
                        FontSize = 16.0d,
                        TextWrapping = TextWrapping.Wrap,
                        Text = $"{StructureUtils.GetCleanNameFromPropertyName(filter.Key.Name)}",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
                    };
                    Border b = new Border()
                    {
                        BorderBrush = brush,
                        BorderThickness = new Thickness(1.0d),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0.0d, 0.0d, 10.0d, 0.0d)
                    };
                    ScrollViewer sv = new ScrollViewer()
                    {
                        MinWidth = 100.0d,
                        MaxWidth = 350.0d,
                        MaxHeight = 85.0d,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };

                    string tbFilterTypeStr = ASILang.Get("WithValue");
                    string tbValueStr = "";
                    if (filter.Value.FilterType == FilterType.EXACT_MATCH)
                    {
                        tbFilterTypeStr = ASILang.Get("WithValues");
                        if (filter.Value.FilterValues != null)
                            tbValueStr = string.Join(", ", filter.Value.FilterValues);
                    }
                    else
                    {
                        if (filter.Value.FilterType == FilterType.STARTING_WITH)
                            tbFilterTypeStr = ASILang.Get("FilterType_StartingWith").ToLowerInvariant();
                        else if (filter.Value.FilterType == FilterType.ENDING_WITH)
                            tbFilterTypeStr = ASILang.Get("FilterType_EndingWith").ToLowerInvariant();
                        else if (filter.Value.FilterType == FilterType.CONTAINING)
                            tbFilterTypeStr = ASILang.Get("FilterType_Containing").ToLowerInvariant();
                        else if (filter.Value.FilterType == FilterType.CONTAINING)
                            tbFilterTypeStr = ASILang.Get("FilterType_NotContaining").ToLowerInvariant();
                        else if (filter.Value.FilterType == FilterType.LOWER_THAN)
                            tbFilterTypeStr = ASILang.Get("FilterType_LowerThan").ToLowerInvariant();
                        else if (filter.Value.FilterType == FilterType.GREATER_THAN)
                            tbFilterTypeStr = ASILang.Get("FilterType_GreaterThan").ToLowerInvariant();
                        if (filter.Value.FilterValue != null)
                            tbValueStr = filter.Value.FilterValue;
                    }

                    TextBlock tb3 = new TextBlock()
                    {
                        FontSize = 16.0d,
                        TextWrapping = TextWrapping.Wrap,
                        Text = tbFilterTypeStr,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
                    };
                    TextBlock tbValues = new TextBlock()
                    {
                        FontSize = 14.0d,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 350.0d,
                        Text = tbValueStr
                    };
                    Button btn = new Button()
                    {
                        Width = 120.0d,
                        Content = ASILang.Get("Remove"),
                        FontSize = 18.0d,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    btn.Click += btn_RemoveFilter_Click;

                    sv.Content = tbValues;
                    b.Child = sv;
                    grd.Children.Add(tb0);
                    Grid.SetColumn(tb0, 0);
                    grd.Children.Add(tb1);
                    Grid.SetColumn(tb1, 1);
                    grd.Children.Add(tb2);
                    Grid.SetColumn(tb2, 2);
                    grd.Children.Add(tb3);
                    Grid.SetColumn(tb3, 3);
                    grd.Children.Add(b);
                    Grid.SetColumn(b, 4);
                    grd.Children.Add(btn);
                    Grid.SetColumn(btn, 5);
                    sp_ExistingStructureFilters.Children.Add(grd);
                }
        }

        private void cb_IncludePropertiesWithManyValues_CheckedUnchecked(object sender, RoutedEventArgs e)
        {
            ResetFilters();
            FillPropertiesDropDown();
        }

        private void btn_AddFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!AddStructureFilterPopup.IsOpen)
            {
                ResetFilters();
                FillPropertiesDropDown();
                AddStructureFilterPopup.IsOpen = true;
            }
        }

        private void btn_CloseStructureFiltersPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddStructureFilterPopup.IsOpen)
                AddStructureFilterPopup.IsOpen = false;
        }

        private void btn_AddToStructureFilters_Click(object sender, RoutedEventArgs e)
        {
            FilterOperator filterOperator = FilterOperator.NONE;
            if (string.Compare(ASILang.Get("OperatorAND"), tb_StructureFilterOperator.Text, StringComparison.InvariantCulture) == 0)
                filterOperator = FilterOperator.AND;
            else if (string.Compare(ASILang.Get("OperatorOR"), tb_StructureFilterOperator.Text, StringComparison.InvariantCulture) == 0)
                filterOperator = FilterOperator.OR;
            if (filterOperator == FilterOperator.NONE)
            {
                MainWindow.ShowToast(ASILang.Get("MissingOperatorCannotAddFilter"), BackgroundColor.WARNING);
                return;
            }

            if (AddStructureFilterPopup.IsOpen)
            {
                AddStructureFilterPopup.IsOpen = false;
                if (!string.IsNullOrEmpty(_selectedStructureFilter_Name))
                {
                    PropertyInfo? prop = Utils.GetProperty(typeof(Structure), _selectedStructureFilter_Name);
                    if (prop != null)
                    {
                        if (tb_StructureFilterType.Text == ASILang.Get("FilterType_ExactMatch") &&
                            _selectedStructureFilter_Values != null &&
                            _selectedStructureFilter_Values.Count > 0)
                        {
                            _filters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, new Filter()
                            {
                                FilterOperator = filterOperator,
                                FilterType = FilterType.EXACT_MATCH,
                                FilterValues = new List<string>(_selectedStructureFilter_Values)
                            }));
                        }
                        else if (tb_FilterByOther.Text != null)
                        {
                            FilterType ft = FilterType.NONE;
                            if (tb_StructureFilterType.Text == ASILang.Get("FilterType_StartingWith"))
                                ft = FilterType.STARTING_WITH;
                            else if (tb_StructureFilterType.Text == ASILang.Get("FilterType_EndingWith"))
                                ft = FilterType.ENDING_WITH;
                            else if (tb_StructureFilterType.Text == ASILang.Get("FilterType_Containing"))
                                ft = FilterType.CONTAINING;
                            else if (tb_StructureFilterType.Text == ASILang.Get("FilterType_NotContaining"))
                                ft = FilterType.NOT_CONTAINING;
                            else if (tb_StructureFilterType.Text == ASILang.Get("FilterType_LowerThan"))
                                ft = FilterType.LOWER_THAN;
                            else if (tb_StructureFilterType.Text == ASILang.Get("FilterType_GreaterThan"))
                                ft = FilterType.GREATER_THAN;
                            if (ft != FilterType.NONE)
                            {
                                _filters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, new Filter()
                                {
                                    FilterOperator = filterOperator,
                                    FilterType = ft,
                                    FilterValue = tb_FilterByOther.Text
                                }));
                            }
                        }
                        ApplyFiltersAndSort();
                    }
                }
            }
        }

        private void btn_EditFilters_Click(object sender, RoutedEventArgs e)
        {
            if (EditStructureFiltersPopup.IsOpen)
                return;

            FillEditStructureFiltersPopup();
            EditStructureFiltersPopup.IsOpen = true;
        }

        private void RemoveFilter(PropertyInfo prop, Filter filter)
        {
            if (_filters != null && _filters.Count > 0)
            {
                int toDel = -1;
                for (int i = 0; i < _filters.Count; i++)
                    if (_filters[i].Key != null && string.Compare(_filters[i].Key.Name, prop.Name, StringComparison.InvariantCulture) == 0 && _filters[i].Value == filter)
                    {
                        toDel = i;
                        break;
                    }
                _filters.RemoveAt(toDel);
            }
        }

        private void RemoveAllFiltersForProp(PropertyInfo prop)
        {
            List<int> toDel = new List<int>();
            if (_filters != null && _filters.Count > 0)
            {
                for (int i = 0; i < _filters.Count; i++)
                    if (string.Compare(_filters[i].Key.Name, prop.Name, false, CultureInfo.InvariantCulture) == 0)
                        toDel.Add(i);
                if (toDel.Count > 0)
                {
                    toDel.Reverse();
                    foreach (int delIndex in toDel)
                        _filters.RemoveAt(delIndex);
                }
            }
        }

        private void btn_RemoveFilter_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn == null)
                return;

            Grid? grd = btn.Parent as Grid;
            if (grd == null)
                return;

            TextBlock? tb = grd.Children[2] as TextBlock;
            if (tb == null)
                return;

            string? filterName = tb.Text;
            if (filterName == null)
                return;

            filterName = StructureUtils.GetPropertyNameFromCleanName(filterName);
            if (filterName == null)
                return;

            if (_filters != null && _filters.Count > 0)
            {
                PropertyInfo? toDelProp = null;
                Filter? toDelFilter = null;
                for (int i = 0; i < _filters.Count; i++)
                {
                    var filter = _filters.ElementAt(i);
                    if (filter.Key != null && string.Compare(filter.Key.Name, filterName, StringComparison.InvariantCulture) == 0)
                    {
                        toDelProp = filter.Key;
                        toDelFilter = filter.Value;
                        break;
                    }
                }
                if (toDelProp != null && toDelFilter != null)
                {
                    RemoveFilter(toDelProp, toDelFilter);
                    FillEditStructureFiltersPopup();
                    ApplyFiltersAndSort();
                }
            }
        }

        private void btn_CloseEditStructureFiltersPopup_Click(object sender, RoutedEventArgs e)
        {
            if (EditStructureFiltersPopup.IsOpen)
                EditStructureFiltersPopup.IsOpen = false;
        }

        private void btn_RemoveAllStructureFilters_Click(object sender, RoutedEventArgs e)
        {
            _filters.Clear();
            QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
            QuickFilter_Structure_Label = ASILang.Get("ClickHere");
            FillEditStructureFiltersPopup();
            ApplyFiltersAndSort();
        }

        private void mfi_FilterByExactMatch_Click(object sender, RoutedEventArgs e)
        {
            tb_StructureFilterType.Text = ASILang.Get("FilterType_ExactMatch");

            ResetFiltersValues();

            if (!string.IsNullOrWhiteSpace(_selectedStructureFilter_Name))
            {
                PropertyInfo? foundProperty = Utils.GetProperty(typeof(Structure), _selectedStructureFilter_Name);
                List<string> propertiesValues = Utils.GetPropertyValues(SettingsPage._structuresData, foundProperty, MAX_PROPERTY_VALUES);
                mf_StructureFilterValue.Items.Clear();
                if (propertiesValues.Count > 0)
                {
                    propertiesValues.Sort();
                    foreach (string val in propertiesValues)
                        if (val != null)
                        {
                            mf_StructureFilterValue.Items.Add(new MenuFlyoutItem
                            {
                                Text = val,
                                Command = StructureFilterValueSelectCommand,
                                CommandParameter = val
                            });
                        }
                }
            }

            sp_FilterByExactMatch.Visibility = Visibility.Visible;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Visible;
            sp_FilterByOther.Visibility = Visibility.Collapsed;
        }

        private void SetStructureFilterType(string structureFilterType)
        {
            tb_StructureFilterType.Text = structureFilterType;

            ResetFiltersValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByStartingWith_Click(object sender, RoutedEventArgs e) => SetStructureFilterType(ASILang.Get("FilterType_StartingWith"));

        private void mfi_FilterByEndingWith_Click(object sender, RoutedEventArgs e) => SetStructureFilterType(ASILang.Get("FilterType_EndingWith"));

        private void mfi_FilterByContaining_Click(object sender, RoutedEventArgs e) => SetStructureFilterType(ASILang.Get("FilterType_Containing"));

        private void mfi_FilterByNotContaining_Click(object sender, RoutedEventArgs e) => SetStructureFilterType(ASILang.Get("FilterType_NotContaining"));

        private void mfi_FilterByLowerThan_Click(object sender, RoutedEventArgs e) => SetStructureFilterType(ASILang.Get("FilterType_LowerThan"));

        private void mfi_FilterByGreaterThan_Click(object sender, RoutedEventArgs e) => SetStructureFilterType(ASILang.Get("FilterType_GreaterThan"));

        private void tb_FilterByOther_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_AddToStructureFilters.IsEnabled = (tb_FilterByOther.Text.Length > 0);
        }

        private void mfi_FilterOperatorAnd_Click(object sender, RoutedEventArgs e) => tb_StructureFilterOperator.Text = ASILang.Get("OperatorAND");

        private void mfi_FilterOperatorOr_Click(object sender, RoutedEventArgs e) => tb_StructureFilterOperator.Text = ASILang.Get("OperatorOR");

        [RelayCommand]
        private void FiltersPresetGroupSelect(JsonFiltersPreset? preset)
        {
            tb_StructureFiltersGroupName.Text = ASILang.Get("ClickHere");
            btn_AddToStructureFiltersGroup.IsEnabled = false;
            if (preset == null || string.IsNullOrEmpty(preset.Name))
            {
                MainWindow.ShowToast(ASILang.Get("IncorrectFiltersPreset"), BackgroundColor.WARNING);
                return;
            }

            tb_StructureFiltersGroupName.Text = preset.Name;
            btn_AddToStructureFiltersGroup.IsEnabled = true;
        }

        private void FillFiltersGroupNames()
        {
            mf_StructureFiltersGroupNames.Items.Clear();
            if (_filtersPresets != null && _filtersPresets.Count > 0)
                foreach (var preset in _filtersPresets)
                    if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Filters != null && preset.Filters.Count > 0)
                    {
                        mf_StructureFiltersGroupNames.Items.Add(new MenuFlyoutItem
                        {
                            Text = preset.Name,
                            Command = FiltersPresetGroupSelectCommand,
                            CommandParameter = preset
                        });
                    }
        }

        private void btn_AddFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            if (AddStructureFiltersGroupPopup.IsOpen)
                return;

            FillFiltersGroupNames();
            AddStructureFiltersGroupPopup.IsOpen = true;
        }

        private void btn_RemoveFiltersPresetFromGroup_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn == null)
                return;

            Grid? grd = btn.Parent as Grid;
            if (grd == null)
                return;

            TextBlock? tb = grd.Children[1] as TextBlock;
            if (tb == null)
                return;

            string? filtersPresetName = tb.Text;
            if (string.IsNullOrEmpty(filtersPresetName))
                return;

            if (_group != null && _group.Count > 0)
            {
                int toDel = -1;
                for (int i = 0; i < _group.Count; i++)
                    if (_group[i].Value != null && string.Compare(filtersPresetName, _group[i].Value.Name, StringComparison.InvariantCulture) == 0)
                    {
                        toDel = i;
                        break;
                    }

                if (toDel >= 0)
                {
                    _group.RemoveAt(toDel);
                    FillEditFiltersGroup();
                    ApplyFiltersAndSort();
                }
            }
        }

        private void FillEditFiltersGroup()
        {
            sp_StructureFiltersPresetsInGroup.Children.Clear();
            if (_group != null && _group.Count > 0)
            {
                Brush? brush = this.TryFindResource("AcrylicInAppFillColorDefaultBrush") as Brush;
                if (brush == null)
                    brush = new SolidColorBrush(Colors.Gray);
                foreach (var filter in _group)
                    if (filter.Key != FilterOperator.NONE && filter.Value != null && !string.IsNullOrEmpty(filter.Value.Name))
                    {
                        Grid grd = new Grid()
                        {
                            VerticalAlignment = VerticalAlignment.Top,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Margin = new Thickness(0.0d, 5.0d, 0.0d, 0.0d)
                        };
                        grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                        grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                        grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                        TextBlock tb1 = new TextBlock()
                        {
                            FontSize = 16.0d,
                            TextWrapping = TextWrapping.Wrap,
                            Text = $"{(filter.Key == FilterOperator.OR ? ASILang.Get("ORFiltersPreset") : ASILang.Get("ANDFiltersPreset"))}",
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
                        };
                        TextBlock tb2 = new TextBlock()
                        {
                            FontSize = 16.0d,
                            TextWrapping = TextWrapping.Wrap,
                            Text = $"{filter.Value.Name}",
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
                        };
                        Button btn = new Button()
                        {
                            Width = 120.0d,
                            Content = ASILang.Get("Remove"),
                            FontSize = 18.0d,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Right
                        };
                        btn.Click += btn_RemoveFiltersPresetFromGroup_Click;

                        grd.Children.Add(tb1);
                        Grid.SetColumn(tb1, 0);
                        grd.Children.Add(tb2);
                        Grid.SetColumn(tb2, 1);
                        grd.Children.Add(btn);
                        Grid.SetColumn(btn, 2);
                        sp_StructureFiltersPresetsInGroup.Children.Add(grd);
                    }
            }
        }

        private void btn_EditFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            if (EditStructureFiltersGroupPopup.IsOpen)
                return;

            FillEditFiltersGroup();
            EditStructureFiltersGroupPopup.IsOpen = true;
        }

        private void mfi_FiltersGroupOperatorAnd_Click(object sender, RoutedEventArgs e) => tb_StructureFiltersGroupOperator.Text = ASILang.Get("OperatorAND");

        private void mfi_FiltersGroupOperatorOr_Click(object sender, RoutedEventArgs e) => tb_StructureFiltersGroupOperator.Text = ASILang.Get("OperatorOR");

        private void btn_AddToStructureFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            FilterOperator groupOperator = FilterOperator.NONE;
            if (string.Compare(tb_StructureFiltersGroupOperator.Text, ASILang.Get("OperatorAND"), StringComparison.InvariantCulture) == 0)
                groupOperator = FilterOperator.AND;
            else if (string.Compare(tb_StructureFiltersGroupOperator.Text, ASILang.Get("OperatorOR"), StringComparison.InvariantCulture) == 0)
                groupOperator = FilterOperator.OR;
            if (groupOperator == FilterOperator.NONE)
            {
                MainWindow.ShowToast(ASILang.Get("MissingOperatorCannotAddGroup"), BackgroundColor.WARNING);
                AddStructureFiltersGroupPopup.IsOpen = false;
                return;
            }

            if (_filtersPresets != null && _filtersPresets.Count > 0)
                foreach (var preset in _filtersPresets)
                    if (preset != null && !string.IsNullOrEmpty(preset.Name) &&
                        preset.Filters != null && preset.Filters.Count > 0 &&
                        string.Compare(preset.Name, tb_StructureFiltersGroupName.Text, StringComparison.InvariantCulture) == 0)
                    {
                        _group.Add(new KeyValuePair<FilterOperator, JsonFiltersPreset>(groupOperator, preset));
                        MainWindow.ShowToast($"{(ASILang.Get("FiltersPresetAddedToGroup").Replace("#PRESET_NAME#", $"\"{tb_StructureFiltersGroupName.Text}\"", StringComparison.InvariantCulture))}", BackgroundColor.SUCCESS);
                        ApplyFiltersAndSort();
                        AddStructureFiltersGroupPopup.IsOpen = false;
                        return;
                    }

            MainWindow.ShowToast($"{(ASILang.Get("CannotFindFiltersPreset").Replace("#PRESET_NAME#", $"\"{tb_StructureFiltersGroupName.Text}\"", StringComparison.InvariantCulture))}", BackgroundColor.WARNING);
            AddStructureFiltersGroupPopup.IsOpen = false;
        }

        private void btn_CloseStructureFiltersGroupPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddStructureFiltersGroupPopup.IsOpen)
                AddStructureFiltersGroupPopup.IsOpen = false;
        }

        private void btn_RemoveAllStructureFiltersPresetsFromGroup_Click(object sender, RoutedEventArgs e)
        {
            sp_StructureFiltersPresetsInGroup.Children.Clear();
            _group.Clear();
        }

        private void btn_CloseEditStructureFiltersGroupPopup_Click(object sender, RoutedEventArgs e)
        {
            if (EditStructureFiltersGroupPopup.IsOpen)
                EditStructureFiltersGroupPopup.IsOpen = false;
        }

        #endregion

        #region Selected columns

        private void DoFillModifyColumnsPopup(string cleanName, string propName)
        {
            CheckBox cb = new CheckBox()
            {
                FontSize = 14.0d,
                Content = cleanName,
                IsChecked = _selectedColumns.Contains(propName),
                Margin = new Thickness(0.0d, 5.0d, 0.0d, 0.0d),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ToolTipService.SetToolTip(cb, propName);
            cb.Checked += cb_ShowColumn_Checked;
            cb.Unchecked += cb_ShowColumn_Unchecked;
            sp_ExistingColumns.Children.Add(cb);
        }

        private void FillModifyColumnsPopup()
        {
            sp_ExistingColumns.Children.Clear();
            var structureProperties = typeof(Structure).GetProperties();
            if (structureProperties != null && structureProperties.Count() > 0)
            {
                List<KeyValuePair<string, string>>? props = new List<KeyValuePair<string, string>>();
                foreach (var structureProperty in structureProperties)
                {
                    string propName = structureProperty.Name;
                    if (!string.IsNullOrEmpty(propName))
                    {
                        string? cleanName = StructureUtils.GetCleanNameFromPropertyName(propName);
                        if (cleanName != null)
                            props.Add(new KeyValuePair<string, string>(cleanName, propName));
                    }
                }
                props.Sort((a, b) => a.Key.CompareTo(b.Key));
                foreach (var p in props)
                    DoFillModifyColumnsPopup(p.Key, p.Value);
                props.Clear();
                props = null;
            }
        }

        private void cb_ShowColumn_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox? cb = sender as CheckBox;
            if (cb == null)
                return;

            string propName = (string)ToolTipService.GetToolTip(cb);
            if (string.IsNullOrEmpty(propName))
                return;

            if (_selectedColumns.Contains(propName))
            {
                _selectedColumns.Remove(propName);
                if (string.Compare(CurrentSort, propName, StringComparison.InvariantCulture) == 0)
                {
                    SecondaryCurrentSort = null;
                    SecondaryAscendingSort = true;
                    CurrentSort = null;
                    AscendingSort = true;
                }
                else if (string.Compare(SecondaryCurrentSort, propName, StringComparison.InvariantCulture) == 0)
                {
                    SecondaryCurrentSort = null;
                    SecondaryAscendingSort = true;
                }
            }
        }

        private void cb_ShowColumn_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox? cb = sender as CheckBox;
            if (cb == null)
                return;

            string propName = (string)ToolTipService.GetToolTip(cb);
            if (string.IsNullOrEmpty(propName))
                return;

            if (!_selectedColumns.Contains(propName))
                _selectedColumns.Add(propName);
        }

        private void btn_ChangeColumns_Click(object sender, RoutedEventArgs e)
        {
            if (ModifyColumnsPopup.IsOpen)
                return;

            FillModifyColumnsPopup();
            ModifyColumnsPopup.IsOpen = true;
        }

        private void btn_CloseModifyColumnsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ModifyColumnsPopup.IsOpen)
            {
                ModifyColumnsPopup.IsOpen = false;
                RefreshDisplayedColumns();
            }
        }

        #endregion

        #region Filters presets

        public void LoadFiltersPresets()
        {
            _filtersPresets.Clear();

            string filtersPresetsPath = Utils.StructureFiltersPresetsFilePath();
            if (!File.Exists(filtersPresetsPath))
                return;

            string filtersPresetsJson = File.ReadAllText(filtersPresetsPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(filtersPresetsJson))
                return;

            try
            {
                List<JsonFiltersPreset>? jsonFiltersPresets = JsonSerializer.Deserialize<List<JsonFiltersPreset>>(filtersPresetsJson);
                if (jsonFiltersPresets != null && jsonFiltersPresets.Count > 0)
                {
                    // Set filter operator to "AND" if not set, to ensure backward compatibility. This can be removed once everybody have their filters updated, in 1 or 2 months.
                    foreach (var jfp in jsonFiltersPresets)
                        if (jfp != null && jfp.Filters != null && jfp.Filters.Count > 0)
                            foreach (var jf in jfp.Filters)
                                if (jf != null && jf.Filter != null)
                                    if (jf.Filter.FilterOperator == FilterOperator.NONE)
                                        jf.Filter.FilterOperator = FilterOperator.AND;

                    _filtersPresets = jsonFiltersPresets;
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in LoadFiltersPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public void SaveFiltersPresets()
        {
            if (_filters == null || _filters.Count <= 0)
                return;
            try
            {
                if (_filtersPresets != null && _filtersPresets.Count > 0)
                {
                    string jsonString = JsonSerializer.Serialize<List<JsonFiltersPreset>>(_filtersPresets, new JsonSerializerOptions() { WriteIndented = true });
                    File.WriteAllText(Utils.StructureFiltersPresetsFilePath(), jsonString, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in SaveFiltersPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        [RelayCommand]
        private void FiltersPresetSelect(JsonFiltersPreset? preset)
        {
            _selectedFiltersPreset = null;
            tb_ExistingFiltersPreset.Text = ASILang.Get("ClickHere");
            btn_LoadFiltersPreset.IsEnabled = false;
            btn_RemoveFiltersPreset.IsEnabled = false;

            if (preset == null)
                return;

            _selectedFiltersPreset = preset;
            tb_ExistingFiltersPreset.Text = preset.Name;
            btn_LoadFiltersPreset.IsEnabled = true;
            btn_RemoveFiltersPreset.IsEnabled = (string.Compare(preset.Name, ASILang.Get("DefaultPreset"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("OilVeins"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("WaterVeins"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("GasVeins"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("PowerNodes"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("BeaverDams"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("ZPlants"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("WyvernNests"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("GigantoraptorNests"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("ArtifactCrates"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("HordeCrates"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("TributeTerminals"), StringComparison.InvariantCulture) != 0 &&
                string.Compare(preset.Name, ASILang.Get("CityTerminals"), StringComparison.InvariantCulture) != 0);
        }

        private void FillFiltersPresetsDropDown()
        {
            mf_ExistingFiltersPresets.Items.Clear();
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("DefaultPreset"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("OilVeins"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_OilVeins });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("WaterVeins"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_WaterVeins });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("GasVeins"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_GasVeins });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("PowerNodes"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_PowerNodes });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("BeaverDams"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_BeaverDams });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("ZPlants"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_ZPlants });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("WyvernNests"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_WyvernNests });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("GigantoraptorNests"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_GigantoraptorNests });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("ArtifactCrates"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_ArtifactCrates });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("HordeCrates"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_HordeCrates });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("TributeTerminals"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_TributeTerminals });
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem { Text = ASILang.Get("CityTerminals"), Command = FiltersPresetSelectCommand, CommandParameter = _defaultFiltersPreset_CityTerminals });
            foreach (var preset in _filtersPresets)
                if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Filters != null && preset.Filters.Count > 0)
                {
                    mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem
                    {
                        Text = preset.Name,
                        Command = FiltersPresetSelectCommand,
                        CommandParameter = preset
                    });
                }
        }

        private void btn_FiltersPresets_Click(object sender, RoutedEventArgs e)
        {
            if (FiltersPresetsPopup.IsOpen)
                return;

            tb_FiltersPresetName.Text = "";
            _selectedFiltersPreset = null;
            tb_ExistingFiltersPreset.Text = ASILang.Get("ClickHere");
            btn_LoadFiltersPreset.IsEnabled = false;
            btn_RemoveFiltersPreset.IsEnabled = false;

            LoadFiltersPresets();
            FillFiltersPresetsDropDown();

            FiltersPresetsPopup.IsOpen = true;
        }

        private void btn_CloseFiltersPresetsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (FiltersPresetsPopup.IsOpen)
                FiltersPresetsPopup.IsOpen = false;
        }

        private void btn_SaveCurrentFilters_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tb_FiltersPresetName.Text))
            {
                MainWindow.ShowToast(ASILang.Get("PresetNeedsName"), BackgroundColor.ERROR);
                return;
            }

            if (string.Compare(tb_FiltersPresetName.Text, ASILang.Get("DefaultPreset"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("OilVeins"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("WaterVeins"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("GasVeins"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("PowerNodes"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("BeaverDams"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("ZPlants"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("WyvernNests"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("GigantoraptorNests"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("ArtifactCrates"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("HordeCrates"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("TributeTerminals"), StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(tb_FiltersPresetName.Text, ASILang.Get("CityTerminals"), StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                MainWindow.ShowToast(ASILang.Get("PresetNameAlreadyExists"), BackgroundColor.ERROR);
                return;
            }

            foreach (var preset in _filtersPresets)
                if (preset != null && preset.Name != null && string.Compare(preset.Name, tb_FiltersPresetName.Text, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    MainWindow.ShowToast(ASILang.Get("PresetNameAlreadyExists"), BackgroundColor.ERROR);
                    return;
                }

            try
            {
                List<JsonFilter> filters = new List<JsonFilter>();
                foreach (var filter in _filters)
                    if (filter.Key != null && filter.Value != null)
                        filters.Add(new JsonFilter()
                        {
                            PropertyName = filter.Key.Name,
                            Filter = filter.Value
                        });
                if (filters.Count > 0)
                {
                    _selectedFiltersPreset = null;
                    tb_ExistingFiltersPreset.Text = ASILang.Get("ClickHere");
                    btn_LoadFiltersPreset.IsEnabled = false;
                    btn_RemoveFiltersPreset.IsEnabled = false;

                    JsonFiltersPreset newPreset = new JsonFiltersPreset()
                    {
                        Name = tb_FiltersPresetName.Text,
                        Filters = filters,
                    };
                    _filtersPresets.Add(newPreset);
                    mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem
                    {
                        Text = newPreset.Name,
                        Command = FiltersPresetSelectCommand,
                        CommandParameter = newPreset
                    });
                }
                SaveFiltersPresets();
                MainWindow.ShowToast(ASILang.Get("PresetSaved"), BackgroundColor.SUCCESS);
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in btn_SaveCurrentFilters_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void btn_LoadFiltersPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiltersPreset == null || _selectedFiltersPreset.Filters == null || _selectedFiltersPreset.Filters.Count <= 0)
            {
                MainWindow.ShowToast(ASILang.Get("FiltersPresetIsEmpty"), BackgroundColor.WARNING);
                return;
            }

            _filters.Clear();
            QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
            QuickFilter_Structure_Label = ASILang.Get("ClickHere");

            Type type = typeof(Structure);
            foreach (var filter in _selectedFiltersPreset.Filters)
                if (filter != null && !string.IsNullOrEmpty(filter.PropertyName) && filter.Filter != null)
                {
                    PropertyInfo? prop = Utils.GetProperty(type, filter.PropertyName);
                    if (prop != null)
                        _filters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, filter.Filter));
                }
            // TODO: Remove FillEditStructureFiltersPopup()?
            FillEditStructureFiltersPopup();
            ApplyFiltersAndSort();
            MainWindow.ShowToast(ASILang.Get("FiltersPresetLoaded"), BackgroundColor.SUCCESS);
        }

        private void btn_RemoveFiltersPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiltersPreset == null || string.IsNullOrEmpty(_selectedFiltersPreset.Name))
            {
                MainWindow.ShowToast(ASILang.Get("NoFiltersPresetSelected"), BackgroundColor.WARNING);
                return;
            }

            if (_filtersPresets != null && _filtersPresets.Count > 0)
            {
                int toDel = -1;
                for (int i = 0; i < _filtersPresets.Count; i++)
                    if (_filtersPresets[i] != null && string.Compare(_filtersPresets[i].Name, _selectedFiltersPreset.Name, StringComparison.InvariantCulture) == 0)
                    {
                        toDel = i;
                        break;
                    }

                if (toDel >= 0)
                {
                    _selectedFiltersPreset = null;
                    tb_ExistingFiltersPreset.Text = ASILang.Get("ClickHere");
                    btn_LoadFiltersPreset.IsEnabled = false;
                    btn_RemoveFiltersPreset.IsEnabled = false;

                    _filtersPresets.RemoveAt(toDel);
                    SaveFiltersPresets();
                    FillFiltersPresetsDropDown();
                }
                else
                    MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", $"\"{_selectedFiltersPreset.Name}\"", StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
            }
        }

        private void tb_FiltersPresetName_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_SaveCurrentFilters.IsEnabled = (tb_FiltersPresetName.Text.Length > 0);
        }

        private void DefaultFiltersPresetSelected(string presetLabel, JsonFiltersPreset? preset)
        {
            tb_ExistingFiltersPreset.Text = presetLabel;
            btn_LoadFiltersPreset.IsEnabled = true;
            btn_RemoveFiltersPreset.IsEnabled = false;
            _selectedFiltersPreset = preset;
        }

        private void mfi_DefaultFiltersPreset_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("DefaultPreset"), _defaultFiltersPreset);

        private void mfi_DefaultFiltersPreset_OilVeins_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("OilVeins"), _defaultFiltersPreset_OilVeins);
        
        private void mfi_DefaultFiltersPreset_WaterVeins_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("WaterVeins"), _defaultFiltersPreset_WaterVeins);
        
        private void mfi_DefaultFiltersPreset_GasVeins_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("GasVeins"), _defaultFiltersPreset_GasVeins);
        
        private void mfi_DefaultFiltersPreset_PowerNodes_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("PowerNodes"), _defaultFiltersPreset_PowerNodes);

        private void mfi_DefaultFiltersPreset_BeaverDams_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("BeaverDams"), _defaultFiltersPreset_BeaverDams);
        
        private void mfi_DefaultFiltersPreset_ZPlants_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("ZPlants"), _defaultFiltersPreset_ZPlants);

        private void mfi_DefaultFiltersPreset_WyvernNests_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("WyvernNests"), _defaultFiltersPreset_WyvernNests);

        private void mfi_DefaultFiltersPreset_GigantoraptorNests_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("GigantoraptorNests"), _defaultFiltersPreset_GigantoraptorNests);

        private void mfi_DefaultFiltersPreset_ArtifactCrates_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("ArtifactCrates"), _defaultFiltersPreset_ArtifactCrates);
        
        private void mfi_DefaultFiltersPreset_HordeCrates_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("HordeCrates"), _defaultFiltersPreset_HordeCrates);

        private void mfi_DefaultFiltersPreset_TributeTerminals_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("TributeTerminals"), _defaultFiltersPreset_TributeTerminals);
        
        private void mfi_DefaultFiltersPreset_CityTerminals_Click(object sender, RoutedEventArgs e) => DefaultFiltersPresetSelected(ASILang.Get("CityTerminals"), _defaultFiltersPreset_CityTerminals);

        #endregion

        #region Columns preset

        public void LoadColumnsPresets()
        {
            _columnsPresets.Clear();

            string columnsPresetsPath = Utils.StructureColumnsPresetsFilePath();
            if (!File.Exists(columnsPresetsPath))
                return;

            string columnsPresetsJson = File.ReadAllText(columnsPresetsPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(columnsPresetsJson))
                return;

            try
            {
                List<JsonColumnsPreset>? jsonColumnsPresets = JsonSerializer.Deserialize<List<JsonColumnsPreset>>(columnsPresetsJson);
                if (jsonColumnsPresets != null && jsonColumnsPresets.Count > 0)
                    _columnsPresets = jsonColumnsPresets;
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in LoadColumnsPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        public void SaveColumnsPresets()
        {
            if (_selectedColumns == null || _selectedColumns.Count <= 0)
                return;
            try
            {
                if (_columnsPresets != null && _columnsPresets.Count > 0)
                {
                    string jsonString = JsonSerializer.Serialize<List<JsonColumnsPreset>>(_columnsPresets, new JsonSerializerOptions() { WriteIndented = true });
                    File.WriteAllText(Utils.StructureColumnsPresetsFilePath(), jsonString, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in SaveColumnsPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        [RelayCommand]
        private void ColumnsPresetSelect(JsonColumnsPreset? preset)
        {
            _selectedColumnsPreset = null;
            tb_ExistingColumnsPreset.Text = ASILang.Get("ClickHere");
            btn_LoadColumnsPreset.IsEnabled = false;
            btn_RemoveColumnsPreset.IsEnabled = false;

            if (preset == null)
                return;

            _selectedColumnsPreset = preset;
            tb_ExistingColumnsPreset.Text = preset.Name;
            btn_LoadColumnsPreset.IsEnabled = true;
            btn_RemoveColumnsPreset.IsEnabled = (string.Compare(preset.Name, ASILang.Get("DefaultPreset"), StringComparison.InvariantCulture) != 0);
        }

        private void FillColumnsPresetsDropDown()
        {
            mf_ExistingColumnsPresets.Items.Clear();
            mf_ExistingColumnsPresets.Items.Add(new MenuFlyoutItem
            {
                Text = ASILang.Get("DefaultPreset"),
                Command = ColumnsPresetSelectCommand,
                CommandParameter = _defaultColumnsPreset
            });
            foreach (var preset in _columnsPresets)
                if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Columns != null && preset.Columns.Count > 0)
                {
                    mf_ExistingColumnsPresets.Items.Add(new MenuFlyoutItem
                    {
                        Text = preset.Name,
                        Command = ColumnsPresetSelectCommand,
                        CommandParameter = preset
                    });
                }
        }

        private void btn_ColumnsPresets_Click(object sender, RoutedEventArgs e)
        {
            if (ColumnsPresetsPopup.IsOpen)
                return;

            tb_ColumnsPresetName.Text = "";
            _selectedColumnsPreset = null;
            tb_ExistingColumnsPreset.Text = ASILang.Get("ClickHere");
            btn_LoadColumnsPreset.IsEnabled = false;
            btn_RemoveColumnsPreset.IsEnabled = false;

            LoadColumnsPresets();
            FillColumnsPresetsDropDown();

            ColumnsPresetsPopup.IsOpen = true;
        }

        private void btn_CloseColumnsPresetsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ColumnsPresetsPopup.IsOpen)
            {
                ColumnsPresetsPopup.IsOpen = false;
                RefreshDisplayedColumns();
            }
        }

        private void btn_SaveCurrentColumns_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tb_ColumnsPresetName.Text))
            {
                MainWindow.ShowToast(ASILang.Get("PresetNeedsName"), BackgroundColor.ERROR);
                return;
            }
            if (string.Compare(tb_ColumnsPresetName.Text, ASILang.Get("DefaultPreset"), StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                MainWindow.ShowToast(ASILang.Get("PresetNameAlreadyExists"), BackgroundColor.ERROR);
                return;
            }

            foreach (var preset in _columnsPresets)
                if (preset != null && preset.Name != null && string.Compare(preset.Name, tb_ColumnsPresetName.Text, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    MainWindow.ShowToast(ASILang.Get("PresetNameAlreadyExists"), BackgroundColor.ERROR);
                    return;
                }

            try
            {
                List<string> columns = new List<string>();
                foreach (var column in _selectedColumns)
                    if (column != null)
                        columns.Add(column);
                if (columns.Count > 0)
                {
                    _selectedColumnsPreset = null;
                    tb_ExistingColumnsPreset.Text = ASILang.Get("ClickHere");
                    btn_LoadColumnsPreset.IsEnabled = false;
                    btn_RemoveColumnsPreset.IsEnabled = false;

                    JsonColumnsPreset newPreset = new JsonColumnsPreset()
                    {
                        Name = tb_ColumnsPresetName.Text,
                        Columns = columns,
                    };
                    _columnsPresets.Add(newPreset);
                    mf_ExistingColumnsPresets.Items.Add(new MenuFlyoutItem
                    {
                        Text = newPreset.Name,
                        Command = ColumnsPresetSelectCommand,
                        CommandParameter = newPreset
                    });
                }
                SaveColumnsPresets();
                MainWindow.ShowToast(ASILang.Get("PresetSaved"), BackgroundColor.SUCCESS);
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in btn_SaveCurrentColumns_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void btn_LoadColumnsPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColumnsPreset == null || _selectedColumnsPreset.Columns == null || _selectedColumnsPreset.Columns.Count <= 0)
            {
                MainWindow.ShowToast(ASILang.Get("ColumnsPresetIsEmpty"), BackgroundColor.WARNING);
                return;
            }

            _selectedColumns.Clear();
            foreach (var column in _selectedColumnsPreset.Columns)
                if (column != null)
                    _selectedColumns.Add(column);
            MainWindow.ShowToast(ASILang.Get("ColumnsPresetLoaded"), BackgroundColor.SUCCESS);
        }

        private void btn_RemoveColumnsPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColumnsPreset == null || string.IsNullOrEmpty(_selectedColumnsPreset.Name))
            {
                MainWindow.ShowToast(ASILang.Get("NoColumnsPresetSelected"), BackgroundColor.WARNING);
                return;
            }

            if (_columnsPresets != null && _columnsPresets.Count > 0)
            {
                int toDel = -1;
                for (int i = 0; i < _columnsPresets.Count; i++)
                    if (_columnsPresets[i] != null && string.Compare(_columnsPresets[i].Name, _selectedColumnsPreset.Name, StringComparison.InvariantCulture) == 0)
                    {
                        toDel = i;
                        break;
                    }

                if (toDel >= 0)
                {
                    _selectedColumnsPreset = null;
                    tb_ExistingColumnsPreset.Text = ASILang.Get("ClickHere");
                    btn_LoadColumnsPreset.IsEnabled = false;
                    btn_RemoveColumnsPreset.IsEnabled = false;

                    _columnsPresets.RemoveAt(toDel);
                    SaveColumnsPresets();
                    FillColumnsPresetsDropDown();
                }
                else
                    MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", $"\"{_selectedColumnsPreset.Name}\"", StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
            }
        }

        private void tb_ColumnsPresetName_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_SaveCurrentColumns.IsEnabled = (tb_ColumnsPresetName.Text.Length > 0);
        }

        private void mfi_DefaultColumnsPreset_Click(object sender, RoutedEventArgs e)
        {
            tb_ExistingColumnsPreset.Text = ASILang.Get("DefaultPreset");
            btn_LoadColumnsPreset.IsEnabled = true;
            btn_RemoveColumnsPreset.IsEnabled = false;
            _selectedColumnsPreset = _defaultColumnsPreset;
        }

        #endregion

        #region Group presets

        public void LoadGroupPresets()
        {
            _groupPresets.Clear();

            string groupPresetsPath = Utils.StructureGroupsPresetsFilePath();
            if (!File.Exists(groupPresetsPath))
                return;

            string groupPresetsJson = File.ReadAllText(groupPresetsPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(groupPresetsJson))
                return;

            try
            {
                List<JsonGroupPreset>? jsonGroupPresets = JsonSerializer.Deserialize<List<JsonGroupPreset>>(groupPresetsJson);
                if (jsonGroupPresets != null && jsonGroupPresets.Count > 0)
                    _groupPresets = jsonGroupPresets;
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in LoadGroupPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void SaveGroupPresets()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize<List<JsonGroupPreset>>(_groupPresets, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.StructureGroupsPresetsFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in SaveGroupPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        [RelayCommand]
        private void GroupPresetSelect(JsonGroupPreset? preset)
        {
            _selectedGroupPreset = null;
            tb_ExistingGroupPreset.Text = ASILang.Get("ClickHere");
            btn_LoadGroupPreset.IsEnabled = false;
            btn_RemoveGroupPreset.IsEnabled = false;

            if (preset == null)
                return;

            _selectedGroupPreset = preset;
            tb_ExistingGroupPreset.Text = preset.Name;
            btn_LoadGroupPreset.IsEnabled = true;
            btn_RemoveGroupPreset.IsEnabled = true;
        }

        private void FillGroupPresetsDropDown()
        {
            mf_ExistingGroupPresets.Items.Clear();
            foreach (var preset in _groupPresets)
                if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.FiltersPresets != null && preset.FiltersPresets.Count > 0)
                {
                    mf_ExistingGroupPresets.Items.Add(new MenuFlyoutItem
                    {
                        Text = preset.Name,
                        Command = GroupPresetSelectCommand,
                        CommandParameter = preset
                    });
                }
        }

        private void btn_SaveCurrentGroup_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tb_GroupPresetName.Text))
            {
                MainWindow.ShowToast(ASILang.Get("PresetNeedsName"), BackgroundColor.ERROR);
                return;
            }

            foreach (var preset in _groupPresets)
                if (preset != null && preset.Name != null && string.Compare(preset.Name, tb_GroupPresetName.Text, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    MainWindow.ShowToast(ASILang.Get("PresetNameAlreadyExists"), BackgroundColor.ERROR);
                    return;
                }

            try
            {
                List<KeyValuePair<FilterOperator, string>> group = new List<KeyValuePair<FilterOperator, string>>();
                foreach (var g in _group)
                    if (g.Key != FilterOperator.NONE && g.Value != null && !string.IsNullOrEmpty(g.Value.Name))
                        group.Add(new KeyValuePair<FilterOperator, string>(g.Key, g.Value.Name));
                if (group.Count > 0)
                {
                    _selectedGroupPreset = null;
                    tb_ExistingGroupPreset.Text = ASILang.Get("ClickHere");
                    btn_LoadGroupPreset.IsEnabled = false;
                    btn_RemoveGroupPreset.IsEnabled = false;

                    JsonGroupPreset newPreset = new JsonGroupPreset()
                    {
                        Name = tb_GroupPresetName.Text,
                        FiltersPresets = group,
                    };
                    _groupPresets.Add(newPreset);
                    mf_ExistingGroupPresets.Items.Add(new MenuFlyoutItem
                    {
                        Text = newPreset.Name,
                        Command = GroupPresetSelectCommand,
                        CommandParameter = newPreset
                    });
                }
                SaveGroupPresets();
                FillGroupPresetsDropDown();
                MainWindow.ShowToast(ASILang.Get("PresetSaved"), BackgroundColor.SUCCESS);
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in btn_SaveCurrentGroup_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void btn_GroupPresets_Click(object sender, RoutedEventArgs e)
        {
            if (GroupPresetsPopup.IsOpen)
                return;

            tb_GroupPresetName.Text = "";
            _selectedGroupPreset = null;
            tb_ExistingGroupPreset.Text = ASILang.Get("ClickHere");
            btn_LoadGroupPreset.IsEnabled = false;
            btn_RemoveGroupPreset.IsEnabled = false;

            LoadFiltersPresets();
            LoadGroupPresets();
            FillGroupPresetsDropDown();

            GroupPresetsPopup.IsOpen = true;
        }

        private void btn_LoadGroupPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGroupPreset == null || _selectedGroupPreset.FiltersPresets == null || _selectedGroupPreset.FiltersPresets.Count <= 0)
            {
                MainWindow.ShowToast(ASILang.Get("GroupPresetIsEmpty"), BackgroundColor.WARNING);
                return;
            }

            Type type = typeof(Structure);
            _group.Clear();
            foreach (var filter in _selectedGroupPreset.FiltersPresets)
                if (filter.Key != FilterOperator.NONE && !string.IsNullOrEmpty(filter.Value))
                    foreach (var filtersPreset in _filtersPresets)
                        if (filtersPreset != null && string.Compare(filter.Value, filtersPreset.Name, StringComparison.InvariantCulture) == 0)
                        {
                            _group.Add(new KeyValuePair<FilterOperator, JsonFiltersPreset>(filter.Key, filtersPreset));
                            break;
                        }
            MainWindow.ShowToast(ASILang.Get("GroupPresetLoaded"), BackgroundColor.SUCCESS);
            ApplyFiltersAndSort();
        }

        private void btn_RemoveGroupPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGroupPreset == null || string.IsNullOrEmpty(_selectedGroupPreset.Name))
            {
                MainWindow.ShowToast(ASILang.Get("NoGroupPresetSelected"), BackgroundColor.WARNING);
                return;
            }

            if (_groupPresets != null && _groupPresets.Count > 0)
            {
                int toDel = -1;
                for (int i = 0; i < _groupPresets.Count; i++)
                    if (_groupPresets[i] != null && string.Compare(_groupPresets[i].Name, _selectedGroupPreset.Name, StringComparison.InvariantCulture) == 0)
                    {
                        toDel = i;
                        break;
                    }

                if (toDel >= 0)
                {
                    _selectedGroupPreset = null;
                    tb_ExistingGroupPreset.Text = ASILang.Get("ClickHere");
                    btn_LoadGroupPreset.IsEnabled = false;
                    btn_RemoveGroupPreset.IsEnabled = false;

                    _groupPresets.RemoveAt(toDel);
                    SaveGroupPresets();
                    FillGroupPresetsDropDown();
                }
                else
                    MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", $"\"{_selectedGroupPreset.Name}\"", StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
            }
        }

        private void tb_GroupPresetName_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_SaveCurrentGroup.IsEnabled = (tb_GroupPresetName.Text.Length > 0);
        }

        private void btn_CloseGroupPresetsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (GroupPresetsPopup.IsOpen)
                GroupPresetsPopup.IsOpen = false;
        }

        #endregion

        #region DataGrid context menu

        private void mfi_contextMenuGetID_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Structure? structure = (mfi.DataContext as Structure);
                    if (structure != null && structure.StructureID != null && structure.StructureID.HasValue)
                        clipboardStr = $"{structure.StructureID.Value.ToString(CultureInfo.InvariantCulture)}";
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetID_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
                Utils.AddToClipboard(clipboardStr, false);
            }
        }

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

        private void mfi_contextMenuGetJson_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Structure? structure = (mfi.DataContext as Structure);
                    if (structure != null)
                        clipboardStr = JsonSerializer.Serialize<Structure>(structure, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetJson_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
                Utils.AddToClipboard(clipboardStr, false);
            }
        }

        private void mfi_contextMenuGetAllJson_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                var selectedStructures = dg_Structures.SelectedItems;
                if (selectedStructures != null && selectedStructures.Count > 0)
                {
                    List<Structure>? structures = new List<Structure>();
                    for (int i = 0; i < selectedStructures.Count; i++)
                    {
                        Structure? structure = (selectedStructures[i] as Structure);
                        if (structure != null)
                            structures.Add(structure);
                    }
                    if (structures.Count > 0)
                    {
                        clipboardStr = JsonSerializer.Serialize<List<Structure>>(structures, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                        structures.Clear();
                        structures = null;
                    }
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetAllJson_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
                Utils.AddToClipboard(clipboardStr, false);
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
                    PropertyCleanName = DinoUtils.GetCleanNameFromPropertyName("TargetingTeam"),
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
            if (SettingsPage._legacySearch != null && SettingsPage._legacySearch.HasValue && SettingsPage._legacySearch.Value)
            {
                RemoveAllFiltersForProp(_targetingTeamProp);
                if (SettingsPage._allTribesForStructures.ContainsKey(tribeName))
                    _filters.Add(new KeyValuePair<PropertyInfo, Filter>(_targetingTeamProp, new Filter()
                    {
                        FilterOperator = FilterOperator.AND,
                        FilterType = FilterType.EXACT_MATCH,
                        FilterValues = new List<string>() { SettingsPage._allTribesForStructures[tribeName].ToString(CultureInfo.InvariantCulture) }
                    }));
                QuickFilter_Tribe_Label = tribeName;
                FillEditStructureFiltersPopup();
            }
            else
            {
                _quickFilter_SelectedTribe = string.Compare(tribeName, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) == 0 ? null : tribeName;
                SetupQuickFilter();
            }
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
            if (SettingsPage._legacySearch != null && SettingsPage._legacySearch.HasValue && SettingsPage._legacySearch.Value)
            {
                RemoveAllFiltersForProp(_shortNameProp);
                if (SettingsPage._allShortNamesForStructuresSorted.Contains(shortName))
                    _filters.Add(new KeyValuePair<PropertyInfo, Filter>(_shortNameProp, new Filter()
                    {
                        FilterOperator = FilterOperator.AND,
                        FilterType = FilterType.EXACT_MATCH,
                        FilterValues = new List<string>() { shortName }
                    }));
                QuickFilter_Structure_Label = shortName;
                FillEditStructureFiltersPopup();
            }
            else
            {
                _quickFilter_SelectedStructure = string.Compare(shortName, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) == 0 ? null : shortName;
                SetupQuickFilter();
            }
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

        #region New search method

        private void btn_CreateFilter_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBuilder._searchBuilder != null)
            {
                SearchBuilder._searchBuilder.Show();
                SearchBuilder._searchBuilder.Activate();
            }
            else
            {
                var s = new SearchBuilder();
                s.Initialize(SearchType.STRUCTURES);
                s.Show();
            }
        }

        private void cbb_ExistingQueries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(cbb_ExistingQueries, false);
            if (!string.IsNullOrWhiteSpace(selected) && SearchBuilder._queries != null && SearchBuilder._queries.Queries != null && SearchBuilder._queries.Queries.ContainsKey(selected))
            {
                btn_LoadQuery.IsEnabled = true;
                btn_EditQuery.IsEnabled = true;
                btn_DeleteQuery.IsEnabled = true;
            }
            else
            {
                btn_LoadQuery.IsEnabled = false;
                btn_EditQuery.IsEnabled = false;
                btn_DeleteQuery.IsEnabled = false;
            }
        }

        private void btn_LoadQuery_Click(object sender, RoutedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(cbb_ExistingQueries, false);
            if (string.IsNullOrWhiteSpace(selected))
            {
                MainWindow.ShowToast(ASILang.Get("NoFilterSelected"), BackgroundColor.WARNING);
                return;
            }
            if (SearchBuilder._queries != null && SearchBuilder._queries.Queries != null && SearchBuilder._queries.Queries.ContainsKey(selected) && SearchBuilder._queries.Queries[selected] != null)
            {
                SearchBuilder._query = SearchBuilder._queries.Queries[selected];
                ApplyFiltersAndSort();
                MainWindow.ShowToast(ASILang.Get("FilterLoaded"), BackgroundColor.SUCCESS);
            }
            else
            {
                MainWindow.ShowToast(ASILang.Get("FilterNotFound"), BackgroundColor.WARNING);
                return;
            }
        }

        public void UpdateSavedSearchQueriesDropdown()
        {
            cbb_ExistingQueries.Items.Clear();
            if (SearchBuilder._queries != null && SearchBuilder._queries.Queries != null && SearchBuilder._queries.Queries.Count > 0)
                foreach (var q in SearchBuilder._queries.Queries)
                    cbb_ExistingQueries.Items.Add(q.Key);
        }

        private void btn_DeleteQuery_Click(object sender, RoutedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(cbb_ExistingQueries, false);
            if (!string.IsNullOrWhiteSpace(selected) && SearchBuilder._queries != null && SearchBuilder._queries.Queries != null && SearchBuilder._queries.Queries.ContainsKey(selected))
            {
                SearchBuilder._queries.Queries.Remove(selected);
                UpdateSavedSearchQueriesDropdown();
                if (SearchBuilder.SaveSearchQueries())
                    MainWindow.ShowToast($"{ASILang.Get("FilterSaved")}", BackgroundColor.SUCCESS);
                else
                    MainWindow.ShowToast($"{ASILang.Get("SaveFilterFailed")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
            }
            else
                MainWindow.ShowToast(ASILang.Get("FilterNotFound"), BackgroundColor.WARNING);
        }

        private void btn_EditQuery_Click(object sender, RoutedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(cbb_ExistingQueries, false);
            if (!string.IsNullOrWhiteSpace(selected) && SearchBuilder._queries != null && SearchBuilder._queries.Queries != null && SearchBuilder._queries.Queries.ContainsKey(selected))
            {
                if (EditSearchQuery._editSearchQuery != null)
                {
                    EditSearchQuery._editSearchQuery.Initialize(selected, SearchBuilder._queries.Queries[selected], true);
                    EditSearchQuery._editSearchQuery.Show();
                    EditSearchQuery._editSearchQuery.Activate();
                }
                else
                {
                    var s = new EditSearchQuery();
                    s.Initialize(selected, SearchBuilder._queries.Queries[selected], true);
                    s.Show();
                }
            }
        }

        #endregion
    }
}
