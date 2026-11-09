using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VietRoad.Api.Admin;
using VietRoad.Api.Models;
using VietRoad.Data;

namespace VietRoad.Api.Subscriptions;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/users/me/subscription -> goi hien tai cua chinh minh (app goi sau khi dang nhap).
        app.MapGet("/api/v1/users/me/subscription",
                (ClaimsPrincipal user, SubscriptionService s, CancellationToken ct) => s.GetCurrentAsync(UserId(user), ct))
            .RequireAuthorization();

        // Admin quan ly goi cua nguoi dung.
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization(AdminEndpoints.AdminPolicy);

        // GET  /api/v1/admin/users/{id}/subscriptions          -> goi hien tai + lich su
        admin.MapGet("/users/{id:guid}/subscriptions", async (Guid id, SubscriptionService s, CancellationToken ct) =>
            await s.GetForAdminAsync(id, ct) is { } r ? Results.Ok(r) : NotFound());

        // POST /api/v1/admin/users/{id}/subscriptions  { "plan": "premium", "days": 30, "note": "..." }
        admin.MapPost("/users/{id:guid}/subscriptions", Grant);

        // POST /api/v1/admin/subscriptions/{id}/cancel           -> huy 1 lan kich hoat
        admin.MapPost("/subscriptions/{id:int}/cancel", async (int id, ClaimsPrincipal user, SubscriptionService s, CancellationToken ct) =>
            await s.CancelAsync(id, UserId(user), ct) ? Results.NoContent() : NotFound());
    }

    private static async Task<IResult> Grant(
        Guid id, GrantRequest req, ClaimsPrincipal user, SubscriptionService s, CancellationToken ct)
    {
        if (req.Plan is null || !Plans.Paid.Contains(req.Plan))
            return BadRequest($"plan phai la mot trong: {string.Join(", ", Plans.Paid)}");
        if (req.Days is not { } days || days is < 1 or > SubscriptionService.MaxDays)
            return BadRequest($"days phai trong [1, {SubscriptionService.MaxDays}]");
        if (req.Note is { Length: > 500 })
            return BadRequest("note toi da 500 ky tu");

        var record = await s.GrantAsync(id, req.Plan, days, req.Note, UserId(user), ct);
        return record is null ? NotFound() : Results.Ok(record);
    }

    /// <summary>
    /// Bo loc cho endpoint chi danh cho goi co tinh nang <paramref name="feature"/>:
    /// khong co -> 403 kem thong bao de app hien cho tai xe.
    /// </summary>
    public static RouteGroupBuilder RequireFeature(this RouteGroupBuilder group, string feature, string message)
    {
        group.AddEndpointFilter(async (ctx, next) =>
        {
            var userId = ctx.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var subs = ctx.HttpContext.RequestServices.GetRequiredService<SubscriptionService>();
            if (userId is null || !await subs.HasFeatureAsync(Guid.Parse(userId), feature, ctx.HttpContext.RequestAborted))
                return Results.Json(new ErrorResponse(message), statusCode: StatusCodes.Status403Forbidden);
            return await next(ctx);
        });
        return group;
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    private static IResult BadRequest(string message) => Results.BadRequest(new ErrorResponse(message));
    private static IResult NotFound() => Results.NotFound(new ErrorResponse("Khong tim thay"));
}
