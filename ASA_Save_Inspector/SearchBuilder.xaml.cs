using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.ObjectModelUtils;
using ASA_Save_Inspector.Pages;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace ASA_Save_Inspector
{
    public enum SearchType
    {
        PAWNS = 0,
        DINOS = 1,
        STRUCTURES = 2,
        ITEMS = 3,
        PLAYERS = 4,
        TRIBES = 5
    }

    public enum LogicalOperator
    {
        AND = 0,
        OR = 1
    }

    public enum SearchOperator
    {
        MATCHING = 0,
        NOT_MATCHING = 1,
        EQUALS = 2,
        NOT_EQUALS = 3,
        STARTING_WITH = 4,
        ENDING_WITH = 5,
        CONTAINING = 6,
        NOT_CONTAINING = 7,
        LOWER_THAN = 8,
        GREATER_THAN = 9
    }

    [DynamicLinqType]
    public static class LinqUtils
    {
        public static string GetObjAsString(object obj) => (obj != null ? obj.ToString() ?? string.Empty : string.Empty);
        public static string GetObjAsString(Int32 obj) => obj.ToString(CultureInfo.InvariantCulture);
        public static string GetObjAsString(Int64 obj) => obj.ToString(CultureInfo.InvariantCulture);
        public static string GetObjAsString(Single obj) => obj.ToString(CultureInfo.InvariantCulture);
        public static string GetObjAsString(Double obj) => obj.ToString(CultureInfo.InvariantCulture);
        public static string GetObjAsString(Int32? obj) => (obj != null && obj.HasValue ? obj.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        public static string GetObjAsString(Int64? obj) => (obj != null && obj.HasValue ? obj.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        public static string GetObjAsString(Single? obj) => (obj != null && obj.HasValue ? obj.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        public static string GetObjAsString(Double? obj) => (obj != null && obj.HasValue ? obj.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
    }

    public class SearchQueryPart
    {
        [JsonInclude]
        public SearchType Type { get; set; } = SearchType.PAWNS;
        [JsonInclude]
        public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.AND;
        [JsonInclude]
        public string? PropertyName { get; set; } = null;
        [JsonInclude]
        public string? PropertyCleanName { get; set; } = null;
        [JsonInclude]
        public SearchOperator Operator { get; set; } = SearchOperator.MATCHING;
        [JsonInclude]
        public string? Value { get; set; } = null;
        [JsonInclude]
        public int Group { get; set; } = 0;

        [JsonIgnore]
        private PropertyInfo? _prop = null;
        [JsonIgnore]
        public PropertyInfo? Property
        {
            get
            {
                if (_prop != null)
                    return _prop;
                Type objType = SearchBuilder.GetTypeFromSearchType(this.Type);
                _prop = Utils.GetProperty(objType, PropertyName);
                return _prop;
            }
            private set { }
        }

        [JsonIgnore]
        public bool IsComplexObject
        {
            get { return IsNonNullableComplexObject || IsNullableComplexObject; }
            private set { }
        }

        [JsonIgnore]
        public bool IsNonNullableComplexObject
        {
            get
            {
                return (Property != null && (
                    string.Compare(Property.PropertyType.ToString(), "ASA_Save_Inspector.ObjectModel.TypeStringValue", StringComparison.InvariantCulture) == 0 ||
                    string.Compare(Property.PropertyType.ToString(), "ASA_Save_Inspector.ObjectModel.TypeIntValue", StringComparison.InvariantCulture) == 0 ||
                    string.Compare(Property.PropertyType.ToString(), "ASA_Save_Inspector.ObjectModel.NameStringValue", StringComparison.InvariantCulture) == 0));
            }
            private set { }
        }

        [JsonIgnore]
        public bool IsNullableComplexObject
        {
            get
            {
                return (Property != null && (
                    string.Compare(Property.PropertyType.ToString(), "System.Nullable`1[ASA_Save_Inspector.ObjectModel.TypeStringValue]", StringComparison.InvariantCulture) == 0 ||
                    string.Compare(Property.PropertyType.ToString(), "System.Nullable`1[ASA_Save_Inspector.ObjectModel.TypeIntValue]", StringComparison.InvariantCulture) == 0 ||
                    string.Compare(Property.PropertyType.ToString(), "System.Nullable`1[ASA_Save_Inspector.ObjectModel.NameStringValue]", StringComparison.InvariantCulture) == 0));
            }
            private set { }
        }

        [JsonIgnore]
        public bool IsNumber
        {
            get { return IsNonNullableNumber || IsNullableNumber; }
            private set { }
        }

        [JsonIgnore]
        public bool IsNonNullableNumber
        {
            get
            {
                return (Property != null && (
                    string.Compare(Property.PropertyType.ToString(), "System.Int32", StringComparison.InvariantCulture) == 0 || 
                    string.Compare(Property.PropertyType.ToString(), "System.Int64", StringComparison.InvariantCulture) == 0 || 
                    string.Compare(Property.PropertyType.ToString(), "System.Single", StringComparison.InvariantCulture) == 0 || 
                    string.Compare(Property.PropertyType.ToString(), "System.Double", StringComparison.InvariantCulture) == 0));
            }
            private set { }
        }

        [JsonIgnore]
        public bool IsNullableNumber
        {
            get
            {
                return (Property != null && (
                    string.Compare(Property.PropertyType.ToString(), "System.Nullable`1[System.Int32]", StringComparison.InvariantCulture) == 0 ||
                    string.Compare(Property.PropertyType.ToString(), "System.Nullable`1[System.Int64]", StringComparison.InvariantCulture) == 0 ||
                    string.Compare(Property.PropertyType.ToString(), "System.Nullable`1[System.Single]", StringComparison.InvariantCulture) == 0 ||
                    string.Compare(Property.PropertyType.ToString(), "System.Nullable`1[System.Double]", StringComparison.InvariantCulture) == 0));
            }
            private set { }
        }

        [JsonIgnore]
        public bool IsBool
        {
            get { return IsNonNullableBool || IsNullableBool; }
            private set { }
        }

        [JsonIgnore]
        public bool IsNonNullableBool
        {
            get { return (Property != null && string.Compare(Property.PropertyType.ToString(), "System.Boolean", StringComparison.InvariantCulture) == 0); }
            private set { }
        }

        [JsonIgnore]
        public bool IsNullableBool
        {
            get { return (Property != null && string.Compare(Property.PropertyType.ToString(), "System.Nullable`1[System.Boolean]", StringComparison.InvariantCulture) == 0); }
            private set { }
        }

        public static string? ToJson(SearchQueryPart? part)
        {
            if (part == null)
                return null;

            string? json = null;
            try { json = JsonSerializer.Serialize(part, Utils.IndentedJson); }
            catch (Exception ex) { json = null; Logger.Instance.Log($"Failed to serialize SearchQueryPart into JSON. Exception=[{ex}]", Logger.LogLevel.ERROR); }
            return json;
        }

        public static SearchQueryPart? FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            SearchQueryPart? part = null;
            try { part = JsonSerializer.Deserialize<SearchQueryPart>(json); }
            catch (Exception ex) { part = null; Logger.Instance.Log($"Failed to deserialize SearchQueryPart from JSON. Exception=[{ex}]", Logger.LogLevel.ERROR); }
            return part;
        }
    }

    public class SearchQuery
    {
        [JsonInclude]
        public List<SearchQueryPart> Parts { get; set; } = new List<SearchQueryPart>();

        public string ToExpression()
        {
            if (Parts.Count <= 0)
                return string.Empty;

            string ret = string.Empty;
            int currentGroup = 0;
            for (int i = 0; i < Parts.Count; i++)
            {
                while (currentGroup > Parts[i].Group)
                {
                    ret += ")";
                    --currentGroup;
                }

                if (i > 0)
                    ret += Parts[i].LogicalOperator == LogicalOperator.OR ? " or " : " and ";

                while (currentGroup < Parts[i].Group)
                {
                    ret += "(";
                    ++currentGroup;
                }

                if (Parts[i].Operator == SearchOperator.NOT_MATCHING)
                    ret += (Parts[i].IsBool ? $"x.{Parts[i].PropertyName} != {(Parts[i].Value == "True" ? "true" : "false")}" : (Parts[i].IsNumber ? $" != {Parts[i].Value}" : $"LinqUtils.GetObjAsString(x.{Parts[i].PropertyName}) != \"{Parts[i].Value}\""));
                else if (Parts[i].Operator == SearchOperator.EQUALS)
                    ret += (Parts[i].IsBool ? $"x.{Parts[i].PropertyName} == {(Parts[i].Value == "True" ? "true" : "false")}" : (Parts[i].IsNumber ? $" == {Parts[i].Value}" : $"LinqUtils.GetObjAsString(x.{Parts[i].PropertyName}) == \"{Parts[i].Value}\""));
                else if (Parts[i].Operator == SearchOperator.NOT_EQUALS)
                    ret += (Parts[i].IsBool ? $"x.{Parts[i].PropertyName} != {(Parts[i].Value == "True" ? "true" : "false")}" : (Parts[i].IsNumber ? $" != {Parts[i].Value}" : $"LinqUtils.GetObjAsString(x.{Parts[i].PropertyName}) != \"{Parts[i].Value}\""));
                else if (Parts[i].Operator == SearchOperator.STARTING_WITH)
                    ret += $"LinqUtils.GetObjAsString(x.{Parts[i].PropertyName}).StartsWith(\"{Parts[i].Value}\") == true";
                else if (Parts[i].Operator == SearchOperator.ENDING_WITH)
                    ret += $"LinqUtils.GetObjAsString(x.{Parts[i].PropertyName}).EndsWith(\"{Parts[i].Value}\") == true";
                else if (Parts[i].Operator == SearchOperator.CONTAINING)
                    ret += $"LinqUtils.GetObjAsString(x.{Parts[i].PropertyName}).Contains(\"{Parts[i].Value}\") == true";
                else if (Parts[i].Operator == SearchOperator.NOT_CONTAINING)
                    ret += $"LinqUtils.GetObjAsString(x.{Parts[i].PropertyName}).Contains(\"{Parts[i].Value}\") == false";
                else if (Parts[i].Operator == SearchOperator.LOWER_THAN)
                    ret += $"x.{Parts[i].PropertyName} < {Parts[i].Value}";
                else if (Parts[i].Operator == SearchOperator.GREATER_THAN)
                    ret += $"x.{Parts[i].PropertyName} > {Parts[i].Value}";
                else // (Parts[i].Operator == SearchOperator.MATCHING)
                    ret += (Parts[i].IsBool ? $"x.{Parts[i].PropertyName} == {(Parts[i].Value == "True" ? "true" : "false")}" : (Parts[i].IsNumber ? $"x.{Parts[i].PropertyName} == {Parts[i].Value}" : $"LinqUtils.GetObjAsString(x.{Parts[i].PropertyName}) == \"{Parts[i].Value}\""));
            }

            while (currentGroup > 0)
            {
                ret += ")";
                --currentGroup;
            }

            return ret;
        }

        public override string ToString()
        {
            if (Parts.Count <= 0)
                return string.Empty;

            string ret = string.Empty;
            int currentGroup = 0;
            for (int i = 0; i < Parts.Count; i++)
            {
                while (currentGroup > Parts[i].Group)
                {
                    ret += ")";
                    --currentGroup;
                }

                if (i > 0)
                    ret += Parts[i].LogicalOperator == LogicalOperator.OR ? $" {ASILang.Get("OperatorOR")} " : $" {ASILang.Get("OperatorAND")} ";

                while (currentGroup < Parts[i].Group)
                {
                    ret += "(";
                    ++currentGroup;
                }

                ret += Parts[i].PropertyCleanName;

                ret += $" {Utils.GetSearchOperatorAsString(Parts[i].Operator)} ";

                ret += Parts[i].Value;
            }

            while (currentGroup > 0)
            {
                ret += ")";
                --currentGroup;
            }

            return ret;
        }

        public static string? ToJson(SearchQuery? query)
        {
            if (query == null || query.Parts == null || query.Parts.Count <= 0)
                return null;

            string? json = null;
            try { json = JsonSerializer.Serialize(query, Utils.IndentedJson); }
            catch (Exception ex) { json = null; Logger.Instance.Log($"Failed to serialize SearchQuery into JSON. Exception=[{ex}]", Logger.LogLevel.ERROR); }
            return json;
        }

        public static SearchQuery? FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            SearchQuery? query = null;
            try { query = JsonSerializer.Deserialize<SearchQuery>(json); }
            catch (Exception ex) { query = null; Logger.Instance.Log($"Failed to deserialize SearchQuery from JSON. Exception=[{ex}]", Logger.LogLevel.ERROR); }
            return query;
        }
    }

    public class SearchQueries
    {
        [JsonInclude]
        public Dictionary<string, SearchQuery> Queries { get; set; } = new Dictionary<string, SearchQuery>();

        public static string? ToJson(SearchQueries? queries)
        {
            if (queries == null || queries.Queries == null || queries.Queries.Count <= 0)
                return null;

            string? json = null;
            try { json = JsonSerializer.Serialize(queries, Utils.IndentedJson); }
            catch (Exception ex) { json = null; Logger.Instance.Log($"Failed to serialize SearchQueries into JSON. Exception=[{ex}]", Logger.LogLevel.ERROR); }
            return json;
        }

        public static SearchQueries? FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            SearchQueries? queries = null;
            try { queries = JsonSerializer.Deserialize<SearchQueries>(json); }
            catch (Exception ex) { queries = null; Logger.Instance.Log($"Failed to deserialize SearchQueries from JSON. Exception=[{ex}]", Logger.LogLevel.ERROR); }
            return queries;
        }
    }

    public sealed partial class SearchBuilder : Window
    {
        private const int MAX_PROPERTY_VALUES = 300;

        public static SearchBuilder? _searchBuilder = null;

        public static SearchQueries? _queries = null;
        public static string? _savedQueriesFilePath = null;
        public static SearchQuery? _query = null;

        public AppWindow? _appWindow = null;
        public SearchType _searchType = SearchType.PAWNS;
        public Type _searchObjType = typeof(PlayerPawn);
        public SearchOperator _operator = SearchOperator.EQUALS;
        public int _currentGroup = 0;

        private Dictionary<string, string> _properties = new Dictionary<string, string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public SearchBuilder()
        {
            InitializeComponent();
            _searchBuilder = this;
            _appWindow = this.AppWindow;

            // Calculate page center.
            AdjustToSizeChange();

            // Hide system title bar.
            ExtendsContentIntoTitleBar = true;
            if (ExtendsContentIntoTitleBar == true)
                this._appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            // Set title bar title.
            this.Title = $"{MainWindow._appName} v{Utils.GetVersionStr()} - {ASILang.Get("Filtering")}";
            TitleBarTextBlock.Text = $"{MainWindow._appName} v{Utils.GetVersionStr()} - {ASILang.Get("Filtering")}";

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

        public void Initialize(SearchType type)
        {
            this._searchType = type;
            this._searchObjType = GetTypeFromSearchType(type);
            // Fill query builder dropdown.
            FillPropertiesDropdown();
            // Init search statics.
            InitSearch(type);
            // Update queries dropdown.
            UpdateSavedSearchQueriesDropdownLocal();
        }

        public static void InitSearch(SearchType type)
        {
            // Init current search query.
            _query = new SearchQuery();
            // Load saved search queries.
            if (type == SearchType.PAWNS)
                _savedQueriesFilePath = Utils.PlayerPawnSearchQueriesFilePath();
            else if (type == SearchType.DINOS)
                _savedQueriesFilePath = Utils.DinoSearchQueriesFilePath();
            else if (type == SearchType.STRUCTURES)
                _savedQueriesFilePath = Utils.StructureSearchQueriesFilePath();
            else if (type == SearchType.ITEMS)
                _savedQueriesFilePath = Utils.ItemSearchQueriesFilePath();
            else if (type == SearchType.PLAYERS)
                _savedQueriesFilePath = Utils.PlayerSearchQueriesFilePath();
            else if (type == SearchType.TRIBES)
                _savedQueriesFilePath = Utils.TribeSearchQueriesFilePath();
            else
                _savedQueriesFilePath = null;
            LoadSearchQueries();
            // Update saved search queries in main window.
            UpdateSavedSearchQueriesDropdownInMain(type);
        }

        private void page_SearchBuilder_SizeChanged(object sender, WindowSizeChangedEventArgs args)
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
            WindowWidth = Math.Max(1, Convert.ToInt32(Math.Round(this.Bounds.Width)) - 52);
            WindowHeight = Math.Max(1, Convert.ToInt32(Math.Round(this.Bounds.Height)) - 52);
        }

        private void FillPropertiesDropdown()
        {
            _properties.Clear();

            PropertyInfo[]? properties = _searchObjType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties != null && properties.Count() > 0)
            {
                List<string> toAdd = new List<string>();
                foreach (var p in properties)
                {
                    if (!string.IsNullOrEmpty(p.Name))
                    {
                        string? cleanName = DinoUtils.GetCleanNameFromPropertyName(p.Name);
                        if (!string.IsNullOrEmpty(cleanName))
                        {
                            toAdd.Add(cleanName);
                            _properties[cleanName] = p.Name;
                        }
                    }
                }
                if (toAdd.Count > 0)
                {
                    toAdd.Sort();
                    foreach (string cleanName in toAdd)
                        if (!string.IsNullOrEmpty(cleanName))
                            cbb_Property.Items.Add(cleanName);
                }
                toAdd.Clear();
            }
        }

        private IEnumerable<object?>? GetData(SearchType type)
        {
            if (type == SearchType.DINOS)
                return SettingsPage._dinosData;
            else if (type == SearchType.STRUCTURES)
                return SettingsPage._structuresData;
            else if (type == SearchType.ITEMS)
                return SettingsPage._itemsData;
            else if (type == SearchType.PLAYERS)
                return SettingsPage._playersData;
            else if (type == SearchType.TRIBES)
                return SettingsPage._tribesData;
            else
                return SettingsPage._playerPawnsData;
        }

        private void SelectionChanged()
        {
            cbb_Value.Visibility = Visibility.Collapsed;
            tb_Value.Visibility = Visibility.Collapsed;
            cbb_Value.Items.Clear();
            if (_operator == SearchOperator.MATCHING || _operator == SearchOperator.NOT_MATCHING)
            {
                string? cleanPropertyName = Utils.GetComboBoxSelection(cbb_Property, false);
                if (!string.IsNullOrEmpty(cleanPropertyName) && _properties.ContainsKey(cleanPropertyName))
                {
                    PropertyInfo? property = Utils.GetProperty(_searchObjType, _properties[cleanPropertyName]);
                    List<string> propertyValues = Utils.GetPropertyValues(GetData(_searchType), property, MAX_PROPERTY_VALUES);
                    if (propertyValues.Count > 0)
                    {
                        propertyValues.Sort();
                        foreach (string val in propertyValues)
                            if (!string.IsNullOrEmpty(val))
                                cbb_Value.Items.Add(val);
                    }
                }
                cbb_Value.Visibility = Visibility.Visible;
            }
            else
                tb_Value.Visibility = Visibility.Visible;
        }

        private void cbb_Operator_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(sender, true);
            if (string.IsNullOrEmpty(selected))
                return;

            _operator = Utils.GetSearchOperatorFromString(selected);

            SelectionChanged();
        }

        private void cbb_Property_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            cbb_Operator.IsEnabled = true;
            cbi_GreaterThan.IsEnabled = true;
            cbi_LowerThan.IsEnabled = true;
            string? cleanPropertyName = Utils.GetComboBoxSelection(cbb_Property, false);
            if (!string.IsNullOrEmpty(cleanPropertyName) && _properties.ContainsKey(cleanPropertyName))
            {
                PropertyInfo? property = Utils.GetProperty(_searchObjType, _properties[cleanPropertyName]);
                if (property != null)
                {
                    string propertyType = property.PropertyType.ToString();
                    if (!string.IsNullOrEmpty(propertyType))
                    {
                        bool isNumber = false;
                        if (string.Compare(propertyType, "System.Boolean", StringComparison.InvariantCulture) == 0 ||
                            string.Compare(propertyType, "System.Nullable`1[System.Boolean]", StringComparison.InvariantCulture) == 0)
                        {
                            cbb_Operator.SelectedIndex = 0; // Set operator to "Matching".
                            cbb_Operator.IsEnabled = false;
                        }
                        else if (string.Compare(propertyType, "System.Int32", StringComparison.InvariantCulture) == 0 ||
                            string.Compare(propertyType, "System.Int64", StringComparison.InvariantCulture) == 0 ||
                            string.Compare(propertyType, "System.Single", StringComparison.InvariantCulture) == 0 ||
                            string.Compare(propertyType, "System.Double", StringComparison.InvariantCulture) == 0 ||
                            string.Compare(propertyType, "System.Nullable`1[System.Int32]", StringComparison.InvariantCulture) == 0 ||
                            string.Compare(propertyType, "System.Nullable`1[System.Int64]", StringComparison.InvariantCulture) == 0 ||
                            string.Compare(propertyType, "System.Nullable`1[System.Single]", StringComparison.InvariantCulture) == 0 ||
                            string.Compare(propertyType, "System.Nullable`1[System.Double]", StringComparison.InvariantCulture) == 0)
                        {
                            isNumber = true;
                        }
                        if (!isNumber)
                        {
                            if (cbb_Operator.SelectedIndex == 8 || cbb_Operator.SelectedIndex == 9) // GreaterThan or LowerThan currently selected.
                                cbb_Operator.SelectedIndex = 2; // Set operator to "Equals".
                            cbi_GreaterThan.IsEnabled = false;
                            cbi_LowerThan.IsEnabled = false;
                        }
                    }
                }
            }

            SelectionChanged();
        }

        private void btn_StartGroup_Click(object sender, RoutedEventArgs e)
        {
            ++_currentGroup;
            btn_EndGroup.IsEnabled = true;
            tb_CurrentGroupLabelValue.Text = _currentGroup.ToString(CultureInfo.InvariantCulture);
        }

        private void btn_EndGroup_Click(object sender, RoutedEventArgs e)
        {
            --_currentGroup;
            if (_currentGroup == 0)
                btn_EndGroup.IsEnabled = false;
            tb_CurrentGroupLabelValue.Text = _currentGroup.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateBtnsStates()
        {
            bool hasQueryParts = (_query != null && _query.Parts != null && _query.Parts.Count > 0);
            cbb_LogicalOperator.Visibility = hasQueryParts ? Visibility.Visible : Visibility.Collapsed;
            btn_RemovePreviouslyAdded.IsEnabled = hasQueryParts;
        }

        private void btn_Add_Click(object sender, RoutedEventArgs e)
        {
            string? cleanPropertyName = Utils.GetComboBoxSelection(cbb_Property, false);
            if (string.IsNullOrEmpty(cleanPropertyName) || !_properties.ContainsKey(cleanPropertyName))
                return;
            string? value = (_operator == SearchOperator.MATCHING || _operator == SearchOperator.NOT_MATCHING) ? cbb_Value.SelectedItem?.ToString() : tb_Value.Text;
            if (string.IsNullOrEmpty(value))
                return;
            if (_query == null)
                return;

            string? logicalOperator = Utils.GetComboBoxSelection(cbb_LogicalOperator, true);
            LogicalOperator lo = (!string.IsNullOrEmpty(logicalOperator) && string.Compare(logicalOperator, ASILang.Get("OperatorOR"), StringComparison.InvariantCulture) == 0) ? LogicalOperator.OR : LogicalOperator.AND;

            _query.Parts.Add(new SearchQueryPart()
            {
                LogicalOperator = lo,
                PropertyName = _properties[cleanPropertyName],
                PropertyCleanName = cleanPropertyName,
                Operator = _operator,
                Value = value,
                Group = _currentGroup
            });

            UpdateBtnsStates();
            ShowQuery();
        }

        public static IEnumerable<object>? DoSearchQuery(SearchType type, bool showToastsInMainWindow)
        {
            if (_query == null || _query.Parts == null || _query.Parts.Count <= 0)
            {
                if (showToastsInMainWindow)
                    MainWindow.ShowToast(ASILang.Get("EmptyFilter"), BackgroundColor.WARNING);
                else
                    SearchBuilder.ShowToast(ASILang.Get("EmptyFilter"), BackgroundColor.WARNING);
                return null;
            }

            string expression = $"x => {_query.ToExpression()}";
            IEnumerable<object>? results = null;
            try
            {
                if (type == SearchType.DINOS)
                {
                    if (SettingsPage._dinosData != null)
                        results = SettingsPage._dinosData.AsQueryable().Where(expression);
                }
                else if (type == SearchType.STRUCTURES)
                {
                    if (SettingsPage._structuresData != null)
                        results = SettingsPage._structuresData.AsQueryable().Where(expression);
                }
                else if (type == SearchType.ITEMS)
                {
                    if (SettingsPage._itemsData != null)
                        results = SettingsPage._itemsData.AsQueryable().Where(expression);
                }
                else if (type == SearchType.PLAYERS)
                {
                    if (SettingsPage._playersData != null)
                        results = SettingsPage._playersData.AsQueryable().Where(expression);
                }
                else if (type == SearchType.TRIBES)
                {
                    if (SettingsPage._tribesData != null)
                        results = SettingsPage._tribesData.AsQueryable().Where(expression);
                }
                else
                {
                    if (SettingsPage._playerPawnsData != null)
                        results = SettingsPage._playerPawnsData.AsQueryable().Where(expression);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("FilteringError")} Expression=[{expression}] Exception=[{ex}]", Logger.LogLevel.ERROR);
                if (showToastsInMainWindow)
                    MainWindow.ShowToast($"{ASILang.Get("FilteringError")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                else
                    SearchBuilder.ShowToast($"{ASILang.Get("FilteringError")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
                results = null;
            }
            return results;
        }

        private void btn_TryQuery_Click(object sender, RoutedEventArgs e) => DoSearchQuery(_searchType, false);

        private void ShowQuery() => Utils.FormatQuery(ref sp_QueryDisplay, _query, false);

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
                    FadeOutStoryboard.Begin();
                    await Task.Delay(600);
                    if (PopupNotification.IsOpen)
                    {
                        b_popupNotification.Opacity = 0;
                        PopupNotification.IsOpen = false;
                    }
                });
            }
        }

        public static Type GetTypeFromSearchType(SearchType type)
        {
            if (type == SearchType.DINOS)
                return typeof(Dino);
            else if (type == SearchType.STRUCTURES)
                return typeof(Structure);
            else if (type == SearchType.ITEMS)
                return typeof(Item);
            else if (type == SearchType.PLAYERS)
                return typeof(Player);
            else if (type == SearchType.TRIBES)
                return typeof(Tribe);
            else
                return typeof(PlayerPawn);
        }

        public static void ShowToast(string msg, BackgroundColor color = BackgroundColor.DEFAULT, int duration = 4000)
        {
            if (duration < 1100)
                duration = 1100;
#pragma warning disable CS1998
            if (SearchBuilder._searchBuilder != null)
                SearchBuilder._searchBuilder.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    SearchBuilder._searchBuilder.ShowNotificationMsg(msg, color, duration);
                });
