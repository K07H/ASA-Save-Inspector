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

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class ItemsPage : Page, INotifyPropertyChanged
    {
        #region Constants

        private const double _scrollBarWidth = 24.0d;
        private const int MAX_PROPERTY_VALUES = 1000;

        #endregion

        #region Statics

        private static readonly object lockObject = new object();

        public static ItemsPage? _page = null;

        private static string? _currentSort = null;
        private static bool _ascendingSort = true;
        private static string? _secondaryCurrentSort = null;
        private static bool _secondaryAscendingSort = true;

        private static bool _addedDefaultFilters = false;
        private static List<KeyValuePair<PropertyInfo, Filter>> _filters = new List<KeyValuePair<PropertyInfo, Filter>>();

        private static bool _setDefaultSelectedColumns = false;
        private static List<string> _selectedColumns = new List<string>();

        private static List<KeyValuePair<FilterOperator, JsonFiltersPreset>> _group = new List<KeyValuePair<FilterOperator, JsonFiltersPreset>>();

        private static JsonFiltersPreset _defaultFiltersPreset = new JsonFiltersPreset() { Name = ASILang.Get("DefaultPreset"), Filters = new List<JsonFilter>() };

        private static JsonColumnsPreset _defaultColumnsPreset = new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>() };

        // Map name, save file datetime and in-game datetime.
        public static string MapName => (SettingsPage._currentlyLoadedMapName ?? ASILang.Get("Unknown"));
        public static string SaveGameDatetime => (Utils.GetSaveFileDateTimeStr() ?? ASILang.Get("UnknownDate"));
        public static string InGameDatetime => Utils.GetInGameDateTimeStr();

        #endregion

        #region Properties

        public IEnumerable<Item>? _lastDisplayed = null;
        public ObservableCollection<Item>? _lastDisplayedVM = null;
        private string? _selectedItemFilter_Name = null;
        private List<string>? _selectedItemFilter_Values = new List<string>();

        private List<JsonFiltersPreset> _filtersPresets = new List<JsonFiltersPreset>();
        private JsonFiltersPreset? _selectedFiltersPreset = null;

        private List<JsonColumnsPreset> _columnsPresets = new List<JsonColumnsPreset>();
        private JsonColumnsPreset? _selectedColumnsPreset = null;

        private List<JsonGroupPreset> _groupPresets = new List<JsonGroupPreset>();
        private JsonGroupPreset? _selectedGroupPreset = null;

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

        #endregion

        #region Constructor/Destructor

        public ItemsPage()
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

            InitDefaultFilters();
            // Add default filters.
            AddDefaultFilters();

            // Set default selected columns.
            if (!_setDefaultSelectedColumns)
            {
                if (_selectedColumns != null && ItemUtils.DefaultSelectedColumns != null && ItemUtils.DefaultSelectedColumns.Count > 0)
                    foreach (string c in ItemUtils.DefaultSelectedColumns)
                        _selectedColumns.Add(c);
                _setDefaultSelectedColumns = true;
            }

            // Set "Include Properties With Many Values" checkbox label.
            cb_IncludePropertiesWithManyValues.Content = ASILang.Get("IncludeVariablesWithMoreThanNValues").Replace("#VALUES_AMOUNT#", $"{MAX_PROPERTY_VALUES.ToString(CultureInfo.InvariantCulture)}", StringComparison.InvariantCulture);

            // Grab items data from settings if not set.
            if (_lastDisplayed == null)
                _lastDisplayed = SettingsPage._itemsData;

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
                if (_page.dg_Items != null)
                {
                    if (_page.dg_Items.ItemsSource != null)
                        _page.dg_Items.ItemsSource = null;
                    if (gr_Main != null)
                        gr_Main.Children.Remove(_page.dg_Items);
                }
                if (_page._selectedItemFilter_Values != null)
                {
                    _page._selectedItemFilter_Values.Clear();
                    _page._selectedItemFilter_Values = null;
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

        private void InitDefaultFilters()
        {
            _defaultColumnsPreset = new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>() };

            _defaultFiltersPreset = new JsonFiltersPreset() { Name = ASILang.Get("DefaultPreset"), Filters = new List<JsonFilter>() };
        }

        public bool GoToItem(int? id1, int? id2)
        {
            if (id1 != null && id1.HasValue && id2 != null && id2.HasValue && _lastDisplayedVM != null)
            {
                Item? item = _lastDisplayedVM.FirstOrDefault(d => (d?.ItemID?.ItemID1 == id1 && d?.ItemID.ItemID2 == id2), null);
                if (item != null)
                {
                    dg_Items.SelectedItem = item;
                    dg_Items.UpdateLayout();
                    dg_Items.ScrollIntoView(dg_Items.SelectedItem, null);
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
                _group.Clear();
                _filters.Clear();
                _filters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, new Filter() { FilterOperator = FilterOperator.AND, FilterType = FilterType.CONTAINING, FilterValue = inventoryUUID }));
                ApplyFiltersAndSort();
            }
            return true;
        }

        private bool LastItemDoubleTap(MapPoint? point)
        {
            if (point == null || _lastDisplayedVM == null || string.IsNullOrWhiteSpace(point.ID) || point.ID == "00")
                return false;

            Item? item = _lastDisplayedVM.FirstOrDefault(d => (string.Compare(point.ID, d?.GetItemID(), StringComparison.InvariantCulture) == 0), null);
            if (item != null)
            {
                dg_Items.SelectedItem = item;
                dg_Items.UpdateLayout();
                dg_Items.ScrollIntoView(dg_Items.SelectedItem, null);
                return true;
            }
            return false;
        }

        private void Init(ref IEnumerable<Item>? items, bool onlyRefresh)
        {
            dg_Items.MaxHeight = Math.Max(1.0d, gr_Main.ActualHeight - (tb_Title.ActualHeight + sp_SortAndFilters.ActualHeight + _scrollBarWidth + 4.0d));
            dg_Items.MaxWidth = Math.Max(1.0d, gr_Main.ActualWidth - (_scrollBarWidth + 4.0d));
            if (!onlyRefresh && items != null)
            {
                if (_lastDisplayedVM != null)
                {
                    _lastDisplayedVM.Clear();
                    _lastDisplayedVM = null;
                }
                _lastDisplayedVM = new ObservableCollection<Item>(items);
            }
            dg_Items.ItemsSource = _lastDisplayedVM;
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(250);
                ReorderColumns();
            });
            if (items != null && MainWindow._minimap != null)
                MainWindow.UpdateMinimap(items.Select(d =>
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
                            X = minimapCoords.Value,
                            Y = minimapCoords.Key
                        };
                    }
                    return null;
                }), LastItemDoubleTap);
        }

        private void ReorderColumns()
        {
            if (dg_Items?.Columns != null && dg_Items.Columns.Count > 0)
            {
                List<string?> orders = new List<string?>();
                foreach (DataGridColumn col in dg_Items.Columns)
                    if (col != null)
                        orders.Add(col.Header.ToString());
                orders.Sort();

                List<string?> defaultOrders = new List<string?>();
                foreach (string defaultOrder in ItemUtils.DefaultColumnsOrder)
                    if (orders.Contains(defaultOrder))
                        defaultOrders.Add(defaultOrder);
                foreach (string? otherOrder in orders)
                    if (!defaultOrders.Contains(otherOrder))
                        defaultOrders.Add(otherOrder);

                for (int i = 0; i < defaultOrders.Count; i++)
                {
                    foreach (DataGridColumn col in dg_Items.Columns)
                        if (col != null && string.Compare(defaultOrders[i], col.Header.ToString(), StringComparison.InvariantCulture) == 0)
                        {
                            col.DisplayIndex = i;
                            break;
                        }
                }
            }
        }

        private void RefreshDisplayedColumns()
        {
            if (MainWindow._mainWindow == null)
                return;
#pragma warning disable CS1998
            MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                MainWindow._mainWindow.NavView_Navigate(typeof(HomePage), new EntranceNavigationTransitionInfo());
                MainWindow._mainWindow.NavView_Navigate(typeof(ItemsPage), new EntranceNavigationTransitionInfo());
            });
