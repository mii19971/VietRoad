using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VietRoad.Api.Admin;
using VietRoad.Api.Auth;
using VietRoad.Data;

namespace VietRoad.Api.Tests;

// ---------- Doc tung dong (ham thuan) ----------

public class AlertImportParseTests
{
    private static AlertImportRow Parse(params (string Col, string? Val)[] cells) =>
        AlertImportService.ParseRow(cells.ToDictionary(c => c.Col, c => c.Val), 2);

    [Theory]
    [InlineData("Vĩ độ", "lat")]
    [InlineData("vi do", "lat")]
    [InlineData("Latitude", "lat")]
    [InlineData("Vĩ độ (Latitude)", "lat")]
    [InlineData("KINH ĐỘ", "lng")]
    [InlineData("Tọa độ", "coords")]
    [InlineData("Giới hạn tốc độ (km/h)", "speed")]
    [InlineData("Loại", "type")]
    [InlineData("STT", null)]
    [InlineData("Ghi chú", null)]
    public void ColumnOf(string header, string? expected) => Assert.Equal(expected, AlertImportService.ColumnOf(header));

    [Theory]
    [InlineData("Camera tốc độ", AlertTypes.SpeedCamera)]
    [InlineData("camera toc do", AlertTypes.SpeedCamera)]
    [InlineData("Đèn đỏ", AlertTypes.RedLightCamera)]
    [InlineData("Camera phạt nguội", AlertTypes.TrafficCamera)]
    [InlineData("Trạm thu phí", AlertTypes.TollBooth)]
    [InlineData("Đường ngang", AlertTypes.RailwayCrossing)]
    [InlineData("Gờ giảm tốc", AlertTypes.SpeedBump)]
    [InlineData("speed_camera", AlertTypes.SpeedCamera)]
    [InlineData("Chốt CSGT", null)]   // khong nhap chot CSGT tu file
    public void TypeOf(string value, string? expected) => Assert.Equal(expected, AlertImportService.TypeOf(value));

    [Theory]
    [InlineData("21.028511, 105.854164", 21.028511, 105.854164)]  // copy tu Google Maps
    [InlineData("21,0285; 105,8542", 21.0285, 105.8542)]           // Excel tieng Viet
    [InlineData("21,0285, 105,8542", 21.0285, 105.8542)]
    public void ParseCoords(string value, double lat, double lng) =>
        Assert.Equal((lat, lng), AlertImportService.ParseCoords(value));

    [Fact]
    public void ValidRow_WithSeparateLatLng_AndCommaDecimal()
    {
        var r = Parse(("Loại", "Camera tốc độ"), ("Tên", "Camera QL1A km 50"), ("Vĩ độ", "20,95"), ("Kinh độ", "105,95"),
            ("Giới hạn tốc độ", "80 km/h"), ("Hướng", "180"));

        Assert.Equal("ok", r.Status);
        Assert.Equal(AlertTypes.SpeedCamera, r.Type);
        Assert.Equal("Camera QL1A km 50", r.Name);
        Assert.Equal((20.95, 105.95), (r.Lat!.Value, r.Lng!.Value));
        Assert.Equal(80, r.SpeedLimit);
        Assert.Equal(180, r.Direction);
    }

    [Fact]
    public void MissingName_UsesDefault_AndSpeedIgnoredForTollBooth()
    {
        var r = Parse(("Loại", "Trạm thu phí"), ("Tọa độ", "10.9, 107.1"), ("Giới hạn tốc độ", "60"));
        Assert.Equal("ok", r.Status);
        Assert.Equal("Trạm thu phí", r.Name);
        Assert.Null(r.SpeedLimit);
    }

    [Fact]
    public void SwappedLatLng_GivesHelpfulError()
    {
        var r = Parse(("Loại", "Camera tốc độ"), ("Vĩ độ", "105.85"), ("Kinh độ", "21.03"));
        Assert.Equal("error", r.Status);
        Assert.Contains("đảo ngược", r.Message);
    }

    [Fact]
    public void BadValues_AllErrorsListed()
    {
        var r = Parse(("Loại", "xe buýt"), ("Tọa độ", "abc"), ("Giới hạn tốc độ", "500"));
        Assert.Equal("error", r.Status);
        Assert.Contains("Không hiểu loại", r.Message);
        Assert.Contains("Tọa độ", r.Message);
        Assert.Contains("Giới hạn tốc độ", r.Message);
    }

    [Fact]
    public void OutsideVietnam_IsError() =>
        Assert.Contains("ngoài Việt Nam", Parse(("Loại", "Camera tốc độ"), ("Tọa độ", "37.42, -122.08")).Message);
}

// ---------- API ----------

