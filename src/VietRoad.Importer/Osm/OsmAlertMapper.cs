using System.Globalization;
using System.Text.RegularExpressions;
using VietRoad.Data;

namespace VietRoad.Importer.Osm;

public record MapResult(IReadOnlyList<ImportedAlert> Alerts, int Skipped);

/// <summary>
/// Chuyen du lieu OSM sang ImportedAlert. Chi xu ly du lieu, khong goi mang hay database,
/// nen de viet unit test.
/// </summary>
public static partial class OsmAlertMapper
{
    public static MapResult Map(OsmResponse response)
    {
        // Tra cuu toa do node theo id. Mot node co the xuat hien 2 lan
        // (1 lan kem tags, 1 lan chi co toa do), uu tien ban co tags.
        var nodes = new Dictionary<long, OsmElement>();
        foreach (var e in response.Elements.Where(e => e.Type == "node" && e.Lat is not null && e.Lon is not null))
        {
            if (!nodes.TryGetValue(e.Id, out var existing) || existing.Tags is null)
                nodes[e.Id] = e;
        }

        // 1. Cac node duoc gan the highway=speed_camera.
        var byNodeId = new Dictionary<long, ImportedAlert>();
        foreach (var n in nodes.Values.Where(n => Tag(n, "highway") == "speed_camera"))
        {
            byNodeId[n.Id] = new ImportedAlert(
                ExternalId: $"osm:node/{n.Id}",
                Type: AlertTypes.SpeedCamera,
                Name: NameOf(n.Tags, AlertTypes.SpeedCamera),
                Lat: n.Lat!.Value,
                Lng: n.Lon!.Value,
                SpeedLimit: ParseMaxSpeed(Tag(n, "maxspeed")),
                Direction: ParseDirection(Tag(n, "direction") ?? Tag(n, "camera:direction")));
        }

        // 2. Cac relation type=enforcement (vung kiem soat).
        var fromRelations = new List<ImportedAlert>();
        var skipped = 0;
        foreach (var r in response.Elements.Where(e => e.Type == "relation" && Tag(e, "type") == "enforcement"))
        {
            var type = MapEnforcementType(Tag(r, "enforcement"));

            // Vi tri: uu tien node "device" (chinh la camera), khong co thi lay diem bat dau "from".
            var member = r.Members?.FirstOrDefault(m => m.Type == "node" && m.Role == "device")
                      ?? r.Members?.FirstOrDefault(m => m.Type == "node" && m.Role == "from");

            if (type is null || member is null || !nodes.TryGetValue(member.Ref, out var node))
            {
                skipped++; // loai khong ho tro (vd kiem tra trong tai) hoac thieu vi tri
                continue;
            }

            var speedLimit = ParseMaxSpeed(Tag(r, "maxspeed"));
            var direction = ParseDirection(Tag(r, "direction"));

            // Camera nay da co o buoc 1 -> gop thong tin, khong tao dong trung.
            if (byNodeId.TryGetValue(node.Id, out var camera))
            {
                byNodeId[node.Id] = camera with
                {
                    Type = type,
                    SpeedLimit = camera.SpeedLimit ?? speedLimit,
                    Direction = camera.Direction ?? direction,
                };
                continue;
            }

            fromRelations.Add(new ImportedAlert(
                ExternalId: $"osm:relation/{r.Id}",
                Type: type,
                Name: NameOf(r.Tags, type),
                Lat: node.Lat!.Value,
                Lng: node.Lon!.Value,
                SpeedLimit: speedLimit,
                Direction: direction));
        }

        return new MapResult([.. byNodeId.Values, .. fromRelations], skipped);
    }

    private static string? Tag(OsmElement e, string key) =>
        e.Tags is not null && e.Tags.TryGetValue(key, out var v) ? v : null;

    // Gia tri the "enforcement" tren OSM -> loai canh bao cua VietRoad. null = khong ho tro.
    private static string? MapEnforcementType(string? enforcement) => enforcement switch
    {
        "maxspeed" => AlertTypes.SpeedCamera,
        "traffic_signals" => AlertTypes.RedLightCamera,
        "average_speed" => AlertTypes.AverageSpeedCamera,
        _ => null,
    };

    private static string NameOf(Dictionary<string, string>? tags, string type)
    {
        if (tags is not null && tags.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            return name;

        return type switch
        {
            AlertTypes.RedLightCamera => "Camera đèn đỏ",
            AlertTypes.AverageSpeedCamera => "Camera tốc độ trung bình",
            _ => "Camera tốc độ",
        };
    }

    /// <summary>"50", "50 km/h" -> 50. "30 mph", "VN:urban", "" -> null.</summary>
    public static int? ParseMaxSpeed(string? value)
    {
        if (value is null || value.Contains("mph", StringComparison.OrdinalIgnoreCase))
            return null;

        var match = LeadingNumber().Match(value);
        return match.Success && int.TryParse(match.Value, out var n) && n is > 0 and <= 200 ? n : null;
    }

    private static readonly Dictionary<string, double> Cardinal = new(StringComparer.OrdinalIgnoreCase)
    {
        ["N"] = 0, ["NNE"] = 22.5, ["NE"] = 45, ["ENE"] = 67.5,
        ["E"] = 90, ["ESE"] = 112.5, ["SE"] = 135, ["SSE"] = 157.5,
        ["S"] = 180, ["SSW"] = 202.5, ["SW"] = 225, ["WSW"] = 247.5,
        ["W"] = 270, ["WNW"] = 292.5, ["NW"] = 315, ["NNW"] = 337.5,
    };

    /// <summary>"90" -> 90, "NE" -> 45, "forward" / "45-135" / null -> null.</summary>
    public static double? ParseDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // InvariantCulture: luon doc dau "." la thap phan, khong phu thuoc cai dat may.
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees))
            return ((degrees % 360) + 360) % 360; // dua ve khoang [0, 360)

        return Cardinal.TryGetValue(value.Trim(), out var d) ? d : null;
    }

    // Source generator tao san Regex luc bien dich -> nhanh hon tao Regex luc chay.
    [GeneratedRegex(@"^\s*\d+")]
    private static partial Regex LeadingNumber();
}
