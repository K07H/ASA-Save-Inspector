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
using NetTopologySuite.Index.HPRtree;
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
using System.Xml.Linq;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class DinosPage : Page, INotifyPropertyChanged
    {
        #region Constants

        private const double _scrollBarWidth = 24.0d;
        private const int MAX_PROPERTY_VALUES = 500;

        #endregion

        #region Statics

        private static readonly object lockObject = new object();

        public static DinosPage? _page = null;

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

        public IEnumerable<Dino>? _lastDisplayed = null;
        public ObservableCollection<Dino>? _lastDisplayedVM = null;
        private string? _selectedDinoFilter_Name = null;
        private List<string>? _selectedDinoFilter_Values = new List<string>();

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

            // Add default filters.
            if (!_addedDefaultFilters)
            {
                if (_filters != null)
                {
                    // Add "IsTamed => True".
                    PropertyInfo? isTamedProp = typeof(Dino).GetProperty("IsTamed", BindingFlags.Instance | BindingFlags.Public);
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
                _addedDefaultFilters = true;
            }

            // Set default selected columns.
            if (!_setDefaultSelectedColumns)
            {
                if (_selectedColumns != null && DinoUtils.DefaultSelectedColumns != null && DinoUtils.DefaultSelectedColumns.Count > 0)
                    foreach (string c in DinoUtils.DefaultSelectedColumns)
                        _selectedColumns.Add(c);
                _setDefaultSelectedColumns = true;
            }

            // Set "Include Properties With Many Values" checkbox label.
            cb_IncludePropertiesWithManyValues.Content = $"Include variables with more than {MAX_PROPERTY_VALUES} values (not recommended).";

            // Grab dinos data from settings if not set.
            if (_lastDisplayed == null)
                _lastDisplayed = SettingsPage._dinosData;

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

        private void RefreshPrimarySortLabel() => tb_PrimarySort.Text = $"Primary sort: {_currentSort} {(AscendingSort ? "Ascending" : "Descending")}";
        private void RefreshSecondarySortLabel() => tb_SecondarySort.Text = $"Secondary sort: {_secondaryCurrentSort} {(SecondaryAscendingSort ? "Ascending" : "Descending")}";

        public bool GoToDino(int? id1, int? id2)
        {
            if (id1 != null && id1.HasValue && id2 != null && id2.HasValue && _lastDisplayedVM != null)
            {
                Dino? dino = _lastDisplayedVM.FirstOrDefault(d => (d?.DinoID1 == id1 && d?.DinoID2 == id2), null);
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
#if DEBUG
            Debug.WriteLine($"Dino \"{(point?.Name ?? string.Empty)}\" has been double clicked.");
#endif
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
                MainWindow.UpdateMinimap(dinos.Select(d => {
                    string dinoIDStr = d.GetDinoID();
                    KeyValuePair<double, double> minimapCoords = new KeyValuePair<double, double>(0.0d, 0.0d);
                    if (d.bIsCryopodded != null && d.bIsCryopodded.HasValue && d.bIsCryopodded.Value)
                    {
                        var cryopodPos = Utils.GetCryopodCoordsByUUID(d.CryopodUUID);
                        minimapCoords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, cryopodPos.Item1, cryopodPos.Item2, cryopodPos.Item3);
                    }
                    else
                        minimapCoords = d.GetASIMinimapCoords(); // Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, d.ActorTransformX, d.ActorTransformY, d.ActorTransformZ);
                    return new MapPoint()
                    {
                        ID = dinoIDStr,
                        Name = d.ShortName,
                        Description = $"{(dinoIDStr != "00" ? $"ID: {dinoIDStr}\n" : string.Empty)}{(!string.IsNullOrWhiteSpace(d.TamedName) ? $"Name: {d.TamedName}\n" : string.Empty)}Level: {(d.CurrentLevel != null && d.CurrentLevel.HasValue ? d.CurrentLevel.Value.ToString(CultureInfo.InvariantCulture) : "0")}",
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
                foreach (string defaultOrder in DinoUtils.DefaultColumnsOrder)
                    if (orders.Contains(defaultOrder))
                        defaultOrders.Add(defaultOrder);
                foreach (string? otherOrder in orders)
                    if (!defaultOrders.Contains(otherOrder))
                        defaultOrders.Add(otherOrder);

                for (int i = 0; i < defaultOrders.Count; i++)
                {
                    foreach (DataGridColumn col in dg_Dinos.Columns)
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
                MainWindow._mainWindow.NavView_Navigate(typeof(DinosPage), new EntranceNavigationTransitionInfo());
            });
#pragma warning restore CS1998
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<Dino>? filtered = ApplyFiltering();
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

        //private void dg_Dinos_LayoutUpdated(object sender, object e) => AdjustColumnSizes();

        private void dg_Dinos_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            mfi_contextMenuGetAllJson.Visibility = (dg_Dinos.SelectedItems != null && dg_Dinos.SelectedItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed);

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
            }
            mfi_contextMenuGetCoords.Visibility = ueCoords;
            mfi_contextMenuGoToCryopod.Visibility = cryopodEntries;
            mfi_contextMenuGetCryopodCoords.Visibility = cryopodEntries;
            mfi_contextMenuGetCryopodMapCoords.Visibility = cryopodEntries;
        }

        private void dg_Dinos_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (!_selectedColumns.Contains((string)e.Column.Header))
                e.Cancel = true; //e.Column.Visibility = Visibility.Collapsed;
            e.Column.Header = DinoUtils.GetCleanNameFromPropertyName(e.PropertyName);
        }

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

                tb_DinoFilterType.Text = "Click here...";
                sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
                sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
                sp_FilterByOther.Visibility = Visibility.Collapsed;

                tb_FilterByOther.Text = "";
                btn_AddToDinoFilters.IsEnabled = false;
                if (_selectedDinoFilter_Values != null)
                    _selectedDinoFilter_Values.Clear();
                RefreshSelectedDinoFilterValues();

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
            mf_DinoFilterName.Items.Clear();
            _selectedDinoFilter_Name = null;
            tb_DinoFilterName.Text = "Click here...";

            btn_AddToDinoFilters.IsEnabled = false;
            if (_selectedDinoFilter_Values != null)
                _selectedDinoFilter_Values.Clear();
            RefreshSelectedDinoFilterValues();

            bool includePropertiesWithManyValues = (cb_IncludePropertiesWithManyValues.IsChecked != null && cb_IncludePropertiesWithManyValues.IsChecked.HasValue && cb_IncludePropertiesWithManyValues.IsChecked.Value);
            var dinoProperties = typeof(Dino).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (dinoProperties != null && dinoProperties.Count() > 0)
            {
                Dictionary<string, string> propNames = new Dictionary<string, string>();
                List<string> toAdd = new List<string>();
                foreach (var dinoProperty in dinoProperties)
                {
                    if (includePropertiesWithManyValues || !Utils.PropertyHasMoreValuesThan(SettingsPage._dinosData, dinoProperty, MAX_PROPERTY_VALUES))
                    {
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

        private bool CheckMatchFilter(Dino d)
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

        private IEnumerable<Dino>? ApplyFiltering()
        {
            if (SettingsPage._dinosData == null)
                return null;
            return SettingsPage._dinosData.Where(a => CheckMatchFilter(a));
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
                    sp_ExistingDinoFilters.Children.Add(grd);
                }
        }

        private void cb_IncludePropertiesWithManyValues_CheckedUnchecked(object sender, RoutedEventArgs e) => FillPropertiesDropDown();

        private void btn_AddFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!AddDinoFilterPopup.IsOpen)
            {
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
            if (AddDinoFilterPopup.IsOpen)
            {
                AddDinoFilterPopup.IsOpen = false;
                if (!string.IsNullOrEmpty(_selectedDinoFilter_Name))
                {
                    PropertyInfo? prop = Utils.GetProperty(typeof(Dino), _selectedDinoFilter_Name);
                    if (prop != null)
                    {
                        if (tb_DinoFilterType.Text == "Exact match" &&
                            _selectedDinoFilter_Values != null &&
                            _selectedDinoFilter_Values.Count > 0)
                        {
                            _filters[prop] = new Filter()
                            {
                                FilterType = FilterType.EXACT_MATCH,
                                FilterValues = new List<string>(_selectedDinoFilter_Values)
                            };
                        }
                        else if (tb_FilterByOther.Text != null)
                        {
                            FilterType ft = FilterType.NONE;
                            if (tb_DinoFilterType.Text == "Starting with")
                                ft = FilterType.STARTING_WITH;
                            else if (tb_DinoFilterType.Text == "Ending with")
                                ft = FilterType.ENDING_WITH;
                            else if (tb_DinoFilterType.Text == "Containing")
                                ft = FilterType.CONTAINING;
                            else if (tb_DinoFilterType.Text == "Lower than")
                                ft = FilterType.LOWER_THAN;
                            else if (tb_DinoFilterType.Text == "Greater than")
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
            if (EditDinoFiltersPopup.IsOpen)
                return;

            FillEditDinoFiltersPopup();
            EditDinoFiltersPopup.IsOpen = true;
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

            filterName = DinoUtils.GetPropertyNameFromCleanName(filterName);
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
            FillEditDinoFiltersPopup();
            ApplyFiltersAndSort();
        }

        private void mfi_FilterByExactMatch_Click(object sender, RoutedEventArgs e)
        {
            tb_DinoFilterType.Text = "Exact match";

            tb_FilterByOther.Text = "";
            btn_AddToDinoFilters.IsEnabled = false;
            if (_selectedDinoFilter_Values != null)
                _selectedDinoFilter_Values.Clear();
            RefreshSelectedDinoFilterValues();

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

        private void mfi_FilterByStartingWith_Click(object sender, RoutedEventArgs e)
        {
            tb_DinoFilterType.Text = "Starting with";

            tb_FilterByOther.Text = "";
            btn_AddToDinoFilters.IsEnabled = false;
            if (_selectedDinoFilter_Values != null)
                _selectedDinoFilter_Values.Clear();
            RefreshSelectedDinoFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByEndingWith_Click(object sender, RoutedEventArgs e)
        {
            tb_DinoFilterType.Text = "Ending with";

            tb_FilterByOther.Text = "";
            btn_AddToDinoFilters.IsEnabled = false;
            if (_selectedDinoFilter_Values != null)
                _selectedDinoFilter_Values.Clear();
            RefreshSelectedDinoFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByContaining_Click(object sender, RoutedEventArgs e)
        {
            tb_DinoFilterType.Text = "Containing";

            tb_FilterByOther.Text = "";
            btn_AddToDinoFilters.IsEnabled = false;
            if (_selectedDinoFilter_Values != null)
                _selectedDinoFilter_Values.Clear();
            RefreshSelectedDinoFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByLowerThan_Click(object sender, RoutedEventArgs e)
        {
            tb_DinoFilterType.Text = "Lower than";

            tb_FilterByOther.Text = "";
            btn_AddToDinoFilters.IsEnabled = false;
            if (_selectedDinoFilter_Values != null)
                _selectedDinoFilter_Values.Clear();
            RefreshSelectedDinoFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void mfi_FilterByGreaterThan_Click(object sender, RoutedEventArgs e)
        {
            tb_DinoFilterType.Text = "Greater than";

            tb_FilterByOther.Text = "";
            btn_AddToDinoFilters.IsEnabled = false;
            if (_selectedDinoFilter_Values != null)
                _selectedDinoFilter_Values.Clear();
            RefreshSelectedDinoFilterValues();

            sp_FilterByOther.Visibility = Visibility.Visible;
            sp_FilterByExactMatch.Visibility = Visibility.Collapsed;
            sp_FilterByExactMatchSelection.Visibility = Visibility.Collapsed;
        }

        private void tb_FilterByOther_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_AddToDinoFilters.IsEnabled = (tb_FilterByOther.Text.Length > 0);
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
                    File.WriteAllText(Utils.DinoFiltersPresetsFilePath(), jsonString, Encoding.UTF8);
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

            Type type = typeof(Dino);
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

            string columnsPresetsPath = Utils.DinoColumnsPresetsFilePath();
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
                    File.WriteAllText(Utils.DinoColumnsPresetsFilePath(), jsonString, Encoding.UTF8);
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
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null && dino.DinoID1 != null && dino.DinoID1.HasValue && dino.DinoID2 != null && dino.DinoID2.HasValue)
                        clipboardStr = dino.GetDinoID();
                }
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetID_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            Utils.AddToClipboard(clipboardStr);
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
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetCoords_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            Utils.AddToClipboard(clipboardStr);
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
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetCryopodCoords_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (string.IsNullOrEmpty(clipboardStr))
                MainWindow.ShowToast("Cryopod UE coords not found", BackgroundColor.WARNING);
            Utils.AddToClipboard(clipboardStr);
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
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetCryopodMapCoords_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (string.IsNullOrEmpty(clipboardStr))
                MainWindow.ShowToast("Cryopod GPS coords not found", BackgroundColor.WARNING);
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
                    Dino? dino = (mfi.DataContext as Dino);
                    if (dino != null)
                        clipboardStr = JsonSerializer.Serialize<Dino>(dino, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                }
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetJson_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (string.IsNullOrEmpty(clipboardStr))
                MainWindow.ShowToast("JSON is empty", BackgroundColor.WARNING);
            Utils.AddToClipboard(clipboardStr);
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
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetAllJson_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            if (string.IsNullOrEmpty(clipboardStr))
                MainWindow.ShowToast("JSON is empty", BackgroundColor.WARNING);
            Utils.AddToClipboard(clipboardStr);
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
                                                MainWindow.ShowToast("Cryopod not found, check filters", BackgroundColor.WARNING);
                                        });
                                });
                            }
#pragma warning restore CS1998
                        }
                        else
                            MainWindow.ShowToast("Cryopod not found", BackgroundColor.WARNING);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGoToCryopod_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        #endregion
    }
}
