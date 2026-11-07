using VietRoad.Api.Models;
using VietRoad.Api.Services;
using VietRoad.Api.Utils;

namespace VietRoad.Api.Endpoints;

public static class AlertEndpoints
{
    private const double MaxRadiusM = 5000;

    /// <summary>Dang ky cac API cua Alert Service vao app.</summary>
    public static void MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        // Nhom chung tien to /api/v1/alerts
        var group = app.MapGroup("/api/v1/alerts");

        // GET /api/v1/alerts/nearby?lat=21.03&lng=105.85&radius=2000
        group.MapGet("/nearby", GetNearby);
    }

    // ASP.NET Core tu dong:
    //  - lay lat, lng, radius tu query string va doi sang double
    //  - lay IAlertService tu DI container (vi khong co trong URL)
    //  - CancellationToken: bao huy neu app ngat ket noi giua chung
    private static async Task<IResult> GetNearby(
        double lat,
        double lng,
        IAlertService alertService,
        CancellationToken ct,
        double radius = 1000)
    {
        if (!GeoUtils.IsValidCoord(lat, lng))
            return Results.BadRequest(new ErrorResponse("lat/lng khong hop le"));

        if (radius is <= 0 or > MaxRadiusM)
            return Results.BadRequest(new ErrorResponse($"radius phai trong (0, {MaxRadiusM}]"));

        var alerts = await alertService.FindNearbyAsync(lat, lng, radius, ct);
        return Results.Ok(new NearbyAlertsResponse(alerts.Count, alerts));
    }
}
