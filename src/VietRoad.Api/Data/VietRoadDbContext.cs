using Microsoft.EntityFrameworkCore;

namespace VietRoad.Api.Data;

/// <summary>
/// "Cua ngo" vao database. Moi DbSet tuong ung 1 bang.
/// EF Core dich cau LINQ tren DbSet thanh SQL.
/// </summary>
public class VietRoadDbContext(DbContextOptions<VietRoadDbContext> options) : DbContext(options)
{
    /// <summary>Moi bang cua VietRoad nam trong schema nay, tach khoi du lieu cua du an khac.</summary>
    public const string Schema = "vietroad";

    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();

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

            // Chi muc tren (lat, lng): giup cau "WHERE lat BETWEEN .. AND lng BETWEEN .."
            // khong phai quet ca bang.
            e.HasIndex(a => new { a.Lat, a.Lng });

            // Du lieu mau, duoc chen vao khi chay migration.
            e.HasData(
                new AlertEntity { Id = 1, Type = "speed_camera", Lat = 21.0285, Lng = 105.8542, SpeedLimit = 50,   Name = "Camera Ho Hoan Kiem" },
                new AlertEntity { Id = 2, Type = "speed_camera", Lat = 21.0368, Lng = 105.8345, SpeedLimit = 60,   Name = "Camera Kim Ma" },
                new AlertEntity { Id = 3, Type = "red_light",    Lat = 21.0245, Lng = 105.8412, SpeedLimit = null, Name = "Den do Tran Hung Dao" },
                new AlertEntity { Id = 4, Type = "speed_camera", Lat = 10.7769, Lng = 106.7009, SpeedLimit = 50,   Name = "Camera Q1 TP.HCM" });
        });
    }
}
