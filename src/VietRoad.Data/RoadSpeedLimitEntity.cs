namespace VietRoad.Data;

/// <summary>
/// Mot dong trong bang "vietroad.road_speed_limits" = 1 doan duong OSM (way) co the maxspeed.
/// App tai cac doan quanh xe, tu tim xe dang di tren doan nao -> biet gioi han toc do hien tai.
///
/// Khong co PostGIS nen luu:
///  - Points: day toa do dang JSON "[[lat,lng],[lat,lng],...]" (theo chieu xe chay neu Oneway)
///  - MinLat/MaxLat/MinLng/MaxLng: khung bao, de tim "doan nao nam gan day" bang so sanh so thuong.
/// </summary>
public class RoadSpeedLimitEntity
{
    /// <summary>Id cua way tren OpenStreetMap (khong tu tang).</summary>
    public long Id { get; set; }

    public required string Highway { get; set; }   // motorway, trunk, primary...
    public string? Name { get; set; }
    public string? Ref { get; set; }                // so hieu duong, vd "QL1A", "CT.01"
    public int MaxSpeed { get; set; }               // km/h
    public bool Oneway { get; set; }                // true: chi ap dung theo chieu cua Points
    public required string Points { get; set; }

    public double MinLat { get; set; }
    public double MaxLat { get; set; }
    public double MinLng { get; set; }
    public double MaxLng { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
