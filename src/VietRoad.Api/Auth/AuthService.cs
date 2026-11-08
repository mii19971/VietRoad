using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VietRoad.Data;

namespace VietRoad.Api.Auth;

/// <summary>
/// User Service: dang ky, dang nhap, doi token, dang xuat.
/// Cac ham tra ve null khi that bai; endpoint quyet dinh ma loi HTTP.
/// </summary>
public class AuthService(
    VietRoadDbContext db,
    TokenService tokens,
    IPasswordHasher<UserEntity> hasher,
    TimeProvider time)
{
    // User gia, chi dung de bam mat khau khi email khong ton tai (xem LoginAsync).
    private static readonly UserEntity DummyUser = new()
    {
        Email = "", PasswordHash = "", DisplayName = "", Role = UserRoles.User,
    };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>null = email da duoc dang ky.</summary>
    public async Task<AuthResponse?> RegisterAsync(
        string email, string password, string displayName, CancellationToken ct, string role = UserRoles.User)
    {
        email = NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return null;

        var user = new UserEntity
        {
            // Guid v7: co phan thoi gian o dau -> id tang dan, chi muc database hoat dong tot hon v4.
            Id = Guid.CreateVersion7(),
            Email = email,
            PasswordHash = "",
            DisplayName = displayName,
            Role = role,
            CreatedAt = time.GetUtcNow(),
        };
        // PasswordHasher cua ASP.NET Core Identity: PBKDF2 + salt ngau nhien + 100.000 vong lap.
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);

        try
        {
            return await IssueTokensAsync(user, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // 2 request dang ky cung email den cung luc: ca 2 qua duoc buoc AnyAsync,
            // nhung chi muc UNIQUE trong database chan request thu 2.
            return null;
        }
    }

    /// <summary>He thong da co it nhat 1 admin chua (de biet co can "cai dat lan dau" khong).</summary>
    public Task<bool> HasAnyAdminAsync(CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Role == UserRoles.Admin, ct);

    /// <summary>null = sai email hoac mat khau (co tinh khong noi cai nao sai).</summary>
    public async Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken ct)
    {
        email = NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            // Van bam mat khau du khong can, de thoi gian tra loi giong het truong hop sai mat khau.
            // Neu khong, ke tan cong do thoi gian se biet email nao da dang ky.
            hasher.HashPassword(DummyUser, password);
            return null;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        // Thuat toan bam duoc nang cap (vd tang so vong lap) -> bam lai bang thuat toan moi.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);

        user.LastLoginAt = time.GetUtcNow();
        return await IssueTokensAsync(user, ct);
    }

    /// <summary>
    /// Doi refresh token cu lay cap token moi ("rotation": moi refresh token chi dung duoc 1 lan).
    /// null = token khong hop le / het han / da thu hoi.
    /// </summary>
    public async Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User) // lay kem user trong cung 1 cau SQL (JOIN)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            return null;

        var now = time.GetUtcNow();

        if (stored.RevokedAt is not null)
        {
            // Token DA DUOC DOI roi ma co nguoi dung lai -> co the bi danh cap
            // (ke gian va chu that cung giu 1 token). Thu hoi moi phien cua user cho an toan.
            if (stored.ReplacedById is not null)
                await RevokeAllAsync(stored.UserId, now, ct);
            return null;
        }

        if (stored.ExpiresAt <= now)
            return null;

        return await IssueTokensAsync(stored.User, ct, replacing: stored);
    }

    /// <summary>Thu hoi refresh token cua thiet bi nay. Goi nhieu lan cung khong sao.</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    public Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken ct) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfile(u.Id, u.Email, u.DisplayName, u.Role, u.CreatedAt))
            .FirstOrDefaultAsync(ct);

    // Tao access token + refresh token moi, luu hash refresh token, ghi tat ca trong 1 lan SaveChanges.
    private async Task<AuthResponse> IssueTokensAsync(
        UserEntity user, CancellationToken ct, RefreshTokenEntity? replacing = null)
    {
        var now = time.GetUtcNow();
        var (accessToken, accessExpiresAt) = tokens.CreateAccessToken(user);
        var (refreshToken, refreshHash, refreshExpiresAt) = tokens.CreateRefreshToken();

        var entity = new RefreshTokenEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
        };
        db.RefreshTokens.Add(entity);

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedById = entity.Id;
        }

        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken, accessExpiresAt,
            refreshToken, refreshExpiresAt,
            new UserProfile(user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt));
    }

    private async Task RevokeAllAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in active)
            t.RevokedAt = now;

        await db.SaveChangesAsync(ct);
    }
}
