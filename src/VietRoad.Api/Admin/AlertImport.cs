using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VietRoad.Api.Utils;
using VietRoad.Data;

namespace VietRoad.Api.Admin;

/// <summary>
/// Admin gui len cac dong da doc tu file CSV / Excel: moi dong = { ten cot: gia tri }.
/// DryRun = true: chi kiem tra, tra ve ket qua tung dong (de xem truoc), KHONG ghi database.
/// </summary>
public record AlertImportRequest(List<Dictionary<string, string?>>? Rows, bool DryRun);

/// <summary>Ket qua 1 dong. Status: "ok" (hop le / da nhap), "duplicate" (da co), "error" (sai du lieu).</summary>
public record AlertImportRow(
    int Row,                 // so dong trong file (dong tieu de = 1, du lieu bat dau tu 2)
    string Status,
    string? Message,
    string? Type,
    string? Name,
    double? Lat,
    double? Lng,
    int? SpeedLimit,
    double? Direction,
    int? ExistingId,         // trung voi canh bao co san nay
    int? CreatedId);         // id canh bao vua tao (khi DryRun = false)

public record AlertImportResult(int Total, int Valid, int Duplicates, int Errors, int Created, List<AlertImportRow> Rows);

/// <summary>
/// Nhap hang loat canh bao (vd danh sach camera phat nguoi do cong an tinh cong bo).
/// Nguon = admin (do tin cay cao, coi nhu da duyet). Bo qua dong trung voi canh bao da co
/// (cung loai, cach &lt; 50 m, cung huong) va dong trung nhau ngay trong file.
/// </summary>
public class AlertImportService(VietRoadDbContext db, TimeProvider time)
{
    public const int MaxRows = 2000;
    public const string RowKey = "__row";
    public const double DuplicateRadiusM = 50;

    /// <summary>Loai duoc nhap. Khong co chot CSGT: chot chi ton tai vai gio, khong hop nhap tu danh sach.</summary>
    public static readonly string[] ImportableTypes =
    [
        AlertTypes.SpeedCamera, AlertTypes.RedLightCamera, AlertTypes.AverageSpeedCamera,
        AlertTypes.TrafficCamera, AlertTypes.TollBooth, AlertTypes.RailwayCrossing, AlertTypes.SpeedBump, AlertTypes.SchoolZone, AlertTypes.HospitalZone,
    ];

