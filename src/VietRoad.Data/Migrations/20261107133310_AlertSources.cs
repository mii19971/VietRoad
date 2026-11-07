using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VietRoad.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlertSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "vietroad",
                table: "alerts",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "vietroad",
                table: "alerts",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "vietroad",
                table: "alerts",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "vietroad",
                table: "alerts",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "confidence",
                schema: "vietroad",
                table: "alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "vietroad",
                table: "alerts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<double>(
                name: "direction",
                schema: "vietroad",
                table: "alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                schema: "vietroad",
                table: "alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                schema: "vietroad",
                table: "alerts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "vietroad",
                table: "alerts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "vietroad",
                table: "alerts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "vietroad",
                table: "alerts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_alerts_external_id",
                schema: "vietroad",
                table: "alerts",
                column: "external_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_alerts_external_id",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "confidence",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "direction",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "external_id",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "vietroad",
                table: "alerts");

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
        }
    }
}
