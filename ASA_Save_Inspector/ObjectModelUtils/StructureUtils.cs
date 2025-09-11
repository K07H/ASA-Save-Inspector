using ASA_Save_Inspector.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace ASA_Save_Inspector.ObjectModelUtils
{
    internal class StructureUtils
    {
        public static List<string> DefaultSelectedColumns = new List<string>()
        {
            "ShortName",
            "TargetingTeam",
            "bIsBlueprint",
            "bIsEngram",
            "bHasItems",
            "bHasFuel",
            "bIsLocked",
            "bIsPinLocked",
            "bIsPowered",
            "BoxName",
            "CurrentItemCount",
            "NumBullets",
            "Health",
            "Location",
            "OwnerName",
            "OriginalPlacedTimeStamp",
            "OriginalPlacerPlayerID",
            "SignText",
            "StructureID",
            "MapCoords"
        };

        public static List<string> DefaultColumnsOrder = new List<string>()
        {
            "Structure",
            "Structure ID",
            "Tribe ID"
        };

        private static readonly Dictionary<string, string> CleanNames = new Dictionary<string, string>()
        {
            { "ShortName", "Structure" },
            { "TargetingTeam", "Tribe ID" },
            { "OwnerName", "Owner Name" },
            { "OwningPlayerID", "Owning Player ID" },
            { "OwningPlayerName", "Owning Player Name" },
            { "bIsBlueprint", "Blueprint" },
            { "bIsEngram", "Engram" },
            { "bHasItems", "Has Items" },
            { "bHasFuel", "Has Fuel" },
            { "bIsFertilized", "Fertilized" },
            { "bIsFoundation", "Foundation" },
            { "bIsLocked", "Locked" },
            { "bIsPinLocked", "Pin Locked" },
            { "bIsPowered", "Powered" },
            { "BoxName", "Box Name" },
            { "CurrentItemCount", "Nb Items" },
            { "NumBullets", "Nb Bullets" },
            { "OriginalPlacedTimeStamp", "Placed Date" },
            { "OriginalPlacerPlayerID", "Placed By" },
            { "SignText", "Sign Text" },
            { "StructureID", "Structure ID" },
            { "MapCoords", "Map Coords" }
        };

        public static readonly List<string> DoNotCheckPropertyValuesAmount = new List<string>()
        {
            "ActivatedAtTime",
            "ActivatedAtTimeReadable",
            "ActorTransformX",
            "ActorTransformY",
            "ActorTransformZ",
            "AutoCloseTimeSetting",
            "bHasResetDecayTime",
            "CropRefreshTimeCache",
            "CropRefreshTimeCacheReadable",
            "CurrentFuelTimeCache",
            "CurrentFuelTimeCacheReadable",
            "Health",
            "InventoryUUID",
            "ItemArchetype",
            "LastActivatedTime",
            "LastActivatedTimeReadable",
            "LastAutoDurabilityDecreaseTime",
            "LastAutoDurabilityDecreaseTimeReadable",
            "LastCheckedFuelTime",
            "LastCheckedFuelTimeReadable",
            "LastCropRefreshTime",
            "LastCropRefreshTimeReadable",
            "LastDamagedAtTime",
            "LastDamagedAtTimeReadable",
            "LastDeactivatedTime",
            "LastDeactivatedTimeReadable",
            "LastEnterStasisTime",
            "LastEnterStasisTimeReadable",
            "LastFireTime",
            "LastFireTimeReadable",
            "LastInAllyRangeTimeSerialized",
            "LastInAllyRangeTimeSerializedReadable",
            "LastInventoryRefreshTime",
            "LastInventoryRefreshTimeReadable",
            "LastLongReloadStartTime",
            "LastLongReloadStartTimeReadable",
            "LinkedStructureUUIDs",
            "Location",
            "MapCoords",
            "NetDestructionTime",
            "NetDestructionTimeReadable",
            "NextAllowedUseTime",
            "NextAllowedUseTimeReadable",
            "NextBoostActivationTime",
            "NextBoostActivationTimeReadable",
            "NextBoostDeactivationTime",
            "NextBoostDeactivationTimeReadable",
            "OriginalCreationTime",
            "OriginalCreationTimeReadable",
            "OriginalPlacedTimeStamp",
            "OriginalRespecStartTime",
            "OriginalRespecStartTimeReadable",
            "PlacedOnFloorStructure",
            "PreventCryopodDeploymentTilTime",
            "PreventCryopodDeploymentTilTimeReadable",
            "ShortName",
            "StructureID",
            "StructuresPlacedOnFloor",
            "timeStateStarted",
            "timeStateStartedReadable",
            "UUID",
        };

        public static string? GetCleanNameFromPropertyName(string? propertyName) => (propertyName != null && CleanNames.ContainsKey(propertyName) ? CleanNames[propertyName] : propertyName);
        public static string? GetPropertyNameFromCleanName(string? cleanName) => Utils.GetPropertyNameFromCleanName(CleanNames, cleanName);
    }
}

namespace ASA_Save_Inspector.ObjectModel
{
    public partial class CustomFolderItem
    {
        public override string ToString() => $"name={(name ?? string.Empty)} inventory_comp_type={(inventory_comp_type != null && inventory_comp_type.HasValue ? inventory_comp_type.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)} custom_folder_ids={Utils.JoinObjectsToString(custom_folder_ids)}";
    }

    public partial class WirelessExchangeRefs
    {
        public override string ToString() => $"properties={Utils.JoinObjectsToString(properties)}";
    }

