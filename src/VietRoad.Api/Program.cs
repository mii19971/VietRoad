using Microsoft.EntityFrameworkCore;
using VietRoad.Api.Data;
using VietRoad.Api.Endpoints;
using VietRoad.Api.Services;

// ===== 1. Cau hinh (truoc khi app chay) =====
var builder = WebApplication.CreateBuilder(args);

// Doc chuoi ket noi tu appsettings.{Environment}.json hoac bien moi truong
// ConnectionStrings__VietRoadDb.
var connectionString = builder.Configuration.GetConnectionString("VietRoadDb");
var useDatabase = !string.IsNullOrWhiteSpace(connectionString);

if (useDatabase)
{
    // Dang ky DbContext dung PostgreSQL.
    // Bang lich su migration cung dat trong schema "vietroad" de khong lan voi du an khac.
    builder.Services.AddDbContext<VietRoadDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", VietRoadDbContext.Schema)));

    // Scoped = moi request 1 instance (vi DbContext khong dung chung giua cac request).
    builder.Services.AddScoped<IAlertService, PostgresAlertService>();
}
else
{
    // Chua co database -> dung du lieu mau trong bo nho.
    builder.Services.AddSingleton<IAlertService, InMemoryAlertService>();
}

// Tra loi theo chuan ProblemDetails khi co loi bat ngo.
builder.Services.AddProblemDetails();

var app = builder.Build();

app.Logger.LogInformation("Alert data source: {Source}", useDatabase ? "PostgreSQL" : "In-memory");

// Khi dev: tu tao/cap nhat bang trong database theo cac migration.
if (useDatabase && app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VietRoadDbContext>();
    await db.Database.MigrateAsync();
}

// ===== 2. Pipeline xu ly request (API Gateway) =====
// Bat moi exception de server khong sap:
//  - Request sai (vd: lat=abc, thieu lat) -> giu ma 400 cua no
//  - Loi bat ngo khac                     -> 500
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex is BadHttpRequestException bad
        ? bad.StatusCode
        : StatusCodes.Status500InternalServerError,
});

// Kiem tra server con song.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    dataSource = useDatabase ? "postgres" : "in-memory",
    time = DateTimeOffset.UtcNow,
}));

// Cac API cua Alert Service.
app.MapAlertEndpoints();

// ===== 3. Chay server =====
app.Run();
