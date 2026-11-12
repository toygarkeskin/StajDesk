using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace StajDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PanelEklerVeTamMetinArama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "tickets",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('turkish', coalesce(title, '') || ' ' || coalesce(description, ''))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false),
                    publish_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    store_id = table.Column<int>(type: "integer", nullable: true),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcements", x => x.id);
                    table.ForeignKey(
                        name: "fk_announcements_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_announcements_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "on_call_shifts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_on_call_shifts", x => x.id);
                    table.CheckConstraint("ck_on_call_shifts_range", "ends_on >= starts_on");
                    table.ForeignKey(
                        name: "fk_on_call_shifts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    uploaded_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_attachments", x => x.id);
                    table.CheckConstraint("ck_ticket_attachments_size", "size > 0");
                    table.ForeignKey(
                        name: "fk_ticket_attachments_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_attachments_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_search_vector",
                table: "tickets",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_author_id",
                table: "announcements",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_publish_at_expires_at",
                table: "announcements",
                columns: new[] { "publish_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_announcements_store_id",
                table: "announcements",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_on_call_shifts_starts_on_ends_on",
                table: "on_call_shifts",
                columns: new[] { "starts_on", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "ix_on_call_shifts_user_id",
                table: "on_call_shifts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachments_storage_key",
                table: "ticket_attachments",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachments_ticket_id",
                table: "ticket_attachments",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachments_uploaded_by_id",
                table: "ticket_attachments",
                column: "uploaded_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "on_call_shifts");

            migrationBuilder.DropTable(
                name: "ticket_attachments");

            migrationBuilder.DropIndex(
                name: "ix_tickets_search_vector",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "tickets");
        }
    }
}
