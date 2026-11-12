using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StajDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrganizasyonVeKimlik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "department_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "manager_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "position_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "password_reset_tokens",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    manager_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                    table.CheckConstraint("ck_departments_not_own_parent", "parent_id IS NULL OR parent_id <> id");
                    table.ForeignKey(
                        name: "fk_departments_departments_parent_id",
                        column: x => x.parent_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_departments_users_manager_id",
                        column: x => x.manager_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "employee_profiles",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    termination_date = table.Column<DateOnly>(type: "date", nullable: true),
                    employment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    personal_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    emergency_contact_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    emergency_contact_relation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    photo_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    cover_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_profiles", x => x.user_id);
                    table.CheckConstraint("ck_employee_profiles_dates", "termination_date IS NULL OR hire_date IS NULL OR termination_date >= hire_date");
                    table.ForeignKey(
                        name: "fk_employee_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "otp_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_otp_challenges", x => x.id);
                    table.ForeignKey(
                        name: "fk_otp_challenges_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.id);
                    table.CheckConstraint("ck_positions_level", "level BETWEEN 1 AND 20");
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    theme = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    email_ticket_updates = table.Column<bool>(type: "boolean", nullable: false),
                    email_announcements = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_department_id",
                table: "users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_manager_id",
                table: "users",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_position_id",
                table: "users",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true,
                filter: "username IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_not_own_manager",
                table: "users",
                sql: "manager_id IS NULL OR manager_id <> id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_code",
                table: "departments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_departments_manager_id",
                table: "departments",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_parent_id",
                table: "departments",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_profiles_employee_number",
                table: "employee_profiles",
                column: "employee_number",
                unique: true,
                filter: "employee_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_otp_challenges_user_id",
                table: "otp_challenges",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_title",
                table: "positions",
                column: "title",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_departments_department_id",
                table: "users",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_users_positions_position_id",
                table: "users",
                column: "position_id",
                principalTable: "positions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_users_users_manager_id",
                table: "users",
                column: "manager_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ---- Veri taşıma: mevcut kayıtlar kaybolmasın ----
            // 1) Eski serbest metin "department" değerleri departman tablosuna taşınır ve kullanıcılar bağlanır.
            migrationBuilder.Sql("""
                INSERT INTO departments (code, name, description, is_active, created_at)
                SELECT 'D' || row_number() OVER (ORDER BY d), d, 'Eski departman alanından taşındı', true, now()
                FROM (SELECT DISTINCT trim(department) AS d FROM users WHERE department IS NOT NULL AND trim(department) <> '') x;

                UPDATE users u SET department_id = dp.id
                FROM departments dp
                WHERE dp.description = 'Eski departman alanından taşındı' AND dp.name = trim(u.department);
                """);
            // 2) Bu sürümden önce açılmış hesaplar doğrulanmış sayılır (yeni kural yalnızca yeni kayıtlara uygulanır).
            migrationBuilder.Sql("UPDATE users SET email_verified_at = created_at WHERE email_verified_at IS NULL AND password_hash <> '';");
            // 3) Her kullanıcı için boş personel kartı.
            migrationBuilder.Sql("""
                INSERT INTO employee_profiles (user_id, employment_type)
                SELECT id, 'FullTime' FROM users u WHERE NOT EXISTS (SELECT 1 FROM employee_profiles p WHERE p.user_id = u.id);
                """);

            migrationBuilder.DropColumn(
                name: "department",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_departments_department_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_users_positions_position_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_users_users_manager_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "employee_profiles");

            migrationBuilder.DropTable(
                name: "otp_challenges");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "user_settings");

            migrationBuilder.DropIndex(
                name: "ix_users_department_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_manager_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_position_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_username",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_not_own_manager",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email_verified_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "manager_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "position_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "username",
                table: "users");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "password_reset_tokens");

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
