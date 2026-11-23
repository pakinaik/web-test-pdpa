using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebTestPdpa.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cipher = table.Column<byte[]>(type: "bytea", nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    download_count = table.Column<int>(type: "integer", nullable: false),
                    last_downloaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_files_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_report_files_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_report_files_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_report_files_conversation_id",
                table: "report_files",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_files_expires_at",
                table: "report_files",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_report_files_message_id",
                table: "report_files",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_files_user_id",
                table: "report_files",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_files");
        }
    }
}
