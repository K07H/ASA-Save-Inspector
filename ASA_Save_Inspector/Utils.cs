using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ASA_Save_Inspector.ObjectModel;
using ASA_Save_Inspector.Pages;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace ASA_Save_Inspector
{
    internal static class Utils
    {
        private static Version? _version = null;
#if DEBUG
        public static bool DoCheckForPropertyValuesAmount = true;
#else
        public static bool DoCheckForPropertyValuesAmount = false;
#endif

        public static readonly string ASILatestVersionUrl = "https://github.com/K07H/ASA-Save-Inspector/releases/latest";
        public static readonly string ASIVersionFileUrl = "https://raw.githubusercontent.com/K07H/ASA-Save-Inspector/refs/heads/main/Version.txt";
        public static readonly string ArkParseArchiveUrl = "https://github.com/K07H/ark-save-parser/archive/refs/heads/main.zip";
        public static readonly string ArkParseVersionFileUrl = "https://github.com/K07H/ark-save-parser/raw/refs/heads/main/ASI_VERSION.txt";

        public static string GetBaseDir() => AppDomain.CurrentDomain.BaseDirectory; //AppContext.BaseDirectory
        public static string GetBaseDirParent()
        {
            try { return Path.GetFullPath(Path.Combine(GetBaseDir(), "..")); }
            catch { return string.Empty; }
        }

        public static readonly Dictionary<string, string> ConfigFiles = new Dictionary<string, string>()
        {
            { "settings.json", "ASISettings" },
            { "export_profiles.json", "ExportProfiles" },
            { "export_presets.json", "ExportPresets" },
            { "custom_blueprints.json", "CustomBlueprints" },
            { "dino_filters.json", "DinoFilters" },
            { "dino_groups.json", "DinoGroups" },
            { "dino_columns.json", "DinoColumns" },
            { "dino_columns_order.json", "DinoColumnsOrder" },
            { "pawn_filters.json", "PawnFilters" },
            { "pawn_groups.json", "PawnGroups" },
            { "pawn_columns.json", "PawnColumns" },
            { "pawn_columns_order.json", "PawnColumnsOrder" },
            { "structure_filters.json", "StructureFilters" },
            { "structure_groups.json", "StructureGroups" },
            { "structure_columns.json", "StructureColumns" },
            { "structure_columns_order.json", "StructureColumnsOrder" },
            { "item_filters.json", "ItemFilters" },
            { "item_groups.json", "ItemGroups" },
            { "item_columns.json", "ItemColumns" },
            { "item_columns_order.json", "ItemColumnsOrder" },
            { "player_filters.json", "PlayerFilters" },
            { "player_groups.json", "PlayerGroups" },
            { "player_columns.json", "PlayerColumns" },
            { "player_columns_order.json", "PlayerColumnsOrder" },
            { "tribe_filters.json", "TribeFilters" },
            { "tribe_groups.json", "TribeGroups" },
            { "tribe_columns.json", "TribeColumns" },
            { "tribe_columns_order.json", "TribeColumnsOrder" }
        };

        public static string GetLocalAppDataDir() => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string GetASIAppDataDir() => Path.Combine(GetLocalAppDataDir(), "Programs", "ASA Save Inspector");
        public static string GetDataDir() => Path.Combine(GetLocalAppDataDir(), "Programs", "ASA Save Inspector"); //Path.Combine(GetBaseDir(), "data");
        public static string GetAssetsDir() => Path.Combine(GetBaseDir(), "Assets");
        public static string GetLangDir() => Path.Combine(GetDataDir(), "Languages");
        public static string PythonVenvFolder() => Path.Combine(GetDataDir(), "python_venv");
        public static string ArkParseFolder() => Path.Combine(GetDataDir(), "ark-save-parser-main");
        public static string JsonExportsFolder() => Path.Combine(GetDataDir(), "json_exports");
        public static string GetTempDir() => Path.Combine(GetDataDir(), "temp");

        public static string PythonFilePathFromVenv() => Path.Combine(PythonVenvFolder(), "Scripts", "python.exe");
        public static string ActivatePythonVenvFilePath() => Path.Combine(PythonVenvFolder(), "Scripts", "activate.bat");
        public static string DeactivatePythonVenvFilePath() => Path.Combine(PythonVenvFolder(), "Scripts", "deactivate.bat");
        public static string LogsFilePath() => Path.Combine(GetDataDir(), "logs.txt");
        public static string PreviousLogsFilePath() => Path.Combine(GetDataDir(), "logs_previous.txt");
        public static string MapsInfoFilePath() => Path.Combine(GetDataDir(), "maps_info.json");
        public static string SettingsFilePath() => Path.Combine(GetDataDir(), "settings.json");
        public static string CustomBlueprintsFilePath() => Path.Combine(GetDataDir(), "custom_blueprints.json");
        public static string PlayerPawnSearchQueriesFilePath() => Path.Combine(GetDataDir(), "pawn_search_queries.json");
        public static string DinoSearchQueriesFilePath() => Path.Combine(GetDataDir(), "dino_search_queries.json");
        public static string StructureSearchQueriesFilePath() => Path.Combine(GetDataDir(), "structure_search_queries.json");
        public static string ItemSearchQueriesFilePath() => Path.Combine(GetDataDir(), "item_search_queries.json");
        public static string PlayerSearchQueriesFilePath() => Path.Combine(GetDataDir(), "player_search_queries.json");
        public static string TribeSearchQueriesFilePath() => Path.Combine(GetDataDir(), "tribe_search_queries.json");
        public static string DinoFiltersPresetsFilePath() => Path.Combine(GetDataDir(), "dino_filters.json");
        public static string DinoGroupsPresetsFilePath() => Path.Combine(GetDataDir(), "dino_groups.json");
        public static string DinoColumnsPresetsFilePath() => Path.Combine(GetDataDir(), "dino_columns.json");
        public static string DinoColumnsOrderFilePath() => Path.Combine(GetDataDir(), "dino_columns_order.json");
        public static string PlayerPawnFiltersPresetsFilePath() => Path.Combine(GetDataDir(), "pawn_filters.json");
        public static string PlayerPawnGroupsPresetsFilePath() => Path.Combine(GetDataDir(), "pawn_groups.json");
        public static string PlayerPawnColumnsPresetsFilePath() => Path.Combine(GetDataDir(), "pawn_columns.json");
        public static string PlayerPawnColumnsOrderFilePath() => Path.Combine(GetDataDir(), "pawn_columns_order.json");
        public static string StructureFiltersPresetsFilePath() => Path.Combine(GetDataDir(), "structure_filters.json");
        public static string StructureGroupsPresetsFilePath() => Path.Combine(GetDataDir(), "structure_groups.json");
        public static string StructureColumnsPresetsFilePath() => Path.Combine(GetDataDir(), "structure_columns.json");
        public static string StructureColumnsOrderFilePath() => Path.Combine(GetDataDir(), "structure_columns_order.json");
        public static string ItemFiltersPresetsFilePath() => Path.Combine(GetDataDir(), "item_filters.json");
        public static string ItemGroupsPresetsFilePath() => Path.Combine(GetDataDir(), "item_groups.json");
        public static string ItemColumnsPresetsFilePath() => Path.Combine(GetDataDir(), "item_columns.json");
        public static string ItemColumnsOrderFilePath() => Path.Combine(GetDataDir(), "item_columns_order.json");
        public static string PlayerFiltersPresetsFilePath() => Path.Combine(GetDataDir(), "player_filters.json");
        public static string PlayerGroupsPresetsFilePath() => Path.Combine(GetDataDir(), "player_groups.json");
        public static string PlayerColumnsPresetsFilePath() => Path.Combine(GetDataDir(), "player_columns.json");
        public static string PlayerColumnsOrderFilePath() => Path.Combine(GetDataDir(), "player_columns_order.json");
        public static string TribeFiltersPresetsFilePath() => Path.Combine(GetDataDir(), "tribe_filters.json");
        public static string TribeGroupsPresetsFilePath() => Path.Combine(GetDataDir(), "tribe_groups.json");
        public static string TribeColumnsPresetsFilePath() => Path.Combine(GetDataDir(), "tribe_columns.json");
        public static string TribeColumnsOrderFilePath() => Path.Combine(GetDataDir(), "tribe_columns_order.json");
        public static string ExportProfilesFilePath() => Path.Combine(GetDataDir(), "export_profiles.json");
        public static string ExportPresetsFilePath() => Path.Combine(GetDataDir(), "export_presets.json");
        public static string DontCheckForUpdateFilePath() => Path.Combine(GetDataDir(), "skip_update_check.txt");
        public static string DontReimportPreviousDataFilePath() => Path.Combine(GetDataDir(), "skip_reimport_previous_data.txt");
        public static string ArkParseVersionFilePath() => Path.Combine(ArkParseFolder(), "ASI_VERSION.txt");
        public static string ArkParseJsonApiFilePath() => Path.Combine(ArkParseFolder(), "src", "arkparse", "api", "json_api.py");
        public static string ArkParseArchiveFilePath() => Path.Combine(GetDataDir(), "ark-save-parser-main.zip");
        public static string ASIArchiveFilePath() => Path.Combine(GetTempDir(), "ASA_Save_Inspector_vVERSIONSTR_WinX64.zip");
        public static string AsiExportFastOrigFilePath() => Path.Combine(GetAssetsDir(), "asi_export_fast.py");
        public static string AsiExportFastFilePath() => Path.Combine(GetDataDir(), "asi_export_fast.py");
        public static string PythonVenvSetupFilePath() => Path.Combine(GetDataDir(), "python_venv_setup.bat");
        public static string PythonVenvTestScriptPath() => Path.Combine(GetDataDir(), "python_venv_test.py");
        public static string PythonVenvTestBatchPath() => Path.Combine(GetDataDir(), "python_venv_test.bat");
        public static string ArkParseSetupFilePath() => Path.Combine(GetDataDir(), "arkparse_setup.bat");
        public static string ArkParseRunnerFilePath() => Path.Combine(GetDataDir(), "arkparse_runner.bat");

        public static readonly string[] FilterableTypes = new string[]
        {
            "System.String",
            "System.Boolean",
            "System.Int32",
            "System.Int64",
            "System.Single",
            "System.Double",
            "ASA_Save_Inspector.ObjectModel.TypeStringValue",
            "ASA_Save_Inspector.ObjectModel.TypeIntValue",
            "ASA_Save_Inspector.ObjectModel.NameStringValue",
            "System.Nullable`1[System.String]",
            "System.Nullable`1[System.Boolean]",
            "System.Nullable`1[System.Int32]",
            "System.Nullable`1[System.Int64]",
            "System.Nullable`1[System.Single]",
            "System.Nullable`1[System.Double]",
            "System.Nullable`1[ASA_Save_Inspector.ObjectModel.TypeStringValue]",
            "System.Nullable`1[ASA_Save_Inspector.ObjectModel.TypeIntValue]",
            "System.Nullable`1[ASA_Save_Inspector.ObjectModel.NameStringValue]",
        };

        public static readonly List<string> FilterBooleanTypes = new List<string>()
        {
            "System.Boolean",
            "System.Nullable`1[System.Boolean]",
        };

        public static readonly List<string> FilterNumberTypes = new List<string>()
        {
            "System.Int32",
            "System.Int64",
            "System.Single",
            "System.Double",
            "System.Nullable`1[System.Int32]",
            "System.Nullable`1[System.Int64]",
            "System.Nullable`1[System.Single]",
            "System.Nullable`1[System.Double]",
        };

        public static List<ArkMapInfo> _allMaps = new List<ArkMapInfo>()
        {
            new ArkMapInfo() { MapName = "The Island", MinimapFilename = "TheIsland_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -342900.0,
                    OriginMinY = -342900.0,
                    OriginMinZ = -15000.0,
                    OriginMaxX = 342900.0,
                    OriginMaxY = 342900.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -342900.0,
                    PlayableMinY = -342900.0,
                    PlayableMinZ = -15000.0,
                    PlayableMaxX = 342900.0,
                    PlayableMaxY = 342900.0,
                    PlayableMaxZ = 54695.0
                }
            },
            new ArkMapInfo() { MapName = "Scorched Earth", MinimapFilename = "ScorchedEarth_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -393650.0,
                    OriginMinY = -393650.0,
                    OriginMinZ = -25515.0,
                    OriginMaxX = 393750.0,
                    OriginMaxY = 393750.0,
                    OriginMaxZ = 66645.0,
                    PlayableMinX = -393650.0,
                    PlayableMinY = -393650.0,
                    PlayableMinZ = -25515.0,
                    PlayableMaxX = 393750.0,
                    PlayableMaxY = 393750.0,
                    PlayableMaxZ = 66645.0
                }
            },
            new ArkMapInfo() { MapName = "The Center", MinimapFilename = "TheCenter_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -524364.0,
                    OriginMinY = -337215.0,
                    OriginMinZ = -171880.46875,
                    OriginMaxX = 513040.0,
                    OriginMaxY = 700189.0,
                    OriginMaxZ = 101159.6875,
                    PlayableMinX = -524364.0,
                    PlayableMinY = -337215.0,
                    PlayableMinZ = -171880.46875,
                    PlayableMaxX = 513040.0,
                    PlayableMaxY = 700189.0,
                    PlayableMaxZ = 101159.6875
                }
            },
            new ArkMapInfo() { MapName = "Aberration", MinimapFilename = "Aberration_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -400000.0,
                    OriginMinY = -400000.0,
                    OriginMinZ = -15000.0,
                    OriginMaxX = 400000.0,
                    OriginMaxY = 400000.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -400000.0,
                    PlayableMinY = -400000.0,
                    PlayableMinZ = -15000.0,
                    PlayableMaxX = 400000.0,
                    PlayableMaxY = 400000.0,
                    PlayableMaxZ = 54695.0
                }
            },
            new ArkMapInfo() { MapName = "Extinction", MinimapFilename = "Extinction_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -342900.0,
                    OriginMinY = -342900.0,
                    OriginMinZ = -15000.0,
                    OriginMaxX = 342900.0,
                    OriginMaxY = 342900.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -342900.0,
                    PlayableMinY = -342900.0,
                    PlayableMinZ = -15000.0,
                    PlayableMaxX = 342900.0,
                    PlayableMaxY = 342900.0,
                    PlayableMaxZ = 54695.0
                }
            },
            new ArkMapInfo() { MapName = "Ragnarok", MinimapFilename = "Ragnarok_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -655000.0,
                    OriginMinY = -655000.0,
                    OriginMinZ = -655000.0,
                    OriginMaxX = 655000.0,
                    OriginMaxY = 655000.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -655000.0,
                    PlayableMinY = -655000.0,
                    PlayableMinZ = -100000.0,
                    PlayableMaxX = 655000.0,
                    PlayableMaxY = 655000.0,
                    PlayableMaxZ = 655000.0
                }
            },
            new ArkMapInfo() { MapName = "Valguero", MinimapFilename = "Valguero_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -408000.0,
                    OriginMinY = -408000.0,
                    OriginMinZ = -655000.0,
                    OriginMaxX = 408000.0,
                    OriginMaxY = 408000.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -408000.0,
                    PlayableMinY = -408000.0,
                    PlayableMinZ = -100000.0,
                    PlayableMaxX = 408000.0,
                    PlayableMaxY = 408000.0,
                    PlayableMaxZ = 655000.0
                }
            },
            new ArkMapInfo() { MapName = "Astraeos", MinimapFilename = "Astraeos_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -800000.0,
                    OriginMinY = -800000.0,
                    OriginMinZ = -15000.0,
                    OriginMaxX = 800000.0,
                    OriginMaxY = 800000.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -800000.0,
                    PlayableMinY = -800000.0,
                    PlayableMinZ = -15000.0,
                    PlayableMaxX = 800000.0,
                    PlayableMaxY = 800000.0,
                    PlayableMaxZ = 54695.0
                }
            },
            new ArkMapInfo() { MapName = "Svartalfheim", MinimapFilename = "Svartalfheim_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -203250.0,
                    OriginMinY = -203250.0,
                    OriginMinZ = -15000.0,
                    OriginMaxX = 203250.0,
                    OriginMaxY = 203250.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -203250.0,
                    PlayableMinY = -203250.0,
                    PlayableMinZ = -15000.0,
                    PlayableMaxX = 203250.0,
                    PlayableMaxY = 203250.0,
                    PlayableMaxZ = 54695.0
                }
            },
            new ArkMapInfo() { MapName = "Club ARK", MinimapFilename = "ClubArk_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -12812.0,
                    OriginMinY = -15121.0,
                    OriginMinZ = -12500.0,
                    OriginMaxX = 12078.0,
                    OriginMaxY = 9770.0,
                    OriginMaxZ = 12500.0,
                    PlayableMinX = -10581.0,
                    PlayableMinY = -15121.0,
                    PlayableMinZ = -12500.0,
                    PlayableMaxX = 9847.0,
                    PlayableMaxY = 9770.0,
                    PlayableMaxZ = 12500.0
                }
            },
            new ArkMapInfo() { MapName = "LostColony", MinimapFilename = "LostColony_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -408000.0,
                    OriginMinY = -408000.0,
                    OriginMinZ = -15000.0,
                    OriginMaxX = 408000.0,
                    OriginMaxY = 408000.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -408000.0,
                    PlayableMinY = -408000.0,
                    PlayableMinZ = -15000.0,
                    PlayableMaxX = 408000.0,
                    PlayableMaxY = 408000.0,
                    PlayableMaxZ = 54695.0
                }
            },
            new ArkMapInfo() { MapName = "Unknown", MinimapFilename = "TheIsland_Minimap_Margin.jpg", Bounds = new MapBounds()
                {
                    OriginMinX = -342900.0,
                    OriginMinY = -342900.0,
                    OriginMinZ = -15000.0,
                    OriginMaxX = 342900.0,
                    OriginMaxY = 342900.0,
                    OriginMaxZ = 54695.0,
                    PlayableMinX = -342900.0,
                    PlayableMinY = -342900.0,
                    PlayableMinZ = -15000.0,
                    PlayableMaxX = 342900.0,
                    PlayableMaxY = 342900.0,
                    PlayableMaxZ = 54695.0
                }
            },
        };

        public static JsonSerializerOptions IndentedJson = new JsonSerializerOptions { WriteIndented = true };

        private static Regex _unicodeEscapedChars = new Regex(@"\\u(?<Value>[a-zA-Z0-9]{4})", RegexOptions.Compiled);

        public static string DecodeUnicodeEscapedStr(string value)
        {
            string ret = string.Empty;
            try
            {
                ret = _unicodeEscapedChars.Replace(value, m => ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString());
            }
            catch
            {
                ret = value;
            }
            return ret;
        }

        public static void ReplaceUnicodeEscapedStrInObject(object? obj, Type type)
        {
            if (obj != null)
            {
                var properties = Utils.GetStringPropertiesForType(type);
                foreach (PropertyInfo property in properties)
                {
                    string? current = property.GetValue(obj) as String;
                    if (!string.IsNullOrEmpty(current) && current.Contains("\\u", StringComparison.InvariantCulture))
                        property.SetValue(obj, Utils.DecodeUnicodeEscapedStr(current));
                }
            }
        }

        public static string GetVersionStr()
        {
            if (_version == null)
                _version = typeof(MainWindow).Assembly.GetName().Version;
            if (_version != null)
                return $"{_version.Major.ToString(CultureInfo.InvariantCulture)}.{_version.Build.ToString(CultureInfo.InvariantCulture)}";
            return "1.0";
        }

        public static void EnsureDataFolderExist()
        {
            if (!Directory.Exists(GetDataDir()))
            {
                try { Directory.CreateDirectory(GetDataDir()); }
                catch (Exception ex) { Logger.Instance.Log($"Exception caught in EnsureDataFolderExist. Exception=[{ex}]", Logger.LogLevel.ERROR); }
            }
        }

        public static void EnsureLangFolderExist()
        {
            if (!Directory.Exists(GetLangDir()))
            {
                try { Directory.CreateDirectory(GetLangDir()); }
                catch (Exception ex) { Logger.Instance.Log($"Exception caught in EnsureLangFolderExist. Exception=[{ex}]", Logger.LogLevel.ERROR); }
            }
        }

        public static List<string>? GetPathsFromEnvironmentVariables()
        {
            var envVars = Environment.GetEnvironmentVariables();
            if (envVars != null && envVars.Count > 0)
                foreach (DictionaryEntry envVar in envVars)
                    if (string.Compare("Path", envVar.Key.ToString(), StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        if (envVar.Value == null)
                            return null;
                        string? paths = envVar.Value.ToString();
                        if (string.IsNullOrWhiteSpace(paths))
                            return null;
                        return paths.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
            return null;
        }

        public static string? GetShortNameFromItemArchetype(string? itemArchetype)
        {
            if (string.IsNullOrEmpty(itemArchetype))
                return itemArchetype;
            string? shortName = itemArchetype;
            if (!string.IsNullOrEmpty(itemArchetype) && itemArchetype.Contains('.'))
            {
                int stt = itemArchetype.LastIndexOf('.');
                if (stt >= 0)
                {
                    stt += 1;
                    if (itemArchetype.Length > stt)
                    {
                        shortName = itemArchetype.Substring(stt);
                        if (shortName.EndsWith("_C") && shortName.Length > 2)
                            shortName = shortName.Substring(0, shortName.Length - 2);
                    }
                }
            }
            return shortName;
        }

        public static string JoinObjectsToString<T>(IList<T?>? objs)
        {
            string result = string.Empty;
            if (objs != null && objs.Count > 0)
                foreach (var obj in objs)
                    if (obj != null)
                    {
                        if (result.Length > 0)
                            result += ", ";
                        result += obj.ToString();
                    }
            return result;
        }

        public static IEnumerable<PropertyInfo> GetStringPropertiesForType(Type t) => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite).Where(p => !p.GetIndexParameters().Any()).Where(p => p.PropertyType == typeof(string));

        public static string? GetPropertyNameFromCleanName(Dictionary<string, string> cleanNames, string? cleanName)
        {
            if (cleanNames == null || cleanNames.Count <= 0 || string.IsNullOrEmpty(cleanName))
                return cleanName;

            if (cleanNames.ContainsValue(cleanName))
                foreach (var elem in cleanNames)
                    if (string.Compare(elem.Value, cleanName, StringComparison.InvariantCulture) == 0)
                        return elem.Key;
            return cleanName;
        }

        public static string? GetPropertyValueForObject(PropertyInfo? prop, object? obj)
        {
            if (prop == null || obj == null)
                return null;

            string? propValue = null;
            try { propValue = prop.GetValue(obj)?.ToString(); }
            catch { return null; }
            // Default null booleans to "False".
            if (propValue == null && string.Compare(prop.PropertyType.ToString(), "System.Nullable`1[System.Boolean]", StringComparison.InvariantCulture) == 0)
                propValue = "False";
            // Check if current type is a List.
            if (propValue != null && propValue.StartsWith("System.Collections.Generic.List`1["))
            {
                try
                {
                    IList<object?>? list = prop.GetValue(obj) as IList<object?>;
                    if (list != null)
                        return JoinObjectsToString(list);
                }
                catch { return propValue; }
            }

            return propValue;
        }

        public static PropertyInfo? GetProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type objType, string? propertyName)
        {
            if (objType == null || string.IsNullOrEmpty(propertyName))
                return null;
            var properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties != null && properties.Count() > 0)
                foreach (var property in properties)
                    if (string.Compare(propertyName, property.Name, StringComparison.InvariantCulture) == 0)
                        return property;
            return null;
        }

        public static List<string> GetPropertyValues(IEnumerable<object?>? objs, PropertyInfo? property, int maxValues = -1)
        {
            List<string> result = new List<string>();
            if (objs == null || objs.Count() <= 0 || property == null)
                return result;
            int nbOptions = 0;
            foreach (var obj in objs)
                if (obj != null)
                {
                    string? propValue = GetPropertyValueForObject(property, obj);
                    if (propValue != null && !result.Contains(propValue))
                    {
                        result.Add(propValue);
                        ++nbOptions;
                        if (maxValues > 0 && nbOptions >= maxValues)
                            return result;
                    }
                }
            return result;
        }

        public static int GetPropertyValuesCount(IEnumerable<object?>? objs, PropertyInfo? property, int maxValues = -1)
        {
            if (objs == null || objs.Count() <= 0 || property == null)
                return 0;
            List<string> result = new List<string>();
            foreach (var obj in objs)
                if (obj != null)
                {
                    string? propValue = GetPropertyValueForObject(property, obj);
                    if (propValue != null && !result.Contains(propValue))
                        result.Add(propValue);
                    if (maxValues > 0 && result.Count > maxValues)
                        break;
                }
            int retval = result.Count;
            result.Clear();
            return retval;
        }

        private const int MAX_OBJECTS_TO_CHECK = 500;

        public static bool PropertyHasMoreValuesThan(IEnumerable<object?>? objs, PropertyInfo? property, int maxValues)
        {
            if (objs == null || property == null || maxValues <= 0)
                return false;
            int nbObjs = objs.Count();
            if (nbObjs <= 0)
                return false;
            List<string> result = new List<string>();

            // Check first MAX_OBJECTS_TO_CHECK objects.
            for (int i = 0; i < nbObjs && i < MAX_OBJECTS_TO_CHECK; i++)
            {
                var obj = objs.ElementAt(i);
                if (obj != null)
                {
                    string? propValue = GetPropertyValueForObject(property, obj);
                    if (propValue != null && !result.Contains(propValue))
                        result.Add(propValue);
                    if (maxValues > 0 && result.Count > maxValues)
                    {
                        result.Clear();
                        return true;
                    }
                }
            }
            // Check last MAX_OBJECTS_TO_CHECK objects.
            if (nbObjs > (MAX_OBJECTS_TO_CHECK * 2))
                for (int j = (nbObjs - MAX_OBJECTS_TO_CHECK - 1); j < nbObjs; j++)
                {
                    var obj = objs.ElementAt(j);
                    if (obj != null)
                    {
                        string? propValue = GetPropertyValueForObject(property, obj);
                        if (propValue != null && !result.Contains(propValue))
                            result.Add(propValue);
                        if (maxValues > 0 && result.Count > maxValues)
                        {
                            result.Clear();
                            return true;
                        }
                    }
                }
            result.Clear();
            return false;
        }

        public static double Lerp(double a, double b, double t)
        {
            return (1 - t) * a + t * b;
        }

        public static double InvertLerp(double a, double b, double v)
        {
            return (v - a) / (b - a);
        }

        /// <summary>
        /// Returns Key Lat (y) and Value Long (x) as minimap coordinates, based on provided UE coords.
        /// </summary>
        /// <param name="mapBounds">The map bounds</param>
        /// <param name="x">X coord</param>
        /// <param name="y">Y coord</param>
        /// <param name="z">Z coord</param>
        /// <returns>Returns Key Lat (y) and Value Long (x) as minimap coordinates, based on provided UE coords.</returns>
        public static KeyValuePair<double, double> transformToMapCoords(MapBounds mapBounds, double x, double y, double z, double minX = 0.0d, double maxX = 100.0d, double minY = 0.0d, double maxY = 100.0d)
        {
            double yMaxDiff = y - mapBounds.OriginMaxY;
            double xMaxDiff = x - mapBounds.OriginMaxX;

            double originYDiff = mapBounds.OriginMinY - mapBounds.OriginMaxY;
            double originXDiff = mapBounds.OriginMinX - mapBounds.OriginMaxX;

            double latRatio = yMaxDiff / originYDiff;
            double lonRatio = xMaxDiff / originXDiff;

            double lat = Lerp(maxY, minY, latRatio);
            double lon = Lerp(maxX, minX, lonRatio);

            return new KeyValuePair<double, double>(Double.Round(lat, 1), Double.Round(lon, 1));
        }

        /// <summary>
        /// Returns Key X (long) and Value Y (lat) as UE coordinates, based on provided minimap coords.
        /// </summary>
        /// <param name="mapBounds">The map bounds</param>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <returns>Returns Key X (long) and Value Y (lat) as UE coordinates, based on provided minimap coords.</returns>
        public static KeyValuePair<double, double> mapCoordsToTransform(MapBounds mapBounds, double lat, double lon)
        {
            double originYDiff = mapBounds.OriginMinY - mapBounds.OriginMaxY;
            double originXDiff = mapBounds.OriginMinX - mapBounds.OriginMaxX;

            double latRatio = InvertLerp(100.0d, 0.0d, lat);
            double lonRatio = InvertLerp(100.0d, 0.0d, lon);

            double yMaxDiff = latRatio * originYDiff;
            double xMaxDiff = lonRatio * originXDiff;

            double y = yMaxDiff + mapBounds.OriginMaxY;
            double x = xMaxDiff + mapBounds.OriginMaxX;

            return new KeyValuePair<double, double>(x, y);
        }

        /// <summary>
        /// Returns map GPS coordinates from given transform position, with Key being latitude (Y) and Value being longitude (X).
        /// </summary>
        /// <param name="mapName">The map name</param>
        /// <param name="x">Transform x</param>
        /// <param name="y">Transform y</param>
        /// <param name="z">Transform z</param>
        /// <returns>Returns map GPS coordinates from given transform position, with Key being latitude (Y) and Value being longitude (X).</returns>
        public static KeyValuePair<double, double> GetMapCoords(string? mapName, double? x, double? y, double? z)
        {
            if (x == null || !x.HasValue || y == null || !y.HasValue || z == null || !z.HasValue)
                return new KeyValuePair<double, double>(50.0d, 50.0d);

            ArkMapInfo? mapInfo = Utils.GetMapInfoFromName(mapName ?? "Unknown");
            if (mapInfo == null)
                mapInfo = Utils.GetMapInfoFromName("Unknown");
            if (mapInfo != null)
                return Utils.transformToMapCoords(mapInfo.Bounds, x.Value, y.Value, z.Value);
            return new KeyValuePair<double, double>(50.0d, 50.0d);
        }

        /// <summary>
        /// Returns ASI minimap coords, with Key being Y axis and Value being X axis.
        /// </summary>
        /// <param name="mapName">The name of the map.</param>
        /// <param name="x">X coord</param>
        /// <param name="y">Y coord</param>
        /// <param name="z">Z coord</param>
        /// <returns>Returns ASI minimap coords, with Key being Y axis and Value being X axis.</returns>
        public static KeyValuePair<double, double> GetASIMinimapCoords(string? mapName, double? x, double? y, double? z)
        {
            if (x == null || !x.HasValue || y == null || !y.HasValue || z == null || !z.HasValue)
                return new KeyValuePair<double, double>(50.0d, 50.0d);

            ArkMapInfo? mapInfo = Utils.GetMapInfoFromName(mapName ?? "Unknown");
            if (mapInfo == null)
                mapInfo = Utils.GetMapInfoFromName("Unknown");
            if (mapInfo != null)
            {
                var pos = Utils.transformToMapCoords(mapInfo.Bounds, x.Value, y.Value, z.Value, Minimap.X_MIN, Minimap.X_MAX, Minimap.Y_MIN, Minimap.Y_MAX);
                return new KeyValuePair<double, double>(Minimap.Y_MAX - pos.Key, pos.Value);
            }
            return new KeyValuePair<double, double>(50.0d, 50.0d);
        }

        /// <summary>
        /// Returns map GPS coordinates from given ASI minimap coords, with Key being latitude (Y) and Value being longitude (X).
        /// </summary>
        /// <param name="mapName">The name of the map.</param>
        /// <param name="x">ASI minimap X coord</param>
        /// <param name="y">ASI minimap Y coord</param>
        /// <returns>Returns map GPS coordinates from given ASI minimap coords, with Key being latitude (Y) and Value being longitude (X).</returns>
        public static KeyValuePair<double, double> GetMapCoordsFromASIMinimapCoords(string? mapName, double? y, double? x)
        {
            if (x == null || !x.HasValue || y == null || !y.HasValue)
                return new KeyValuePair<double, double>(50.0d, 50.0d);

            double actualY = Math.Clamp((((((y.Value - Minimap.MARGIN) / Minimap.Y_MAX) * 100.0d) - 100.0f) * -1.0d), 0.0d, 100.0d);
            double actualX = Math.Clamp((((x.Value - Minimap.MARGIN) / Minimap.X_MAX) * 100.0d), 0.0d, 100.0d);
            return new KeyValuePair<double, double>(actualY, actualX);
        }

        public static void AddToClipboard(string str, bool showToast = true)
        {
            DataPackage dataPackage = new();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(str);
            Clipboard.SetContent(dataPackage);
            if (showToast)
                MainWindow.ShowToast("Copied to clipboard", BackgroundColor.SUCCESS);
        }

        public static Item? FindItemByUUID(string? uuid)
        {
            if (!string.IsNullOrEmpty(uuid) && SettingsPage._itemsData != null && SettingsPage._itemsData.Count > 0)
                return SettingsPage._itemsData.FirstOrDefault(i => uuid == i?.UUID, null);
            return null;
        }

        public static KeyValuePair<ArkObjectType, object?> FindObjectByInventoryUUID(string? inventoryUUID)
        {
            if (!string.IsNullOrEmpty(inventoryUUID))
            {
                if (SettingsPage._playerPawnsData != null && SettingsPage._playerPawnsData.Count > 0)
                {
                    PlayerPawn? container = SettingsPage._playerPawnsData.FirstOrDefault(p => inventoryUUID == p?.InventoryUUID, null);
                    if (container != null)
                        return new KeyValuePair<ArkObjectType, object?>(ArkObjectType.PLAYER_PAWN, container);
                }
                if (SettingsPage._structuresData != null && SettingsPage._structuresData.Count > 0)
                {
                    Structure? container = SettingsPage._structuresData.FirstOrDefault(s => inventoryUUID == s?.InventoryUUID, null);
                    if (container != null)
                        return new KeyValuePair<ArkObjectType, object?>(ArkObjectType.STRUCTURE, container);
                }
                if (SettingsPage._dinosData != null && SettingsPage._dinosData.Count > 0)
                {
                    Dino? container = SettingsPage._dinosData.FirstOrDefault(d => inventoryUUID == d?.InventoryUUID, null);
                    if (container != null)
                        return new KeyValuePair<ArkObjectType, object?>(ArkObjectType.DINO, container);
                }
            }
            return new KeyValuePair<ArkObjectType, object?>(ArkObjectType.UNKNOWN, null);
        }

        public static T? FindParent<T>(DependencyObject childElement) where T : Control
        {
            DependencyObject currentElement = childElement;
            while (currentElement != null)
            {
                if (currentElement is T matchingElement)
                    return matchingElement;
                currentElement = VisualTreeHelper.GetParent(currentElement);
            }
            return null;
        }

        public static ArkMapInfo? GetMapInfoFromName(string mapName)
        {
            foreach (ArkMapInfo mapInfo in Utils._allMaps)
                if (mapInfo != null && string.Compare(mapName, mapInfo.MapName, StringComparison.InvariantCulture) == 0)
                    return mapInfo;
            return null;
        }

        public static Tuple<double, double, double> GetCryopodCoordsByUUID(string? cryopodUUID)
        {
            if (!string.IsNullOrEmpty(cryopodUUID))
            {
                Item? item = Utils.FindItemByUUID(cryopodUUID);
                if (item != null)
                {
                    var found = Utils.FindObjectByInventoryUUID(item.OwnerInventoryUUID);
                    if (found.Key != ArkObjectType.UNKNOWN && found.Value != null)
                    {
                        if (found.Key == ArkObjectType.PLAYER_PAWN)
                        {
                            PlayerPawn? pp = found.Value as PlayerPawn;
                            if (pp != null)
                                return new Tuple<double, double, double>((pp.ActorTransformX != null && pp.ActorTransformX.HasValue ? pp.ActorTransformX.Value : 0.0d), (pp.ActorTransformY != null && pp.ActorTransformY.HasValue ? pp.ActorTransformY.Value : 0.0d), (pp.ActorTransformZ != null && pp.ActorTransformZ.HasValue ? pp.ActorTransformZ.Value : 0.0d));
                        }
                        if (found.Key == ArkObjectType.DINO)
                        {
                            Dino? d = found.Value as Dino;
                            if (d != null)
                                return new Tuple<double, double, double>((d.ActorTransformX != null && d.ActorTransformX.HasValue ? d.ActorTransformX.Value : 0.0d), (d.ActorTransformY != null && d.ActorTransformY.HasValue ? d.ActorTransformY.Value : 0.0d), (d.ActorTransformZ != null && d.ActorTransformZ.HasValue ? d.ActorTransformZ.Value : 0.0d));
                        }
                        if (found.Key == ArkObjectType.STRUCTURE)
                        {
                            Structure? s = found.Value as Structure;
                            if (s != null)
                                return new Tuple<double, double, double>((s.ActorTransformX != null && s.ActorTransformX.HasValue ? s.ActorTransformX.Value : 0.0d), (s.ActorTransformY != null && s.ActorTransformY.HasValue ? s.ActorTransformY.Value : 0.0d), (s.ActorTransformZ != null && s.ActorTransformZ.HasValue ? s.ActorTransformZ.Value : 0.0d));
                        }
                    }
                }
            }
            return new Tuple<double, double, double>(0.0d, 0.0d, 0.0d);
        }

        public static string? GetSaveFileDateTimeStr() => (SettingsPage._saveFileData?.SaveDateTime != null && SettingsPage._saveFileData.SaveDateTime.HasValue ? SettingsPage._saveFileData.SaveDateTime.Value : DateTime.UnixEpoch).ToString("yyyy-MM-dd HH\\hmm\\mss\\s", CultureInfo.InvariantCulture);

        public static string GetInGameTimeStr()
        {
            if (SettingsPage._saveFileData?.CurrentTime != null && SettingsPage._saveFileData.CurrentTime.HasValue)
            {
                int currTime = Math.Max(0, Convert.ToInt32(Math.Floor(SettingsPage._saveFileData.CurrentTime.Value), CultureInfo.InvariantCulture));
                int hours = Math.Max(0, Convert.ToInt32(Math.Floor((double)currTime / 3600.0d), CultureInfo.InvariantCulture));
                int remaining = (currTime % 3600);
                int minutes = Math.Max(0, Convert.ToInt32(Math.Floor((double)remaining / 60.0d), CultureInfo.InvariantCulture));
                remaining = (remaining % 60);
                int seconds = Math.Max(0, remaining);
                string hoursStr = hours.ToString(CultureInfo.InvariantCulture);
                if (hoursStr.Length < 2)
                    hoursStr = $"0{hoursStr}";
                string minutesStr = minutes.ToString(CultureInfo.InvariantCulture);
                if (minutesStr.Length < 2)
                    minutesStr = $"0{minutesStr}";
                string secondsStr = seconds.ToString(CultureInfo.InvariantCulture);
                if (secondsStr.Length < 2)
                    secondsStr = $"0{secondsStr}";
                return $"{hoursStr}:{minutesStr}:{secondsStr}";
            }
            return "00:00:00";
        }

        public static string GetInGameDateTimeStr() => $"Day {(SettingsPage._saveFileData?.CurrentDay != null && SettingsPage._saveFileData.CurrentDay.HasValue ? SettingsPage._saveFileData.CurrentDay.Value.ToString(CultureInfo.InvariantCulture) : "0")}, {GetInGameTimeStr()}";

        public static DateTime? GetDateTimeFromGameTime(double? gameTime)
        {
            DateTime? ret = null;
            if (gameTime != null && gameTime.HasValue && SettingsPage._saveFileData != null &&
                SettingsPage._saveFileData.SaveDateTime != null && SettingsPage._saveFileData.SaveDateTime.HasValue &&
                SettingsPage._saveFileData.GameTime != null && SettingsPage._saveFileData.GameTime.HasValue)
                ret = SettingsPage._saveFileData.SaveDateTime.Value.AddSeconds(gameTime.Value - SettingsPage._saveFileData.GameTime.Value);
            return ret;
        }

        public static void ClearAllPagesFiltersAndGroups()
        {
            PlayerPawnsPage.ClearPageFiltersAndGroups();
            DinosPage.ClearPageFiltersAndGroups();
            StructuresPage.ClearPageFiltersAndGroups();
            ItemsPage.ClearPageFiltersAndGroups();
            PlayersPage.ClearPageFiltersAndGroups();
            TribesPage.ClearPageFiltersAndGroups();
        }

        public static bool MoveDirectory(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return false;

            bool hasErrors = false;
            var sourcePath = source.TrimEnd('/', '\\', ' ');
            var targetPath = target.TrimEnd('/', '\\', ' ');

            if (sourcePath.Length < 4 || targetPath.Length < 4 || !Directory.Exists(sourcePath))
                return false;

            IEnumerable<IGrouping<string?, string>>? files = null;
            try { files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).GroupBy(s => Path.GetDirectoryName(s)); }
            catch (Exception ex1)
            {
                files = null;
                hasErrors = true;
                Logger.Instance.Log($"Exception caught in MoveDirectory. Could not enumerate filtes in \"{sourcePath}\". Exception=[{ex1}]", Logger.LogLevel.ERROR);
            }
            if (files != null && files.Count() > 0)
                foreach (var folder in files)
                    if (folder != null && !string.IsNullOrEmpty(folder.Key))
                    {
                        string targetFolder = folder.Key.Replace(sourcePath, targetPath);
                        if (!string.IsNullOrEmpty(targetFolder))
                        {
                            if (!Directory.Exists(targetFolder))
                                try { Directory.CreateDirectory(targetFolder); }
                                catch (Exception ex2)
                                {
                                    hasErrors = true;
                                    Logger.Instance.Log($"Exception caught in MoveDirectory. Could not create directory at \"{targetFolder}\". Exception=[{ex2}]", Logger.LogLevel.ERROR);
                                }
                            if (Directory.Exists(targetFolder))
                                foreach (var file in folder)
                                    if (!string.IsNullOrEmpty(file))
                                    {
                                        string? targetFile = null;
                                        try { targetFile = Path.Combine(targetFolder, Path.GetFileName(file)); }
                                        catch (Exception ex3)
                                        {
                                            targetFile = null;
                                            hasErrors = true;
                                            Logger.Instance.Log($"Exception caught in MoveDirectory. Could not determine target file path for \"{file}\" with folder \"{targetFolder}\". Exception=[{ex3}]", Logger.LogLevel.ERROR);
                                        }
                                        if (targetFile != null)
                                        {
                                            bool attemptCopy = false;
                                            try { File.Move(file, targetFile, true); }
                                            catch (Exception ex4)
                                            {
                                                attemptCopy = true;
                                                Logger.Instance.Log($"Exception caught in MoveDirectory. Could not move file \"{file}\" to \"{targetFile}\". Attempting copy. Exception=[{ex4}]", Logger.LogLevel.ERROR);
                                            }
                                            if (attemptCopy)
                                                try { File.Copy(file, targetFile, true); }
                                                catch (Exception ex5)
                                                {
                                                    hasErrors = true;
                                                    Logger.Instance.Log($"Exception caught in MoveDirectory. Could not copy file \"{file}\" to \"{targetFile}\". Exception=[{ex5}]", Logger.LogLevel.ERROR);
                                                }
                                        }
                                    }
                        }
                    }
            if (!hasErrors)
                try { Directory.Delete(source, true); }
                catch (Exception ex6)
                {
                    hasErrors = true;
                    Logger.Instance.Log($"Exception caught in MoveDirectory. Could not delete origin directory at \"{source}\". Exception=[{ex6}]", Logger.LogLevel.ERROR);
                }
            return !hasErrors;
        }

        public static string BytesSizeToReadableString(double size)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (size >= 1024.0d && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024.0d;
            }
            return String.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", size, sizes[order]);
        }

        public static double GetDirectorySize(string path)
        {
            string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            double totalSize = 0.0d;
            foreach (string name in files)
                try
                {
                    if (File.Exists(name))
                    {
                        FileInfo info = new FileInfo(name);
                        totalSize += Convert.ToDouble(info.Length);
                    }
                }
                catch { }
            return totalSize;
        }

        public static IEnumerable<string>? GetPreviousASIFolders()
        {
            string? actualDirName = null;
            string[]? directories = null;

            try
            {
                string actualDir = Utils.GetBaseDir();
                if (string.IsNullOrEmpty(actualDir) || !Directory.Exists(actualDir))
                    return null;
                actualDirName = Path.GetFileName(Path.GetDirectoryName(actualDir));
                if (string.IsNullOrEmpty(actualDirName))
                    return null;

                string parentDir = Utils.GetBaseDirParent();
                if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir))
                    return null;
                directories = Directory.GetDirectories(parentDir);
                if (directories == null || directories.Length <= 0)
                    return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in CheckForPreviousData. Exception=[{ex}]", Logger.LogLevel.WARNING);
                return null;
            }

            if (string.IsNullOrEmpty(actualDirName) || directories == null || directories.Length <= 0)
                return null;

            DirectoryInfo? actualDI = null;
            try { actualDI = new DirectoryInfo(actualDirName); }
            catch { actualDI = null; }

            Dictionary<string, DateTime> foundDirs = new Dictionary<string, DateTime>();
            foreach (var dir in directories)
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    string? dirName = null;
                    try
                    {
                        if (dir.EndsWith("/", StringComparison.InvariantCulture) || dir.EndsWith("\\", StringComparison.InvariantCulture))
                            dirName = Path.GetFileName(Path.GetDirectoryName(dir));
                        else
                            dirName = Path.GetFileName(dir);
                    }
                    catch { dirName = null; }
                    if (string.IsNullOrEmpty(dirName))
                        continue;
                    if (string.Compare(actualDirName, dirName, StringComparison.InvariantCulture) == 0)
                        continue;

                    DirectoryInfo? di = null;
                    if (actualDI != null)
                    {
                        try { di = new DirectoryInfo(dirName); }
                        catch { di = null; }
                    }
                    if (actualDI != null && di != null && string.Compare(actualDI.FullName, di.FullName, false, CultureInfo.InvariantCulture) == 0)
                        continue;

                    DateTime? dt = null;
                    try { dt = Directory.GetLastWriteTimeUtc(dir); }
                    catch { dt = null; }
                    if (dt != null && dt.HasValue)
                        foundDirs[dir] = dt.Value;
                }

            if (foundDirs.Count <= 0)
                return null;

            return (from dir in foundDirs orderby dir.Value descending select dir.Key);
        }

        public static bool IsDarkTheme() => (SettingsPage._darkTheme != null && SettingsPage._darkTheme.HasValue && !SettingsPage._darkTheme.Value ? false : true);

        public static readonly Brush _greenNotificationBackground = new SolidColorBrush(Colors.DarkGreen);
        public static readonly Brush _orangeNotificationBackground = new SolidColorBrush(Colors.DarkOrange);
        public static readonly Brush _redNotificationBackground = new SolidColorBrush(Colors.DarkRed);
        public static readonly Brush _grayNotificationBackground = new SolidColorBrush(Colors.DarkGray);
        public static readonly Brush _darkBlueForeground = new SolidColorBrush(Colors.Blue);
        public static readonly Brush _lightBlueForeground = new SolidColorBrush(Colors.CornflowerBlue);
        public static readonly Brush _darkGreenForeground = new SolidColorBrush(Colors.DarkGreen);
        public static readonly Brush _lightGreenForeground = new SolidColorBrush(Colors.Green);
        public static Brush BlueForeground() => Utils.IsDarkTheme() ? _lightBlueForeground : _darkBlueForeground;
        public static Brush GreenForeground() => Utils.IsDarkTheme() ? _lightGreenForeground : _darkGreenForeground;

        public static string GetSearchOperatorAsString(SearchOperator op)
        {
            if (op == SearchOperator.NOT_MATCHING)
                return ASILang.Get("FilterType_NotMatching");
            else if (op == SearchOperator.EQUALS)
                return ASILang.Get("FilterType_Equals");
            else if (op == SearchOperator.NOT_EQUALS)
                return ASILang.Get("FilterType_NotEquals");
            else if (op == SearchOperator.STARTING_WITH)
                return ASILang.Get("FilterType_StartingWith");
            else if (op == SearchOperator.ENDING_WITH)
                return ASILang.Get("FilterType_EndingWith");
            else if (op == SearchOperator.CONTAINING)
                return ASILang.Get("FilterType_Containing");
            else if (op == SearchOperator.NOT_CONTAINING)
                return ASILang.Get("FilterType_NotContaining");
            else if (op == SearchOperator.LOWER_THAN)
                return ASILang.Get("FilterType_LowerThan");
            else if (op == SearchOperator.GREATER_THAN)
                return ASILang.Get("FilterType_GreaterThan");
            else
                return ASILang.Get("FilterType_Matching");
        }

        public static SearchOperator GetSearchOperatorFromString(string? str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return SearchOperator.MATCHING;

            if (string.Compare(str, ASILang.Get("FilterType_NotMatching"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.NOT_MATCHING;
            else if (string.Compare(str, ASILang.Get("FilterType_Equals"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.EQUALS;
            else if (string.Compare(str, ASILang.Get("FilterType_NotEquals"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.NOT_EQUALS;
            else if (string.Compare(str, ASILang.Get("FilterType_StartingWith"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.STARTING_WITH;
            else if (string.Compare(str, ASILang.Get("FilterType_EndingWith"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.ENDING_WITH;
            else if (string.Compare(str, ASILang.Get("FilterType_Containing"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.CONTAINING;
            else if (string.Compare(str, ASILang.Get("FilterType_NotContaining"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.NOT_CONTAINING;
            else if (string.Compare(str, ASILang.Get("FilterType_LowerThan"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.LOWER_THAN;
            else if (string.Compare(str, ASILang.Get("FilterType_GreaterThan"), StringComparison.InvariantCulture) == 0)
                return SearchOperator.GREATER_THAN;
            else
                return SearchOperator.MATCHING;
        }

        private static SearchOperator GetOperatorFromSelectionChanged(SelectionChangedEventArgs e)
        {
            if (e != null && e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string? selected = e.AddedItems[0] as string;
                if (selected != null)
                    return Utils.GetSearchOperatorFromString(selected);
            }
            return SearchOperator.MATCHING;
        }

        private static LogicalOperator GetLogicalOperatorFromSelectionChanged(SelectionChangedEventArgs e)
        {
            if (e != null && e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string? selected = e.AddedItems[0] as string;
                if (selected != null)
                {
                    if (string.Compare(selected, ASILang.Get("OperatorOR"), StringComparison.InvariantCulture) == 0)
                        return LogicalOperator.OR;
                    else
                        return LogicalOperator.AND;
                }
            }
            return LogicalOperator.AND;
        }

        public static void FormatQuery(ref StackPanel mainSp, SearchQuery? query, bool isEditable)
        {
            mainSp.Children.Clear();

            if (query == null || query.Parts == null || query.Parts.Count <= 0)
                return;

            int currentGroup = 0;

            TextBlock? tb0 = new TextBlock()
            {
                FontSize = 14.0d,
                TextWrapping = TextWrapping.Wrap,
                Text = ASILang.Get("FilterBy"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
            };
            mainSp.Children.Add(tb0);
            for (int i = 0; i < query.Parts.Count; i++)
                if (query.Parts[i] != null)
                {
                    StackPanel sp = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(5.0d, 5.0d, 0.0d, 0.0d) };
                    StackPanel spPad = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
                    int j = 0;
                    while (j < currentGroup)
                    {
                        TextBlock padBlock = new TextBlock()
                        {
                            FontSize = 14.0d,
                            TextWrapping = TextWrapping.Wrap,
                            Text = "",
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            TextAlignment = TextAlignment.Right,
                            Width = 40.0d
                        };
                        spPad.Children.Add(padBlock);
                        ++j;
                    }
                    while (j < query.Parts[i].Group)
                    {
                        TextBlock padBlockGroup = new TextBlock()
                        {
                            FontSize = 14.0d,
                            TextWrapping = TextWrapping.Wrap,
                            Text = "(",
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            TextAlignment = TextAlignment.Right,
                            Width = 40.0d,
                            Margin = new Thickness(0.0d, 6.0d, 0.0d, 0.0d)
                        };
                        spPad.Children.Add(padBlockGroup);
                        ++j;
                        ++currentGroup;
                    }
                    TextBlock tb1 = new TextBlock()
                    {
                        FontSize = 14.0d,
                        TextWrapping = TextWrapping.Wrap,
                        Text = query.Parts[i].PropertyCleanName,
                        Foreground = Utils.GreenForeground(),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
                    };

                    ComboBox? cbb2 = null;
                    TextBlock? tb2 = null;
                    if (isEditable)
                    {
                        cbb2 = new ComboBox()
                        {
                            FontSize = 14.0d,
                            Foreground = Utils.BlueForeground(),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d),
                            Tag = i
                        };
                        cbb2.Items.Add(ASILang.Get("FilterType_Matching"));
                        cbb2.Items.Add(ASILang.Get("FilterType_NotMatching"));
                        cbb2.Items.Add(ASILang.Get("FilterType_Equals"));
                        cbb2.Items.Add(ASILang.Get("FilterType_NotEquals"));
                        cbb2.Items.Add(ASILang.Get("FilterType_StartingWith"));
                        cbb2.Items.Add(ASILang.Get("FilterType_EndingWith"));
                        cbb2.Items.Add(ASILang.Get("FilterType_Containing"));
                        cbb2.Items.Add(ASILang.Get("FilterType_NotContaining"));
                        cbb2.Items.Add(ASILang.Get("FilterType_LowerThan"));
                        cbb2.Items.Add(ASILang.Get("FilterType_GreaterThan"));
                        if (query.Parts[i].Operator == SearchOperator.NOT_MATCHING)
                            cbb2.SelectedIndex = 1;
                        else if (query.Parts[i].Operator == SearchOperator.EQUALS)
                            cbb2.SelectedIndex = 2;
                        else if (query.Parts[i].Operator == SearchOperator.NOT_EQUALS)
                            cbb2.SelectedIndex = 3;
                        else if (query.Parts[i].Operator == SearchOperator.STARTING_WITH)
                            cbb2.SelectedIndex = 4;
                        else if (query.Parts[i].Operator == SearchOperator.ENDING_WITH)
                            cbb2.SelectedIndex = 5;
                        else if (query.Parts[i].Operator == SearchOperator.CONTAINING)
                            cbb2.SelectedIndex = 6;
                        else if (query.Parts[i].Operator == SearchOperator.NOT_CONTAINING)
                            cbb2.SelectedIndex = 7;
                        else if (query.Parts[i].Operator == SearchOperator.LOWER_THAN)
                            cbb2.SelectedIndex = 8;
                        else if (query.Parts[i].Operator == SearchOperator.GREATER_THAN)
                            cbb2.SelectedIndex = 9;
                        else // if (query.Parts[i].Operator == SearchOperator.MATCHING)
                            cbb2.SelectedIndex = 0;
                        cbb2.SelectionChanged += (o, e) =>
                        {
                            ComboBox? cbb = o as ComboBox;
                            if (cbb != null)
                            {
                                int partId = (int)cbb.Tag;
                                SearchOperator newSearchOperator = GetOperatorFromSelectionChanged(e);
                                if (EditSearchQuery._editSearchQuery != null)
                                    EditSearchQuery._editSearchQuery.ModifyQuerySearchOperator(partId, newSearchOperator);
                            }
                        };
                    }
                    else
                    {
                        tb2 = new TextBlock()
                        {
                            FontSize = 14.0d,
                            TextWrapping = TextWrapping.Wrap,
                            Text = Utils.GetSearchOperatorAsString(query.Parts[i].Operator),
                            Foreground = Utils.BlueForeground(),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
                        };
                    }

                    TextBox? tb3_a = null;
                    TextBlock? tb3_b = null;
                    if (isEditable)
                    {
                        tb3_a = new TextBox()
                        {
                            FontSize = 14.0d,
                            TextWrapping = TextWrapping.Wrap,
                            Text = query.Parts[i].Value ?? string.Empty,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d),
                            IsReadOnly = false,
                            AcceptsReturn = false,
                            Tag = i
                        };
                        tb3_a.TextChanged += (o, e) =>
                        {
                            TextBox? tb = o as TextBox;
                            if (tb != null)
                            {
                                int partId = (int)tb.Tag;
                                string newValue = tb.Text;
                                if (EditSearchQuery._editSearchQuery != null)
                                    EditSearchQuery._editSearchQuery.ModifyQueryValue(partId, newValue);
                            }
                        };
                    }
                    else
                    {
                        tb3_b = new TextBlock()
                        {
                            FontSize = 14.0d,
                            TextWrapping = TextWrapping.Wrap,
                            Text = query.Parts[i].Value ?? string.Empty,
                            Foreground = Utils.GreenForeground(),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
                        };
                    }

                    TextBlock? tb4 = null;
                    TextBlock? tb5 = null;
                    ComboBox? cbb5 = null;
                    if (i + 1 < query.Parts.Count)
                    {
                        if (currentGroup > query.Parts[i + 1].Group)
                        {
                            tb4 = new TextBlock()
                            {
                                FontSize = 14.0d,
                                TextWrapping = TextWrapping.Wrap,
                                Text = "",
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
                            };
                            string txt = string.Empty;
                            while (currentGroup > query.Parts[i + 1].Group)
                            {
                                txt += ") ";
                                --currentGroup;
                            }
                            tb4.Text = txt;
                        }
                        if (isEditable)
                        {
                            cbb5 = new ComboBox()
                            {
                                FontSize = 14.0d,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d),
                                Tag = i + 1
                            };
                            cbb5.Items.Add(ASILang.Get("OperatorAND"));
                            cbb5.Items.Add(ASILang.Get("OperatorOR"));
                            if (query.Parts[i + 1].LogicalOperator == LogicalOperator.OR)
                                cbb5.SelectedIndex = 1;
                            else
                                cbb5.SelectedIndex = 0;
                            cbb5.SelectionChanged += (o, e) =>
                            {
                                ComboBox? cbb = o as ComboBox;
                                if (cbb != null)
                                {
                                    int partId = (int)cbb.Tag;
                                    LogicalOperator newLogicalOperator = GetLogicalOperatorFromSelectionChanged(e);
                                    if (EditSearchQuery._editSearchQuery != null)
                                        EditSearchQuery._editSearchQuery.ModifyQueryLogicalOperator(partId, newLogicalOperator);
                                }
                            };
                        }
                        else
                        {
                            tb5 = new TextBlock()
                            {
                                FontSize = 14.0d,
                                TextWrapping = TextWrapping.Wrap,
                                Text = query.Parts[i + 1].LogicalOperator == LogicalOperator.OR ? ASILang.Get("OperatorOR") : ASILang.Get("OperatorAND"),
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
                            };
                        }
                    }
                    else
                    {
                        if (currentGroup > 0)
                        {
                            tb4 = new TextBlock()
                            {
                                FontSize = 14.0d,
                                TextWrapping = TextWrapping.Wrap,
                                Text = "",
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness(5.0d, 0.0d, 0.0d, 0.0d)
                            };
                            string txt = string.Empty;
                            while (currentGroup > 0)
                            {
                                txt += ") ";
                                --currentGroup;
                            }
                            tb4.Text = txt;
                        }
                    }
                    if (spPad != null && spPad.Children.Count > 0)
                        sp.Children.Add(spPad);
                    sp.Children.Add(tb1);
                    if (isEditable)
                        sp.Children.Add(cbb2);
                    else
                        sp.Children.Add(tb2);
                    if (isEditable)
                        sp.Children.Add(tb3_a);
                    else
                        sp.Children.Add(tb3_b);
                    if (tb4 != null)
                        sp.Children.Add(tb4);
                    if (isEditable)
                    {
                        if (cbb5 != null)
                            sp.Children.Add(cbb5);
                    }
                    else
                    {
                        if (tb5 != null)
                            sp.Children.Add(tb5);
                    }
                    mainSp.Children.Add(sp);
                }
        }

        public static void LogASIDataFolder()
        {
            string dataDirPath = GetDataDir();
            if (!Directory.Exists(dataDirPath))
            {
                Logger.Instance.Log($"{ASILang.Get("CannotFindASIDataFolder")} Path=[{dataDirPath ?? "NULL"}]", Logger.LogLevel.ERROR);
                return;
            }
            string[]? files = null;
            try { files = Directory.GetFiles(dataDirPath, "*.*", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                files = null;
                Logger.Instance.Log($"{ASILang.Get("ListingASIDataFolderError")} Exception=[{ex}]");
            }
            if (files == null || files.Length <= 0)
                return;
            Logger.Instance.Log($"{ASILang.Get("ListingASIDataFolder")}{Environment.NewLine}{string.Join(Environment.NewLine, files)}", Logger.LogLevel.INFO);
        }

        public static string? GetComboBoxSelection(object cbb, bool asComboBoxItem)
        {
            if (cbb == null)
                return null;
            ComboBox? cb = cbb as ComboBox;
            if (cb == null)
                return null;
            if (!asComboBoxItem)
            {
                if (cb.SelectedItem == null)
                    return null;
                return cb.SelectedItem.ToString();
            }
            ComboBoxItem? cbi = cb.SelectedItem as ComboBoxItem;
            if (cbi == null)
                return null;
            return cbi.Content.ToString();
        }

        public static void LockAllPages(bool doLock)
        {
            if (MainWindow._mainWindow != null && MainWindow._mainWindow._navView != null)
                MainWindow._mainWindow._navView.IsEnabled = !doLock;
            if (AboutPage._page != null)
                AboutPage._page.IsEnabled = !doLock;
            if (DinosPage._page != null)
                DinosPage._page.IsEnabled = !doLock;
            if (ItemsPage._page != null)
                ItemsPage._page.IsEnabled = !doLock;
            if (OtherPage._page != null)
                OtherPage._page.IsEnabled = !doLock;
            if (PlayerPawnsPage._page != null)
                PlayerPawnsPage._page.IsEnabled = !doLock;
            if (PlayersPage._page != null)
                PlayersPage._page.IsEnabled = !doLock;
            if (SettingsPage._page != null)
                SettingsPage._page.IsEnabled = !doLock;
            if (StructuresPage._page != null)
                StructuresPage._page.IsEnabled = !doLock;
            if (TribesPage._page != null)
                TribesPage._page.IsEnabled = !doLock;
        }
    }

    public enum ArkObjectType
    {
        UNKNOWN = 0,
        PLAYER_PAWN = 1,
        DINO = 2,
        STRUCTURE = 3
    }

    public enum FilterOperator
    {
        NONE = 0,
        AND = 1,
        OR = 2
    }

    public enum FilterType
    {
        NONE = 0,
        EXACT_MATCH = 1,
        STARTING_WITH = 2,
        ENDING_WITH = 3,
        CONTAINING = 4,
        GREATER_THAN = 5,
        LOWER_THAN = 6,
        NOT_CONTAINING = 7
    }

    public class Filter
    {
        public FilterOperator FilterOperator { get; set; } = FilterOperator.NONE;
        public FilterType FilterType { get; set; } = FilterType.NONE;
        public string? FilterValue { get; set; } = null;
        public List<string>? FilterValues { get; set; } = null;
    }

    public class JsonFilter
    {
        public string? PropertyName { get; set; } = null;
        public Filter? Filter { get; set; } = null;
    }

    public class JsonFiltersPreset
    {
        public string? Name { get; set; } = null;
        public List<JsonFilter>? Filters { get; set; } = null;
    }

    public class JsonColumnsPreset
    {
        public string? Name { get; set; } = null;
        public List<string>? Columns { get; set; } = null;
    }

    public class JsonGroupPreset
    {
        public string? Name { get; set; } = null;
        public List<KeyValuePair<FilterOperator, string>>? FiltersPresets { get; set; } = null;
    }

    class SwapVisitor : ExpressionVisitor
    {
        private readonly Expression from;
        private readonly Expression to;

        public SwapVisitor(Expression from, Expression to)
        {
            this.from = from;
            this.to = to;
        }

        [return: NotNullIfNotNull("node")]
        public override Expression? Visit(Expression? node)
        {
            return node == from ? to : base.Visit(node);
        }
    }

    public class GroupInfoCollection<K, T> : IGrouping<K, T>
    {
        private readonly IEnumerable<T> _items;

        public GroupInfoCollection(K key, IEnumerable<T> items)
        {
            Key = key;
            _items = items;
        }

        public K Key { get; }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }

    public class ColumnOrder
    {
        public string? HeaderName { get; set; } = null;
        public int DisplayIndex { get; set; } = -1;
    }
}
