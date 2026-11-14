using Microsoft.EntityFrameworkCore;
using VietRoad.Data;
using VietRoad.Importer.Osm;

namespace VietRoad.Importer;

/// <summary>
/// Ghi gioi han toc do vao bang road_speed_limits: them / cap nhat theo id way,
/// xoa doan OSM khong con (hoac da bo the maxspeed). Bang nay chi chua du lieu OSM nen xoa thang.
/// CHI goi khi da tai DU tat ca loai duong - neu khong se xoa nham duong cua loai tai loi.
/// </summary>
public class SpeedLimitImporter(VietRoadDbContext db)
{
    public async Task<ImportStats> ImportAsync(IReadOnlyList<ImportedRoadSpeed> roads, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.RoadSpeedLimits.ToDictionaryAsync(r => r.Id, ct);
        int added = 0, updated = 0, unchanged = 0;

        foreach (var road in roads.DistinctBy(r => r.Id))
        {
            var points = SpeedLimitMapper.PointsJson(road.Points);
            if (existing.Remove(road.Id, out var row))
            {
                Apply(road, points, row);
                var entry = db.Entry(row);
                entry.DetectChanges();
                if (entry.State == EntityState.Modified) { row.UpdatedAt = now; updated++; }
                else unchanged++;
            }
            else
            {
                row = new RoadSpeedLimitEntity { Id = road.Id, Highway = road.Highway, Points = points, UpdatedAt = now };
                Apply(road, points, row);
                db.RoadSpeedLimits.Add(row);
                added++;
            }
        }

        // Con lai: OSM da xoa duong / bo maxspeed.
        db.RoadSpeedLimits.RemoveRange(existing.Values);
        await db.SaveChangesAsync(ct);
        return new ImportStats(added, updated, unchanged, existing.Count);
    }

    private static void Apply(ImportedRoadSpeed road, string points, RoadSpeedLimitEntity row)
    {
        row.Highway = road.Highway;
        row.Name = Truncate(road.Name, 200);
        row.Ref = Truncate(road.Ref, 64);
        row.MaxSpeed = road.MaxSpeed;
        row.Oneway = road.Oneway;
        row.Points = points;
        row.MinLat = road.Points.Min(p => p.Lat);
        row.MaxLat = road.Points.Max(p => p.Lat);
        row.MinLng = road.Points.Min(p => p.Lng);
        row.MaxLng = road.Points.Max(p => p.Lng);
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
