using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WinUIEx;

namespace ASA_Save_Inspector.Pages
{
    public sealed class DataPageControls
    {
        public required Grid MainGrid { get; init; }                         // gr_Main
        public required TextBlock Title { get; init; }                       // tb_Title
        public required FrameworkElement SortAndFilters { get; init; }       // sp_SortAndFilters
        public required FrameworkElement EditFilters { get; init; }          // sp_EditFilters
        public required FrameworkElement EditFiltersGroup { get; init; }     // sp_EditFiltersGroup
        public required FrameworkElement EditColumns { get; init; }          // sp_EditColumns
        public required FrameworkElement CurrentSort { get; init; }          // sp_CurrentSort
        public required StackPanel ExistingColumns { get; init; }            // sp_ExistingColumns
        public required TextBlock PrimarySort { get; init; }                 // tb_PrimarySort
        public required TextBlock SecondarySort { get; init; }               // tb_SecondarySort
        public required Run PrimarySortRun { get; init; }                    // run_PrimarySort
        public required Run SecondarySortRun { get; init; }                  // run_SecondarySort
        public required Run NbLinesSelectedRun { get; init; }                // run_NbLinesSelected
        public required DataGrid DataGrid { get; init; }                     // dg_Data
        public required MenuFlyoutItem GetAllJsonMenuItem { get; init; }     // mfi_contextMenuGetAllJson
        public required Popup ModifyColumnsPopup { get; init; }              // ModifyColumnsPopup
        public required Popup ColumnsPresetsPopup { get; init; }             // ColumnsPresetsPopup
        public required TextBox ColumnsPresetName { get; init; }             // tb_ColumnsPresetName
        public required Button SaveCurrentColumns { get; init; }             // btn_SaveCurrentColumns
        public required TextBlock ExistingColumnsPreset { get; init; }       // tb_ExistingColumnsPreset
        public required MenuFlyout ExistingColumnsPresets { get; init; }     // mf_ExistingColumnsPresets
        public required Button LoadColumnsPreset { get; init; }              // btn_LoadColumnsPreset
        public required Button RemoveColumnsPreset { get; init; }            // btn_RemoveColumnsPreset
        public required Button SetDefaultColumnsPreset { get; init; }        // btn_SetDefaultColumnsPreset
        public required ComboBox ExistingQueries { get; init; }              // cbb_ExistingQueries
        public required Button LoadQuery { get; init; }                      // btn_LoadQuery
        public required Button EditQuery { get; init; }                      // btn_EditQuery
        public required Button DeleteQuery { get; init; }                    // btn_DeleteQuery
        public required TextBox QuickSearch { get; init; }                   // tb_QuickSearch
    }

    public abstract class DataPageBase<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> : Page, INotifyPropertyChanged where T : class
    {
        #region Constants

        protected const double _scrollBarWidth = 24.0d;

        #endregion

        #region Statics

        private static readonly object lockObject = new object();

        /// <summary>Current instance of the page (each page exposes it as its own static "_page").</summary>
        protected static DataPageBase<T>? _currentPage = null;

        private static string? _currentSort = null;
        private static bool _ascendingSort = true;
        private static string? _secondaryCurrentSort = null;
        private static bool _secondaryAscendingSort = true;

        private static bool _setDefaultSelectedColumns = false;
        protected static List<string> _selectedColumns = new List<string>();

        private static JsonColumnsPreset? _defaultColumnsPresetValue = null;

        /// <summary>Text of the quick search field (kept when the page is re-created, ex: after changing the displayed columns).</summary>
        private static string _quickSearchText = string.Empty;

        #endregion

        #region Page specific members

        /// <summary>Search type of the page (ex: SearchType.TRIBES).</summary>
        protected abstract SearchType PageSearchType { get; }
        /// <summary>All the loaded data of the page (ex: SettingsPage._tribesData).</summary>
        protected abstract List<T>? AllData { get; }
        /// <summary>Label shown in the "page not found" toast (ex: "Tribes").</summary>
        protected abstract string PageLabel { get; }
        protected abstract IReadOnlyList<string> DefaultSelectedColumns { get; }
        protected abstract IReadOnlyList<string> DefaultColumnsOrder { get; }
        protected abstract string? GetCleanNameFromPropertyName(string? propertyName);
        protected abstract string? GetPropertyNameFromCleanName(string? cleanName);
        protected abstract string ColumnsOrderFilePath { get; }
        protected abstract string ColumnsPresetsFilePath { get; }
        /// <summary>Name of the default columns preset saved in the settings (ex: SettingsPage._defaultColumnsPreset_Tribes).</summary>
        protected abstract string? DefaultColumnsPresetName { get; set; }
        /// <summary>ID copied to the clipboard by the "Get ID" context menu entry.</summary>
        protected abstract string? GetObjectID(T obj);
        /// <summary>Updates the WindowWidth and WindowHeight properties of the page (they are declared in the page because XAML bindings use them).</summary>
        protected abstract void SetWindowSize(int width, int height);

