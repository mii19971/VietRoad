using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using VietRoad.Admin.Components;
using VietRoad.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// Static SSR: moi trang render tren server roi tra HTML, khong can WebSocket.
builder.Services.AddRazorComponents();

// --- Dang nhap bang cookie ---
// Sau khi dang nhap qua VietRoad.Api, Admin cat access token vao cookie (da duoc ma hoa,
// trinh duyet khong doc duoc noi dung). Moi request sau, trinh duyet tu gui cookie len.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "vietroad_admin";
        o.Cookie.HttpOnly = true;                 // JavaScript khong doc duoc cookie
        o.Cookie.SameSite = SameSiteMode.Strict;  // trang web khac khong gui kem cookie nay
        o.LoginPath = "/login";                   // chua dang nhap -> chuyen ve day
        o.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState(); // cho <AuthorizeView> biet ai dang dang nhap

// --- Goi VietRoad.Api ---
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Thieu cau hinh Api:BaseUrl");

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<ApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>(); // tu gan token vao moi request

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();  // doc cookie -> biet admin la ai
app.UseAuthorization();   // trang co [Authorize] ma chua dang nhap -> /login
app.UseAntiforgery();     // chong CSRF cho moi form POST

app.MapStaticAssets();
app.MapRazorComponents<App>();

// Dang xuat: xoa cookie roi ve trang dang nhap.
app.MapGet("/logout", async (HttpContext ctx, string? expired) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect(expired is null ? "/login" : "/login?expired=1");
});

app.Run();