#pragma warning restore CS1998
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<Item>? filtered = null;
            if (_group != null && _group.Count > 0)
                filtered = ApplyGroupFiltering();
            else
                filtered = ApplyFiltering(SettingsPage._itemsData, _filters);
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

        //private void dg_Items_LayoutUpdated(object sender, object e) => AdjustColumnSizes();

        private void dg_Items_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            run_NbLinesSelected.Text = (dg_Items.SelectedItems != null ? dg_Items.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) : "0");
            mfi_contextMenuGetAllJson.Visibility = (dg_Items.SelectedItems != null && dg_Items.SelectedItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed);
            mfi_contextMenuGetCoords.Visibility = Visibility.Visible;
            Visibility goToContainerVisibility = Visibility.Collapsed;

            DataGridRow? row = Utils.FindParent<DataGridRow>((UIElement)e.OriginalSource);
            if (row != null)
            {
                Item? d = row.DataContext as Item;
                if (d != null)
                {
                    var owner = d.Owner();
                    if (owner != null && owner.HasValue && owner.Value.Key != ArkObjectType.UNKNOWN && owner.Value.Value != null)
                        goToContainerVisibility = Visibility.Visible;
                }
            }
            mfi_contextMenuGoToContainer.Visibility = goToContainerVisibility;
        }

        private void dg_Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            run_NbLinesSelected.Text = (dg_Items.SelectedItems != null ? dg_Items.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) : "0");
        }

        private void dg_Items_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (!_selectedColumns.Contains((string)e.Column.Header))
                e.Cancel = true; //e.Column.Visibility = Visibility.Collapsed;
            e.Column.Header = ItemUtils.GetCleanNameFromPropertyName(e.PropertyName);
        }

        #endregion

        #region Sorting

        private object? GetItemPropertyValueByName(string cleanName, Item d)
        {
            PropertyInfo? prop = Utils.GetProperty(typeof(Item), ItemUtils.GetPropertyNameFromCleanName(cleanName));
            if (prop == null)
                return null;
            return prop.GetValue(d);
        }

        private void SimpleSort(ref IEnumerable<Item> items, string cleanName)
        {
            if (AscendingSort)
                items = items.OrderBy(o => GetItemPropertyValueByName(cleanName, o));
            else
                items = items.OrderByDescending(o => GetItemPropertyValueByName(cleanName, o));
        }

