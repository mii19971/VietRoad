using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietRoad.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verified_at",
                schema: "vietroad",
                table: "alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by_user_id",
                schema: "vietroad",
                table: "alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_alerts_verified_by_user_id",
                schema: "vietroad",
                table: "alerts",
                column: "verified_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_alerts_users_verified_by_user_id",
                schema: "vietroad",
                table: "alerts",
                column: "verified_by_user_id",
                principalSchema: "vietroad",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alerts_users_verified_by_user_id",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropIndex(
                name: "IX_alerts_verified_by_user_id",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "verified_at",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "verified_by_user_id",
                schema: "vietroad",
                table: "alerts");
        }
    }
}
