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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class TribesPage : Page, INotifyPropertyChanged
    {
        #region Constants

        private const double _scrollBarWidth = 24.0d;
        private const int MAX_PROPERTY_VALUES = 500;

        #endregion

        #region Statics

        private static readonly object lockObject = new object();

        public static TribesPage? _page = null;

        private static string? _currentSort = null;
        private static bool _ascendingSort = true;
        private static string? _secondaryCurrentSort = null;
        private static bool _secondaryAscendingSort = true;

        private static bool _addedDefaultFilters = false;
        private static Dictionary<PropertyInfo, Filter> _filters = new Dictionary<PropertyInfo, Filter>();

        private static bool _setDefaultSelectedColumns = false;
        private static List<string> _selectedColumns = new List<string>();

        private static JsonFiltersPreset _defaultFiltersPreset = new JsonFiltersPreset()
        {
            Name = "Default preset",
            Filters = new List<JsonFilter>()
        };

        private static JsonColumnsPreset _defaultColumnsPreset = new JsonColumnsPreset()
        {
            Name = "Default preset",
            Columns = new List<string>()
        };

        #endregion

        #region Properties

        public IEnumerable<Tribe>? _lastDisplayed = null;
        public ObservableCollection<Tribe>? _lastDisplayedVM = null;
        private string? _selectedTribeFilter_Name = null;
        private List<string>? _selectedTribeFilter_Values = new List<string>();

        private List<JsonFiltersPreset> _filtersPresets = new List<JsonFiltersPreset>();
        private JsonFiltersPreset? _selectedFiltersPreset = null;

        private List<JsonColumnsPreset> _columnsPresets = new List<JsonColumnsPreset>();
        private JsonColumnsPreset? _selectedColumnsPreset = null;

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

        public TribesPage()
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

            // Add default filters.
            if (!_addedDefaultFilters)
            {
                /*
                if (_filters != null)
                {
                    // Add "IsTamed => True".
                    PropertyInfo? isTamedProp = typeof(Tribe).GetProperty("IsTamed", BindingFlags.Instance | BindingFlags.Public);
                    if (isTamedProp != null && !_filters.ContainsKey(isTamedProp))
                    {
                        Filter defaultFilter = new Filter() { FilterType = FilterType.EXACT_MATCH, FilterValues = new List<string>() { "True" } };
                        _filters[isTamedProp] = defaultFilter;
                        if (_defaultFiltersPreset?.Filters != null)
                            _defaultFiltersPreset.Filters.Add(new JsonFilter()
                            {
                                PropertyName = isTamedProp.Name,
                                Filter = defaultFilter
                            });
                    }
                }
                */
                _addedDefaultFilters = true;
            }

            // Set default selected columns.
            if (!_setDefaultSelectedColumns)
            {
                if (_selectedColumns != null && TribeUtils.DefaultSelectedColumns != null && TribeUtils.DefaultSelectedColumns.Count > 0)
                    foreach (string c in TribeUtils.DefaultSelectedColumns)
                        _selectedColumns.Add(c);
                _setDefaultSelectedColumns = true;
            }

            // Set "Include Properties With Many Values" checkbox label.
            cb_IncludePropertiesWithManyValues.Content = $"Include variables with more than {MAX_PROPERTY_VALUES} values (not recommended).";

            // Grab tribes data from settings if not set.
            if (_lastDisplayed == null)
                _lastDisplayed = SettingsPage._tribesData;

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
                if (_page.dg_Tribes != null)
                {
                    if (_page.dg_Tribes.ItemsSource != null)
                        _page.dg_Tribes.ItemsSource = null;
                    if (gr_Main != null)
                        gr_Main.Children.Remove(_page.dg_Tribes);
                }
                if (_page._selectedTribeFilter_Values != null)
                {
                    _page._selectedTribeFilter_Values.Clear();
                    _page._selectedTribeFilter_Values = null;
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

        private void RefreshPrimarySortLabel() => tb_PrimarySort.Text = $"Primary sort: {_currentSort} {(AscendingSort ? "Ascending" : "Descending")}";
        private void RefreshSecondarySortLabel() => tb_SecondarySort.Text = $"Secondary sort: {_secondaryCurrentSort} {(SecondaryAscendingSort ? "Ascending" : "Descending")}";

        public bool GoToTribe(int? tribeID)
        {
            if (tribeID != null && tribeID.HasValue && _lastDisplayedVM != null)
            {
                Tribe? tribe = _lastDisplayedVM.FirstOrDefault(d => (d?.TribeID == tribeID), null);
                if (tribe != null)
                {
                    dg_Tribes.SelectedItem = tribe;
                    dg_Tribes.UpdateLayout();
                    dg_Tribes.ScrollIntoView(dg_Tribes.SelectedItem, null);
                    return true;
                }
            }
            return false;
        }

        private bool LastTribeDoubleTap(MapPoint? point)
        {
            return false;
        }

        private void Init(ref IEnumerable<Tribe>? tribes, bool onlyRefresh)
        {
            dg_Tribes.MaxHeight = Math.Max(1.0d, gr_Main.ActualHeight - (tb_Title.ActualHeight + sp_SortAndFilters.ActualHeight + _scrollBarWidth + 4.0d));
            dg_Tribes.MaxWidth = Math.Max(1.0d, gr_Main.ActualWidth - (_scrollBarWidth + 4.0d));
            if (!onlyRefresh && tribes != null)
            {
                if (_lastDisplayedVM != null)
                {
                    _lastDisplayedVM.Clear();
                    _lastDisplayedVM = null;
                }
                _lastDisplayedVM = new ObservableCollection<Tribe>(tribes);
            }
            dg_Tribes.ItemsSource = _lastDisplayedVM;
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(250);
                ReorderColumns();
            });
            if (tribes != null && MainWindow._minimap != null)
                MainWindow.UpdateMinimap(new List<MapPoint>(), LastTribeDoubleTap);
        }

        private void ReorderColumns()
        {
            if (dg_Tribes?.Columns != null && dg_Tribes.Columns.Count > 0)
            {
                List<string?> orders = new List<string?>();
                foreach (DataGridColumn col in dg_Tribes.Columns)
                    if (col != null)
                        orders.Add(col.Header.ToString());
                orders.Sort();

                List<string?> defaultOrders = new List<string?>();
                if (TribeUtils.DefaultColumnsOrder != null && TribeUtils.DefaultColumnsOrder.Count > 0)
                    foreach (string defaultOrder in TribeUtils.DefaultColumnsOrder)
                        if (orders.Contains(defaultOrder))
                            defaultOrders.Add(defaultOrder);
                foreach (string? otherOrder in orders)
                    if (!defaultOrders.Contains(otherOrder))
                        defaultOrders.Add(otherOrder);

                for (int i = 0; i < defaultOrders.Count; i++)
                {
                    foreach (DataGridColumn col in dg_Tribes.Columns)
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
                MainWindow._mainWindow.NavView_Navigate(typeof(TribesPage), new EntranceNavigationTransitionInfo());
            });
#pragma warning restore CS1998
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<Tribe>? filtered = ApplyFiltering();
            if (filtered != null)
                ApplySorting(ref filtered);
            Init(ref filtered, false);
        }

        private readonly Thickness _defaultMarginSortAndFilter = new Thickness(50.0d, 0.0d, 0.0d, 0.0d);
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
                    Grid.SetRow(sp_EditFilters, 0);
                    Grid.SetRow(sp_EditColumns, 1);
                    Grid.SetRow(sp_CurrentSort, 2);
                    Grid.SetColumn(sp_EditFilters, 0);
                    Grid.SetColumn(sp_EditColumns, 0);
                    Grid.SetColumn(sp_CurrentSort, 0);
                }
                else
                {
                    sp_EditColumns.Margin = _defaultMarginSortAndFilter;
                    sp_CurrentSort.Margin = _defaultMarginSortAndFilter;
                    Grid.SetColumn(sp_EditFilters, 0);
                    Grid.SetColumn(sp_EditColumns, 1);
                    Grid.SetColumn(sp_CurrentSort, 2);
                    Grid.SetRow(sp_EditFilters, 0);
                    Grid.SetRow(sp_EditColumns, 0);
                    Grid.SetRow(sp_CurrentSort, 0);
                }
            }
        }

        private void page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustToSizeChange();
            Init(ref _lastDisplayed, true);
        }

        //private void dg_Tribes_LayoutUpdated(object sender, object e) => AdjustColumnSizes();

        private void dg_Tribes_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            mfi_contextMenuGetAllJson.Visibility = (dg_Tribes.SelectedItems != null && dg_Tribes.SelectedItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed);
        }

        private void dg_Tribes_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (!_selectedColumns.Contains((string)e.Column.Header))
                e.Cancel = true; //e.Column.Visibility = Visibility.Collapsed;
            e.Column.Header = TribeUtils.GetCleanNameFromPropertyName(e.PropertyName);
        }

        #endregion

        #region Sorting

        private object? GetTribePropertyValueByName(string cleanName, Tribe d)
        {
            PropertyInfo? prop = Utils.GetProperty(typeof(Tribe), TribeUtils.GetPropertyNameFromCleanName(cleanName));
            if (prop == null)
                return null;
            return prop.GetValue(d);
        }

        private void SimpleSort(ref IEnumerable<Tribe> tribes, string cleanName)
        {
            if (AscendingSort)
                tribes = tribes.OrderBy(o => GetTribePropertyValueByName(cleanName, o));
            else
                tribes = tribes.OrderByDescending(o => GetTribePropertyValueByName(cleanName, o));
        }

