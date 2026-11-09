namespace VietRoad.Api.Admin;

/// <summary>1 trang ket qua. Total = tong so dong khop bo loc (de ve phan trang).</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Canh bao kem day du thong tin quan tri (nhieu hon ban tra cho app tai xe).</summary>
public record AdminAlert(
    int Id,
    string Type,
    string Name,
    double Lat,
    double Lng,
    int? SpeedLimit,
    double? Direction,
    string Source,
    string? ExternalId,
    int Confidence,
    int ConfirmCount,
    int DenyCount,
    bool IsActive,
    bool IsVisible,               // tai xe co dang thay canh bao nay khong
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? VerifiedAt,
    string? CreatedByEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Admin tao / sua canh bao. Truong null khi sua = giu nguyen.</summary>
public record AlertUpsertRequest(
    string? Type, string? Name, double? Lat, double? Lng,
    int? SpeedLimit, double? Direction, bool? IsActive);

public record AdminUser(
    Guid Id, string Email, string DisplayName, string Role,
    DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt, int ReportCount,
    DateTimeOffset? PremiumUntil);   // null = goi Mien phi

public record RoleChangeRequest(string? Role);

public record AdminStats(
    int VisibleAlerts,
    int PendingReports,
    int HiddenAlerts,                       // do tin cay thap
    int InactiveAlerts,                     // admin tu choi / OSM da xoa
    IReadOnlyDictionary<string, int> VisibleBySource,
    int Users,
    int Admins,
    int ReportsLast24h,
    int VotesLast24h);

/// <summary>Bo loc trang thai trong danh sach canh bao.</summary>
public enum AlertStatus
{
    All,
    Pending,   // bao cao nguoi dung chua duyet
    Visible,   // tai xe dang thay
    Hidden,    // con hieu luc nhung do tin cay thap
    Inactive,  // bi tat
    Expired,   // het han (chot CSGT)
}
