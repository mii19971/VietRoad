using Microsoft.EntityFrameworkCore;
using VietRoad.Api.Data;
using VietRoad.Api.Models;
using VietRoad.Api.Utils;

namespace VietRoad.Api.Services;

/// <summary>
/// Alert Service doc tu PostgreSQL (schema "vietroad").
/// Tim kiem 2 buoc:
///   1. Database loc so bo trong hinh chu nhat bao quanh (nhanh, dung chi muc).
///   2. C# tinh khoang cach chinh xac tren so it dong con lai, loai diem o goc hinh chu nhat.
/// </summary>
public class PostgresAlertService(VietRoadDbContext db) : IAlertService
{
    public async Task<IReadOnlyList<NearbyAlert>> FindNearbyAsync(
        double lat, double lng, double radiusM, CancellationToken ct = default)
    {
        var box = GeoUtils.BoundingBox(lat, lng, radiusM);

        // Buoc 1 - EF Core dich thanh SQL:
        //   SELECT ... FROM vietroad.alerts
        //   WHERE lat >= @minLat AND lat <= @maxLat AND lng >= @minLng AND lng <= @maxLng
        var candidates = await db.Alerts
            .AsNoTracking() // chi doc, khong can EF theo doi thay doi -> nhanh hon
            .Where(a => a.Lat >= box.MinLat && a.Lat <= box.MaxLat
                     && a.Lng >= box.MinLng && a.Lng <= box.MaxLng)
            .ToListAsync(ct);

        // Buoc 2 - tinh khoang cach that, loc dung ban kinh, sap xep gan -> xa.
        return candidates
            .Select(a => new NearbyAlert(
                new Alert(a.Id, a.Type, a.Lat, a.Lng, a.SpeedLimit, a.Name),
                (int)Math.Round(GeoUtils.DistanceMeters(lat, lng, a.Lat, a.Lng))))
            .Where(x => x.DistanceM <= radiusM)
            .OrderBy(x => x.DistanceM)
            .ToList();
    }
}
