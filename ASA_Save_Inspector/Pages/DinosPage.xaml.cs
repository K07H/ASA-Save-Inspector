using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.ObjectModelUtils;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using WinUIEx;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class DinosPage : Page, INotifyPropertyChanged
    {
        #region Constants

        private const double _scrollBarWidth = 24.0d;

        #endregion

        #region Statics

        private static readonly object lockObject = new object();

        public static DinosPage? _page = null;

        private static string? _currentSort = null;
        private static bool _ascendingSort = true;
        private static string? _secondaryCurrentSort = null;
        private static bool _secondaryAscendingSort = true;

        private static bool _setDefaultSelectedColumns = false;
        private static List<string> _selectedColumns = new List<string>();

        private static JsonColumnsPreset _defaultColumnsPreset = new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>(DinoUtils.DefaultSelectedColumns) };

        // Map name, save file datetime and in-game datetime.
        public static string MapName => (SettingsPage._currentlyLoadedMapName ?? ASILang.Get("Unknown"));
        public static string SaveGameDatetime => (Utils.GetSaveFileDateTimeStr() ?? ASILang.Get("UnknownDate"));
        public static string InGameDatetime => Utils.GetInGameDateTimeStr();

        #endregion

        #region Properties

        public IEnumerable<Dino>? _lastDisplayed = null;
        public ObservableCollection<Dino>? _lastDisplayedVM = null;

        private List<JsonColumnsPreset> _columnsPresets = new List<JsonColumnsPreset>();
        private JsonColumnsPreset? _selectedColumnsPreset = null;

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
            SearchBuilder.InitSearch(SearchType.DINOS);

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
                MainWindow.UpdateMinimap(dinosFiltered, LastDinoDoubleTap);
            }
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

        public void ApplyFiltersAndSort()
        {
            IEnumerable<Dino>? filtered = null;
            if (SearchBuilder._query == null || SearchBuilder._query.Parts == null || SearchBuilder._query.Parts.Count <= 0)
                filtered = SettingsPage._dinosData;
            else
                filtered = SearchBuilder.DoSearchQuery(SearchType.DINOS, true) as IEnumerable<Dino>;
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
                    string? subMapName = null;
                    if (d.bIsCryopodded != null && d.bIsCryopodded.HasValue && d.bIsCryopodded.Value)
                    {
                        var cryopodPos = Utils.GetCryopodCoordsByUUID(d.CryopodUUID);
                        //minimapCoords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, cryopodPos.Item1, cryopodPos.Item2, cryopodPos.Item3);
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

        public static bool ApplyCurrentQuery()
        {
            if (_page == null)
            {
                MainWindow.ShowToast(ASILang.Get("PageNotFound") + " (Dinos)", BackgroundColor.WARNING);
                return false;
            }

            _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                if (SearchBuilder._query != null && SearchBuilder._query.Parts != null && SearchBuilder._query.Parts.Count > 0)
                {
                    _page.ApplyFiltersAndSort();
                    MainWindow.ShowToast(ASILang.Get("FilterLoaded"), BackgroundColor.SUCCESS);
                }
                else
                    MainWindow.ShowToast(ASILang.Get("EmptyFilter"), BackgroundColor.WARNING);
            });
            return true;
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

        private void btn_DuplicateQuery_Click(object sender, RoutedEventArgs e) => Utils.DuplicateSearchQuery(Utils.GetComboBoxSelection(cbb_ExistingQueries, false), SearchType.DINOS);

        #endregion
    }
}
