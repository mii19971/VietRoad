using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using VietRoad.Api.Models;

namespace VietRoad.Api.Tests;

// WebApplicationFactory dung ca app that trong bo nho (khong mo cong mang)
// va cho ta mot HttpClient de goi API.
public class AlertApiTests(TestAppFactory factory)
    : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Nearby_NearHoGuom_FindsHoanKiemCameraFirst()
    {
        var body = await _client.GetFromJsonAsync<NearbyAlertsResponse>(
            "/api/v1/alerts/nearby?lat=21.0285&lng=105.8542&radius=1500");

        Assert.NotNull(body);
        Assert.Equal(1, body.Alerts[0].Alert.Id);                  // gan nhat
        Assert.All(body.Alerts, x => Assert.True(x.Alert.Lat > 20)); // khong lan camera TP.HCM
    }

    [Theory]
    [InlineData("lat=abc&lng=105")]          // khong phai so
    [InlineData("lat=95&lng=105")]           // ngoai pham vi
    [InlineData("lat=21&lng=105&radius=0")]  // ban kinh sai
    [InlineData("lng=105")]                  // thieu lat
    public async Task Nearby_InvalidInput_Returns400(string query)
    {
        var res = await _client.GetAsync($"/api/v1/alerts/nearby?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
