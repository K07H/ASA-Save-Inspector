using System.Globalization;

namespace ASA_Save_Inspector.ObjectModel
{
    public class TypeStringValue
    {
        public string? type { get; set; }
        public string? value { get; set; }

        public override string ToString() => (value ?? string.Empty);
    }

    public class TypeIntValue
    {
        public string? type { get; set; }
        public int? value { get; set; }

        public override string ToString() => (value != null && value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
    }

    public class NameStringValue
    {
        public string? name { get; set; }
        public string? value { get; set; }

        public override string ToString() => (value ?? string.Empty);
    }

    public class TypeObjectValue
    {
        public string? type { get; set; }
        public object? value { get; set; }

        public override string ToString() => (value?.ToString() ?? string.Empty);
    }

    public class Vector
    {
        public double? x { get; set; }
        public double? y { get; set; }
        public double? z { get; set; }

        public override string ToString() => $"x={(x != null && x.HasValue ? x.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} y={(y != null && y.HasValue ? y.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} z={(z != null && z.HasValue ? z.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public class Quaternion
    {
        public double? x { get; set; }
        public double? y { get; set; }
        public double? z { get; set; }
        public double? w { get; set; }

        public override string ToString() => $"x={(x != null && x.HasValue ? x.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} y={(y != null && y.HasValue ? y.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} z={(z != null && z.HasValue ? z.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} w={(w != null && w.HasValue ? w.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public class Rotator
    {
        public double? pitch { get; set; }
        public double? yaw { get; set; }
        public double? roll { get; set; }

        public override string ToString() => $"pitch={(pitch != null && pitch.HasValue ? pitch.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} yaw={(yaw != null && yaw.HasValue ? yaw.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} roll={(roll != null && roll.HasValue ? roll.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public class LinearColor
    {
        public double? r { get; set; }
        public double? g { get; set; }
        public double? b { get; set; }
        public double? a { get; set; }

        public override string ToString() => $"r={(r != null && r.HasValue ? r.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} g={(g != null && g.HasValue ? g.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} b={(b != null && b.HasValue ? b.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)} a={(a != null && a.HasValue ? a.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public class ItemID
    {
        public int? ItemID1 { get; set; }
        public int? ItemID2 { get; set; }

        public override string ToString() => $"id1={(ItemID1 != null && ItemID1.HasValue ? ItemID1.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)} id2={(ItemID2 != null && ItemID2.HasValue ? ItemID2.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}";
    }

    public class Property
    {
        public string? name { get; set; }
        public string? type { get; set; }
        public string? value { get; set; }

        public override string ToString() => $"name={(name ?? string.Empty)} type={(type ?? string.Empty)} value={(value ?? string.Empty)}";
    }

    public class Properties
    {
        public CustomList<Property>? properties { get; set; }

        public override string ToString() => (properties != null && properties.Count > 0 ? string.Join(", ", properties) : string.Empty);
    }

    public class BytesArray
    {
        public int? size { get; set; }
        public string? data { get; set; }

        public override string ToString() => $"size={(size != null && size.HasValue ? size.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)} type={(data ?? string.Empty)}";
    }

    public class CustomItemData
    {
        public CustomList<BytesArray>? byte_arrays { get; set; }
        public CustomList<double?>? doubles { get; set; }
        public CustomList<float?>? floats { get; set; }
        public CustomList<string>? strings { get; set; }
        public CustomList<TypeStringValue>? classes { get; set; }
        public CustomList<TypeStringValue>? objects { get; set; }
        public object? painting_id_map { get; set; }
        public object? painting_revision_map { get; set; }
        public string? custom_data_name { get; set; }
        public CustomList<string>? custom_data_soft_classes { get; set; }

        public override string ToString() => $"{custom_data_name ?? string.Empty} [{strings}] [{floats}] [{doubles}]";
    }

    public class DinoID
    {
        public string? name { get; set; }
        public int? id1 { get; set; }
        public int? id2 { get; set; }

        public override string ToString() => $"{(id1 != null && id1.HasValue ? id1.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)} {(id2 != null && id2.HasValue ? id2.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)} {name ?? string.Empty}";
    }

    public class DinoAncestor
    {
        public DinoID? male { get; set; }
        public DinoID? female { get; set; }

        public override string ToString() => $"male={(male != null ? male.ToString() : string.Empty)} female={(female != null ? female.ToString() : string.Empty)}";
    }
}