    public partial class StoredTrait
    {
        public override string ToString() => $"class_name={(class_name ?? string.Empty)} name={(name ?? string.Empty)} unique_id={(unique_id != null && unique_id.HasValue ? unique_id.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public partial class Structure : INotifyPropertyChanged
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        /*
        private double? _lat = null;
        private double? _long = null;

        public void InitMapCoords(string? mapName)
        {
            KeyValuePair<double, double> mapCoords = Utils.GetMapCoords(mapName, ActorTransformX, ActorTransformY, ActorTransformZ);
            _lat = mapCoords.Key;
            _long = mapCoords.Value;
        }
        */

        public string? ShortName
        {
            get => Utils.GetShortNameFromItemArchetype(ItemArchetype);
            private set { }
        }

        public string? Location
        {
            get { return $"{(ActorTransformX != null && ActorTransformX.HasValue ? ActorTransformX.Value.ToString("F0", CultureInfo.InvariantCulture) : "0")} {(ActorTransformY != null && ActorTransformY.HasValue ? ActorTransformY.Value.ToString("F0", CultureInfo.InvariantCulture) : "0")} {(ActorTransformZ != null && ActorTransformZ.HasValue ? ActorTransformZ.Value.ToString("F0", CultureInfo.InvariantCulture) : "0")}"; }
            private set { }
        }

        public string? MapCoords
        {
            get { return $"{GetGPSCoords().Key.ToString("F1", CultureInfo.InvariantCulture)} {GetGPSCoords().Value.ToString("F1", CultureInfo.InvariantCulture)}"; }
            private set { }
        }

        public DateTime? ActivatedAtTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(ActivatedAtTime); }
            private set { }
        }

        public DateTime? CropRefreshTimeCacheReadable
        {
            get { return Utils.GetDateTimeFromGameTime(CropRefreshTimeCache); }
            private set { }
        }

        public DateTime? CurrentFuelTimeCacheReadable
        {
            get { return Utils.GetDateTimeFromGameTime(CurrentFuelTimeCache); }
            private set { }
        }

        public DateTime? LastActivatedTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastActivatedTime); }
            private set { }
        }

        public DateTime? LastAutoDurabilityDecreaseTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastAutoDurabilityDecreaseTime); }
            private set { }
        }

        public DateTime? LastCheckedFuelTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastCheckedFuelTime); }
            private set { }
        }

        public DateTime? LastCropRefreshTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastCropRefreshTime); }
            private set { }
        }

        public DateTime? LastDamagedAtTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastDamagedAtTime); }
            private set { }
        }

        public DateTime? LastDeactivatedTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastDeactivatedTime); }
            private set { }
        }

        public DateTime? LastEnterStasisTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastEnterStasisTime); }
            private set { }
        }

        public DateTime? LastFireTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastFireTime); }
            private set { }
        }

        public DateTime? LastInAllyRangeTimeSerializedReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastInAllyRangeTimeSerialized); }
            private set { }
        }

        public DateTime? LastInventoryRefreshTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastInventoryRefreshTime); }
            private set { }
        }

        public DateTime? LastLongReloadStartTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastLongReloadStartTime); }
            private set { }
        }

        public DateTime? NetDestructionTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(NetDestructionTime); }
            private set { }
        }

        public DateTime? NextAllowedUseTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(NextAllowedUseTime); }
            private set { }
        }

        public DateTime? NextBoostActivationTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(NextBoostActivationTime); }
            private set { }
        }

        public DateTime? NextBoostDeactivationTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(NextBoostDeactivationTime); }
            private set { }
        }

        public DateTime? OriginalCreationTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(OriginalCreationTime); }
            private set { }
        }

        public DateTime? OriginalRespecStartTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(OriginalRespecStartTime); }
            private set { }
        }

        public DateTime? PreventCryopodDeploymentTilTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(PreventCryopodDeploymentTilTime); }
            private set { }
        }

        public DateTime? timeStateStartedReadable
        {
            get { return Utils.GetDateTimeFromGameTime(timeStateStarted); }
            private set { }
        }

        private KeyValuePair<double, double>? _gpsCoords = null;
        public KeyValuePair<double, double> GetGPSCoords()
        {
            if (_gpsCoords != null && _gpsCoords.HasValue)
                return _gpsCoords.Value;
            if (string.IsNullOrEmpty(SettingsPage._currentlyLoadedMapName))
                return new KeyValuePair<double, double>(0.0d, 0.0d);
            var coords = Utils.GetMapCoords(SettingsPage._currentlyLoadedMapName, ActorTransformX, ActorTransformY, ActorTransformZ);
            _gpsCoords = new KeyValuePair<double, double>(coords.Key, coords.Value);
            return _gpsCoords.Value;
        }

        private KeyValuePair<double, double>? _asiMinimapCoords = null;
        public KeyValuePair<double, double> GetASIMinimapCoords()
        {
            if (_asiMinimapCoords != null && _asiMinimapCoords.HasValue)
                return _asiMinimapCoords.Value;
            if (string.IsNullOrEmpty(SettingsPage._currentlyLoadedMapName))
                return new KeyValuePair<double, double>(0.0d, 0.0d);
            var coords = Utils.GetASIMinimapCoords(SettingsPage._currentlyLoadedMapName, ActorTransformX, ActorTransformY, ActorTransformZ);
            _asiMinimapCoords = new KeyValuePair<double, double>(coords.Key, coords.Value);
            return _asiMinimapCoords.Value;
        }

    }
}
