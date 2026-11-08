namespace VietRoad.Api.Routing;

/// <summary>
/// Giai ma "encoded polyline" (dinh dang cua Google, OSRM dung mac dinh).
/// Day toa do duoc nen thanh chuoi ky tu ngan, vd "_p~iF~ps|U_ulLnnqC" = 2 diem.
/// Cach nen: moi so = (toa do * 10^5) - toa do truoc do, roi chia thanh cum 5 bit,
/// moi cum + 63 thanh 1 ky tu ASCII. Giai ma = lam nguoc lai.
/// </summary>
public static class Polyline
{
    public static List<(double Lat, double Lng)> Decode(string encoded)
    {
        var points = new List<(double, double)>();
        int index = 0, lat = 0, lng = 0;

        while (index < encoded.Length)
        {
            lat += NextValue(encoded, ref index);
            lng += NextValue(encoded, ref index);
            points.Add((lat / 1e5, lng / 1e5));
        }
        return points;
    }

    // Doc 1 so: gom cac cum 5 bit cho toi khi gap cum khong co bit "con tiep" (0x20).
    private static int NextValue(string s, ref int index)
    {
        int result = 0, shift = 0, b;
        do
        {
            b = s[index++] - 63;
            result |= (b & 0x1F) << shift;
            shift += 5;
        } while (b >= 0x20);

        // Bit cuoi = dau am/duong.
        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
