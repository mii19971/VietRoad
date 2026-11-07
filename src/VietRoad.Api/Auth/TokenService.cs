using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VietRoad.Data;

namespace VietRoad.Api.Auth;

/// <summary>Tao access token (JWT) va refresh token.</summary>
public class TokenService(IOptions<JwtOptions> options, TimeProvider time)
{
    private readonly JwtOptions _jwt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>
    /// JWT = 3 phan "header.payload.signature" ma hoa base64url.
    /// Payload chua cac "claim" (thong tin ve user). Ai cung DOC duoc payload,
    /// nhung khong ai SUA duoc vi chu ky se sai -> khong bao gio dua mat khau vao day.
    /// </summary>
    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(UserEntity user)
    {
        var now = time.GetUtcNow();
        var expiresAt = now.AddMinutes(_jwt.AccessTokenMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), // "sub" = id cua user
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.DisplayName),
                new Claim("role", user.Role),
            ]),
            // HMAC-SHA256: ky va kiem tra bang cung 1 khoa bi mat.
            SigningCredentials = new SigningCredentials(_jwt.GetSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return (token, expiresAt);
    }

    /// <summary>
    /// Refresh token chi la 32 byte ngau nhien (khong phai JWT).
    /// Tra ve ban goc (gui cho app) va hash (luu database).
    /// </summary>
    public (string Token, string Hash, DateTimeOffset ExpiresAt) CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token), time.GetUtcNow().AddDays(_jwt.RefreshTokenDays));
    }

    /// <summary>
    /// SHA-256 la du cho refresh token (khac voi mat khau) vi token da la 256 bit ngau nhien,
    /// khong the doan bang cach thu.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
