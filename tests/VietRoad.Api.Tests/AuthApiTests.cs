using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using VietRoad.Api.Auth;

namespace VietRoad.Api.Tests;

public class AuthApiTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Moi test dung email rieng vi cac test trong lop dung chung 1 database.
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<AuthResponse> RegisterAsync(string email, string password = "matkhau123")
    {
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, password, "Tai xe"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshToken));

    [Fact]
    public async Task Register_ThenGetMe_ReturnsProfile()
    {
        var email = NewEmail();
        var auth = await RegisterAsync(email);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var me = await res.Content.ReadFromJsonAsync<UserProfile>();
        Assert.Equal(email, me!.Email);
        Assert.Equal("user", me.Role);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var res = await _client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithTamperedToken_Returns401()
    {
        var auth = await RegisterAsync(NewEmail());
        var tampered = auth.AccessToken[..^4] + "AAAA"; // sua vai ky tu cuoi cua chu ky

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Register_SameEmailTwice_Returns409_CaseInsensitive()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var res = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email.ToUpperInvariant(), "matkhau123", null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Theory]
    [InlineData("khong-phai-email", "matkhau123")]
    [InlineData("a@example.com", "ngan")]
    [InlineData(null, "matkhau123")]
    public async Task Register_InvalidInput_Returns400(string? email, string password)
    {
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, password, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401_RightPassword_Returns200()
    {
        var email = NewEmail();
        await RegisterAsync(email, "dung-mat-khau");

        var wrong = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "sai-mat-khau"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var right = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "dung-mat-khau"));
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(NewEmail(), "matkhau123"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndReuseRevokesWholeFamily()
    {
        var auth = await RegisterAsync(NewEmail());

        // Doi lan 1: thanh cong, nhan token moi.
        var first = await RefreshAsync(auth.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);

        // Dung lai token CU -> nghi bi danh cap -> 401 ...
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);

        // ... va token MOI cung bi thu hoi.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(rotated.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var auth = await RegisterAsync(NewEmail());

        var logout = await _client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
    }
}