    public async Task<AlertImportResult> ImportAsync(
        IReadOnlyList<Dictionary<string, string?>> rows, bool dryRun, Guid adminId, CancellationToken ct)
    {
        // So dong trong file: Admin gui kem "__row" (vi da bo dong trong); khong co thi tinh theo thu tu.
        var parsed = rows.Select((r, i) =>
            ParseRow(r, r.TryGetValue(RowKey, out var n) && int.TryParse(n, out var row) ? row : i + 2)).ToList();
        var valid = parsed.Where(p => p.Status == "ok").ToList();

        // Lay 1 lan cac canh bao dang bat trong khung bao quanh moi dong hop le (khong query tung dong).
        var existing = new List<AlertEntity>();
        if (valid.Count > 0)
        {
            const double margin = 0.001; // ~100 m
            double minLat = valid.Min(v => v.Lat!.Value) - margin, maxLat = valid.Max(v => v.Lat!.Value) + margin;
            double minLng = valid.Min(v => v.Lng!.Value) - margin, maxLng = valid.Max(v => v.Lng!.Value) + margin;
            existing = await db.Alerts.AsNoTracking()
                .Where(a => a.IsActive && a.Lat >= minLat && a.Lat <= maxLat && a.Lng >= minLng && a.Lng <= maxLng)
                .ToListAsync(ct);
        }

        var accepted = new List<AlertImportRow>();
        var result = new List<AlertImportRow>();
        foreach (var row in parsed)
        {
            if (row.Status != "ok") { result.Add(row); continue; }

            var dupDb = existing.FirstOrDefault(a => SameSpot(a.Type, a.Lat, a.Lng, a.Direction, row));
            if (dupDb is not null)
            {
                result.Add(row with { Status = "duplicate", Message = $"Đã có cảnh báo #{dupDb.Id} ({dupDb.Name}) cách dưới {DuplicateRadiusM:0} m", ExistingId = dupDb.Id });
                continue;
            }
            var dupFile = accepted.FirstOrDefault(a => SameSpot(a.Type!, a.Lat!.Value, a.Lng!.Value, a.Direction, row));
            if (dupFile is not null)
            {
                result.Add(row with { Status = "duplicate", Message = $"Trùng dòng {dupFile.Row} trong file" });
                continue;
            }
            accepted.Add(row);
            result.Add(row);
        }

        var created = 0;
        if (!dryRun && accepted.Count > 0)
        {
            var now = time.GetUtcNow();
            var entities = accepted.ToDictionary(r => r.Row, r => new AlertEntity
            {
                Type = r.Type!,
                Name = r.Name!,
                Lat = r.Lat!.Value,
                Lng = r.Lng!.Value,
                SpeedLimit = r.SpeedLimit,
                Direction = r.Direction,
                Source = AlertSources.Admin,
                Confidence = AlertScoring.Compute(AlertSources.Admin, verified: true, 0, 0),
                CreatedByUserId = adminId,
                VerifiedAt = now,
                VerifiedByUserId = adminId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.Alerts.AddRange(entities.Values);
            await db.SaveChangesAsync(ct); // 1 transaction: nhap het hoac khong gi ca
            created = entities.Count;

            result = result.Select(r => entities.TryGetValue(r.Row, out var e) && r.Status == "ok"
                ? r with { CreatedId = e.Id } : r).ToList();
        }

        return new AlertImportResult(
            Total: result.Count,
            Valid: result.Count(r => r.Status == "ok"),
            Duplicates: result.Count(r => r.Status == "duplicate"),
            Errors: result.Count(r => r.Status == "error"),
            Created: created,
            Rows: result);
    }

    private static bool SameSpot(string type, double lat, double lng, double? direction, AlertImportRow row) =>
        type == row.Type &&
        GeoUtils.DistanceMeters(lat, lng, row.Lat!.Value, row.Lng!.Value) < DuplicateRadiusM &&
        (direction is null || row.Direction is null || GeoUtils.AngleDifference(direction.Value, row.Direction.Value) <= 45);

    // ===================== Doc 1 dong (ham thuan -> unit test) =====================

    /// <summary>Doc 1 dong cua file. Sai o dau thi Status = "error" kem ly do tieng Viet.</summary>
    public static AlertImportRow ParseRow(Dictionary<string, string?> raw, int rowNumber)
    {
        // Chuan hoa ten cot: "Vĩ độ" / "vi do" / "Latitude" -> cung 1 khoa.
        var cols = new Dictionary<string, string>();
        foreach (var (key, value) in raw)
        {
            if (string.IsNullOrWhiteSpace(value) || ColumnOf(key) is not { } col) continue;
            cols.TryAdd(col, value.Trim());
        }
        string? Get(string c) => cols.TryGetValue(c, out var v) ? v : null;

        var errors = new List<string>();

        var type = TypeOf(Get("type"));
        if (Get("type") is null) errors.Add("Thiếu loại");
        else if (type is null) errors.Add($"Không hiểu loại \"{Get("type")}\"");

        double? lat = null, lng = null;
        if (Get("coords") is { } coords)
        {
            if (ParseCoords(coords) is { } c) (lat, lng) = c;
            else errors.Add($"Tọa độ \"{coords}\" không đúng dạng \"21.0285, 105.8542\"");
        }
        else
        {
            lat = ParseNumber(Get("lat"));
            lng = ParseNumber(Get("lng"));
            if (lat is null || lng is null) errors.Add("Thiếu vĩ độ / kinh độ (hoặc cột Tọa độ)");
        }
        if (lat is { } la && lng is { } ln)
        {
            // Hay gap: dao nguoc vi do / kinh do (105, 21).
            if (!GeoUtils.IsInVietnam(la, ln) && GeoUtils.IsInVietnam(ln, la))
                errors.Add("Vĩ độ và kinh độ bị đảo ngược (vĩ độ VN khoảng 8–23, kinh độ khoảng 102–110)");
            else if (!GeoUtils.IsInVietnam(la, ln))
                errors.Add("Tọa độ nằm ngoài Việt Nam");
        }

        int? speed = null;
        if (Get("speed") is { } s)
        {
            speed = LeadingInt(s);
            if (speed is null or < 5 or > 200) errors.Add($"Giới hạn tốc độ \"{s}\" không hợp lệ (5–200)");
        }

        double? direction = null;
        if (Get("direction") is { } d)
        {
            direction = ParseNumber(d);
            if (direction is null or < 0 or >= 360) errors.Add($"Hướng \"{d}\" không hợp lệ (0–359 độ)");
        }

        var name = Get("name");
        if (name is { Length: > 200 }) errors.Add("Tên dài quá 200 ký tự");
        if (type is not null) name ??= AlertTypes.DefaultName(type);
        if (type is not null && !AlertTypes.HasSpeedLimit(type)) speed = null; // vd den do, tram thu phi: bo

        return new AlertImportRow(rowNumber,
            errors.Count == 0 ? "ok" : "error",
            errors.Count == 0 ? null : string.Join("; ", errors),
            type, name, lat, lng, speed, direction, null, null);
    }

    /// <summary>
    /// Ten cot -> khoa chuan. null = cot khong dung (vd STT, Ghi chu).
    /// Nhan ca cot co chu thich phia sau: "Vĩ độ (Latitude)", "Giới hạn tốc độ (km/h)".
    /// </summary>
    public static string? ColumnOf(string header)
    {
        var h = Simplify(header);
        foreach (var (prefix, column) in ColumnPrefixes)
            if (h == prefix || (prefix.Length >= 4 && h.StartsWith(prefix, StringComparison.Ordinal)))
                return column;
        return null;
    }

    // Thu tu quan trong: "tocdo" (gioi han) dat sau cac ten dai hon de khong nham.
    private static readonly (string Prefix, string Column)[] ColumnPrefixes =
    [
        ("loaicanhbao", "type"), ("loai", "type"), ("type", "type"),
        ("tencanhbao", "name"), ("ten", "name"), ("name", "name"), ("vitri", "name"), ("diadiem", "name"),
        ("vido", "lat"), ("latitude", "lat"), ("lat", "lat"),
        ("kinhdo", "lng"), ("longitude", "lng"), ("lng", "lng"), ("lon", "lng"), ("long", "lng"),
        ("toado", "coords"), ("coordinates", "coords"), ("coords", "coords"), ("gps", "coords"),
        ("gioihantocdo", "speed"), ("tocdotoida", "speed"), ("gioihan", "speed"), ("tocdo", "speed"),
        ("speedlimit", "speed"), ("maxspeed", "speed"),
        ("huongcamera", "direction"), ("huong", "direction"), ("direction", "direction"),
    ];

    /// <summary>"Camera tốc độ" / "toc do" / "speed_camera" -> speed_camera. null = khong hieu.</summary>
    public static string? TypeOf(string? value) => value is null ? null : Simplify(value) switch
    {
        "cameratocdo" or "tocdo" or "speedcamera" or "bantocdo" => AlertTypes.SpeedCamera,
        "cameradendo" or "dendo" or "vuotdendo" or "redlightcamera" or "redlight" => AlertTypes.RedLightCamera,
        "cameratocdotrungbinh" or "tocdotrungbinh" or "averagespeedcamera" or "averagespeed" => AlertTypes.AverageSpeedCamera,
        "cameragiamsat" or "giamsat" or "cameraphatnguoi" or "phatnguoi" or "trafficcamera" => AlertTypes.TrafficCamera,
        "tramthuphi" or "thuphi" or "tollbooth" or "bot" => AlertTypes.TollBooth,
        "duongngangduongsat" or "duongngang" or "giaocatduongsat" or "railwaycrossing" or "levelcrossing" => AlertTypes.RailwayCrossing,
        "gogiamtoc" or "vachgiamtoc" or "giamtoc" or "speedbump" => AlertTypes.SpeedBump,
        "khuvuctruonghoc" or "truonghoc" or "truong" or "schoolzone" or "school" => AlertTypes.SchoolZone,
        "khuvucbenhvien" or "benhvien" or "hospitalzone" or "hospital" => AlertTypes.HospitalZone,
        _ => null,
    };

    /// <summary>"21.028511, 105.854164" (copy tu Google Maps) / "21,0285; 105,8542" -> (lat, lng).</summary>
    public static (double Lat, double Lng)? ParseCoords(string value)
    {
        string[] parts =
            value.Contains(';') ? value.Split(';')
            : Regex.Matches(value, ",").Count == 1 ? value.Split(',')
            : value.Split(", ");                       // "21,0285, 105,8542": phay thap phan + ", " ngan cach
        if (parts.Length != 2) return null;
        return ParseNumber(parts[0]) is { } lat && ParseNumber(parts[1]) is { } lng ? (lat, lng) : null;
    }

    /// <summary>"21.0285" va "21,0285" (Excel tieng Viet) deu doc duoc.</summary>
    public static double? ParseNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Contains(',') && !v.Contains('.')) v = v.Replace(',', '.');
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static int? LeadingInt(string value)
    {
        var m = Regex.Match(value, @"^\s*(\d+)");
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    /// <summary>Bo dau, chu thuong, bo khoang trang / ky tu dac biet: "Vĩ độ (lat)" -> "vidolat".</summary>
    public static string Simplify(string s)
    {
        var d = s.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
                sb.Append(c);
        return sb.ToString();
    }
}
