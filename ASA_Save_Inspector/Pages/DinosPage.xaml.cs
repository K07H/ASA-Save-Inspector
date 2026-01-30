using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public sealed partial class DinosPage : Page, INotifyPropertyChanged
    {
        #region Constants

        private const double _scrollBarWidth = 24.0d;
        private const int MAX_PROPERTY_VALUES = 300;

        #endregion

        #region Statics

        private static readonly object lockObject = new object();

        public static DinosPage? _page = null;

        private static string? _currentSort = null;
        private static bool _ascendingSort = true;
        private static string? _secondaryCurrentSort = null;
        private static bool _secondaryAscendingSort = true;

        //private static bool _addedDefaultFilters = false;
        private static List<KeyValuePair<PropertyInfo, Filter>> _filters = new List<KeyValuePair<PropertyInfo, Filter>>();

        private static bool _setDefaultSelectedColumns = false;
        private static List<string> _selectedColumns = new List<string>();

        private static List<KeyValuePair<FilterOperator, JsonFiltersPreset>> _group = new List<KeyValuePair<FilterOperator, JsonFiltersPreset>>();

        private static JsonFiltersPreset _defaultFiltersPreset = new JsonFiltersPreset() { Name = ASILang.Get("DefaultPreset"), Filters = new List<JsonFilter>() };

        private static JsonColumnsPreset _defaultColumnsPreset = new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>(DinoUtils.DefaultSelectedColumns) };

        // Map name, save file datetime and in-game datetime.
        public static string MapName => (SettingsPage._currentlyLoadedMapName ?? ASILang.Get("Unknown"));
        public static string SaveGameDatetime => (Utils.GetSaveFileDateTimeStr() ?? ASILang.Get("UnknownDate"));
        public static string InGameDatetime => Utils.GetInGameDateTimeStr();

        #endregion

        #region Properties

        public IEnumerable<Dino>? _lastDisplayed = null;
        public ObservableCollection<Dino>? _lastDisplayedVM = null;
        private string? _selectedDinoFilter_Name = null;
        private List<string>? _selectedDinoFilter_Values = new List<string>();

        private List<JsonFiltersPreset> _filtersPresets = new List<JsonFiltersPreset>();
        private JsonFiltersPreset? _selectedFiltersPreset = null;

        private List<JsonColumnsPreset> _columnsPresets = new List<JsonColumnsPreset>();
        private JsonColumnsPreset? _selectedColumnsPreset = null;

        private List<JsonGroupPreset> _groupPresets = new List<JsonGroupPreset>();
        private JsonGroupPreset? _selectedGroupPreset = null;

        private List<string> _propertiesWithManyValues = new List<string>(DinoUtils.DoNotCheckPropertyValuesAmount);

        private ObservableCollection<string> _quickFilter_allTribes = new ObservableCollection<string>() { ASILang.Get("ClickHere") };
        private ObservableCollection<string> _quickFilter_allCleanNames = new ObservableCollection<string>() { ASILang.Get("ClickHere") };

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

        private string _quickFilter_Dino_Label = ASILang.Get("Initializing");
        public string QuickFilter_Dino_Label
        {
            get { return _quickFilter_Dino_Label; }
            set { _quickFilter_Dino_Label = value; OnPropertyChanged(); }
        }

        #endregion

        #region Constructor/Destructor

        public DinosPage()
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
                btn_EditQuery.Visibility = Visibility.Collapsed;
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
                btn_EditQuery.Visibility = Visibility.Visible;
                btn_DeleteQuery.Visibility = Visibility.Visible;
            }
            SearchBuilder.InitSearch(SearchType.DINOS);

            // Init default presets.
            //InitDefaultPresets();

            // Set default selected columns.
            if (!_setDefaultSelectedColumns)
            {
                LoadColumnsPresets();
                bool fallbackToDefault = true;
                if (!string.IsNullOrEmpty(SettingsPage._defaultColumnsPreset_Dinos))
                    foreach (var preset in _columnsPresets)
                        if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Columns != null && preset.Columns.Count > 0)
                            if (string.Compare(SettingsPage._defaultColumnsPreset_Dinos, preset.Name, StringComparison.InvariantCulture) == 0)
                            {
                                fallbackToDefault = false;
                                foreach (var c in preset.Columns)
                                    _selectedColumns.Add(c);
                                break;
                            }
                if (fallbackToDefault && _selectedColumns != null && DinoUtils.DefaultSelectedColumns != null && DinoUtils.DefaultSelectedColumns.Count > 0)
                    foreach (string c in DinoUtils.DefaultSelectedColumns)
                        _selectedColumns.Add(c);
                _setDefaultSelectedColumns = true;
            }

            // Grab dinos data from settings if not set.
            if (_lastDisplayed == null)
                _lastDisplayed = SettingsPage._dinosData;

            InitTribesQuickFilter();
            InitDinosQuickFilter();

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
                if (_page.dg_Dinos != null)
                {
                    if (_page.dg_Dinos.ItemsSource != null)
                        _page.dg_Dinos.ItemsSource = null;
                    if (gr_Main != null)
                        gr_Main.Children.Remove(_page.dg_Dinos);
                }
                if (_page._selectedDinoFilter_Values != null)
                {
                    _page._selectedDinoFilter_Values.Clear();
                    _page._selectedDinoFilter_Values = null;
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

        /*
        private static void InitDefaultPresets()
        {
            _defaultColumnsPreset = new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>(DinoUtils.DefaultSelectedColumns) };

            _defaultFiltersPreset = new JsonFiltersPreset() { Name = ASILang.Get("DefaultPreset"), Filters = new List<JsonFilter>() };

            // Add "IsTamed => True" to default filters preset.
            PropertyInfo? isTamedProp = typeof(Dino).GetProperty("IsTamed", BindingFlags.Instance | BindingFlags.Public);
            Filter defaultFilter = new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.EXACT_MATCH, FilterValues = new List<string>() { "True" } };
            if (isTamedProp != null && _defaultFiltersPreset?.Filters != null)
            {
                _defaultFiltersPreset.Filters.Add(new JsonFilter()
                {
                    PropertyName = isTamedProp.Name,
                    Filter = defaultFilter
                });
            }

            // Add "IsTamed => True" to current filters.
            if (!_addedDefaultFilters && _filters != null && isTamedProp != null)
            {
                _addedDefaultFilters = true;
                _filters.Add(new KeyValuePair<PropertyInfo, Filter>(isTamedProp, defaultFilter));
            }
        }
        */

        public bool GoToDino(int? id1, int? id2)
        {
            if (id1 != null && id1.HasValue && id2 != null && id2.HasValue && _lastDisplayedVM != null)
            {
                Dino? dino = _lastDisplayedVM.FirstOrDefault(d => (d?.DinoID1 == id1 && d?.DinoID2 == id2), null);
                if (dino == null) // Dino not found. That's probably due to current filters.
                {
                    ClearPageFiltersAndGroups(); // Remove current filtering & grouping.
                    ApplyFiltersAndSort(); // Refresh.
                    dino = _lastDisplayedVM.FirstOrDefault(d => (d?.DinoID1 == id1 && d?.DinoID2 == id2), null);
                }
                if (dino != null)
                {
                    dg_Dinos.SelectedItem = dino;
                    dg_Dinos.UpdateLayout();
                    dg_Dinos.ScrollIntoView(dg_Dinos.SelectedItem, null);
                    return true;
                }
            }
            return false;
        }

        private bool LastDinoDoubleTap(MapPoint? point)
        {
            if (point == null || _lastDisplayedVM == null || string.IsNullOrWhiteSpace(point.ID) || point.ID == "00")
                return false;

            Dino? dino = _lastDisplayedVM.FirstOrDefault(d => (string.Compare(point.ID, d?.GetDinoID(), StringComparison.InvariantCulture) == 0), null);
            if (dino != null)
            {
                dg_Dinos.SelectedItem = dino;
                dg_Dinos.UpdateLayout();
                dg_Dinos.ScrollIntoView(dg_Dinos.SelectedItem, null);
                return true;
            }
            return false;
        }

        private void Init(ref IEnumerable<Dino>? dinos, bool onlyRefresh)
        {
            dg_Dinos.MaxHeight = Math.Max(1.0d, gr_Main.ActualHeight - (tb_Title.ActualHeight + sp_SortAndFilters.ActualHeight + _scrollBarWidth + 4.0d));
            dg_Dinos.MaxWidth = Math.Max(1.0d, gr_Main.ActualWidth - (_scrollBarWidth + 4.0d));
            if (!onlyRefresh && dinos != null)
            {
                if (_lastDisplayedVM != null)
                {
                    _lastDisplayedVM.Clear();
                    _lastDisplayedVM = null;
                }
                _lastDisplayedVM = new ObservableCollection<Dino>(dinos);
            }
            dg_Dinos.ItemsSource = _lastDisplayedVM;
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(250);
                ReorderColumns();
            });
            if (dinos != null && MainWindow._minimap != null)
                MainWindow.UpdateMinimap(dinos.Select(d =>
                {
                    string dinoIDStr = d.GetDinoID();
                    KeyValuePair<double, double> minimapCoords = new KeyValuePair<double, double>(0.0d, 0.0d);
                    if (d.bIsCryopodded != null && d.bIsCryopodded.HasValue && d.bIsCryopodded.Value)
                    {
                        var cryopodPos = Utils.GetCryopodCoordsByUUID(d.CryopodUUID);
                        minimapCoords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, cryopodPos.Item1, cryopodPos.Item2, cryopodPos.Item3);
                    }
                    else
                        minimapCoords = d.GetASIMinimapCoords();
                    return new MapPoint()
                    {
                        ID = dinoIDStr,
                        Name = d.CleanName,
                        Description = $"{(dinoIDStr != "00" ? $"{ASILang.Get("ID")}: {dinoIDStr}\n" : string.Empty)}{(!string.IsNullOrWhiteSpace(d.TamedName) ? $"{ASILang.Get("Name")}: {d.TamedName}\n" : string.Empty)}{ASILang.Get("Level")}: {(d.CurrentLevel != null && d.CurrentLevel.HasValue ? d.CurrentLevel.Value.ToString(CultureInfo.InvariantCulture) : "0")}",
                        X = minimapCoords.Value,
                        Y = minimapCoords.Key
                    };
                }), LastDinoDoubleTap);
        }

        private void ReorderColumns()
        {
            if (dg_Dinos?.Columns != null && dg_Dinos.Columns.Count > 0)
            {
                List<string?> orders = new List<string?>();
                foreach (DataGridColumn col in dg_Dinos.Columns)
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
                foreach (string defaultOrder in DinoUtils.DefaultColumnsOrder)
                    if (orders.Contains(defaultOrder) && !defaultOrders.Contains(defaultOrder))
                        defaultOrders.Add(defaultOrder);

                // Add remaining unspecified order.
                foreach (string? otherOrder in orders)
                    if (!defaultOrders.Contains(otherOrder))
                        defaultOrders.Add(otherOrder);

                int j = 0;
                for (int i = 0; i < defaultOrders.Count; i++)
                {
                    foreach (DataGridColumn col in dg_Dinos.Columns)
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
            if (dg_Dinos?.Columns == null || dg_Dinos.Columns.Count <= 0)
                return;
            List<ColumnOrder> order = new List<ColumnOrder>();
            foreach (DataGridColumn col in dg_Dinos.Columns)
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
                File.WriteAllText(Utils.DinoColumnsOrderFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in SaveColumnsOrder. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private List<ColumnOrder>? LoadColumnsOrder()
        {
            string columnsOrderFilepath = Utils.DinoColumnsOrderFilePath();
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
                MainWindow._mainWindow.NavView_Navigate(typeof(DinosPage), new EntranceNavigationTransitionInfo());
            });
#pragma warning restore CS1998
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<Dino>? filtered = null;
            if (SettingsPage._legacySearch != null && SettingsPage._legacySearch.HasValue && SettingsPage._legacySearch.Value)
            {
                if (_group != null && _group.Count > 0)
                    filtered = ApplyGroupFiltering();
                else
                    filtered = ApplyFiltering(SettingsPage._dinosData, _filters);
            }
            else
            {
                if (SearchBuilder._query == null || SearchBuilder._query.Parts == null || SearchBuilder._query.Parts.Count <= 0)
                    filtered = SettingsPage._dinosData;
                else
                    filtered = SearchBuilder.DoSearchQuery(SearchType.DINOS, true) as IEnumerable<Dino>;
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

        //private void dg_Dinos_LayoutUpdated(object sender, object e) => AdjustColumnSizes();

        private void dg_Dinos_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            run_NbLinesSelected.Text = (dg_Dinos.SelectedItems != null ? dg_Dinos.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) : "0");
            mfi_contextMenuGetAllJson.Visibility = (dg_Dinos.SelectedItems != null && dg_Dinos.SelectedItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed);

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

        private void dg_Dinos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            run_NbLinesSelected.Text = (dg_Dinos.SelectedItems != null ? dg_Dinos.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) : "0");
        }

        private void dg_Dinos_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (!_selectedColumns.Contains((string)e.Column.Header))
                e.Cancel = true; //e.Column.Visibility = Visibility.Collapsed;
            e.Column.Header = DinoUtils.GetCleanNameFromPropertyName(e.PropertyName);
        }

        private void dg_Dinos_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
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
                    if (d.bIsCryopodded != null && d.bIsCryopodded.HasValue && d.bIsCryopodded.Value)
                    {
                        var cryopodPos = Utils.GetCryopodCoordsByUUID(d.CryopodUUID);
                        minimapCoords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, cryopodPos.Item1, cryopodPos.Item2, cryopodPos.Item3);
                    }
                    else
                        minimapCoords = d.GetASIMinimapCoords();
                    double x = minimapCoords.Value;
                    double y = minimapCoords.Key;
                    Minimap.ShowCallout(dinoIDStr, x, y);
                }
            }
        }

        private void dg_Dinos_ColumnReordered(object sender, DataGridColumnEventArgs e) => SaveColumnsOrder();

        #endregion

        #region Sorting

        private object? GetDinoPropertyValueByName(string cleanName, Dino d)
        {
            PropertyInfo? prop = Utils.GetProperty(typeof(Dino), DinoUtils.GetPropertyNameFromCleanName(cleanName));
            if (prop == null)
                return null;
            return prop.GetValue(d);
        }

        private void SimpleSort(ref IEnumerable<Dino> dinos, string cleanName)
        {
            if (AscendingSort)
                dinos = dinos.OrderBy(o => GetDinoPropertyValueByName(cleanName, o));
            else
                dinos = dinos.OrderByDescending(o => GetDinoPropertyValueByName(cleanName, o));
        }

