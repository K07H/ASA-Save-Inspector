using ASA_Save_Inspector.Pages;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ASA_Save_Inspector
{
    public class ASILangFile
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string FlagFilepath { get; set; } = string.Empty;
        public Dictionary<string, string> Items { get; set; } = new Dictionary<string, string>();
    }

    public static class ASILang
    {
        public const string DEFAULT_LANGUAGE_CODE = "en_GB";
        private const string UNKNOWN_LANGUAGE_FLAG_PATH = "/Assets/FlagUnknownIcon96.png";
        public static readonly List<string> IMAGE_EXTENSIONS = new List<string> { ".JPG", ".JPEG", ".BMP", ".GIF", ".PNG" };

        private static bool _initialized = false;
        public static string _selectedLanguage = DEFAULT_LANGUAGE_CODE;
        public static Dictionary<string, ASILangFile> _languages = new Dictionary<string, ASILangFile>();

        static ASILang()
        {
            EnsureInitialized();
        }

        public static string GetLanguageCode(string? code) => (!string.IsNullOrEmpty(code) && ASILang._languages.ContainsKey(code) ? code : ASILang.DEFAULT_LANGUAGE_CODE);
        public static string GetFlagImageFullPath(string flagPath) => $"{Utils.GetBaseDir()}{flagPath.Replace("/", "\\", StringComparison.InvariantCulture)}".Replace("\\\\", "\\", StringComparison.InvariantCulture);
        
        public static BitmapImage? GetFlagImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;
            try
            {
                BitmapImage? flagImage = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                return flagImage;
            }
            catch { }
            return null;
        }

        private static bool Initialize()
        {
            string[]? jsonFiles = null;
            string langDir = Utils.GetLangDir();
            Utils.EnsureLangFolderExist();
            try { jsonFiles = Directory.GetFiles(langDir, "*.json"); }
            catch (Exception ex)
            {
                jsonFiles = null;
                Logger.Instance.Log($"Failed to get files in language directory at \"{langDir}\". Exception=[{ex}]", Logger.LogLevel.ERROR);
                return false;
            }
            
            if (jsonFiles == null || jsonFiles.Length <= 0)
            {
                Logger.Instance.Log($"Language directory is empty at \"{langDir}\".", Logger.LogLevel.ERROR);
                return false;
            }

            foreach (string jsonFile in jsonFiles)
                if (!string.IsNullOrEmpty(jsonFile) && File.Exists(jsonFile))
                {
                    string langJson = string.Empty;
                    try { langJson = File.ReadAllText(jsonFile, Encoding.UTF8); }
                    catch (Exception ex2)
                    {
                        langJson = string.Empty;
                        Logger.Instance.Log($"Failed to read language file at \"{jsonFile}\". Exception=[{ex2}]", Logger.LogLevel.ERROR);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(langJson))
                    {
                        Logger.Instance.Log($"JSON language file is empty at \"{jsonFile}\".", Logger.LogLevel.ERROR);
                        continue;
                    }

                    ASILangFile? lang = null;
                    try { lang = JsonSerializer.Deserialize<ASILangFile>(langJson); }
                    catch (Exception ex3)
                    {
                        lang = null;
                        Logger.Instance.Log($"Failed to deserialize language file at \"{jsonFile}\". Exception=[{ex3}]", Logger.LogLevel.ERROR);
                        continue;
                    }
                    if (lang == null)
                    {
                        Logger.Instance.Log($"Failed to parse language file at \"{jsonFile}\".", Logger.LogLevel.ERROR);
                        continue;
                    }

                    if (string.IsNullOrEmpty(lang.Name))
                    {
                        Logger.Instance.Log($"Language file \"{jsonFile}\" does not have a valid name. Skipping.", Logger.LogLevel.ERROR);
                        continue;
                    }
                    if (string.IsNullOrEmpty(lang.Code))
                    {
                        Logger.Instance.Log($"Language file \"{jsonFile}\" does not have a valid code. Skipping.", Logger.LogLevel.ERROR);
                        continue;
                    }
                    if (lang.Items == null || lang.Items.Count <= 0)
                    {
                        Logger.Instance.Log($"Language {lang.Name} does not have any entries. Skipping.", Logger.LogLevel.ERROR);
                        continue;
                    }
                    bool useDefaultFlag = false;
                    if (string.IsNullOrEmpty(lang.FlagFilepath))
                    {
                        Logger.Instance.Log($"Path to flag image is not set for language {lang.Name}. Using default flag.", Logger.LogLevel.WARNING);
                        useDefaultFlag = true;
                    }
                    else
                    {
                        string flagFilePath = GetFlagImageFullPath(lang.FlagFilepath);
                        if (!File.Exists(flagFilePath))
                        {
                            Logger.Instance.Log($"Flag image not found at {flagFilePath}. Using default flag.", Logger.LogLevel.WARNING);
                            useDefaultFlag = true;
                        }
                        else
                        {
                            if (!IMAGE_EXTENSIONS.Contains(Path.GetExtension(flagFilePath).ToUpperInvariant()))
                            {
                                Logger.Instance.Log($"Flag image at {flagFilePath} does not have a valid extension (only JPG, JPEG, BMP, GIF and PNG are supported). Using default flag.", Logger.LogLevel.WARNING);
                                useDefaultFlag = true;
                            }
                        }
                    }
                    if (useDefaultFlag)
                    {
                        string unknownLanguageFlag = GetFlagImageFullPath(UNKNOWN_LANGUAGE_FLAG_PATH);
                        lang.FlagFilepath = (File.Exists(unknownLanguageFlag) ? unknownLanguageFlag : string.Empty);
                    }

                    _languages[lang.Code] = lang;
                    Logger.Instance.Log($"Language file added: \"{lang.Name}\" (nb entries: {lang.Items.Count.ToString(CultureInfo.InvariantCulture)}).", Logger.LogLevel.INFO);
                }

            return (_languages.Count > 0);
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;
            _initialized = true;

            EnsureLangFileExistsAndIsUpToDate("English", "en_GB", "/Assets/FlagGBIcon96.png", en_GB);
            EnsureLangFileExistsAndIsUpToDate("Français", "fr_FR", "/Assets/FlagFRIcon96.png", fr_FR);

            bool success = Initialize();

            if (!_languages.ContainsKey("en_GB"))
                _languages["en_GB"] = new ASILangFile()
                {
                    Name = "English",
                    Code = "en_GB",
                    FlagFilepath = "/Assets/FlagGBIcon96.png",
                    Items = en_GB
                };
            if (!_languages.ContainsKey("fr_FR"))
                _languages["fr_FR"] = new ASILangFile()
                {
                    Name = "Français",
                    Code = "fr_FR",
                    FlagFilepath = "/Assets/FlagFRIcon96.png",
                    Items = fr_FR
                };

            Logger.Instance.Log("Languages initialized.", Logger.LogLevel.INFO);
        }

        public static void SwitchLanguage(string langCode)
        {
            EnsureInitialized();
            _selectedLanguage = (_languages.ContainsKey(langCode) ? langCode : DEFAULT_LANGUAGE_CODE);
        }

        public static string Get(string key)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (!_languages.ContainsKey(_selectedLanguage))
                return string.Empty;
            if (!_languages[_selectedLanguage].Items.ContainsKey(key))
                return string.Empty;
            if (_languages[_selectedLanguage].Items[key] == null)
                return string.Empty;
            return _languages[_selectedLanguage].Items[key];
        }

        public static void EnsureLangFileExistsAndIsUpToDate(string name, string code, string flagFilepath, Dictionary<string, string> items)
        {
            Utils.EnsureLangFolderExist();

            string outputFilepath = Path.Combine(Utils.GetLangDir(), $"{code}.json");
            /*
#if !DEBUG // Always overwrite language files in DEBUG.
            if (File.Exists(outputFilepath))
                return;
#endif
            */

            ASILangFile lang = new ASILangFile()
            {
                Name = name,
                Code = code,
                FlagFilepath = flagFilepath,
                Items = items
            };
            string jsonStr = string.Empty;
            try { jsonStr = JsonSerializer.Serialize<ASILangFile>(lang, new JsonSerializerOptions() { WriteIndented = true }); }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to serialize language {name} into JSON. Exception=[{ex}]", Logger.LogLevel.ERROR);
                return;
            }
            if (string.IsNullOrEmpty(jsonStr))
                return;
            try { File.WriteAllText(outputFilepath, jsonStr, Encoding.UTF8); }
            catch (Exception ex2)
            {
                Logger.Instance.Log($"Failed to write JSON file for language {name} at \"{outputFilepath}\". Exception=[{ex2}]", Logger.LogLevel.ERROR);
                return;
            }
        }

        private static readonly Dictionary<string, string> en_GB = new Dictionary<string, string>()
        {
            { "Language_Name", "English" },
            { "Language_Code", "en_GB" },

            // About page
            { "Version", "Version" },
            { "Author", "Author" },
            { "ASI_Description_Line1", "is an ARK Survival Ascended save file analyzer." },
            { "ASI_Description_Line2", "It allows to search, filter and inspect the various game objects like dinos, items, structures and so on." },
            { "ASI_Description_Line3", "You can check it out on GitHub here:" },
            { "ArkParse_Description_Line1", "Powered by" },
            { "ArkParse_Description_Line2", "uses ArkParse, a Python library for reading and modifying ARK Survival Ascended save files." },
            { "ArkParse_Description_Line3", "You can check it out on GitHub here:" },
            { "ASA_Description_Line1", "Some images from this app were extracted from" },
            { "ASA_Description_Line2", "You can check game's website here:" },
            { "Discord_Description_Line1", "Join us on" },

            // Shared
            { "Name", "Name" },
            { "ID", "ID" },
            { "Level", "Level" },
            { "Map", "Map" },
            { "MapName", "Map name" },
            { "SaveDatetime", "Save datetime" },
            { "InGameDatetime", "In-game datetime" },
            { "Columns", "Columns" },
            { "Add", "Add" },
            { "Modify", "Modify" },
            { "OK", "OK" },
            { "Cancel", "Cancel" },
            { "Close", "Close" },
            { "Save", "Save" },
            { "Load", "Load" },
            { "Remove", "Remove" },
            { "Yes", "Yes" },
            { "No", "No" },
            { "Settings", "Settings" },
            { "Pawns", "Pawns" },
            { "Dinos", "Dinos" },
            { "Structures", "Structures" },
            { "Items", "Items" },
            { "Players", "Players" },
            { "Tribes", "Tribes" },
            { "Other", "Other" },
            { "About", "About" },
            { "ClickHere", "Click here..." },
            { "Operator", "Operator" },
            { "OperatorAND", "AND" },
            { "OperatorOR", "OR" },
            { "SortAscending", "Ascending" },
            { "SortDescending", "Descending" },
            { "Unknown", "Unknown" },
            { "UnknownDate", "1970-01-01 00h00m00s" },
            { "UniqueID", "Unique ID" },
            { "TribeID", "Tribe ID" },
            { "ErrorHappened", "An error happened, see logs for details." },
            { "Filters", "Filters" },
            { "AddFilter", "Add filter" },
            { "EditFilters", "Edit filters" },
            { "Preset", "Preset" },
            { "Presets", "Presets" },
            { "FiltersGroups", "Filters groups" },
            { "AddToGroup", "Add to group" },
            { "EditGroup", "Edit group" },
            { "NbLinesSelected", "Nb lines selected" },
            { "PrimarySort", "Primary sort" },
            { "SecondarySort", "Secondary sort" },
            { "CopyIdToClipboard", "Copy ID to clipboard" },
            { "CopyJsonToClipboard", "Copy JSON to clipboard" },
            { "CopyUECoordsToClipboard", "Copy UE coords to clipboard" },
            { "GoToContainer", "Go to container" },
            { "GoToPlayerData", "Go to associated player data" },
            { "GoToTribeData", "Go to associated tribe data" },
            { "GoToInventoryItems", "Go to inventory items" },
            { "GoToCryopodItem", "Go to cryopod item" },
            { "CopyCryopodUECoordsToClipboard", "Copy cryopod UE coords to clipboard" },
            { "CopyCryopodGPSCoordsToClipboard", "Copy cryopod GPS coords to clipboard" },
            { "CopySelectedLinesJsonToClipboard", "Copy selected lines JSON to clipboard" },
            { "CopyTribeLogsToClipboard", "Copy tribe logs to clipboard" },
            { "IncludeVariablesWithMoreThanNValues", "Include variables with more than #VALUES_AMOUNT# values (slower but shows all variables)." },
            { "FilterBy", "Filter by" },
            { "FilterType", "Filter type" },
            { "FilterType_ExactMatch", "Exact match" },
            { "FilterType_StartingWith", "Starting with" },
            { "FilterType_EndingWith", "Ending with" },
            { "FilterType_Containing", "Containing" },
            { "FilterType_NotContaining", "Not containing" },
            { "FilterType_LowerThan", "Lower than" },
            { "FilterType_GreaterThan", "Greater than" },
            { "FilterValue", "Filter value" },
            { "SelectedValues", "Selected values" },
            { "RemoveAllFilters", "Remove all filters" },
            { "RemoveAllFiltersPresetsFromGroup", "Remove all filters presets from group" },
            { "PresetName", "Preset name" },
            { "ExistingPresets", "Existing presets" },
            { "DefaultPreset", "Default preset" },
            { "FiltersPreset", "Filters preset" },
            { "SaveFiltersIntoPreset", "Save current filters into preset" },
            { "SaveColumnsIntoPreset", "Save current columns into preset" },
            { "SaveGroupIntoPreset", "Save current group into preset" },
            { "WithValue", "with value" },
            { "WithValues", "with value(s)" },
            { "MissingOperatorCannotAddFilter", "Missing operator, cannot add filter." },
            { "MissingOperatorCannotAddGroup", "Missing operator, cannot add group." },
            { "IncorrectFiltersPreset", "Incorrect filters preset (empty preset or bad name)." },
            { "ORFiltersPreset", "OR filters preset" },
            { "ANDFiltersPreset", "AND filters preset" },
            { "FiltersPresetAddedToGroup", "Filters preset #PRESET_NAME# added to group." },
            { "CannotFindFiltersPreset", "Failed to find filters preset #PRESET_NAME#." },
            { "PresetNeedsName", "A preset needs a name." },
            { "PresetNameAlreadyExists", "This preset name already exists." },
            { "PresetSaved", "Preset saved." },
            { "PresetNotFound", "Preset #PRESET_NAME# not found." },
            { "FiltersPresetIsEmpty", "Filters preset is empty." },
            { "FiltersPresetLoaded", "Filters preset has been loaded." },
            { "NoFiltersPresetSelected", "No filters preset is currently selected." },
            { "ColumnsPresetIsEmpty", "Columns preset is empty." },
            { "ColumnsPresetLoaded", "Columns preset has been loaded." },
            { "NoColumnsPresetSelected", "No columns preset is currently selected." },
            { "GroupPresetIsEmpty", "Group preset is empty." },
            { "GroupPresetLoaded", "Group preset has been loaded." },
            { "NoGroupPresetSelected", "No group preset is currently selected." },
            { "CryopodNotFound", "Cryopod not found." },
            { "PlayerPawnNotFound", "Player pawn not found." },
            { "DinoNotFound", "Dino not found." },
            { "StructureNotFound", "Structure not found." },
            { "TribeNotFound", "Tribe not found." },
            { "CheckFilters", "Check filters." },
            { "FilteringByInventoryIDFailed", "Filtering by inventory ID failed." },
            { "InventoryIDNotFound", "Inventory ID not found." },
            { "ApplicationTheme", "Application theme" },
            { "ApplicationTheme_Dark", "Dark" },
            { "ApplicationTheme_Light", "Light" },
            { "OpenMiniMap", "Open Minimap" },
            { "OpenASIDataFolder", "Open ASI data folder" },
            { "CustomBlueprints", "Custom blueprints" },
            { "ForceReinstallArkParse", "Force reinstall ArkParse" },
            { "CustomBlueprintsSectionDescription", "You can use this section to register and manage custom blueprints. Note that registering a custom blueprint does not necessarily means that its data will be extracted successfully (data extraction succeeds if the object is similar enough to a basic ASA game object)." },
            { "RegisterCustomBlueprint", "Register custom blueprint" },
            { "BlueprintType", "Blueprint type" },
            { "DinoBlueprint", "Dino blueprint" },
            { "DinoBlueprints", "Dino blueprints" },
            { "ItemBlueprint", "Item blueprint" },
            { "ItemBlueprints", "Item blueprints" },
            { "StructureBlueprint", "Structure blueprint" },
            { "StructureBlueprints", "Structure blueprints" },
            { "BlueprintClassName", "Blueprint class name" },
            { "BlueprintClassNameExample", "Example: PrimalItem_WeaponCrossbow" },
            { "CustomBlueprintsRegistered", "Custom blueprints registered" },
            { "CannotFindASIDataFolder", "Unable to locate ASI data folder." },
            { "ReinstallArkParseFailed", "Failed to reinstall ArkParse, please check logs." },
            { "CustomDinoBlueprintUnregistered", "Custom dino blueprint unregistered." },
            { "CustomItemBlueprintUnregistered", "Custom item blueprint unregistered." },
            { "CustomStructureBlueprintUnregistered", "Custom structure blueprint unregistered." },
            { "CustomBlueprintNeedsClassName", "Cannot add blueprint without class name." },
            { "BadBlueprintTypeSelected", "Bad blueprint type selected." },
            { "CustomDinoBlueprintAlreadyRegistered", "Custom dino blueprint already registered." },
            { "CustomDinoBlueprintAdded", "Custom dino blueprint has been added." },
            { "CustomItemBlueprintAlreadyRegistered", "Custom item blueprint already registered." },
            { "CustomItemBlueprintAdded", "Custom item blueprint has been added." },
            { "CustomStructureBlueprintAlreadyRegistered", "Custom structure blueprint already registered." },
            { "CustomStructureBlueprintAdded", "Custom structure blueprint has been added." },
            { "ThemeSwitched_RestartAppToApplyChanges", "#THEME_NAME# theme enabled. Restart app to see changes." },
            { "UpdateAvailable", "New update available" },
            { "UpdateAvailableDescription", "Version #NEW_VERSION# of ASA Save Inspector is available (you are currently using version #MY_VERSION#). Would you like to download it (this will open the GitHub page in your web browser)?" },
            { "ASIStopped", "ASA Save Inspector has stopped." },
            { "CheckUpdateFailed", "Failed to check for update." },
            { "ASIIsUpToDate", "ASI is up to date." },
            { "OpenURLFailed", "Failed to open web browser with URL #URL#." },
            { "Language", "Language" },
            { "PythonSetup", "Python Setup" },
            { "PythonSetup_DescriptionLine1", "ASA Save Inspector needs Python 3 in order to extract data from save files using ArkParse." },
            { "PythonSetup_DescriptionLine2", "Please select Python executable from the dropdown menu below:" },
            { "PythonSetup_DescriptionLine3", "If you don't have Python 3 installed, you can get it here:" },
            { "JsonData", "JSON data" },
            { "JsonData_DescriptionLine1", "Please select the JSON data from the dropdown menu below:" },
            { "JsonData_ExtractSingleFile", "Extract JSON data (single save file)" },
            { "JsonData_ExtractWithPreset", "Extract JSON data (using preset)" },
            { "JsonData_ExtractFromDirectory", "Select JSON data folder manually" },
            { "JsonData_ExtractFailedCheckPython", "Cannot extract JSON data. Please verify the Python Setup above." },
            { "JsonData_SuccessfullyLoaded", "JSON data successfully loaded." },
            { "JsonData_Load", "Load JSON data" },
            { "JsonData_Remove", "Remove JSON data" },
            { "ExtractPreset_Select", "Select extraction preset" },
            { "ExtractPreset_AddExtractProfile", "Add current extraction settings to preset" },
            { "ExtractPreset_CreatePreset", "Create new extraction preset" },
            { "ExtractPreset_RemovePreset", "Remove existing extraction preset" },
            { "ExtractPreset_ViewDetails", "View extraction preset details" },
            { "ExtractJsonData", "Extract JSON data" },
            { "ExtractName", "Extraction name (optional)" },
            { "ExtractType", "Extraction type" },
            { "ExtractType_Fast", "Fast extraction" },
            { "ExtractType_Legacy", "Legacy extraction" },
            { "ExtractType_Description", "Legacy extraction can be faster if you extract only one type of JSON data, but that's not guaranteed. Prefer using fast extraction." },
            { "ExtractFailed", "Failed to extract" },
            { "JsonDataSelection", "JSON data to extract" },
            { "JsonDataInfo", "JSON data Info" },
            { "FoundJsonData", "Found JSON data" },
            { "JsonDataRemovalConfirm", "Confirm Removal" },
            { "JsonDataRemovalConfirm_Description", "Are you sure that you want to delete folder #DIRECTORY_PATH#?" },
            { "Extract", "Extract" },
            { "Create", "Create" },
            { "SelectASASaveFile", "Select ASA save file" },
            { "AddToPreset", "Add to preset" },
            { "WelcomeToASI", "Welcome to ASA Save Inspector!" },
            { "QuickStart", "Quick start:" },
            { "QuickStart_DescriptionLine1", "1. Click on \"Settings\" in the left menu." },
            { "QuickStart_DescriptionLine2", "2. Configure Python." },
            { "QuickStart_DescriptionLine3", "3. Extract then load the JSON data." },
            { "QuickStart_DescriptionLine4", "4. You can now navigate in the app using left menu buttons!" },
            { "Path", "Path" },
            { "ManualSelection", "Manual selection" },
            { "NoValidJsonFilesFound", "No valid JSON files were found in the folder." },
            { "CannotGetJsonData_IncorrectMapName", "Cannot get JSON data: Incorrect ASA map name." },
            { "CannotGetJsonData_NoValidFileName", "Cannot get JSON data: No valid JSON file name found." },
            { "CannotGetJsonData_JsonExportProfileCreationFailed", "Failed to create new JSON export profile." },
            { "IncorrectASASaveFile", "Incorrect ASA save file." },
            { "CannotExtractJsonData_IncorrectSaveFilePath", "Cannot extract JSON data: Incorrect ASA save file path." },
            { "CannotExtractJsonData_IncorrectMapName", "Cannot extract JSON data: Incorrect ASA map name." },
            { "CannotExtractJsonData_NoDataTypeSelected", "Cannot extract JSON data: No data type selected." },
            { "NoDataTypeSelected", "No data type selected." },
            { "JsonDataExtractionFailed", "JSON data extraction failed." },
            { "LoadJsonFailed_NoExportProfileSelected", "Failed to load JSON data (no export profile selected)." },
            { "LoadJsonFailed_ExportFolderNotFound", "Failed to load JSON data (export folder not found)." },
            { "LoadJsonFailed_SaveFileInfoNotFound", "Could not load savefile info JSON data (file not found at #FILEPATH#)." },
            { "LoadJsonFailed_SaveFileInfoParsingError", "Failed to load savefile info JSON data." },
            { "LoadJsonFailed_DinosFileNotFound", "Could not load dinos JSON data (file not found at #FILEPATH#)." },
            { "LoadJsonFailed_DinosFileParsingError", "Failed to load dinos JSON data." },
            { "LoadJsonFailed_PawnsFileNotFound", "Could not load player pawns JSON data (file not found at #FILEPATH#)." },
            { "LoadJsonFailed_PawnsFileParsingError", "Failed to load player pawns JSON data." },
            { "LoadJsonFailed_ItemsFileNotFound", "Could not load items JSON data (file not found at #FILEPATH#)." },
            { "LoadJsonFailed_ItemsFileParsingError", "Failed to load items JSON data." },
            { "LoadJsonFailed_StructuresFileNotFound", "Could not load structures JSON data (file not found at #FILEPATH#)." },
            { "LoadJsonFailed_StructuresFileParsingError", "Failed to load structures JSON data." },
            { "LoadJsonFailed_PlayersFileNotFound", "Could not load players JSON data (file not found at #FILEPATH#)." },
            { "LoadJsonFailed_PlayersFileParsingError", "Failed to load players JSON data." },
            { "LoadJsonFailed_TribesFileNotFound", "Could not load tribes JSON data (file not found at #FILEPATH#)." },
            { "LoadJsonFailed_TribesFileParsingError", "Failed to load tribes JSON data." },
            { "LoadJsonData_PartiallyLoaded", "JSON data was partially loaded (see logs for more info)." },
            { "LoadJsonData_Success", "JSON data successfully loaded." },
            { "RemoveJsonDataFailed_NoExportProfileSelected", "Failed to remove JSON data (no export profile selected)." },
            { "RemoveJsonDataFailed_AlreadyRemoved", "JSON data has already been removed (folder not found at #DIRECTORY_PATH#)." },
            { "RemoveJsonDataFailed", "Failed to remove JSON data." },
            { "RemoveJsonDataFailed_Details", "Failed to remove JSON data, see logs for details." },
            { "ExtractPreset_NameRequired", "An extraction preset requires a name." },
            { "ExtractPreset_Created", "Extraction preset created." },
            { "ExtractPreset_NoPresetSelected", "No extraction preset selected." },
            { "ExtractPreset_Removed", "Preset removed." },
            { "ExtractPreset_CannotAddExtractProfile_NoDataTypeSelected", "Cannot add current extraction settings to preset: No data type selected." },
            { "ExtractPreset_NoDataTypeSelected", "No data type selected." },
            { "ExtractPreset_FailedToFormatExtractProfile", "Failed to format extraction settings." },
            { "ExtractPreset_ExtractProfileAdded", "Extraction settings added to preset." },
            { "CannotAddToPreset", "Cannot add to preset." },
            { "IncorrectSaveFile", "Incorrect ASA save file." },
            { "IncorrectMapName", "Incorrect map name." },
            { "NoPresetSelected", "No preset selected." },
            { "NoExportProfileSelected", "No export profile selected." },
            { "ExtractionStarted", "Extraction started." },
            { "PresetIsEmpty", "Preset is empty." },
            { "OilVeins", "Oil Veins" },
            { "WaterVeins", "Water Veins" },
            { "GasVeins", "Gas Veins" },
            { "PowerNodes", "Power Nodes" },
            { "BeaverDams", "Beaver Dams" },
            { "ZPlants", "Z Plants" },
            { "WyvernNests", "Wyvern Nests" },
            { "GigantoraptorNests", "Gigantoraptor Nests" },
            { "ArtifactCrates", "Artifact Crates" },
            { "HordeCrates", "HordeCrates" },
            { "TributeTerminals", "Terminals" },
            { "CityTerminals", "City Terminals" },
            { "NoValidID_PlayerPawn", "Player pawn does not have a valid ID." },
            { "NoValidID_Dino", "Dino does not have a valid ID." },
            { "NoValidID_Structure", "Structure does not have a valid ID." },
            { "NoValidID_Player", "Player does not have a valid ID." },
            { "UnknownContainerType", "Unknown container type." },
            { "PlayersDataPageNotFound", "Players data page not found." },
            { "TribesDataPageNotFound", "Tribes data page not found." },

            // Python manager
            { "GetArkParseVersionFailed", "Could not get ArkParse version." },
            { "PyProjectTomlFileNotFound", "Local file pyproject.toml not found." },
            { "PyProjectTomlFileIsEmpty", "Local file pyproject.toml is empty." },
            { "PyProjectTomlFileParsingFailed", "Could not find line starting with 'version = \"' in local pyproject.toml." },
            { "RepoPyProjectTomlFileNotFound", "File pyproject.toml not found in repository." },
            { "RepoPyProjectTomlFileIsEmpty", "File pyproject.toml from repository is empty." },
            { "RepoPyProjectTomlFileParsingFailed", "Could not find line starting with 'version = \"' in pyproject.toml from repository." },
            { "ArkParseUpToDate", "ArkParse is up to date." },
            { "ArkParseUpdating", "Updating ArkParse..." },
            { "ArkParseDownloading", "Downloading ArkParse archive..." },
            { "ArkParseDownloadingError", "An error happened in DownloadArkParse." },
            { "ArkParseDownloadingFail", "Downloading ArkParse failed." },
            { "ArkParseDownloadingSuccess", "ArkParse archive successfully downloaded." },
            { "ArkParseArchiveExtracting", "Extracting ArkParse archive..." },
            { "ArkParseArchiveExtractingFail", "ArkParse archive extraction failed." },
            { "ArkParseArchiveExtractingSuccess", "ArkParse archive successfully extracted." },
            { "ArkParseDownloadAndExtractError", "An error happened in DownloadAndExtractArkParse." },
            { "PythonVenvAlreadySetup", "Python's virtual environment is already setup." },
            { "PythonVenvSetup", "Setting up virtual environment for Python..." },
            { "PythonVenvSetupError", "An error happened in SetupPythonVenv." },
            { "PythonVenvSetupFail", "Python's virtual environment setup failed." },
            { "PythonVenvSetupSuccess", "Python's virtual environment has been setup." },
            { "PythonExitCode", "Python exit code" },
            { "ArkParseInstalling", "Installing ArkParse..." },
            { "ArkParseInstallError", "An error happened in InstallArkParse." },
            { "ArkParseInstallFail", "ArkParse installation failed." },
            { "ArkParseInstallSuccess", "ArkParse successfully installed." },
            { "PythonVenvActivationScriptNotFound", "Python's virtual environment activation script not found at #FILEPATH#." },
            { "PythonVenvActivate", "Activating Python's virtual environment..." },
            { "PythonVenvActivateError", "An error happened in ActivatePythonVenv." },
            { "PythonVenvActivateFail", "Python's virtual environment activation failed." },
            { "PythonVenvActivateSuccess", "Python's virtual environment has been activated." },
            { "PythonVenvDeactivationScriptNotFound", "Python's virtual environment deactivation script not found at #FILEPATH#." },
            { "PythonVenvDeactivate", "Deactivating Python's virtual environment..." },
            { "PythonVenvDeactivateError", "An error happened in DeactivatePythonVenv." },
            { "PythonVenvDeactivateFail", "Python's virtual environment deactivation failed." },
            { "PythonVenvDeactivateSuccess", "Python's virtual environment has been deactivated." },
            { "ASIExportScript_BadSourceFilePath", "Wrong source file path to ASI export script provided." },
            { "ASIExportScript_BadDestFilePath", "Wrong destination file path for ASI export script provided." },
            { "ASIExportScript_NotFound", "Could not find file #FILEPATH# in Assets folder." },
            { "ASIExportScript_DirectoryNotAllowed", "Destination file path must not be a directory." },
            { "ASIExportScript_Error", "An error happened in CreateAsiExportScriptFile." },
            { "PythonVenvAddSetupError", "An error happened in AddPythonVenvSetup." },
            { "ArkParseAddSetupError", "An error happened in AddArkParseSetup." },
            { "ArkParseAddRunnerError", "An error happened in AddArkParseRunner." },
            { "ArkParseInstallingUpdating", "Installing/updating ArkParse..." },
            { "PythonExeNotSet", "Python executable not set." },
            { "CheckASISettings", "Check ASA Save Inspector settings." },
            { "CheckSettings", "Check settings." },
            { "ArkParseExtractingJsonData", "Extracting JSON data..." },
            { "ArkParseJsonExportProfileCreationFailed", "Failed to create new JSON export profile." },
            { "PleaseWait", "Please wait." },
            { "ArkParseRunError", "An error happened in RunArkParse." },
            { "ArkParseJsonExtractFail", "JSON data extraction failed." },
            { "ArkParseJsonExtractSuccess", "JSON data successfully extracted." },
        };

        private static readonly Dictionary<string, string> fr_FR = new Dictionary<string, string>()
        {
            { "Language_Name", "Français" },
            { "Language_Code", "fr_FR" },

            // About page
            { "Version", "Version" },
            { "Author", "Auteur" },
            { "ASI_Description_Line1", "est un analyseur de fichier de sauvegarde pour ARK Survival Ascended." },
            { "ASI_Description_Line2", "Il permet de rechercher, filtrer et inspecter les différents objets du jeu tels que les dinos, objets, structures, etc." },
            { "ASI_Description_Line3", "Le projet est hébergé sur GitHub :" },
            { "ArkParse_Description_Line1", "Propulsé par" },
            { "ArkParse_Description_Line2", "utilise ArkParse, une librairie Python de lecture et modification des fichiers de sauvegarde ARK Survival Ascended." },
            { "ArkParse_Description_Line3", "Le projet est hébergé sur GitHub :" },
            { "ASA_Description_Line1", "Certaines images de cette appli ont été extraite de" },
            { "ASA_Description_Line2", "Vous pouvez consulter le site web du jeu ici :" },
            { "Discord_Description_Line1", "Rejoignez-nous sur" },

            // Shared
            { "Name", "Nom" },
            { "ID", "ID" },
            { "Level", "Niveau" },
            { "Map", "Carte" },
            { "MapName", "Nom de la carte" },
            { "SaveDatetime", "Date de la sauvegarde" },
            { "InGameDatetime", "Date en jeu" },
            { "Columns", "Colonnes" },
            { "Add", "Ajouter" },
            { "Modify", "Modifier" },
            { "OK", "OK" },
            { "Cancel", "Annuler" },
            { "Close", "Fermer" },
            { "Save", "Sauvegarder" },
            { "Load", "Charger" },
            { "Remove", "Supprimer" },
            { "Yes", "Oui" },
            { "No", "Non" },
            { "Settings", "Réglages" },
            { "Pawns", "Persos" },
            { "Dinos", "Dinos" },
            { "Structures", "Structures" },
            { "Items", "Objets" },
            { "Players", "Joueurs" },
            { "Tribes", "Tribus" },
            { "Other", "Autre" },
            { "About", "À Propos" },
            { "ClickHere", "Cliquez ici..." },
            { "Operator", "Operateur" },
            { "OperatorAND", "ET" },
            { "OperatorOR", "OU" },
            { "SortAscending", "Ascendant" },
            { "SortDescending", "Descendant" },
            { "Unknown", "Inconnu" },
            { "UnknownDate", "1970-01-01 00h00m00s" },
            { "UniqueID", "ID unique" },
            { "TribeID", "ID de tribu" },
            { "ErrorHappened", "Une erreur est survenue, voir les logs pour plus d'info." },
            { "Filters", "Filtres" },
            { "AddFilter", "Ajouter filtre" },
            { "EditFilters", "Modifier filtres" },
            { "Preset", "Préréglage" },
            { "Presets", "Préréglages" },
            { "FiltersGroups", "Groupes de filtres" },
            { "AddToGroup", "Ajouter au groupe" },
            { "EditGroup", "Modifier le groupe" },
            { "NbLinesSelected", "Nb de lignes sélectionnées" },
            { "PrimarySort", "Tri primaire" },
            { "SecondarySort", "Tri secondaire" },
            { "CopyIdToClipboard", "Copier l'ID dans le presse-papier" },
            { "CopyJsonToClipboard", "Copier le JSON dans le presse-papier" },
            { "CopyUECoordsToClipboard", "Copier les coords UE dans le presse-papier" },
            { "GoToContainer", "Aller au conteneur" },
            { "GoToPlayerData", "Aller aux données du joueur" },
            { "GoToTribeData", "Aller aux données de la tribu" },
            { "GoToInventoryItems", "Voir les objets dans l'inventaire" },
            { "GoToCryopodItem", "Aller à la cryopod" },
            { "CopyCryopodUECoordsToClipboard", "Copier les coords UE de la cryopod dans le presse-papier" },
            { "CopyCryopodGPSCoordsToClipboard", "Copier les coords GPS de la cryopod dans le presse-papier" },
            { "CopySelectedLinesJsonToClipboard", "Copier le JSON des lignes sélectionnées dans le presse-papier" },
            { "CopyTribeLogsToClipboard", "Copier les logs de tribu dans le presse-papier" },
            { "IncludeVariablesWithMoreThanNValues", "Inclure les variables qui ont plus de #VALUES_AMOUNT# valeurs (plus lent mais affiche toutes les variables)." },
            { "FilterBy", "Filtrer par" },
            { "FilterType", "Type de filtre" },
            { "FilterType_ExactMatch", "Correspondance exacte" },
            { "FilterType_StartingWith", "Commençant par" },
            { "FilterType_EndingWith", "Finissant par" },
            { "FilterType_Containing", "Contenant" },
            { "FilterType_NotContaining", "Ne contenant pas" },
            { "FilterType_LowerThan", "Plus petit que" },
            { "FilterType_GreaterThan", "Plus grand que" },
            { "FilterValue", "Valeur du filtre" },
            { "SelectedValues", "Valeurs sélectionnées" },
            { "RemoveAllFilters", "Supprimer tous les filtres" },
            { "RemoveAllFiltersPresetsFromGroup", "Supprimer tous les préréglages de filtres du groupe" },
            { "PresetName", "Nom du préréglage" },
            { "ExistingPresets", "Préréglages existants" },
            { "DefaultPreset", "Préréglage par défaut" },
            { "FiltersPreset", "Préréglage de filtres" },
            { "SaveFiltersIntoPreset", "Sauvegarder les filtres en cours dans un préréglage" },
            { "SaveColumnsIntoPreset", "Sauvegarder les colonnes dans un préréglage" },
            { "SaveGroupIntoPreset", "Sauvegarder le groupe dans un préréglage" },
            { "WithValue", "avec la valeur" },
            { "WithValues", "avec les valeur(s)" },
            { "MissingOperatorCannotAddFilter", "Opérateur manquand, impossible d'ajouter le filtre." },
            { "MissingOperatorCannotAddGroup", "Opérateur manquand, impossible d'ajouter le groupe." },
            { "IncorrectFiltersPreset", "Préréglage de filtres incorrect (préréglage vide ou nom incorrect)." },
            { "ORFiltersPreset", "Préréglage de filtres OU" },
            { "ANDFiltersPreset", "Préréglage de filtres ET" },
            { "FiltersPresetAddedToGroup", "Préréglage de filtres #PRESET_NAME# ajouté au groupe." },
            { "CannotFindFiltersPreset", "Impossible de trouver le préréglage de filtres #PRESET_NAME#." },
            { "PresetNeedsName", "Un préréglage a besoin d'un nom." },
            { "PresetNameAlreadyExists", "Ce nom de préréglage existe déjà." },
            { "PresetSaved", "Préréglage sauvegardé." },
            { "PresetNotFound", "Préréglage #PRESET_NAME# introuvable." },
            { "FiltersPresetIsEmpty", "Le préréglage de filtres est vide." },
            { "FiltersPresetLoaded", "Le préréglage de filtre a été chargé." },
            { "NoFiltersPresetSelected", "Aucun préréglage de filtres n'est selectionné." },
            { "ColumnsPresetIsEmpty", "Le préréglage de colonnes est vide." },
            { "ColumnsPresetLoaded", "Le préréglage de colonnes a été chargé." },
            { "NoColumnsPresetSelected", "Aucun préréglage de colonnes n'est sélectionné." },
            { "GroupPresetIsEmpty", "Le préréglage de groupe est vide." },
            { "GroupPresetLoaded", "Le préréglage de groupe a été chargé." },
            { "NoGroupPresetSelected", "Aucun préréglage de groupe n'est selectionné." },
            { "CryopodNotFound", "Cryopod non trouvée." },
            { "PlayerPawnNotFound", "Perso non trouvé." },
            { "DinoNotFound", "Dino non trouvé." },
            { "StructureNotFound", "Structure non trouvée." },
            { "TribeNotFound", "Tribe not found." },
            { "CheckFilters", "Vérifiez les filtres." },
            { "FilteringByInventoryIDFailed", "Le filtrage par ID d'inventaire a échoué." },
            { "InventoryIDNotFound", "L'ID d'inventaire n'a pas été trouvé." },
            { "ApplicationTheme", "Thème de l'appli" },
            { "ApplicationTheme_Dark", "Sombre" },
            { "ApplicationTheme_Light", "Clair" },
            { "OpenMiniMap", "Ouvrir la mini-carte" },
            { "OpenASIDataFolder", "Ouvrir le dossier de données d'ASI" },
            { "CustomBlueprints", "Blueprints personnalisés" },
            { "ForceReinstallArkParse", "Réinstaller ArkParse" },
            { "CustomBlueprintsSectionDescription", "Vous pouvez utiliser cette section pour ajouter et gérer des blueprints personnalisés. Notez qu'ajouter un blueprint personnalisé ne signifie pas forcément que l'extraction va réussir (l'extraction des données réussie uniquement si l'élément est suffisament similaire à un élément du jeu de base)." },
            { "RegisterCustomBlueprint", "Ajouter un blueprint personnalisé" },
            { "BlueprintType", "Type de blueprint" },
            { "DinoBlueprint", "Blueprint de dino" },
            { "DinoBlueprints", "Blueprints de dino" },
            { "ItemBlueprint", "Blueprint d'objet" },
            { "ItemBlueprints", "Blueprints d'objet" },
            { "StructureBlueprint", "Blueprint de structure" },
            { "StructureBlueprints", "Blueprints de structure" },
            { "BlueprintClassName", "Nom de classe du blueprint" },
            { "BlueprintClassNameExample", "Exemple : PrimalItem_WeaponCrossbow" },
            { "CustomBlueprintsRegistered", "Blueprint personnalisé ajouté" },
            { "CannotFindASIDataFolder", "Le dossier de données d'ASI n'a pas été trouvé." },
            { "ReinstallArkParseFailed", "La réinstallation d'ArkParse a échouée, voir logs pour plus d'info." },
            { "CustomDinoBlueprintUnregistered", "Blueprint personnalisé de dino supprimé." },
            { "CustomItemBlueprintUnregistered", "Blueprint personnalisé d'objet supprimé." },
            { "CustomStructureBlueprintUnregistered", "Blueprint personnalisé de structure supprimé." },
            { "CustomBlueprintNeedsClassName", "Un blueprint personnalisé a besoin d'un nom de classe." },
            { "BadBlueprintTypeSelected", "Type de blueprint personnalisé sélectionné incorrect." },
            { "CustomDinoBlueprintAlreadyRegistered", "Ce blueprint personnalisé de dino existe déjà." },
            { "CustomDinoBlueprintAdded", "Le blueprint personnalisé de dino a été ajouté." },
            { "CustomItemBlueprintAlreadyRegistered", "Ce blueprint personnalisé d'objet existe déjà." },
            { "CustomItemBlueprintAdded", "Le blueprint personnalisé d'objet a été ajouté." },
            { "CustomStructureBlueprintAlreadyRegistered", "Ce blueprint personnalisé de structure existe déjà." },
            { "CustomStructureBlueprintAdded", "Le blueprint personnalisé de structure a été ajouté." },
            { "ThemeSwitched_RestartAppToApplyChanges", "#THEME_NAME# theme enabled. Restart app to see changes." },
            { "UpdateAvailable", "Nouvelle mise à jour disponible" },
            { "UpdateAvailableDescription", "La version #NEW_VERSION# de ASA Save Inspector est disponible (vous utilisez actuellement la version #MY_VERSION#). Voulez-vous la télécharger (cela ouvrira la page web d'ASI dans votre navigateur) ?" },
            { "ASIStopped", "ASA Save Inspector s'est arrêté." },
            { "CheckUpdateFailed", "La recherche de mise à jour a échouée." },
            { "ASIIsUpToDate", "ASI est à jour." },
            { "OpenURLFailed", "Echec de l'ouverture du navigateur web sur l'adresse #URL#." },
            { "Language", "Langue" },
            { "PythonSetup", "Configuration de Python" },
            { "PythonSetup_DescriptionLine1", "ASA Save Inspector a besoin de Python 3 afin d'extraire les données du fichier de sauvegarde via ArkParse." },
            { "PythonSetup_DescriptionLine2", "Sélectionnez l'exécutable Python via le menu déroulant ci-dessous :" },
            { "PythonSetup_DescriptionLine3", "Si vous n'avez pas Python 3 d'installé, vous pouvez le récuperer ici :" },
            { "JsonData", "Données JSON" },
            { "JsonData_DescriptionLine1", "Choisissez les données JSON via le menu déroulant ci-dessous :" },
            { "JsonData_ExtractSingleFile", "Extraire les données JSON (via fichier de sauvegarde)" },
            { "JsonData_ExtractWithPreset", "Extraire les données JSON (via préréglages)" },
            { "JsonData_ExtractFromDirectory", "Récupérer les données JSON depuis un dossier" },
            { "JsonData_ExtractFailedCheckPython", "Impossible d'extraire les données JSON. Vérifiez la configuration de Python ci-dessus." },
            { "JsonData_SuccessfullyLoaded", "Les données JSON ont bien été chargées." },
            { "JsonData_Load", "Charger les données JSON" },
            { "JsonData_Remove", "Supprimer les données JSON" },
            { "ExtractPreset_Select", "Choisissez un préréglage d'extraction" },
            { "ExtractPreset_AddExtractProfile", "Ajouer les paramètres d'extraction en cours au préréglage" },
            { "ExtractPreset_CreatePreset", "Créer un nouveau préréglage d'extraction" },
            { "ExtractPreset_RemovePreset", "Supprimer un préréglage d'extraction existant" },
            { "ExtractPreset_ViewDetails", "Voir les détails du préréglage d'extraction" },
            { "ExtractJsonData", "Extraire les données JSON" },
            { "ExtractName", "Nom de l'extraction (optionnel)" },
            { "ExtractType", "Type d'extraction" },
            { "ExtractType_Fast", "Extraction rapide" },
            { "ExtractType_Legacy", "Extraction legacy (ancienne méthode)" },
            { "ExtractType_Description", "L'extraction legacy peut être plus rapide si vous ne sélectionnez qu'un seul type de données JSON, mais cela n'est pas garantit. Préférez l'extraction rapide." },
            { "ExtractFailed", "L'extraction a échouée" },
            { "JsonDataSelection", "Données JSON à extraire" },
            { "JsonDataInfo", "Info sur les données JSON" },
            { "FoundJsonData", "Les données JSON ont été trouvés" },
            { "JsonDataRemovalConfirm", "Confirmez la suppression" },
            { "JsonDataRemovalConfirm_Description", "Êtes-vous sûr de vouloir supprimer le dossier #DIRECTORY_PATH# ?" },
            { "Extract", "Extraire" },
            { "Create", "Créer" },
            { "SelectASASaveFile", "Sélectionner le fichier de sauvegarde ASA" },
            { "AddToPreset", "Ajouter au préréglage" },
            { "WelcomeToASI", "Bienvenue dans ASA Save Inspector !" },
            { "QuickStart", "Démarrage rapide :" },
            { "QuickStart_DescriptionLine1", "1. Cliquez sur \"Réglages\" dans le menu de gauche." },
            { "QuickStart_DescriptionLine2", "2. Configurez Python." },
            { "QuickStart_DescriptionLine3", "3. Extrayez puis chargez les données JSON." },
            { "QuickStart_DescriptionLine4", "4. Vous pouvez désormais naviguer dans l'appli via les boutons dans le menu de gauche !" },
            { "Path", "Chemin" },
            { "ManualSelection", "Sélection manuelle" },
            { "NoValidJsonFilesFound", "Aucun fichier JSON valide dans le dossier." },
            { "CannotGetJsonData_IncorrectMapName", "Récupération des données JSON impossible: Nom de carte incorrect." },
            { "CannotGetJsonData_NoValidFileName", "Récupération des données JSON impossible: Aucun nom de fichier JSON valide." },
            { "CannotGetJsonData_JsonExportProfileCreationFailed", "La création d'un nouveau profil d'export JSON a échouée." },
            { "IncorrectASASaveFile", "Fichier de sauvegarde ASA incorrect." },
            { "CannotExtractJsonData_IncorrectSaveFilePath", "Extraction des données JSON impossible: Chemin vers fichier de sauvegarde incorrect." },
            { "CannotExtractJsonData_IncorrectMapName", "Extraction des données JSON impossible: Nom de carte incorrect." },
            { "CannotExtractJsonData_NoDataTypeSelected", "Extraction des données JSON impossible: Aucun type de données sélectionné." },
            { "NoDataTypeSelected", "Aucun type de données selectionné." },
            { "JsonDataExtractionFailed", "L'extraction des données JSON a échouée." },
            { "LoadJsonFailed_NoExportProfileSelected", "Echec du chargement des données JSON: Aucun profil d'export sélectionné." },
            { "LoadJsonFailed_ExportFolderNotFound", "Echec du chargement des données JSON: Dossier d'export introuvable." },
            { "LoadJsonFailed_SaveFileInfoNotFound", "Echec du chargement des données JSON des info de sauvegarde: Fichier non trouvé à #FILEPATH#." },
            { "LoadJsonFailed_SaveFileInfoParsingError", "Echec du chargement des données JSON des info de sauvegarde." },
            { "LoadJsonFailed_DinosFileNotFound", "Echec du chargement des données JSON des dinos: Fichier non trouvé à #FILEPATH#." },
            { "LoadJsonFailed_DinosFileParsingError", "Echec du chargement des données JSON des dinos." },
            { "LoadJsonFailed_PawnsFileNotFound", "Echec du chargement des données JSON des persos: Fichier non trouvé à #FILEPATH#." },
            { "LoadJsonFailed_PawnsFileParsingError", "Echec du chargement des données JSON des persos." },
            { "LoadJsonFailed_ItemsFileNotFound", "Echec du chargement des données JSON des objets: Fichier non trouvé à #FILEPATH#." },
            { "LoadJsonFailed_ItemsFileParsingError", "Echec du chargement des données JSON des objets." },
            { "LoadJsonFailed_StructuresFileNotFound", "Echec du chargement des données JSON des structures: Fichier non trouvé à #FILEPATH#." },
            { "LoadJsonFailed_StructuresFileParsingError", "Echec du chargement des données JSON des structures." },
            { "LoadJsonFailed_PlayersFileNotFound", "Echec du chargement des données JSON des joueurs: Fichier non trouvé à #FILEPATH#." },
            { "LoadJsonFailed_PlayersFileParsingError", "Echec du chargement des données JSON des joueurs." },
            { "LoadJsonFailed_TribesFileNotFound", "Echec du chargement des données JSON des tribus: Fichier non trouvé à #FILEPATH#." },
            { "LoadJsonFailed_TribesFileParsingError", "Echec du chargement des données JSON des tribus." },
            { "LoadJsonData_PartiallyLoaded", "Les données JSON ont été partiellement chargées (voir logs pour plus d'info)." },
            { "LoadJsonData_Success", "Les données JSON ont bien été chargées." },
            { "RemoveJsonDataFailed_NoExportProfileSelected", "Echec de la suppression des données JSON: Aucun profil d'export sélectionné." },
            { "RemoveJsonDataFailed_AlreadyRemoved", "Les données JSON ont déjà été supprimées (dossier non trouvé à #DIRECTORY_PATH#)." },
            { "RemoveJsonDataFailed", "Echec de la suppression des données JSON." },
            { "RemoveJsonDataFailed_Details", "Echec de la suppression des données JSON, voir les logs pour plus d'info." },
            { "ExtractPreset_NameRequired", "Un préréglage d'extraction doit avoir un nom." },
            { "ExtractPreset_Created", "Préréglage d'extraction créé." },
            { "ExtractPreset_NoPresetSelected", "Aucun préréglage d'extraction sélectionné." },
            { "ExtractPreset_Removed", "Préréglage supprimé." },
            { "ExtractPreset_CannotAddExtractProfile_NoDataTypeSelected", "Impossible d'ajouter le profil d'extraction au préréglage: Aucun type de données sélectionné." },
            { "ExtractPreset_NoDataTypeSelected", "Aucun type de données sélectionné." },
            { "ExtractPreset_FailedToFormatExtractProfile", "Echec du formattage du profil d'extraction." },
            { "ExtractPreset_ExtractProfileAdded", "Profil d'extraction ajouté au préréglage." },
            { "CannotAddToPreset", "Impossible d'ajouter au préréglage." },
            { "IncorrectSaveFile", "Fichier de sauvegarde ASA incorrect." },
            { "IncorrectMapName", "Nom de carte incorrect." },
            { "NoPresetSelected", "Aucun préréglage sélectionné." },
            { "NoExportProfileSelected", "Aucun profil d'extraction sélectionné." },
            { "ExtractionStarted", "L'extraction commence." },
            { "PresetIsEmpty", "Le préréglage est vide." },
            { "OilVeins", "Filons de Pétrole" },
            { "WaterVeins", "Veines d'Eau" },
            { "GasVeins", "Veines de Gaz" },
            { "PowerNodes", "Nœuds d'Alimentation" },
            { "BeaverDams", "Barrages de Castor" },
            { "ZPlants", "Plantes Z" },
            { "WyvernNests", "Nids de Wyverne" },
            { "GigantoraptorNests", "Nids de Gigantoraptor" },
            { "ArtifactCrates", "Caisses d'Artefact" },
            { "HordeCrates", "Caisses de Horde" },
            { "TributeTerminals", "Terminaux" },
            { "CityTerminals", "Terminaux de Ville" },
            { "NoValidID_PlayerPawn", "Le perso n'a pas d'identifiant correct." },
            { "NoValidID_Dino", "Le dino n'a pas d'identifiant correct." },
            { "NoValidID_Structure", "La structure n'a pas d'identifiant correct." },
            { "UnknownContainerType", "Le type du conteneur est inconnu." },
            { "PlayersDataPageNotFound", "Page de données des joueurs non trouvée." },
            { "TribesDataPageNotFound", "Page de données des tribus non trouvée." },
            
            // Python manager
            { "GetArkParseVersionFailed", "Impossible de récupérer la version de ArkParse." },
            { "PyProjectTomlFileNotFound", "Le fichier local pyproject.toml est introuvable." },
            { "PyProjectTomlFileIsEmpty", "Le fichier local pyproject.toml est vide." },
            { "PyProjectTomlFileParsingFailed", "Impossible de trouver la ligne commençant par 'version = \"' dans le fichier local pyproject.toml." },
            { "RepoPyProjectTomlFileNotFound", "Le fichier pyproject.toml est introuvable dans le dépôt." },
            { "RepoPyProjectTomlFileIsEmpty", "Le fichier pyproject.toml du dépôt est vide." },
            { "RepoPyProjectTomlFileParsingFailed", "Impossible de trouver la ligne commençant par 'version = \"' dans le fichier pyproject.toml du dépôt." },
            { "ArkParseUpToDate", "ArkParse est à jour." },
            { "ArkParseUpdating", "Mise à jour de ArkParse..." },
            { "ArkParseDownloading", "Téléchargement de l'archive ArkParse..." },
            { "ArkParseDownloadingError", "Une erreur est survenue dans DownloadArkParse." },
            { "ArkParseDownloadingFail", "Le téléchargement de ArkParse a échoué." },
            { "ArkParseDownloadingSuccess", "L'archive ArkParse a bien été téléchargée." },
            { "ArkParseArchiveExtracting", "Extraction de l'archive ArkParse..." },
            { "ArkParseArchiveExtractingFail", "L'extraction de l'archive ArkParse a échouée." },
            { "ArkParseArchiveExtractingSuccess", "L'archive ArkParse a bien été extraite." },
            { "ArkParseDownloadAndExtractError", "Une erreur est survenue dans DownloadAndExtractArkParse." },
            { "PythonVenvAlreadySetup", "L'environnement virtuel Python est déjà installé." },
            { "PythonVenvSetup", "Installation de l'environnement virtuel Python..." },
            { "PythonVenvSetupError", "Une erreur est survenue dans SetupPythonVenv." },
            { "PythonVenvSetupFail", "L'installation de l'environnement virtuel Python a échouée." },
            { "PythonVenvSetupSuccess", "L'environnement virtuel Python a bien été installé." },
            { "PythonExitCode", "Code de retour Python" },
            { "ArkParseInstalling", "Installation de ArkParse..." },
            { "ArkParseInstallError", "Une erreur est survenue dans InstallArkParse." },
            { "ArkParseInstallFail", "L'installation de ArkParse a échouée." },
            { "ArkParseInstallSuccess", "ArkParse a bien été installé." },
            { "PythonVenvActivationScriptNotFound", "Le script d'activation de l'environnement virtuel Python est introuvable à #FILEPATH#." },
            { "PythonVenvActivate", "Activation de l'environnement virtuel Python..." },
            { "PythonVenvActivateError", "Une erreur est survenue dans ActivatePythonVenv." },
            { "PythonVenvActivateFail", "L'activation de l'environnement virtuel Python a échouée." },
            { "PythonVenvActivateSuccess", "L'environnement virtuel Python a bien été activé." },
            { "PythonVenvDeactivationScriptNotFound", "Le script de désactivation de l'environnement virtuel Python est introuvable à #FILEPATH#." },
            { "PythonVenvDeactivate", "Désactivation de l'environnement virtuel Python..." },
            { "PythonVenvDeactivateError", "Une erreur est survenue dans DeactivatePythonVenv." },
            { "PythonVenvDeactivateFail", "La désactivation de l'environnement virtuel Python a échouée." },
            { "PythonVenvDeactivateSuccess", "L'environnement virtuel Python a bien été désactivé." },
            { "ASIExportScript_BadSourceFilePath", "Le chemin source vers le script d'export ASI est incorrect." },
            { "ASIExportScript_BadDestFilePath", "Le chemin destination vers le script d'export ASI est incorrect." },
            { "ASIExportScript_NotFound", "Impossible de trouver le fichier #FILEPATH# dans le dossier d'Assets." },
            { "ASIExportScript_DirectoryNotAllowed", "Le chemin de destination du fichier ne doit pas être un dossier." },
            { "ASIExportScript_Error", "Une erreur est survenue dans CreateAsiExportScriptFile." },
            { "PythonVenvAddSetupError", "Une erreur est survenue dans AddPythonVenvSetup." },
            { "ArkParseAddSetupError", "Une erreur est survenue dans AddArkParseSetup." },
            { "ArkParseAddRunnerError", "Une erreur est survenue dans AddArkParseRunner." },
            { "ArkParseInstallingUpdating", "Installation/Mise à jour de ArkParse..." },
            { "PythonExeNotSet", "L'exécutable Python n'est pas définit." },
            { "CheckASISettings", "Vérifiez les réglages d'ASA Save Inspector." },
            { "CheckSettings", "Vérifiez les réglages." },
            { "ArkParseExtractingJsonData", "Extraction des données JSON..." },
            { "ArkParseJsonExportProfileCreationFailed", "La création d'un nouveau profil d'export JSON a échouée." },
            { "PleaseWait", "Veuillez patienter." },
            { "ArkParseRunError", "Une erreur est survenue dans RunArkParse." },
            { "ArkParseJsonExtractFail", "L'extraction des données JSON a échouée." },
            { "ArkParseJsonExtractSuccess", "Les données JSON ont bien été extraites." },
        };
    }
}
