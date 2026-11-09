using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using VietRoad.Api.Auth;
using VietRoad.Api.Routing;

namespace VietRoad.Api.Tests;

// ---------- Ham thuan: khong can server ----------

public class PolylineTests
{
    [Fact]
    public void Decode_GoogleExample()
    {
        // Vi du chuan trong tai lieu cua Google.
        var pts = Polyline.Decode("_p~iF~ps|U_ulLnnqC_mqNvxq`@");
        Assert.Equal([(38.5, -120.2), (40.7, -120.95), (43.252, -126.453)], pts);
    }

    [Fact]
    public void Decode_Empty_ReturnsNoPoints() => Assert.Empty(Polyline.Decode(""));
}

public class VietnameseInstructionsTests
{
    [Theory]
    [InlineData("turn", "left", "Phố Huế", null, "Rẽ trái vào Phố Huế")]
    [InlineData("turn", "slight right", null, null, "Chếch sang phải")]
    [InlineData("end of road", "right", "Trần Hưng Đạo", null, "Rẽ phải vào Trần Hưng Đạo")]
    [InlineData("continue", "uturn", null, null, "Quay đầu")]
    [InlineData("depart", null, "Đinh Tiên Hoàng", null, "Đi theo Đinh Tiên Hoàng")]
    [InlineData("depart", null, null, null, "Bắt đầu đi")]
    [InlineData("arrive", "right", null, null, "Đã đến nơi, điểm đến ở bên phải")]
    [InlineData("arrive", null, null, null, "Đã đến nơi")]
    [InlineData("roundabout", "right", "Lê Duẩn", 2, "Vào vòng xuyến, đi ra ở lối ra thứ hai vào Lê Duẩn")]
    [InlineData("exit roundabout", "right", "Lê Duẩn", null, "Ra khỏi vòng xuyến vào Lê Duẩn")]
    [InlineData("off ramp", "slight left", "QL1A", null, "Rẽ ra lối bên trái vào QL1A")]
    [InlineData("fork", "slight right", null, null, "Đi theo nhánh bên phải")]
    [InlineData("new name", "straight", "Hàng Bài", null, "Đi tiếp vào Hàng Bài")]
    public void Build(string type, string? modifier, string? name, int? exit, string expected) =>
        Assert.Equal(expected, VietnameseInstructions.Build(type, modifier, name, exit));

    [Theory]
    [InlineData("new name", "straight", false)] // chi doi ten duong: khong doc
    [InlineData("continue", null, false)]
    [InlineData("continue", "left", true)]      // "continue" nhung co re: phai doc
    [InlineData("turn", "right", true)]
    [InlineData("arrive", null, true)]
    public void IsWorthAnnouncing(string type, string? modifier, bool expected) =>
        Assert.Equal(expected, VietnameseInstructions.IsWorthAnnouncing(type, modifier));
}

public class OsrmMappingTests
{
    [Fact]
    public void ToRouteResponse_JoinsStepGeometries_AndPointsAtManeuvers()
    {
        // Duong: A -> B (xuat phat), B -> C (chi doi ten, bi bo), C -> D (re trai), den D.
        (double, double) a = (21.0, 105.0), b = (21.001, 105.0), c = (21.002, 105.0), d = (21.002, 104.999);
        var route = new OsrmRoute(300, 60, [new OsrmLeg([
            Step("depart", null, "Phố A", [a, b]),
            Step("new name", "straight", "Phố B", [b, c]),
            Step("turn", "left", "Phố C", [c, d]),
            Step("arrive", null, "", [d, d]),
        ])]);

        var r = OsrmRouteProvider.ToRouteResponse(route);

        // Cac diem noi lien, khong lap diem chung: A B C D (+ D cua buoc arrive bi gop)
        Assert.Equal(4, r.Points.Count);
        // Buoc "new name" (chi doi ten duong) da bi bo.
        Assert.Equal(["Đi theo Phố A", "Rẽ trái vào Phố C", "Đã đến nơi"],
            r.Steps.Select(s => s.Instruction).ToArray());
        Assert.Equal(0, r.Steps[0].PointIndex);          // xuat phat tai A
        Assert.Equal(2, r.Steps[1].PointIndex);          // re trai tai C
        Assert.Equal(3, r.Steps[2].PointIndex);          // den noi tai D
        Assert.Equal(21.002, r.Points[r.Steps[1].PointIndex][0], 5);
    }

