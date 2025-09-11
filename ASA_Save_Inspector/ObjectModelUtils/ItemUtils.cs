using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace ASA_Save_Inspector.ObjectModelUtils
{
    public static class ItemUtils
    {
        public static readonly List<string> DefaultSelectedColumns = new List<string>()
        {
            "ShortName",
            "ContainerName",
            "ContainerTribeID",
            "ContainerTribeName",
            "Armor",
            "bEquippedItem",
            "bIsBlueprint",
            "CraftedSkillBonus",
            "CrafterCharacterName",
            "CrafterTribeName",
            "Damage",
            "Durability",
            "HyperthermalResistance",
            "HypothermalResistance",
            "ItemQualityIndex",
            "ItemQuantity",
            "ItemRating",
            "SavedDurability",
            "MapCoords",
        };

        public static List<string> DefaultColumnsOrder = new List<string>()
        {
            "Item",
            "Container name",
            "Container tribe ID",
            "Container tribe name",
            "Map coords",
            "Quantity",
            "Blueprint",
            "Equipped",
            "Durability",
            "Saved durability",
            "Rating",
            "Quality index",
            "Crafter",
            "Crafter tribe",
            "Crafted skill bonus",
            "Armor",
            "Damage",
            "HyperthermalResistance",
            "HypothermalResistance",
        };

        private static readonly Dictionary<string, string> CleanNames = new Dictionary<string, string>()
        {
            { "ShortName", "Item" },
            { "bEquippedItem", "Equipped" },
            { "bIsBlueprint", "Blueprint" },
            { "CraftedSkillBonus", "Crafted skill bonus" },
            { "CrafterCharacterName", "Crafter" },
            { "CrafterTribeName", "Crafter tribe" },
            { "HyperthermalResistance", "Hyperthermal resistance" },
            { "HypothermalResistance", "Hypothermal resistance" },
            { "ItemID", "ID" },
            { "ItemQualityIndex", "Quality index" },
            { "ItemQuantity", "Quantity" },
            { "ItemRating", "Rating" },
            { "SavedDurability", "Saved durability" },
            { "OwnerInventoryUUID", "Owner inv UUID" },
            { "ContainerType", "Container type" },
            { "ContainerName", "Container name" },
            { "ContainerTribeID", "Container tribe ID" },
            { "ContainerTribeName", "Container tribe name" },
            { "MapCoords", "Map coords" },
        };

        public static readonly List<string> DoNotCheckPropertyValuesAmount = new List<string>()
        {
            "Armor",
            "AssociatedDinoID1",
            "AssociatedDinoID2",
            "ContainerName",
            "CreationTime",
            "CreationTimeReadable",
            "Durability",
            "ItemArchetype",
            "ItemID",
            "ItemRating",
            "LastAutoDurabilityDecreaseTime",
            "LastAutoDurabilityDecreaseTimeReadable",
            "LastEnterStasisTime",
            "LastEnterStasisTimeReadable",
            "LastSpoilingTime",
            "LastSpoilingTimeReadable",
            "LastTorchDurabilityLossTime",
            "LastTorchDurabilityLossTimeReadable",
            "LastUseTime",
            "LastUseTimeReadable",
            "Location",
            "MapCoords",
            "NextCraftCompletionTime",
            "NextCraftCompletionTimeReadable",
            "NextSpoilingTime",
            "NextSpoilingTimeReadable",
            "OriginalCreationTime",
            "OriginalCreationTimeReadable",
            "OriginalItemDropLocation",
            "OwnerInventoryUUID",
            "ShortName",
            "UECoords",
            "UUID",
        };

        public static string? GetCleanNameFromPropertyName(string? propertyName) => (propertyName != null && CleanNames.ContainsKey(propertyName) ? CleanNames[propertyName] : propertyName);
        public static string? GetPropertyNameFromCleanName(string? cleanName) => Utils.GetPropertyNameFromCleanName(CleanNames, cleanName);
    }
}

namespace ASA_Save_Inspector.ObjectModel
{
    public partial class Item : INotifyPropertyChanged
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        public string? ShortName
        {
            get => Utils.GetShortNameFromItemArchetype(ItemArchetype);
            private set { }
        }

        public string? GetItemID() => $"{(ItemID?.ItemID1?.ToString(CultureInfo.InvariantCulture) ?? "0")}{(ItemID?.ItemID2?.ToString(CultureInfo.InvariantCulture) ?? "0")}";

        private bool _searchedOwner = false;
        private KeyValuePair<ArkObjectType, object?>? _owner = null;
        public KeyValuePair<ArkObjectType, object?>? Owner()
        {
            if (_owner == null && !_searchedOwner && !string.IsNullOrEmpty(OwnerInventoryUUID))
            {
                _owner = Utils.FindObjectByInventoryUUID(this.OwnerInventoryUUID);
                _searchedOwner = true;
            }
            return _owner;
        }

