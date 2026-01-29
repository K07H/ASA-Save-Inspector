using ASA_Save_Inspector.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace ASA_Save_Inspector.ObjectModelUtils
{
    public static class PlayerUtils
    {
        public static readonly List<string> DefaultSelectedColumns = new List<string>()
        {
            "ExperiencePoints",
            "FoundOnMap",
            "IsFemale",
            "Level",
            "MapCoords",
            "NumOfDeaths",
            "PlayerName",
            "PlayerCharacterName",
            "PlayerDataID",
            "TribeID",
            "TribeName",
            "UniqueID",
            "Health",
            "Stamina",
            "Oxygen",
            "Food",
            "Weight",
            "MeleeDamage",
            "MovementSpeed",
            "Fortitude",
            "CraftingSpeed",
        };

        public static List<string> DefaultColumnsOrder = new List<string>()
        {
            "ID",
            "Unique ID",
            "Tribe ID",
            "Tribe name",
            "Name",
            "Platform name",
            "Female",
            "Pawn found",
            "Map coords",
            "Num deaths",
            "XP",
            "Level",
            "Health",
            "Stamina",
            "Oxygen",
            "Food",
            "Weight",
            "Melee dmg",
            "Move speed",
            "Fortitude",
            "Craft speed",
        };

        private static readonly Dictionary<string, string> CleanNames = new Dictionary<string, string>()
        {
            { "ExperiencePoints", "XP" },
            { "FoundOnMap", "Pawn found" },
            { "IsFemale", "Female" },
            { "MapCoords", "Map coords" },
            { "NumOfDeaths", "Num deaths" },
            { "PlayerName", "Platform name" },
            { "PlayerCharacterName", "Name" },
            { "PlayerDataID", "ID" },
            { "TribeID", "Tribe ID" },
            { "TribeName", "Tribe name" },
            { "UniqueID", "Unique ID" },
            { "MeleeDamage", "Melee dmg" },
            { "MovementSpeed", "Move speed" },
            { "CraftingSpeed", "Craft speed" },
        };

        public static readonly List<string> DoNotCheckPropertyValuesAmount = new List<string>()
        {
            "BodyColors",
            "DynamicMaterialBytes",
            "ExperiencePoints",
            "LastTimeDiedToEnemyTeam",
            "LastTimeDiedToEnemyTeamReadable",
            "Level",
            "LoginTime",
            "LoginTimeReadable",
            "NumOfDeaths",
            "PercentageOfFacialHairGrowth",
            "PerMapExplorerNoteUnlocks",
            "PlayerCharacterName",
            "PlayerDataID",
            //"PlayerName",
            "PlayerState_EngramBlueprints",
            "PlayerState_TotalEngramPoints",
            "RawBoneModifiers",
            "SavedNetworkAddress",
            //"TribeID",
            //"TribeName",
            "UniqueID",
        };

        public static string? GetCleanNameFromPropertyName(string? propertyName) => (propertyName != null && CleanNames.ContainsKey(propertyName) ? CleanNames[propertyName] : propertyName);
        public static string? GetPropertyNameFromCleanName(string? cleanName) => Utils.GetPropertyNameFromCleanName(CleanNames, cleanName);
    }
}

namespace ASA_Save_Inspector.ObjectModel
{
    public partial class Config : IComparable, IComparable<Config>
    {
        public int CompareTo(object? obj) => string.Compare(this.ToString(), (obj != null ? obj.ToString() : string.Empty));

        public int CompareTo(Config? other) => string.Compare(this.ToString(), (other != null ? other.ToString() : string.Empty));

        public override string ToString() => $"bIsFemale={(bIsFemale != null && bIsFemale.HasValue ? bIsFemale.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, EyebrowIndex={(EyebrowIndex != null && EyebrowIndex.HasValue ? EyebrowIndex.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, HeadHairIndex={(HeadHairIndex != null && HeadHairIndex.HasValue ? HeadHairIndex.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, PercentageOfFacialHairGrowth={(PercentageOfFacialHairGrowth != null && PercentageOfFacialHairGrowth.HasValue ? PercentageOfFacialHairGrowth.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, PercentOfFullHeadHairGrowth={(PercentOfFullHeadHairGrowth != null && PercentOfFullHeadHairGrowth.HasValue ? PercentOfFullHeadHairGrowth.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, PlayerSpawnRegionIndex={(PlayerSpawnRegionIndex != null && PlayerSpawnRegionIndex.HasValue ? PlayerSpawnRegionIndex.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, PlayerVoiceCollectionIndex={(PlayerVoiceCollectionIndex != null && PlayerVoiceCollectionIndex.HasValue ? PlayerVoiceCollectionIndex.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, BodyColors={(BodyColors != null && BodyColors.Count > 0 ? BodyColors.ToString() : string.Empty)}, DynamicMaterialBytes={(DynamicMaterialBytes != null && DynamicMaterialBytes.Count > 0 ? DynamicMaterialBytes.ToString() : string.Empty)}, RawBoneModifier={(RawBoneModifiers != null && RawBoneModifiers.Count > 0 ? RawBoneModifiers.ToString() : string.Empty)}";
    }

