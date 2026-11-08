namespace VietRoad.Api.Routing;

/// <summary>1 ket qua tim kiem dia diem.</summary>
public record Place(string Name, string Address, double Lat, double Lng);

public record PlacesResponse(IReadOnlyList<Place> Places);

/// <summary>
/// 1 chi dan re (maneuver).
/// PointIndex: vi tri cua diem re trong RouteResponse.Points -> app tinh duoc "con bao xa toi cho re".
/// Instruction: cau tieng Viet doc cho tai xe, vd "Rẽ trái vào Phố Huế".
/// </summary>
public record RouteStep(
    int PointIndex,
    double Lat,
    double Lng,
    string Type,        // kieu OSRM: depart, turn, roundabout, arrive...
    string? Modifier,   // left, right, slight left, uturn...
    string? Name,       // ten duong sau khi re
    string Instruction);

/// <summary>
/// Tuyen duong.
/// Points: cac diem [lat, lng] noi lien tao thanh duong di (de app do vi tri xe so voi tuyen).
/// Steps: chi gom cac cho can lam gi do (re, vao vong xuyen, den noi...), da bo cac buoc
/// "di thang, doi ten duong" de app khong noi qua nhieu.
/// </summary>
public record RouteResponse(
    double DistanceM,
    double DurationS,
    IReadOnlyList<double[]> Points,
    IReadOnlyList<RouteStep> Steps);
