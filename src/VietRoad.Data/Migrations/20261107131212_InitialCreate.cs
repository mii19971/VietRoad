using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VietRoad.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vietroad");

            migrationBuilder.CreateTable(
                name: "alerts",
                schema: "vietroad",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    speed_limit = table.Column<int>(type: "integer", nullable: true),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "vietroad",
                table: "alerts",
                columns: new[] { "id", "lat", "lng", "name", "speed_limit", "type" },
                values: new object[,]
                {
                    { 1, 21.028500000000001, 105.85420000000001, "Camera Ho Hoan Kiem", 50, "speed_camera" },
                    { 2, 21.036799999999999, 105.83450000000001, "Camera Kim Ma", 60, "speed_camera" },
                    { 3, 21.0245, 105.8412, "Den do Tran Hung Dao", null, "red_light" },
                    { 4, 10.776899999999999, 106.7009, "Camera Q1 TP.HCM", 50, "speed_camera" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_lat_lng",
                schema: "vietroad",
                table: "alerts",
                columns: new[] { "lat", "lng" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts",
                schema: "vietroad");
        }
    }
}
