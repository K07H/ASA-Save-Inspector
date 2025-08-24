using System.Collections.Generic;

namespace ASA_Save_Inspector.ObjectModel
{

    public partial class Config
    {
        public bool? bIsFemale { get; set; }
        public CustomList<LinearColor?>? BodyColors { get; set; }
        public CustomList<int?>? DynamicMaterialBytes { get; set; }
        public int? EyebrowIndex { get; set; }
        public int? HeadHairIndex { get; set; }
        public double? PercentageOfFacialHairGrowth { get; set; }
        public double? PercentOfFullHeadHairGrowth { get; set; }
        public int? PlayerSpawnRegionIndex { get; set; }
        public int? PlayerVoiceCollectionIndex { get; set; }
        public CustomList<double?>? RawBoneModifiers { get; set; }
    }

    public partial class CharacterStatusComponentNumberOfLevelUpPointsApplied
    {
        public int? CraftingSpeed { get; set; }
        public int? Food { get; set; }
        public int? Fortitude { get; set; }
        public int? Health { get; set; }
        public int? MeleeDamage { get; set; }
        public int? MovementSpeed { get; set; }
        public int? Oxygen { get; set; }
        public int? Stamina { get; set; }
        public int? Temperature { get; set; }
        public int? Torpidity { get; set; }
        public int? Water { get; set; }
        public int? Weight { get; set; }
    }

    public partial class Stats
    {
        public double? CharacterStatusComponent_ExperiencePoints { get; set; }
        public int? CharacterStatusComponent_ExtraCharacterLevel { get; set; }
        public CharacterStatusComponentNumberOfLevelUpPointsApplied? CharacterStatusComponent_NumberOfLevelUpPointsApplied { get; set; }
        public CustomList<string?>? EmoteUnlocks { get; set; }
        public CustomList<long?>? PerMapExplorerNoteUnlocks { get; set; }
        public CustomList<TypeObjectValue?>? PlayerState_EngramBlueprints { get; set; }
        public int? PlayerState_TotalEngramPoints { get; set; }
    }

    public partial class PrimalBuffPersistentData
    {
        public string? ForPrimalBuffClass { get; set; }
        public string? ForPrimalBuffClassString { get; set; }
    }

    public partial class Player
    {
        public double? ActorTransformX { get; set; }
        public double? ActorTransformY { get; set; }
        public double? ActorTransformZ { get; set; }
        public bool? bFirstSpawned { get; set; }
        public Config? Config { get; set; }
        public bool? FoundOnMap { get; set; }
        public double? LastTimeDiedToEnemyTeam { get; set; }
        public double? LoginTime { get; set; }
        public object? MyPersistentBuffDatas { get; set; }
        public double? NumOfDeaths { get; set; }
        public string? PlayerCharacterName { get; set; }
        public int? PlayerDataID { get; set; }
        public string? PlayerName { get; set; }
        public CustomList<PrimalBuffPersistentData?>? PrimalBuffPersistentData { get; set; }
        public string? SavedNetworkAddress { get; set; }
        public int? SavedPlayerDataVersion { get; set; }
        public Stats? Stats { get; set; }
        public int? TribeID { get; set; }
        public string? UniqueID { get; set; }
    }

}
