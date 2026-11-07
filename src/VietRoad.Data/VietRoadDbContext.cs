using Microsoft.EntityFrameworkCore;

namespace VietRoad.Data;

/// <summary>
/// "Cua ngo" vao database. Moi DbSet tuong ung 1 bang.
/// EF Core dich cau LINQ tren DbSet thanh SQL.
/// Nam o project rieng de API va Importer dung chung.
/// </summary>
public class VietRoadDbContext(DbContextOptions<VietRoadDbContext> options) : DbContext(options)
{
    /// <summary>Moi bang cua VietRoad nam trong schema nay, tach khoi du lieu cua du an khac.</summary>
    public const string Schema = "vietroad";

    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    // Mo ta cau truc bang. Migration duoc sinh ra tu day.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Bang nao khong chi dinh schema thi mac dinh vao "vietroad" (thay vi "public").
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<AlertEntity>(e =>
        {
            e.ToTable("alerts");
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.Type).HasColumnName("type").HasMaxLength(32);
            e.Property(a => a.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(a => a.SpeedLimit).HasColumnName("speed_limit");
            e.Property(a => a.Lat).HasColumnName("lat");
            e.Property(a => a.Lng).HasColumnName("lng");
            e.Property(a => a.Direction).HasColumnName("direction");
            e.Property(a => a.Source).HasColumnName("source").HasMaxLength(16);
            e.Property(a => a.ExternalId).HasColumnName("external_id").HasMaxLength(64);
            e.Property(a => a.Confidence).HasColumnName("confidence");
            e.Property(a => a.ExpiresAt).HasColumnName("expires_at");
            e.Property(a => a.IsActive).HasColumnName("is_active");
            e.Property(a => a.CreatedAt).HasColumnName("created_at");
            e.Property(a => a.UpdatedAt).HasColumnName("updated_at");

            // Chi muc tren (lat, lng): giup cau "WHERE lat BETWEEN .. AND lng BETWEEN .."
            // khong phai quet ca bang.
            e.HasIndex(a => new { a.Lat, a.Lng });

            // Moi doi tuong ben nguon chi co 1 dong. PostgreSQL cho phep nhieu NULL
            // trong chi muc UNIQUE, nen dong khong co external_id (vd admin nhap) khong bi anh huong.
            e.HasIndex(a => a.ExternalId).IsUnique();
        });

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Email).HasColumnName("email").HasMaxLength(254);
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(256);
            e.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(100);
            e.Property(u => u.Role).HasColumnName("role").HasMaxLength(16);
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.Property(u => u.LastLoginAt).HasColumnName("last_login_at");

            // Moi email chi dang ky 1 lan - database tu chan, ke ca khi 2 request den cung luc.
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<RefreshTokenEntity>(e =>
        {
            e.ToTable("refresh_tokens");
            e.Property(t => t.Id).HasColumnName("id");
            e.Property(t => t.UserId).HasColumnName("user_id");
            e.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            e.Property(t => t.CreatedAt).HasColumnName("created_at");
            e.Property(t => t.ExpiresAt).HasColumnName("expires_at");
            e.Property(t => t.RevokedAt).HasColumnName("revoked_at");
            e.Property(t => t.ReplacedById).HasColumnName("replaced_by_id");

            e.HasIndex(t => t.TokenHash).IsUnique();

            // 1 user - nhieu token (moi thiet bi 1 token). Xoa user thi xoa luon token.
            e.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
