using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VietRoad.Data;
using VietRoad.Importer;
using VietRoad.Importer.Osm;

// Cach dung:
//   dotnet run --project src/VietRoad.Importer               -> nhap vao database
//   dotnet run --project src/VietRoad.Importer -- --dry-run  -> chi in ra, khong ghi database

var dryRun = args.Contains("--dry-run");

// ===== 1. Doc cau hinh =====
// Thu tu uu tien (sau de len truoc): User Secrets < bien moi truong < tham so dong lenh.
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .AddCommandLine(args.Where(a => a != "--dry-run").ToArray())
    .Build();

var overpassEndpoint = config["Overpass:Endpoint"] ?? OverpassClient.DefaultEndpoint;

// ===== 2. Tai du lieu tu OpenStreetMap =====
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
// Overpass yeu cau User-Agent de biet ai dang goi.
http.DefaultRequestHeaders.UserAgent.ParseAdd("VietRoad-Importer/0.1");

Console.WriteLine($"Dang tai du lieu tu {overpassEndpoint} ...");
var osm = await new OverpassClient(http).FetchVietnamCamerasAsync(overpassEndpoint);

// ===== 3. Chuyen sang dang cua VietRoad =====
var result = OsmAlertMapper.Map(osm);
Console.WriteLine($"OSM: {osm.Elements.Count} phan tu -> {result.Alerts.Count} canh bao, bo qua {result.Skipped}.");

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
var connectionString = config.GetConnectionString("VietRoadDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Thieu ConnectionStrings:VietRoadDb (dat bang dotnet user-secrets).");
    return 1;
}

var optionsBuilder = new DbContextOptionsBuilder<VietRoadDbContext>();
optionsBuilder.UseVietRoadDb(connectionString);

await using var db = new VietRoadDbContext(optionsBuilder.Options);

// Importer khong tu tao bang; viec do la cua migration (API chay khi khoi dong).
var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count > 0)
{
    Console.Error.WriteLine($"Database chua cap nhat ({string.Join(", ", pending)}). Hay chay API mot lan de migrate.");
    return 1;
}

var stats = await new AlertImporter(db).ImportOsmAsync(result.Alerts);
Console.WriteLine($"Xong: them {stats.Added}, cap nhat {stats.Updated}, giu nguyen {stats.Unchanged}, an {stats.Deactivated}.");
return 0;