    private static OsrmStep Step(string type, string? modifier, string name, (double Lat, double Lng)[] pts) =>
        new(Encode(pts), name, null, new OsrmManeuver(type, modifier, null, [pts[0].Lng, pts[0].Lat]));

    // Ma hoa nguoc lai voi Polyline.Decode (chi dung trong test).
    private static string Encode((double Lat, double Lng)[] pts)
    {
        var sb = new StringBuilder();
        int prevLat = 0, prevLng = 0;
        foreach (var (lat, lng) in pts)
        {
            int la = (int)Math.Round(lat * 1e5), ln = (int)Math.Round(lng * 1e5);
            Write(la - prevLat); Write(ln - prevLng);
            prevLat = la; prevLng = ln;
        }
        return sb.ToString();

        void Write(int v)
        {
            var u = v < 0 ? ~(v << 1) : v << 1;
            while (u >= 0x20) { sb.Append((char)((0x20 | (u & 0x1F)) + 63)); u >>= 5; }
            sb.Append((char)(u + 63));
        }
    }
}

// ---------- API: dung dich vu chi duong GIA (khong goi OSRM / Nominatim that) ----------

public class FakeRouting : IRouteProvider, IPlaceSearch
{
    public Func<RouteResponse?> Route { get; set; } = () => null;
    public Func<IReadOnlyList<Place>> Places { get; set; } = () => [];

    public Task<RouteResponse?> RouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct) =>
        Task.FromResult(Route());

    public Task<IReadOnlyList<Place>> SearchAsync(string query, double? nearLat, double? nearLng, CancellationToken ct) =>
        Task.FromResult(Places());

    public Func<Place?> Reverse { get; set; } = () => null;

    public Task<Place?> ReverseAsync(double lat, double lng, CancellationToken ct) => Task.FromResult(Reverse());
}

public class NominatimMappingTests
{
    [Fact]
    public void ReversePlace_NoName_UsesStreetAndWard_SkipsHouseNumberAndPostcode()
    {
        var p = NominatimPlaceSearch.ToReversePlace(new NominatimItem(
            "", "15, Phố Đinh Tiên Hoàng, Phường Hoàn Kiếm, Hà Nội, 11024, Việt Nam", "21.0285", "105.8542"));
        Assert.Equal("Phố Đinh Tiên Hoàng, Phường Hoàn Kiếm", p!.Name);
    }

    [Fact]
    public void ReversePlace_RealNominatimSample_StreetPlusArea()
    {
        // Ket qua that cua Nominatim tai Ho Guom.
        var p = NominatimPlaceSearch.ToReversePlace(new NominatimItem(
            "Phố Đinh Tiên Hoàng", "Phố Đinh Tiên Hoàng, Khu phố cổ, Phường Hoàn Kiếm, Hà Nội, 11024, Việt Nam", "21.0285", "105.8539"));
        Assert.Equal("Phố Đinh Tiên Hoàng, Khu phố cổ", p!.Name);
    }

    [Fact]
    public void ReversePlace_ErrorResponse_ReturnsNull() =>
        Assert.Null(NominatimPlaceSearch.ToReversePlace(new NominatimItem(null, null, null, null)));
}

public class RoutingTestFactory : TestAppFactory
{
    public FakeRouting Fake { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<IRouteProvider>(Fake);
            s.AddSingleton<IPlaceSearch>(Fake);
        });
    }
}

