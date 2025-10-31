using ASA_Save_Inspector.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace ASA_Save_Inspector.ObjectModelUtils
{
    public static class DinoUtils
    {
        public static List<string> DefaultSelectedColumns = new List<string>()
        {
            "ShortName",
            "TamedName",
            "DinoID1",
            "DinoID2",
            "bIsCryopodded",
            "bIsFemale",
            "bIsBaby",
            "BaseLevel",
            "BaseHealth",
            "BaseStamina",
            "BaseOxygen",
            "BaseFood",
            "BaseWeight",
            "BaseDamage",
            "BaseSpeed",
            "CurrentLevel",
            "TamerString",
            "ImprinterName",
            "OwningTribeID",
            "TribeName",
            "TargetingTeam",
            "IsTamed",
            "IsUnclaimed",
            "MapCoords",
            "CryopodCoords"
        };

        public static List<string> DefaultColumnsOrder = new List<string>()
        {
            "Dino",
            "Level",
            "Base Level",
            "Tamed",
            "Unclaimed",
            "Baby",
            "Female",
            "Cryoed",
            "Map Coords",
            "Cryo Coords",
            "Base HP",
            "Base Stam",
            "Base Oxy",
            "Base Food",
            "Base Weight",
            "Base Dmg",
            "Base Speed",
            "ID 1",
            "ID 2",
            "Owning Tribe ID"
        };

        private static readonly Dictionary<string, string> CleanNames = new Dictionary<string, string>()
        {
            { "ShortName", "Dino" },
            { "TamedName", "Tamed Name" },
            { "DinoID1", "ID 1" },
            { "DinoID2", "ID 2" },
            { "bIsCryopodded", "Cryoed" },
            { "bIsFemale", "Female" },
            { "bIsBaby", "Baby" },
            { "BaseLevel", "Base Level" },
            { "CurrentLevel", "Level" },
            { "TamerString", "Tamer" },
            { "ImprinterName", "Imprinter" },
            { "TribeName", "Tribe" },
            { "IsTamed", "Tamed" },
            { "IsUnclaimed", "Unclaimed" },
            { "OwningTribeID", "Owning Tribe ID" },
            { "TargetingTeam", "Tribe ID" },
            { "BaseHealth", "Base HP" },
            { "BaseStamina", "Base Stam" },
            { "BaseTorpidity", "Base Torpidity" },
            { "BaseOxygen", "Base Oxy" },
            { "BaseFood", "Base Food" },
            { "BaseWater", "Base Water" },
            { "BaseTemperature", "Base Temperature" },
            { "BaseWeight", "Base Weight" },
            { "BaseDamage", "Base Dmg" },
            { "BaseSpeed", "Base Speed" },
            { "BaseFortitude", "Base Fortitude" },
            { "BaseCraftingSpeed", "Base Crafting" },
            { "MutatedHealth", "Mutated HP" },
            { "MutatedStamina", "Mutated Stam" },
            { "MutatedTorpidity", "Mutated Torpidity" },
            { "MutatedOxygen", "Mutated Oxy" },
            { "MutatedFood", "Mutated Food" },
            { "MutatedWater", "Mutated Water" },
            { "MutatedTemperature", "Mutated Temperature" },
            { "MutatedWeight", "Mutated Weight" },
            { "MutatedDamage", "Mutated Dmg" },
            { "MutatedSpeed", "Mutated Speed" },
            { "MutatedFortitude", "Mutated Fortitude" },
            { "MutatedCraftingSpeed", "Mutated Crafting" },
            { "AddedHealth", "Added HP" },
            { "AddedStamina", "Added Stam" },
            { "AddedTorpidity", "Added Torpidity" },
            { "AddedOxygen", "Added Oxy" },
            { "AddedFood", "Added Food" },
            { "AddedWater", "Added Water" },
            { "AddedTemperature", "Added Temperature" },
            { "AddedWeight", "Added Weight" },
            { "AddedDamage", "Added Dmg" },
            { "AddedSpeed", "Added Speed" },
            { "AddedFortitude", "Added Fortitude" },
            { "AddedCraftingSpeed", "Added Crafting" },
            { "ValueHealth", "Value HP" },
            { "ValueStamina", "Value Stam" },
            { "ValueTorpidity", "Value Torpidity" },
            { "ValueOxygen", "Value Oxy" },
            { "ValueFood", "Value Food" },
            { "ValueWater", "Value Water" },
            { "ValueTemperature", "Value Temperature" },
            { "ValueWeight", "Value Weight" },
            { "ValueDamage", "Value Dmg" },
            { "ValueSpeed", "Value Speed" },
            { "ValueFortitude", "Value Fortitude" },
            { "ValueCraftingSpeed", "Value Crafting" },
            { "MapCoords", "Map Coords" },
            { "CryopodCoords", "Cryo Coords" },
        };

        public static readonly List<string> DoNotCheckPropertyValuesAmount = new List<string>()
        {
            "ActorTransformX",
            "ActorTransformY",
            "ActorTransformZ",
            "AddedStatPoints",
            "AlgaeSwimmingTimeOffset",
            "BabyAge",
            "BabyNextCuddleTime",
            "BabyNextCuddleTimeReadble",
            "BaseLevel",
            "BaseStatPoints",
            "BiteCorpseTime",
            "BiteCorpseTimeReadable",
            "bTimersAreInUTC",
            "CharacterSavedDynamicBase",
            "CharacterSavedDynamicBaseRelativeLocation",
            "CharacterSavedDynamicBaseRelativeRotation",
            "ColorSetIndices",
            "ColorSetNames",
            "CorpseDestructionTime",
            "CorpseDestructionTimer",
            "CorpseDestructionTimeReadable",
            "CorpseDestructionTimerReadable",
            "CryopodUUID",
            "CurrentLevel",
            "DinoAncestors",
            "DinoAncestorsMale",
            "DinoDownloadedAtTime",
            "DinoDownloadedAtTimeReadable",
            "DinoID1",
            "DinoID2",
            "FriendModeEndTime",
            "FriendModeEndTimeReadable",
            "GeneTraits",
            "Instigator",
            "InventoryUUID",
            "ItemArchetype",
            "LastEggSpawnChanceTime",
            "LastEggSpawnChanceTimeReadable",
            "LastEnterStasisTime",
            "LastEnterStasisTimeReadable",
            "LastFeatherPluckTime",
            "LastFeatherPluckTimeReadable",
            "LastInAllyRangeSerialized",
            "LastMilkProductionTime",
            "LastMilkProductionTimeReadable",
            "LastReincarnateTime",
            "LastReincarnateTimeReadable",
            "LastSkinnedTime",
            "LastSkinnedTimeReadable",
            "LastTameConsumedFoodTime",
            "LastTameConsumedFoodTimeReadable",
            "LastTimeFinishedTraining",
            "LastTimeFinishedTrainingReadable",
            "LastTimeHarvested",
            "LastTimeHarvestedReadable",
            "LastTimeSheared",
            "LastTimeShearedReadable",
            "LastTimeSwimming_Algae",
            "LastTimeSwimming_AlgaeReadable",
            "LastTimeUpdatedCharacterStatusComponent",
            "LastTimeUpdatedCharacterStatusComponentReadable",
            "LastTimeUpdatedCorpseDestructionTime",
            "LastTimeUpdatedCorpseDestructionTimeReadable",
            "LastTimeUsedSkillsArray",
            "LastTimeUsedSkillsArrayReadable",
            "LastUnstasisStructureTime",
            "LastUnstasisStructureTimeReadable",
            "LastUpdate",
            "LastUpdatedBabyAgeAtTime",
            "LastUpdatedBabyAgeAtTimeReadable",
            "LastUpdatedGestationAtTime",
            "LastUpdatedGestationAtTimeReadable",
            "LastUpdatedMatingAtTime",
            "LastUpdatedMatingAtTimeReadable",
            "LastUpkeepNetworkTime",
            "LastUpkeepNetworkTimeReadable",
            "LastWildNestSpawnTime",
            "LastWildNestSpawnTimeReadable",
            "Location",
            "MapCoords",
            "MutatedStatPoints",
            "MyCharacterStatusComponent",
            "NextAllowedMatingTime",
            "NextAllowedMatingTimeReadable",
            "NumTimesTrained",
            "OriginalCreationTime",
            "OriginalCreationTimeReadable",
            "OriginalNPCVolumeName",
            "RequiredTameAffinity",
            //"ShortName",
            "StatValues",
            "TamedAtTime",
            "TamedAtTimeReadable",
            "TamedName",
            "TamedTimeStamp",
            "TamingLastFoodConsumptionTime",
            "TamingLastFoodConsumptionTimeReadable",
            "TimeOfLastFeeding",
            "TimeOfLastFeedingReadable",
            "TimeOfLastHarvest",
            "TimeOfLastHarvestReadable",
            "UntamedPoopTimeCache",
            "UntamedPoopTimeCacheReadable",
            "UUID",
            "ValueDamage",
            "ValueFood",
            "ValueHealth",
            "ValueOxygen",
            "ValueStamina",
            "ValueTorpidity",
            "ValueWeight",
            "WildRandomScale",
        };

        public static string? GetCleanNameFromPropertyName(string? propertyName) => (propertyName != null && CleanNames.ContainsKey(propertyName) ? CleanNames[propertyName] : propertyName);
        public static string? GetPropertyNameFromCleanName(string? cleanName) => Utils.GetPropertyNameFromCleanName(CleanNames, cleanName);
    }
}

