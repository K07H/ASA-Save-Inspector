namespace ASA_Save_Inspector.ObjectModel
{

    public partial class PlatformProfileID
    {
        public int? unknown { get; set; }
        public string? value { get; set; }
        public string? value_type { get; set; }
    }

    public partial class PlayerPawn
    {
        public double? ActorTransformX { get; set; }
        public double? ActorTransformY { get; set; }
        public double? ActorTransformZ { get; set; }
        public bool? bGaveInitialItems { get; set; }
        public bool? bHatHidden { get; set; }
        public bool? bIsSleeping { get; set; }
        public LinearColor? BodyColors { get; set; }
        public bool? bSavedWhenStasised { get; set; }
        public bool? bSleepingDisableRagdoll { get; set; }
        public bool? bWasBeingDragged { get; set; }
        public TypeStringValue? CharacterSavedDynamicBase { get; set; }
        public string? CharacterSavedDynamicBaseBoneName { get; set; }
        public Vector? CharacterSavedDynamicBaseRelativeLocation { get; set; }
        public Quaternion? CharacterSavedDynamicBaseRelativeRotation { get; set; }
        public string? ClassName { get; set; }
        public TypeStringValue? CurrentWeapon { get; set; }
        public ItemID? CurrentWeaponItemID { get; set; }
        public int? DynamicMaterialBytes { get; set; }
        public int? DynamicOverrideHairDyeBytes { get; set; }
        public int? EyebrowCustomCosmeticModID { get; set; }
        public int? EyebrowIndex { get; set; }
        public int? FacialHairCustomCosmeticModID { get; set; }
        public int? FacialHairIndex { get; set; }
        public int? HeadHairCustomCosmeticModID { get; set; }
        public int? HeadHairIndex { get; set; }
        public TypeStringValue? Instigator { get; set; }
        public string? InventoryUUID { get; set; }
        public string? ItemArchetype { get; set; }
        public double? LastEnterStasisTime { get; set; }
        public double? LastTimeUpdatedCharacterStatusComponent { get; set; }
        public int? LinkedPlayerDataID { get; set; }
        public TypeStringValue? MyCharacterStatusComponent { get; set; }
        public int? NumAscensions { get; set; }
        public int? NumAscensionsAb { get; set; }
        public int? NumAscensionsScorched { get; set; }
        public int? NumChibiLevelUps { get; set; }
        public double? OriginalCreationTime { get; set; }
        public LinearColor? OriginalHairColor { get; set; }
        public TypeStringValue? Owner { get; set; }
        public TypeStringValue? PaintingComponent { get; set; }
        public double? PercentOfFullFacialHairGrowth { get; set; }
        public double? PercentOfFullHeadHairGrowth { get; set; }
        public PlatformProfileID? PlatformProfileID { get; set; }
        public string? PlatformProfileName { get; set; }
        public TypeStringValue? Player_Voice_Collection { get; set; }
        public string? PlayerName { get; set; }
        public double? RawBoneModifiers { get; set; }
        public double? SavedLastTimeHadController { get; set; }
        public CustomList<string?>? SavedSleepAnim { get; set; }
        public TypeStringValue? SeatingStructure { get; set; }
        public int? SeatingStructureSeatNumber { get; set; }
        public int? TargetingTeam { get; set; }
        public string? TribeName { get; set; }
        public string? UUID { get; set; }
    }

}
