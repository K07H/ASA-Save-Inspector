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
    public sealed partial class StructuresPage : Page, INotifyPropertyChanged
    {
        #region Constants

        private const double _scrollBarWidth = 24.0d;

        #endregion

        #region Statics

        private static readonly object lockObject = new object();

        public static StructuresPage? _page = null;

        private static string? _currentSort = null;
        private static bool _ascendingSort = true;
        private static string? _secondaryCurrentSort = null;
        private static bool _secondaryAscendingSort = true;

        private static bool _setDefaultSelectedColumns = false;
        private static List<string> _selectedColumns = new List<string>();

        private static JsonColumnsPreset _defaultColumnsPreset = new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>(StructureUtils.DefaultSelectedColumns) };

        // Map name, save file datetime and in-game datetime.
        public static string MapName => (SettingsPage._currentlyLoadedMapName ?? ASILang.Get("Unknown"));
        public static string SaveGameDatetime => (Utils.GetSaveFileDateTimeStr() ?? ASILang.Get("UnknownDate"));
        public static string InGameDatetime => Utils.GetInGameDateTimeStr();

        #endregion

        #region Properties

        public IEnumerable<Structure>? _lastDisplayed = null;
        public ObservableCollection<Structure>? _lastDisplayedVM = null;

        private List<JsonColumnsPreset> _columnsPresets = new List<JsonColumnsPreset>();
        private JsonColumnsPreset? _selectedColumnsPreset = null;

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
            tb_CreateFilter.Visibility = Visibility.Visible;
            btn_CreateFilter.Visibility = Visibility.Visible;
            tb_SavedQueries.Visibility = Visibility.Visible;
            cbb_ExistingQueries.Visibility = Visibility.Visible;
            btn_LoadQuery.Visibility = Visibility.Visible;
            btn_EditQuery.Visibility = Visibility.Visible;
            btn_DeleteQuery.Visibility = Visibility.Visible;
            SearchBuilder.InitSearch(SearchType.STRUCTURES);

            // Set default selected columns.
            if (!_setDefaultSelectedColumns)
            {
                LoadColumnsPresets();
                bool fallbackToDefault = true;
                if (!string.IsNullOrEmpty(SettingsPage._defaultColumnsPreset_Structures))
                    foreach (var preset in _columnsPresets)
                        if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Columns != null && preset.Columns.Count > 0)
                            if (string.Compare(SettingsPage._defaultColumnsPreset_Structures, preset.Name, StringComparison.InvariantCulture) == 0)
                            {
                                fallbackToDefault = false;
                                foreach (var c in preset.Columns)
                                    _selectedColumns.Add(c);
                                break;
                            }
                if (fallbackToDefault && _selectedColumns != null && StructureUtils.DefaultSelectedColumns != null && StructureUtils.DefaultSelectedColumns.Count > 0)
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
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
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
                MainWindow.UpdateMinimap(structuresFiltered, LastStructureDoubleTap);
            }
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

        public void ApplyFiltersAndSort()
        {
            IEnumerable<Structure>? filtered = null;
            if (SearchBuilder._query == null || SearchBuilder._query.Parts == null || SearchBuilder._query.Parts.Count <= 0)
                filtered = SettingsPage._structuresData;
            else
                filtered = SearchBuilder.DoSearchQuery(SearchType.STRUCTURES, true) as IEnumerable<Structure>;
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
                    string? subMapName = s.GetSubMapName();
                    Minimap.ShowCallout(structureIDStr, x, y, subMapName);
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

        #region Columns preset

        public void LoadColumnsPresets()
        {
            _columnsPresets.Clear();

            string columnsPresetsPath = Utils.StructureColumnsPresetsFilePath();
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

            SettingsPage._defaultColumnsPreset_Structures = _selectedColumnsPreset.Name;
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

        public static bool ApplyCurrentQuery()
        {
            if (_page == null)
            {
                MainWindow.ShowToast(ASILang.Get("PageNotFound") + " (Structures)", BackgroundColor.WARNING);
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

        private void btn_DuplicateQuery_Click(object sender, RoutedEventArgs e) => Utils.DuplicateSearchQuery(Utils.GetComboBoxSelection(cbb_ExistingQueries, false), SearchType.STRUCTURES);

        #endregion
    }
}