    public partial class CharacterStatusComponentNumberOfLevelUpPointsApplied : IComparable, IComparable<CharacterStatusComponentNumberOfLevelUpPointsApplied>
    {
        public int CompareTo(object? obj) => string.Compare(this.ToString(), (obj != null ? obj.ToString() : string.Empty));

        public int CompareTo(CharacterStatusComponentNumberOfLevelUpPointsApplied? other) => string.Compare(this.ToString(), (other != null ? other.ToString() : string.Empty));

        public override string ToString() => $"CraftingSpeed={(CraftingSpeed != null && CraftingSpeed.HasValue ? CraftingSpeed.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Food={(Food != null && Food.HasValue ? Food.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Fortitude={(Fortitude != null && Fortitude.HasValue ? Fortitude.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Health={(Health != null && Health.HasValue ? Health.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, MeleeDamage={(MeleeDamage != null && MeleeDamage.HasValue ? MeleeDamage.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, MovementSpeed={(MovementSpeed != null && MovementSpeed.HasValue ? MovementSpeed.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Oxygen={(Oxygen != null && Oxygen.HasValue ? Oxygen.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Stamina={(Stamina != null && Stamina.HasValue ? Stamina.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Temperature={(Temperature != null && Temperature.HasValue ? Temperature.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Torpidity={(Torpidity != null && Torpidity.HasValue ? Torpidity.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Water={(Water != null && Water.HasValue ? Water.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, Weight={(Weight != null && Weight.HasValue ? Weight.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public partial class Stats
    {
        public int CompareTo(object? obj) => string.Compare(this.ToString(), (obj != null ? obj.ToString() : string.Empty));

        public int CompareTo(Stats? other) => string.Compare(this.ToString(), (other != null ? other.ToString() : string.Empty));

        public override string ToString() => $"ExperiencePoints={(CharacterStatusComponent_ExperiencePoints != null && CharacterStatusComponent_ExperiencePoints.HasValue ? CharacterStatusComponent_ExperiencePoints.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, ExtraCharacterLevel={(CharacterStatusComponent_ExtraCharacterLevel != null && CharacterStatusComponent_ExtraCharacterLevel.HasValue ? CharacterStatusComponent_ExtraCharacterLevel.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}, NumberOfLevelUpPointsApplied={(CharacterStatusComponent_NumberOfLevelUpPointsApplied != null ? CharacterStatusComponent_NumberOfLevelUpPointsApplied.ToString() : string.Empty)}, EmoteUnlocks={(EmoteUnlocks != null && EmoteUnlocks.Count > 0 ? EmoteUnlocks.ToString() : string.Empty)}, PerMapExplorerNoteUnlocks={(PerMapExplorerNoteUnlocks != null && PerMapExplorerNoteUnlocks.Count > 0 ? PerMapExplorerNoteUnlocks.ToString() : string.Empty)}, EngramBlueprints{(PlayerState_EngramBlueprints != null && PlayerState_EngramBlueprints.Count > 0 ? PlayerState_EngramBlueprints.ToString() : string.Empty)}, TotalEngramPoints={(PlayerState_TotalEngramPoints != null && PlayerState_TotalEngramPoints.HasValue ? PlayerState_TotalEngramPoints.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public partial class PrimalBuffPersistentData : IComparable, IComparable<PrimalBuffPersistentData>
    {
        public int CompareTo(object? obj) => string.Compare(this.ToString(), (obj != null ? obj.ToString() : string.Empty));

        public int CompareTo(PrimalBuffPersistentData? other) => string.Compare(this.ToString(), (other != null ? other.ToString() : string.Empty));

        public override string ToString() => $"ForPrimalBuffClass={(ForPrimalBuffClass ?? string.Empty)}, ForPrimalBuffClassString={(ForPrimalBuffClassString ?? string.Empty)}";
    }

    public partial class Player : INotifyPropertyChanged, IComparable, IComparable<Player>
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        public int CompareTo(object? obj) => string.Compare(this.ToString(), (obj != null ? obj.ToString() : string.Empty));

        public int CompareTo(Player? other) => string.Compare(this.ToString(), (other != null ? other.ToString() : string.Empty));

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

        private bool _searchedTribeName = false;
        private string? _tribeName = null;
        public string? TribeName
        {
            get
            {
                if (TribeID != null && TribeID.HasValue && !_searchedTribeName && SettingsPage._tribesData != null)
                {
                    _searchedTribeName = true;
                    Tribe? t = SettingsPage._tribesData.FirstOrDefault(t => t?.TribeID == this.TribeID, null);
                    if (t != null)
                        _tribeName = t.TribeName;
                }
                return _tribeName;
            }
            private set { }
        }

        public DateTime? LastTimeDiedToEnemyTeamReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTimeDiedToEnemyTeam); }
            private set { }
        }

        public DateTime? LoginTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LoginTime); }
            private set { }
        }

        public bool? IsFemale
        {
            get { return Config?.bIsFemale; }
            private set { }
        }

        public string? BodyColors
        {
            get { return (Config?.BodyColors?.ToString() ?? string.Empty); }
            private set { }
        }

        public string? DynamicMaterialBytes
        {
            get { return (Config?.DynamicMaterialBytes?.ToString() ?? string.Empty); }
            private set { }
        }

        public int? EyebrowIndex
        {
            get { return Config?.EyebrowIndex; }
            private set { }
        }

        public int? HeadHairIndex
        {
            get { return Config?.HeadHairIndex; }
            private set { }
        }

        public double? PercentageOfFacialHairGrowth
        {
            get { return Config?.PercentageOfFacialHairGrowth; }
            private set { }
        }

        public double? PercentOfFullHeadHairGrowth
        {
            get { return Config?.PercentOfFullHeadHairGrowth; }
            private set { }
        }

        public int? PlayerSpawnRegionIndex
        {
            get { return Config?.PlayerSpawnRegionIndex; }
            private set { }
        }

        public int? PlayerVoiceCollectionIndex
        {
            get { return Config?.PlayerVoiceCollectionIndex; }
            private set { }
        }

        public string? RawBoneModifiers
        {
            get { return (Config?.RawBoneModifiers?.ToString() ?? string.Empty); }
            private set { }
        }

        public string? ExperiencePoints
        {
            get { return (Stats?.CharacterStatusComponent_ExperiencePoints != null && Stats.CharacterStatusComponent_ExperiencePoints.HasValue ? Stats.CharacterStatusComponent_ExperiencePoints.Value.ToString("F0", CultureInfo.InvariantCulture) : "0"); }
            private set { }
        }

        public int? Level
        {
            get { return Stats?.CharacterStatusComponent_ExtraCharacterLevel; }
            private set { }
        }

        public string? EmoteUnlocks
        {
            get { return (Stats?.EmoteUnlocks?.ToString() ?? string.Empty); }
            private set { }
        }

        public string? PerMapExplorerNoteUnlocks
        {
            get { return (Stats?.PerMapExplorerNoteUnlocks?.ToString() ?? string.Empty); }
            private set { }
        }

        public string? PlayerState_EngramBlueprints
        {
            get { return (Stats?.PlayerState_EngramBlueprints?.ToString() ?? string.Empty); }
            private set { }
        }

        public int? PlayerState_TotalEngramPoints
        {
            get { return Stats?.PlayerState_TotalEngramPoints; }
            private set { }
        }

        public int? Health
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Health ?? 0; }
            private set { }
        }

        public int? Stamina
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Stamina ?? 0; }
            private set { }
        }

        public int? Torpidity
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Torpidity ?? 0; }
            private set { }
        }

        public int? Oxygen
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Oxygen ?? 0; }
            private set { }
        }

        public int? Food
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Food ?? 0; }
            private set { }
        }

        public int? Water
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Water ?? 0; }
            private set { }
        }

        public int? Temperature
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Temperature ?? 0; }
            private set { }
        }

        public int? Weight
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Weight ?? 0; }
            private set { }
        }

        public int? MeleeDamage
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.MeleeDamage ?? 0; }
            private set { }
        }

        public int? MovementSpeed
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.MovementSpeed ?? 0; }
            private set { }
        }

        public int? Fortitude
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.Fortitude ?? 0; }
            private set { }
        }

        public int? CraftingSpeed
        {
            get { return Stats?.CharacterStatusComponent_NumberOfLevelUpPointsApplied?.CraftingSpeed ?? 0; }
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
