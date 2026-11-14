using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VietRoad.Data;
using VietRoad.Importer;
using VietRoad.Importer.Osm;

// Cong cu dong lenh cho doi van hanh. Cach dung:
//   dotnet run --project src/VietRoad.Importer                        -> nhap camera tu OSM vao database
//   dotnet run --project src/VietRoad.Importer -- --dry-run           -> chi in ra, khong ghi database
//   dotnet run --project src/VietRoad.Importer -- make-admin a@b.com  -> cap quyen admin cho tai khoan

var dryRun = args.Contains("--dry-run");
var isMakeAdmin = args.Length >= 2 && args[0] == "make-admin";

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
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
// Overpass yeu cau User-Agent de biet ai dang goi.
http.DefaultRequestHeaders.UserAgent.ParseAdd("VietRoad-Importer/0.1");

Console.WriteLine($"Dang tai du lieu tu {overpassEndpoint} ...");
var osm = await new OverpassClient(http).FetchVietnamCamerasAsync(overpassEndpoint);

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
