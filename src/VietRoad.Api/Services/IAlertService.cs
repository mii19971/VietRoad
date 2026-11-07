using VietRoad.Api.Models;

namespace VietRoad.Api.Services;

/// <summary>
/// "Hop dong" cua Alert Service. Code khac chi biet interface nay,
/// nen co the doi giua du lieu mau va PostgreSQL ma khong phai sua cho khac.
/// </summary>
public interface IAlertService
{
    // Task = bat dong bo: trong luc cho database tra loi, thread duoc
    // giai phong de phuc vu request khac.
    Task<IReadOnlyList<NearbyAlert>> FindNearbyAsync(
        double lat, double lng, double radiusM, CancellationToken ct = default);
}
