using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using ASA_Save_Inspector.Pages;

namespace ASA_Save_Inspector.ObjectModelUtils
{
    public static class PlayerPawnUtils
    {
        public static readonly List<string> DefaultSelectedColumns = new List<string>()
        {
            "PlayerName",
            "PlatformProfileName",
            "LinkedPlayerDataID",
            "UniqueNetID",
            "TargetingTeam",
            "TribeName",
            "MapCoords",
            "bIsSleeping",
            "ShortName",
        };

        public static List<string> DefaultColumnsOrder = new List<string>()
        {
            "Name",
            "Platform name",
            "Player ID",
            "Unique ID",
            "Tribe ID",
            "Tribe name",
            "Map coords",
            "Sleeping",
            "Character",
        };

        private static readonly Dictionary<string, string> CleanNames = new Dictionary<string, string>()
        {
            { "TargetingTeam", "Tribe ID" },
            { "TribeName", "Tribe name" },
            { "UniqueNetID", "Unique ID" },
            { "LinkedPlayerDataID", "Player ID" },
            { "bIsSleeping", "Sleeping" },
            { "ShortName", "Character" },
            { "PlatformProfileName", "Platform name" },
            { "PlayerName", "Name" },
            { "MapCoords", "Map coords" },
        };

        public static readonly List<string> DoNotCheckPropertyValuesAmount = new List<string>()
        {
            "ActorTransformX",
            "ActorTransformY",
            "ActorTransformZ",
            "BodyColors",
            "CurrentWeapon",
            "DynamicMaterialBytes",
            "Instigator",
            "InventoryUUID",
            "LastEnterStasisTime",
            "LastEnterStasisTimeReadable",
            "LastTimeUpdatedCharacterStatusComponent",
            "LastTimeUpdatedCharacterStatusComponentReadable",
            "LinkedPlayerDataID",
            "Location",
            "MapCoords",
            "MyCharacterStatusComponent",
            "OriginalCreationTime",
            "OriginalCreationTimeReadable",
            "OriginalHairColor",
            "PlatformProfileID",
            "PlatformProfileName",
            "PlayerName",
            "SavedLastTimeHadController",
            "SavedLastTimeHadControllerReadable",
            "UUID",
            "UniqueNetID"
        };

        public static string? GetCleanNameFromPropertyName(string? propertyName) => (propertyName != null && CleanNames.ContainsKey(propertyName) ? CleanNames[propertyName] : propertyName);
        public static string? GetPropertyNameFromCleanName(string? cleanName) => Utils.GetPropertyNameFromCleanName(CleanNames, cleanName);
    }
}

namespace ASA_Save_Inspector.ObjectModel
{
    public partial class PlatformProfileID : IComparable, IComparable<PlatformProfileID>
    {
        public int CompareTo(object? obj) => string.Compare(this.ToString(), (obj != null ? obj.ToString() : string.Empty));

        public int CompareTo(PlatformProfileID? other) => string.Compare(this.ToString(), (other != null ? other.ToString() : string.Empty));

        public override string ToString() => $"value={(value ?? string.Empty)} value_type={(value_type ?? string.Empty)} unknown={(unknown != null && unknown.HasValue ? unknown.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public partial class PlayerPawn : INotifyPropertyChanged, IComparable, IComparable<PlayerPawn>
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        public int CompareTo(object? obj) => string.Compare(this.ToString(), (obj != null ? obj.ToString() : string.Empty));

        public int CompareTo(PlayerPawn? other) => string.Compare(this.ToString(), (other != null ? other.ToString() : string.Empty));

        public string? ShortName
        {
            get => Utils.GetShortNameFromItemArchetype(ItemArchetype);
            private set { }
        }

        public string? UniqueNetID
        {
            get { return $"{(PlatformProfileID != null && PlatformProfileID.value != null ? PlatformProfileID.value : string.Empty)}"; }
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

        public DateTime? LastEnterStasisTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastEnterStasisTime); }
            private set { }
        }

        public DateTime? LastTimeUpdatedCharacterStatusComponentReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTimeUpdatedCharacterStatusComponent); }
            private set { }
        }

        public DateTime? OriginalCreationTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(OriginalCreationTime); }
            private set { }
        }

        public DateTime? SavedLastTimeHadControllerReadable
        {
            get { return Utils.GetDateTimeFromGameTime(SavedLastTimeHadController); }
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
