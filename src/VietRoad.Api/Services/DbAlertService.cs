using Microsoft.EntityFrameworkCore;
using VietRoad.Api.Models;
using VietRoad.Api.Utils;
using VietRoad.Data;

namespace VietRoad.Api.Services;

/// <summary>
/// Alert Service doc tu database (PostgreSQL khi chay that, EF InMemory khi test).
/// Tim kiem 2 buoc:
///   1. Database loc so bo trong hinh chu nhat bao quanh (nhanh, dung chi muc).
///   2. C# tinh khoang cach chinh xac tren so it dong con lai, loai diem o goc hinh chu nhat.
/// </summary>
public class DbAlertService(VietRoadDbContext db, TimeProvider time) : IAlertService
{
    public async Task<IReadOnlyList<NearbyAlert>> FindNearbyAsync(
        double lat, double lng, double radiusM, CancellationToken ct = default)
    {
        var box = GeoUtils.BoundingBox(lat, lng, radiusM);

        var candidates = await db.Alerts
            .AsNoTracking() // chi doc, khong can EF theo doi thay doi -> nhanh hon
            .Where(a => a.Lat >= box.MinLat && a.Lat <= box.MaxLat
                     && a.Lng >= box.MinLng && a.Lng <= box.MaxLng)
            .Where(VisibleAt(time.GetUtcNow()))
            .ToListAsync(ct);

        // Buoc 2 - tinh khoang cach that, loc dung ban kinh, sap xep gan -> xa.
        return candidates
            .Select(a => new NearbyAlert(
                Alert.From(a),
                (int)Math.Round(GeoUtils.DistanceMeters(lat, lng, a.Lat, a.Lng))))
            .Where(x => x.DistanceM <= radiusM)
            .OrderBy(x => x.DistanceM)
            .ToList();
    }

    /// <summary>
    /// Dieu kien de 1 canh bao duoc hien cho tai xe. Dung chung cho tim kiem va bao cao trung.
    /// Tra ve Expression (khong phai Func) de EF Core dich duoc sang SQL:
    ///   is_active AND (expires_at IS NULL OR expires_at > @now) AND confidence >= 20
    /// </summary>
    public static System.Linq.Expressions.Expression<Func<AlertEntity, bool>> VisibleAt(DateTimeOffset now) =>
        a => a.IsActive
          && (a.ExpiresAt == null || a.ExpiresAt > now)
          && a.Confidence >= AlertScoring.MinVisible;
}
