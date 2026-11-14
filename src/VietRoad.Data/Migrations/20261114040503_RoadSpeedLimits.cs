using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietRoad.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoadSpeedLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "road_speed_limits",
                schema: "vietroad",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    highway = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    @ref = table.Column<string>(name: "ref", type: "character varying(64)", maxLength: 64, nullable: true),
                    max_speed = table.Column<int>(type: "integer", nullable: false),
                    oneway = table.Column<bool>(type: "boolean", nullable: false),
                    points = table.Column<string>(type: "text", nullable: false),
                    min_lat = table.Column<double>(type: "double precision", nullable: false),
                    max_lat = table.Column<double>(type: "double precision", nullable: false),
                    min_lng = table.Column<double>(type: "double precision", nullable: false),
                    max_lng = table.Column<double>(type: "double precision", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_road_speed_limits", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_road_speed_limits_min_lat_max_lat",
                schema: "vietroad",
                table: "road_speed_limits",
                columns: new[] { "min_lat", "max_lat" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "road_speed_limits",
                schema: "vietroad");
        }
    }
}
