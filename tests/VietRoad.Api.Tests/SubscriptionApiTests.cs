using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VietRoad.Api.Admin;
using VietRoad.Api.Auth;
using VietRoad.Api.Endpoints;
using VietRoad.Api.Models;
using VietRoad.Api.Subscriptions;
using VietRoad.Data;

namespace VietRoad.Api.Tests;

public class SubscriptionApiTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    // Vi tri ngau nhien o Tay Nguyen, xa du lieu mau va cac lop test khac.
    private static (double Lat, double Lng) RandomSpot() =>
        (12 + Random.Shared.NextDouble(), 108 + Random.Shared.NextDouble());

    private async Task<(HttpClient Client, Guid UserId)> NewClientAsync(bool admin = false)
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
        return (client, reg.User.Id);
    }

    private static Task<SubscriptionInfo?> MySubscriptionAsync(HttpClient c) =>
        c.GetFromJsonAsync<SubscriptionInfo>("/api/v1/users/me/subscription");

    private static Task<HttpResponseMessage> GrantAsync(HttpClient admin, Guid userId, int days, string plan = Plans.Premium) =>
        admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/subscriptions", new GrantRequest(plan, days, "test"));

    [Fact]
    public async Task NewUser_IsFree_WithNoPaidFeatures()
    {
        var (user, _) = await NewClientAsync();
        var sub = await MySubscriptionAsync(user);

        Assert.Equal(Plans.Free, sub!.Plan);
        Assert.Null(sub.ExpiresAt);
        Assert.Empty(sub.Features);
    }

    [Fact]
    public async Task AdminGrant_MakesUserPremium_AndExtensionsStack()
    {
        var (admin, _) = await NewClientAsync(admin: true);
        var (user, userId) = await NewClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await GrantAsync(admin, userId, 30)).StatusCode);
        var sub = await MySubscriptionAsync(user);
        Assert.Equal(Plans.Premium, sub!.Plan);
        Assert.Contains(Features.Navigation, sub.Features);
        Assert.Contains(Features.PoliceAlerts, sub.Features);
        Assert.InRange((sub.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalDays, 29.9, 30.1);

        // Gia han them 30 ngay: noi tiep, khong mat ngay nao -> ~60 ngay.
        await GrantAsync(admin, userId, 30);
        sub = await MySubscriptionAsync(user);
        Assert.InRange((sub!.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalDays, 59.9, 60.1);

        // Admin xem: 2 lan kich hoat, 1 dang hieu luc + 1 cho toi luot.
        var history = await admin.GetFromJsonAsync<UserSubscriptions>($"/api/v1/admin/users/{userId}/subscriptions");
        Assert.Equal(["upcoming", "active"], history!.History.Select(h => h.Status).ToArray());

        // Danh sach nguoi dung cua admin hien han Premium.
        var users = await admin.GetFromJsonAsync<PagedResult<AdminUser>>("/api/v1/admin/users?pageSize=100");
        Assert.NotNull(users!.Items.Single(u => u.Id == userId).PremiumUntil);
    }

    [Fact]
    public async Task Cancel_ReturnsUserToFree()
    {
        var (admin, _) = await NewClientAsync(admin: true);
        var (user, userId) = await NewClientAsync();
        var record = await (await GrantAsync(admin, userId, 30)).Content.ReadFromJsonAsync<SubscriptionRecord>();

        var cancel = await admin.PostAsync($"/api/v1/admin/subscriptions/{record!.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(Plans.Free, (await MySubscriptionAsync(user))!.Plan);
    }

    [Fact]
    public async Task Grant_Validation_And_OnlyAdmin()
    {
        var (admin, _) = await NewClientAsync(admin: true);
        var (user, userId) = await NewClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await GrantAsync(admin, userId, 30, plan: "gold")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await GrantAsync(admin, userId, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GrantAsync(admin, Guid.NewGuid(), 30)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GrantAsync(user, userId, 30)).StatusCode); // tu tang minh
    }

    [Fact]
    public async Task PoliceCheckpoints_OnlyVisibleToPremium()
    {
        var spot = RandomSpot();
        var (reporter, _) = await NewClientAsync();
        var report = await reporter.PostAsJsonAsync("/api/v1/reports",
            new ReportRequest(AlertTypes.PoliceCheckpoint, spot.Lat, spot.Lng, null, null));
        Assert.Equal(HttpStatusCode.Created, report.StatusCode); // goi Mien phi van BAO CAO duoc

        var url = $"/api/v1/alerts/nearby?lat={spot.Lat}&lng={spot.Lng}&radius=500";
        int PoliceCount(NearbyAlertsResponse r) => r.Alerts.Count(a => a.Alert.Type == AlertTypes.PoliceCheckpoint);

        // Khong dang nhap / goi Mien phi: khong thay.
        Assert.Equal(0, PoliceCount((await factory.CreateClient().GetFromJsonAsync<NearbyAlertsResponse>(url))!));
        Assert.Equal(0, PoliceCount((await reporter.GetFromJsonAsync<NearbyAlertsResponse>(url))!));

        // Premium: thay.
        var (premium, premiumId) = await NewClientAsync();
        await factory.GrantPremiumAsync(premiumId);
        Assert.Equal(1, PoliceCount((await premium.GetFromJsonAsync<NearbyAlertsResponse>(url))!));
    }

    [Fact]
    public async Task Nearby_WithInvalidToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-sai");
        var res = await client.GetAsync("/api/v1/alerts/nearby?lat=21.03&lng=105.85");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
