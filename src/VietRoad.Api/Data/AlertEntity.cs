namespace VietRoad.Api.Data;

/// <summary>
/// Mot dong trong bang "vietroad.alerts".
/// Database chua co PostGIS, nen vi tri luu bang 2 cot so: lat, lng.
/// </summary>
public class AlertEntity
{
    public int Id { get; set; }
    public required string Type { get; set; }
    public required string Name { get; set; }
    public int? SpeedLimit { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}
