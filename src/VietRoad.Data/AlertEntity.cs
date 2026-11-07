namespace VietRoad.Data;

/// <summary>Mot dong trong bang "vietroad.alerts".</summary>
public class AlertEntity
{
    public int Id { get; set; }

    /// <summary>Loai canh bao, xem <see cref="AlertTypes"/>.</summary>
    public required string Type { get; set; }
    public required string Name { get; set; }
    public int? SpeedLimit { get; set; }

    // Database chua co PostGIS, nen vi tri luu bang 2 cot so.
    public double Lat { get; set; }
    public double Lng { get; set; }

    /// <summary>
    /// Huong camera chup, tinh bang do (0 = Bac, 90 = Dong, 180 = Nam, 270 = Tay).
    /// null = khong ro / chup ca 2 chieu.
    /// </summary>
    public double? Direction { get; set; }

    /// <summary>Du lieu den tu dau, xem <see cref="AlertSources"/>.</summary>
    public required string Source { get; set; }

    /// <summary>Ma ben nguon, vd "osm:node/123". Dung de nhap lai ma khong tao ban trung.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Do tin cay 0..100.</summary>
    public int Confidence { get; set; }

    /// <summary>Canh bao tam thoi (vd chot CSGT) het han luc nay. null = vinh vien.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>false = an di (vd camera da go) ma khong xoa dong.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Cac gia tri hop le cho cot Type.</summary>
public static class AlertTypes
{
    public const string SpeedCamera = "speed_camera";
    public const string RedLightCamera = "red_light_camera";
    public const string AverageSpeedCamera = "average_speed_camera";
}

/// <summary>Cac gia tri hop le cho cot Source.</summary>
public static class AlertSources
{
    public const string Osm = "osm";
    public const string User = "user";
    public const string Admin = "admin";
    public const string Partner = "partner";
}
