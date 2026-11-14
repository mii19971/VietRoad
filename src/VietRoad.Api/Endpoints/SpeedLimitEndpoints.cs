using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VietRoad.Api.Models;
using VietRoad.Api.Utils;
using VietRoad.Data;

namespace VietRoad.Api.Endpoints;

/// <summary>1 doan duong co gioi han toc do. Points = [[lat, lng], ...] theo chieu xe chay neu Oneway.</summary>
public record RoadSpeedSegment(long Id, int MaxSpeed, bool Oneway, string? Name, string? Ref, double[][] Points);

public record SpeedLimitsResponse(int Count, IReadOnlyList<RoadSpeedSegment> Segments);

/// <summary>
/// Gioi han toc do theo doan duong (du lieu maxspeed cua OpenStreetMap).
/// App tai 1 lan cho vung quanh xe, roi tu tim xe dang o doan nao -> khong can goi API moi giay.
/// Khong bat buoc dang nhap, moi goi deu dung: day la tinh nang an toan.
/// </summary>
public static class SpeedLimitEndpoints
{
    public const double MaxRadiusM = 3000;

    public static void MapSpeedLimitEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/speed-limits/nearby?lat=21.03&lng=105.85&radius=2000
        app.MapGet("/api/v1/speed-limits/nearby", GetNearby);
    }

    private static async Task<IResult> GetNearby(
        double lat, double lng, VietRoadDbContext db, CancellationToken ct, double radius = 2000)
    {
        if (!GeoUtils.IsValidCoord(lat, lng))
            return Results.BadRequest(new ErrorResponse("lat/lng khong hop le"));
        if (radius is <= 0 or > MaxRadiusM)
            return Results.BadRequest(new ErrorResponse($"radius phai trong (0, {MaxRadiusM}]"));

        // Doan duong co khung bao CAT khung tim kiem (khong can nam tron trong).
        var box = GeoUtils.BoundingBox(lat, lng, radius);
        var rows = await db.RoadSpeedLimits.AsNoTracking()
            .Where(r => r.MaxLat >= box.MinLat && r.MinLat <= box.MaxLat &&
                        r.MaxLng >= box.MinLng && r.MinLng <= box.MaxLng)
            .ToListAsync(ct);

        var segments = rows
            .Select(r => new RoadSpeedSegment(r.Id, r.MaxSpeed, r.Oneway, r.Name, r.Ref,
                JsonSerializer.Deserialize<double[][]>(r.Points) ?? []))
            .ToList();
        return Results.Ok(new SpeedLimitsResponse(segments.Count, segments));
    }
}
