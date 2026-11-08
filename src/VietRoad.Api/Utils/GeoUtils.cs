namespace VietRoad.Api.Utils;

/// <summary>Hinh chu nhat theo toa do: lat trong [MinLat, MaxLat], lng trong [MinLng, MaxLng].</summary>
public record BoundingBox(double MinLat, double MaxLat, double MinLng, double MaxLng);

public static class GeoUtils
{
    // Ban kinh trung binh cua Trai Dat (met).
    private const double EarthRadiusM = 6_371_000;

    // 1 do vi do ~ 111.32 km (gan nhu khong doi o moi noi).
    private const double MetersPerDegreeLat = 111_320;

    // Doi do sang radian, vi Math.Sin/Math.Cos dung radian.
    private static double ToRad(double deg) => deg * Math.PI / 180;

    /// <summary>
    /// Khoang cach (met) giua 2 diem GPS theo cong thuc Haversine
    /// (khoang cach "duong chim bay" tren mat cau).
    /// </summary>
    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);

        var a = Math.Pow(Math.Sin(dLat / 2), 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);

        return 2 * EarthRadiusM * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>
    /// Hinh chu nhat bao quanh vong tron ban kinh radiusM quanh (lat, lng).
    /// Moi diem trong vong tron chac chan nam trong hinh chu nhat nay,
    /// nen dung no de database loc so bo truoc khi tinh khoang cach chinh xac.
    /// </summary>
    public static BoundingBox BoundingBox(double lat, double lng, double radiusM)
    {
        var dLat = radiusM / MetersPerDegreeLat;

        // Cang xa xich dao, 1 do kinh do cang ngan (nhan voi cos(lat)),
        // nen can noi rong khoang lng. Max(..., 0.01) tranh chia cho ~0 gan 2 cuc.
        var dLng = radiusM / (MetersPerDegreeLat * Math.Max(Math.Cos(ToRad(lat)), 0.01));

        return new BoundingBox(lat - dLat, lat + dLat, lng - dLng, lng + dLng);
    }

    /// <summary>Goc lech nho nhat giua 2 huong (do), ket qua 0..180. Vd 350 va 10 -> 20.</summary>
    public static double AngleDifference(double a, double b)
    {
        var diff = Math.Abs(a - b) % 360;
        return diff > 180 ? 360 - diff : diff;
    }

    /// <summary>Toa do hop le: lat trong [-90, 90], lng trong [-180, 180].</summary>
    public static bool IsValidCoord(double lat, double lng) =>
        lat is >= -90 and <= 90 && lng is >= -180 and <= 180;

    /// <summary>
    /// Diem co nam trong khung bao dat lien Viet Nam khong (khung chu nhat, gan dung:
    /// co lan sang 1 phan Lao / Campuchia, du de chan toa do sai hoan toan nhu o My).
    /// </summary>
    public static bool IsInVietnam(double lat, double lng) =>
        lat is >= 8.0 and <= 23.5 && lng is >= 102.0 and <= 110.0;
}
