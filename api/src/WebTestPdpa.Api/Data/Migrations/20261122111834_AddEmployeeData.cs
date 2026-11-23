using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebTestPdpa.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "income_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name_th = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_income_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_th = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    days_per_year = table.Column<short>(type: "smallint", nullable: false),
                    is_paid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_th = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false),
                    department_th = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title_th = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    first_name_th = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name_th = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    first_name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    national_id = table.Column<string>(type: "char(13)", nullable: false),
                    tax_id = table.Column<string>(type: "char(13)", nullable: false),
                    social_security_no = table.Column<string>(type: "char(13)", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    gender = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    nationality = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    marital_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    religion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    blood_type = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    chronic_disease = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ethnicity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bank_account_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    position_id = table.Column<int>(type: "integer", nullable: false),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.id);
                    table.ForeignKey(
                        name: "fk_employees_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employees_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "employee_emergency_contacts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    relationship = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_emergency_contacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_emergency_contacts_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_leaves",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    leave_type_id = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    days = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_leaves", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_leaves_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_employee_leaves_leave_types_leave_type_id",
                        column: x => x.leave_type_id,
                        principalTable: "leave_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_monthly_incomes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    income_type_id = table.Column<int>(type: "integer", nullable: false),
                    pay_month = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_monthly_incomes", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_monthly_incomes_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_employee_monthly_incomes_income_types_income_type_id",
                        column: x => x.income_type_id,
                        principalTable: "income_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_salary_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    base_salary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    position_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_salary_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_salary_history_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_employee_salary_history_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_emergency_contacts_employee_id",
                table: "employee_emergency_contacts",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_leaves_employee_id_start_date",
                table: "employee_leaves",
                columns: new[] { "employee_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_leaves_leave_type_id",
                table: "employee_leaves",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_monthly_incomes_employee_id_income_type_id_pay_mon",
                table: "employee_monthly_incomes",
                columns: new[] { "employee_id", "income_type_id", "pay_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_monthly_incomes_income_type_id",
                table: "employee_monthly_incomes",
                column: "income_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_salary_history_employee_id_effective_date",
                table: "employee_salary_history",
                columns: new[] { "employee_id", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_salary_history_position_id",
                table: "employee_salary_history",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_employee_code",
                table: "employees",
                column: "employee_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_national_id",
                table: "employees",
                column: "national_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_position_id",
                table: "employees",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_user_id",
                table: "employees",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_income_types_code",
                table: "income_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leave_types_code",
                table: "leave_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_positions_code",
                table: "positions",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_emergency_contacts");

            migrationBuilder.DropTable(
                name: "employee_leaves");

            migrationBuilder.DropTable(
                name: "employee_monthly_incomes");

            migrationBuilder.DropTable(
                name: "employee_salary_history");

            migrationBuilder.DropTable(
                name: "leave_types");

            migrationBuilder.DropTable(
                name: "income_types");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "positions");
        }
    }
}
