namespace VietRoad.Api.Models;

/// <summary>Mot diem canh bao tren duong (camera, den do...).</summary>
public record Alert(
    int Id,
    string Type,
    double Lat,
    double Lng,
    int? SpeedLimit,
    string Name,
    double? Direction,   // huong camera (do), null = khong ro
    string Source);      // "osm" -> app phai ghi "© OpenStreetMap contributors"

/// <summary>Canh bao kem khoang cach tu vi tri tai xe den no.</summary>
public record NearbyAlert(Alert Alert, int DistanceM);

/// <summary>Du lieu tra ve cho app.</summary>
public record NearbyAlertsResponse(int Count, IReadOnlyList<NearbyAlert> Alerts);

/// <summary>Noi dung loi tra ve khi request sai.</summary>
public record ErrorResponse(string Error);
