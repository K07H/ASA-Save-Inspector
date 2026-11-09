using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ASA_Save_Inspector.ObjectModel
{
    public static class UiFormat
    {
        public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo(ASILang.Get("MicrosoftCultureFormat"));

        public static string Num(double? value, string format) =>
            value is double d ? d.ToString(format, Culture) : "-";

        public static string Date(long unixSeconds) =>
            DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString(ASILang.Get("DateTimeFormat"), Culture);

        private static readonly Regex CamelSplit = new("(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

        public static string Prettify(string s) => CamelSplit.Replace(s.Replace('_', ' '), " ").Trim();
    }

    public sealed class ClusterOption
    {
        public IReadOnlyList<ClusterDataEntry> Entries { get; }
        public bool IsAll { get; }
        public string Uuid { get; }
        public int ItemCount { get; }
        public int DinoCount { get; }
        //public string Uuid => Entry.UUID;
        //public int ItemCount => Entry.Items.Count;
        //public int DinoCount => Entry.Dinos.Count;
        public int CryopodDinoCount { get; }
        public bool IsEmpty => ItemCount == 0 && DinoCount == 0;
        public string Label { get; }

        public ClusterOption(ClusterDataEntry entry) : this(new[] { entry }, isAll: false) { }

        public static ClusterOption All(IEnumerable<ClusterDataEntry> entries) => new(entries.ToList(), isAll: true);

        private ClusterOption(IReadOnlyList<ClusterDataEntry> entries, bool isAll)
        {
            Entries = entries;
            IsAll = isAll;
            Uuid = isAll ? ASILang.Get("All") : entries[0].UUID;
            ItemCount = entries.Sum(e => e.Items.Count);
            DinoCount = entries.Sum(e => e.Dinos.Count);
            CryopodDinoCount = entries.Sum(e => e.Items.Count(i => i.Item.Dino != null));

            Label = $"{Uuid}  -  " +
                    (isAll ? $"{entries.Count} {ASILang.Get("ClusterDataEntries")}, " : "") +
                    $"{ItemCount} {ASILang.Get("Items").ToLowerInvariant()}, {DinoCount} {ASILang.Get("Dinos").ToLowerInvariant()}" +
                    (CryopodDinoCount > 0 ? $" (+{CryopodDinoCount} {ASILang.Get("InCryopods")})" : "");
        }

        public override string ToString() => Label;
    }

    public sealed class ItemRow
    {
        public string Name { get; init; } = "";
        public string Category { get; init; } = "";
        public int Quantity { get; init; }
        public string Quality { get; init; } = "";
        public int? QualityIndex { get; init; }
        public string Rating { get; init; } = "";
        public double? RatingValue { get; init; }
        public string Durability { get; init; } = "";
        public double? DurabilityValue { get; init; }
        public string Stat { get; init; } = "";
        public double? StatValue { get; init; }
        public string Crafter { get; init; } = "";
        public string Details { get; init; } = "";
        public string Uploaded { get; init; } = "";
        public long UploadedValue { get; init; }
        public string Cluster { get; init; } = "";

        private string? _search;
        public string SearchText => _search ??= string.Join(' ', Name, Category, Quality, Stat, Crafter, Details, Cluster);

        private static readonly string[] QualityNames =
            { ASILang.Get("QualityPrimitive"), ASILang.Get("QualityRamshackle"), ASILang.Get("QualityApprentice"), ASILang.Get("QualityJourneyman"), ASILang.Get("QualityMastercraft"), ASILang.Get("QualityAscendant") };

        private static readonly Dictionary<string, string> CategoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["armor"] = ASILang.Get("Armor"),
            ["weapon"] = ASILang.Get("Weapon"),
            ["saddle"] = ASILang.Get("Saddle"),
            ["skin"] = ASILang.Get("Skin"),
            ["resource"] = ASILang.Get("Resource"),
            ["consumable"] = ASILang.Get("Consumable"),
            ["consumablebuff"] = ASILang.Get("ConsumableBuff"),
            ["ammo"] = ASILang.Get("Ammo"),
            ["costume"] = ASILang.Get("Costume"),
            ["structure"] = ASILang.Get("Structure"),
            ["structureskin"] = ASILang.Get("StructureSkin"),
        };

        private static readonly Regex BlueprintRegex =
            new(@"^PrimalItem(?<cat>[A-Za-z]*)_(?<rest>.+)$", RegexOptions.Compiled);

        public static ItemRow From(ClusterDataItemUpload upload, string clusterUuid)
        {
            var it = upload.Item;
            var (bpCategory, bpName) = ParseBlueprint(upload.Blueprint);

            // Name: ShortName if present, otherwise derived from the blueprint path
            string name = UiFormat.Prettify(it.ShortName?.FirstOrDefault() ?? bpName);

            string category = it.Dino != null ? ASILang.Get("Cryopod")
                : TranslateCategory(it.ClassName?.FirstOrDefault() ?? bpCategory);

            string crafter = it.CrafterCharacterName is { Length: > 0 }
                ? (it.CrafterTribeName is { Length: > 0 } ? $"{it.CrafterCharacterName} ({it.CrafterTribeName})" : it.CrafterCharacterName)
                : "-";

            // Main stat (armor or damage)
            double? statValue = it.Armor ?? it.Damage;
            string stat = it.Armor is double a ? $"{ASILang.Get("Armor")} {UiFormat.Num(a, "F1")}"
                        : it.Damage is double d ? $"{ASILang.Get("Damage")} {UiFormat.Num(d, "F1")}"
                        : "-";

            // Details: cryopod content, saddle, blueprint flag, crafted flag
            var details = new List<string>();
            if (it.Dino != null)
                details.Add($"{it.Dino.ShortName} lvl {it.Dino.CurrentLevel}" +
                            (it.CryopodType is { Length: > 0 } t ? $" ({t})" : ""));
            if (it.Saddle != null)
                details.Add("saddle: " + UiFormat.Prettify(it.Saddle.ShortName?.FirstOrDefault() ?? "?"));
            if (it.IsBlueprint == true) details.Add(ASILang.Get("Blueprint"));
            if (it.IsCrafted == true) details.Add(ASILang.Get("Crafted"));

            double? durability = it.Durability ?? it.ItemDurability;
            int? quality = it.ItemQualityIndex is int q && q >= 0 && q < QualityNames.Length ? q : null;

            return new ItemRow
            {
                Name = name,
                Category = category,
                Quantity = it.ItemQuantity,
                Quality = quality is int qi ? QualityNames[qi] : "-",
                QualityIndex = quality,
                Rating = UiFormat.Num(it.ItemRating, "F2"),
                RatingValue = it.ItemRating,
                Durability = UiFormat.Num(durability, "F1"),
                DurabilityValue = durability,
                Stat = stat,
                StatValue = statValue,
                Crafter = crafter,
                Details = details.Count > 0 ? string.Join(", ", details) : "-",
                Uploaded = UiFormat.Date(upload.UploadTime),
                UploadedValue = upload.UploadTime,
                Cluster = clusterUuid,
            };
        }

        private static (string category, string name) ParseBlueprint(string blueprint)
        {
            string cls = blueprint[(blueprint.LastIndexOf('.') + 1)..];
            if (cls.EndsWith("_C", StringComparison.Ordinal)) cls = cls[..^2];

            var m = BlueprintRegex.Match(cls);
            if (!m.Success) return ("", cls);

            string cat = m.Groups["cat"].Value;
            string rest = m.Groups["rest"].Value;
            if (cat.Length == 0 && (rest.StartsWith("Weapon", StringComparison.Ordinal) || rest.StartsWith(ASILang.Get("Weapon"), StringComparison.Ordinal))) cat = ASILang.Get("Weapon");
            return (cat, rest);
        }

        private static string TranslateCategory(string category) =>
            string.IsNullOrEmpty(category) ? ASILang.Get("Misc")
            : CategoryNames.TryGetValue(category, out var label) ? label
            : UiFormat.Prettify(char.ToUpperInvariant(category[0]) + category[1..]);
    }

    public sealed class DinoRow
    {
        public string Species { get; init; } = "";
        public string Sex { get; init; } = "";
        public int BaseLevel { get; init; }
        public int CurrentLevel { get; init; }
        public string Name { get; init; } = "";
        public string Tribe { get; init; } = "";
        public string Health { get; init; } = "";
        public double? HealthValue { get; init; }
        public string Stamina { get; init; } = "";
        public double? StaminaValue { get; init; }
        public string Weight { get; init; } = "";
        public double? WeightValue { get; init; }
        public string Melee { get; init; } = "";
        public double? MeleeValue { get; init; }
        public int Mutations { get; init; }
        public string Genes { get; init; } = "";
        public string Server { get; init; } = "";
        public string Source { get; init; } = "";
        public string Cluster { get; init; } = "";

        private string? _search;
        public string SearchText => _search ??= string.Join(' ', Species, Name, Tribe, Genes, Server, Source, Cluster);

        private static readonly Regex StatRegex = new(@"(?<k>[a-z_]+)=(?<v>-?\d+(?:\.\d+)?)", RegexOptions.Compiled);

        public static Dictionary<string, int> ParseStatPoints(string? value)
        {
            var result = new Dictionary<string, int>();

            if (string.IsNullOrWhiteSpace(value))
                return result;

            var match = Regex.Match(value, @"\[(.*?)\]");

            if (!match.Success)
                return result;

            foreach (var entry in match.Groups[1].Value.Split(','))
            {
                var parts = entry.Split('=');
                if (parts.Length != 2)
                    continue;

                string key = parts[0].Trim();
                if (int.TryParse(parts[1].Trim(), out int points))
                    result[key] = points;
            }

            return result;
        }

        public static DinoRow From(ClusterDataDino d, string source, string clusterUuid)
        {
            var rawStats = ParseStats(d.StatValues);
            var baseStats = ParseStatPoints(d.BaseStatPoints);
            var mutatedStats = ParseStatPoints(d.MutatedStatPoints);
            var addedStats = ParseStatPoints(d.AddedStatPoints);

            double? health = Get(rawStats, "health");
            double? stamina = Get(rawStats, "stamina");
            double? weight = Get(rawStats, "weight");
            double? melee = Get(rawStats, "melee_damage");

            string FormatStat(string key, double? rawValue, string format)
            {
                int basePoints = baseStats.GetValueOrDefault(key);
                int mutatedPoints = mutatedStats.GetValueOrDefault(key);
                int addedPoints = addedStats.GetValueOrDefault(key);

                return $"{UiFormat.Num(rawValue, format)} ({basePoints}|{mutatedPoints}|{addedPoints})";
            }

            string server = (d.TamedOnServerName ?? "").Trim();

            return new DinoRow
            {
                Species = UiFormat.Prettify(d.ShortName),
                Sex = d.IsFemale ? ASILang.Get("Female") : ASILang.Get("Male"),
                BaseLevel = d.BaseLevel,
                CurrentLevel = d.CurrentLevel,
                Name = d.TamedName is { Length: > 0 } ? d.TamedName : "—",
                Tribe = d.TribeName ?? d.TamerString ?? "—",
                Health = FormatStat("health", health, "F0"),
                HealthValue = health,
                Stamina = FormatStat("stamina", stamina, "F0"),
                StaminaValue = stamina,
                Weight = FormatStat("weight", weight, "F1"),
                WeightValue = weight,
                Melee = FormatStat("melee_damage", melee, "F2"),
                MeleeValue = melee,
                Mutations = (d.RandomMutationsMale ?? 0) + (d.RandomMutationsFemale ?? 0),
                Genes = d.GeneTraits.Count > 0 ? string.Join(", ", d.GeneTraits) : "-",
                Server = server.Length > 0 ? server : "-",
                Source = source,
                Cluster = clusterUuid,
            };
        }

        private static Dictionary<string, double> ParseStats(string? text)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in StatRegex.Matches(text ?? ""))
                if (double.TryParse(m.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    result[m.Groups["k"].Value] = v;
            return result;
        }

        private static double? Get(Dictionary<string, double> stats, string key) => stats.TryGetValue(key, out var v) ? v : null;
    }
}
