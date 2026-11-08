using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace VietRoad.Admin.Services;

/// <summary>Ghi cookie dang nhap tu ket qua cua API. Dung chung cho trang Dang nhap va Dang ky.</summary>
public static class AdminSignIn
{
    public static Task SignInAsync(HttpContext http, AuthResult auth)
    {
        // Cookie chua access token cua API (de goi API thay admin), da duoc ma hoa.
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", auth.User.Id.ToString()),
                new Claim("name", auth.User.DisplayName),
                new Claim("email", auth.User.Email),
                new Claim("role", auth.User.Role),
                new Claim(BearerTokenHandler.AccessTokenClaim, auth.AccessToken),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: "name",
            roleType: "role");

        return http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            // Cookie het han cung luc voi access token -> het han thi dang nhap lai.
            new AuthenticationProperties { ExpiresUtc = auth.AccessTokenExpiresAt });
    }
}
