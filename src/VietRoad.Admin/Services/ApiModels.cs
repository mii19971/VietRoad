namespace VietRoad.Admin.Services;

// Admin la 1 "client" cua VietRoad.Api, giong app Android: tu khai bao cac kieu du lieu
// can dung. "Hop dong" giua 2 ben la JSON, ten truong khop la doc duoc.

public record UserProfile(Guid Id, string Email, string DisplayName, string Role, DateTimeOffset CreatedAt);

public record AuthResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserProfile User);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public record AdminAlert(
    int Id, string Type, string Name, double Lat, double Lng, int? SpeedLimit, double? Direction,
    string Source, string? ExternalId, int Confidence, int ConfirmCount, int DenyCount,
    bool IsActive, bool IsVisible, DateTimeOffset? ExpiresAt, DateTimeOffset? VerifiedAt,
    string? CreatedByEmail, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    /// <summary>Link xem vi tri tren ban do OpenStreetMap.</summary>
    public string MapUrl => FormattableString.Invariant(
        $"https://www.openstreetmap.org/?mlat={Lat}&mlon={Lng}#map=18/{Lat}/{Lng}");
}

public record AlertUpsert(
    string? Type, string? Name, double? Lat, double? Lng, int? SpeedLimit, double? Direction, bool? IsActive);

public record AdminUser(
    Guid Id, string Email, string DisplayName, string Role,
    DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt, int ReportCount,
    DateTimeOffset? PremiumUntil);   // null = goi Mien phi

// ----- Goi dich vu -----

public record SubscriptionInfo(string Plan, string PlanLabel, DateTimeOffset? ExpiresAt, List<string> Features);

public record SubscriptionRecord(
    int Id, string Plan, DateTimeOffset StartsAt, DateTimeOffset ExpiresAt, string Source, string? Note,
    string? CreatedByEmail, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt, string Status);

public record UserSubscriptions(SubscriptionInfo Current, List<SubscriptionRecord> History);

public record AdminStats(
    int VisibleAlerts, int PendingReports, int HiddenAlerts, int InactiveAlerts,
    Dictionary<string, int> VisibleBySource, int Users, int Admins, int ReportsLast24h, int VotesLast24h);

/// <summary>Ket qua goi API co the that bai vi du lieu sai (400/404/409): Error chua thong bao cua API.</summary>
public record ApiResult<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;
}

public static class Labels
{
    public static readonly string[] AlertTypes = ["speed_camera", "red_light_camera", "average_speed_camera", "police_checkpoint"];

    public static string Type(string type) => type switch
    {
        "speed_camera" => "Camera tốc độ",
        "red_light_camera" => "Camera đèn đỏ",
        "average_speed_camera" => "Camera tốc độ TB",
        "police_checkpoint" => "Chốt CSGT",
        _ => type,
    };

    public static string Time(DateTimeOffset? t) => t?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";

    public static string Date(DateTimeOffset? t) => t?.ToLocalTime().ToString("dd/MM/yyyy") ?? "—";

    public static string Feature(string feature) => feature switch
    {
        "navigation" => "Chỉ đường bằng giọng nói",
        "police_alerts" => "Cảnh báo chốt CSGT",
        _ => feature,
    };

    public static string SubscriptionStatus(string status) => status switch
    {
        "active" => "Đang dùng",
        "upcoming" => "Chờ tới lượt",
        "expired" => "Hết hạn",
        "cancelled" => "Đã hủy",
        _ => status,
    };
}
