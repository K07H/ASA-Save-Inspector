using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ASA_Save_Inspector.ObjectModel
{
    public class ClusterDataEntry
    {
        public string Path { get; set; } = "";
        public string UUID { get; set; } = "";
        public List<ClusterDataDinoUpload> Dinos { get; set; } = new();
        public List<ClusterDataItemUpload> Items { get; set; } = new();
    }

    public class ClusterDataDinoUpload
    {
        public double Version { get; set; }
        public long UploadTime { get; set; }
        public ClusterDataDino Dino { get; set; } = new();
    }

    public class ClusterDataItemUpload
    {
        public double Version { get; set; }
        public long UploadTime { get; set; }
        public string? UUID { get; set; }
        public ClusterDataItem Item { get; set; } = new();
        public string Blueprint { get; set; } = "";
    }

    public class ClusterDataItem
    {
        public string UUID { get; set; } = "";
        public int ItemQuantity { get; set; }
        public ClusterDataItemId ItemID { get; set; } = new();
        public double Version { get; set; }
        public long UploadTime { get; set; }

        public List<ClusterDataCustomItemData>? CustomItemDatas { get; set; }

        public List<string>? ShortName { get; set; }
        public List<string>? ClassName { get; set; }
        public List<string>? ItemArchetype { get; set; }

        [JsonPropertyName("bIsBlueprint")] public bool? IsBlueprint { get; set; }
        [JsonPropertyName("bEquippedItem")] public bool? EquippedItem { get; set; }
        [JsonPropertyName("bIsRated")] public bool? IsRated { get; set; }
        [JsonPropertyName("bIsCrafted")] public bool? IsCrafted { get; set; }

        public int? ItemQualityIndex { get; set; }
        public double? ItemRating { get; set; }
        public double? SavedDurability { get; set; }
        public string? CrafterCharacterName { get; set; }
        public string? CrafterTribeName { get; set; }

        public ClusterDataArkTributeItem? ArkTributeItem { get; set; }

        public double? Durability { get; set; }
        public List<string>? ImplementedStats { get; set; }
        public double? Armor { get; set; }
        public double? Damage { get; set; }
        public double? HyperthermalResistance { get; set; }
        public double? HypothermalResistance { get; set; }

        public ClusterDataDino? Dino { get; set; }
        public ClusterDataItem? Saddle { get; set; }
        public string? CryopodType { get; set; }

        public List<JsonElement>? CustomResourceRequirements { get; set; }
        public string? CustomItemDescription { get; set; }
        public ClusterDataObjectRef? LastOwnerPlayer { get; set; }
        public ClusterDataObjectRef? ItemSkinTemplate { get; set; }
        public ClusterDataObjectRef? CustomCosmeticModSkinReplacementOriginalClass { get; set; }
        public double? CreationTime { get; set; }
        public double? LastAutoDurabilityDecreaseTime { get; set; }
        public double? ItemStatClampsMultiplier { get; set; }
        public double? ItemDurability { get; set; }
        public double? CraftingSkill { get; set; }
        public double? CraftedSkillBonus { get; set; }
        public int? EggNewMutationCount { get; set; }
        public int? ItemVersion { get; set; }

        [JsonPropertyName("bIsEngram")] public bool? IsEngram { get; set; }
        [JsonPropertyName("bForcePreventGrinding")] public bool? ForcePreventGrinding { get; set; }
        [JsonPropertyName("bIsFromClubArk")] public bool? IsFromClubArk { get; set; }
        [JsonPropertyName("bIsEquipped")] public bool? IsEquipped { get; set; }
        [JsonPropertyName("bIsSlot")] public bool? IsSlot { get; set; }
        [JsonPropertyName("bNetInfoFromClient")] public bool? NetInfoFromClient { get; set; }
    }

    public class ClusterDataItemId
    {
        public long ItemID1 { get; set; }
        public long ItemID2 { get; set; }
    }

    public class ClusterDataCustomItemData
    {
        [JsonPropertyName("byte_arrays")] public List<JsonElement> ByteArrays { get; set; } = new();
        [JsonPropertyName("nr_of_arrays")] public int NrOfArrays { get; set; }
        [JsonPropertyName("objects")] public List<JsonElement> Objects { get; set; } = new();
        [JsonPropertyName("painting_id_map")] public JsonElement? PaintingIdMap { get; set; }
        [JsonPropertyName("painting_revision_map")] public JsonElement? PaintingRevisionMap { get; set; }
        [JsonPropertyName("custom_data_soft_classes")] public List<JsonElement> CustomDataSoftClasses { get; set; } = new();
        [JsonPropertyName("skins")] public List<JsonElement> Skins { get; set; } = new();
        [JsonPropertyName("doubles")] public List<double> Doubles { get; set; } = new();
        [JsonPropertyName("strings")] public List<string> Strings { get; set; } = new();
        [JsonPropertyName("floats")] public List<double> Floats { get; set; } = new();
        [JsonPropertyName("classes")] public List<JsonElement> Classes { get; set; } = new();
        [JsonPropertyName("names")] public List<JsonElement> Names { get; set; } = new();
        [JsonPropertyName("custom_data_name")] public string CustomDataName { get; set; } = "";
    }

    public class ClusterDataArkTributeItem
    {
        [JsonPropertyName("properties")] public List<ClusterDataArkProperty> Properties { get; set; } = new();
        [JsonPropertyName("_prop_index")] public JsonElement? PropIndex { get; set; }
        [JsonPropertyName("_nested_containers")] public JsonElement? NestedContainers { get; set; }
    }

    public class ClusterDataArkProperty
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("position")] public int Position { get; set; }
        [JsonPropertyName("unknown_byte")] public JsonElement UnknownByte { get; set; }
        [JsonPropertyName("value")] public JsonElement Value { get; set; }
        [JsonPropertyName("nr_of_bytes")] public int NrOfBytes { get; set; }
        [JsonPropertyName("name_position")] public long NamePosition { get; set; }
        [JsonPropertyName("value_position")] public long ValuePosition { get; set; }
        [JsonPropertyName("bytes")] public string Bytes { get; set; } = "";

        public bool IsNull => Value.ValueKind == JsonValueKind.Null;
        public string? AsString() => Value.ValueKind == JsonValueKind.String ? Value.GetString() : null;
        public long? AsLong() => Value.ValueKind == JsonValueKind.Number && Value.TryGetInt64(out var l) ? l : null;
        public double? AsDouble() => Value.ValueKind == JsonValueKind.Number ? Value.GetDouble() : null;
        public bool? AsBool() => Value.ValueKind is JsonValueKind.True or JsonValueKind.False ? Value.GetBoolean() : null;
        public ClusterDataQuaternion? AsQuaternion() => HasProps("x", "y", "z", "w") ? Value.Deserialize<ClusterDataQuaternion>() : null;
        public ClusterDataVector3? AsVector3() => HasProps("x", "y", "z") && !HasProps("w") ? Value.Deserialize<ClusterDataVector3>() : null;
        public ClusterDataIdPair? AsIdPair() => HasProps("id1", "id2") ? Value.Deserialize<ClusterDataIdPair>() : null;
        public ClusterDataObjectRef? AsObjectRef() => HasProps("type", "value") ? Value.Deserialize<ClusterDataObjectRef>() : null;
        public ClusterDataArkTributeItem? AsStruct() => HasProps("properties") ? Value.Deserialize<ClusterDataArkTributeItem>() : null;

        private bool HasProps(params string[] names)
        {
            if (Value.ValueKind != JsonValueKind.Object) return false;
            foreach (var n in names)
                if (!Value.TryGetProperty(n, out _)) return false;
            return true;
        }
    }

    public class ClusterDataVector3
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("z")] public double Z { get; set; }
    }

    public class ClusterDataQuaternion
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("z")] public double Z { get; set; }
        [JsonPropertyName("w")] public double W { get; set; }
    }

    public class ClusterDataIdPair
    {
        [JsonPropertyName("id1")] public long Id1 { get; set; }
        [JsonPropertyName("id2")] public long Id2 { get; set; }
    }

    public class ClusterDataObjectRef
    {
        [JsonPropertyName("type")] public int Type { get; set; }
        [JsonPropertyName("value")] public JsonElement Value { get; set; }
    }

    public class ClusterDataDino
    {
        public string UUID { get; set; } = "";
        public long DinoID1 { get; set; }
        public long DinoID2 { get; set; }

        [JsonPropertyName("bIsCryopodded")] public bool IsCryopodded { get; set; }
        [JsonPropertyName("bIsFemale")] public bool IsFemale { get; set; }

        public string ShortName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string ItemArchetype { get; set; } = "";

        public double ActorTransformX { get; set; }
        public double ActorTransformY { get; set; }
        public double ActorTransformZ { get; set; }

        public int BaseLevel { get; set; }
        public int CurrentLevel { get; set; }

        public string BaseStatPoints { get; set; } = "";
        public string AddedStatPoints { get; set; } = "";
        public string MutatedStatPoints { get; set; } = "";
        public string StatValues { get; set; } = "";

        public List<string> GeneTraits { get; set; } = new();

        public string ColorSetIndices { get; set; } = "";
        public string ColorSetNames { get; set; } = "";

        public string? TamerString { get; set; }
        public string? TribeName { get; set; }
        public string? TamedName { get; set; }
        public string TamedTimeStamp { get; set; } = "";

        public string UploadedFromServerName { get; set; } = "";
        public string? LatestUploadedFromServerName { get; set; }
        public string TamedOnServerName { get; set; } = "";

        public double UntamedPoopTimeCache { get; set; }
        public double RequiredTameAffinity { get; set; }
        public int TamedAggressionLevel { get; set; }

        [JsonPropertyName("bServerInitializedDino")] public bool ServerInitializedDino { get; set; }
        [JsonPropertyName("bPassiveFlee")] public bool? PassiveFlee { get; set; }

        public ClusterDataTypeValue? MyCharacterStatusComponent { get; set; }
        public ClusterDataTypeValue? UnknownProperty { get; set; } // cryopod

        public long TargetingTeam { get; set; }
        public long? TamingTeamID { get; set; }

        public List<ClusterDataDinoAncestorPair>? DinoAncestors { get; set; }
        public List<ClusterDataDinoAncestorPair>? DinoAncestorsMale { get; set; }

        public string? ImprinterName { get; set; }
        public string? ImprinterPlayerUniqueNetId { get; set; }

        public double? NextAllowedMatingTime { get; set; }
        public double? DinoDownloadedAtTime { get; set; }

        public int? RandomMutationsMale { get; set; }
        public int? RandomMutationsFemale { get; set; }

        public string? PreviousUploadedFromServerName { get; set; }
        public double? CurrentTameAffinity { get; set; }
        public double? TameIneffectivenessModifier { get; set; }
        public double? LastTimeFinishedTraining { get; set; }
        public double? SavedLastConsumeStoredXPCooldown { get; set; }
        public double? StoredXP { get; set; }
        public double? WanderRadiusMultiplier { get; set; }

        [JsonPropertyName("bIgnoreAllyLook")] public bool? IgnoreAllyLook { get; set; }
        [JsonPropertyName("bIsSmallLion")] public bool? IsSmallLion { get; set; }
        [JsonPropertyName("bSlowfallEnabled")] public bool? SlowfallEnabled { get; set; }

        public ClusterDataArkTributeItem? WildRidingStartTransform { get; set; }
    }

    public class ClusterDataTypeValue
    {
        [JsonPropertyName("type")] public int Type { get; set; }
        [JsonPropertyName("value")] public int Value { get; set; }
    }

    public class ClusterDataDinoAncestorPair
    {
        [JsonPropertyName("male")] public ClusterDataDinoAncestor Male { get; set; } = new();
        [JsonPropertyName("female")] public ClusterDataDinoAncestor Female { get; set; } = new();
    }

    public class ClusterDataDinoAncestor
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("id_")] public ClusterDataIdPair Id { get; set; } = new();
    }

    public static class ClusterDataLoader
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static List<ClusterDataEntry> Load(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<List<ClusterDataEntry>>(stream, Options) ?? new List<ClusterDataEntry>();
        }
    }
}
