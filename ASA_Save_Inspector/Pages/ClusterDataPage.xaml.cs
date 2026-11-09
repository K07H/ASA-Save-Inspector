using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ASA_Save_Inspector.ObjectModel;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class ClusterDataPage : Page
    {
        public static ClusterDataPage? _page = null;

        public static string? SelectedClusterDataFolderPath { get; set; } = null;

        private sealed record SortState(string Property, bool Descending);

        private List<ClusterDataEntry> _clusters = new();
        private ClusterOption? _selected;
        private List<ItemRow> _allItems = new();
        private List<DinoRow> _allDinos = new();
        private SortState? _itemSort;   // null = default order
        private SortState? _dinoSort;

        public ClusterDataPage()
        {
            InitializeComponent();
            _page = this;
            SettingsPage.LoadSettingsStatic(true);
            var t = Initialize();
            if (t != null)
                t.Wait();
        }

        private void SetSelectedClusterDataFolderPath(string? folderPath)
        {
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                SelectedClusterDataFolderPath = folderPath;
                tb_SelectedClusterDataFolder.Text = SelectedClusterDataFolderPath;
                tb_SelectedClusterDataFolder.Visibility = Visibility.Visible;
                btn_InspectClusterDataFolder.IsEnabled = true;
            }
            else
            {
                SelectedClusterDataFolderPath = string.Empty;
                tb_SelectedClusterDataFolder.Text = SelectedClusterDataFolderPath;
                tb_SelectedClusterDataFolder.Visibility = Visibility.Collapsed;
                btn_InspectClusterDataFolder.IsEnabled = false;
            }
            SettingsPage.SaveSettings();
        }

        private async Task Initialize()
        {
            var list = StorageApplicationPermissions.FutureAccessList;
            if (list != null && list.ContainsItem("ASIClusterDataFolderPath"))
            {
                try
                {
                    StorageFolder folder = await list.GetFolderAsync("ASIClusterDataFolderPath");
                    if (folder != null && !string.IsNullOrWhiteSpace(folder.Path) && Directory.Exists(folder.Path))
                    {
                        SetSelectedClusterDataFolderPath(folder.Path);
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("ASIClusterDataFolderPath", folder);
                    }
                }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(SelectedClusterDataFolderPath) && Directory.Exists(SelectedClusterDataFolderPath))
                SetSelectedClusterDataFolderPath(SelectedClusterDataFolderPath);
        }

        private async Task OpenClusterDataFolderSelector()
        {
            if (App._window == null)
                return;
            StorageFolder? folder = null;
            try
            {
                var folderPicker = new FolderPicker();

                // Configure the FolderPicker
                folderPicker.ViewMode = PickerViewMode.List;
                folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add("*");

                // HWND initialization (needed for WinUI3)
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App._window);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                // Open FolderPicker window
                folder = await folderPicker.PickSingleFolderAsync();
            }
            catch (Exception ex)
            {
                folder = null;
                Logger.Instance.Log($"{ASILang.Get("OpenFolderPickerException")} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }

            if (folder != null && !string.IsNullOrWhiteSpace(folder.Path) && Directory.Exists(folder.Path))
            {
                SetSelectedClusterDataFolderPath(folder.Path);
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("ASIClusterDataFolderPath", folder);
            }
        }

#pragma warning disable CS4014
        private void btn_SelectClusterDataFolder_Click(object sender, RoutedEventArgs e) => OpenClusterDataFolderSelector();
#pragma warning restore CS4014

        private static void ShowProcessError(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string str = PythonManager.StripPythonColors(e.Data);
                Logger.Instance.Log(str, Logger.LogLevel.ERROR);
            }
        }

        private static void ShowProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string str = PythonManager.StripPythonColors(e.Data);
                Logger.Instance.Log(str, Logger.LogLevel.INFO);
            }
        }

        private async Task<bool> InspectClusterData()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SelectedClusterDataFolderPath) || !Directory.Exists(SelectedClusterDataFolderPath))
                {
                    await ShowErrorAsync("Error", ASILang.Get("NoClusterDataSelected"));
                    Logger.Instance.Log(ASILang.Get("NoClusterDataSelected"), Logger.LogLevel.ERROR);
                    return false;
                }

                if (SettingsPage.UsePythonVenv())
                {
                    // Don't proceed if python venv is NOT present.
                    if (!File.Exists(Utils.PythonFilePathFromVenv()))
                    {
                        await ShowErrorAsync("Error", $"{ASILang.Get("PythonVenvSetupIsNotSetup")} {ASILang.Get("PleaseGoToSettings")}");
                        Logger.Instance.Log($"{ASILang.Get("PythonVenvSetupIsNotSetup")} {ASILang.Get("PleaseGoToSettings")}", Logger.LogLevel.ERROR);
                        return false;
                    }
                }
                else
                {
                    // Don't proceed if python global exe is NOT present.
                    if (!File.Exists(SettingsPage._pythonExePath))
                    {
                        await ShowErrorAsync("Error", $"{ASILang.Get("PythonExeNotSet")} {ASILang.Get("PleaseGoToSettings")}");
                        Logger.Instance.Log($"{ASILang.Get("PythonExeNotSet")} {ASILang.Get("PleaseGoToSettings")}", Logger.LogLevel.ERROR);
                        return false;
                    }
                }

                string asiExportClusterDataScriptPath = Utils.AsiExportClusterDataFilePath();
                if (!File.Exists(asiExportClusterDataScriptPath))
                {
                    await ShowErrorAsync("Error", ASILang.Get("ExportClusterDataScriptNotFound"));
                    Logger.Instance.Log(ASILang.Get("ExportClusterDataScriptNotFound"), Logger.LogLevel.ERROR);
                    return false;
                }

                Utils.EnsureDataFolderExist();

                string exportFolderPath = Utils.ClusterDataExportFolder();
                if (!Directory.Exists(exportFolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(exportFolderPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"{ASILang.Get("FailedToCreateClusterDataExportFolder")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                        return false;
                    }
                }

                PythonManager.AddPythonTestFile();
                string? arkParseClusterDataRunnerPath = Utils.ArkParseClusterDataRunnerFilePath();
                string? arkParseClusterDataRunnerContent = null;
                if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value && File.Exists(Utils.PythonTestScriptPath()))
                    arkParseClusterDataRunnerContent = string.Format(
    @"
{0}
echo PATH environment variable
echo %PATH%
echo PYTHONPATH environment variable
echo %PYTHONPATH%
""{1}"" ""{2}""
",
    SettingsPage.UsePythonVenv() ? $"set \"PYTHONPATH={Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages")}\"" : string.Empty,
    SettingsPage.UsePythonVenv() ? Utils.PythonFilePathFromVenv() : SettingsPage._pythonExePath,
    Utils.PythonTestScriptPath());
                else
                    arkParseClusterDataRunnerContent = string.Format(
    @"
{0}
",
    SettingsPage.UsePythonVenv() ? $"set \"PYTHONPATH={Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages")}\"" : string.Empty);


                arkParseClusterDataRunnerContent += string.Format(
    @"
""{0}"" ""{1}"" ""{2}"" ""{3}""
",
    SettingsPage.UsePythonVenv() ? Utils.PythonFilePathFromVenv() : SettingsPage._pythonExePath,
    asiExportClusterDataScriptPath,
    SelectedClusterDataFolderPath,
    exportFolderPath);

                try { File.WriteAllText(arkParseClusterDataRunnerPath, arkParseClusterDataRunnerContent, Encoding.ASCII); }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("FailedToWriteClusterDataExtractScript")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return false;
                }

                if (!File.Exists(arkParseClusterDataRunnerPath))
                {
                    Logger.Instance.Log($"{ASILang.Get("FailedToWriteClusterDataExtractScript")}", Logger.LogLevel.ERROR);
                    return false;
                }

                Process? process = null;
                int exitCode = -1;
                try
                {
                    process = new Process();
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.FileName = arkParseClusterDataRunnerPath;
                    process.StartInfo.WorkingDirectory = Utils.GetDataDir();
                    process.OutputDataReceived += ShowProcessOutput;
                    process.ErrorDataReceived += ShowProcessError;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("ClusterDataExtractScriptError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return false;
                }
                finally
                {
                    try
                    {
                        if (process != null)
                            exitCode = process.ExitCode;
                    }
                    catch { exitCode = -1; }
                    if (process != null)
                    {
                        process.Dispose();
                        process = null;
                    }
                }
                Logger.Instance.Log($"{ASILang.Get("PythonExitCode")}: {exitCode.ToString(CultureInfo.InvariantCulture)}", Logger.LogLevel.INFO);
                if (exitCode != 0)
                {
                    Logger.Instance.Log($"{ASILang.Get("ClusterDataExtractScriptError")}", Logger.LogLevel.ERROR);
                    return false;
                }

                string clusterDataFilepath = Path.Combine(exportFolderPath, "cluster_data.json");
                if (!File.Exists(clusterDataFilepath))
                {
                    Logger.Instance.Log(ASILang.Get("NoDataFoundInClusterData"), Logger.LogLevel.INFO);
                    return false;
                }
                var fi = new FileInfo(clusterDataFilepath);
                if (fi == null || fi.Length <= 0)
                {
                    Logger.Instance.Log(ASILang.Get("NoDataFoundInClusterData"), Logger.LogLevel.INFO);
                    return false;
                }

                Logger.Instance.Log(ASILang.Get("ClusterDataExtractedSuccessfully"), Logger.LogLevel.INFO);

                await LoadFileAsync(clusterDataFilepath);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("ClusterDataExtractionFailed")} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            finally
            {
                SetBusy(false);
            }
            return true;
        }

        private void btn_InspectClusterDataFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedClusterDataFolderPath) || !Directory.Exists(SelectedClusterDataFolderPath))
            {
#pragma warning disable CS4014
                ShowErrorAsync("Error", ASILang.Get("ClusterDataFolderSelectionRequired"));
#pragma warning restore CS4014
                return;
            }
            SetBusy(true);
