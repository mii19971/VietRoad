using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VietRoad.Api.Services;
using VietRoad.Data;

namespace VietRoad.Api.Admin;

public enum AdminResult { Ok, NotFound, OsmReadOnly, CannotDemoteSelf }

/// <summary>Nghiep vu cua trang quan tri: duyet bao cao, sua canh bao, quan ly nguoi dung.</summary>
public class AdminService(VietRoadDbContext db, TimeProvider time)
{
    public const int MaxPageSize = 100;

    // ---------- Canh bao ----------

    public async Task<PagedResult<AdminAlert>> ListAlertsAsync(
        string? source, string? type, AlertStatus status, string? search, int page, int pageSize, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        // IQueryable: chi la "cau truy van dang xay", chua chay. Moi .Where them 1 dieu kien AND.
        IQueryable<AlertEntity> query = db.Alerts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(source)) query = query.Where(a => a.Source == source);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(a => a.Type == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a => a.Name.ToLower().Contains(s) || (a.ExternalId != null && a.ExternalId.Contains(s)));
        }
        query = query.Where(StatusFilter(status, now));

        var total = await query.CountAsync(ct);          // SQL 1: SELECT COUNT(*) ...
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var rows = await WithCreatorEmail(query
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)             // SQL 2: ... OFFSET .. LIMIT ..
                .Take(pageSize))
            .ToListAsync(ct);

        var items = rows
            .OrderByDescending(r => r.Alert.CreatedAt).ThenByDescending(r => r.Alert.Id)
            .Select(r => ToDto(r.Alert, r.Email, now))
            .ToList();

        return new PagedResult<AdminAlert>(items, total, page, pageSize);
    }

    public async Task<AdminAlert?> GetAlertAsync(int id, CancellationToken ct)
    {
        var row = await WithCreatorEmail(db.Alerts.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(ct);
        return row is null ? null : ToDto(row.Alert, row.Email, time.GetUtcNow());
    }

    /// <summary>Admin tao canh bao: nguon "admin", coi nhu da duyet.</summary>
    public async Task<AdminAlert> CreateAlertAsync(Guid adminId, AlertUpsertRequest input, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var alert = new AlertEntity
        {
            Type = input.Type!,
            Name = string.IsNullOrWhiteSpace(input.Name) ? AlertTypes.DefaultName(input.Type!) : input.Name.Trim(),
            Lat = input.Lat!.Value,
            Lng = input.Lng!.Value,
            Direction = input.Direction,
            SpeedLimit = AlertTypes.HasSpeedLimit(input.Type!) ? input.SpeedLimit : null,
            Source = AlertSources.Admin,
            IsActive = input.IsActive ?? true,
            ExpiresAt = input.Type == AlertTypes.PoliceCheckpoint ? now + ReportService.PoliceCheckpointLifetime : null,
            VerifiedAt = now,
            VerifiedByUserId = adminId,
            CreatedByUserId = adminId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        alert.Confidence = AlertScoring.Compute(alert);

        db.Alerts.Add(alert);
        await db.SaveChangesAsync(ct);
        return (await GetAlertAsync(alert.Id, ct))!;
    }

    /// <summary>
    /// Sua canh bao. Du lieu OSM chi duoc bat / tat: sua vi tri, ten... o day se bi
    /// lan nhap OSM sau ghi de, nen phai sua tren OpenStreetMap (va giup ca cong dong).
    /// </summary>
    public async Task<AdminResult> UpdateAlertAsync(int id, AlertUpsertRequest input, CancellationToken ct)
    {
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null) return AdminResult.NotFound;

        var editsData = input.Type is not null || input.Name is not null || input.Lat is not null
                     || input.Lng is not null || input.SpeedLimit is not null || input.Direction is not null;
        if (alert.Source == AlertSources.Osm && editsData)
            return AdminResult.OsmReadOnly;

        if (input.Type is not null) alert.Type = input.Type;
        if (!string.IsNullOrWhiteSpace(input.Name)) alert.Name = input.Name.Trim();
        if (input.Lat is not null) alert.Lat = input.Lat.Value;
        if (input.Lng is not null) alert.Lng = input.Lng.Value;
        if (input.Direction is not null) alert.Direction = input.Direction;
        if (input.SpeedLimit is not null) alert.SpeedLimit = input.SpeedLimit;
        if (!AlertTypes.HasSpeedLimit(alert.Type)) alert.SpeedLimit = null;
        if (input.IsActive is not null) alert.IsActive = input.IsActive.Value;

        alert.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return AdminResult.Ok;
    }

    /// <summary>Duyet: canh bao duoc tin cay cao, roi khoi hang cho.</summary>
    public async Task<AdminResult> ApproveAsync(int id, Guid adminId, CancellationToken ct)
    {
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null) return AdminResult.NotFound;

        var now = time.GetUtcNow();
        alert.VerifiedAt = now;
        alert.VerifiedByUserId = adminId;
        alert.IsActive = true;
        alert.Confidence = AlertScoring.Compute(alert); // diem goc len 90
        alert.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return AdminResult.Ok;
    }

    /// <summary>Tu choi: tat canh bao (khong xoa, de con lich su va thong ke).</summary>
    public async Task<AdminResult> RejectAsync(int id, CancellationToken ct)
    {
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null) return AdminResult.NotFound;

        alert.IsActive = false;
        alert.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return AdminResult.Ok;
    }

    // ---------- Nguoi dung ----------

    public async Task<PagedResult<AdminUser>> ListUsersAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        IQueryable<UserEntity> query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.Email.Contains(s) || u.DisplayName.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var now = time.GetUtcNow();

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUser(
                u.Id, u.Email, u.DisplayName, u.Role, u.CreatedAt, u.LastLoginAt,
                db.Alerts.Count(a => a.CreatedByUserId == u.Id), // EF dich thanh sub-query COUNT
                // Premium con han toi khi nao (tinh ca cac lan gia han noi tiep); null = Mien phi.
                db.Subscriptions
                    .Where(s => s.UserId == u.Id && s.Plan == Plans.Premium && s.CancelledAt == null && s.ExpiresAt > now)
                    .Max(s => (DateTimeOffset?)s.ExpiresAt)))
            .ToListAsync(ct);

        return new PagedResult<AdminUser>(items, total, page, pageSize);
    }

    /// <summary>
    /// Doi quyen. Luu y: quyen nam trong access token, nen chi co hieu luc khi nguoi do
    /// nhan token moi (dang nhap lai / refresh, toi da AccessTokenMinutes).
    /// </summary>
    public async Task<AdminResult> SetRoleAsync(Guid userId, string role, Guid adminId, CancellationToken ct)
    {
        // Tu ha quyen minh -> co the khong con admin nao.
        if (userId == adminId && role != UserRoles.Admin)
            return AdminResult.CannotDemoteSelf;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return AdminResult.NotFound;

        user.Role = role;
        await db.SaveChangesAsync(ct);
        return AdminResult.Ok;
    }

    // ---------- Thong ke ----------

    public async Task<AdminStats> GetStatsAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var since = now.AddDays(-1);

        var visibleBySource = await db.Alerts
            .Where(DbAlertService.VisibleAt(now))
            .GroupBy(a => a.Source)                       // SQL: GROUP BY source
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Source, x => x.Count, ct);

        return new AdminStats(
            VisibleAlerts: visibleBySource.Values.Sum(),
            PendingReports: await db.Alerts.CountAsync(StatusFilter(AlertStatus.Pending, now), ct),
            HiddenAlerts: await db.Alerts.CountAsync(StatusFilter(AlertStatus.Hidden, now), ct),
            InactiveAlerts: await db.Alerts.CountAsync(a => !a.IsActive, ct),
            VisibleBySource: visibleBySource,
            Users: await db.Users.CountAsync(ct),
            Admins: await db.Users.CountAsync(u => u.Role == UserRoles.Admin, ct),
            ReportsLast24h: await db.Alerts.CountAsync(a => a.Source == AlertSources.User && a.CreatedAt > since, ct),
            VotesLast24h: await db.AlertVotes.CountAsync(v => v.UpdatedAt > since, ct));
    }

    // ---------- Ham phu ----------

    private static Expression<Func<AlertEntity, bool>> StatusFilter(AlertStatus status, DateTimeOffset now) => status switch
    {
        AlertStatus.Pending => a => a.Source == AlertSources.User && a.VerifiedAt == null
                                 && a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now),
        AlertStatus.Visible => DbAlertService.VisibleAt(now),
        AlertStatus.Hidden => a => a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now)
                                && a.Confidence < AlertScoring.MinVisible,
        AlertStatus.Inactive => a => !a.IsActive,
        AlertStatus.Expired => a => a.ExpiresAt != null && a.ExpiresAt <= now,
        _ => a => true,
    };

    private record AlertWithEmail(AlertEntity Alert, string? Email);

    // LEFT JOIN users: lay email nguoi bao cao (neu co).
    private IQueryable<AlertWithEmail> WithCreatorEmail(IQueryable<AlertEntity> alerts) =>
        from a in alerts
        join u in db.Users on a.CreatedByUserId equals u.Id into creators
        from u in creators.DefaultIfEmpty()
        select new AlertWithEmail(a, u == null ? null : u.Email);

    private static AdminAlert ToDto(AlertEntity a, string? createdByEmail, DateTimeOffset now) => new(
        a.Id, a.Type, a.Name, a.Lat, a.Lng, a.SpeedLimit, a.Direction, a.Source, a.ExternalId,
        a.Confidence, a.ConfirmCount, a.DenyCount, a.IsActive,
        // Cung dieu kien voi DbAlertService.VisibleAt, nhung chay tren object C# da tai ve.
        IsVisible: a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now) && a.Confidence >= AlertScoring.MinVisible,
        a.ExpiresAt, a.VerifiedAt, createdByEmail, a.CreatedAt, a.UpdatedAt);
}
