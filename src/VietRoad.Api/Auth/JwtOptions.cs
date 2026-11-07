using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace VietRoad.Api.Auth;

/// <summary>Cau hinh JWT, doc tu muc "Jwt" trong appsettings / User Secrets.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Ai phat hanh token (server cua minh).</summary>
    public string Issuer { get; set; } = "vietroad-api";

    /// <summary>Token danh cho ai (app VietRoad).</summary>
    public string Audience { get; set; } = "vietroad-app";

    /// <summary>
    /// Khoa bi mat de ky token. Ai co khoa nay se tao duoc token gia,
    /// nen chi de trong User Secrets / bien moi truong, KHONG commit vao git.
    /// </summary>
    public string SigningKey { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
