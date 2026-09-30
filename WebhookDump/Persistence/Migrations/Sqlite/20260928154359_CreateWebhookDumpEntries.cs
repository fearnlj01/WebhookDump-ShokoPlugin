using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shoko.Plugin.WebhookDump.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class CreateWebhookDumpEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebhookDumpEntries",
                columns: table => new
                {
                    VideoFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscordMessageId = table.Column<long>(type: "INTEGER", nullable: true),
                    InsertionTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDumpEntries", x => x.VideoFileId);
                });

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
