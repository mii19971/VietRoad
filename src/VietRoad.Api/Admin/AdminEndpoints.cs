using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VietRoad.Api.Models;
using VietRoad.Api.Utils;
using VietRoad.Data;

namespace VietRoad.Api.Admin;

public static class AdminEndpoints
{
    /// <summary>Ten policy phan quyen, dang ky trong Program.cs: chi role "admin".</summary>
    public const string AdminPolicy = "Admin";

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Ca nhom: chua dang nhap -> 401, dang nhap nhung khong phai admin -> 403.
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization(AdminPolicy);

        admin.MapGet("/stats", (AdminService s, CancellationToken ct) => s.GetStatsAsync(ct));

        admin.MapGet("/alerts", ListAlerts);
        admin.MapGet("/alerts/{id:int}", GetAlert);
        admin.MapPost("/alerts", CreateAlert);
        admin.MapPut("/alerts/{id:int}", UpdateAlert);
        admin.MapPost("/alerts/{id:int}/approve", Approve);
        admin.MapPost("/alerts/{id:int}/reject", Reject);

        admin.MapGet("/users", ListUsers);
        admin.MapPut("/users/{id:guid}/role", SetRole);
    }

    // GET /api/v1/admin/alerts?status=pending&source=user&type=speed_camera&q=kim+ma&page=1&pageSize=20
    private static async Task<IResult> ListAlerts(
        AdminService s, CancellationToken ct,
        string? source = null, string? type = null, string? status = null, string? q = null,
        int page = 1, int pageSize = 20)
    {
        // Enum.TryParse(..., ignoreCase: true): "pending" / "Pending" deu duoc.
        var parsedStatus = AlertStatus.All;
        if (status is not null && !Enum.TryParse(status, ignoreCase: true, out parsedStatus))
            return BadRequest("status phai la: all, pending, visible, hidden, inactive, expired");

        return Results.Ok(await s.ListAlertsAsync(source, type, parsedStatus, q, page, pageSize, ct));
    }

    private static async Task<IResult> GetAlert(int id, AdminService s, CancellationToken ct) =>
        await s.GetAlertAsync(id, ct) is { } alert ? Results.Ok(alert) : NotFound();

    private static async Task<IResult> CreateAlert(
        AlertUpsertRequest req, ClaimsPrincipal user, AdminService s, CancellationToken ct)
    {
        if (req.Type is null || req.Lat is null || req.Lng is null)
            return BadRequest("Can type, lat, lng");
        if (Validate(req) is { } error)
            return BadRequest(error);

        var alert = await s.CreateAlertAsync(UserId(user), req, ct);
        return Results.Created($"/api/v1/admin/alerts/{alert.Id}", alert);
    }

    private static async Task<IResult> UpdateAlert(
        int id, AlertUpsertRequest req, AdminService s, CancellationToken ct)
    {
        if (Validate(req) is { } error)
            return BadRequest(error);

        return await s.UpdateAlertAsync(id, req, ct) switch
        {
            AdminResult.Ok => Results.Ok(await s.GetAlertAsync(id, ct)),
            AdminResult.OsmReadOnly => Results.Conflict(new ErrorResponse(
                "Du lieu OSM chi duoc bat/tat. Sua vi tri/ten tren openstreetmap.org, lan nhap sau se cap nhat.")),
            _ => NotFound(),
        };
    }

    private static async Task<IResult> Approve(int id, ClaimsPrincipal user, AdminService s, CancellationToken ct) =>
        await s.ApproveAsync(id, UserId(user), ct) == AdminResult.Ok ? Results.Ok(await s.GetAlertAsync(id, ct)) : NotFound();

    private static async Task<IResult> Reject(int id, AdminService s, CancellationToken ct) =>
        await s.RejectAsync(id, ct) == AdminResult.Ok ? Results.Ok(await s.GetAlertAsync(id, ct)) : NotFound();

    private static async Task<IResult> ListUsers(
        AdminService s, CancellationToken ct, string? q = null, int page = 1, int pageSize = 20) =>
        Results.Ok(await s.ListUsersAsync(q, page, pageSize, ct));

    private static async Task<IResult> SetRole(
        Guid id, RoleChangeRequest req, ClaimsPrincipal user, AdminService s, CancellationToken ct)
    {
        if (req.Role is not (UserRoles.User or UserRoles.Admin))
            return BadRequest("role phai la \"user\" hoac \"admin\"");

        return await s.SetRoleAsync(id, req.Role, UserId(user), ct) switch
        {
            AdminResult.Ok => Results.NoContent(),
            AdminResult.CannotDemoteSelf => BadRequest("Khong the tu bo quyen admin cua chinh minh"),
            _ => NotFound(),
        };
    }

    // Kiem tra cac truong CO GUI LEN (null = khong doi). null = hop le.
    private static string? Validate(AlertUpsertRequest r)
    {
        if (r.Type is not null && !AlertTypes.UserReportable.Contains(r.Type))
            return $"type phai la mot trong: {string.Join(", ", AlertTypes.UserReportable)}";
        if ((r.Lat is not null || r.Lng is not null) && !GeoUtils.IsValidCoord(r.Lat ?? 0, r.Lng ?? 0))
            return "lat/lng khong hop le";
        if (r.Direction is < 0 or >= 360)
            return "direction phai trong [0, 360)";
        if (r.SpeedLimit is < 5 or > 200)
            return "speedLimit phai trong [5, 200]";
        if (r.Name is { Length: > 200 })
            return "name toi da 200 ky tu";
        return null;
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    private static IResult BadRequest(string message) => Results.BadRequest(new ErrorResponse(message));
    private static IResult NotFound() => Results.NotFound(new ErrorResponse("Khong tim thay"));
}
