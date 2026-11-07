namespace VietRoad.Data;

/// <summary>
/// Mot refresh token da cap cho 1 thiet bi dang nhap.
/// Chi luu HASH cua token: lo database ra ngoai cung khong dung duoc token.
/// </summary>
public class RefreshTokenEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = null!;

    /// <summary>SHA-256 cua token (hex, 64 ky tu).</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Thoi diem bi thu hoi (da dung de doi token moi, hoac dang xuat). null = con hieu luc.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Token moi da thay the token nay. Dung de phat hien token bi danh cap (xem AuthService).</summary>
    public Guid? ReplacedById { get; set; }
}
