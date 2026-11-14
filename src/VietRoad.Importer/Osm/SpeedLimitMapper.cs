using System.Globalization;
using System.Text;

namespace VietRoad.Importer.Osm;

/// <summary>1 doan duong co gioi han toc do, san sang ghi vao bang road_speed_limits.</summary>
public record ImportedRoadSpeed(
    long Id, string Highway, string? Name, string? Ref, int MaxSpeed, bool Oneway,
    IReadOnlyList<(double Lat, double Lng)> Points);

/// <summary>Chuyen way OSM (co maxspeed + toa do) sang ImportedRoadSpeed. Ham thuan -> unit test.</summary>
public static class SpeedLimitMapper
{
    public static (List<ImportedRoadSpeed> Roads, int Skipped) Map(OsmResponse response)
    {
        var roads = new List<ImportedRoadSpeed>();
        var skipped = 0;
        foreach (var w in response.Elements.Where(e => e.Type == "way"))
        {
            var tags = w.Tags ?? [];
            var maxSpeed = OsmAlertMapper.ParseMaxSpeed(tags.GetValueOrDefault("maxspeed"));
            var points = w.Geometry?.Select(p => (Round(p.Lat), Round(p.Lon))).ToList();
            if (maxSpeed is null || points is not { Count: >= 2 } || !tags.TryGetValue("highway", out var highway))
            {
                skipped++; // vd maxspeed="VN:urban", "30 mph", hoac thieu toa do
                continue;
            }

            // Mot chieu: oneway=yes/true/1. Cao toc (motorway) mac dinh 1 chieu tren OSM tru khi oneway=no.
            // oneway=-1: 1 chieu NGUOC voi thu tu diem -> dao lai de Points luon theo chieu xe chay.
            var onewayTag = tags.GetValueOrDefault("oneway");
            var reversed = onewayTag == "-1";
            var oneway = reversed || onewayTag is "yes" or "true" or "1" ||
                         (highway is "motorway" or "motorway_link" && onewayTag != "no");
            if (reversed) points.Reverse();

            roads.Add(new ImportedRoadSpeed(w.Id, highway, tags.GetValueOrDefault("name"), tags.GetValueOrDefault("ref"),
                maxSpeed.Value, oneway, points));
        }
        return (roads, skipped);
    }

    // 5 chu so thap phan ~ 1 m: du chinh xac, JSON gon hon.
    private static double Round(double v) => Math.Round(v, 5);

    /// <summary>[(21.0285, 105.8542), ...] -> "[[21.0285,105.8542],...]" (luu vao cot points).</summary>
    public static string PointsJson(IEnumerable<(double Lat, double Lng)> points)
    {
        var sb = new StringBuilder("[");
        foreach (var (lat, lng) in points)
        {
            if (sb.Length > 1) sb.Append(',');
            sb.Append('[').Append(lat.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(lng.ToString(CultureInfo.InvariantCulture)).Append(']');
        }
        return sb.Append(']').ToString();
    }
}