namespace ASA_Save_Inspector.ObjectModel
{
    public partial class BondedDinoData
    {
        public override string ToString() => $"dino_class={(dino_class ?? string.Empty)} dino_name={(dino_name ?? string.Empty)} id1={(id1 != null && id1.HasValue ? id1.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)} id2={(id2 != null && id2.HasValue ? id2.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public partial class SaddleStructure
    {
        public override string ToString() => $"bone_name={(bone_name ?? string.Empty)} location={(location != null ? location.ToString() : string.Empty)} my_structure={(my_structure ?? string.Empty)} rotation={(rotation != null ? rotation.ToString() : string.Empty)}";
    }

    public partial class Dino : INotifyPropertyChanged
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        private int[]? _baseStats = null;
        private int[]? _mutatedStats = null;
        private int[]? _addedStats = null;
        private double[]? _statsValues = null;

        //private double? _lat = null;
        //private double? _long = null;

        public void InitStats()
        {
            _baseStats = ParseStatsPoints(BaseStatPoints);
            _mutatedStats = ParseStatsPoints(MutatedStatPoints);
            _addedStats = ParseStatsPoints(AddedStatPoints);
            _statsValues = ParseStatsValues(StatValues);
        }

        /*
        public void InitMapCoords(string? mapName)
        {
            KeyValuePair<double, double> coords = Utils.GetMapCoords(mapName, ActorTransformX, ActorTransformY, ActorTransformZ);
            _lat = coords.Key;
            _long = coords.Value;
        }
        */

