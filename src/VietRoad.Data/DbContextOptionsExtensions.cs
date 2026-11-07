using Microsoft.EntityFrameworkCore;

namespace VietRoad.Data;

public static class DbContextOptionsExtensions
{
    /// <summary>
    /// Cau hinh ket noi PostgreSQL cho VietRoad. API va Importer cung goi ham nay
    /// de chac chan dung cau hinh giong nhau.
    /// </summary>
    public static DbContextOptionsBuilder UseVietRoadDb(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql =>
            // Bang lich su migration cung dat trong schema "vietroad" de khong lan voi du an khac.
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", VietRoadDbContext.Schema));
}
