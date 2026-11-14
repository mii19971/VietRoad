using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VietRoad.Api.Admin;
using VietRoad.Api.Auth;
using VietRoad.Api.Endpoints;
using VietRoad.Api.Routing;
using VietRoad.Api.Services;
using VietRoad.Api.Subscriptions;
using VietRoad.Data;

// ===== 1. Cau hinh (truoc khi app chay) =====
var builder = WebApplication.CreateBuilder(args);

// Doc chuoi ket noi tu User Secrets / appsettings / bien moi truong ConnectionStrings__VietRoadDb.
var connectionString = builder.Configuration.GetConnectionString("VietRoadDb");
var useDatabase = !string.IsNullOrWhiteSpace(connectionString);

if (useDatabase)
{
    // Dang ky DbContext dung PostgreSQL (cau hinh chung nam trong VietRoad.Data).
    builder.Services.AddDbContext<VietRoadDbContext>(options =>
        options.UseVietRoadDb(connectionString!));

    // Scoped = moi request 1 instance (vi DbContext khong dung chung giua cac request).
    builder.Services.AddScoped<IAlertService, DbAlertService>();
}
else
{
    // Chua co database -> canh bao dung du lieu mau trong bo nho (chi de chay thu).
    // User Service va User Reports van can DbContext; test tu dang ky DbContext rieng.
    builder.Services.AddSingleton<IAlertService, InMemoryAlertService>();
}

// --- User Reports + Admin ---
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<AlertImportService>();

// --- User Service + JWT ---
// Doc muc "Jwt" thanh JwtOptions, kiem tra ngay khi khoi dong: thieu khoa -> dung luon, bao loi ro rang.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey phai dai it nhat 32 ky tu (dat bang dotnet user-secrets).")
    .ValidateOnStart();

// Xac thuc (authentication) = "ban la ai?": doc header "Authorization: Bearer <JWT>" va kiem tra token.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.MapInboundClaims = false; // giu nguyen ten claim "sub", "role"... nhu luc tao token
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,              // dung server minh phat hanh?
            ValidAudience = jwt.Audience,          // dung danh cho app VietRoad?
            IssuerSigningKey = jwt.GetSigningKey(), // chu ky dung? (khong bi sua)
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),  // cho phep lech dong ho toi da 30 giay
        };
    });

// Phan quyen (authorization) = "ban duoc lam gi?". Dung cho RequireAuthorization().
// Policy "Admin": token phai co claim role = "admin".
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminEndpoints.AdminPolicy, p => p.RequireRole(UserRoles.Admin));

builder.Services.AddSingleton(TimeProvider.System); // lay gio hien tai (test co the thay bang gio gia)
builder.Services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AuthService>();

// --- Routing Service (chi duong) ---
// AddHttpClient<Interface, Class>: "typed client" - .NET tao HttpClient da cau hinh san
// (dia chi, User-Agent, thoi gian cho) va quan ly ket noi giup (khong tu new HttpClient).
builder.Services.AddOptions<RoutingOptions>().Bind(builder.Configuration.GetSection(RoutingOptions.SectionName));
builder.Services.AddHttpClient<IRouteProvider, OsrmRouteProvider>((sp, http) =>
{
    var o = sp.GetRequiredService<IOptions<RoutingOptions>>().Value;
    http.BaseAddress = new Uri(o.OsrmBaseUrl);
    http.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
    http.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<IPlaceSearch, NominatimPlaceSearch>((sp, http) =>
{
    var o = sp.GetRequiredService<IOptions<RoutingOptions>>().Value;
    http.BaseAddress = new Uri(o.NominatimBaseUrl);
    http.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
    http.Timeout = TimeSpan.FromSeconds(10);
});

// Nen JSON tra ve (gzip / brotli) neu app chap nhan: du lieu gioi han toc do quanh xe co the
// ~200 KB, nen con ~30 KB -> tiet kiem 4G cho tai xe. OkHttp (app Android) tu giai nen.
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

// Tra loi theo chuan ProblemDetails khi co loi bat ngo.
builder.Services.AddProblemDetails();

var app = builder.Build();

app.Logger.LogInformation("Alert data source: {Source}", useDatabase ? "PostgreSQL" : "In-memory");

// Khi dev: tu tao/cap nhat bang trong database theo cac migration.
if (useDatabase && app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VietRoadDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex) when (ex is Npgsql.NpgsqlException or TimeoutException)
    {
        // Bao ro rang thay vi chi co stack trace dai: API khong chay duoc khi thieu database.
        app.Logger.LogCritical(
            "KHONG KET NOI DUOC DATABASE ({Host}). Kiem tra mang / VPN, hoac chuoi ket noi trong User Secrets. Loi: {Error}",
            db.Database.GetDbConnection().DataSource, ex.Message);
        throw;
    }
}

// ===== 2. Pipeline xu ly request (API Gateway) =====
// Bat moi exception de server khong sap:
//  - Request sai (vd: lat=abc, thieu lat, JSON hong) -> giu ma 400 cua no
//  - Loi bat ngo khac                                -> 500
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex is BadHttpRequestException bad
        ? bad.StatusCode
        : StatusCodes.Status500InternalServerError,
});

app.UseResponseCompression();
app.UseAuthentication(); // doc token -> biet user la ai
app.UseAuthorization();  // chan endpoint RequireAuthorization neu chua dang nhap

// Kiem tra server con song.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    dataSource = useDatabase ? "postgres" : "in-memory",
    time = DateTimeOffset.UtcNow,
}));

// Cac API cua tung service.
app.MapAlertEndpoints();
app.MapAuthEndpoints();
app.MapReportEndpoints();
app.MapAdminEndpoints();
app.MapRoutingEndpoints();
app.MapSubscriptionEndpoints();
app.MapSpeedLimitEndpoints();

// ===== 3. Chay server =====
app.Run();
