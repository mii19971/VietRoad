using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VietRoad.Api.Models;
using VietRoad.Api.Services;
using VietRoad.Api.Subscriptions;
using VietRoad.Api.Utils;
using VietRoad.Data;

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
        HttpContext http,
        CancellationToken ct,
        double radius = 1000)
    {
        if (!GeoUtils.IsValidCoord(lat, lng))
            return Results.BadRequest(new ErrorResponse("lat/lng khong hop le"));

        if (radius is <= 0 or > MaxRadiusM)
            return Results.BadRequest(new ErrorResponse($"radius phai trong (0, {MaxRadiusM}]"));

        // API nay khong bat buoc dang nhap. Nhung NEU co gui token ma token sai / het han -> 401,
        // de app biet ma lay token moi (neu khong se lang le bi coi nhu goi Mien phi).
        var user = http.User;
        if (http.Request.Headers.Authorization.Count > 0 && user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var alerts = await alertService.FindNearbyAsync(lat, lng, radius, ct);

        // Chot CSGT chi danh cho goi co tinh nang PoliceAlerts (Premium).
        var userId = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var seesPolice = userId is not null && await http.RequestServices.GetRequiredService<SubscriptionService>()
            .HasFeatureAsync(Guid.Parse(userId), Features.PoliceAlerts, ct);
        if (!seesPolice)
            alerts = alerts.Where(a => a.Alert.Type != AlertTypes.PoliceCheckpoint).ToList();

        return Results.Ok(new NearbyAlertsResponse(alerts.Count, alerts));
    }
}
