using VietRoad.Data;

namespace VietRoad.Api.Models;

/// <summary>Mot diem canh bao tren duong (camera, chot CSGT...).</summary>
public record Alert(
    int Id,
    string Type,
    double Lat,
    double Lng,
    int? SpeedLimit,
    string Name,
    double? Direction,           // huong camera (do), null = khong ro
    string Source,               // "osm" -> app phai ghi "© OpenStreetMap contributors"
    int Confidence,              // 0..100
    int ConfirmCount,            // so nguoi xac nhan "van con"
    int DenyCount,               // so nguoi bao "khong con"
    DateTimeOffset? ExpiresAt)   // canh bao tam thoi (chot CSGT) het han luc nay
{
    /// <summary>Doi tu dang database sang dang tra cho app.</summary>
    public static Alert From(AlertEntity a) => new(
        a.Id, a.Type, a.Lat, a.Lng, a.SpeedLimit, a.Name, a.Direction, a.Source,
        a.Confidence, a.ConfirmCount, a.DenyCount, a.ExpiresAt);
}

/// <summary>Canh bao kem khoang cach tu vi tri tai xe den no.</summary>
public record NearbyAlert(Alert Alert, int DistanceM);

/// <summary>Du lieu tra ve cho app.</summary>
public record NearbyAlertsResponse(int Count, IReadOnlyList<NearbyAlert> Alerts);

/// <summary>Noi dung loi tra ve khi request sai.</summary>
public record ErrorResponse(string Error);
