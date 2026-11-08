using System.Net;
using System.Net.Http.Json;
using VietRoad.Api.Auth;

namespace VietRoad.Api.Tests;

public class BootstrapAdminTests
{
    private record Status(bool NeedsAdmin);

    [Fact]
    public async Task FirstAdmin_CanBeCreatedOnce_ThenEndpointIsClosed()
    {
        // Factory rieng -> database trong, chua co admin.
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        Assert.True((await client.GetFromJsonAsync<Status>("/api/v1/auth/bootstrap-status"))!.NeedsAdmin);

        var first = await client.PostAsJsonAsync("/api/v1/auth/bootstrap-admin",
            new RegisterRequest("boss@example.com", "matkhau123", "Boss"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("admin", (await first.Content.ReadFromJsonAsync<AuthResponse>())!.User.Role);

        Assert.False((await client.GetFromJsonAsync<Status>("/api/v1/auth/bootstrap-status"))!.NeedsAdmin);

        // Nguoi thu 2 khong the tu lam admin nua.
        var second = await client.PostAsJsonAsync("/api/v1/auth/bootstrap-admin",
            new RegisterRequest("hacker@example.com", "matkhau123", null));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task NormalRegister_NeverCreatesAdmin()
    {
        await using var factory = new TestAppFactory();
        var res = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("first@example.com", "matkhau123", null));

        // Du he thong chua co admin, dang ky thuong van chi la "user".
        Assert.Equal("user", (await res.Content.ReadFromJsonAsync<AuthResponse>())!.User.Role);
    }
}
