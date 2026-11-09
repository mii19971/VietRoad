using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace VietRoad.Admin.Services;

/// <summary>Thong bao khi khong ket noi duoc toi VietRoad.Api.</summary>
public static class ApiConnection
{
    public const string Message =
        "Không kết nối được VietRoad.Api (http://localhost:5000). Hãy chạy project VietRoad.Api với profile \"http\".";
}

/// <summary>API tra 401/403: token het han hoac khong con quyen admin -> phai dang nhap lai.</summary>
public class ApiUnauthorizedException() : Exception("Phien dang nhap het han hoac khong co quyen.");

/// <summary>
/// Goi VietRoad.Api. Moi trang Admin dung class nay, khong tu viet HttpClient.
/// "Typed HttpClient": DI tao san HttpClient voi BaseAddress va BearerTokenHandler.
/// </summary>
public class ApiClient(HttpClient http)
{
    private record ErrorBody(string? Error);

    // ----- Dang nhap (chua co token) -----

    /// <summary>null = sai email / mat khau.</summary>
    public async Task<AuthResult?> LoginAsync(string email, string password)
    {
        var res = await http.PostAsJsonAsync("api/v1/auth/login", new { email, password });
        if (res.StatusCode == HttpStatusCode.Unauthorized) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<AuthResult>();
    }

    // ----- Dang ky -----

    private record BootstrapStatus(bool NeedsAdmin);

    /// <summary>true = he thong chua co admin nao -> nguoi dang ky dau tien se la admin.</summary>
    public async Task<bool> NeedsFirstAdminAsync() =>
        (await http.GetFromJsonAsync<BootstrapStatus>("api/v1/auth/bootstrap-status"))!.NeedsAdmin;

    /// <summary>asFirstAdmin = true: goi bootstrap-admin (chi thanh cong khi chua co admin).</summary>
    public Task<ApiResult<AuthResult>> RegisterAsync(string email, string password, string? displayName, bool asFirstAdmin) =>
        SendAsync<AuthResult>(http.PostAsJsonAsync(
            asFirstAdmin ? "api/v1/auth/bootstrap-admin" : "api/v1/auth/register",
            new { email, password, displayName }));

    // ----- Thong ke -----

    public Task<AdminStats> GetStatsAsync() => GetAsync<AdminStats>("api/v1/admin/stats");

    // ----- Canh bao -----

    public Task<PagedResult<AdminAlert>> ListAlertsAsync(
        string? status = null, string? source = null, string? type = null, string? q = null, int page = 1, int pageSize = 20)
    {
        // QueryHelpers: ghep query string va tu ma hoa ky tu dac biet (dau cach, &, tieng Viet...).
        var query = new Dictionary<string, string?>
        {
            ["status"] = status, ["source"] = source, ["type"] = type, ["q"] = q,
            ["page"] = page.ToString(), ["pageSize"] = pageSize.ToString(),
        }.Where(kv => !string.IsNullOrEmpty(kv.Value));

        return GetAsync<PagedResult<AdminAlert>>(QueryHelpers.AddQueryString("api/v1/admin/alerts", query));
    }

    public async Task<AdminAlert?> GetAlertAsync(int id)
    {
        var res = await http.GetAsync($"api/v1/admin/alerts/{id}");
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        return await ReadAsync<AdminAlert>(res);
    }

    public Task<ApiResult<AdminAlert>> CreateAlertAsync(AlertUpsert body) =>
        SendAsync<AdminAlert>(http.PostAsJsonAsync("api/v1/admin/alerts", body));

    public Task<ApiResult<AdminAlert>> UpdateAlertAsync(int id, AlertUpsert body) =>
        SendAsync<AdminAlert>(http.PutAsJsonAsync($"api/v1/admin/alerts/{id}", body));

    public Task<ApiResult<AdminAlert>> ApproveAsync(int id) =>
        SendAsync<AdminAlert>(http.PostAsync($"api/v1/admin/alerts/{id}/approve", null));

    public Task<ApiResult<AdminAlert>> RejectAsync(int id) =>
        SendAsync<AdminAlert>(http.PostAsync($"api/v1/admin/alerts/{id}/reject", null));

    // ----- Nguoi dung -----

    public Task<PagedResult<AdminUser>> ListUsersAsync(string? q = null, int page = 1, int pageSize = 20) =>
        GetAsync<PagedResult<AdminUser>>(QueryHelpers.AddQueryString("api/v1/admin/users",
            new Dictionary<string, string?> { ["q"] = q ?? "", ["page"] = page.ToString(), ["pageSize"] = pageSize.ToString() }));

    public Task<ApiResult<bool>> SetRoleAsync(Guid userId, string role) =>
        SendAsync<bool>(http.PutAsJsonAsync($"api/v1/admin/users/{userId}/role", new { role }));

    // ----- Goi dich vu -----

    /// <summary>Goi hien tai + lich su. null = khong co nguoi dung nay.</summary>
    public async Task<UserSubscriptions?> GetSubscriptionsAsync(Guid userId)
    {
        var res = await http.GetAsync($"api/v1/admin/users/{userId}/subscriptions");
        return res.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<UserSubscriptions>(res);
    }

    public Task<ApiResult<SubscriptionRecord>> GrantSubscriptionAsync(Guid userId, string plan, int days, string? note) =>
        SendAsync<SubscriptionRecord>(http.PostAsJsonAsync($"api/v1/admin/users/{userId}/subscriptions", new { plan, days, note }));

    public Task<ApiResult<bool>> CancelSubscriptionAsync(int subscriptionId) =>
        SendAsync<bool>(http.PostAsync($"api/v1/admin/subscriptions/{subscriptionId}/cancel", null));

    // ----- Ham phu -----

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage res)
    {
        ThrowIfUnauthorized(res);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    // Loi 400/404/409 -> tra thong bao cua API de hien cho admin, khong nem exception.
    private static async Task<ApiResult<T>> SendAsync<T>(Task<HttpResponseMessage> request)
    {
        var res = await request;
        ThrowIfUnauthorized(res);

        if (res.IsSuccessStatusCode)
        {
            var hasBody = res.StatusCode != HttpStatusCode.NoContent;
            return new ApiResult<T>(hasBody ? await res.Content.ReadFromJsonAsync<T>() : default, null);
        }

        var error = await res.Content.ReadFromJsonAsync<ErrorBody>();
        return new ApiResult<T>(default, error?.Error ?? $"Loi {(int)res.StatusCode}");
    }

    private static void ThrowIfUnauthorized(HttpResponseMessage res)
    {
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ApiUnauthorizedException();
    }
}

/// <summary>
/// Gan "Authorization: Bearer &lt;token&gt;" vao moi request gui toi API.
/// Token lay tu cookie dang nhap cua admin (claim "access_token").
/// IHttpContextAccessor dung duoc vi Admin chay o che do static SSR: moi trang = 1 request HTTP.
/// </summary>
public class BearerTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    public const string AccessTokenClaim = "access_token";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = accessor.HttpContext?.User.FindFirst(AccessTokenClaim)?.Value;
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}