#pragma warning disable CS8604
        private void DoubleSort(ref IEnumerable<Tribe> tribes, string cleanName)
        {
            if (AscendingSort)
            {
                if (SecondaryAscendingSort)
                    tribes = tribes.OrderBy(o => GetTribePropertyValueByName(cleanName, o)).ThenBy(o => GetTribePropertyValueByName(SecondaryCurrentSort, o));
                else
                    tribes = tribes.OrderBy(o => GetTribePropertyValueByName(cleanName, o)).ThenByDescending(o => GetTribePropertyValueByName(SecondaryCurrentSort, o));
            }
            else
            {
                if (SecondaryAscendingSort)
                    tribes = tribes.OrderByDescending(o => GetTribePropertyValueByName(cleanName, o)).ThenBy(o => GetTribePropertyValueByName(SecondaryCurrentSort, o));
                else
                    tribes = tribes.OrderByDescending(o => GetTribePropertyValueByName(cleanName, o)).ThenByDescending(o => GetTribePropertyValueByName(SecondaryCurrentSort, o));
            }
        }
#pragma warning restore CS8604

        private void SortDataGrid(ref IEnumerable<Tribe> tribes, string cleanName)
        {
            if (tribes == null)
                return;

            if (!string.IsNullOrWhiteSpace(CurrentSort) && SecondaryCurrentSort != null)
                DoubleSort(ref tribes, cleanName);
            else
                SimpleSort(ref tribes, cleanName);
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

        private void ApplySorting(ref IEnumerable<Tribe> tribes)
        {
            if (CurrentSort != null)
            {
                SortDataGrid(ref tribes, CurrentSort);
                RefreshPrimarySortLabel();
                tb_PrimarySort.Visibility = Visibility.Visible;
                if (SecondaryCurrentSort != null)
                {
                    RefreshSecondarySortLabel();
                    tb_SecondarySort.Visibility = Visibility.Visible;
                }
            }
        }

        private void dg_Tribes_Sorting(object sender, DataGridColumnEventArgs e)
        {
            if (e?.Column?.Header != null)
            {
                string? cleanName = e.Column.Header.ToString();
                if (SettingsPage._tribesData != null && cleanName != null)
                {
                    SetSorting(cleanName);
                    ApplyFiltersAndSort();
                }
            }
        }

        #endregion

        #region Filtering

        private void RefreshSelectedTribeFilterValues()
        {
            tb_tribeFilterValues.Text = (_selectedTribeFilter_Values != null ? string.Join(", ", _selectedTribeFilter_Values) : string.Empty);
            b_tribeFilterValues.Visibility = (tb_tribeFilterValues.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed);
        }

        [RelayCommand]
        private void TribeFilterValueSelect(string filterValue)
        {
            if (!string.IsNullOrWhiteSpace(filterValue) && _selectedTribeFilter_Values != null)
            {
                if (_selectedTribeFilter_Values.Contains(filterValue))
                    _selectedTribeFilter_Values.Remove(filterValue);
                else
                    _selectedTribeFilter_Values.Add(filterValue);
                RefreshSelectedTribeFilterValues();
                btn_AddToTribeFilters.IsEnabled = (_selectedTribeFilter_Values.Count > 0);
            }
        }

        [RelayCommand]
        private void TribeFilterSelect(string propName)
        {
            if (!string.IsNullOrWhiteSpace(propName))
            {
                _selectedTribeFilter_Name = propName;
                tb_TribeFilterName.Text = TribeUtils.GetCleanNameFromPropertyName(_selectedTribeFilter_Name);

                tb_TribeFilterType.Text = "Click here...";
                sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
                sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
                sp_FilterByOther.Visibility = Visibility.Collapsed;

                tb_FilterByOther.Text = "";
                btn_AddToTribeFilters.IsEnabled = false;
                if (_selectedTribeFilter_Values != null)
                    _selectedTribeFilter_Values.Clear();
                RefreshSelectedTribeFilterValues();

                PropertyInfo? foundProperty = Utils.GetProperty(typeof(Tribe), propName);
                if (foundProperty != null)
                {
                    string propType = foundProperty.PropertyType.ToString();
                    if (Utils.FilterBooleanTypes.Contains(propType))
                    {
                        mfi_FilterByExactMatch.Visibility = Visibility.Visible;
                        mfi_FilterByStartingWith.Visibility = Visibility.Collapsed;
                        mfi_FilterByEndingWith.Visibility = Visibility.Collapsed;
                        mfi_FilterByContaining.Visibility = Visibility.Collapsed;
                        mfi_FilterByLowerThan.Visibility = Visibility.Collapsed;
                        mfi_FilterByGreaterThan.Visibility = Visibility.Collapsed;
                    }
                    else if (Utils.FilterNumberTypes.Contains(propType))
                    {
                        mfi_FilterByExactMatch.Visibility = Visibility.Visible;
                        mfi_FilterByStartingWith.Visibility = Visibility.Visible;
                        mfi_FilterByEndingWith.Visibility = Visibility.Visible;
                        mfi_FilterByContaining.Visibility = Visibility.Visible;
                        mfi_FilterByLowerThan.Visibility = Visibility.Visible;
                        mfi_FilterByGreaterThan.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        mfi_FilterByExactMatch.Visibility = Visibility.Visible;
                        mfi_FilterByStartingWith.Visibility = Visibility.Visible;
                        mfi_FilterByEndingWith.Visibility = Visibility.Visible;
                        mfi_FilterByContaining.Visibility = Visibility.Visible;
                        mfi_FilterByLowerThan.Visibility = Visibility.Collapsed;
                        mfi_FilterByGreaterThan.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void FillPropertiesDropDown()
        {
            mf_TribeFilterName.Items.Clear();
            _selectedTribeFilter_Name = null;
            tb_TribeFilterName.Text = "Click here...";

            btn_AddToTribeFilters.IsEnabled = false;
            if (_selectedTribeFilter_Values != null)
                _selectedTribeFilter_Values.Clear();
            RefreshSelectedTribeFilterValues();

            bool includePropertiesWithManyValues = (cb_IncludePropertiesWithManyValues.IsChecked != null && cb_IncludePropertiesWithManyValues.IsChecked.HasValue && cb_IncludePropertiesWithManyValues.IsChecked.Value);
            var tribeProperties = typeof(Tribe).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (tribeProperties != null && tribeProperties.Count() > 0)
            {
                Dictionary<string, string> propNames = new Dictionary<string, string>();
                List<string> toAdd = new List<string>();
                foreach (var tribeProperty in tribeProperties)
                {
                    if (includePropertiesWithManyValues || !Utils.PropertyHasMoreValuesThan(SettingsPage._tribesData, tribeProperty, MAX_PROPERTY_VALUES))
                    {
                        string propName = tribeProperty.Name;
                        if (propName != null)
                        {
                            string? cleanName = TribeUtils.GetCleanNameFromPropertyName(propName);
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
                            mf_TribeFilterName.Items.Add(new MenuFlyoutItem
                            {
                                Text = cleanName,
                                Command = TribeFilterSelectCommand,
                                CommandParameter = propNames[cleanName]
                            });
                        }
                }
                propNames.Clear();
                toAdd.Clear();
            }
        }

        private bool CheckMatchFilter(Tribe d)
        {
            if (_filters == null || _filters.Count <= 0)
                return true;

            foreach (var filter in _filters)
                if (filter.Key != null && filter.Value != null)
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

        private IEnumerable<Tribe>? ApplyFiltering()
        {
            if (SettingsPage._tribesData == null)
                return null;
            return SettingsPage._tribesData.Where(a => CheckMatchFilter(a));
        }

        private void FillEditTribeFiltersPopup()
        {
            sp_ExistingTribeFilters.Children.Clear();
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
                        Text = "Filter by",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
                    };
                    TextBlock tb2 = new TextBlock()
                    {
                        FontSize = 16.0d,
                        TextWrapping = TextWrapping.Wrap,
                        Text = $"{TribeUtils.GetCleanNameFromPropertyName(filter.Key.Name)}",
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

                    string tbFilterTypeStr = "with value";
                    string tbValueStr = "";
                    if (filter.Value.FilterType == FilterType.EXACT_MATCH)
                    {
                        tbFilterTypeStr = "with value(s)";
                        if (filter.Value.FilterValues != null)
                            tbValueStr = string.Join(", ", filter.Value.FilterValues);
                    }
                    else
                    {
                        if (filter.Value.FilterType == FilterType.STARTING_WITH)
                            tbFilterTypeStr = "starting with";
                        else if (filter.Value.FilterType == FilterType.ENDING_WITH)
                            tbFilterTypeStr = "ending with";
                        else if (filter.Value.FilterType == FilterType.CONTAINING)
                            tbFilterTypeStr = "containing";
                        else if (filter.Value.FilterType == FilterType.LOWER_THAN)
                            tbFilterTypeStr = "lower than";
                        else if (filter.Value.FilterType == FilterType.GREATER_THAN)
                            tbFilterTypeStr = "greater than";
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
                        Width = 90.0d,
                        Content = "Remove",
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
                    sp_ExistingTribeFilters.Children.Add(grd);
                }
        }

        private void cb_IncludePropertiesWithManyValues_CheckedUnchecked(object sender, RoutedEventArgs e) => FillPropertiesDropDown();

        private void btn_AddFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!AddTribeFilterPopup.IsOpen)
            {
                FillPropertiesDropDown();
                AddTribeFilterPopup.IsOpen = true;
            }
        }

        private void btn_CloseTribeFiltersPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddTribeFilterPopup.IsOpen)
                AddTribeFilterPopup.IsOpen = false;
        }

        private void btn_AddToTribeFilters_Click(object sender, RoutedEventArgs e)
        {
            if (AddTribeFilterPopup.IsOpen)
            {
                AddTribeFilterPopup.IsOpen = false;
                if (!string.IsNullOrEmpty(_selectedTribeFilter_Name))
                {
                    PropertyInfo? prop = Utils.GetProperty(typeof(Tribe), _selectedTribeFilter_Name);
                    if (prop != null)
                    {
                        if (tb_TribeFilterType.Text == "Exact match" &&
                            _selectedTribeFilter_Values != null &&
                            _selectedTribeFilter_Values.Count > 0)
                        {
                            _filters[prop] = new Filter()
                            {
                                FilterType = FilterType.EXACT_MATCH,
                                FilterValues = new List<string>(_selectedTribeFilter_Values)
                            };
                        }
                        else if (tb_FilterByOther.Text != null)
                        {
                            FilterType ft = FilterType.NONE;
                            if (tb_TribeFilterType.Text == "Starting with")
                                ft = FilterType.STARTING_WITH;
                            else if (tb_TribeFilterType.Text == "Ending with")
                                ft = FilterType.ENDING_WITH;
                            else if (tb_TribeFilterType.Text == "Containing")
                                ft = FilterType.CONTAINING;
                            else if (tb_TribeFilterType.Text == "Lower than")
                                ft = FilterType.LOWER_THAN;
                            else if (tb_TribeFilterType.Text == "Greater than")
                                ft = FilterType.GREATER_THAN;
                            if (ft != FilterType.NONE)
                            {
                                _filters[prop] = new Filter()
                                {
                                    FilterType = ft,
                                    FilterValue = tb_FilterByOther.Text
                                };
                            }
                        }
                        ApplyFiltersAndSort();
                    }
                }
            }
        }

        private void btn_EditFilters_Click(object sender, RoutedEventArgs e)
        {
            if (EditTribeFiltersPopup.IsOpen)
                return;

            FillEditTribeFiltersPopup();
            EditTribeFiltersPopup.IsOpen = true;
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

            filterName = TribeUtils.GetPropertyNameFromCleanName(filterName);
            if (filterName == null)
                return;

            if (_filters != null && _filters.Count > 0)
            {
                PropertyInfo? toDel = null;
                for (int i = 0; i < _filters.Count; i++)
                {
                    var filter = _filters.ElementAt(i);
                    if (filter.Key != null && string.Compare(filter.Key.Name, filterName, StringComparison.InvariantCulture) == 0)
                    {
                        toDel = filter.Key;
                        break;
                    }
                }
                if (toDel != null)
                {
                    _filters.Remove(toDel);
                    FillEditTribeFiltersPopup();
                    ApplyFiltersAndSort();
                }
            }
        }

        private void btn_CloseEditTribeFiltersPopup_Click(object sender, RoutedEventArgs e)
        {
            if (EditTribeFiltersPopup.IsOpen)
                EditTribeFiltersPopup.IsOpen = false;
        }

        private void btn_RemoveAllTribeFilters_Click(object sender, RoutedEventArgs e)
        {
            _filters.Clear();
            FillEditTribeFiltersPopup();
            ApplyFiltersAndSort();
        }

        private void mfi_FilterByExactMatch_Click(object sender, RoutedEventArgs e)
        {
            tb_TribeFilterType.Text = "Exact match";

            tb_FilterByOther.Text = "";
            btn_AddToTribeFilters.IsEnabled = false;
            if (_selectedTribeFilter_Values != null)
                _selectedTribeFilter_Values.Clear();
            RefreshSelectedTribeFilterValues();

            if (!string.IsNullOrWhiteSpace(_selectedTribeFilter_Name))
            {
                PropertyInfo? foundProperty = Utils.GetProperty(typeof(Tribe), _selectedTribeFilter_Name);
                List<string> propertiesValues = Utils.GetPropertyValues(SettingsPage._tribesData, foundProperty, MAX_PROPERTY_VALUES);
                mf_TribeFilterValue.Items.Clear();
                if (propertiesValues.Count > 0)
                {
                    propertiesValues.Sort();
                    foreach (string val in propertiesValues)
                        if (val != null)
                        {
                            mf_TribeFilterValue.Items.Add(new MenuFlyoutItem
                            {
                                Text = val,
                                Command = TribeFilterValueSelectCommand,
                                CommandParameter = val
                            });
                        }
                }
            }

            sp_FilterByExactMatch.Visibility = Visibility.Visible;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Visible;
            sp_FilterByOther.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByStartingWith_Click(object sender, RoutedEventArgs e)
        {
            tb_TribeFilterType.Text = "Starting with";

            tb_FilterByOther.Text = "";
            btn_AddToTribeFilters.IsEnabled = false;
            if (_selectedTribeFilter_Values != null)
                _selectedTribeFilter_Values.Clear();
            RefreshSelectedTribeFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByEndingWith_Click(object sender, RoutedEventArgs e)
        {
            tb_TribeFilterType.Text = "Ending with";

            tb_FilterByOther.Text = "";
            btn_AddToTribeFilters.IsEnabled = false;
            if (_selectedTribeFilter_Values != null)
                _selectedTribeFilter_Values.Clear();
            RefreshSelectedTribeFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByContaining_Click(object sender, RoutedEventArgs e)
        {
            tb_TribeFilterType.Text = "Containing";

            tb_FilterByOther.Text = "";
            btn_AddToTribeFilters.IsEnabled = false;
            if (_selectedTribeFilter_Values != null)
                _selectedTribeFilter_Values.Clear();
            RefreshSelectedTribeFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByLowerThan_Click(object sender, RoutedEventArgs e)
        {
            tb_TribeFilterType.Text = "Lower than";

            tb_FilterByOther.Text = "";
            btn_AddToTribeFilters.IsEnabled = false;
            if (_selectedTribeFilter_Values != null)
                _selectedTribeFilter_Values.Clear();
            RefreshSelectedTribeFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByGreaterThan_Click(object sender, RoutedEventArgs e)
        {
            tb_TribeFilterType.Text = "Greater than";

            tb_FilterByOther.Text = "";
            btn_AddToTribeFilters.IsEnabled = false;
            if (_selectedTribeFilter_Values != null)
                _selectedTribeFilter_Values.Clear();
            RefreshSelectedTribeFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void tb_FilterByOther_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_AddToTribeFilters.IsEnabled = (tb_FilterByOther.Text.Length > 0);
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
            var tribeProperties = typeof(Tribe).GetProperties();
            if (tribeProperties != null && tribeProperties.Count() > 0)
            {
                List<KeyValuePair<string, string>>? props = new List<KeyValuePair<string, string>>();
                foreach (var tribeProperty in tribeProperties)
                {
                    string propName = tribeProperty.Name;
                    if (!string.IsNullOrEmpty(propName))
                    {
                        string? cleanName = TribeUtils.GetCleanNameFromPropertyName(propName);
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

            string filtersPresetsPath = Utils.TribeFiltersPresetsFilePath();
            if (!File.Exists(filtersPresetsPath))
                return;

            string filtersPresetsJson = File.ReadAllText(filtersPresetsPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(filtersPresetsJson))
                return;

            try
            {
                List<JsonFiltersPreset>? jsonFiltersPresets = JsonSerializer.Deserialize<List<JsonFiltersPreset>>(filtersPresetsJson);
                if (jsonFiltersPresets != null && jsonFiltersPresets.Count > 0)
                    _filtersPresets = jsonFiltersPresets;
            }
            catch (Exception ex)
            {
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
                    File.WriteAllText(Utils.TribeFiltersPresetsFilePath(), jsonString, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SaveFiltersPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        [RelayCommand]
        private void FiltersPresetSelect(JsonFiltersPreset? preset)
        {
            _selectedFiltersPreset = null;
            tb_ExistingFiltersPreset.Text = "Click here...";
            btn_LoadFiltersPreset.IsEnabled = false;
            btn_RemoveFiltersPreset.IsEnabled = false;
            tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
            tb_FiltersPresetHasBeenLoaded.Visibility = Visibility.Collapsed;

            if (preset == null)
                return;

            _selectedFiltersPreset = preset;
            tb_ExistingFiltersPreset.Text = preset.Name;
            btn_LoadFiltersPreset.IsEnabled = true;
            btn_RemoveFiltersPreset.IsEnabled = (string.Compare(preset.Name, "Default preset", StringComparison.InvariantCulture) != 0);
        }

        private void FillFiltersPresetsDropDown()
        {
            mf_ExistingFiltersPresets.Items.Clear();
            mf_ExistingFiltersPresets.Items.Add(new MenuFlyoutItem
            {
                Text = "Default preset",
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
            tb_ExistingFiltersPreset.Text = "Click here...";
            btn_LoadFiltersPreset.IsEnabled = false;
            btn_RemoveFiltersPreset.IsEnabled = false;
            tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
            tb_FiltersPresetHasBeenLoaded.Visibility = Visibility.Collapsed;

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
            tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(tb_FiltersPresetName.Text))
                return;
            if (string.Compare(tb_FiltersPresetName.Text, "Default preset", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Visible;
                Logger.Instance.Log("This preset name already exists.", Logger.LogLevel.WARNING);
                return;
            }

            foreach (var preset in _filtersPresets)
                if (preset != null && preset.Name != null && string.Compare(preset.Name, tb_FiltersPresetName.Text, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Visible;
                    Logger.Instance.Log("This preset name already exists.", Logger.LogLevel.WARNING);
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
                    tb_ExistingFiltersPreset.Text = "Click here...";
                    btn_LoadFiltersPreset.IsEnabled = false;
                    btn_RemoveFiltersPreset.IsEnabled = false;
                    tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
                    tb_FiltersPresetHasBeenLoaded.Visibility = Visibility.Collapsed;

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
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in btn_SaveCurrentFilters_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void btn_LoadFiltersPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiltersPreset == null || _selectedFiltersPreset.Filters == null || _selectedFiltersPreset.Filters.Count <= 0)
                return;

            Type type = typeof(Tribe);
            _filters.Clear();
            foreach (var filter in _selectedFiltersPreset.Filters)
                if (filter != null && !string.IsNullOrEmpty(filter.PropertyName) && filter.Filter != null)
                {
                    PropertyInfo? prop = Utils.GetProperty(type, filter.PropertyName);
                    if (prop != null)
                        _filters.Add(prop, filter.Filter);
                }
            tb_FiltersPresetHasBeenLoaded.Visibility = Visibility.Visible;
            ApplyFiltersAndSort();
        }

        private void btn_RemoveFiltersPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiltersPreset == null || string.IsNullOrEmpty(_selectedFiltersPreset.Name))
                return;

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
                    tb_ExistingFiltersPreset.Text = "Click here...";
                    btn_LoadFiltersPreset.IsEnabled = false;
                    btn_RemoveFiltersPreset.IsEnabled = false;
                    tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
                    tb_FiltersPresetHasBeenLoaded.Visibility = Visibility.Collapsed;

                    _filtersPresets.RemoveAt(toDel);
                    SaveFiltersPresets();
                    FillFiltersPresetsDropDown();
                }
                else
                    Logger.Instance.Log($"Preset \"{_selectedFiltersPreset.Name}\" not found.", Logger.LogLevel.WARNING);
            }
        }

        private void tb_FiltersPresetName_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_SaveCurrentFilters.IsEnabled = (tb_FiltersPresetName.Text.Length > 0);
            tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
        }

        private void mfi_DefaultFiltersPreset_Click(object sender, RoutedEventArgs e)
        {
            tb_ExistingFiltersPreset.Text = "Default preset";
            btn_LoadFiltersPreset.IsEnabled = true;
            btn_RemoveFiltersPreset.IsEnabled = false;
            tb_FiltersPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
            tb_FiltersPresetHasBeenLoaded.Visibility = Visibility.Collapsed;
            _selectedFiltersPreset = _defaultFiltersPreset;
        }

        #endregion

        #region Columns preset

        public void LoadColumnsPresets()
        {
            _columnsPresets.Clear();

            string columnsPresetsPath = Utils.TribeColumnsPresetsFilePath();
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
                    File.WriteAllText(Utils.TribeColumnsPresetsFilePath(), jsonString, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in SaveColumnsPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        [RelayCommand]
        private void ColumnsPresetSelect(JsonColumnsPreset? preset)
        {
            _selectedColumnsPreset = null;
            tb_ExistingColumnsPreset.Text = "Click here...";
            btn_LoadColumnsPreset.IsEnabled = false;
            btn_RemoveColumnsPreset.IsEnabled = false;
            tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
            tb_ColumnsPresetHasBeenLoaded.Visibility = Visibility.Collapsed;

            if (preset == null)
                return;

            _selectedColumnsPreset = preset;
            tb_ExistingColumnsPreset.Text = preset.Name;
            btn_LoadColumnsPreset.IsEnabled = true;
            btn_RemoveColumnsPreset.IsEnabled = (string.Compare(preset.Name, "Default preset", StringComparison.InvariantCulture) != 0);
        }

        private void FillColumnsPresetsDropDown()
        {
            mf_ExistingColumnsPresets.Items.Clear();
            mf_ExistingColumnsPresets.Items.Add(new MenuFlyoutItem
            {
                Text = "Default preset",
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
            tb_ExistingColumnsPreset.Text = "Click here...";
            btn_LoadColumnsPreset.IsEnabled = false;
            btn_RemoveColumnsPreset.IsEnabled = false;
            tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
            tb_ColumnsPresetHasBeenLoaded.Visibility = Visibility.Collapsed;

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
            tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(tb_ColumnsPresetName.Text))
                return;
            if (string.Compare(tb_ColumnsPresetName.Text, "Default preset", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Visible;
                Logger.Instance.Log($"This preset name already exists.", Logger.LogLevel.WARNING);
                return;
            }

            foreach (var preset in _columnsPresets)
                if (preset != null && preset.Name != null && string.Compare(preset.Name, tb_ColumnsPresetName.Text, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Visible;
                    Logger.Instance.Log($"This preset name already exists.", Logger.LogLevel.WARNING);
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
                    tb_ExistingColumnsPreset.Text = "Click here...";
                    btn_LoadColumnsPreset.IsEnabled = false;
                    btn_RemoveColumnsPreset.IsEnabled = false;
                    tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
                    tb_ColumnsPresetHasBeenLoaded.Visibility = Visibility.Collapsed;

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
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in btn_SaveCurrentColumns_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void btn_LoadColumnsPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColumnsPreset == null || _selectedColumnsPreset.Columns == null || _selectedColumnsPreset.Columns.Count <= 0)
                return;

            _selectedColumns.Clear();
            foreach (var column in _selectedColumnsPreset.Columns)
                if (column != null)
                    _selectedColumns.Add(column);
            tb_ColumnsPresetHasBeenLoaded.Visibility = Visibility.Visible;
        }

        private void btn_RemoveColumnsPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColumnsPreset == null || string.IsNullOrEmpty(_selectedColumnsPreset.Name))
                return;

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
                    tb_ExistingColumnsPreset.Text = "Click here...";
                    btn_LoadColumnsPreset.IsEnabled = false;
                    btn_RemoveColumnsPreset.IsEnabled = false;
                    tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
                    tb_ColumnsPresetHasBeenLoaded.Visibility = Visibility.Collapsed;

                    _columnsPresets.RemoveAt(toDel);
                    SaveColumnsPresets();
                    FillColumnsPresetsDropDown();
                }
                else
                    Logger.Instance.Log($"Preset \"{_selectedColumnsPreset.Name}\" not found.", Logger.LogLevel.WARNING);
            }
        }

        private void tb_ColumnsPresetName_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_SaveCurrentColumns.IsEnabled = (tb_ColumnsPresetName.Text.Length > 0);
            tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
        }

        private void mfi_DefaultColumnsPreset_Click(object sender, RoutedEventArgs e)
        {
            tb_ExistingColumnsPreset.Text = "Default preset";
            btn_LoadColumnsPreset.IsEnabled = true;
            btn_RemoveColumnsPreset.IsEnabled = false;
            tb_ColumnsPresetNameAlreadyExists.Visibility = Visibility.Collapsed;
            tb_ColumnsPresetHasBeenLoaded.Visibility = Visibility.Collapsed;
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
                    Tribe? tribe = (mfi.DataContext as Tribe);
                    if (tribe != null && tribe.TribeID != null && tribe.TribeID.HasValue)
                        clipboardStr = tribe.TribeID.Value.ToString(CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetID_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            Utils.AddToClipboard(clipboardStr);
        }

        private void mfi_contextMenuGetJson_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Tribe? tribe = (mfi.DataContext as Tribe);
                    if (tribe != null)
                        clipboardStr = JsonSerializer.Serialize<Tribe>(tribe, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                }
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetJson_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            Utils.AddToClipboard(clipboardStr);
        }

        private void mfi_contextMenuGetAllJson_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                var selectedTribes = dg_Tribes.SelectedItems;
                if (selectedTribes != null && selectedTribes.Count > 0)
                {
                    List<Tribe>? tribes = new List<Tribe>();
                    for (int i = 0; i < selectedTribes.Count; i++)
                    {
                        Tribe? tribe = (selectedTribes[i] as Tribe);
                        if (tribe != null)
                            tribes.Add(tribe);
                    }
                    if (tribes.Count > 0)
                    {
                        clipboardStr = JsonSerializer.Serialize<List<Tribe>>(tribes, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                        tribes.Clear();
                        tribes = null;
                    }
                }
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetAllJson_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            Utils.AddToClipboard(clipboardStr);
        }

        private void mfi_contextMenuGetLogs_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Tribe? tribe = (mfi.DataContext as Tribe);
                    if (tribe != null && tribe.TribeLog != null && tribe.TribeLog.Count > 0)
                        clipboardStr = (tribe.TribeLogsFormatted() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetLogs_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            Utils.AddToClipboard(clipboardStr);
        }

        #endregion
    }
}
