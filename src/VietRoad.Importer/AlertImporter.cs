using Microsoft.EntityFrameworkCore;
using VietRoad.Data;
using VietRoad.Importer.Osm;

namespace VietRoad.Importer;

public record ImportStats(int Added, int Updated, int Unchanged, int Deactivated);

/// <summary>
/// Ghi du lieu da chuyen doi vao bang alerts theo kieu "upsert":
///  - external_id chua co        -> them moi
///  - external_id da co          -> cap nhat neu co thay doi
///  - co trong DB nhung OSM khong con -> an di (is_active = false), khong xoa
/// Chi dung den cac dong co source = "osm"; du lieu tu nguon khac giu nguyen.
/// </summary>
public class AlertImporter(VietRoadDbContext db)
{

    public async Task<ImportStats> ImportOsmAsync(IReadOnlyList<ImportedAlert> items, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Doc tat ca dong OSM hien co, tra cuu theo external_id.
        var existing = await db.Alerts
            .Where(a => a.Source == AlertSources.Osm)
            .ToDictionaryAsync(a => a.ExternalId!, ct);

        int added = 0, updated = 0, unchanged = 0;

        foreach (var item in items)
        {
            // Remove: lay ra khoi dictionary; cuoi cung con lai la nhung dong OSM da xoa.
            if (existing.Remove(item.ExternalId, out var row))
            {
                // Khong tu bat lai IsActive: neu admin da an camera nay thi ton trong quyet dinh do.
                Apply(item, row);

                // EF so sanh voi gia tri luc doc ra de biet dong co thuc su thay doi khong.
                var entry = db.Entry(row);
                entry.DetectChanges();
                if (entry.State == EntityState.Modified)
                {
                    row.UpdatedAt = now;
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }
            else
            {
                var newRow = new AlertEntity
                {
                    Type = item.Type,
                    Name = item.Name,
                    Source = AlertSources.Osm,
                    ExternalId = item.ExternalId,
                    // Diem goc cho du lieu OSM; tang / giam khi nguoi dung xac nhan / phu nhan.
                    Confidence = AlertScoring.BaseConfidence(AlertSources.Osm),
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                Apply(item, newRow);
                db.Alerts.Add(newRow);
                added++;
            }
        }

        // Nhung dong con lai: OSM khong con doi tuong nay (camera da go / bi xoa).
        var deactivated = 0;
        foreach (var row in existing.Values.Where(r => r.IsActive))
        {
            row.IsActive = false;
            row.UpdatedAt = now;
            deactivated++;
        }

        // Tat ca thay doi duoc ghi trong 1 transaction: hoac thanh cong het, hoac khong gi ca.
        await db.SaveChangesAsync(ct);

        return new ImportStats(added, updated, unchanged, deactivated);
    }

    private static void Apply(ImportedAlert item, AlertEntity row)
    {
        row.Type = item.Type;
        row.Name = item.Name;
        row.Lat = item.Lat;
        row.Lng = item.Lng;
        row.SpeedLimit = item.SpeedLimit;
        row.Direction = item.Direction;
    }
}