public class RoutingApiTests(RoutingTestFactory factory) : IClassFixture<RoutingTestFactory>
{
    private const string RouteUrl = "/api/v1/routes?fromLat=21.03&fromLng=105.85&toLat=21.02&toLng=105.84";

    /// <summary>Nguoi dung moi; mac dinh co Premium (chi duong la tinh nang Premium).</summary>
    private async Task<HttpClient> UserClientAsync(bool premium = true)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest($"u-{Guid.NewGuid():N}@example.com", "matkhau123", null));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        if (premium) await factory.GrantPremiumAsync(auth!.User.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Route_FreePlan_Returns403()
    {
        var res = await (await UserClientAsync(premium: false)).GetAsync(RouteUrl);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var places = await (await UserClientAsync(premium: false)).GetAsync("/api/v1/places/search?q=ho");
        Assert.Equal(HttpStatusCode.Forbidden, places.StatusCode);
    }

    [Fact]
    public async Task Route_WithoutLogin_Returns401()
    {
        var res = await factory.CreateClient().GetAsync(RouteUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Route_BadCoordinates_Returns400()
    {
        var res = await (await UserClientAsync()).GetAsync("/api/v1/routes?fromLat=999&fromLng=105&toLat=21&toLng=105");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Route_OutsideVietnam_Returns400()
    {
        // Vi tri mac dinh cua may ao Android (California) -> Ha Noi.
        var res = await (await UserClientAsync()).GetAsync("/api/v1/routes?fromLat=37.42&fromLng=-122.08&toLat=21.03&toLng=105.85");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Route_Found_ReturnsRoute()
    {
        factory.Fake.Route = () => new RouteResponse(1200, 180,
            [[21.03, 105.85], [21.02, 105.84]],
            [new RouteStep(1, 21.02, 105.84, "arrive", null, null, "Đã đến nơi")]);

        var route = await (await UserClientAsync()).GetFromJsonAsync<RouteResponse>(RouteUrl);

        Assert.Equal(1200, route!.DistanceM);
        Assert.Equal("Đã đến nơi", route.Steps.Single().Instruction);
    }

    [Fact]
    public async Task Route_NoRoad_Returns404()
    {
        factory.Fake.Route = () => null;
        var res = await (await UserClientAsync()).GetAsync(RouteUrl);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Route_ProviderDown_Returns502()
    {
        factory.Fake.Route = () => throw new RoutingUnavailableException("mat mang");
        var res = await (await UserClientAsync()).GetAsync(RouteUrl);
        Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
    }

    [Fact]
    public async Task SearchPlaces_ReturnsPlaces_AndRequiresQuery()
    {
        factory.Fake.Places = () => [new Place("Hồ Hoàn Kiếm", "Hồ Hoàn Kiếm, Hà Nội", 21.0288, 105.8525)];
        var client = await UserClientAsync();

        var ok = await client.GetFromJsonAsync<PlacesResponse>("/api/v1/places/search?q=ho%20guom&lat=21&lng=105.8");
        Assert.Equal("Hồ Hoàn Kiếm", ok!.Places.Single().Name);

        var empty = await client.GetAsync("/api/v1/places/search?q=%20");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task ReversePlace_ReturnsAddress_Or404()
    {
        var client = await UserClientAsync();

        factory.Fake.Reverse = () => new Place("Phố Đinh Tiên Hoàng, Phường Hoàn Kiếm", "...", 21.0285, 105.8542);
        var ok = await client.GetFromJsonAsync<Place>("/api/v1/places/reverse?lat=21.0285&lng=105.8542");
        Assert.Equal("Phố Đinh Tiên Hoàng, Phường Hoàn Kiếm", ok!.Name);

        factory.Fake.Reverse = () => null;
        var none = await client.GetAsync("/api/v1/places/reverse?lat=21.0285&lng=105.8542");
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }
}