#pragma warning restore CS1998
        }

        private void btn_SaveQuery_Click(object sender, RoutedEventArgs e)
        {
            if (SaveQueryPopup.IsOpen)
                return;
            if (_query == null || _query.Parts == null || _query.Parts.Count <= 0)
            {
                ShowNotificationMsg($"{ASILang.Get("EmptyFilter")} {ASILang.Get("NothingToSave")}", BackgroundColor.WARNING);
                return;
            }

            SaveQueryPopup.IsOpen = true;

            /*
            string? json = SearchQuery.ToJson(_query);
            if (!string.IsNullOrWhiteSpace(json))
                try { File.WriteAllText("C:\\Users\\Mangem0rt\\Documents\\test_query.json", json, Encoding.UTF8); }
                catch (Exception ex) { Logger.Instance.Log($"Failed to save SearchQuery to JSON at \"C:\\Users\\Mangem0rt\\Documents\\test_query.json\". Exception=[{ex}]"); }
            */
        }

        private void btn_CancelQuery_Click(object sender, RoutedEventArgs e) => this.Close();

        private void btn_LoadQuery_Click(object sender, RoutedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(cbb_ExistingQueries, false);
            if (string.IsNullOrWhiteSpace(selected))
            {
                ShowNotificationMsg(ASILang.Get("NoFilterSelected"), BackgroundColor.WARNING);
                return;
            }
            if (_queries != null && _queries.Queries != null && _queries.Queries.ContainsKey(selected) && _queries.Queries[selected] != null)
            {
                _query = _queries.Queries[selected];
                ShowNotificationMsg(ASILang.Get("FilterLoaded"), BackgroundColor.SUCCESS);
                ShowQuery();
            }
            else
            {
                ShowNotificationMsg(ASILang.Get("FilterNotFound"), BackgroundColor.WARNING);
                return;
            }

            /*
            if (!File.Exists("C:\\Users\\Mangem0rt\\Documents\\test_query.json"))
                return;

            string? json = null;
            try { json = File.ReadAllText("C:\\Users\\Mangem0rt\\Documents\\test_query.json", Encoding.UTF8); }
            catch (Exception ex) { json = null; Logger.Instance.Log($"Failed to read SearchQuery from JSON at \"C:\\Users\\Mangem0rt\\Documents\\test_query.json\". Exception=[{ex}]"); }
            if (string.IsNullOrWhiteSpace(json))
                return;
            _query = SearchQuery.FromJson(json);
            */
        }

        private void tb_SearchQueryName_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_SaveSearchQuery.IsEnabled = (tb_SearchQueryName.Text.Length > 0);
        }

        private static void UpdateSavedSearchQueriesDropdownInMain(SearchType type)
        {
            if (type == SearchType.PAWNS)
            {
                if (PlayerPawnsPage._page != null)
                    PlayerPawnsPage._page.UpdateSavedSearchQueriesDropdown();
            }
            else if (type == SearchType.DINOS)
            {
                if (DinosPage._page != null)
                    DinosPage._page.UpdateSavedSearchQueriesDropdown();
            }
            else if (type == SearchType.STRUCTURES)
            {
                if (StructuresPage._page != null)
                    StructuresPage._page.UpdateSavedSearchQueriesDropdown();
            }
            else if (type == SearchType.ITEMS)
            {
                if (ItemsPage._page != null)
                    ItemsPage._page.UpdateSavedSearchQueriesDropdown();
            }
            else if (type == SearchType.PLAYERS)
            {
                if (PlayersPage._page != null)
                    PlayersPage._page.UpdateSavedSearchQueriesDropdown();
            }
            else if (type == SearchType.TRIBES)
            {
                if (TribesPage._page != null)
                    TribesPage._page.UpdateSavedSearchQueriesDropdown();
            }
        }

        private void UpdateSavedSearchQueriesDropdownLocal()
        {
            // Update dropdown in SearchBuilder
            cbb_ExistingQueries.Items.Clear();
            if (_queries != null && _queries.Queries != null && _queries.Queries.Count > 0)
                foreach (var q in _queries.Queries)
                    cbb_ExistingQueries.Items.Add(q.Key);
        }

        public static void LoadSearchQueries()
        {
            if (!string.IsNullOrWhiteSpace(_savedQueriesFilePath) && File.Exists(_savedQueriesFilePath))
            {
                string? savedQueriesTxt = null;
                try { savedQueriesTxt = File.ReadAllText(_savedQueriesFilePath, Encoding.UTF8); }
                catch (Exception ex) { savedQueriesTxt = null; Logger.Instance.Log($"Failed to read file content at \"{_savedQueriesFilePath}\". Exception=[{ex}]", Logger.LogLevel.ERROR); }
                if (!string.IsNullOrWhiteSpace(savedQueriesTxt))
                    _queries = SearchQueries.FromJson(savedQueriesTxt);
            }
        }

        public static bool SaveSearchQueries()
        {
            if (!string.IsNullOrWhiteSpace(_savedQueriesFilePath))
            {
                string? queriesJson = SearchQueries.ToJson(_queries);
                try { File.WriteAllText(_savedQueriesFilePath, queriesJson, Encoding.UTF8); return true; }
                catch (Exception ex) { Logger.Instance.Log($"Failed to save search queries to \"{_savedQueriesFilePath}\". Exception=[{ex}]", Logger.LogLevel.ERROR); }
            }
            return false;
        }

        private void CloseSaveSearchQueryPopup()
        {
            if (SaveQueryPopup.IsOpen)
            {
                tb_SearchQueryName.Text = string.Empty;
                btn_SaveSearchQuery.IsEnabled = false;
                SaveQueryPopup.IsOpen = false;
            }
        }

        private void SavedSearchQueriesChanged(bool closeSaveSearchQueryPopup)
        {
            UpdateSavedSearchQueriesDropdownLocal();
            UpdateSavedSearchQueriesDropdownInMain(_searchType);
            if (SaveSearchQueries())
            {
                ShowNotificationMsg($"{ASILang.Get("FilterSaved")}", BackgroundColor.SUCCESS);
                if (closeSaveSearchQueryPopup)
                    CloseSaveSearchQueryPopup();
            }
            else
                ShowNotificationMsg($"{ASILang.Get("SaveFilterFailed")} {ASILang.Get("SeeLogsForDetails")}", BackgroundColor.ERROR);
        }

        private void btn_SaveSearchQuery_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tb_SearchQueryName.Text))
            {
                ShowNotificationMsg(ASILang.Get("EmptyFilterNameError"), BackgroundColor.WARNING);
                return;
            }
            if (_queries != null && _queries.Queries != null && _queries.Queries.ContainsKey(tb_SearchQueryName.Text))
            {
                ShowNotificationMsg(ASILang.Get("FilterNameAlreadyExists"), BackgroundColor.WARNING);
                return;
            }
            if (_query == null)
                return;

            if (_queries == null)
                _queries = new SearchQueries();
            if (_queries.Queries == null)
                _queries.Queries = new Dictionary<string, SearchQuery>();
            _queries.Queries.Add(tb_SearchQueryName.Text, _query);
            SavedSearchQueriesChanged(true);
        }

        private void btn_CancelSaveSearchQuery_Click(object sender, RoutedEventArgs e) => CloseSaveSearchQueryPopup();

        private void cbb_ExistingQueries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(cbb_ExistingQueries, false);
            if (!string.IsNullOrWhiteSpace(selected) && _queries != null && _queries.Queries != null && _queries.Queries.ContainsKey(selected))
            {
                btn_LoadQuery.IsEnabled = true;
                btn_DeleteQuery.IsEnabled = true;
            }
            else
            {
                btn_LoadQuery.IsEnabled = false;
                btn_DeleteQuery.IsEnabled = false;
            }
        }

        private void btn_DeleteQuery_Click(object sender, RoutedEventArgs e)
        {
            string? selected = Utils.GetComboBoxSelection(cbb_ExistingQueries, false);
            if (!string.IsNullOrWhiteSpace(selected) && _queries != null && _queries.Queries != null && _queries.Queries.ContainsKey(selected))
            {
                _queries.Queries.Remove(selected);
                SavedSearchQueriesChanged(false);
            }
            else
                ShowNotificationMsg(ASILang.Get("FilterNotFound"), BackgroundColor.WARNING);
        }

        private void page_SearchBuilder_Closed(object sender, WindowEventArgs args)
        {
            //_queries = null;
            //_savedQueriesFilePath = null;
            _query = null;
            _searchBuilder = null;
        }

        private void btn_RemovePreviouslyAdded_Click(object sender, RoutedEventArgs e)
        {
            if (_query == null || _query.Parts == null || _query.Parts.Count <= 0)
            {
                UpdateBtnsStates();
                return;
            }

            _query.Parts.RemoveAt(_query.Parts.Count - 1);

            UpdateBtnsStates();
            ShowQuery();
        }
    }
}