        public bool IsTamed
        {
            get { return TargetingTeam > 50000; }
            private set { }
        }

        public bool IsUnclaimed
        {
            get { return TargetingTeam == 2000000000; }
            private set { }
        }

        public int OwningTribeID
        {
            get
            {
                if (Cryopod != null && Cryopod.ContainerTribeID != null && Cryopod.ContainerTribeID.HasValue)
                    return Cryopod.ContainerTribeID.Value;
                else if (TargetingTeam != null && TargetingTeam.HasValue)
                    return TargetingTeam.Value;
                else
                    return 0;
            }
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

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseHealth { get { return (_baseStats == null ? 0 : _baseStats[0]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseStamina { get { return (_baseStats == null ? 0 : _baseStats[1]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseTorpidity { get { return (_baseStats == null ? 0 : _baseStats[2]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseOxygen { get { return (_baseStats == null ? 0 : _baseStats[3]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseFood { get { return (_baseStats == null ? 0 : _baseStats[4]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseWater { get { return (_baseStats == null ? 0 : _baseStats[5]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseTemperature { get { return (_baseStats == null ? 0 : _baseStats[6]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseWeight { get { return (_baseStats == null ? 0 : _baseStats[7]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseDamage { get { return (_baseStats == null ? 0 : _baseStats[8]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseSpeed { get { return (_baseStats == null ? 0 : _baseStats[9]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseFortitude { get { return (_baseStats == null ? 0 : _baseStats[10]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int BaseCraftingSpeed { get { return (_baseStats == null ? 0 : _baseStats[11]); } private set { } }


        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedHealth { get { return (_mutatedStats == null ? 0 : _mutatedStats[0]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedStamina { get { return (_mutatedStats == null ? 0 : _mutatedStats[1]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedTorpidity { get { return (_mutatedStats == null ? 0 : _mutatedStats[2]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedOxygen { get { return (_mutatedStats == null ? 0 : _mutatedStats[3]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedFood { get { return (_mutatedStats == null ? 0 : _mutatedStats[4]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedWater { get { return (_mutatedStats == null ? 0 : _mutatedStats[5]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedTemperature { get { return (_mutatedStats == null ? 0 : _mutatedStats[6]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedWeight { get { return (_mutatedStats == null ? 0 : _mutatedStats[7]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedDamage { get { return (_mutatedStats == null ? 0 : _mutatedStats[8]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedSpeed { get { return (_mutatedStats == null ? 0 : _mutatedStats[9]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedFortitude { get { return (_mutatedStats == null ? 0 : _mutatedStats[10]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int MutatedCraftingSpeed { get { return (_mutatedStats == null ? 0 : _mutatedStats[11]); } private set { } }


        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedHealth { get { return (_addedStats == null ? 0 : _addedStats[0]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedStamina { get { return (_addedStats == null ? 0 : _addedStats[1]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedTorpidity { get { return (_addedStats == null ? 0 : _addedStats[2]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedOxygen { get { return (_addedStats == null ? 0 : _addedStats[3]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedFood { get { return (_addedStats == null ? 0 : _addedStats[4]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedWater { get { return (_addedStats == null ? 0 : _addedStats[5]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedTemperature { get { return (_addedStats == null ? 0 : _addedStats[6]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedWeight { get { return (_addedStats == null ? 0 : _addedStats[7]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedDamage { get { return (_addedStats == null ? 0 : _addedStats[8]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedSpeed { get { return (_addedStats == null ? 0 : _addedStats[9]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedFortitude { get { return (_addedStats == null ? 0 : _addedStats[10]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public int AddedCraftingSpeed { get { return (_addedStats == null ? 0 : _addedStats[11]); } private set { } }


        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueHealth { get { return (_statsValues == null ? 0.0d : _statsValues[0]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueStamina { get { return (_statsValues == null ? 0.0d : _statsValues[1]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueTorpidity { get { return (_statsValues == null ? 0.0d : _statsValues[2]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueOxygen { get { return (_statsValues == null ? 0.0d : _statsValues[3]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueFood { get { return (_statsValues == null ? 0.0d : _statsValues[4]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueWater { get { return (_statsValues == null ? 0.0d : _statsValues[5]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueTemperature { get { return (_statsValues == null ? 0.0d : _statsValues[6]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueWeight { get { return (_statsValues == null ? 0.0d : _statsValues[7]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueDamage { get { return (_statsValues == null ? 0.0d : _statsValues[8]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueSpeed { get { return (_statsValues == null ? 0.0d : _statsValues[9]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueFortitude { get { return (_statsValues == null ? 0.0d : _statsValues[10]); } private set { } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public double ValueCraftingSpeed { get { return (_statsValues == null ? 0.0d : _statsValues[11]); } private set { } }

        private bool _searchedCryo = false;
        private Item? _cryopod = null;
        public Item? Cryopod
        {
            get
            {
                if (_cryopod == null && !_searchedCryo && bIsCryopodded != null && bIsCryopodded.HasValue && bIsCryopodded.Value && !string.IsNullOrEmpty(CryopodUUID))
                {
                    _cryopod = Utils.FindItemByUUID(CryopodUUID);
                    _searchedCryo = true;
                }
                return _cryopod;
            }
            private set { }
        }

        public string? CryopodCoords
        {
            get { return Cryopod?.MapCoords; }
            private set { }
        }

        public DateTime? BabyNextCuddleTimeReadble
        {
            get { return Utils.GetDateTimeFromGameTime(BabyNextCuddleTime); }
            private set { }
        }

        public DateTime? BiteCorpseTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(BiteCorpseTime); }
            private set { }
        }

        public DateTime? CorpseDestructionTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(CorpseDestructionTime); }
            private set { }
        }

        public DateTime? CorpseDestructionTimerReadable
        {
            get { return Utils.GetDateTimeFromGameTime(CorpseDestructionTimer); }
            private set { }
        }

        public DateTime? DinoDownloadedAtTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(DinoDownloadedAtTime); }
            private set { }
        }

        public DateTime? FriendModeEndTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(FriendModeEndTime); }
            private set { }
        }

        public DateTime? LastEggSpawnChanceTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastEggSpawnChanceTime); }
            private set { }
        }

        public DateTime? LastEnterStasisTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastEnterStasisTime); }
            private set { }
        }

        public DateTime? LastFeatherPluckTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastFeatherPluckTime); }
            private set { }
        }

        public DateTime? LastMilkProductionTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastMilkProductionTime); }
            private set { }
        }

        public DateTime? LastReincarnateTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastReincarnateTime); }
            private set { }
        }

        public DateTime? LastSkinnedTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastSkinnedTime); }
            private set { }
        }

        public DateTime? LastTameConsumedFoodTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTameConsumedFoodTime); }
            private set { }
        }

        public DateTime? LastTimeFinishedTrainingReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTimeFinishedTraining); }
            private set { }
        }

        public DateTime? LastTimeHarvestedReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTimeHarvested); }
            private set { }
        }

        public DateTime? LastTimeShearedReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTimeSheared); }
            private set { }
        }

        public DateTime? LastTimeSwimming_AlgaeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTimeSwimming_Algae); }
            private set { }
        }

        public DateTime? LastTimeUpdatedCharacterStatusComponentReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTimeUpdatedCharacterStatusComponent); }
            private set { }
        }

        public DateTime? LastTimeUpdatedCorpseDestructionTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTimeUpdatedCorpseDestructionTime); }
            private set { }
        }

        private CustomList<DateTime?>? _lastTimeUsedSkillsArrayReadable = null;
        public CustomList<DateTime?>? LastTimeUsedSkillsArrayReadable
        {
            get
            {
                if (_lastTimeUsedSkillsArrayReadable != null)
                    return _lastTimeUsedSkillsArrayReadable;
                _lastTimeUsedSkillsArrayReadable = new CustomList<DateTime?>();
                if (LastTimeUsedSkillsArray != null && LastTimeUsedSkillsArray.Count > 0)
                    foreach (var lastTimeUsedSkill in LastTimeUsedSkillsArray)
                    {
                        if (lastTimeUsedSkill == null)
                            _lastTimeUsedSkillsArrayReadable.Add(null);
                        else
                            _lastTimeUsedSkillsArrayReadable.Add(Utils.GetDateTimeFromGameTime(lastTimeUsedSkill));
                    }
                return _lastTimeUsedSkillsArrayReadable;
            }
            private set { }
        }

        public DateTime? LastUnstasisStructureTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastUnstasisStructureTime); }
            private set { }
        }

        public DateTime? LastUpdatedBabyAgeAtTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastUpdatedBabyAgeAtTime); }
            private set { }
        }

        public DateTime? LastUpdatedGestationAtTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastUpdatedGestationAtTime); }
            private set { }
        }

        public DateTime? LastUpdatedMatingAtTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastUpdatedMatingAtTime); }
            private set { }
        }

        public DateTime? LastUpkeepNetworkTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastUpkeepNetworkTime); }
            private set { }
        }

        public DateTime? LastWildNestSpawnTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastWildNestSpawnTime); }
            private set { }
        }

        public DateTime? NextAllowedMatingTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(NextAllowedMatingTime); }
            private set { }
        }

        public DateTime? OriginalCreationTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(OriginalCreationTime); }
            private set { }
        }

        public DateTime? TamedAtTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(TamedAtTime); }
            private set { }
        }

        public DateTime? TamingLastFoodConsumptionTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(TamingLastFoodConsumptionTime); }
            private set { }
        }

        public DateTime? TimeOfLastFeedingReadable
        {
            get { return Utils.GetDateTimeFromGameTime(TimeOfLastFeeding); }
            private set { }
        }

        public DateTime? TimeOfLastHarvestReadable
        {
            get { return Utils.GetDateTimeFromGameTime(TimeOfLastHarvest); }
            private set { }
        }

        public DateTime? UntamedPoopTimeCacheReadable
        {
            get { return Utils.GetDateTimeFromGameTime(UntamedPoopTimeCache); }
            private set { }
        }

        public string GetDinoID() => $"{(DinoID1 != null && DinoID1.HasValue ? DinoID1.Value.ToString(CultureInfo.InvariantCulture) : "0")}{(DinoID2 != null && DinoID2.HasValue ? DinoID2.Value.ToString(CultureInfo.InvariantCulture) : "0")}";

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

        private int[]? ParseStatsPoints(string? stats)
        {
            //Statpoints(None)([health=30, stamina=27, torpidity=0, oxygen=33, food=30, water=0, temperature=0, weight=34, melee_damage=35, movement_speed=0, fortitude=0, crafting_speed=0])
            if (string.IsNullOrEmpty(stats))
                return null;

            int stt = stats.IndexOf('[', StringComparison.InvariantCulture);
            stt += 1;
            int end = stats.IndexOf("]", stt, StringComparison.InvariantCulture);
            if (stt > 0 && end > 0 && end > stt && stats.Length > end)
            {
                string statsStr = stats.Substring(stt, (end - stt));
                string[] splitted = statsStr.Split(", ", StringSplitOptions.RemoveEmptyEntries);
                if (splitted != null && splitted.Length == 12)
                {
                    int[] statsPoints = new int[12];
                    for (int i = 0; i < 12; i++)
                    {
                        string[] nameValue = splitted[i].Split('=', StringSplitOptions.RemoveEmptyEntries);
                        if (nameValue != null && nameValue.Length == 2)
                        {
                            if (int.TryParse(nameValue[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentStat) && currentStat >= 0 && currentStat <= 255)
                                statsPoints[i] = currentStat;
                            else
                            {
                                statsPoints[i] = 0;
                                Logger.Instance.Log($"Incorrect amount of points \"{nameValue[1]}\" for stat {nameValue[0]} (dino ID: {DinoID1} {DinoID2}).", Logger.LogLevel.WARNING);
                            }
                        }
                        else
                            Logger.Instance.Log($"Incorrect stat string \"{splitted[i]}\" (dino ID: {DinoID1} {DinoID2}).", Logger.LogLevel.WARNING);
                    }
                    return statsPoints;
                }
            }
            return null;
        }

        private double[]? ParseStatsValues(string? stats)
        {
            //Statvalues(points added)([health=12973.373046875, stamina=879.046875, torpidity=0.0, oxygen=645.0, food=8598.466796875, water=100.0, temperature=0.0, weight=260.70001220703125, melee_damage=2.5868003368377686, movement_speed=0.0, fortitude=0.0, crafting_speed=0.0])
            if (string.IsNullOrEmpty(stats))
                return null;

            int stt = stats.IndexOf('[', StringComparison.InvariantCulture);
            stt += 1;
            int end = stats.IndexOf("]", stt, StringComparison.InvariantCulture);
            if (stt > 0 && end > 0 && end > stt && stats.Length > end)
            {
                string statsStr = stats.Substring(stt, (end - stt));
                string[] splitted = statsStr.Split(", ", StringSplitOptions.RemoveEmptyEntries);
                if (splitted != null && splitted.Length == 12)
                {
                    double[] statsPoints = new double[12];
                    for (int i = 0; i < 12; i++)
                    {
                        string[] nameValue = splitted[i].Split('=', StringSplitOptions.RemoveEmptyEntries);
                        if (nameValue != null && nameValue.Length == 2)
                        {
                            if (double.TryParse(nameValue[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double currentStat))
                                statsPoints[i] = currentStat;
                            else
                            {
                                statsPoints[i] = 0.0d;
                                Logger.Instance.Log($"Incorrect value \"{nameValue[1]}\" for stat {nameValue[0]} (dino ID: {DinoID1} {DinoID2}).", Logger.LogLevel.WARNING);
                            }
                        }
                        else
                            Logger.Instance.Log($"Incorrect stat values string \"{splitted[i]}\" (dino ID: {DinoID1} {DinoID2}).", Logger.LogLevel.WARNING);
                    }
                    return statsPoints;
                }
            }
            return null;
        }
    }
}
