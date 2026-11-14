using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VietRoad.Data;
using VietRoad.Importer;
using VietRoad.Importer.Osm;

// Cong cu dong lenh cho doi van hanh. Cach dung:
//   dotnet run --project src/VietRoad.Importer                        -> nhap camera tu OSM vao database
//   dotnet run --project src/VietRoad.Importer -- --dry-run           -> chi in ra, khong ghi database
//   dotnet run --project src/VietRoad.Importer -- make-admin a@b.com  -> cap quyen admin cho tai khoan
//   dotnet run --project src/VietRoad.Importer -- speed-limits        -> nhap gioi han toc do duong tu OSM

var dryRun = args.Contains("--dry-run");
var isMakeAdmin = args.Length >= 2 && args[0] == "make-admin";
var isSpeedLimits = args.Length >= 1 && args[0] == "speed-limits";

// ===== 1. Doc cau hinh =====
// Thu tu uu tien (sau de len truoc): User Secrets < bien moi truong < tham so dong lenh.
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .AddCommandLine(args.Where(a => a.StartsWith("--") && a != "--dry-run").ToArray())
    .Build();

if (isMakeAdmin)
{
    await using var adminDb = await OpenDatabaseAsync();
    if (adminDb is null) return 1;

    var email = args[1].Trim().ToLowerInvariant();
    var user = await adminDb.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null)
    {
        Console.Error.WriteLine($"Khong tim thay tai khoan {email}. Hay dang ky truoc (POST /api/v1/auth/register).");
        return 1;
    }

    user.Role = UserRoles.Admin;
    await adminDb.SaveChangesAsync();
    Console.WriteLine($"Da cap quyen admin cho {email}. Dang nhap lai de nhan token moi co role admin.");
    return 0;
}

var overpassEndpoint = config["Overpass:Endpoint"] ?? OverpassClient.DefaultEndpoint;

// ===== 2. Tai du lieu tu OpenStreetMap =====
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(6) };
// Overpass yeu cau User-Agent de biet ai dang goi.
http.DefaultRequestHeaders.UserAgent.ParseAdd("VietRoad-Importer/0.1");

if (isSpeedLimits)
{
    // Tai tung loai duong; 1 loai loi -> dung han, KHONG ghi (tranh xoa nham du lieu loai do).
    var overpass = new OverpassClient(http);
    var roads = new List<ImportedRoadSpeed>();
    foreach (var group in OverpassClient.SpeedLimitHighwayGroups)
    {
        Console.Write($"Dang tai {group,-40} ");
        var response = await overpass.FetchSpeedLimitWaysAsync(overpassEndpoint, group);
        var (mapped, skippedWays) = SpeedLimitMapper.Map(response);
        roads.AddRange(mapped);
        Console.WriteLine($"{mapped.Count,6} doan, bo qua {skippedWays} (maxspeed khong phai so)");
    }

    Console.WriteLine($"Tong {roads.Count} doan duong, {roads.Sum(r => r.Points.Count)} diem.");
    foreach (var g in roads.GroupBy(r => r.MaxSpeed).OrderByDescending(g => g.Count()).Take(8))
        Console.WriteLine($"  {g.Key,3} km/h: {g.Count()} doan");
    if (dryRun) { Console.WriteLine("--dry-run: khong ghi database."); return 0; }

    await using var speedDb = await OpenDatabaseAsync();
    if (speedDb is null) return 1;
    var s = await new SpeedLimitImporter(speedDb).ImportAsync(roads);
    Console.WriteLine($"Xong: them {s.Added}, cap nhat {s.Updated}, giu nguyen {s.Unchanged}, xoa {s.Deactivated}.");
    return 0;
}

Console.WriteLine($"Dang tai du lieu tu {overpassEndpoint} ...");
var overpassClient = new OverpassClient(http);
var cameraData = await overpassClient.FetchVietnamCamerasAsync(overpassEndpoint);
// Truy van rieng (gop chung 1 lan qua nang, de bi 504). Loi -> dung han, KHONG ghi: neu chi co
// camera ma ghi vao, buoc "an diem khong con tren OSM" se an nham het duong ngang / go giam toc.
var hazardData = await overpassClient.FetchVietnamHazardsAsync(overpassEndpoint);
var osm = new OsmResponse([.. cameraData.Elements, .. hazardData.Elements]);

// ===== 3. Chuyen sang dang cua VietRoad =====
var result = OsmAlertMapper.Map(osm);
Console.WriteLine($"OSM: {osm.Elements.Count} phan tu -> {result.Alerts.Count} canh bao, bo qua {result.Skipped}, gop trung {result.Merged}.");

foreach (var group in result.Alerts.GroupBy(a => a.Type))
    Console.WriteLine($"  {group.Key,-22} {group.Count()}");

if (dryRun)
{
    foreach (var a in result.Alerts.Take(10))
        Console.WriteLine($"  {a.ExternalId,-22} {a.Type,-20} {a.Lat:F5},{a.Lng:F5}  max={a.SpeedLimit} dir={a.Direction}  {a.Name}");
    Console.WriteLine("--dry-run: khong ghi database.");
    return 0;
}

// ===== 4. Ghi vao database =====
await using var db = await OpenDatabaseAsync();
if (db is null) return 1;

var stats = await new AlertImporter(db).ImportOsmAsync(result.Alerts);
Console.WriteLine($"Xong: them {stats.Added}, cap nhat {stats.Updated}, giu nguyen {stats.Unchanged}, an {stats.Deactivated}.");
return 0;

// Mo ket noi database; null = loi (da in thong bao).
async Task<VietRoadDbContext?> OpenDatabaseAsync()
{
    var connectionString = config.GetConnectionString("VietRoadDb");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Console.Error.WriteLine("Thieu ConnectionStrings:VietRoadDb (dat bang dotnet user-secrets).");
        return null;
    }

    var optionsBuilder = new DbContextOptionsBuilder<VietRoadDbContext>();
    optionsBuilder.UseVietRoadDb(connectionString);
    var db = new VietRoadDbContext(optionsBuilder.Options);

    // Cong cu khong tu tao bang; viec do la cua migration (API chay khi khoi dong).
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        Console.Error.WriteLine($"Database chua cap nhat ({string.Join(", ", pending)}). Hay chay API mot lan de migrate.");
        await db.DisposeAsync();
        return null;
    }
    return db;
}
