using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.ObjectModelUtils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASA_Save_Inspector.Pages
{
    public abstract class PlayersPageBase : DataPageBase<Player> { }

    public sealed partial class PlayersPage : PlayersPageBase
    {
        #region Page specific members (see DataPageBase)

        public static PlayersPage? _page => (_currentPage as PlayersPage);

        public static bool ApplyCurrentQuery() => ApplyCurrentQueryCore("Players");

        public PlayersPage()
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

        protected override SearchType PageSearchType => SearchType.PLAYERS;
        protected override List<Player>? AllData => SettingsPage._playersData;
        protected override string PageLabel => "Players";
        protected override IReadOnlyList<string> DefaultSelectedColumns => PlayerUtils.DefaultSelectedColumns;
        protected override IReadOnlyList<string> DefaultColumnsOrder => PlayerUtils.DefaultColumnsOrder;
        protected override string? GetCleanNameFromPropertyName(string? propertyName) => PlayerUtils.GetCleanNameFromPropertyName(propertyName);
        protected override string? GetPropertyNameFromCleanName(string? cleanName) => PlayerUtils.GetPropertyNameFromCleanName(cleanName);
        protected override string ColumnsOrderFilePath => Utils.PlayerColumnsOrderFilePath();
        protected override string ColumnsPresetsFilePath => Utils.PlayerColumnsPresetsFilePath();

        protected override string? DefaultColumnsPresetName
        {
            get => SettingsPage._defaultColumnsPreset_Players;
            set => SettingsPage._defaultColumnsPreset_Players = value;
        }

        protected override string? GetObjectID(Player player) => player.UniqueID;

        protected override void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        protected override void OnDataGridPointerReleased(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            mfi_contextMenuGetCoords.Visibility = Visibility.Visible;
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

        public bool GoToPlayer(int? linkedPlayerID)
        {
            if (linkedPlayerID != null && linkedPlayerID.HasValue && _lastDisplayedVM != null)
            {
                Player? player = _lastDisplayedVM.FirstOrDefault(d => (d?.PlayerDataID == linkedPlayerID), null);
                if (player != null)
                {
                    dg_Data.SelectedItem = player;
                    dg_Data.UpdateLayout();
                    dg_Data.ScrollIntoView(dg_Data.SelectedItem, null);
                    return true;
                }
            }
            return false;
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
                    Player? player = (mfi.DataContext as Player);
                    if (player != null && player.Location != null)
                        clipboardStr = player.Location;
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

        private void mfi_contextMenuGoToTribeData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
                if (mfi != null)
                {
                    Player? player = (mfi.DataContext as Player);
                    if (player != null && player.TribeID != null && player.TribeID.HasValue)
                    {
#pragma warning disable CS1998
                        if (MainWindow._mainWindow != null)
                        {
                            MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                            {
                                if (MainWindow._mainWindow != null && MainWindow._mainWindow._navView != null)
                                {
                                    MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnTribesData;
                                    MainWindow._mainWindow.NavView_Navigate(typeof(TribesPage), new EntranceNavigationTransitionInfo());
                                    await Task.Delay(250);
                                    if (TribesPage._page != null)
                                    {
                                        TribesPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                                        {
                                            if (!TribesPage._page.GoToTribe(player.TribeID))
                                                MainWindow.ShowToast($"{ASILang.Get("TribeNotFound")} {ASILang.Get("CheckFilters")}", BackgroundColor.WARNING);
                                        });
                                    }
                                    else
                                        MainWindow.ShowToast(ASILang.Get("TribesDataPageNotFound"), BackgroundColor.WARNING);
                                }
                            });
                        }
#pragma warning restore CS1998
                    }
                    else
                        MainWindow.ShowToast(ASILang.Get("NoValidID_Player"), BackgroundColor.WARNING);
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGoToTribeData_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
        }

        #endregion
    }
}
