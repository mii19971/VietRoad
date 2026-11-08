using System.Globalization;
using System.Net;
using System.Text.Json.Serialization;

namespace VietRoad.Api.Routing;

/// <summary>Tinh duong di giua 2 diem. Interface -> test thay bang ban gia, khong goi mang.</summary>
public interface IRouteProvider
{
    /// <returns>null = khong co duong (vd: diem den giua bien).</returns>
    Task<RouteResponse?> RouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct);
}

/// <summary>Loi tu dich vu ben ngoai (mat mang, may chu loi...). Endpoint doi thanh HTTP 502.</summary>
public class RoutingUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Goi OSRM: GET route/v1/driving/{lng},{lat};{lng},{lat}?steps=true
/// Chu y: OSRM dung thu tu (lng, lat) - nguoc voi thoi quen (lat, lng).
/// </summary>
public class OsrmRouteProvider(HttpClient http, ILogger<OsrmRouteProvider> logger) : IRouteProvider
{
    public async Task<RouteResponse?> RouteAsync(
        double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        // InvariantCulture: luon dung dau "." cho so thap phan (may cai tieng Viet dung ",").
        var coords = string.Format(CultureInfo.InvariantCulture, "{0},{1};{2},{3}", fromLng, fromLat, toLng, toLat);
        // overview=false: khong can duong tong; ta tu ghep tu duong cua tung buoc (biet chinh xac diem re o dau).
        var url = $"route/v1/driving/{coords}?steps=true&overview=false&geometries=polyline";

        OsrmResponse? body;
        try
        {
            using var res = await http.GetAsync(url, ct);
            // OSRM tra 400 + code "NoRoute"/"NoSegment" khi khong co duong: van doc JSON.
            if (!res.IsSuccessStatusCode && res.StatusCode != HttpStatusCode.BadRequest)
                throw new RoutingUnavailableException($"OSRM tra ve {(int)res.StatusCode}");
            body = await res.Content.ReadFromJsonAsync<OsrmResponse>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Khong goi duoc OSRM");
            throw new RoutingUnavailableException("Khong ket noi duoc dich vu chi duong", ex);
        }

        if (body?.Code is "NoRoute" or "NoSegment") return null;
        if (body?.Code != "Ok" || body.Routes is not [var route, ..])
        {
            logger.LogWarning("OSRM loi: {Code} {Message}", body?.Code, body?.Message);
            throw new RoutingUnavailableException($"OSRM loi: {body?.Code}");
        }

        return ToRouteResponse(route);
    }

    /// <summary>
    /// Doi ket qua OSRM sang dang cua VietRoad. public static de unit test voi JSON mau.
    /// Moi buoc co duong rieng (geometry): ghep noi tiep thanh 1 day diem, nho vi tri diem re.
    /// </summary>
    public static RouteResponse ToRouteResponse(OsrmRoute route)
    {
        var points = new List<double[]>();
        var steps = new List<RouteStep>();

        foreach (var s in route.Legs.SelectMany(l => l.Steps))
        {
            var stepPoints = Polyline.Decode(s.Geometry ?? "");

            // Diem dau cua buoc nay trung diem cuoi cua buoc truoc -> bo bot 1 diem.
            var skipFirst = points.Count > 0 && stepPoints.Count > 0 &&
                            SameAsLast(points, stepPoints[0]);
            var maneuverIndex = skipFirst || stepPoints.Count == 0 ? points.Count - 1 : points.Count;

            // Bo moi diem trung diem lien truoc (vd buoc "arrive" cua OSRM = 2 diem giong het).
            foreach (var p in stepPoints)
                if (points.Count == 0 || !SameAsLast(points, p)) points.Add([p.Lat, p.Lng]);

            var m = s.Maneuver;
            if (!VietnameseInstructions.IsWorthAnnouncing(m.Type, m.Modifier)) continue;

            var name = string.IsNullOrWhiteSpace(s.Name) ? s.Ref : s.Name; // khong co ten -> dung so hieu (QL1A)
            steps.Add(new RouteStep(
                PointIndex: Math.Max(0, maneuverIndex),
                Lat: m.Location[1],
                Lng: m.Location[0],
                Type: m.Type,
                Modifier: m.Modifier,
                Name: string.IsNullOrWhiteSpace(name) ? null : name,
                Instruction: VietnameseInstructions.Build(m.Type, m.Modifier, name, m.Exit)));
        }

        return new RouteResponse(route.Distance, route.Duration, points, steps);
    }

    private static bool SameAsLast(List<double[]> points, (double Lat, double Lng) p)
    {
        var last = points[^1];
        return last[0] == p.Lat && last[1] == p.Lng;
    }
}

// ----- JSON cua OSRM (chi lay cac truong can dung) -----

public record OsrmResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("routes")] List<OsrmRoute>? Routes);

public record OsrmRoute(
    [property: JsonPropertyName("distance")] double Distance,
    [property: JsonPropertyName("duration")] double Duration,
    [property: JsonPropertyName("legs")] List<OsrmLeg> Legs);

public record OsrmLeg([property: JsonPropertyName("steps")] List<OsrmStep> Steps);

public record OsrmStep(
    [property: JsonPropertyName("geometry")] string? Geometry,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("ref")] string? Ref,
    [property: JsonPropertyName("maneuver")] OsrmManeuver Maneuver);

public record OsrmManeuver(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("modifier")] string? Modifier,
    [property: JsonPropertyName("exit")] int? Exit,
    [property: JsonPropertyName("location")] double[] Location); // [lng, lat]
