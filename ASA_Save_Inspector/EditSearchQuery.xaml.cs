using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ASA_Save_Inspector;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditSearchQuery : Window
{
    public static EditSearchQuery? _editSearchQuery = null;
    public AppWindow? _appWindow = null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? _queryName = null;
    public SearchQuery? _query = null;
    public bool _isEditable = false;
    public SearchType _searchType = SearchType.PAWNS;

    public EditSearchQuery()
    {
        InitializeComponent();
        _editSearchQuery = this;
        _appWindow = this.AppWindow;

        // Calculate page center.
        AdjustToSizeChange();

        // Hide system title bar.
        ExtendsContentIntoTitleBar = true;
        if (ExtendsContentIntoTitleBar == true)
            this._appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Set title bar title.
        this.Title = $"{MainWindow._appName} v{Utils.GetVersionStr()} - {ASILang.Get("EditFilter")}";
        TitleBarTextBlock.Text = $"{MainWindow._appName} v{Utils.GetVersionStr()} - {ASILang.Get("EditFilter")}";

        // Set icon.
        if (_appWindow != null)
        {
            _appWindow.SetIcon(@"Assets\ASI.ico");
            _appWindow.SetTitleBarIcon(@"Assets\ASI.ico");
            _appWindow.SetTaskbarIcon(@"Assets\ASI.ico");
        }

        // Set window default size.
        AppWindow.Resize(new Windows.Graphics.SizeInt32(700, 500));
    }

    private void page_EditSearchQuery_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        AdjustToSizeChange();
    }

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

    private void AdjustToSizeChange()
    {
        if (SearchBuilder._searchBuilder != null)
        {
            WindowWidth = Math.Max(1, Convert.ToInt32(Math.Round(SearchBuilder._searchBuilder.Bounds.Width)) - 52);
            WindowHeight = Math.Max(1, Convert.ToInt32(Math.Round(SearchBuilder._searchBuilder.Bounds.Height)) - 52);
        }
    }

    private void ShowNotificationMsg(string msg, BackgroundColor color = BackgroundColor.DEFAULT, int duration = 3000)
    {
        if (!PopupNotification.IsOpen)
        {
            switch (color)
            {
                case BackgroundColor.DEFAULT:
                    b_innerPopupNotification.Background = Utils._grayNotificationBackground;
                    break;
                case BackgroundColor.SUCCESS:
                    b_innerPopupNotification.Background = Utils._greenNotificationBackground;
                    break;
                case BackgroundColor.WARNING:
                    b_innerPopupNotification.Background = Utils._orangeNotificationBackground;
                    break;
                case BackgroundColor.ERROR:
                    b_innerPopupNotification.Background = Utils._redNotificationBackground;
                    break;
                default:
                    b_innerPopupNotification.Background = Utils._grayNotificationBackground;
                    break;
            }
            tb_popupNotificationMsg.Text = msg;
            PopupNotification.IsOpen = true;

            FadeInStoryboard.Begin();
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                await Task.Delay(duration - 600);
                try { FadeOutStoryboard.Begin(); }
                catch { }
                await Task.Delay(600);
                bool isOpen = false;
                try { isOpen = PopupNotification.IsOpen; }
                catch { isOpen = false; }
                if (isOpen)
                {
                    try { b_popupNotification.Opacity = 0; }
                    catch { }
                    try { PopupNotification.IsOpen = false; }
                    catch { }
                }
            });
        }
    }

    public void Initialize(string? queryName, SearchQuery query, bool isEditable, SearchType searchType)
    {
        _queryName = queryName;
        _query = SearchQuery.GetCopy(query);
        _isEditable = isEditable;
        _searchType = searchType;

        Utils.FormatQuery(ref sp_Query, _query, _isEditable, false, _searchType, SearchBuilder.GetTypeFromSearchType(_searchType));
    }

    public void ModifyQuerySearchOperator(int partId, SearchOperator newSearchOperator)
    {
        if (_query == null || _query.Parts == null || partId >= _query.Parts.Count)
            return;

        SearchOperator prevSearchOperator = _query.Parts[partId].Operator;
        bool wasMatchingOperator = (prevSearchOperator == SearchOperator.MATCHING || prevSearchOperator == SearchOperator.NOT_MATCHING);
        bool becomesMatchingOperator = (newSearchOperator == SearchOperator.MATCHING || newSearchOperator == SearchOperator.NOT_MATCHING);

        _query.Parts[partId].Operator = newSearchOperator;

        if ((wasMatchingOperator && !becomesMatchingOperator) || (!wasMatchingOperator && becomesMatchingOperator))
            Utils.FormatQuery(ref sp_Query, _query, _isEditable, false, _searchType, SearchBuilder.GetTypeFromSearchType(_searchType));

#if DEBUG
        ShowNotificationMsg($"Changed operator for query part {partId}.", BackgroundColor.SUCCESS, 1500);
#endif
    }

    public void ModifyQueryValue(int partId, string newValue)
    {
        if (_query == null || _query.Parts == null || partId >= _query.Parts.Count)
            return;

        _query.Parts[partId].Value = newValue;

#if DEBUG
        ShowNotificationMsg($"Changed value for query part {partId}.", BackgroundColor.SUCCESS, 1500);
#endif
    }

    public void ModifyQueryLogicalOperator(int partId, LogicalOperator newLogicalOperator)
    {
        if (_query == null || _query.Parts == null || partId >= _query.Parts.Count)
            return;

        _query.Parts[partId].LogicalOperator = newLogicalOperator;

#if DEBUG
        ShowNotificationMsg($"Changed logical operator for query part {partId}.", BackgroundColor.SUCCESS, 1500);
#endif
    }

    public void RemoveQueryPart(int partId)
    {
        if (_query == null || _query.Parts == null || partId >= _query.Parts.Count)
            return;

        if (_query.Parts.Count <= 1)
        {
            ShowNotificationMsg(ASILang.Get("FilterRequiresOneCondition"), BackgroundColor.ERROR, 3000);
            return;
        }

        _query.Parts.RemoveAt(partId);
        Utils.FormatQuery(ref sp_Query, _query, _isEditable, false, _searchType, SearchBuilder.GetTypeFromSearchType(_searchType));

#if DEBUG
        ShowNotificationMsg($"Removed query part {partId}.", BackgroundColor.SUCCESS, 1500);
#endif
    }

    private void btn_SaveSearchQuery_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_queryName) && _query != null && SearchBuilder._queries != null && SearchBuilder._queries.Queries != null && SearchBuilder._queries.Queries.ContainsKey(_queryName))
        {
            SearchBuilder._queries.Queries[_queryName] = _query;
            if (SearchBuilder.SaveSearchQueries())
                MainWindow.ShowToast($"{ASILang.Get("FilterSaved")}", BackgroundColor.SUCCESS);
            else
                MainWindow.ShowToast($"{ASILang.Get("SaveFilterFailed")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
            this.Close();
        }
    }

    private void btn_CancelSaveSearchQuery_Click(object sender, RoutedEventArgs e) => this.Close();

    private void page_EditSearchQuery_Closed(object sender, WindowEventArgs args)
    {
        _query = null;
        _editSearchQuery = null;
    }
}
