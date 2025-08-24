using System.Collections.Generic;

namespace ASA_Save_Inspector.ObjectModel
{

    public partial class Item
    {
        public double? Armor { get; set; }
        public int? AssociatedDinoID1 { get; set; }
        public int? AssociatedDinoID2 { get; set; }
        public TypeStringValue? AssociatedWeapon { get; set; }
        public bool? bAllowEquppingItem { get; set; }
        public bool? bAllowRemovalFromInventory { get; set; }
        public bool? bCanSlot { get; set; }
        public bool? bEquippedItem { get; set; }
        public bool? bForcePreventGrinding { get; set; }
        public bool? bHideFromInventoryDisplay { get; set; }
        public bool? bIsBlueprint { get; set; }
        public bool? bIsClubArkReward { get; set; }
        public bool? bIsInitialItem { get; set; }
        public bool? bNetInfoFromClient { get; set; }
        public bool? bSavedWhenStasised { get; set; }
        public string? ClassName { get; set; }
        public double? CraftedSkillBonus { get; set; }
        public string? CrafterCharacterName { get; set; }
        public string? CrafterTribeName { get; set; }
        public int? CraftQueue { get; set; }
        public double? CreationTime { get; set; }
        public int? CustomCosmeticModSkinReplacementID { get; set; }
        public TypeStringValue? CustomCosmeticModSkinReplacementOriginalClass { get; set; }
        public int? CustomCosmeticModSkinVariantID { get; set; }
        public CustomList<CustomItemData?>? CustomItemDatas { get; set; }
        public string? CustomItemDescription { get; set; }
        public string? CustomItemName { get; set; }
        public double? Damage { get; set; }
        public string? DroppedByName { get; set; }
        public int? DroppedByPlayerID { get; set; }
        public double? Durability { get; set; }
        public int? EggColorSetIndices { get; set; }
        public CustomList<DinoAncestor?>? EggDinoAncestors { get; set; }
        public CustomList<DinoAncestor?>? EggDinoAncestorsMale { get; set; }
        public CustomList<string?>? EggDinoGeneTraits { get; set; }
        public int? EggGenderOverride { get; set; }
        public int? EggNumberMutationsApplied { get; set; }
        public int? EggNumberOfLevelUpPointsApplied { get; set; }
        public int? EggRandomMutationsFemale { get; set; }
        public int? EggRandomMutationsMale { get; set; }
        public double? EggTamedIneffectivenessModifier { get; set; }
        public double? HyperthermalResistance { get; set; }
        public double? HypothermalResistance { get; set; }
        public string? ItemArchetype { get; set; }
        public int? ItemColorID { get; set; }
        public TypeStringValue? ItemCustomClass { get; set; }
        public ItemID? ItemID { get; set; }
        public int? ItemQualityIndex { get; set; }
        public int? ItemQuantity { get; set; }
        public double? ItemRating { get; set; }
        public TypeStringValue? ItemSkinTemplate { get; set; }
        public int? ItemVersion { get; set; }
        public double? LastAutoDurabilityDecreaseTime { get; set; }
        public double? LastEnterStasisTime { get; set; }
        public double? LastSpoilingTime { get; set; }
        public double? LastTorchDurabilityLossTime { get; set; }
        public double? LastUseTime { get; set; }
        public TypeStringValue? MyItem { get; set; }
        public string? MySkillName { get; set; }
        public double? NextCraftCompletionTime { get; set; }
        public double? NextSpoilingTime { get; set; }
        public double? OriginalCreationTime { get; set; }
        public Vector? OriginalItemDropLocation { get; set; }
        public string? OwnerInventoryUUID { get; set; }
        public int? PreSkinItemColorID { get; set; }
        public double? SavedDurability { get; set; }
        public int? SlotIndex { get; set; }
        public int? TargetingTeam { get; set; }
        public int? TempSlotIndex { get; set; }
        public string? UUID { get; set; }
        public int? WeaponClipAmmo { get; set; }
    }

}
