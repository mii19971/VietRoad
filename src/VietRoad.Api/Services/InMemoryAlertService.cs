using VietRoad.Api.Models;
using VietRoad.Api.Utils;

namespace VietRoad.Api.Services;

/// <summary>
/// Alert Service dung du lieu mau trong bo nho.
/// Dung khi chua cau hinh database (vd: chay test, may chua co Docker).
/// </summary>
public class InMemoryAlertService : IAlertService
{
    private static readonly Alert[] Alerts =
    [
        // Du lieu GIA, chi de chay thu / test. Du lieu that nam trong database (nhap tu OSM).
        new(1, "speed_camera",     21.0285, 105.8542, 50,   "Camera Ho Hoan Kiem",  90,   "sample"),
        new(2, "speed_camera",     21.0368, 105.8345, 60,   "Camera Kim Ma",        null, "sample"),
        new(3, "red_light_camera", 21.0245, 105.8412, null, "Den do Tran Hung Dao", null, "sample"),
        new(4, "speed_camera",     10.7769, 106.7009, 50,   "Camera Q1 TP.HCM",     180,  "sample"),
    ];

    public Task<IReadOnlyList<NearbyAlert>> FindNearbyAsync(
        double lat, double lng, double radiusM, CancellationToken ct = default)
    {
        IReadOnlyList<NearbyAlert> result = Alerts
            .Select(a => new NearbyAlert(
                a,
                (int)Math.Round(GeoUtils.DistanceMeters(lat, lng, a.Lat, a.Lng))))
            .Where(x => x.DistanceM <= radiusM)
            .OrderBy(x => x.DistanceM)
            .ToList();

        // Khong co gi phai cho, nen tra ve Task da hoan thanh ngay.
        return Task.FromResult(result);
    }
}
