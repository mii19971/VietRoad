namespace VietRoad.Api.Routing;

/// <summary>
/// Cau hinh Routing Service (muc "Routing" trong appsettings.json).
/// Mac dinh dung may chu cong cong mien phi cua OSRM / Nominatim (du lieu OpenStreetMap):
/// chi de phat trien. Khi chay that nen tu dung OSRM + Nominatim voi file ban do Viet Nam
/// roi doi 2 dia chi duoi day, khong phai sua code.
/// </summary>
public class RoutingOptions
{
    public const string SectionName = "Routing";

    /// <summary>May chu tinh duong di (OSRM). Phai ket thuc bang "/".</summary>
    public string OsrmBaseUrl { get; set; } = "https://router.project-osrm.org/";

    /// <summary>May chu tim dia diem (Nominatim). Phai ket thuc bang "/".</summary>
    public string NominatimBaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>Nominatim / OSRM cong cong bat buoc gui ten ung dung, thieu se bi chan.</summary>
    public string UserAgent { get; set; } = "VietRoad/0.1";
}
