using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Deranjamente.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrawlPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "Outages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CrawlerSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Judet = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CadenceMinutes = table.Column<int>(type: "integer", nullable: false),
                    LookaheadDays = table.Column<int>(type: "integer", nullable: false),
                    Attribution = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlerSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrawlRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CrawlerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    RowsFound = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outages_Provider_ContentHash",
                table: "Outages",
                columns: new[] { "Provider", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_CrawlerSources_Key",
                table: "CrawlerSources",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrawlRuns_CrawlerKey_StartedAt",
                table: "CrawlRuns",
                columns: new[] { "CrawlerKey", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrawlerSources");

            migrationBuilder.DropTable(
                name: "CrawlRuns");

            migrationBuilder.DropIndex(
                name: "IX_Outages_Provider_ContentHash",
                table: "Outages");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "Outages");
        }
    }
}
