using Microsoft.AspNetCore.Components;
using VietRoad.Admin.Services;

namespace VietRoad.Admin.Components;

/// <summary>
/// Lop cha cho moi trang quan tri (@inherits AdminPageBase).
/// Gom phan lap lai: goi API, va neu API bao het phien (401/403) thi dua ve trang dang nhap.
/// </summary>
public abstract class AdminPageBase : ComponentBase
{
    [Inject] protected ApiClient Api { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;

    /// <summary>Thong bao hien o dau trang sau khi thao tac (thanh cong / loi).</summary>
    protected string? Message { get; set; }
    protected bool MessageIsError { get; set; }

    protected async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (ApiUnauthorizedException)
        {
            // O static SSR, NavigateTo tra ve redirect 302 cho trinh duyet.
            Nav.NavigateTo("/logout?expired=1");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            // StatusCode null = khong ket noi duoc (API chua chay / sai cong), khac voi API tra loi loi.
            Show(ApiConnection.Message, "");
        }
    }

    protected void Show(string? error, string success)
    {
        MessageIsError = error is not null;
        Message = error ?? success;
    }
}
