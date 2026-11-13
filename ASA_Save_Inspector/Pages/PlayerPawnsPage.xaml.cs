using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.ObjectModelUtils;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ASA_Save_Inspector.Pages
{
    public abstract class PlayerPawnsPageBase : DataPageBase<PlayerPawn> { }

    public sealed partial class PlayerPawnsPage : PlayerPawnsPageBase
    {
        #region Page specific members (see DataPageBase)

        public static PlayerPawnsPage? _page => (_currentPage as PlayerPawnsPage);

        public static bool ApplyCurrentQuery() => ApplyCurrentQueryCore("PlayerPawns");

        public PlayerPawnsPage()
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
        }

        protected override SearchType PageSearchType => SearchType.PAWNS;
        protected override List<PlayerPawn>? AllData => SettingsPage._playerPawnsData;
        protected override string PageLabel => "PlayerPawns";
        protected override IReadOnlyList<string> DefaultSelectedColumns => PlayerPawnUtils.DefaultSelectedColumns;
        protected override IReadOnlyList<string> DefaultColumnsOrder => PlayerPawnUtils.DefaultColumnsOrder;
        protected override string? GetCleanNameFromPropertyName(string? propertyName) => PlayerPawnUtils.GetCleanNameFromPropertyName(propertyName);
        protected override string? GetPropertyNameFromCleanName(string? cleanName) => PlayerPawnUtils.GetPropertyNameFromCleanName(cleanName);
        protected override string ColumnsOrderFilePath => Utils.PlayerPawnColumnsOrderFilePath();
        protected override string ColumnsPresetsFilePath => Utils.PlayerPawnColumnsPresetsFilePath();

        protected override string? DefaultColumnsPresetName
        {
            get => SettingsPage._defaultColumnsPreset_PlayerPawns;
            set => SettingsPage._defaultColumnsPreset_PlayerPawns = value;
        }

        protected override string? GetObjectID(PlayerPawn playerPawn) => (!string.IsNullOrWhiteSpace(playerPawn.UniqueNetID) ? playerPawn.UniqueNetID : null);

        protected override void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        protected override void RefreshMinimap(IEnumerable<PlayerPawn> playerpawns)
        {
            var playerpawnsPoints = playerpawns.Select(d =>
            {
                string playerpawnIDStr = (d.LinkedPlayerDataID != null && d.LinkedPlayerDataID.HasValue ? d.LinkedPlayerDataID.Value.ToString(CultureInfo.InvariantCulture) : "0");
                KeyValuePair<double, double> minimapCoords = d.GetASIMinimapCoords();
                string? subMapName = d.GetSubMapName();
                return new MapPoint()
                {
                    ID = playerpawnIDStr,
                    Name = d.ShortName,
                    Description = $"{(playerpawnIDStr != "0" ? $"{ASILang.Get("ID")}: {playerpawnIDStr}\n" : string.Empty)}{(!string.IsNullOrWhiteSpace(d.PlayerName) ? $"{ASILang.Get("Name")}: {d.PlayerName}\n" : string.Empty)}{(!string.IsNullOrWhiteSpace(d.UniqueNetID) ? $"{ASILang.Get("UniqueID")}: {d.UniqueNetID}" : string.Empty)}",
                    X = minimapCoords.Value,
                    Y = minimapCoords.Key,
                    SubMapName = subMapName
                };
            });
            var playerpawnsFiltered = playerpawnsPoints.Where(d => string.Compare(d.SubMapName, SettingsPage._currentlyLoadedSubMapName, StringComparison.InvariantCultureIgnoreCase) == 0);
            MainWindow.UpdateMinimap(playerpawnsFiltered, OnMinimapDoubleTap);
        }

        protected override bool OnMinimapDoubleTap(MapPoint? point)
        {
            if (point == null || _lastDisplayedVM == null || string.IsNullOrWhiteSpace(point.ID) || point.ID == "00")
                return false;

            PlayerPawn? playerpawn = _lastDisplayedVM.FirstOrDefault(d => (string.Compare(point.ID, (d != null && d.LinkedPlayerDataID != null && d.LinkedPlayerDataID.HasValue ? d.LinkedPlayerDataID.Value.ToString(CultureInfo.InvariantCulture) : "0"), StringComparison.InvariantCulture) == 0), null);
            if (playerpawn != null)
            {
                dg_Data.SelectedItem = playerpawn;
                dg_Data.UpdateLayout();
                dg_Data.ScrollIntoView(dg_Data.SelectedItem, null);
                return true;
            }
            return false;
        }

        protected override void OnDataGridPointerReleased(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            mfi_contextMenuGetCoords.Visibility = Visibility.Visible;

            Visibility goToInventoryItemsVisibility = Visibility.Collapsed;
            DataGridRow? row = Utils.FindParent<DataGridRow>((UIElement)e.OriginalSource);
            if (row != null)
            {
                PlayerPawn? pp = row.DataContext as PlayerPawn;
                if (pp != null && !string.IsNullOrEmpty(pp.InventoryUUID))
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
            SearchBuilder._query = new SearchQuery();
        }

        public bool GoToPlayerPawn(int? linkedPlayerID)
        {
            if (linkedPlayerID != null && linkedPlayerID.HasValue && _lastDisplayedVM != null)
            {
                PlayerPawn? playerPawn = _lastDisplayedVM.FirstOrDefault(d => (d?.LinkedPlayerDataID == linkedPlayerID), null);
                if (playerPawn == null) // Player pawn not found. That's probably due to current filters.
                {
                    ClearPageFilters(); // Remove current filtering.
                    ApplyFiltersAndSort(); // Refresh.
                    playerPawn = _lastDisplayedVM.FirstOrDefault(d => (d?.LinkedPlayerDataID == linkedPlayerID), null);
                }
                if (playerPawn != null)
                {
                    dg_Data.SelectedItem = playerPawn;
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
                PlayerPawn? pp = row.DataContext as PlayerPawn;
                if (pp != null)
                {
                    string playerpawnIDStr = (pp.LinkedPlayerDataID != null && pp.LinkedPlayerDataID.HasValue ? pp.LinkedPlayerDataID.Value.ToString(CultureInfo.InvariantCulture) : "0");
                    var minimapCoords = pp.GetASIMinimapCoords();
                    double x = minimapCoords.Value;
                    double y = minimapCoords.Key;
                    string? subMapName = pp.GetSubMapName();
                    Minimap.ShowCallout(playerpawnIDStr, x, y, subMapName);
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
                    PlayerPawn? playerpawn = (mfi.DataContext as PlayerPawn);
                    if (playerpawn != null && playerpawn.Location != null)
                        clipboardStr = playerpawn.Location;
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

        private void mfi_contextMenuGoToPlayerData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    PlayerPawn? playerpawn = (mfi.DataContext as PlayerPawn);
                    if (playerpawn != null && playerpawn.LinkedPlayerDataID != null && playerpawn.LinkedPlayerDataID.HasValue)
                    {

#pragma warning disable CS1998
                        if (MainWindow._mainWindow != null)
                        {
                            MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                            {
                                if (MainWindow._mainWindow != null && MainWindow._mainWindow._navView != null)
                                {
                                    MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnPlayersData;
                                    MainWindow._mainWindow.NavView_Navigate(typeof(PlayersPage), new EntranceNavigationTransitionInfo());
                                    await Task.Delay(250);
                                    if (PlayersPage._page != null)
                                    {
                                        PlayersPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                        {
                                            if (!PlayersPage._page.GoToPlayer(playerpawn.LinkedPlayerDataID))
                                                MainWindow.ShowToast($"{ASILang.Get("PlayerPawnNotFound")} {ASILang.Get("CheckFilters")}", BackgroundColor.WARNING);
                                        });
                                    }
                                    else
                                        MainWindow.ShowToast(ASILang.Get("PlayersDataPageNotFound"), BackgroundColor.WARNING);
                                }
                            });
                        }
#pragma warning restore CS1998
                    }
                    else
                        MainWindow.ShowToast(ASILang.Get("NoValidID_PlayerPawn"), BackgroundColor.WARNING);
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGoToPlayerData_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        private void mfi_contextMenuGoToInventoryItems_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    PlayerPawn? pp = (mfi.DataContext as PlayerPawn);
                    if (pp != null && !string.IsNullOrEmpty(pp.InventoryUUID))
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
                                        if (!ItemsPage._page.FilterByInventoryUUID(pp.InventoryUUID))
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
    }
}
