using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using VietRoad.Api.Auth;
using VietRoad.Api.Endpoints;
using VietRoad.Api.Models;
using VietRoad.Data;

namespace VietRoad.Api.Tests;

public class ReportApiTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    // Moi test dung 1 vi tri ngau nhien o mien Trung, xa du lieu mau va xa nhau,
    // vi cac test trong lop dung chung 1 database.
    private static (double Lat, double Lng) RandomSpot() =>
        (14 + Random.Shared.NextDouble() * 2, 108 + Random.Shared.NextDouble());

    // Moi lan goi = 1 tai khoan moi, tra ve HttpClient da gan san token.
    private async Task<HttpClient> NewUserClientAsync()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest($"u-{Guid.NewGuid():N}@example.com", "matkhau123", null));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> ReportAsync(
        HttpClient c, string type, (double Lat, double Lng) at, double? direction = null, int? speedLimit = null) =>
        c.PostAsJsonAsync("/api/v1/reports", new ReportRequest(type, at.Lat, at.Lng, direction, speedLimit));

    private static Task<HttpResponseMessage> VoteAsync(HttpClient c, int alertId, string vote) =>
        c.PostAsJsonAsync($"/api/v1/alerts/{alertId}/votes", new VoteRequest(vote));

    private async Task<NearbyAlertsResponse> NearbyAsync((double Lat, double Lng) at) =>
        (await factory.CreateClient().GetFromJsonAsync<NearbyAlertsResponse>(
            $"/api/v1/alerts/nearby?lat={at.Lat}&lng={at.Lng}&radius=500"))!;

    [Fact]
    public async Task Report_WithoutLogin_Returns401()
    {
        var res = await ReportAsync(factory.CreateClient(), AlertTypes.SpeedCamera, RandomSpot());
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Report_NewCamera_IsCreatedAndVisibleNearby()
    {
        var spot = RandomSpot();
        var res = await ReportAsync(await NewUserClientAsync(), AlertTypes.SpeedCamera, spot, direction: 90, speedLimit: 60);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<ReportResponse>())!;
        Assert.True(body.Created);
        Assert.Equal(AlertSources.User, body.Alert.Source);
        Assert.Equal(1, body.Alert.ConfirmCount);   // nguoi bao tu tinh 1 xac nhan
        Assert.Equal(40, body.Alert.Confidence);    // 30 + 10
        Assert.Equal(60, body.Alert.SpeedLimit);

        var nearby = await NearbyAsync(spot);
        Assert.Contains(nearby.Alerts, a => a.Alert.Id == body.Alert.Id);
    }

    [Fact]
    public async Task Report_SameSpotByAnotherUser_IsMergedAsConfirmation()
    {
        var spot = RandomSpot();
        var first = (await (await ReportAsync(await NewUserClientAsync(), AlertTypes.SpeedCamera, spot, 90))
            .Content.ReadFromJsonAsync<ReportResponse>())!;

        // Cach ~20 m, huong lech 10 do -> cung 1 camera.
        var second = await ReportAsync(await NewUserClientAsync(), AlertTypes.SpeedCamera, (spot.Lat + 0.0002, spot.Lng), 100);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ReportResponse>())!;
        Assert.False(body.Created);
        Assert.Equal(first.Alert.Id, body.Alert.Id);
        Assert.Equal(2, body.Alert.ConfirmCount);
    }

    [Fact]
    public async Task Report_OppositeDirection_CreatesSeparateCamera()
    {
        var spot = RandomSpot();
        await ReportAsync(await NewUserClientAsync(), AlertTypes.SpeedCamera, spot, direction: 0);

        // Cung cho, huong nguoc lai (180) -> camera cua chieu kia.
        var res = await ReportAsync(await NewUserClientAsync(), AlertTypes.SpeedCamera, spot, direction: 180);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task ThreeDenies_HideAlert_ButTwoDoNot()
    {
        var spot = RandomSpot();
        var created = (await (await ReportAsync(await NewUserClientAsync(), AlertTypes.RedLightCamera, spot))
            .Content.ReadFromJsonAsync<ReportResponse>())!;
        var id = created.Alert.Id;

        await VoteAsync(await NewUserClientAsync(), id, "deny");
        await VoteAsync(await NewUserClientAsync(), id, "deny");
        Assert.Contains((await NearbyAsync(spot)).Alerts, a => a.Alert.Id == id); // 40 - 20 = 20: van hien

        await VoteAsync(await NewUserClientAsync(), id, "deny");
        Assert.DoesNotContain((await NearbyAsync(spot)).Alerts, a => a.Alert.Id == id); // 10: bi an
    }

    [Fact]
    public async Task VotingTwice_DoesNotDoubleCount_ChangingMindReplacesVote()
    {
        var spot = RandomSpot();
        var created = (await (await ReportAsync(await NewUserClientAsync(), AlertTypes.SpeedCamera, spot))
            .Content.ReadFromJsonAsync<ReportResponse>())!;
        var voter = await NewUserClientAsync();

        await VoteAsync(voter, created.Alert.Id, "confirm");
        var again = (await (await VoteAsync(voter, created.Alert.Id, "confirm")).Content.ReadFromJsonAsync<Alert>())!;
        Assert.Equal(2, again.ConfirmCount); // nguoi bao + voter, khong phai 3

        var changed = (await (await VoteAsync(voter, created.Alert.Id, "deny")).Content.ReadFromJsonAsync<Alert>())!;
        Assert.Equal(1, changed.ConfirmCount);
        Assert.Equal(1, changed.DenyCount);
    }

    [Fact]
    public async Task PoliceCheckpoint_ExpiresAfterAboutTwoHours()
    {
        var res = await ReportAsync(await NewUserClientAsync(), AlertTypes.PoliceCheckpoint, RandomSpot(), speedLimit: 50);
        var alert = (await res.Content.ReadFromJsonAsync<ReportResponse>())!.Alert;

        Assert.NotNull(alert.ExpiresAt);
        Assert.InRange(alert.ExpiresAt.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(119), TimeSpan.FromMinutes(121));
        Assert.Null(alert.SpeedLimit); // chot CSGT khong co gioi han toc do -> bo qua
    }

    [Fact]
    public async Task VoteOnOsmCamera_Works()
    {
        // Camera mau id=1 (source osm, do tin cay goc 50).
        var res = await VoteAsync(await NewUserClientAsync(), 1, "confirm");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await res.Content.ReadFromJsonAsync<Alert>())!.Confidence >= 60);
    }

    [Fact]
    public async Task DailyLimit_Returns429()
    {
        var client = await NewUserClientAsync();
        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.Created, (await ReportAsync(client, AlertTypes.SpeedCamera, RandomSpot())).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await ReportAsync(client, AlertTypes.SpeedCamera, RandomSpot())).StatusCode);
    }

    [Fact]
    public async Task Vote_UnknownAlert_Returns404()
    {
        var res = await VoteAsync(await NewUserClientAsync(), 999_999, "confirm");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Theory]
    [InlineData("ufo", 15.0, 108.0, null)]                 // loai khong hop le
    [InlineData("speed_camera", 95.0, 108.0, null)]        // lat sai
    [InlineData("speed_camera", 15.0, 108.0, 360.0)]       // huong sai
    public async Task Report_InvalidInput_Returns400(string type, double lat, double lng, double? direction)
    {
        var res = await ReportAsync(await NewUserClientAsync(), type, (lat, lng), direction);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Vote_InvalidValue_Returns400()
    {
        var res = await VoteAsync(await NewUserClientAsync(), 1, "maybe");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
