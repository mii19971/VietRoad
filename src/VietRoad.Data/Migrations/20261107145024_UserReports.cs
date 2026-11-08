using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietRoad.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "confirm_count",
                schema: "vietroad",
                table: "alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "vietroad",
                table: "alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "deny_count",
                schema: "vietroad",
                table: "alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_confirmed_at",
                schema: "vietroad",
                table: "alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alert_votes",
                schema: "vietroad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_confirm = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_votes_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "vietroad",
                        principalTable: "alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_votes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "vietroad",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_created_by_user_id_created_at",
                schema: "vietroad",
                table: "alerts",
                columns: new[] { "created_by_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_votes_alert_id_user_id",
                schema: "vietroad",
                table: "alert_votes",
                columns: new[] { "alert_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_votes_user_id",
                schema: "vietroad",
                table: "alert_votes",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_alerts_users_created_by_user_id",
                schema: "vietroad",
                table: "alerts",
                column: "created_by_user_id",
                principalSchema: "vietroad",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alerts_users_created_by_user_id",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropTable(
                name: "alert_votes",
                schema: "vietroad");

            migrationBuilder.DropIndex(
                name: "IX_alerts_created_by_user_id_created_at",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "confirm_count",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "deny_count",
                schema: "vietroad",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "last_confirmed_at",
                schema: "vietroad",
                table: "alerts");
        }
    }
}
