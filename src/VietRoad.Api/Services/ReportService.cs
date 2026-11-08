using Microsoft.EntityFrameworkCore;
using VietRoad.Api.Utils;
using VietRoad.Data;

namespace VietRoad.Api.Services;

public record ReportInput(string Type, double Lat, double Lng, double? Direction, int? SpeedLimit);

public enum ReportOutcome { Created, MergedIntoExisting, DailyLimitReached }

public record ReportResult(ReportOutcome Outcome, AlertEntity? Alert);

/// <summary>
/// User Reports: nguoi dung bao canh bao moi, va xac nhan / phu nhan canh bao co san.
/// </summary>
public class ReportService(VietRoadDbContext db, TimeProvider time)
{
    /// <summary>Bao cao cach canh bao cung loai trong ban kinh nay -> coi la cung 1 diem.</summary>
    public const double DuplicateRadiusM = 50;

    /// <summary>2 huong lech qua goc nay -> 2 camera khac nhau (vd 2 chieu cua 1 con duong).</summary>
    public const double SameDirectionMaxDiff = 45;

    /// <summary>Moi nguoi toi da bao nhieu canh bao MOI trong 24 gio (chong spam).</summary>
    public const int MaxReportsPerDay = 20;

    /// <summary>Chot CSGT chi ton tai vai gio. Moi lan co nguoi xac nhan thi gia han them.</summary>
    public static readonly TimeSpan PoliceCheckpointLifetime = TimeSpan.FromHours(2);

    public async Task<ReportResult> ReportAsync(Guid userId, ReportInput input, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        // 1. Da co canh bao giong vay gan day -> khong tao trung, tinh la 1 phieu xac nhan.
        var duplicate = await FindDuplicateAsync(input, now, ct);
        if (duplicate is not null)
        {
            await ApplyVoteAsync(duplicate, userId, isConfirm: true, now, ct);
            await db.SaveChangesAsync(ct);
            return new ReportResult(ReportOutcome.MergedIntoExisting, duplicate);
        }

        // 2. Gioi han so bao cao moi / ngay (chi dem bao cao tao canh bao moi).
        var since = now.AddDays(-1);
        var reportsToday = await db.Alerts.CountAsync(a => a.CreatedByUserId == userId && a.CreatedAt > since, ct);
        if (reportsToday >= MaxReportsPerDay)
            return new ReportResult(ReportOutcome.DailyLimitReached, null);

        // 3. Tao canh bao moi.
        var alert = new AlertEntity
        {
            Type = input.Type,
            Name = AlertTypes.DefaultName(input.Type),
            Lat = input.Lat,
            Lng = input.Lng,
            Direction = input.Direction,
            SpeedLimit = AlertTypes.HasSpeedLimit(input.Type) ? input.SpeedLimit : null,
            Source = AlertSources.User,
            CreatedByUserId = userId,
            ExpiresAt = input.Type == AlertTypes.PoliceCheckpoint ? now + PoliceCheckpointLifetime : null,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Alerts.Add(alert);

        // Nguoi bao cao tu dong tinh la 1 xac nhan. Luu canh bao va phieu trong 1 lan SaveChanges.
        await ApplyVoteAsync(alert, userId, isConfirm: true, now, ct);
        await db.SaveChangesAsync(ct);

        return new ReportResult(ReportOutcome.Created, alert);
    }

    /// <summary>Xac nhan / phu nhan 1 canh bao. null = khong tim thay (hoac da het han / bi an).</summary>
    public async Task<AlertEntity?> VoteAsync(int alertId, Guid userId, bool isConfirm, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var alert = await db.Alerts.FirstOrDefaultAsync(
            a => a.Id == alertId && a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now), ct);
        if (alert is null)
            return null;

        await ApplyVoteAsync(alert, userId, isConfirm, now, ct);
        await db.SaveChangesAsync(ct);
        return alert;
    }

    private async Task<AlertEntity?> FindDuplicateAsync(ReportInput input, DateTimeOffset now, CancellationToken ct)
    {
        var box = GeoUtils.BoundingBox(input.Lat, input.Lng, DuplicateRadiusM);

        // Khong loc theo do tin cay: bao lai 1 diem dang bi an se lam no hien tro lai.
        var candidates = await db.Alerts
            .Where(a => a.Type == input.Type
                     && a.Lat >= box.MinLat && a.Lat <= box.MaxLat
                     && a.Lng >= box.MinLng && a.Lng <= box.MaxLng
                     && a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now))
            .ToListAsync(ct);

        return candidates
            .Where(a => GeoUtils.DistanceMeters(input.Lat, input.Lng, a.Lat, a.Lng) <= DuplicateRadiusM)
            // Khong biet huong o 1 trong 2 ben -> coi nhu cung diem.
            .Where(a => a.Direction is null || input.Direction is null
                     || GeoUtils.AngleDifference(a.Direction.Value, input.Direction.Value) <= SameDirectionMaxDiff)
            .OrderBy(a => GeoUtils.DistanceMeters(input.Lat, input.Lng, a.Lat, a.Lng))
            .FirstOrDefault();
    }

    /// <summary>
    /// Ghi / doi phieu cua user, roi tinh lai so dem va do tin cay TU DAU tu danh sach phieu.
    /// Tinh lai tu dau (thay vi +1 / -1) thi khong bao gio bi lech, ke ca khi user doi y.
    /// Chua SaveChanges - nguoi goi luu 1 lan cho ca canh bao lan phieu.
    /// </summary>
    private async Task ApplyVoteAsync(AlertEntity alert, Guid userId, bool isConfirm, DateTimeOffset now, CancellationToken ct)
    {
        // Canh bao moi (chua luu, Id = 0) thi chua co phieu nao.
        var votes = alert.Id == 0
            ? []
            : await db.AlertVotes.Where(v => v.AlertId == alert.Id).ToListAsync(ct);

        var mine = votes.FirstOrDefault(v => v.UserId == userId);
        if (mine is null)
        {
            mine = new AlertVoteEntity
            {
                Id = Guid.CreateVersion7(),
                Alert = alert, // gan qua navigation: EF tu dien AlertId sau khi canh bao duoc luu
                UserId = userId,
                IsConfirm = isConfirm,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.AlertVotes.Add(mine);
            votes.Add(mine);
        }
        else if (mine.IsConfirm != isConfirm)
        {
            mine.IsConfirm = isConfirm; // doi y: thay phieu cu, khong cong them
            mine.UpdatedAt = now;
        }

        alert.ConfirmCount = votes.Count(v => v.IsConfirm);
        alert.DenyCount = votes.Count(v => !v.IsConfirm);
        alert.Confidence = AlertScoring.Compute(alert.Source, alert.ConfirmCount, alert.DenyCount);
        alert.UpdatedAt = now;

        if (isConfirm)
        {
            alert.LastConfirmedAt = now;
            // Chot CSGT con do -> gia han.
            if (alert.Type == AlertTypes.PoliceCheckpoint)
                alert.ExpiresAt = now + PoliceCheckpointLifetime;
        }
    }
}
