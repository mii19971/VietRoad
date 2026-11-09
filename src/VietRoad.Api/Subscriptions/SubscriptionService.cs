using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VietRoad.Data;

namespace VietRoad.Api.Subscriptions;

/// <summary>Goi hien tai cua 1 nguoi (tra cho app de biet duoc dung tinh nang nao).</summary>
public record SubscriptionInfo(
    string Plan,                         // "free" | "premium"
    string PlanLabel,                    // "Miễn phí" | "Premium"
    DateTimeOffset? ExpiresAt,           // het han luc nao (tinh ca cac lan gia han noi tiep); null = mien phi
    IReadOnlyCollection<string> Features);

/// <summary>1 lan kich hoat goi (hien cho admin).</summary>
public record SubscriptionRecord(
    int Id,
    string Plan,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt,
    string Source,
    string? Note,
    string? CreatedByEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string Status);                      // active | upcoming | expired | cancelled

public record UserSubscriptions(SubscriptionInfo Current, IReadOnlyList<SubscriptionRecord> History);

/// <summary>Admin kich hoat goi: Plan = "premium", Days = so ngay (vd 30, 365).</summary>
public record GrantRequest(string? Plan, int? Days, string? Note);

/// <summary>
/// Subscription Service: goi dich vu cua nguoi dung va quyen dung tinh nang.
/// Hien tai goi do admin kich hoat; sau nay cong thanh toan (Google Play, VNPay...)
/// chi can goi GrantAsync voi Source tuong ung.
/// </summary>
public class SubscriptionService(VietRoadDbContext db, TimeProvider time)
{
    public const int MaxDays = 3650;

    /// <summary>Dong dang co hieu luc tai thoi diem now (dung trong cau LINQ -> SQL).</summary>
    public static Expression<Func<SubscriptionEntity, bool>> ActiveAt(DateTimeOffset now) =>
        s => s.CancelledAt == null && s.StartsAt <= now && s.ExpiresAt > now;

    public async Task<SubscriptionInfo> GetCurrentAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var active = await db.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Where(ActiveAt(now))
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefaultAsync(ct);

        if (active is null) return Info(Plans.Free, null);

        // Gia han noi tiep (vd 2 lan x 30 ngay) -> bao ngay het han cuoi cung cua chuoi.
        var lastEnd = await db.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Plan == active.Plan && s.CancelledAt == null && s.ExpiresAt > now)
            .MaxAsync(s => s.ExpiresAt, ct);

        return Info(active.Plan, lastEnd);
    }

    public async Task<bool> HasFeatureAsync(Guid userId, string feature, CancellationToken ct) =>
        (await GetCurrentAsync(userId, ct)).Features.Contains(feature);

    /// <summary>
    /// Kich hoat / gia han. Dang co goi con han -> lan nay bat dau tu luc goi cu het
    /// (khong mat ngay nao). null = khong co nguoi dung nay.
    /// </summary>
    public async Task<SubscriptionRecord?> GrantAsync(
        Guid userId, string plan, int days, string? note, Guid adminId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct)) return null;

        var now = time.GetUtcNow();
        var currentEnd = await db.Subscriptions
            .Where(s => s.UserId == userId && s.CancelledAt == null && s.ExpiresAt > now)
            .MaxAsync(s => (DateTimeOffset?)s.ExpiresAt, ct);
        var start = currentEnd ?? now;

        var entity = new SubscriptionEntity
        {
            UserId = userId,
            Plan = plan,
            StartsAt = start,
            ExpiresAt = start.AddDays(days),
            Source = SubscriptionSources.Admin,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedByUserId = adminId,
            CreatedAt = now,
        };
        db.Subscriptions.Add(entity);
        await db.SaveChangesAsync(ct);

        return (await HistoryAsync(userId, ct)).First(r => r.Id == entity.Id);
    }

    /// <summary>Huy 1 lan kich hoat (vd tang nham). false = khong tim thay.</summary>
    public async Task<bool> CancelAsync(int subscriptionId, Guid adminId, CancellationToken ct)
    {
        var s = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == subscriptionId, ct);
        if (s is null) return false;
        if (s.CancelledAt is null)
        {
            s.CancelledAt = time.GetUtcNow();
            s.CancelledByUserId = adminId;
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    /// <summary>Goi hien tai + lich su (cho trang Admin). null = khong co nguoi dung nay.</summary>
    public async Task<UserSubscriptions?> GetForAdminAsync(Guid userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct)) return null;
        return new UserSubscriptions(await GetCurrentAsync(userId, ct), await HistoryAsync(userId, ct));
    }

    private async Task<List<SubscriptionRecord>> HistoryAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var rows = await (
            from s in db.Subscriptions.AsNoTracking()
            where s.UserId == userId
            join u in db.Users on s.CreatedByUserId equals u.Id into admins
            from admin in admins.DefaultIfEmpty()
            orderby s.CreatedAt descending
            select new { s, AdminEmail = admin == null ? null : admin.Email }
        ).ToListAsync(ct);

        return rows.Select(r => new SubscriptionRecord(
            r.s.Id, r.s.Plan, r.s.StartsAt, r.s.ExpiresAt, r.s.Source, r.s.Note, r.AdminEmail,
            r.s.CreatedAt, r.s.CancelledAt, StatusOf(r.s, now))).ToList();
    }

    public static string StatusOf(SubscriptionEntity s, DateTimeOffset now) =>
        s.CancelledAt is not null ? "cancelled"
        : s.StartsAt > now ? "upcoming"
        : s.ExpiresAt <= now ? "expired"
        : "active";

    private static SubscriptionInfo Info(string plan, DateTimeOffset? expiresAt) =>
        new(plan, Plans.Label(plan), expiresAt, Plans.FeaturesOf(plan).Order().ToList());
}
