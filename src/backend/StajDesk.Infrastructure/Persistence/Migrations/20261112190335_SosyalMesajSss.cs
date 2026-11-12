using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StajDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SosyalMesajSss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_a_id = table.Column<int>(type: "integer", nullable: false),
                    user_b_id = table.Column<int>(type: "integer", nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                    table.CheckConstraint("ck_conversations_pair", "user_a_id < user_b_id");
                    table.ForeignKey(
                        name: "fk_conversations_users_user_a_id",
                        column: x => x.user_a_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conversations_users_user_b_id",
                        column: x => x.user_b_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "faq_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    answer = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faq_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "social_posts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    image_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    image_content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    removed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    removed_by_id = table.Column<int>(type: "integer", nullable: true),
                    removed_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_social_posts", x => x.id);
                    table.ForeignKey(
                        name: "fk_social_posts_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "direct_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<int>(type: "integer", nullable: false),
                    sender_id = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_direct_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_direct_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "social_comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_id = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    removed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    removed_by_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_social_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_social_comments_social_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "social_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_social_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "social_likes",
                columns: table => new
                {
                    post_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_social_likes", x => new { x.post_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_social_likes_social_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "social_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_social_likes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "social_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_id = table.Column<int>(type: "integer", nullable: false),
                    reporter_id = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_by_id = table.Column<int>(type: "integer", nullable: true),
                    resolution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_social_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_social_reports_social_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "social_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_social_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_user_a_id_user_b_id",
                table: "conversations",
                columns: new[] { "user_a_id", "user_b_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversations_user_b_id_last_message_at",
                table: "conversations",
                columns: new[] { "user_b_id", "last_message_at" });

            migrationBuilder.CreateIndex(
                name: "ix_direct_messages_conversation_id_id",
                table: "direct_messages",
                columns: new[] { "conversation_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_direct_messages_sender_id_created_at",
                table: "direct_messages",
                columns: new[] { "sender_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_faq_items_category_sort_order",
                table: "faq_items",
                columns: new[] { "category", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_social_comments_author_id",
                table: "social_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_social_comments_post_id_created_at",
                table: "social_comments",
                columns: new[] { "post_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_social_likes_user_id",
                table: "social_likes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_social_posts_author_id_created_at",
                table: "social_posts",
                columns: new[] { "author_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_social_posts_removed_at_created_at",
                table: "social_posts",
                columns: new[] { "removed_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_social_reports_post_id",
                table: "social_reports",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_social_reports_reporter_id",
                table: "social_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_social_reports_resolved_at_created_at",
                table: "social_reports",
                columns: new[] { "resolved_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "direct_messages");

            migrationBuilder.DropTable(
                name: "faq_items");

            migrationBuilder.DropTable(
                name: "social_comments");

            migrationBuilder.DropTable(
                name: "social_likes");

            migrationBuilder.DropTable(
                name: "social_reports");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "social_posts");
        }
    }
}
