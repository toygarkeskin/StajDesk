using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StajDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KurumsalSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "show_on_website",
                table: "departments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_public",
                table: "announcements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "advertisements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    image_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    link_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    page = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    placement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_advertisements", x => x.id);
                    table.CheckConstraint("ck_advertisements_dates", "ends_at IS NULL OR ends_at > starts_at");
                });

            migrationBuilder.CreateTable(
                name: "external_snapshots",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_snapshots", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "job_openings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    employment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    requirements = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closes_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_openings", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_openings_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "media_files",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    uploaded_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_files", x => x.key);
                    table.ForeignKey(
                        name: "fk_media_files_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "news",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    image_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news", x => x.id);
                    table.ForeignKey(
                        name: "fk_news_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "site_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    value = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "ad_stats_daily",
                columns: table => new
                {
                    advertisement_id = table.Column<int>(type: "integer", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    impressions = table.Column<int>(type: "integer", nullable: false),
                    clicks = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ad_stats_daily", x => new { x.advertisement_id, x.day });
                    table.ForeignKey(
                        name: "fk_ad_stats_daily_advertisements_advertisement_id",
                        column: x => x.advertisement_id,
                        principalTable: "advertisements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_advertisements_page_placement_is_active",
                table: "advertisements",
                columns: new[] { "page", "placement", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_job_openings_department_id",
                table: "job_openings",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_openings_is_published_published_at",
                table: "job_openings",
                columns: new[] { "is_published", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_media_files_uploaded_by_id",
                table: "media_files",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_author_id",
                table: "news",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_is_published_published_at",
                table: "news",
                columns: new[] { "is_published", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_news_slug",
                table: "news",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_stats_daily");

            migrationBuilder.DropTable(
                name: "external_snapshots");

            migrationBuilder.DropTable(
                name: "job_openings");

            migrationBuilder.DropTable(
                name: "media_files");

            migrationBuilder.DropTable(
                name: "news");

            migrationBuilder.DropTable(
                name: "site_settings");

            migrationBuilder.DropTable(
                name: "advertisements");

            migrationBuilder.DropColumn(
                name: "show_on_website",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "is_public",
                table: "announcements");
        }
    }
}
