using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using VietRoad.Api.Endpoints;
using VietRoad.Data;

namespace VietRoad.Api.Tests;

public class SpeedLimitApiTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private async Task SeedAsync(long id, int maxSpeed, params (double Lat, double Lng)[] pts)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VietRoadDbContext>();
        db.RoadSpeedLimits.Add(new RoadSpeedLimitEntity
        {
            Id = id, Highway = "trunk", MaxSpeed = maxSpeed, Oneway = false, Name = $"Duong {id}",
            Points = "[" + string.Join(",", pts.Select(p => FormattableString.Invariant($"[{p.Lat},{p.Lng}]"))) + "]",
            MinLat = pts.Min(p => p.Lat), MaxLat = pts.Max(p => p.Lat),
            MinLng = pts.Min(p => p.Lng), MaxLng = pts.Max(p => p.Lng),
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Nearby_ReturnsSegmentsCrossingTheArea_WithPoints()
    {
        // Duong dai di NGANG qua vung tim (2 dau deu nam ngoai ban kinh) van phai duoc tra ve.
        await SeedAsync(9001, 80, (15.000, 107.900), (15.000, 108.100));
        await SeedAsync(9002, 50, (15.300, 108.000), (15.301, 108.000)); // cach ~33 km: khong tra

        var res = await factory.CreateClient().GetFromJsonAsync<SpeedLimitsResponse>(
            "/api/v1/speed-limits/nearby?lat=15.001&lng=108.0&radius=1000");

        var seg = Assert.Single(res!.Segments, s => s.Id is 9001 or 9002);
        Assert.Equal((9001, 80), (seg.Id, seg.MaxSpeed));
        Assert.Equal([15.0, 107.9], seg.Points[0]);
    }

    [Fact]
    public async Task Nearby_Validation()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/speed-limits/nearby?lat=999&lng=105")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/speed-limits/nearby?lat=21&lng=105&radius=10000")).StatusCode);
    }
}