#pragma warning disable CS8604
        private void DoubleSort(ref IEnumerable<Item> items, string cleanName)
        {
            if (AscendingSort)
            {
                if (SecondaryAscendingSort)
                    items = items.OrderBy(o => GetItemPropertyValueByName(cleanName, o)).ThenBy(o => GetItemPropertyValueByName(SecondaryCurrentSort, o));
                else
                    items = items.OrderBy(o => GetItemPropertyValueByName(cleanName, o)).ThenByDescending(o => GetItemPropertyValueByName(SecondaryCurrentSort, o));
            }
            else
            {
                if (SecondaryAscendingSort)
                    items = items.OrderByDescending(o => GetItemPropertyValueByName(cleanName, o)).ThenBy(o => GetItemPropertyValueByName(SecondaryCurrentSort, o));
                else
                    items = items.OrderByDescending(o => GetItemPropertyValueByName(cleanName, o)).ThenByDescending(o => GetItemPropertyValueByName(SecondaryCurrentSort, o));
            }
        }
#pragma warning restore CS8604

        private void SortDataGrid(ref IEnumerable<Item> items, string cleanName)
        {
            if (items == null)
                return;

            if (!string.IsNullOrWhiteSpace(CurrentSort) && SecondaryCurrentSort != null)
                DoubleSort(ref items, cleanName);
            else
                SimpleSort(ref items, cleanName);
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

        private void ApplySorting(ref IEnumerable<Item> items)
        {
            if (CurrentSort != null)
            {
                SortDataGrid(ref items, CurrentSort);
                RefreshPrimarySortLabel();
                tb_PrimarySort.Visibility = Visibility.Visible;
                if (SecondaryCurrentSort != null)
                {
                    RefreshSecondarySortLabel();
                    tb_SecondarySort.Visibility = Visibility.Visible;
                }
            }
        }

        private void dg_Items_Sorting(object sender, DataGridColumnEventArgs e)
        {
            if (e?.Column?.Header != null)
            {
                string? cleanName = e.Column.Header.ToString();
                if (SettingsPage._itemsData != null && cleanName != null)
                {
                    SetSorting(cleanName);
                    ApplyFiltersAndSort();
                }
            }
        }

        #endregion

        #region Filtering

        private static void AddDefaultFilters()
        {
            if (!_addedDefaultFilters)
            {
                _addedDefaultFilters = true;
            }
        }

        private void RefreshSelectedItemFilterValues()
        {
            tb_itemFilterValues.Text = (_selectedItemFilter_Values != null ? string.Join(", ", _selectedItemFilter_Values) : string.Empty);
            b_itemFilterValues.Visibility = (tb_itemFilterValues.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed);
        }

        [RelayCommand]
        private void ItemFilterValueSelect(string filterValue)
        {
            if (!string.IsNullOrWhiteSpace(filterValue) && _selectedItemFilter_Values != null)
            {
                if (_selectedItemFilter_Values.Contains(filterValue))
                    _selectedItemFilter_Values.Remove(filterValue);
                else
                    _selectedItemFilter_Values.Add(filterValue);
                RefreshSelectedItemFilterValues();
                btn_AddToItemFilters.IsEnabled = (_selectedItemFilter_Values.Count > 0);
            }
        }

        [RelayCommand]
        private void ItemFilterSelect(string propName)
        {
            if (!string.IsNullOrWhiteSpace(propName))
            {
                _selectedItemFilter_Name = propName;
                tb_ItemFilterName.Text = ItemUtils.GetCleanNameFromPropertyName(_selectedItemFilter_Name);

                tb_ItemFilterType.Text = ASILang.Get("ClickHere");
                sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
                sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
                sp_FilterByOther.Visibility = Visibility.Collapsed;

                ResetFiltersValues();

                PropertyInfo? foundProperty = Utils.GetProperty(typeof(Item), propName);
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
                }
            }
        }

        private void ResetFiltersValues()
        {
            btn_AddToItemFilters.IsEnabled = false;

            tb_FilterByOther.Text = "";

            if (_selectedItemFilter_Values != null)
                _selectedItemFilter_Values.Clear();
            RefreshSelectedItemFilterValues();
        }

        private void ResetFilters()
        {
            mf_ItemFilterName.Items.Clear();
            _selectedItemFilter_Name = null;
            tb_ItemFilterOperator.Text = ASILang.Get("ClickHere");
            tb_ItemFilterName.Text = ASILang.Get("ClickHere");
            tb_ItemFilterType.Text = ASILang.Get("ClickHere");

            ResetFiltersValues();
        }

        private void FillPropertiesDropDown()
        {
            mf_ItemFilterName.Items.Clear();
            _selectedItemFilter_Name = null;
            tb_ItemFilterName.Text = ASILang.Get("ClickHere");

            btn_AddToItemFilters.IsEnabled = false;
            if (_selectedItemFilter_Values != null)
                _selectedItemFilter_Values.Clear();
            RefreshSelectedItemFilterValues();

            bool includePropertiesWithManyValues = (cb_IncludePropertiesWithManyValues.IsChecked != null && cb_IncludePropertiesWithManyValues.IsChecked.HasValue && cb_IncludePropertiesWithManyValues.IsChecked.Value);
            var itemProperties = typeof(Item).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (itemProperties != null && itemProperties.Count() > 0)
            {
                Dictionary<string, string> propNames = new Dictionary<string, string>();
                List<string> toAdd = new List<string>();
                foreach (var itemProperty in itemProperties)
                {
                    if (includePropertiesWithManyValues || !Utils.PropertyHasMoreValuesThan(SettingsPage._itemsData, itemProperty, MAX_PROPERTY_VALUES))
                    {
                        string propName = itemProperty.Name;
                        if (propName != null)
                        {
                            string? cleanName = ItemUtils.GetCleanNameFromPropertyName(propName);
                            if (cleanName != null)
                            {
                                toAdd.Add(cleanName);
                                propNames[cleanName] = propName;
                            }
                        }
                    }
                }
                if (toAdd.Count > 0)
                {
                    toAdd.Sort();
                    foreach (string cleanName in toAdd)
                        if (cleanName != null)
                        {
                            mf_ItemFilterName.Items.Add(new MenuFlyoutItem
                            {
                                Text = cleanName,
                                Command = ItemFilterSelectCommand,
                                CommandParameter = propNames[cleanName]
                            });
                        }
                }
                propNames.Clear();
                toAdd.Clear();
            }
        }

        private bool CheckMatchFilter(Item d, List<KeyValuePair<PropertyInfo, Filter>> filters)
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

        private void AddOrFilters(ref List<Expression<Func<Item, bool>>> orFilters, List<KeyValuePair<PropertyInfo, Filter>> filters)
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

        private IEnumerable<Item>? DoApplyGroupFiltering(IEnumerable<Item>? filtered, JsonFiltersPreset preset)
        {
            if (filtered == null)
                return null;

            IEnumerable<Item>? ret = null;
            if (preset.Filters != null && preset.Filters.Count > 0)
            {
                List<KeyValuePair<PropertyInfo, Filter>> currentFilters = new List<KeyValuePair<PropertyInfo, Filter>>();
                Type type = typeof(Item);
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

        private IEnumerable<Item>? ApplyGroupFiltering()
        {
            IEnumerable<Item>? andFiltered = null;
            for (int i = 0; i < _group.Count; i++)
                if (_group[i].Key == FilterOperator.AND && _group[i].Value != null)
                {
                    if (andFiltered == null)
                        andFiltered = DoApplyGroupFiltering(SettingsPage._itemsData, _group[i].Value);
                    else
                        andFiltered = DoApplyGroupFiltering(andFiltered, _group[i].Value);
                }

            IEnumerable<Item>? orFiltered = null;
            List<IEnumerable<Item>?> orFiltereds = new List<IEnumerable<Item>?>();
            for (int j = 0; j < _group.Count; j++)
                if (_group[j].Key == FilterOperator.OR && _group[j].Value != null)
                    orFiltereds.Add(DoApplyGroupFiltering(SettingsPage._itemsData, _group[j].Value));

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

        private IEnumerable<Item>? ApplyFiltering(IEnumerable<Item>? items, List<KeyValuePair<PropertyInfo, Filter>> filters)
        {
            if (items == null)
                return null;

            var orFilters = new List<Expression<Func<Item, bool>>>();
            AddOrFilters(ref orFilters, filters);
            var lambda = AnyOf(orFilters.ToArray());

            return items.Where(lambda.Compile()).Where(a => CheckMatchFilter(a, filters));
        }

        public static void ClearPageFiltersAndGroups()
        {
#pragma warning disable CS1998
            if (_page != null)
                _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    _page.sp_ItemFiltersPresetsInGroup.Children.Clear();
                    _page.sp_ExistingItemFilters.Children.Clear();
                });
#pragma warning restore CS1998
            _group.Clear();
            _filters.Clear();
            _addedDefaultFilters = false;
            AddDefaultFilters();
        }

        private void FillEditItemFiltersPopup()
        {
            sp_ExistingItemFilters.Children.Clear();
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
                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
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
                        Text = $"{ItemUtils.GetCleanNameFromPropertyName(filter.Key.Name)}",
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
                        else if (filter.Value.FilterType == FilterType.NOT_CONTAINING)
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
                    grd.Children.Add(tb1);
                    Grid.SetColumn(tb1, 0);
                    grd.Children.Add(tb2);
                    Grid.SetColumn(tb2, 1);
                    grd.Children.Add(tb3);
                    Grid.SetColumn(tb3, 2);
                    grd.Children.Add(b);
                    Grid.SetColumn(b, 3);
                    grd.Children.Add(btn);
                    Grid.SetColumn(btn, 4);
                    sp_ExistingItemFilters.Children.Add(grd);
                }
        }

        private void cb_IncludePropertiesWithManyValues_CheckedUnchecked(object sender, RoutedEventArgs e)
        {
            ResetFilters();
            FillPropertiesDropDown();
        }

        private void btn_AddFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!AddItemFilterPopup.IsOpen)
            {
                ResetFilters();
                FillPropertiesDropDown();
                AddItemFilterPopup.IsOpen = true;
            }
        }

        private void btn_CloseItemFiltersPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddItemFilterPopup.IsOpen)
                AddItemFilterPopup.IsOpen = false;
        }

        private void btn_AddToItemFilters_Click(object sender, RoutedEventArgs e)
        {
            FilterOperator filterOperator = FilterOperator.NONE;
            if (string.Compare(ASILang.Get("OperatorAND"), tb_ItemFilterOperator.Text, StringComparison.InvariantCulture) == 0)
                filterOperator = FilterOperator.AND;
            else if (string.Compare(ASILang.Get("OperatorOR"), tb_ItemFilterOperator.Text, StringComparison.InvariantCulture) == 0)
                filterOperator = FilterOperator.OR;
            if (filterOperator == FilterOperator.NONE)
            {
                MainWindow.ShowToast(ASILang.Get("MissingOperatorCannotAddFilter"), BackgroundColor.WARNING);
                return;
            }

            if (AddItemFilterPopup.IsOpen)
            {
                AddItemFilterPopup.IsOpen = false;
                if (!string.IsNullOrEmpty(_selectedItemFilter_Name))
                {
                    PropertyInfo? prop = Utils.GetProperty(typeof(Item), _selectedItemFilter_Name);
                    if (prop != null)
                    {
                        if (tb_ItemFilterType.Text == ASILang.Get("FilterType_ExactMatch") &&
                            _selectedItemFilter_Values != null &&
                            _selectedItemFilter_Values.Count > 0)
                        {
                            _filters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, new Filter()
                            {
                                FilterOperator = filterOperator,
                                FilterType = FilterType.EXACT_MATCH,
                                FilterValues = new List<string>(_selectedItemFilter_Values)
                            }));
                        }
                        else if (tb_FilterByOther.Text != null)
                        {
                            FilterType ft = FilterType.NONE;
                            if (tb_ItemFilterType.Text == ASILang.Get("FilterType_StartingWith"))
                                ft = FilterType.STARTING_WITH;
                            else if (tb_ItemFilterType.Text == ASILang.Get("FilterType_EndingWith"))
                                ft = FilterType.ENDING_WITH;
                            else if (tb_ItemFilterType.Text == ASILang.Get("FilterType_Containing"))
                                ft = FilterType.CONTAINING;
                            else if (tb_ItemFilterType.Text == ASILang.Get("FilterType_NotContaining"))
                                ft = FilterType.NOT_CONTAINING;
                            else if (tb_ItemFilterType.Text == ASILang.Get("FilterType_LowerThan"))
                                ft = FilterType.LOWER_THAN;
                            else if (tb_ItemFilterType.Text == ASILang.Get("FilterType_GreaterThan"))
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
            if (EditItemFiltersPopup.IsOpen)
                return;

            FillEditItemFiltersPopup();
            EditItemFiltersPopup.IsOpen = true;
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

        private void btn_RemoveFilter_Click(object sender, RoutedEventArgs e)
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

            string? filterName = tb.Text;
            if (filterName == null)
                return;

            filterName = ItemUtils.GetPropertyNameFromCleanName(filterName);
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
                    FillEditItemFiltersPopup();
                    ApplyFiltersAndSort();
                }
            }
        }

        private void btn_CloseEditItemFiltersPopup_Click(object sender, RoutedEventArgs e)
        {
            if (EditItemFiltersPopup.IsOpen)
                EditItemFiltersPopup.IsOpen = false;
        }

        private void btn_RemoveAllItemFilters_Click(object sender, RoutedEventArgs e)
        {
            _filters.Clear();
            FillEditItemFiltersPopup();
            ApplyFiltersAndSort();
        }

        private void mfi_FilterByExactMatch_Click(object sender, RoutedEventArgs e)
        {
            tb_ItemFilterType.Text = ASILang.Get("FilterType_ExactMatch");

            ResetFiltersValues();

            if (!string.IsNullOrWhiteSpace(_selectedItemFilter_Name))
            {
                PropertyInfo? foundProperty = Utils.GetProperty(typeof(Item), _selectedItemFilter_Name);
                List<string> propertiesValues = Utils.GetPropertyValues(SettingsPage._itemsData, foundProperty, MAX_PROPERTY_VALUES);
                mf_ItemFilterValue.Items.Clear();
                if (propertiesValues.Count > 0)
                {
                    propertiesValues.Sort();
                    foreach (string val in propertiesValues)
                        if (val != null)
                        {
                            mf_ItemFilterValue.Items.Add(new MenuFlyoutItem
                            {
                                Text = val,
                                Command = ItemFilterValueSelectCommand,
                                CommandParameter = val
                            });
                        }
                }
            }

            sp_FilterByExactMatch.Visibility = Visibility.Visible;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Visible;
            sp_FilterByOther.Visibility = Visibility.Collapsed;
        }

        private void SetItemFilterType(string itemFilterType)
        {
            tb_ItemFilterType.Text = itemFilterType;

            ResetFiltersValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByStartingWith_Click(object sender, RoutedEventArgs e) => SetItemFilterType(ASILang.Get("FilterType_StartingWith"));

        private void mfi_FilterByEndingWith_Click(object sender, RoutedEventArgs e) => SetItemFilterType(ASILang.Get("FilterType_EndingWith"));

        private void mfi_FilterByContaining_Click(object sender, RoutedEventArgs e) => SetItemFilterType(ASILang.Get("FilterType_Containing"));

        private void mfi_FilterByNotContaining_Click(object sender, RoutedEventArgs e) => SetItemFilterType(ASILang.Get("FilterType_NotContaining"));

        private void mfi_FilterByLowerThan_Click(object sender, RoutedEventArgs e) => SetItemFilterType(ASILang.Get("FilterType_LowerThan"));

        private void mfi_FilterByGreaterThan_Click(object sender, RoutedEventArgs e) => SetItemFilterType(ASILang.Get("FilterType_GreaterThan"));

        private void tb_FilterByOther_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_AddToItemFilters.IsEnabled = (tb_FilterByOther.Text.Length > 0);
        }

        private void mfi_FilterOperatorAnd_Click(object sender, RoutedEventArgs e) => tb_ItemFilterOperator.Text = ASILang.Get("OperatorAND");

        private void mfi_FilterOperatorOr_Click(object sender, RoutedEventArgs e) => tb_ItemFilterOperator.Text = ASILang.Get("OperatorOR");

        [RelayCommand]
        private void FiltersPresetGroupSelect(JsonFiltersPreset? preset)
        {
            tb_ItemFiltersGroupName.Text = ASILang.Get("ClickHere");
            btn_AddToItemFiltersGroup.IsEnabled = false;
            if (preset == null || string.IsNullOrEmpty(preset.Name))
            {
                MainWindow.ShowToast(ASILang.Get("IncorrectFiltersPreset"), BackgroundColor.WARNING);
                return;
            }

            tb_ItemFiltersGroupName.Text = preset.Name;
            btn_AddToItemFiltersGroup.IsEnabled = true;
        }

        private void FillFiltersGroupNames()
        {
            mf_ItemFiltersGroupNames.Items.Clear();
            if (_filtersPresets != null && _filtersPresets.Count > 0)
                foreach (var preset in _filtersPresets)
                    if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Filters != null && preset.Filters.Count > 0)
                    {
                        mf_ItemFiltersGroupNames.Items.Add(new MenuFlyoutItem
                        {
                            Text = preset.Name,
                            Command = FiltersPresetGroupSelectCommand,
                            CommandParameter = preset
                        });
                    }
        }

        private void btn_AddFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            if (AddItemFiltersGroupPopup.IsOpen)
                return;

            FillFiltersGroupNames();
            AddItemFiltersGroupPopup.IsOpen = true;
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
            sp_ItemFiltersPresetsInGroup.Children.Clear();
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
                        sp_ItemFiltersPresetsInGroup.Children.Add(grd);
                    }
            }
        }

        private void btn_EditFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            if (EditItemFiltersGroupPopup.IsOpen)
                return;

            FillEditFiltersGroup();
            EditItemFiltersGroupPopup.IsOpen = true;
        }

        private void mfi_FiltersGroupOperatorAnd_Click(object sender, RoutedEventArgs e) => tb_ItemFiltersGroupOperator.Text = ASILang.Get("OperatorAND");

        private void mfi_FiltersGroupOperatorOr_Click(object sender, RoutedEventArgs e) => tb_ItemFiltersGroupOperator.Text = ASILang.Get("OperatorOR");

        private void btn_AddToItemFiltersGroup_Click(object sender, RoutedEventArgs e)
        {
            FilterOperator groupOperator = FilterOperator.NONE;
            if (string.Compare(tb_ItemFiltersGroupOperator.Text, ASILang.Get("OperatorAND"), StringComparison.InvariantCulture) == 0)
                groupOperator = FilterOperator.AND;
            else if (string.Compare(tb_ItemFiltersGroupOperator.Text, ASILang.Get("OperatorOR"), StringComparison.InvariantCulture) == 0)
                groupOperator = FilterOperator.OR;
            if (groupOperator == FilterOperator.NONE)
            {
                MainWindow.ShowToast(ASILang.Get("MissingOperatorCannotAddGroup"), BackgroundColor.WARNING);
                AddItemFiltersGroupPopup.IsOpen = false;
                return;
            }

            if (_filtersPresets != null && _filtersPresets.Count > 0)
                foreach (var preset in _filtersPresets)
                    if (preset != null && !string.IsNullOrEmpty(preset.Name) &&
                        preset.Filters != null && preset.Filters.Count > 0 &&
                        string.Compare(preset.Name, tb_ItemFiltersGroupName.Text, StringComparison.InvariantCulture) == 0)
                    {
                        _group.Add(new KeyValuePair<FilterOperator, JsonFiltersPreset>(groupOperator, preset));
                        MainWindow.ShowToast($"{(ASILang.Get("FiltersPresetAddedToGroup").Replace("#PRESET_NAME#", $"\"{tb_ItemFiltersGroupName.Text}\"", StringComparison.InvariantCulture))}", BackgroundColor.SUCCESS);
                        ApplyFiltersAndSort();
                        AddItemFiltersGroupPopup.IsOpen = false;
                        return;
                    }

            MainWindow.ShowToast($"{(ASILang.Get("CannotFindFiltersPreset").Replace("#PRESET_NAME#", $"\"{tb_ItemFiltersGroupName.Text}\"", StringComparison.InvariantCulture))}", BackgroundColor.WARNING);
            AddItemFiltersGroupPopup.IsOpen = false;
        }

        private void btn_CloseItemFiltersGroupPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddItemFiltersGroupPopup.IsOpen)
                AddItemFiltersGroupPopup.IsOpen = false;
        }

        private void btn_RemoveAllItemFiltersPresetsFromGroup_Click(object sender, RoutedEventArgs e)
        {
            sp_ItemFiltersPresetsInGroup.Children.Clear();
            _group.Clear();
        }

        private void btn_CloseEditItemFiltersGroupPopup_Click(object sender, RoutedEventArgs e)
        {
            if (EditItemFiltersGroupPopup.IsOpen)
                EditItemFiltersGroupPopup.IsOpen = false;
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
            var itemProperties = typeof(Item).GetProperties();
            if (itemProperties != null && itemProperties.Count() > 0)
            {
                List<KeyValuePair<string, string>>? props = new List<KeyValuePair<string, string>>();
                foreach (var itemProperty in itemProperties)
                {
                    string propName = itemProperty.Name;
                    if (!string.IsNullOrEmpty(propName))
                    {
                        string? cleanName = ItemUtils.GetCleanNameFromPropertyName(propName);
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

            string filtersPresetsPath = Utils.ItemFiltersPresetsFilePath();
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
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
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
                    File.WriteAllText(Utils.ItemFiltersPresetsFilePath(), jsonString, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
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
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
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

            Type type = typeof(Item);
            _filters.Clear();
            foreach (var filter in _selectedFiltersPreset.Filters)
                if (filter != null && !string.IsNullOrEmpty(filter.PropertyName) && filter.Filter != null)
                {
                    PropertyInfo? prop = Utils.GetProperty(type, filter.PropertyName);
                    if (prop != null)
                        _filters.Add(new KeyValuePair<PropertyInfo, Filter>(prop, filter.Filter));
                }
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

            string columnsPresetsPath = Utils.ItemColumnsPresetsFilePath();
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
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
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
                    File.WriteAllText(Utils.ItemColumnsPresetsFilePath(), jsonString, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
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
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
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

            string groupPresetsPath = Utils.ItemGroupsPresetsFilePath();
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
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in LoadGroupPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void SaveGroupPresets()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize<List<JsonGroupPreset>>(_groupPresets, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Utils.ItemGroupsPresetsFilePath(), jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
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
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.ERROR);
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

            Type type = typeof(Item);
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
                    Item? item = (mfi.DataContext as Item);
                    if (item != null && item.ItemID != null && item.ItemID.ItemID1 != null && item.ItemID.ItemID1.HasValue && item.ItemID.ItemID2 != null && item.ItemID.ItemID2.HasValue)
                        clipboardStr = $"{item.ItemID.ItemID1.Value.ToString(CultureInfo.InvariantCulture)}{item.ItemID.ItemID2.Value.ToString(CultureInfo.InvariantCulture)}";
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.WARNING);
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
                    Item? item = (mfi.DataContext as Item);
                    if (item != null && item.Location != null)
                        clipboardStr = item.Location;
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetCoords_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
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
                    Item? item = (mfi.DataContext as Item);
                    if (item != null)
                        clipboardStr = JsonSerializer.Serialize<Item>(item, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetJson_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
                Utils.AddToClipboard(clipboardStr, false);
            }
        }

        private void mfi_contextMenuGetAllJson_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                var selectedItems = dg_Items.SelectedItems;
                if (selectedItems != null && selectedItems.Count > 0)
                {
                    List<Item>? items = new List<Item>();
                    for (int i = 0; i < selectedItems.Count; i++)
                    {
                        Item? item = (selectedItems[i] as Item);
                        if (item != null)
                            items.Add(item);
                    }
                    if (items.Count > 0)
                    {
                        clipboardStr = JsonSerializer.Serialize<List<Item>>(items, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                        items.Clear();
                        items = null;
                    }
                }
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetAllJson_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
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
                            MainWindow.ShowToast(ASILang.Get("CryopodNotFound"), BackgroundColor.WARNING);
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast(ASILang.Get("ErrorHappened"), BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGoToContainer_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        #endregion
    }
}