        public DateTime? CreationTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(CreationTime); }
            private set { }
        }

        public DateTime? LastAutoDurabilityDecreaseTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastAutoDurabilityDecreaseTime); }
            private set { }
        }

        public DateTime? LastEnterStasisTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastEnterStasisTime); }
            private set { }
        }

        public DateTime? LastSpoilingTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastSpoilingTime); }
            private set { }
        }

        public DateTime? LastTorchDurabilityLossTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastTorchDurabilityLossTime); }
            private set { }
        }

        public DateTime? LastUseTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(LastUseTime); }
            private set { }
        }

        public DateTime? NextCraftCompletionTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(NextCraftCompletionTime); }
            private set { }
        }

        public DateTime? NextSpoilingTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(NextSpoilingTime); }
            private set { }
        }

        public DateTime? OriginalCreationTimeReadable
        {
            get { return Utils.GetDateTimeFromGameTime(OriginalCreationTime); }
            private set { }
        }

        public string? ContainerType
        {
            get
            {
                KeyValuePair<ArkObjectType, object?>? owner = Owner();
                if (owner != null && owner.HasValue)
                {
                    if (owner.Value.Key == ArkObjectType.PLAYER_PAWN)
                        return "Player";
                    else if (owner.Value.Key == ArkObjectType.DINO)
                        return "Dino";
                    else if (owner.Value.Key == ArkObjectType.STRUCTURE)
                        return "Structure";
                }
                return "Unknown";
            }
            private set { }
        }

        public string? ContainerName
        {
            get
            {
                KeyValuePair<ArkObjectType, object?>? owner = Owner();
                if (owner != null && owner.HasValue && owner.Value.Value != null)
                {
                    if (owner.Value.Key == ArkObjectType.PLAYER_PAWN)
                    {
                        PlayerPawn? obj = owner.Value.Value as PlayerPawn;
                        if (obj != null)
                            return $"Player Name={(obj.PlayerName ?? string.Empty)}, ID={(obj.LinkedPlayerDataID != null && obj.LinkedPlayerDataID.HasValue ? obj.LinkedPlayerDataID.Value.ToString(CultureInfo.InvariantCulture) : "0")}, UniqueID={(obj.UniqueNetID ?? string.Empty)}";
                    }
                    else if (owner.Value.Key == ArkObjectType.DINO)
                    {
                        Dino? obj = owner.Value.Value as Dino;
                        if (obj != null)
                            return $"Dino Class={(obj.ShortName ?? string.Empty)}, ID={(obj.GetDinoID() ?? string.Empty)}, Lvl={(obj.CurrentLevel != null && obj.CurrentLevel.HasValue ? obj.CurrentLevel.Value.ToString(CultureInfo.InvariantCulture) : "0")}";
                    }
                    else if (owner.Value.Key == ArkObjectType.STRUCTURE)
                    {
                        Structure? obj = owner.Value.Value as Structure;
                        if (obj != null)
                            return $"Structure Class={(obj.ShortName ?? string.Empty)}, ID={(obj.StructureID != null && obj.StructureID.HasValue ? obj.StructureID.Value.ToString(CultureInfo.InvariantCulture) : "0")}{(!string.IsNullOrWhiteSpace(obj.BoxName) ? $", Label={obj.BoxName}" : string.Empty)}";
                    }
                }
                return null;
            }
            private set { }
        }

        public int? ContainerTribeID
        {
            get
            {
                KeyValuePair<ArkObjectType, object?>? owner = Owner();
                if (owner != null && owner.HasValue && owner.Value.Value != null)
                {
                    if (owner.Value.Key == ArkObjectType.PLAYER_PAWN)
                    {
                        PlayerPawn? obj = owner.Value.Value as PlayerPawn;
                        if (obj != null)
                            return obj.TargetingTeam;
                    }
                    else if (owner.Value.Key == ArkObjectType.DINO)
                    {
                        Dino? obj = owner.Value.Value as Dino;
                        if (obj != null)
                            return obj.TargetingTeam;
                    }
                    else if (owner.Value.Key == ArkObjectType.STRUCTURE)
                    {
                        Structure? obj = owner.Value.Value as Structure;
                        if (obj != null)
                            return obj.TargetingTeam;
                    }
                }
                return null;
                //SettingsPage._itemsData.Where(i => string.Compare(this.OwnerInventoryUUID, i.UUID, System.StringComparison.InvariantCulture) == 0);
            }
            private set { }
        }

        public string? ContainerTribeName
        {
            get
            {
                KeyValuePair<ArkObjectType, object?>? owner = Owner();
                if (owner != null && owner.HasValue && owner.Value.Value != null)
                {
                    if (owner.Value.Key == ArkObjectType.PLAYER_PAWN)
                    {
                        PlayerPawn? obj = owner.Value.Value as PlayerPawn;
                        if (obj != null)
                            return obj.TribeName;
                    }
                    else if (owner.Value.Key == ArkObjectType.DINO)
                    {
                        Dino? obj = owner.Value.Value as Dino;
                        if (obj != null)
                            return obj.TribeName;
                    }
                    else if (owner.Value.Key == ArkObjectType.STRUCTURE)
                    {
                        Structure? obj = owner.Value.Value as Structure;
                        if (obj != null)
                            return obj.OwnerName;
                    }
                }
                return null;
                //SettingsPage._itemsData.Where(i => string.Compare(this.OwnerInventoryUUID, i.UUID, System.StringComparison.InvariantCulture) == 0);
            }
            private set { }
        }

        public string? Location
        {
            get
            {
                KeyValuePair<ArkObjectType, object?>? owner = Owner();
                if (owner != null && owner.HasValue && owner.Value.Value != null)
                {
                    if (owner.Value.Key == ArkObjectType.PLAYER_PAWN)
                    {
                        PlayerPawn? obj = owner.Value.Value as PlayerPawn;
                        if (obj != null)
                            return obj.Location;
                    }
                    else if (owner.Value.Key == ArkObjectType.DINO)
                    {
                        Dino? obj = owner.Value.Value as Dino;
                        if (obj != null)
                            return obj.Location;
                    }
                    else if (owner.Value.Key == ArkObjectType.STRUCTURE)
                    {
                        Structure? obj = owner.Value.Value as Structure;
                        if (obj != null)
                            return obj.Location;
                    }
                }
                return null;
            }
            private set { }
        }

        public Tuple<double?, double?, double?>? UECoords
        {
            get
            {
                KeyValuePair<ArkObjectType, object?>? owner = Owner();
                if (owner != null && owner.HasValue && owner.Value.Value != null)
                {
                    if (owner.Value.Key == ArkObjectType.PLAYER_PAWN)
                    {
                        PlayerPawn? obj = owner.Value.Value as PlayerPawn;
                        if (obj != null)
                            return new Tuple<double?, double?, double?>(obj.ActorTransformX, obj.ActorTransformY, obj.ActorTransformZ);
                    }
                    else if (owner.Value.Key == ArkObjectType.DINO)
                    {
                        Dino? obj = owner.Value.Value as Dino;
                        if (obj != null)
                            return new Tuple<double?, double?, double?>(obj.ActorTransformX, obj.ActorTransformY, obj.ActorTransformZ);
                    }
                    else if (owner.Value.Key == ArkObjectType.STRUCTURE)
                    {
                        Structure? obj = owner.Value.Value as Structure;
                        if (obj != null)
                            return new Tuple<double?, double?, double?>(obj.ActorTransformX, obj.ActorTransformY, obj.ActorTransformZ);
                    }
                }
                return null;
            }
            private set { }
        }

        public string? MapCoords
        {
            get
            {
                KeyValuePair<ArkObjectType, object?>? owner = Owner();
                if (owner != null && owner.HasValue && owner.Value.Value != null)
                {
                    if (owner.Value.Key == ArkObjectType.PLAYER_PAWN)
                    {
                        PlayerPawn? obj = owner.Value.Value as PlayerPawn;
                        if (obj != null)
                            return $"{obj.GetGPSCoords().Key.ToString("F1", CultureInfo.InvariantCulture)} {obj.GetGPSCoords().Value.ToString("F1", CultureInfo.InvariantCulture)}";
                    }
                    else if (owner.Value.Key == ArkObjectType.DINO)
                    {
                        Dino? obj = owner.Value.Value as Dino;
                        if (obj != null)
                            return $"{obj.GetGPSCoords().Key.ToString("F1", CultureInfo.InvariantCulture)} {obj.GetGPSCoords().Value.ToString("F1", CultureInfo.InvariantCulture)}";
                    }
                    else if (owner.Value.Key == ArkObjectType.STRUCTURE)
                    {
                        Structure? obj = owner.Value.Value as Structure;
                        if (obj != null)
                            return $"{obj.GetGPSCoords().Key.ToString("F1", CultureInfo.InvariantCulture)} {obj.GetGPSCoords().Value.ToString("F1", CultureInfo.InvariantCulture)}";
                    }
                }
                return null;
            }
            private set { }
        }
    }
}
