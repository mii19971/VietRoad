using VietRoad.Api.Models;
using VietRoad.Api.Routing;
using VietRoad.Api.Utils;

namespace VietRoad.Api.Endpoints;

/// <summary>
/// Routing Service: tim dia diem + tinh duong di kem chi dan tieng Viet.
/// Bat buoc dang nhap: dich vu ben ngoai (OSRM/Nominatim) gioi han so request, khong mo cho ai cung goi.
/// </summary>
public static class RoutingEndpoints
{
    public static void MapRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        // GET /api/v1/places/search?q=Hồ Gươm&lat=21.03&lng=105.85
        group.MapGet("/places/search", SearchPlaces);

        // GET /api/v1/routes?fromLat=21.03&fromLng=105.85&toLat=21.02&toLng=105.84
        group.MapGet("/routes", Route);
    }

    private static async Task<IResult> SearchPlaces(
        string? q, double? lat, double? lng, IPlaceSearch places, CancellationToken ct)
    {
        q = q?.Trim();
        if (string.IsNullOrEmpty(q) || q.Length > 200)
            return BadRequest("q phai co tu 1 den 200 ky tu");
        if (lat is { } la && lng is { } ln && !GeoUtils.IsValidCoord(la, ln))
            return BadRequest("lat/lng khong hop le");

        try
        {
            return Results.Ok(new PlacesResponse(await places.SearchAsync(q, lat, lng, ct)));
        }
        catch (RoutingUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    private static async Task<IResult> Route(
        double? fromLat, double? fromLng, double? toLat, double? toLng,
        IRouteProvider router, CancellationToken ct)
    {
        if (fromLat is not { } fLat || fromLng is not { } fLng || !GeoUtils.IsValidCoord(fLat, fLng))
            return BadRequest("fromLat/fromLng khong hop le");
        if (toLat is not { } tLat || toLng is not { } tLng || !GeoUtils.IsValidCoord(tLat, tLng))
            return BadRequest("toLat/toLng khong hop le");
        // VietRoad chi phuc vu Viet Nam. Hay gap khi may ao de vi tri mac dinh (California).
        if (!GeoUtils.IsInVietnam(fLat, fLng) || !GeoUtils.IsInVietnam(tLat, tLng))
            return BadRequest("Chỉ hỗ trợ chỉ đường trong Việt Nam. Vị trí hiện tại hoặc điểm đến đang ở ngoài Việt Nam.");

        try
        {
            var route = await router.RouteAsync(fLat, fLng, tLat, tLng, ct);
            return route is null
                ? Results.NotFound(new ErrorResponse("Không tìm thấy đường đi tới điểm này"))
                : Results.Ok(route);
        }
        catch (RoutingUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    // 502 Bad Gateway: loi nam o dich vu phia sau, khong phai do app gui sai.
    // Chi tiet loi (ex.Message) da ghi log trong client; tai xe chi can 1 cau de hieu.
    private static IResult Unavailable(RoutingUnavailableException ex) =>
        Results.Json(new ErrorResponse("Dịch vụ chỉ đường đang lỗi, vui lòng thử lại sau."),
            statusCode: StatusCodes.Status502BadGateway);

    private static IResult BadRequest(string message) => Results.BadRequest(new ErrorResponse(message));
}
