using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StajDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GorevDuyuruTalep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "department_id",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachment_content_type",
                table: "announcements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachment_key",
                table: "announcements",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachment_name",
                table: "announcements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "attachment_size",
                table: "announcements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_department_id",
                table: "announcements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "announcement_reads",
                columns: table => new
                {
                    announcement_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcement_reads", x => new { x.announcement_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_announcement_reads_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_announcement_reads_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_tasks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    assignee_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_tasks_users_assignee_id",
                        column: x => x.assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_tasks_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_department_id_status",
                table: "tickets",
                columns: new[] { "department_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_announcements_target_department_id",
                table: "announcements",
                column: "target_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcement_reads_user_id",
                table: "announcement_reads",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_assignee_id_status_due_date",
                table: "work_tasks",
                columns: new[] { "assignee_id", "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_created_by_id_status",
                table: "work_tasks",
                columns: new[] { "created_by_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "fk_announcements_departments_target_department_id",
                table: "announcements",
                column: "target_department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_departments_department_id",
                table: "tickets",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_announcements_departments_target_department_id",
                table: "announcements");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_departments_department_id",
                table: "tickets");

            migrationBuilder.DropTable(
                name: "announcement_reads");

            migrationBuilder.DropTable(
                name: "work_tasks");

            migrationBuilder.DropIndex(
                name: "ix_tickets_department_id_status",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_announcements_target_department_id",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "attachment_content_type",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "attachment_key",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "attachment_name",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "attachment_size",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "target_department_id",
                table: "announcements");
        }
    }
}
