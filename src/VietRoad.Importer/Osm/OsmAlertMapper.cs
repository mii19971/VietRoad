using System.Globalization;
using System.Text.RegularExpressions;
using VietRoad.Data;

namespace VietRoad.Importer.Osm;

/// <param name="Merged">So diem bi gop vao diem khac (trung lap: nhieu lan xe cua 1 tram thu phi, camera ve 2 lan...).</param>
public record MapResult(IReadOnlyList<ImportedAlert> Alerts, int Skipped, int Merged = 0);

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

        // 1b. Camera giam sat giao thong (phat nguoi, doc bien so...) chua duoc gan la camera toc do.
        foreach (var n in nodes.Values.Where(n => IsTrafficSurveillance(n) && !byNodeId.ContainsKey(n.Id)))
        {
            byNodeId[n.Id] = new ImportedAlert(
                ExternalId: $"osm:node/{n.Id}",
                Type: AlertTypes.TrafficCamera,
                // Khong dung the "name": voi camera giam sat no thuong la ten noi dat (vd "School").
                Name: AlertTypes.DefaultName(AlertTypes.TrafficCamera),
                Lat: n.Lat!.Value,
                Lng: n.Lon!.Value,
                SpeedLimit: null,
                Direction: ParseDirection(Tag(n, "camera:direction") ?? Tag(n, "direction")));
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

        // 3. Tram thu phi: dang diem (node) va dang vung / duong ke (way, lay diem giua).
        var tolls = new List<ImportedAlert>();
        foreach (var n in nodes.Values.Where(n => Tag(n, "barrier") == "toll_booth"))
            tolls.Add(Toll($"osm:node/{n.Id}", n.Tags, n.Lat!.Value, n.Lon!.Value));
        foreach (var w in response.Elements.Where(e => e.Type == "way" && e.Center is not null && Tag(e, "barrier") == "toll_booth"))
            tolls.Add(Toll($"osm:way/{w.Id}", w.Tags, w.Center!.Lat, w.Center.Lon));

        // 4. Gop diem trung.
        //  - Camera cung loai, cung huong, cach nhau < 30 m: thuong la 1 camera bi ve 2 lan.
        //  - Tram thu phi < 150 m: OSM hay ve MOI LAN XE 1 diem -> 1 tram co the 5-10 diem,
        //    khong gop thi app doc "Trạm thu phí" lien tuc.
        var cameras = Deduplicate([.. byNodeId.Values, .. fromRelations], CameraMergeRadiusM, checkDirection: true);
        var tollStations = Deduplicate(tolls, TollMergeRadiusM, checkDirection: false);
        var merged = byNodeId.Count + fromRelations.Count - cameras.Count + tolls.Count - tollStations.Count;

        return new MapResult([.. cameras, .. tollStations], skipped, merged);
    }

    public const double CameraMergeRadiusM = 30;
    public const double TollMergeRadiusM = 150;

    private static ImportedAlert Toll(string externalId, Dictionary<string, string>? tags, double lat, double lng) =>
        new(externalId, AlertTypes.TollBooth, TollName(tags), lat, lng, SpeedLimit: null, Direction: null);

    /// <summary>
    /// Ten tram thu phi tren OSM nhieu khi chi la ten nut giao / cong trinh ("IC11", "Hầm Cù Mông")
    /// -> them tien to cho de hieu: "Trạm thu phí IC11". Ten da co "thu phí" thi giu nguyen.
    /// </summary>
    public static string TollName(Dictionary<string, string>? tags)
    {
        var name = NameOf(tags, AlertTypes.TollBooth);
        return name.Contains("thu phí", StringComparison.OrdinalIgnoreCase) ? name : $"Trạm thu phí {name}";
    }

    private static bool IsTrafficSurveillance(OsmElement n) =>
        Tag(n, "man_made") == "surveillance" &&
        (Tag(n, "surveillance:zone") == "traffic" || Tag(n, "surveillance") == "traffic" || Tag(n, "surveillance:type") == "ALPR");

    /// <summary>
    /// Gop cac diem cung loai nam gan nhau. Diem "dai dien" duoc chon theo thu tu co dinh
    /// (nhieu thong tin nhat, roi theo ExternalId) -> chay import nhieu lan van ra cung ExternalId,
    /// khong tao / an di lung tung. Diem bi gop chi bo sung thong tin con thieu cho diem dai dien.
    /// </summary>
    public static List<ImportedAlert> Deduplicate(IEnumerable<ImportedAlert> items, double radiusM, bool checkDirection)
    {
        var ordered = items
            .OrderByDescending(a => a.Name != AlertTypes.DefaultName(a.Type))   // co ten rieng
            .ThenByDescending(a => a.SpeedLimit is not null)
            .ThenByDescending(a => a.Direction is not null)
            .ThenBy(a => a.ExternalId, StringComparer.Ordinal);

        var kept = new List<ImportedAlert>();
        foreach (var a in ordered)
        {
            var i = kept.FindIndex(k =>
                k.Type == a.Type &&
                DistanceM(k.Lat, k.Lng, a.Lat, a.Lng) < radiusM &&
                (!checkDirection || k.Direction is null || a.Direction is null || AngleDiff(k.Direction.Value, a.Direction.Value) <= 45));

            if (i < 0)
                kept.Add(a);
            else
                kept[i] = kept[i] with
                {
                    SpeedLimit = kept[i].SpeedLimit ?? a.SpeedLimit,
                    Direction = kept[i].Direction ?? a.Direction,
                };
        }
        return kept;
    }

    // Khoang cach gan dung (met) - du chinh xac cho vai tram met.
    private static double DistanceM(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = (lat2 - lat1) * 110_574;
        var dLng = (lng2 - lng1) * 111_320 * Math.Cos(lat1 * Math.PI / 180);
        return Math.Sqrt(dLat * dLat + dLng * dLng);
    }

    private static double AngleDiff(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
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

        return AlertTypes.DefaultName(type);
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
