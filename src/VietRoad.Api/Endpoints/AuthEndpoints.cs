using System.Net.Mail;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VietRoad.Api.Auth;
using VietRoad.Api.Models;

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

        // Bat buoc co access token hop le, neu khong -> 401.
        var users = app.MapGroup("/api/v1/users").RequireAuthorization();
        users.MapGet("/me", GetMe);
    }

    // POST /api/v1/auth/register  { "email": "...", "password": "...", "displayName": "..." }
    // RegisterRequest duoc doc tu JSON body (vi la kieu phuc tap, khong phai so/chuoi).
    private static async Task<IResult> Register(RegisterRequest req, AuthService authService, CancellationToken ct)
    {
        if (!IsValidEmail(req.Email))
            return BadRequest("Email khong hop le");
        if (req.Password is null || req.Password.Length is < MinPasswordLength or > MaxPasswordLength)
            return BadRequest($"Mat khau phai dai {MinPasswordLength}-{MaxPasswordLength} ky tu");

        // Khong nhap ten -> lay phan truoc @ cua email.
        var displayName = string.IsNullOrWhiteSpace(req.DisplayName)
            ? req.Email!.Split('@')[0]
            : req.DisplayName.Trim();
        if (displayName.Length > MaxDisplayNameLength)
            return BadRequest($"Ten hien thi toi da {MaxDisplayNameLength} ky tu");

        var result = await authService.RegisterAsync(req.Email!, req.Password, displayName, ct);

        return result is null
            ? Results.Conflict(new ErrorResponse("Email da duoc dang ky"))  // 409
            : Results.Created("/api/v1/users/me", result);                  // 201
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
