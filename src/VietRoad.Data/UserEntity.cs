namespace VietRoad.Data;

/// <summary>Mot dong trong bang "vietroad.users".</summary>
public class UserEntity
{
    public Guid Id { get; set; }

    /// <summary>Luon luu dang chu thuong, da cat khoang trang (de "A@x.com" va "a@x.com" la 1).</summary>
    public required string Email { get; set; }

    /// <summary>
    /// KHONG BAO GIO luu mat khau goc. Day la chuoi bam (hash) PBKDF2 kem salt,
    /// khong the dao nguoc ra mat khau.
    /// </summary>
    public required string PasswordHash { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>Xem <see cref="UserRoles"/>.</summary>
    public required string Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public List<RefreshTokenEntity> RefreshTokens { get; set; } = [];
}

public static class UserRoles
{
    public const string User = "user";
    public const string Admin = "admin";
}
