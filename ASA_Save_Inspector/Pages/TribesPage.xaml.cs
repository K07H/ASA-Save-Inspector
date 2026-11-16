using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.ObjectModelUtils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ASA_Save_Inspector.Pages
{
    public abstract class TribesPageBase : DataPageBase<Tribe> { }

    public sealed partial class TribesPage : TribesPageBase
    {
        #region Statics

        public static TribesPage? _page => (_currentPage as TribesPage);

        // Map name, save file datetime and in-game datetime (used by x:Bind in the XAML).
        public static string MapName => (SettingsPage._currentlyLoadedMapName ?? ASILang.Get("Unknown"));
        public static string SaveGameDatetime => (Utils.GetSaveFileDateTimeStr() ?? ASILang.Get("UnknownDate"));
        public static string InGameDatetime => Utils.GetInGameDateTimeStr();

        public static bool ApplyCurrentQuery() => ApplyCurrentQueryCore("Tribes");

        #endregion

        #region Constructor

        public TribesPage()
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

        #endregion

        #region Page specific members (see DataPageBase)

        protected override SearchType PageSearchType => SearchType.TRIBES;
        protected override List<Tribe>? AllData => SettingsPage._tribesData;
        protected override string PageLabel => "Tribes";
        protected override IReadOnlyList<string> DefaultSelectedColumns => TribeUtils.DefaultSelectedColumns;
        protected override IReadOnlyList<string> DefaultColumnsOrder => TribeUtils.DefaultColumnsOrder;
        protected override string? GetCleanNameFromPropertyName(string? propertyName) => TribeUtils.GetCleanNameFromPropertyName(propertyName);
        protected override string? GetPropertyNameFromCleanName(string? cleanName) => TribeUtils.GetPropertyNameFromCleanName(cleanName);
        protected override string ColumnsOrderFilePath => Utils.TribeColumnsOrderFilePath();
        protected override string ColumnsPresetsFilePath => Utils.TribeColumnsPresetsFilePath();

        protected override string? DefaultColumnsPresetName
        {
            get => SettingsPage._defaultColumnsPreset_Tribes;
            set => SettingsPage._defaultColumnsPreset_Tribes = value;
        }

        protected override string? GetObjectID(Tribe tribe) => (tribe.TribeID != null && tribe.TribeID.HasValue ? tribe.TribeID.Value.ToString(CultureInfo.InvariantCulture) : null);

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

        protected override void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        public bool GoToTribe(int? tribeID)
        {
            if (tribeID != null && tribeID.HasValue && _lastDisplayedVM != null)
            {
                Tribe? tribe = _lastDisplayedVM.FirstOrDefault(d => (d?.TribeID == tribeID), null);
                if (tribe != null)
                {
                    dg_Data.SelectedItem = tribe;
                    dg_Data.UpdateLayout();
                    dg_Data.ScrollIntoView(dg_Data.SelectedItem, null);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region DataGrid context menu

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
                Utils.AddToClipboard(clipboardStr);
            }
            catch (Exception ex)
            {
                clipboardStr = string.Empty;
                MainWindow.ShowToast($"{ASILang.Get("ErrorHappened")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.WARNING);
                Logger.Instance.Log($"Exception caught in mfi_contextMenuGetLogs_Click. Exception=[{ex}]", Logger.LogLevel.ERROR);
                Utils.AddToClipboard(clipboardStr, false);
            }
        }

        #endregion
    }
}
