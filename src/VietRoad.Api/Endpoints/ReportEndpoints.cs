using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VietRoad.Api.Models;
using VietRoad.Api.Services;
using VietRoad.Api.Utils;
using VietRoad.Data;

namespace VietRoad.Api.Endpoints;

// App gui len.
public record ReportRequest(string? Type, double? Lat, double? Lng, double? Direction, int? SpeedLimit);
public record VoteRequest(string? Vote); // "confirm" | "deny"

// Tra ve.
public record ReportResponse(Alert Alert, bool Created); // Created = false: da gop vao canh bao co san

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        // Ca nhom bat buoc dang nhap: biet ai bao cao, moi nguoi 1 phieu.
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        // POST /api/v1/reports  { "type": "speed_camera", "lat": 21.03, "lng": 105.85, "direction": 90, "speedLimit": 60 }
        group.MapPost("/reports", Report);

        // POST /api/v1/alerts/123/votes  { "vote": "confirm" }
        // {id:int}: route constraint - chi khop khi id la so nguyen.
        group.MapPost("/alerts/{id:int}/votes", Vote);
    }

    private static async Task<IResult> Report(
        ReportRequest req, ClaimsPrincipal user, ReportService reports, CancellationToken ct)
    {
        if (req.Type is null || !AlertTypes.UserReportable.Contains(req.Type))
            return BadRequest($"type phai la mot trong: {string.Join(", ", AlertTypes.UserReportable)}");
        if (req.Lat is not { } lat || req.Lng is not { } lng || !GeoUtils.IsValidCoord(lat, lng))
            return BadRequest("lat/lng khong hop le");
        if (req.Direction is < 0 or >= 360)
            return BadRequest("direction phai trong [0, 360)");
        if (req.SpeedLimit is < 5 or > 200)
            return BadRequest("speedLimit phai trong [5, 200]");

        var result = await reports.ReportAsync(
            UserId(user), new ReportInput(req.Type, lat, lng, req.Direction, req.SpeedLimit), ct);

        return result.Outcome switch
        {
            ReportOutcome.Created =>
                Results.Created($"/api/v1/alerts/{result.Alert!.Id}", new ReportResponse(Alert.From(result.Alert), true)),
            ReportOutcome.MergedIntoExisting =>
                Results.Ok(new ReportResponse(Alert.From(result.Alert!), false)),
            _ => Results.Json(
                new ErrorResponse($"Moi ngay toi da {ReportService.MaxReportsPerDay} bao cao moi"),
                statusCode: StatusCodes.Status429TooManyRequests),
        };
    }

    private static async Task<IResult> Vote(
        int id, VoteRequest req, ClaimsPrincipal user, ReportService reports, CancellationToken ct)
    {
        bool isConfirm;
        switch (req.Vote)
        {
            case "confirm": isConfirm = true; break;
            case "deny": isConfirm = false; break;
            default: return BadRequest("vote phai la \"confirm\" hoac \"deny\"");
        }

        var alert = await reports.VoteAsync(id, UserId(user), isConfirm, ct);
        return alert is null
            ? Results.NotFound(new ErrorResponse("Khong tim thay canh bao"))
            : Results.Ok(Alert.From(alert));
    }

    // RequireAuthorization dam bao da co token hop le, nen "sub" chac chan co.
    private static Guid UserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static IResult BadRequest(string message) => Results.BadRequest(new ErrorResponse(message));
}
