using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shoko.Plugin.WebhookDump.Persistence.Migrations.MySql
{
    /// <inheritdoc />
    public partial class CreateWebhookDumpEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WebhookDumpEntries",
                columns: table => new
                {
                    VideoFileId = table.Column<int>(type: "int", nullable: false),
                    DiscordMessageId = table.Column<long>(type: "bigint", nullable: true),
                    InsertionTimestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDumpEntries", x => x.VideoFileId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDumpEntries_InsertionTimestamp",
                table: "WebhookDumpEntries",
                column: "InsertionTimestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookDumpEntries");
        }
    }
}