        /// <summary>Updates the minimap with the displayed objects (no point by default).</summary>
        protected virtual void RefreshMinimap(IEnumerable<T> displayed) => MainWindow.UpdateMinimap(new List<MapPoint>(), OnMinimapDoubleTap);

        /// <summary>Called when a point is double tapped on the minimap.</summary>
        protected virtual bool OnMinimapDoubleTap(MapPoint? point) => false;

        /// <summary>Called after the common handling of PointerReleased on the data grid (ex: to show/hide page specific context menu entries).</summary>
        protected virtual void OnDataGridPointerReleased(PointerRoutedEventArgs e) { }

        #endregion

        #region Properties

        public IEnumerable<T>? _lastDisplayed = null;
        public ObservableCollection<T>? _lastDisplayedVM = null;

        private List<JsonColumnsPreset> _columnsPresets = new List<JsonColumnsPreset>();
        private JsonColumnsPreset? _selectedColumnsPreset = null;

        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _quickSearchTimer = null;
        private const int QUICK_SEARCH_DELAY_MS = 400;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Named controls of the page (set by InitializePage).</summary>
        protected DataPageControls UI { get; private set; } = null!;

        /// <summary>Command of the columns presets drop down entries (was generated by [RelayCommand] in each page).</summary>
        public IRelayCommand<JsonColumnsPreset?> ColumnsPresetSelectCommand { get; }

        private JsonColumnsPreset DefaultColumnsPreset => (_defaultColumnsPresetValue ??= new JsonColumnsPreset() { Name = ASILang.Get("DefaultPreset"), Columns = new List<string>(DefaultSelectedColumns) });

        private string? CurrentSort
        {
            get => _currentSort;
            set
            {
                _currentSort = value;
                if (_currentSort != null)
                {
                    RefreshPrimarySortLabel();
                    UI.PrimarySort.Visibility = Visibility.Visible;
                }
                else
                    UI.PrimarySort.Visibility = Visibility.Collapsed;
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
                    UI.SecondarySort.Visibility = Visibility.Visible;
                }
                else
                    UI.SecondarySort.Visibility = Visibility.Collapsed;
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

        protected DataPageBase()
        {
            ColumnsPresetSelectCommand = new RelayCommand<JsonColumnsPreset?>(ColumnsPresetSelect);
        }

        protected void InitializePage(DataPageControls controls)
        {
            UI = controls;

            // Restore the quick search text (the TextChanged event it raises is ignored since the text did not change).
            UI.QuickSearch.Text = _quickSearchText;

            // Ensure previous page is garbage collected then bind current page to _currentPage variable (thread-safe).
            lock (lockObject)
            {
                DestroyPage();
                _currentPage = this;
            }

            // Calculate page center.
            AdjustToSizeChange();

            // Close previously opened windows.
            if (SearchBuilder._searchBuilder != null)
                SearchBuilder._searchBuilder.Close();
            if (EditSearchQuery._editSearchQuery != null)
                EditSearchQuery._editSearchQuery.Close();

            // Init search.
            SearchBuilder.InitSearch(PageSearchType);

            // Set default selected columns.
            if (!_setDefaultSelectedColumns)
            {
                LoadColumnsPresets();
                bool fallbackToDefault = true;
                string? defaultPresetName = DefaultColumnsPresetName;
                if (!string.IsNullOrEmpty(defaultPresetName))
                    foreach (var preset in _columnsPresets)
                        if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Columns != null && preset.Columns.Count > 0)
                            if (string.Compare(defaultPresetName, preset.Name, StringComparison.InvariantCulture) == 0)
                            {
                                fallbackToDefault = false;
                                foreach (var c in preset.Columns)
                                    _selectedColumns.Add(c);
                                break;
                            }
                if (fallbackToDefault && _selectedColumns != null && DefaultSelectedColumns != null && DefaultSelectedColumns.Count > 0)
                    foreach (string c in DefaultSelectedColumns)
                        _selectedColumns.Add(c);
                _setDefaultSelectedColumns = true;
            }

            // Grab data from settings if not set.
            if (_lastDisplayed == null)
                _lastDisplayed = AllData;

            // Apply filters, sort and reorder columns.
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(250);
                ApplyFiltersAndSort();
            });
        }

        private void DestroyPage()
        {
            if (_currentPage != null)
            {
                _currentPage._quickSearchTimer?.Stop();
                DataGrid? previousDataGrid = _currentPage.UI?.DataGrid;
                if (previousDataGrid != null)
                {
                    if (previousDataGrid.ItemsSource != null)
                        previousDataGrid.ItemsSource = null;
                    // (Same as before: removes the previous data grid from the main grid of the new page.)
                    if (UI?.MainGrid != null)
                        UI.MainGrid.Children.Remove(previousDataGrid);
                }
                if (_currentPage._lastDisplayedVM != null)
                {
                    _currentPage._lastDisplayedVM.Clear();
                    _currentPage._lastDisplayedVM = null;
                }
                _currentPage._lastDisplayed = null;
                _currentPage = null;
            }
        }

        #endregion

        #region UI

