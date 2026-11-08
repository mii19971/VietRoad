using System.Net.Mail;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VietRoad.Api.Auth;
using VietRoad.Api.Models;
using VietRoad.Data;

namespace VietRoad.Api.Endpoints;

public static class AuthEndpoints
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;
    private const int MaxDisplayNameLength = 100;

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Khong can dang nhap.
        var auth = app.MapGroup("/api/v1/auth");
        auth.MapPost("/register", Register);
        auth.MapPost("/login", Login);
        auth.MapPost("/refresh", Refresh);
        auth.MapPost("/logout", Logout);

        // Cai dat lan dau: tao admin dau tien. Chi dung duoc khi he thong CHUA co admin nao.
        auth.MapGet("/bootstrap-status", async (AuthService s, CancellationToken ct) =>
            Results.Ok(new { needsAdmin = !await s.HasAnyAdminAsync(ct) }));
        auth.MapPost("/bootstrap-admin", BootstrapAdmin);

        // Bat buoc co access token hop le, neu khong -> 401.
        var users = app.MapGroup("/api/v1/users").RequireAuthorization();
        users.MapGet("/me", GetMe);
    }

    // POST /api/v1/auth/register  { "email": "...", "password": "...", "displayName": "..." }
    // RegisterRequest duoc doc tu JSON body (vi la kieu phuc tap, khong phai so/chuoi).
    private static async Task<IResult> Register(RegisterRequest req, AuthService authService, CancellationToken ct)
    {
        if (ValidateRegister(req, out var displayName) is { } error)
            return BadRequest(error);

        var result = await authService.RegisterAsync(req.Email!, req.Password!, displayName, ct);

        return result is null
            ? Results.Conflict(new ErrorResponse("Email da duoc dang ky"))  // 409
            : Results.Created("/api/v1/users/me", result);                  // 201
    }

    // POST /api/v1/auth/bootstrap-admin  (giong register, nhung tai khoan la admin)
    // Sau khi da co admin, endpoint nay luon tra 409 -> nguoi ngoai khong the tu lam admin.
    private static async Task<IResult> BootstrapAdmin(RegisterRequest req, AuthService authService, CancellationToken ct)
    {
        if (await authService.HasAnyAdminAsync(ct))
            return Results.Conflict(new ErrorResponse("He thong da co admin. Hay dang ky tai khoan thuong va nho admin cap quyen."));
        if (ValidateRegister(req, out var displayName) is { } error)
            return BadRequest(error);

        var result = await authService.RegisterAsync(req.Email!, req.Password!, displayName, ct, UserRoles.Admin);

        return result is null
            ? Results.Conflict(new ErrorResponse("Email da duoc dang ky"))
            : Results.Created("/api/v1/users/me", result);
    }

    // Kiem tra du lieu dang ky (dung chung cho register va bootstrap-admin). null = hop le.
    private static string? ValidateRegister(RegisterRequest req, out string displayName)
    {
        displayName = "";
        if (!IsValidEmail(req.Email))
            return "Email khong hop le";
        if (req.Password is null || req.Password.Length is < MinPasswordLength or > MaxPasswordLength)
            return $"Mat khau phai dai {MinPasswordLength}-{MaxPasswordLength} ky tu";

        // Khong nhap ten -> lay phan truoc @ cua email.
        displayName = string.IsNullOrWhiteSpace(req.DisplayName)
            ? req.Email!.Split('@')[0]
            : req.DisplayName.Trim();
        if (displayName.Length > MaxDisplayNameLength)
            return $"Ten hien thi toi da {MaxDisplayNameLength} ky tu";

        return null;
    }

    // POST /api/v1/auth/login  { "email": "...", "password": "..." }
    private static async Task<IResult> Login(LoginRequest req, AuthService authService, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Password))
            return BadRequest("Can nhap email va mat khau");

        var result = await authService.LoginAsync(req.Email, req.Password, ct);
        return result is null ? Unauthorized("Sai email hoac mat khau") : Results.Ok(result);
    }

    // POST /api/v1/auth/refresh  { "refreshToken": "..." }
    private static async Task<IResult> Refresh(RefreshRequest req, AuthService authService, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest("Thieu refreshToken");

        var result = await authService.RefreshAsync(req.RefreshToken, ct);
        return result is null ? Unauthorized("Refresh token khong hop le hoac da het han") : Results.Ok(result);
    }

    // POST /api/v1/auth/logout  { "refreshToken": "..." }
    private static async Task<IResult> Logout(RefreshRequest req, AuthService authService, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.RefreshToken))
            await authService.LogoutAsync(req.RefreshToken, ct);
        return Results.NoContent(); // 204
    }

    // GET /api/v1/users/me   Header: Authorization: Bearer <accessToken>
    // ClaimsPrincipal = user da duoc xac thuc tu token (ASP.NET Core tu dien).
    private static async Task<IResult> GetMe(ClaimsPrincipal principal, AuthService authService, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
            return Unauthorized("Token khong hop le");

        var profile = await authService.GetProfileAsync(userId, ct);
        // Token van con han nhung user da bi xoa -> 401.
        return profile is null ? Unauthorized("Tai khoan khong ton tai") : Results.Ok(profile);
    }

    private static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Length <= 254
        && MailAddress.TryCreate(email.Trim(), out var addr)
        && addr.Address == email.Trim(); // chan dang "Ten <a@b.com>"

    private static IResult BadRequest(string message) => Results.BadRequest(new ErrorResponse(message));

    private static IResult Unauthorized(string message) =>
        Results.Json(new ErrorResponse(message), statusCode: StatusCodes.Status401Unauthorized);
}
