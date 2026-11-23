using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebTestPdpa.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditContextColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "context_employee_ids",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "context_sections",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "context_employee_ids",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "context_sections",
                table: "audit_logs");
        }
    }
}