#pragma warning disable CS4014
            InspectClusterData();
#pragma warning restore CS4014
        }

        public async Task LoadFileAsync(string path)
        {
            try
            {
                // Deserialize off the UI thread (files are several MB)
                var clusters = await Task.Run(() => ClusterDataLoader.Load(path));
                SetClusters(clusters);
                sp_ClusterEntrySelection.Visibility = Visibility.Visible;
                grd_Summary.Visibility = Visibility.Visible;
                piv_ItemsDinos.Visibility = Visibility.Visible;
                if (_selected == null)
                    SummaryText.Text = $"{Path.GetFileName(path)}: ${ASILang.Get("NbClusterLoadedAndNoneToDisplay").Replace("#NB_CLUSTER_DATA_PROFILES#", clusters.Count.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture)}";
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", ASILang.Get("UnableToReadFile"));
                Logger.Instance.Log($"{ASILang.Get("UnableToReadFile")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                sp_ClusterEntrySelection.Visibility = Visibility.Collapsed;
                grd_Summary.Visibility = Visibility.Collapsed;
                piv_ItemsDinos.Visibility = Visibility.Collapsed;
            }
        }

        public void SetClusters(IEnumerable<ClusterDataEntry> clusters)
        {
            _clusters = clusters.ToList();
            RefreshClusterOptions();
        }

        private void RefreshClusterOptions()
        {
            string? previousUuid = (ClusterCombo.SelectedItem as ClusterOption)?.Uuid;
            bool hideEmpty = HideEmptyCheck.IsChecked == true;

            var options = _clusters
                .Select(c => new ClusterOption(c))
                .Where(o => !hideEmpty || !o.IsEmpty)
                .ToList();

            // "ALL" entry first: shows every cluster at once
            if (_clusters.Count > 0)
                options.Insert(0, ClusterOption.All(_clusters));

            ClusterCombo.ItemsSource = options;
            // Keep the current selection if possible, otherwise take the first cluster
            ClusterCombo.SelectedItem = options.FirstOrDefault(o => o.Uuid == previousUuid) ?? options.FirstOrDefault();
        }

        private void ClusterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = ClusterCombo.SelectedItem as ClusterOption;
            BuildRows();
        }

        private void HideEmptyCheck_Click(object sender, RoutedEventArgs e) => RefreshClusterOptions();

        private void IncludeCryopodCheck_Click(object sender, RoutedEventArgs e) => BuildRows();

        private void BuildRows()
        {
            if (_selected == null)
            {
                _allItems = new();
                _allDinos = new();
                SummaryText.Text = _clusters.Count == 0 ? ASILang.Get("NoFileLoaded") : ASILang.Get("NoClusterDataToDisplay");
                ApplyFilter();
                return;
            }

            var entries = _selected.Entries;

            // Default order: items by category then name, dinos by level (highest first)
            _allItems = entries
                .SelectMany(e => e.Items.Select(u => ItemRow.From(u, e.UUID)))
                .OrderBy(r => r.Category).ThenBy(r => r.Name)
                .ToList();

            var dinos = entries
                .SelectMany(e => e.Dinos.Select(u => DinoRow.From(u.Dino, "Upload", e.UUID)))
                .ToList();

            if (IncludeCryopodCheck.IsChecked == true)
            {
                dinos.AddRange(entries.SelectMany(e => e.Items
                    .Where(i => i.Item.Dino != null)
                    .Select(i => DinoRow.From(i.Item.Dino!, "Cryopod", e.UUID))));
            }

            _allDinos = dinos.OrderByDescending(r => r.CurrentLevel).ToList();

            string scope = _selected.IsAll ? $"{ASILang.Get("AllClusterDataEntries")} ({entries.Count})" : entries[0].Path;
            SummaryText.Text = $"{scope}  -  {_selected.ItemCount} {ASILang.Get("Items").ToLowerInvariant()}  -  " +
                               $"{_selected.DinoCount} {ASILang.Get("UploadedDinos")}  -  {_selected.CryopodDinoCount} {ASILang.Get("DinosInCryopod")}";

            // The "Cluster" column only makes sense when several clusters are shown together
            SetClusterColumnVisible(ItemsGrid, _selected.IsAll);
            SetClusterColumnVisible(DinosGrid, _selected.IsAll);

            ApplyFilter();
            ResetColumnWidths(ItemsGrid);
            ResetColumnWidths(DinosGrid);
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            try
            {
                string query = FilterBox.Text.Trim();

                IEnumerable<ItemRow> items = _allItems;
                IEnumerable<DinoRow> dinos = _allDinos;

                if (query.Length > 0)
                {
                    items = items.Where(r => r.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase));
                    dinos = dinos.Where(r => r.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase));
                }

                var itemList = _itemSort is null ? items.ToList() : SortRows(items, _itemSort);
                var dinoList = _dinoSort is null ? dinos.ToList() : SortRows(dinos, _dinoSort);

                if (itemList == null)
                    itemList = new List<ItemRow>();
                if (dinoList == null)
                    dinoList = new List<DinoRow>();

                ItemsGrid.ItemsSource = itemList;
                DinosGrid.ItemsSource = dinoList;
                ShowSortIndicator(ItemsGrid, _itemSort);
                ShowSortIndicator(DinosGrid, _dinoSort);

                ItemsPivot.Header = $"{ASILang.Get("Items")} ({itemList.Count})";
                DinosPivot.Header = $"{ASILang.Get("Dinos")} ({dinoList.Count})";

                ItemsEmptyText.Visibility = itemList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                DinosEmptyText.Visibility = dinoList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowErrorStaticAsync("Error", ASILang.Get("FailedToApplyFilter"));
                Logger.Instance.Log($"{ASILang.Get("FailedToApplyFilter")} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void ItemsGrid_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            _itemSort = NextSort(_itemSort, e.Column);
            ApplyFilter();
        }

        private void DinosGrid_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            _dinoSort = NextSort(_dinoSort, e.Column);
            ApplyFilter();
        }

        // Click on a header: ascending, click again: descending. Column.Tag = name of the sort property.
        private static SortState NextSort(SortState? current, DataGridColumn column)
        {
            string property = (string)column.Tag;
            return current?.Property == property
                ? current with { Descending = !current.Descending }
                : new SortState(property, false);
        }

        private static List<T>? SortRows<T>(IEnumerable<T> rows, SortState state)
        {
            PropertyInfo? prop = typeof(T).GetProperty(state.Property);
            if (prop == null)
            {
                ShowErrorStaticAsync("Error", ASILang.Get("UnknownSortProperty").Replace("#SORT_PROPERTY#", state.Property, StringComparison.InvariantCulture));
                Logger.Instance.Log(ASILang.Get("UnknownSortProperty").Replace("#SORT_PROPERTY#", state.Property, StringComparison.InvariantCulture), Logger.LogLevel.ERROR);
                return null;
            }

            var keyed = rows.Select(r => (Row: r, Key: prop.GetValue(r) as IComparable)).ToList();
            var comparer = Comparer<IComparable>.Create((a, b) =>
                a is string sa && b is string sb
                    ? string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase)
                    : a.CompareTo(b));

            var present = keyed.Where(x => x.Key != null);
            var ordered = state.Descending
                ? present.OrderByDescending(x => x.Key!, comparer)
                : present.OrderBy(x => x.Key!, comparer);

            // Rows without a value ("-") always go last
            return ordered.Concat(keyed.Where(x => x.Key == null)).Select(x => x.Row).ToList();
        }

        private static void ShowSortIndicator(DataGrid grid, SortState? state)
        {
            foreach (var column in grid.Columns)
            {
                column.SortDirection = state != null && (string?)column.Tag == state.Property
                    ? (state.Descending ? DataGridSortDirection.Descending : DataGridSortDirection.Ascending)
                    : null;
            }
        }

        private static void SetClusterColumnVisible(DataGrid grid, bool visible)
        {
            var column = grid.Columns.FirstOrDefault(col => (string?)col.Tag == "Cluster");
            if (column != null)
                column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void ResetColumnWidths(DataGrid grid)
        {
            foreach (var column in grid.Columns)
                column.Width = DataGridLength.Auto;
        }

        private void SetBusy(bool busy)
        {
            BusyRing.IsActive = busy;
            ClusterCombo.IsEnabled = !busy;
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = ASILang.Get("OK")
            };
            await dialog.ShowAsync();
        }

        private static void ShowErrorStaticAsync(string title, string message)
        {
            if (_page != null)
                _ = _page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    await Task.Delay(250);
                    await _page.ShowErrorAsync(title, message);
                });
        }
    }
}