public class AlertImportApiTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private async Task<HttpClient> ClientAsync(bool admin)
    {
        var email = $"u-{Guid.NewGuid():N}@example.com";
        var client = factory.CreateClient();
        var reg = await (await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "matkhau123", null))).Content.ReadFromJsonAsync<AuthResponse>();
        if (admin)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VietRoadDbContext>();
            (await db.Users.FirstAsync(u => u.Email == email)).Role = UserRoles.Admin;
            await db.SaveChangesAsync();
            reg = await (await client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest(email, "matkhau123"))).Content.ReadFromJsonAsync<AuthResponse>();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg!.AccessToken);
        return client;
    }

    private static Dictionary<string, string?> Row(string type, string coords, string? name = null) =>
        new() { ["Loại"] = type, ["Tọa độ"] = coords, ["Tên"] = name, ["STT"] = "1" };

    private static async Task<AlertImportResult> ImportAsync(HttpClient c, bool dryRun, params Dictionary<string, string?>[] rows)
    {
        var res = await c.PostAsJsonAsync("/api/v1/admin/alerts/import", new AlertImportRequest([.. rows], dryRun));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AlertImportResult>())!;
    }

    // Vi tri ngau nhien o Dong Nam Bo, xa du lieu mau va lop test khac.
    private static string RandomCoords(out double lat, out double lng)
    {
        lat = 11 + Random.Shared.NextDouble() * 0.5;
        lng = 106.5 + Random.Shared.NextDouble() * 0.5;
        return FormattableString.Invariant($"{lat:F6}, {lng:F6}");
    }

    [Fact]
    public async Task DryRun_ReportsStatuses_WithoutWriting()
    {
        var admin = await ClientAsync(admin: true);
        var a = RandomCoords(out var lat, out var lng);
        var nearA = FormattableString.Invariant($"{lat + 0.0001:F6}, {lng:F6}");   // ~11 m -> trung dong tren

        var r = await ImportAsync(admin, dryRun: true,
            Row("Camera tốc độ", a, "Cam 1"),
            Row("Camera tốc độ", nearA, "Cam 1 ve lai"),
            Row("Camera tốc độ", "21.0285, 105.8542"),   // trung camera mau #1 (Ho Hoan Kiem) trong database
            Row("xe buyt", a));

        Assert.Equal((4, 1, 2, 1, 0), (r.Total, r.Valid, r.Duplicates, r.Errors, r.Created));
        Assert.Equal(["ok", "duplicate", "duplicate", "error"], r.Rows.Select(x => x.Status).ToArray());
        Assert.Equal(1, r.Rows[2].ExistingId);
        Assert.Contains("dòng 2", r.Rows[1].Message);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VietRoadDbContext>();
        Assert.False(await db.Alerts.AnyAsync(x => x.Name == "Cam 1")); // dry run: chua ghi gi
    }

    [Fact]
    public async Task Import_CreatesVerifiedAdminAlerts_AndSecondRunFindsDuplicates()
    {
        var admin = await ClientAsync(admin: true);
        var coords = RandomCoords(out _, out _);

        var first = await ImportAsync(admin, dryRun: false, Row("Đèn đỏ", coords, "Ngã tư test import"));
        Assert.Equal(1, first.Created);
        var id = first.Rows.Single().CreatedId!.Value;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VietRoadDbContext>();
            var a = await db.Alerts.SingleAsync(x => x.Id == id);
            Assert.Equal((AlertTypes.RedLightCamera, AlertSources.Admin, 90), (a.Type, a.Source, a.Confidence));
            Assert.NotNull(a.VerifiedAt);
        }

        // Nhap lai cung file: khong tao trung.
        var second = await ImportAsync(admin, dryRun: false, Row("Đèn đỏ", coords, "Ngã tư test import"));
        Assert.Equal((0, 1), (second.Created, second.Duplicates));
        Assert.Equal(id, second.Rows.Single().ExistingId);
    }

    [Fact]
    public async Task Import_OnlyAdmin_AndLimits()
    {
        var user = await ClientAsync(admin: false);
        var res = await user.PostAsJsonAsync("/api/v1/admin/alerts/import", new AlertImportRequest([Row("Camera tốc độ", "21.1, 105.9")], true));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var admin = await ClientAsync(admin: true);
        var empty = await admin.PostAsJsonAsync("/api/v1/admin/alerts/import", new AlertImportRequest([], true));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var tooMany = Enumerable.Range(0, AlertImportService.MaxRows + 1).Select(_ => Row("Camera tốc độ", "21.1, 105.9")).ToList();
        var big = await admin.PostAsJsonAsync("/api/v1/admin/alerts/import", new AlertImportRequest(tooMany, true));
        Assert.Equal(HttpStatusCode.BadRequest, big.StatusCode);
    }
}
