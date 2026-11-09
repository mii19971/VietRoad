namespace VietRoad.Data;

/// <summary>
/// Mot dong trong bang "vietroad.subscriptions" = 1 LAN kich hoat goi tra phi cho 1 nguoi dung.
///
/// Luu lich su (khong ghi de): moi lan tang / gia han / huy deu de lai dau vet, biet ai lam, luc nao.
/// Goi hien tai = dong co StartsAt &lt;= bay gio &lt; ExpiresAt va chua bi huy. Khong co dong nao -> goi Mien phi.
///
/// Source cho biet goi den tu dau: "admin" (quan tri vien kich hoat). Sau nay them
/// "google_play", "vnpay"... khi gan thanh toan, cau truc bang khong doi.
/// </summary>
public class SubscriptionEntity
{
    public int Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Xem <see cref="Plans"/>.</summary>
    public required string Plan { get; set; }

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public required string Source { get; set; }
    public string? Note { get; set; }                 // vd "Tang khach hang than thiet"
    public Guid? CreatedByUserId { get; set; }        // admin da kich hoat
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
}

public static class SubscriptionSources
{
    public const string Admin = "admin";
}

/// <summary>
/// Cac goi dich vu va tinh nang cua tung goi. Doi tinh nang cua goi: chi sua o day.
/// </summary>
public static class Plans
{
    public const string Free = "free";
    public const string Premium = "premium";

    /// <summary>Goi co the kich hoat (tra phi). "free" la mac dinh, khong can kich hoat.</summary>
    public static readonly string[] Paid = [Premium];

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> FeaturesByPlan =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Free] = new HashSet<string>(),
            [Premium] = new HashSet<string> { Features.Navigation, Features.PoliceAlerts },
        };

    public static IReadOnlySet<string> FeaturesOf(string plan) =>
        FeaturesByPlan.TryGetValue(plan, out var f) ? f : FeaturesByPlan[Free];

    public static string Label(string plan) => plan switch
    {
        Premium => "Premium",
        _ => "Miễn phí",
    };
}

/// <summary>Tinh nang co the bi gioi han theo goi.</summary>
public static class Features
{
    /// <summary>Chi duong bang giong noi (tim dia diem + tinh duong).</summary>
    public const string Navigation = "navigation";

    /// <summary>Thay canh bao chot CSGT.</summary>
    public const string PoliceAlerts = "police_alerts";
}
