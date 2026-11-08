using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VietRoad.Api.Admin;
using VietRoad.Api.Auth;
using VietRoad.Api.Endpoints;
using VietRoad.Data;

namespace VietRoad.Api.Tests;

public class AdminApiTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private static (double Lat, double Lng) RandomSpot() =>
        (17 + Random.Shared.NextDouble(), 106 + Random.Shared.NextDouble());

    /// <summary>Dang ky user moi; neu admin=true thi cap quyen thang trong database roi DANG NHAP LAI
    /// (role nam trong token, token cu van la "user").</summary>
    private async Task<(HttpClient Client, Guid UserId)> NewClientAsync(bool admin)
    {
        var email = $"u-{Guid.NewGuid():N}@example.com";
        var client = factory.CreateClient();
        var reg = await (await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "matkhau123", null))).Content.ReadFromJsonAsync<AuthResponse>();

        if (admin)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VietRoadDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.Role = UserRoles.Admin;
            await db.SaveChangesAsync();

            reg = await (await client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest(email, "matkhau123"))).Content.ReadFromJsonAsync<AuthResponse>();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg!.AccessToken);
        return (client, reg.User.Id);
    }

    private async Task<int> UserReportAsync()
    {
        var (user, _) = await NewClientAsync(admin: false);
        var spot = RandomSpot();
        var res = await user.PostAsJsonAsync("/api/v1/reports",
            new ReportRequest(AlertTypes.SpeedCamera, spot.Lat, spot.Lng, null, 60));
        return (await res.Content.ReadFromJsonAsync<ReportResponse>())!.Alert.Id;
    }

    [Fact]
    public async Task AdminEndpoints_WithoutLogin_401_AsNormalUser_403()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/admin/stats")).StatusCode);

        var (user, _) = await NewClientAsync(admin: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/admin/stats")).StatusCode);
    }

    [Fact]
    public async Task PendingQueue_Approve_RemovesFromQueue_AndRaisesConfidence()
    {
        var id = await UserReportAsync();
        var (admin, _) = await NewClientAsync(admin: true);

        var pending = await admin.GetFromJsonAsync<PagedResult<AdminAlert>>("/api/v1/admin/alerts?status=pending&pageSize=100");
        Assert.Contains(pending!.Items, a => a.Id == id);

        var approved = await (await admin.PostAsync($"/api/v1/admin/alerts/{id}/approve", null))
            .Content.ReadFromJsonAsync<AdminAlert>();
        Assert.NotNull(approved!.VerifiedAt);
        Assert.Equal(100, approved.Confidence); // 90 (da duyet) + 10 (nguoi bao)

        pending = await admin.GetFromJsonAsync<PagedResult<AdminAlert>>("/api/v1/admin/alerts?status=pending&pageSize=100");
        Assert.DoesNotContain(pending!.Items, a => a.Id == id);
    }

    [Fact]
    public async Task Reject_HidesAlertFromDrivers()
    {
        var id = await UserReportAsync();
        var (admin, _) = await NewClientAsync(admin: true);

        var rejected = await (await admin.PostAsync($"/api/v1/admin/alerts/{id}/reject", null))
            .Content.ReadFromJsonAsync<AdminAlert>();
        Assert.False(rejected!.IsActive);
        Assert.False(rejected.IsVisible);
    }

    [Fact]
    public async Task CreateAlert_AsAdmin_IsVerifiedWithHighConfidence()
    {
        var (admin, _) = await NewClientAsync(admin: true);
        var spot = RandomSpot();

        var res = await admin.PostAsJsonAsync("/api/v1/admin/alerts",
            new AlertUpsertRequest(AlertTypes.RedLightCamera, "Den do Nga Tu So", spot.Lat, spot.Lng, null, 180, null));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var alert = (await res.Content.ReadFromJsonAsync<AdminAlert>())!;
        Assert.Equal(AlertSources.Admin, alert.Source);
        Assert.Equal(90, alert.Confidence);
        Assert.True(alert.IsVisible);
    }

    [Fact]
    public async Task UpdateOsmAlert_OnlyIsActiveAllowed()
    {
        var (admin, _) = await NewClientAsync(admin: true);

        // Alert mau id=2 co source = osm.
        var move = await admin.PutAsJsonAsync("/api/v1/admin/alerts/2",
            new AlertUpsertRequest(null, null, 21.0, 105.0, null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);

        var disable = await admin.PutAsJsonAsync("/api/v1/admin/alerts/2",
            new AlertUpsertRequest(null, null, null, null, null, null, IsActive: false));
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        Assert.False((await disable.Content.ReadFromJsonAsync<AdminAlert>())!.IsActive);
    }

    [Fact]
    public async Task UpdateUserAlert_ChangesFields()
    {
        var id = await UserReportAsync();
        var (admin, _) = await NewClientAsync(admin: true);

        var res = await admin.PutAsJsonAsync($"/api/v1/admin/alerts/{id}",
            new AlertUpsertRequest(null, "Camera da sua ten", null, null, 80, 45, null));
        var alert = (await res.Content.ReadFromJsonAsync<AdminAlert>())!;

        Assert.Equal("Camera da sua ten", alert.Name);
        Assert.Equal(80, alert.SpeedLimit);
        Assert.Equal(45, alert.Direction);
        Assert.NotNull(alert.CreatedByEmail);
    }

    [Fact]
    public async Task SetRole_PromotesUser_ButCannotDemoteSelf()
    {
        var (admin, adminId) = await NewClientAsync(admin: true);
        var (_, userId) = await NewClientAsync(admin: false);

        var promote = await admin.PutAsJsonAsync($"/api/v1/admin/users/{userId}/role", new RoleChangeRequest("admin"));
        Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);

        var selfDemote = await admin.PutAsJsonAsync($"/api/v1/admin/users/{adminId}/role", new RoleChangeRequest("user"));
        Assert.Equal(HttpStatusCode.BadRequest, selfDemote.StatusCode);
    }

    [Fact]
    public async Task Stats_And_ListUsers_Work()
    {
        await UserReportAsync();
        var (admin, _) = await NewClientAsync(admin: true);

        var stats = await admin.GetFromJsonAsync<AdminStats>("/api/v1/admin/stats");
        Assert.True(stats!.PendingReports >= 1);
        Assert.True(stats.Admins >= 1);
        Assert.True(stats.VisibleBySource.ContainsKey(AlertSources.Osm));

        var users = await admin.GetFromJsonAsync<PagedResult<AdminUser>>("/api/v1/admin/users?pageSize=5");
        Assert.True(users!.Total >= 2);
        Assert.True(users.Items.Count <= 5);
    }

    [Fact]
    public async Task ListAlerts_InvalidStatus_Returns400()
    {
        var (admin, _) = await NewClientAsync(admin: true);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/alerts?status=abc")).StatusCode);
    }
}