#pragma warning disable CS8604
        private void DoubleSort(ref IEnumerable<Dino> dinos, string cleanName)
        {
            if (AscendingSort)
            {
                if (SecondaryAscendingSort)
                    dinos = dinos.OrderBy(o => GetDinoPropertyValueByName(cleanName, o)).ThenBy(o => GetDinoPropertyValueByName(SecondaryCurrentSort, o));
                else
                    dinos = dinos.OrderBy(o => GetDinoPropertyValueByName(cleanName, o)).ThenByDescending(o => GetDinoPropertyValueByName(SecondaryCurrentSort, o));
            }
            else
            {
                if (SecondaryAscendingSort)
                    dinos = dinos.OrderByDescending(o => GetDinoPropertyValueByName(cleanName, o)).ThenBy(o => GetDinoPropertyValueByName(SecondaryCurrentSort, o));
                else
                    dinos = dinos.OrderByDescending(o => GetDinoPropertyValueByName(cleanName, o)).ThenByDescending(o => GetDinoPropertyValueByName(SecondaryCurrentSort, o));
            }
        }
#pragma warning restore CS8604

        private void SortDataGrid(ref IEnumerable<Dino> dinos, string cleanName)
        {
            if (dinos == null)
                return;

            if (!string.IsNullOrWhiteSpace(CurrentSort) && SecondaryCurrentSort != null)
                DoubleSort(ref dinos, cleanName);
            else
                SimpleSort(ref dinos, cleanName);
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

        private void ApplySorting(ref IEnumerable<Dino> dinos)
        {
            if (CurrentSort != null)
            {
                SortDataGrid(ref dinos, CurrentSort);
                RefreshPrimarySortLabel();
                tb_PrimarySort.Visibility = Visibility.Visible;
                if (SecondaryCurrentSort != null)
                {
                    RefreshSecondarySortLabel();
                    tb_SecondarySort.Visibility = Visibility.Visible;
                }
            }
        }

        private void dg_Dinos_Sorting(object sender, DataGridColumnEventArgs e)
        {
            if (e?.Column?.Header != null)
            {
                string? cleanName = e.Column.Header.ToString();
                if (SettingsPage._dinosData != null && cleanName != null)
                {
                    SetSorting(cleanName);
                    ApplyFiltersAndSort();
                }
            }
        }

        #endregion

        #region Filtering

        private void RefreshSelectedDinoFilterValues()
        {
            tb_dinoFilterValues.Text = (_selectedDinoFilter_Values != null ? string.Join(", ", _selectedDinoFilter_Values) : string.Empty);
            b_dinoFilterValues.Visibility = (tb_dinoFilterValues.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed);
        }

        [RelayCommand]
        private void DinoFilterValueSelect(string filterValue)
        {
            if (!string.IsNullOrWhiteSpace(filterValue) && _selectedDinoFilter_Values != null)
            {
                if (_selectedDinoFilter_Values.Contains(filterValue))
                    _selectedDinoFilter_Values.Remove(filterValue);
                else
                    _selectedDinoFilter_Values.Add(filterValue);
                RefreshSelectedDinoFilterValues();
                btn_AddToDinoFilters.IsEnabled = (_selectedDinoFilter_Values.Count > 0);
            }
        }

        [RelayCommand]
        private void DinoFilterSelect(string propName)
        {
            if (!string.IsNullOrWhiteSpace(propName))
            {
                _selectedDinoFilter_Name = propName;
                tb_DinoFilterName.Text = DinoUtils.GetCleanNameFromPropertyName(_selectedDinoFilter_Name);

                tb_DinoFilterType.Text = ASILang.Get("ClickHere");
                sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
                sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
                sp_FilterByOther.Visibility = Visibility.Collapsed;

                ResetFiltersValues();

                PropertyInfo? foundProperty = Utils.GetProperty(typeof(Dino), propName);
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
            btn_AddToDinoFilters.IsEnabled = false;

            tb_FilterByOther.Text = "";

            if (_selectedDinoFilter_Values != null)
                _selectedDinoFilter_Values.Clear();
            RefreshSelectedDinoFilterValues();
        }

        private void ResetFilters()
        {
            mf_DinoFilterName.Items.Clear();
            _selectedDinoFilter_Name = null;
            tb_DinoFilterOperator.Text = ASILang.Get("ClickHere");
            tb_DinoFilterName.Text = ASILang.Get("ClickHere");
            tb_DinoFilterType.Text = ASILang.Get("ClickHere");

            ResetFiltersValues();
        }

        private void FillPropertiesDropDown()
        {
            var dinoProperties = typeof(Dino).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (dinoProperties != null && dinoProperties.Count() > 0)
            {
                Dictionary<string, string> propNames = new Dictionary<string, string>();
                List<string> toAdd = new List<string>();
                foreach (var dinoProperty in dinoProperties)
                {
                    // Check amount of values for current property (to know if ExactMatch filter can be used or not).
                    if (Utils.DoCheckForPropertyValuesAmount)
                        if (!DinoUtils.DoNotCheckPropertyValuesAmount.Contains(dinoProperty.Name))
                        {
                            if (Utils.PropertyHasMoreValuesThan(SettingsPage._dinosData, dinoProperty, MAX_PROPERTY_VALUES))
                            {
#if DEBUG
                                Logger.Instance.Log($"Found property with many values: {dinoProperty.Name}", Logger.LogLevel.DEBUG);
#endif
                                if (!_propertiesWithManyValues.Contains(dinoProperty.Name))
                                    _propertiesWithManyValues.Add(dinoProperty.Name);
                            }
#if DEBUG
                            else if (dinoProperty.Name.Contains("Time", StringComparison.InvariantCultureIgnoreCase))
                                Logger.Instance.Log($"Found property with \"time\": {dinoProperty.Name}", Logger.LogLevel.DEBUG);
#endif
                        }
                    // Add current property.
                    string propName = dinoProperty.Name;
                    if (propName != null)
                    {
                        string? cleanName = DinoUtils.GetCleanNameFromPropertyName(propName);
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
                            mf_DinoFilterName.Items.Add(new MenuFlyoutItem
                            {
                                Text = cleanName,
                                Command = DinoFilterSelectCommand,
                                CommandParameter = propNames[cleanName]
                            });
                        }
                }
                propNames.Clear();
                toAdd.Clear();
            }
        }

        private bool CheckMatchFilter(Dino d, List<KeyValuePair<PropertyInfo, Filter>> filters)
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

        private void AddOrFilters(ref List<Expression<Func<Dino, bool>>> orFilters, List<KeyValuePair<PropertyInfo, Filter>> filters)
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

        private IEnumerable<Dino>? DoApplyGroupFiltering(IEnumerable<Dino>? filtered, JsonFiltersPreset preset)
        {
            if (filtered == null)
                return null;

            IEnumerable<Dino>? ret = null;
            if (preset.Filters != null && preset.Filters.Count > 0)
            {
                List<KeyValuePair<PropertyInfo, Filter>> currentFilters = new List<KeyValuePair<PropertyInfo, Filter>>();
                Type type = typeof(Dino);
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

        private IEnumerable<Dino>? ApplyGroupFiltering()
        {
            IEnumerable<Dino>? andFiltered = null;
            for (int i = 0; i < _group.Count; i++)
                if (_group[i].Key == FilterOperator.AND && _group[i].Value != null)
                {
                    if (andFiltered == null)
                        andFiltered = DoApplyGroupFiltering(SettingsPage._dinosData, _group[i].Value);
                    else
                        andFiltered = DoApplyGroupFiltering(andFiltered, _group[i].Value);
                }

            IEnumerable<Dino>? orFiltered = null;
            List<IEnumerable<Dino>?> orFiltereds = new List<IEnumerable<Dino>?>();
            for (int j = 0; j < _group.Count; j++)
                if (_group[j].Key == FilterOperator.OR && _group[j].Value != null)
                    orFiltereds.Add(DoApplyGroupFiltering(SettingsPage._dinosData, _group[j].Value));

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

        private IEnumerable<Dino>? ApplyFiltering(IEnumerable<Dino>? dinos, List<KeyValuePair<PropertyInfo, Filter>> filters)
        {
            if (dinos == null)
                return null;

            var orFilters = new List<Expression<Func<Dino, bool>>>();
            AddOrFilters(ref orFilters, filters);
            var lambda = AnyOf(orFilters.ToArray());

            return dinos.Where(lambda.Compile()).Where(a => CheckMatchFilter(a, filters));
        }

        public static void ClearPageFiltersAndGroups()
        {
            if (_page != null)
                _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    _page.sp_DinoFiltersPresetsInGroup.Children.Clear();
                    _page.sp_ExistingDinoFilters.Children.Clear();
                    _page.QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
                    _page.QuickFilter_Dino_Label = ASILang.Get("ClickHere");
                    _page._quickFilter_SelectedTribe = null;
                    _page._quickFilter_SelectedDino = null;
                });
            SearchBuilder._query = new SearchQuery();
            _group.Clear();
            _filters.Clear();
            //_addedDefaultFilters = false;
            //InitDefaultPresets();

            // TODO: Remove FillEditDinoFiltersPopup()?
            if (_page != null)
                _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => _page.FillEditDinoFiltersPopup());
        }

        private void FillEditDinoFiltersPopup()
        {
            sp_ExistingDinoFilters.Children.Clear();
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
                        Text = ASILang.Get("FilterBy").ToLowerInvariant(),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
                    };
                    TextBlock tb2 = new TextBlock()
                    {
                        FontSize = 16.0d,
                        TextWrapping = TextWrapping.Wrap,
                        Text = $"{DinoUtils.GetCleanNameFromPropertyName(filter.Key.Name)}",
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
                    sp_ExistingDinoFilters.Children.Add(grd);
                }
        }

        private void cb_IncludePropertiesWithManyValues_CheckedUnchecked(object sender, RoutedEventArgs e)
        {
            ResetFilters();
            FillPropertiesDropDown();
        }

        private void btn_AddFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!AddDinoFilterPopup.IsOpen)
            {
                ResetFilters();
                FillPropertiesDropDown();
                AddDinoFilterPopup.IsOpen = true;
            }
        }

        private void btn_CloseDinoFiltersPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddDinoFilterPopup.IsOpen)
                AddDinoFilterPopup.IsOpen = false;
        }

        private void btn_AddToDinoFilters_Click(object sender, RoutedEventArgs e)
        {
            FilterOperator filterOperator = FilterOperator.NONE;
            if (string.Compare(ASILang.Get("OperatorAND"), tb_DinoFilterOperator.Text, StringComparison.InvariantCulture) == 0)
                filterOperator = FilterOperator.AND;
            else if (string.Compare(ASILang.Get("OperatorOR"), tb_DinoFilterOperator.Text, StringComparison.InvariantCulture) == 0)
                filterOperator = FilterOperator.OR;
            if (filterOperator == FilterOperator.NONE)
            {
                MainWindow.ShowToast(ASILang.Get("MissingOperatorCannotAddFilter"), BackgroundColor.WARNING);
                return;
            }

            if (AddDinoFilterPopup.IsOpen)
            {
                AddDinoFilterPopup.IsOpen = false;
                if (!string.IsNullOrEmpty(_selectedDinoFilter_Name))
                {
                    PropertyInfo? prop = Utils.GetProperty(typeof(Dino), _selectedDinoFilter_Name);
                    if (prop != null)
                    {
                        if (tb_DinoFilterType.Text == ASILang.Get("FilterType_ExactMatch") &&
                            _selectedDinoFilter_Values != null &&
                            _selectedDinoFilter_Values.Count > 0)
                        {
                            _filters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, new Filter()
                            {
                                FilterOperator = filterOperator,
                                FilterType = FilterType.EXACT_MATCH,
                                FilterValues = new List<string>(_selectedDinoFilter_Values)
                            }));
                        }
                        else if (tb_FilterByOther.Text != null)
                        {
                            FilterType ft = FilterType.NONE;
                            if (tb_DinoFilterType.Text == ASILang.Get("FilterType_StartingWith"))
                                ft = FilterType.STARTING_WITH;
                            else if (tb_DinoFilterType.Text == ASILang.Get("FilterType_EndingWith"))
                                ft = FilterType.ENDING_WITH;
                            else if (tb_DinoFilterType.Text == ASILang.Get("FilterType_Containing"))
                                ft = FilterType.CONTAINING;
                            else if (tb_DinoFilterType.Text == ASILang.Get("FilterType_NotContaining"))
                                ft = FilterType.NOT_CONTAINING;
                            else if (tb_DinoFilterType.Text == ASILang.Get("FilterType_LowerThan"))
                                ft = FilterType.LOWER_THAN;
                            else if (tb_DinoFilterType.Text == ASILang.Get("FilterType_GreaterThan"))
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
            if (EditDinoFiltersPopup.IsOpen)
                return;

            FillEditDinoFiltersPopup();
            EditDinoFiltersPopup.IsOpen = true;
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

            filterName = DinoUtils.GetPropertyNameFromCleanName(filterName);
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
                    FillEditDinoFiltersPopup();
                    ApplyFiltersAndSort();
                }
            }
        }

        private void btn_CloseEditDinoFiltersPopup_Click(object sender, RoutedEventArgs e)
        {
            if (EditDinoFiltersPopup.IsOpen)
                EditDinoFiltersPopup.IsOpen = false;
        }

        private void btn_RemoveAllDinoFilters_Click(object sender, RoutedEventArgs e)
        {
            _filters.Clear();
            QuickFilter_Tribe_Label = ASILang.Get("ClickHere");
            QuickFilter_Dino_Label = ASILang.Get("ClickHere");
            FillEditDinoFiltersPopup();
            ApplyFiltersAndSort();
        }

        private void mfi_FilterByExactMatch_Click(object sender, RoutedEventArgs e)
        {
            tb_DinoFilterType.Text = ASILang.Get("FilterType_ExactMatch");

            ResetFiltersValues();

            if (!string.IsNullOrWhiteSpace(_selectedDinoFilter_Name))
            {
                PropertyInfo? foundProperty = Utils.GetProperty(typeof(Dino), _selectedDinoFilter_Name);
                List<string> propertiesValues = Utils.GetPropertyValues(SettingsPage._dinosData, foundProperty, MAX_PROPERTY_VALUES);
                mf_DinoFilterValue.Items.Clear();
                if (propertiesValues.Count > 0)
                {
                    propertiesValues.Sort();
                    foreach (string val in propertiesValues)
                        if (val != null)
                        {
                            mf_DinoFilterValue.Items.Add(new MenuFlyoutItem
                            {
                                Text = val,
                                Command = DinoFilterValueSelectCommand,
                                CommandParameter = val
                            });
                        }
                }
            }

            sp_FilterByExactMatch.Visibility = Visibility.Visible;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Visible;
            sp_FilterByOther.Visibility = Visibility.Collapsed;
        }

        private void SetDinoFilterType(string dinoFilterType)
        {
            tb_DinoFilterType.Text = dinoFilterType;

            ResetFiltersValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByStartingWith_Click(object sender, RoutedEventArgs e) => SetDinoFilterType(ASILang.Get("FilterType_StartingWith"));

        private void mfi_FilterByEndingWith_Click(object sender, RoutedEventArgs e) => SetDinoFilterType(ASILang.Get("FilterType_EndingWith"));

        private void mfi_FilterByContaining_Click(object sender, RoutedEventArgs e) => SetDinoFilterType(ASILang.Get("FilterType_Containing"));

        private void mfi_FilterByNotContaining_Click(object sender, RoutedEventArgs e) => SetDinoFilterType(ASILang.Get("FilterType_NotContaining"));

        private void mfi_FilterByLowerThan_Click(object sender, RoutedEventArgs e) => SetDinoFilterType(ASILang.Get("FilterType_LowerThan"));

        private void mfi_FilterByGreaterThan_Click(object sender, RoutedEventArgs e) => SetDinoFilterType(ASILang.Get("FilterType_GreaterThan"));

        private void tb_FilterByOther_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_AddToDinoFilters.IsEnabled = (tb_FilterByOther.Text.Length > 0);
        }

        private void mfi_FilterOperatorAnd_Click(object sender, RoutedEventArgs e) => tb_DinoFilterOperator.Text = ASILang.Get("OperatorAND");

        private void mfi_FilterOperatorOr_Click(object sender, RoutedEventArgs e) => tb_DinoFilterOperator.Text = ASILang.Get("OperatorOR");

        [RelayCommand]
        private void FiltersPresetGroupSelect(JsonFiltersPreset? preset)
        {
            tb_DinoFiltersGroupName.Text = ASILang.Get("ClickHere");
            btn_AddToDinoFiltersGroup.IsEnabled = false;
            if (preset == null || string.IsNullOrEmpty(preset.Name))
            {
                MainWindow.ShowToast(ASILang.Get("IncorrectFiltersPreset"), BackgroundColor.WARNING);
                return;
            }

            tb_DinoFiltersGroupName.Text = preset.Name;
            btn_AddToDinoFiltersGroup.IsEnabled = true;
        }

        private void FillFiltersGroupNames()
        {
            mf_DinoFiltersGroupNames.Items.Clear();
            if (_filtersPresets != null && _filtersPresets.Count > 0)
                foreach (var preset in _filtersPresets)
                    if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Filters != null && preset.Filters.Count > 0)
                    {
                        mf_DinoFiltersGroupNames.Items.Add(new MenuFlyoutItem
                        {
                            Text = preset.Name,
                            Command = FiltersPresetGroupSelectCommand,
                            CommandParameter = preset
                        });
                    }
        }

        private void btn_AddFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            if (AddDinoFiltersGroupPopup.IsOpen)
                return;

            FillFiltersGroupNames();
            AddDinoFiltersGroupPopup.IsOpen = true;
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
            sp_DinoFiltersPresetsInGroup.Children.Clear();
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
                        sp_DinoFiltersPresetsInGroup.Children.Add(grd);
                    }
            }
        }

        private void btn_EditFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            if (EditDinoFiltersGroupPopup.IsOpen)
                return;

            FillEditFiltersGroup();
            EditDinoFiltersGroupPopup.IsOpen = true;
        }

        private void mfi_FiltersGroupOperatorAnd_Click(object sender, RoutedEventArgs e) => tb_DinoFiltersGroupOperator.Text = ASILang.Get("OperatorAND");

        private void mfi_FiltersGroupOperatorOr_Click(object sender, RoutedEventArgs e) => tb_DinoFiltersGroupOperator.Text = ASILang.Get("OperatorOR");

        private void btn_AddToDinoFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            FilterOperator groupOperator = FilterOperator.NONE;
            if (string.Compare(tb_DinoFiltersGroupOperator.Text, ASILang.Get("OperatorAND"), StringComparison.InvariantCulture) == 0)
                groupOperator = FilterOperator.AND;
            else if (string.Compare(tb_DinoFiltersGroupOperator.Text, ASILang.Get("OperatorOR"), StringComparison.InvariantCulture) == 0)
                groupOperator = FilterOperator.OR;
            if (groupOperator == FilterOperator.NONE)
            {
                MainWindow.ShowToast(ASILang.Get("MissingOperatorCannotAddGroup"), BackgroundColor.WARNING);
                AddDinoFiltersGroupPopup.IsOpen = false;
                return;
            }

            if (_filtersPresets != null && _filtersPresets.Count > 0)
                foreach (var preset in _filtersPresets)
                    if (preset != null && !string.IsNullOrEmpty(preset.Name) &&
                        preset.Filters != null && preset.Filters.Count > 0 &&
                        string.Compare(preset.Name, tb_DinoFiltersGroupName.Text, StringComparison.InvariantCulture) == 0)
                    {
                        _group.Add(new KeyValuePair<FilterOperator, JsonFiltersPreset>(groupOperator, preset));
                        MainWindow.ShowToast($"{(ASILang.Get("FiltersPresetAddedToGroup").Replace("#PRESET_NAME#", $"\"{tb_DinoFiltersGroupName.Text}\"", StringComparison.InvariantCulture))}", BackgroundColor.SUCCESS);
                        ApplyFiltersAndSort();
                        AddDinoFiltersGroupPopup.IsOpen = false;
                        return;
                    }

            MainWindow.ShowToast($"{(ASILang.Get("CannotFindFiltersPreset").Replace("#PRESET_NAME#", $"\"{tb_DinoFiltersGroupName.Text}\"", StringComparison.InvariantCulture))}", BackgroundColor.WARNING);
            AddDinoFiltersGroupPopup.IsOpen = false;
        }

        private void btn_CloseDinoFiltersGroupPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddDinoFiltersGroupPopup.IsOpen)
                AddDinoFiltersGroupPopup.IsOpen = false;
        }

        private void btn_RemoveAllDinoFiltersPresetsFromGroup_Click(object sender, RoutedEventArgs e)
        {
            sp_DinoFiltersPresetsInGroup.Children.Clear();
            _group.Clear();
        }

        private void btn_CloseEditDinoFiltersGroupPopup_Click(object sender, RoutedEventArgs e)
        {
            if (EditDinoFiltersGroupPopup.IsOpen)
                EditDinoFiltersGroupPopup.IsOpen = false;
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
            var dinoProperties = typeof(Dino).GetProperties();
            if (dinoProperties != null && dinoProperties.Count() > 0)
            {
                List<KeyValuePair<string, string>>? props = new List<KeyValuePair<string, string>>();
                foreach (var dinoProperty in dinoProperties)
                {
                    string propName = dinoProperty.Name;
                    if (!string.IsNullOrEmpty(propName))
                    {
                        string? cleanName = DinoUtils.GetCleanNameFromPropertyName(propName);
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

            string filtersPresetsPath = Utils.DinoFiltersPresetsFilePath();
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
                    File.WriteAllText(Utils.DinoFiltersPresetsFilePath(), jsonString, Encoding.UTF8);
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
            btn_RemoveFiltersPreset.IsEnabled = (string.Compare(preset.Name, ASILang.Get("DefaultPreset"), StringComparison.InvariantCulture) != 0);
        }

        private void FillFiltersPresetsDropDown()
        {
            mf_ExistingFiltersPresets.Items.Clear();
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem
            {
                Text = ASILang.Get("DefaultPreset"),
                Command = FiltersPresetSelectCommand,
                CommandParameter = _defaultFiltersPreset
            });
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
            if (string.Compare(tb_FiltersPresetName.Text, ASILang.Get("DefaultPreset"), StringComparison.InvariantCultureIgnoreCase) == 0)
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
            QuickFilter_Dino_Label = ASILang.Get("ClickHere");

            Type type = typeof(Dino);
            foreach (var filter in _selectedFiltersPreset.Filters)
                if (filter != null && !string.IsNullOrEmpty(filter.PropertyName) && filter.Filter != null)
                {
                    PropertyInfo? prop = Utils.GetProperty(type, filter.PropertyName);
                    if (prop != null)
                        _filters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, filter.Filter));
                }
            // TODO: Remove FillEditDinoFiltersPopup()?
            FillEditDinoFiltersPopup();
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

        private void mfi_DefaultFiltersPreset_Click(object sender, RoutedEventArgs e)
        {
            tb_ExistingFiltersPreset.Text = ASILang.Get("DefaultPreset");
            btn_LoadFiltersPreset.IsEnabled = true;
            btn_RemoveFiltersPreset.IsEnabled = false;
            _selectedFiltersPreset = _defaultFiltersPreset;
        }

        #endregion

        #region Columns preset

        public void LoadColumnsPresets()
        {
            _columnsPresets.Clear();

            string columnsPresetsPath = Utils.DinoColumnsPresetsFilePath();
            if (!File.Exists(columnsPresetsPath))
                return;

            string? columnsPresetsJson = null;
            try { columnsPresetsJson = File.ReadAllText(columnsPresetsPath, Encoding.UTF8); }
            catch (Exception ex)
            {
                columnsPresetsJson = null;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in LoadColumnsPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
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
                    File.WriteAllText(Utils.DinoColumnsPresetsFilePath(), jsonString, Encoding.UTF8);
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
            btn_SetDefaultColumnsPreset.IsEnabled = false;

            if (preset == null)
                return;

            _selectedColumnsPreset = preset;
            tb_ExistingColumnsPreset.Text = preset.Name;
            btn_LoadColumnsPreset.IsEnabled = true;
            btn_RemoveColumnsPreset.IsEnabled = (string.Compare(preset.Name, ASILang.Get("DefaultPreset"), StringComparison.InvariantCulture) != 0);
            btn_SetDefaultColumnsPreset.IsEnabled = true;
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
            btn_SetDefaultColumnsPreset.IsEnabled = false;

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
                    btn_SetDefaultColumnsPreset.IsEnabled = false;

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

        private void LoadSelectedColumnsPreset(bool showToasts)
        {
            if (_selectedColumnsPreset == null || _selectedColumnsPreset.Columns == null || _selectedColumnsPreset.Columns.Count <= 0)
            {
                if (showToasts)
                    MainWindow.ShowToast(ASILang.Get("ColumnsPresetIsEmpty"), BackgroundColor.WARNING);
                return;
            }

            _selectedColumns.Clear();
            foreach (var column in _selectedColumnsPreset.Columns)
                if (column != null)
                    _selectedColumns.Add(column);
            if (showToasts)
                MainWindow.ShowToast(ASILang.Get("ColumnsPresetLoaded"), BackgroundColor.SUCCESS);
        }

        private void btn_LoadColumnsPreset_Click(object sender, RoutedEventArgs e) => LoadSelectedColumnsPreset(true);

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
                    btn_SetDefaultColumnsPreset.IsEnabled = false;

                    _columnsPresets.RemoveAt(toDel);
                    SaveColumnsPresets();
                    FillColumnsPresetsDropDown();
                }
                else
                    MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", $"\"{_selectedColumnsPreset.Name}\"", StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
            }
        }

        private void btn_SetDefaultColumnsPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColumnsPreset == null || string.IsNullOrEmpty(_selectedColumnsPreset.Name))
            {
                MainWindow.ShowToast(ASILang.Get("NoColumnsPresetSelected"), BackgroundColor.WARNING);
                return;
            }
            if (_selectedColumnsPreset.Columns == null || _selectedColumnsPreset.Columns.Count <= 0)
            {
                MainWindow.ShowToast(ASILang.Get("ColumnsPresetIsEmpty"), BackgroundColor.WARNING);
                return;
            }

            SettingsPage._defaultColumnsPreset_Dinos = _selectedColumnsPreset.Name;
            SettingsPage.SaveSettings();
            MainWindow.ShowToast(ASILang.Get("ColumnsPresetSetAsDefault").Replace("#PRESET_NAME#", _selectedColumnsPreset.Name ?? string.Empty), BackgroundColor.SUCCESS);
            LoadSelectedColumnsPreset(false);
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
            btn_SetDefaultColumnsPreset.IsEnabled = true;
            _selectedColumnsPreset = _defaultColumnsPreset;
        }

        #endregion

        #region Group presets

        public void LoadGroupPresets()
        {
            _groupPresets.Clear();

            string groupPresetsPath = Utils.DinoGroupsPresetsFilePath();
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
                File.WriteAllText(Utils.DinoGroupsPresetsFilePath(), jsonString, Encoding.UTF8);
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

            Type type = typeof(Dino);
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
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null && dino.DinoID1 != null && dino.DinoID1.HasValue && dino.DinoID2 != null && dino.DinoID2.HasValue)
                        clipboardStr = dino.GetDinoID();
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

        private void mfi_contextMenuGetJson_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null)
                        clipboardStr = JsonSerializer.Serialize<Dino>(dino, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
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
                var selectedDinos = dg_Dinos.SelectedItems;
                if (selectedDinos != null && selectedDinos.Count > 0)
                {
                    List<Dino>? dinos = new List<Dino>();
                    for (int i = 0; i < selectedDinos.Count; i++)
                    {
                        Dino? dino = (selectedDinos[i] as Dino);
                        if (dino != null)
                            dinos.Add(dino);
                    }
                    if (dinos.Count > 0)
                    {
                        clipboardStr = JsonSerializer.Serialize<List<Dino>>(dinos, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                        dinos.Clear();
                        dinos = null;
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
            if (SettingsPage._legacySearch != null && SettingsPage._legacySearch.HasValue && SettingsPage._legacySearch.Value)
            {
                RemoveAllFiltersForProp(_owningTribeIdProp);
                if (SettingsPage._allTribesForDinos.ContainsKey(tribeName))
                    _filters.Add(new KeyValuePair<PropertyInfo, Filter>(_owningTribeIdProp, new Filter()
                    {
                        FilterOperator = FilterOperator.AND,
                        FilterType = FilterType.EXACT_MATCH,
                        FilterValues = new List<string>() { SettingsPage._allTribesForDinos[tribeName].ToString(CultureInfo.InvariantCulture) }
                    }));
                QuickFilter_Tribe_Label = tribeName;
                FillEditDinoFiltersPopup();
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

        private static PropertyInfo? _cleanNameProp = Utils.GetProperty(typeof(Dino), nameof(Dino.CleanName));

        private void QuickFilterDinoSelect(string cleanName)
        {
            if (_cleanNameProp == null)
                return;
            if (SettingsPage._legacySearch != null && SettingsPage._legacySearch.HasValue && SettingsPage._legacySearch.Value)
            {
                RemoveAllFiltersForProp(_cleanNameProp);
                if (SettingsPage._allCleanNamesForDinosSorted.Contains(cleanName))
                    _filters.Add(new KeyValuePair<PropertyInfo, Filter>(_cleanNameProp, new Filter()
                    {
                        FilterOperator = FilterOperator.AND,
                        FilterType = FilterType.EXACT_MATCH,
                        FilterValues = new List<string>() { cleanName }
                    }));
                QuickFilter_Dino_Label = cleanName;
                FillEditDinoFiltersPopup();
            }
            else
            {
                _quickFilter_SelectedDino = string.Compare(cleanName, ASILang.Get("All").ToUpper(), StringComparison.InvariantCulture) == 0 ? null : cleanName;
                SetupQuickFilter();
            }
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
                s.Initialize(SearchType.DINOS);
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