#pragma warning disable CS8625
        public void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
#pragma warning restore CS8625

        private void RefreshPrimarySortLabel() => UI.PrimarySortRun.Text = $"{_currentSort} {(AscendingSort ? ASILang.Get("SortAscending") : ASILang.Get("SortDescending"))}";
        private void RefreshSecondarySortLabel() => UI.SecondarySortRun.Text = $"{_secondaryCurrentSort} {(SecondaryAscendingSort ? ASILang.Get("SortAscending") : ASILang.Get("SortDescending"))}";

        private void Init(ref IEnumerable<T>? items, bool onlyRefresh)
        {
            UI.DataGrid.MaxHeight = Math.Max(1.0d, UI.MainGrid.ActualHeight - (UI.Title.ActualHeight + UI.SortAndFilters.ActualHeight + _scrollBarWidth + 4.0d));
            UI.DataGrid.MaxWidth = Math.Max(1.0d, UI.MainGrid.ActualWidth - (_scrollBarWidth + 4.0d));
            if (!onlyRefresh && items != null)
            {
                if (_lastDisplayedVM != null)
                {
                    _lastDisplayedVM.Clear();
                    _lastDisplayedVM = null;
                }
                _lastDisplayedVM = new ObservableCollection<T>(items);
            }
            UI.DataGrid.ItemsSource = _lastDisplayedVM;
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(250);
                ReorderColumns();
            });
            if (items != null && MainWindow._minimap != null)
                RefreshMinimap(items);
        }

        private void ReorderColumns()
        {
            DataGrid dg = UI.DataGrid;
            if (dg?.Columns != null && dg.Columns.Count > 0)
            {
                List<string?> orders = new List<string?>();
                foreach (DataGridColumn col in dg.Columns)
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
                foreach (string defaultOrder in DefaultColumnsOrder)
                    if (orders.Contains(defaultOrder) && !defaultOrders.Contains(defaultOrder))
                        defaultOrders.Add(defaultOrder);

                // Add remaining unspecified order.
                foreach (string? otherOrder in orders)
                    if (!defaultOrders.Contains(otherOrder))
                        defaultOrders.Add(otherOrder);

                int j = 0;
                for (int i = 0; i < defaultOrders.Count; i++)
                {
                    foreach (DataGridColumn col in dg.Columns)
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
            DataGrid dg = UI.DataGrid;
            if (dg?.Columns == null || dg.Columns.Count <= 0)
                return;
            List<ColumnOrder> order = new List<ColumnOrder>();
            foreach (DataGridColumn col in dg.Columns)
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
                Utils.WriteAllTextAtomic(ColumnsOrderFilePath, jsonString);
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in SaveColumnsOrder. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private List<ColumnOrder>? LoadColumnsOrder()
        {
            string columnsOrderFilepath = ColumnsOrderFilePath;
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
            Type pageType = GetType();
#pragma warning disable CS1998
            MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                MainWindow._mainWindow.NavView_Navigate(typeof(HomePage), new EntranceNavigationTransitionInfo());
                MainWindow._mainWindow.NavView_Navigate(pageType, new EntranceNavigationTransitionInfo());
            });
#pragma warning restore CS1998
        }

        // Incremented by each refresh of the data grid: a background refresh started before a more recent one is ignored (and stopped early).
        private int _filterVersion = 0;

        /// <summary>
        /// Synchronous refresh: some callers read _lastDisplayedVM right after this call (ex: ItemsPage).
        /// For a refresh that does not block the UI thread, use ApplyFiltersAndSortInBackgroundAsync.
        /// </summary>
        public void ApplyFiltersAndSort()
        {
            ++_filterVersion;
            IEnumerable<T>? filtered = null;
            try
            {
                if (SearchBuilder._query == null || SearchBuilder._query.Parts == null || SearchBuilder._query.Parts.Count <= 0)
                    filtered = AllData;
                else
                    filtered = SearchBuilder.DoSearchQuery(PageSearchType, true) as IEnumerable<T>;
                if (filtered != null && !string.IsNullOrEmpty(_quickSearchText))
                    filtered = ApplyQuickSearch(filtered, _quickSearchText);
                if (filtered != null)
                {
                    ApplySorting(ref filtered);
                    // Evaluate the query once, here.
                    filtered = filtered.ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("FilteringError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                MainWindow.ShowToast($"{ASILang.Get("FilteringError")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                return;
            }
            Init(ref filtered, false);
        }

        private async Task ApplyFiltersAndSortInBackgroundAsync()
        {
            int version = ++_filterVersion;

            // Build the query on the UI thread: it reads the search query and updates the sort labels.
            IEnumerable<T>? filtered = null;
            if (SearchBuilder._query == null || SearchBuilder._query.Parts == null || SearchBuilder._query.Parts.Count <= 0)
                filtered = AllData;
            else
                filtered = SearchBuilder.DoSearchQuery(PageSearchType, true) as IEnumerable<T>;
            if (filtered != null && !string.IsNullOrEmpty(_quickSearchText))
                filtered = ApplyQuickSearch(filtered, _quickSearchText);
            if (filtered != null)
                ApplySorting(ref filtered);

            // Evaluate it on a background thread. TakeWhile stops the evaluation early if a more recent refresh has been started meanwhile.
            List<T>? result = null;
            if (filtered != null)
            {
                IEnumerable<T> toEvaluate = filtered.TakeWhile(_ => System.Threading.Volatile.Read(ref _filterVersion) == version);
                try { result = await Task.Run(() => toEvaluate.ToList()); }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("FilteringError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                    MainWindow.ShowToast($"{ASILang.Get("FilteringError")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                    return;
                }
            }

            // Back on the UI thread : ignore the result if a more recent refresh has been started, or if this page is no longer displayed.
            if (version != _filterVersion || _currentPage != this)
                return;
            IEnumerable<T>? items = result;
            Init(ref items, false);
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
                int windowWidth = Math.Max(1, Convert.ToInt32(Math.Round(MainWindow._mainWindow.Bounds.Width)) - (displayMode == NavigationViewDisplayMode.Minimal ? 2 : (displayMode == NavigationViewDisplayMode.Compact ? 52 : 164)));
                int windowHeight = Math.Max(1, Convert.ToInt32(Math.Round(MainWindow._mainWindow.Bounds.Height)) - 52);
                SetWindowSize(windowWidth, windowHeight);
                if (windowWidth < 950)
                {
                    UI.EditColumns.Margin = _compactMarginSortAndFilter;
                    UI.CurrentSort.Margin = _compactMarginSortAndFilter;

                    Grid.SetColumn(UI.EditFilters, 0);
                    Grid.SetColumn(UI.EditFiltersGroup, 0);
                    Grid.SetColumn(UI.EditColumns, 0);
                    Grid.SetColumn(UI.CurrentSort, 0);

                    Grid.SetRow(UI.EditFilters, 0);
                    Grid.SetRow(UI.EditFiltersGroup, 1);
                    Grid.SetRow(UI.EditColumns, 2);
                    Grid.SetRow(UI.CurrentSort, 3);
                }
                else
                {
                    UI.EditColumns.Margin = _defaultMarginLeftSortAndFilter;
                    UI.CurrentSort.Margin = _defaultMarginRightSortAndFilter;

                    Grid.SetColumn(UI.EditFilters, 0);
                    Grid.SetRow(UI.EditFilters, 0);

                    Grid.SetColumn(UI.EditFiltersGroup, 0);
                    Grid.SetRow(UI.EditFiltersGroup, 1);

                    Grid.SetColumn(UI.EditColumns, 1);
                    Grid.SetRow(UI.EditColumns, 0);

                    Grid.SetColumn(UI.CurrentSort, 1);
                    Grid.SetRow(UI.CurrentSort, 1);
                }
            }
        }

        protected void page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustToSizeChange();
            Init(ref _lastDisplayed, true);
        }

        protected void dg_Data_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            UI.NbLinesSelectedRun.Text = (UI.DataGrid.SelectedItems != null ? UI.DataGrid.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) : "0");
            UI.GetAllJsonMenuItem.Visibility = (UI.DataGrid.SelectedItems != null && UI.DataGrid.SelectedItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed);
            OnDataGridPointerReleased(e);
        }

        protected void dg_Data_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UI.NbLinesSelectedRun.Text = (UI.DataGrid.SelectedItems != null ? UI.DataGrid.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) : "0");
        }

        protected void dg_Data_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (!_selectedColumns.Contains((string)e.Column.Header))
                e.Cancel = true; //e.Column.Visibility = Visibility.Collapsed;
            e.Column.Header = GetCleanNameFromPropertyName(e.PropertyName);
        }

        protected void dg_Data_ColumnReordered(object sender, DataGridColumnEventArgs e) => SaveColumnsOrder();

        #endregion

        #region Sorting

        private Func<T, object?> GetSortKey(string? cleanName)
        {
            PropertyInfo? prop = Utils.GetProperty(typeof(T), GetPropertyNameFromCleanName(cleanName));
            if (prop == null)
                return _ => null;
            return o => prop.GetValue(o);
        }

        private void SimpleSort(ref IEnumerable<T> items, string cleanName)
        {
            var primaryKey = GetSortKey(cleanName);
            if (AscendingSort)
                items = items.OrderBy(primaryKey);
            else
                items = items.OrderByDescending(primaryKey);
        }

        private void DoubleSort(ref IEnumerable<T> items, string cleanName)
        {
            var primaryKey = GetSortKey(cleanName);
            var secondaryKey = GetSortKey(SecondaryCurrentSort);
            if (AscendingSort)
            {
                if (SecondaryAscendingSort)
                    items = items.OrderBy(primaryKey).ThenBy(secondaryKey);
                else
                    items = items.OrderBy(primaryKey).ThenByDescending(secondaryKey);
            }
            else
            {
                if (SecondaryAscendingSort)
                    items = items.OrderByDescending(primaryKey).ThenBy(secondaryKey);
                else
                    items = items.OrderByDescending(primaryKey).ThenByDescending(secondaryKey);
            }
        }

        private void SortDataGrid(ref IEnumerable<T> items, string cleanName)
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

        private void ApplySorting(ref IEnumerable<T> items)
        {
            if (CurrentSort != null)
            {
                SortDataGrid(ref items, CurrentSort);
                RefreshPrimarySortLabel();
                UI.PrimarySort.Visibility = Visibility.Visible;
                if (SecondaryCurrentSort != null)
                {
                    RefreshSecondarySortLabel();
                    UI.SecondarySort.Visibility = Visibility.Visible;
                }
            }
        }

        protected void dg_Data_Sorting(object sender, DataGridColumnEventArgs e)
        {
            if (e?.Column?.Header != null)
            {
                string? cleanName = e.Column.Header.ToString();
                if (AllData != null && cleanName != null)
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
            UI.ExistingColumns.Children.Add(cb);
        }

        private void FillModifyColumnsPopup()
        {
            UI.ExistingColumns.Children.Clear();
            var objProperties = typeof(T).GetProperties();
            if (objProperties != null && objProperties.Length > 0)
            {
                List<KeyValuePair<string, string>>? props = new List<KeyValuePair<string, string>>();
                foreach (var objProperty in objProperties)
                {
                    string propName = objProperty.Name;
                    if (!string.IsNullOrEmpty(propName))
                    {
                        string? cleanName = GetCleanNameFromPropertyName(propName);
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

        protected void btn_ChangeColumns_Click(object sender, RoutedEventArgs e)
        {
            if (UI.ModifyColumnsPopup.IsOpen)
                return;

            FillModifyColumnsPopup();
            UI.ModifyColumnsPopup.IsOpen = true;
        }

        protected void btn_CloseModifyColumnsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (UI.ModifyColumnsPopup.IsOpen)
            {
                UI.ModifyColumnsPopup.IsOpen = false;
                RefreshDisplayedColumns();
            }
        }

        #endregion

        #region Columns preset

        public void LoadColumnsPresets()
        {
            _columnsPresets.Clear();

            string columnsPresetsPath = ColumnsPresetsFilePath;
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
                    Utils.WriteAllTextAtomic(ColumnsPresetsFilePath, jsonString);
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                Logger.Instance.Log($"Exception caught in SaveColumnsPresets. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void ColumnsPresetSelect(JsonColumnsPreset? preset)
        {
            _selectedColumnsPreset = null;
            UI.ExistingColumnsPreset.Text = ASILang.Get("ClickHere");
            UI.LoadColumnsPreset.IsEnabled = false;
            UI.RemoveColumnsPreset.IsEnabled = false;
            UI.SetDefaultColumnsPreset.IsEnabled = false;

            if (preset == null)
                return;

            _selectedColumnsPreset = preset;
            UI.ExistingColumnsPreset.Text = preset.Name;
            UI.LoadColumnsPreset.IsEnabled = true;
            UI.RemoveColumnsPreset.IsEnabled = (string.Compare(preset.Name, ASILang.Get("DefaultPreset"), StringComparison.InvariantCulture) != 0);
            UI.SetDefaultColumnsPreset.IsEnabled = true;
        }

        private void FillColumnsPresetsDropDown()
        {
            UI.ExistingColumnsPresets.Items.Clear();
            UI.ExistingColumnsPresets.Items.Add(new MenuFlyoutItem
            {
                Text = ASILang.Get("DefaultPreset"),
                Command = ColumnsPresetSelectCommand,
                CommandParameter = DefaultColumnsPreset
            });
            foreach (var preset in _columnsPresets)
                if (preset != null && !string.IsNullOrEmpty(preset.Name) && preset.Columns != null && preset.Columns.Count > 0)
                {
                    UI.ExistingColumnsPresets.Items.Add(new MenuFlyoutItem
                    {
                        Text = preset.Name,
                        Command = ColumnsPresetSelectCommand,
                        CommandParameter = preset
                    });
                }
        }

        protected void btn_ColumnsPresets_Click(object sender, RoutedEventArgs e)
        {
            if (UI.ColumnsPresetsPopup.IsOpen)
                return;

            UI.ColumnsPresetName.Text = "";
            _selectedColumnsPreset = null;
            UI.ExistingColumnsPreset.Text = ASILang.Get("ClickHere");
            UI.LoadColumnsPreset.IsEnabled = false;
            UI.RemoveColumnsPreset.IsEnabled = false;
            UI.SetDefaultColumnsPreset.IsEnabled = false;

            LoadColumnsPresets();
            FillColumnsPresetsDropDown();

            UI.ColumnsPresetsPopup.IsOpen = true;
        }

        protected void btn_CloseColumnsPresetsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (UI.ColumnsPresetsPopup.IsOpen)
            {
                UI.ColumnsPresetsPopup.IsOpen = false;
                RefreshDisplayedColumns();
            }
        }

        protected void btn_SaveCurrentColumns_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UI.ColumnsPresetName.Text))
            {
                MainWindow.ShowToast(ASILang.Get("PresetNeedsName"), BackgroundColor.ERROR);
                return;
            }
            if (string.Compare(UI.ColumnsPresetName.Text, ASILang.Get("DefaultPreset"), StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                MainWindow.ShowToast(ASILang.Get("PresetNameAlreadyExists"), BackgroundColor.ERROR);
                return;
            }

            foreach (var preset in _columnsPresets)
                if (preset != null && preset.Name != null && string.Compare(preset.Name, UI.ColumnsPresetName.Text, StringComparison.InvariantCultureIgnoreCase) == 0)
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
                    UI.ExistingColumnsPreset.Text = ASILang.Get("ClickHere");
                    UI.LoadColumnsPreset.IsEnabled = false;
                    UI.RemoveColumnsPreset.IsEnabled = false;
                    UI.SetDefaultColumnsPreset.IsEnabled = false;

                    JsonColumnsPreset newPreset = new JsonColumnsPreset()
                    {
                        Name = UI.ColumnsPresetName.Text,
                        Columns = columns,
                    };
                    _columnsPresets.Add(newPreset);
                    UI.ExistingColumnsPresets.Items.Add(new MenuFlyoutItem
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

        protected void btn_LoadColumnsPreset_Click(object sender, RoutedEventArgs e) => LoadSelectedColumnsPreset(true);

        protected void btn_RemoveColumnsPreset_Click(object sender, RoutedEventArgs e)
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
                    UI.ExistingColumnsPreset.Text = ASILang.Get("ClickHere");
                    UI.LoadColumnsPreset.IsEnabled = false;
                    UI.RemoveColumnsPreset.IsEnabled = false;
                    UI.SetDefaultColumnsPreset.IsEnabled = false;

                    _columnsPresets.RemoveAt(toDel);
                    SaveColumnsPresets();
                    FillColumnsPresetsDropDown();
                }
                else
                    MainWindow.ShowToast($"{ASILang.Get("PresetNotFound").Replace("#PRESET_NAME#", _selectedColumnsPreset.Name ?? string.Empty, StringComparison.InvariantCulture)}", BackgroundColor.WARNING);
            }
        }

        protected void btn_SetDefaultColumnsPreset_Click(object sender, RoutedEventArgs e)
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

            DefaultColumnsPresetName = _selectedColumnsPreset.Name;
            SettingsPage.SaveSettings();
            MainWindow.ShowToast(ASILang.Get("ColumnsPresetSetAsDefault").Replace("#PRESET_NAME#", _selectedColumnsPreset.Name ?? string.Empty), BackgroundColor.SUCCESS);
            LoadSelectedColumnsPreset(false);
        }

        protected void tb_ColumnsPresetName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UI.SaveCurrentColumns.IsEnabled = (UI.ColumnsPresetName.Text.Length > 0);
        }

        protected void mfi_DefaultColumnsPreset_Click(object sender, RoutedEventArgs e)
        {
            UI.ExistingColumnsPreset.Text = ASILang.Get("DefaultPreset");
            UI.LoadColumnsPreset.IsEnabled = true;
            UI.RemoveColumnsPreset.IsEnabled = false;
            UI.SetDefaultColumnsPreset.IsEnabled = true;
            _selectedColumnsPreset = DefaultColumnsPreset;
        }

        #endregion

        #region DataGrid context menu

        protected void mfi_contextMenuGetID_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    T? obj = (mfi.DataContext as T);
                    if (obj != null)
                        clipboardStr = (GetObjectID(obj) ?? string.Empty);
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

        protected void mfi_contextMenuGetJson_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    T? obj = (mfi.DataContext as T);
                    if (obj != null)
                        clipboardStr = JsonSerializer.Serialize<T>(obj, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
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

        protected void mfi_contextMenuGetAllJson_Click(object sender, RoutedEventArgs e)
        {
            string clipboardStr = string.Empty;
            try
            {
                var selectedObjs = UI.DataGrid.SelectedItems;
                if (selectedObjs != null && selectedObjs.Count > 0)
                {
                    List<T>? objs = new List<T>();
                    for (int i = 0; i < selectedObjs.Count; i++)
                    {
                        T? obj = (selectedObjs[i] as T);
                        if (obj != null)
                            objs.Add(obj);
                    }
                    if (objs.Count > 0)
                    {
                        clipboardStr = JsonSerializer.Serialize<List<T>>(objs, new JsonSerializerOptions() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                        objs.Clear();
                        objs = null;
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

        #region Quick search

        private static IEnumerable<T> ApplyQuickSearch(IEnumerable<T> items, string text)
        {
            List<PropertyInfo> props = new List<PropertyInfo>();
            foreach (string column in _selectedColumns)
            {
                PropertyInfo? prop = Utils.GetProperty(typeof(T), column);
                if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0)
                    props.Add(prop);
            }
            if (props.Count <= 0)
                return items;

            CompareInfo compareInfo = CultureInfo.CurrentCulture.CompareInfo;
            const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            return items.Where(obj =>
            {
                if (obj == null)
                    return false;
                foreach (PropertyInfo prop in props)
                {
                    object? value;
                    try { value = prop.GetValue(obj); }
                    catch { continue; }
                    string? valueStr = value?.ToString();
                    if (!string.IsNullOrEmpty(valueStr) && compareInfo.IndexOf(valueStr, text, options) >= 0)
                        return true;
                }
                return false;
            }); // Evaluated by ApplyFiltersAndSort (UI thread) or ApplyFiltersAndSortInBackgroundAsync (background thread).
        }

        protected void tb_QuickSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_quickSearchTimer == null)
            {
                _quickSearchTimer = this.DispatcherQueue.CreateTimer();
                _quickSearchTimer.Interval = TimeSpan.FromMilliseconds(QUICK_SEARCH_DELAY_MS);
                _quickSearchTimer.IsRepeating = false;
                _quickSearchTimer.Tick += QuickSearchTimer_Tick;
            }
            _quickSearchTimer.Stop();
            _quickSearchTimer.Start();
        }

        private void QuickSearchTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            sender.Stop();
            if (_currentPage != this)
                return; // This page is no longer displayed.
            string text = (UI.QuickSearch.Text ?? string.Empty).Trim();
            if (string.Equals(text, _quickSearchText, StringComparison.Ordinal))
                return;
            _quickSearchText = text;
            ApplyFiltersAndSortInBackgroundAsync().FireAndForget(nameof(ApplyFiltersAndSortInBackgroundAsync));
        }

        public static void ClearQuickSearch()
        {
            _quickSearchText = string.Empty;
            DataPageBase<T>? page = _currentPage;
            if (page != null && page.UI?.QuickSearch != null)
                page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => page.UI.QuickSearch.Text = string.Empty);
        }

        #endregion

        #region Search queries

        protected void btn_CreateFilter_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBuilder._searchBuilder != null)
            {
                SearchBuilder._searchBuilder.Show();
                SearchBuilder._searchBuilder.Activate();
            }
            else
            {
                var s = new SearchBuilder();
                s.Initialize(PageSearchType);
                s.Show();
            }
        }

        protected void cbb_ExistingQueries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(UI.ExistingQueries, false);
            if (!string.IsNullOrWhiteSpace(selected) && SearchBuilder._queries != null && SearchBuilder._queries.Queries != null && SearchBuilder._queries.Queries.ContainsKey(selected))
            {
                UI.LoadQuery.IsEnabled = true;
                UI.EditQuery.IsEnabled = true;
                UI.DeleteQuery.IsEnabled = true;
            }
            else
            {
                UI.LoadQuery.IsEnabled = false;
                UI.EditQuery.IsEnabled = false;
                UI.DeleteQuery.IsEnabled = false;
            }
        }

        protected static bool ApplyCurrentQueryCore(string pageLabel)
        {
            DataPageBase<T>? page = _currentPage;
            if (page == null)
            {
                MainWindow.ShowToast(ASILang.Get("PageNotFound") + $" ({pageLabel})", BackgroundColor.WARNING);
                return false;
            }

            page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                if (SearchBuilder._query != null && SearchBuilder._query.Parts != null && SearchBuilder._query.Parts.Count > 0)
                {
                    page.ApplyFiltersAndSort();
                    MainWindow.ShowToast(ASILang.Get("FilterLoaded"), BackgroundColor.SUCCESS);
                }
                else
                    MainWindow.ShowToast(ASILang.Get("EmptyFilter"), BackgroundColor.WARNING);
            });
            return true;
        }

        protected void btn_LoadQuery_Click(object sender, RoutedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(UI.ExistingQueries, false);
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
            UI.ExistingQueries.Items.Clear();
            if (SearchBuilder._queries != null && SearchBuilder._queries.Queries != null && SearchBuilder._queries.Queries.Count > 0)
                foreach (var q in SearchBuilder._queries.Queries)
                    UI.ExistingQueries.Items.Add(q.Key);
        }

        protected void btn_DeleteQuery_Click(object sender, RoutedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(UI.ExistingQueries, false);
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

        protected void btn_DuplicateQuery_Click(object sender, RoutedEventArgs e) => Utils.DuplicateSearchQuery(Utils.GetComboBoxSelection(UI.ExistingQueries, false), PageSearchType);

        #endregion
    }
}
