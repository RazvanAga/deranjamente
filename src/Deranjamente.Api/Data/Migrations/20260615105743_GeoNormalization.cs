using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Deranjamente.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GeoNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GeoUnresolved",
                table: "Outages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Judete",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SirutaCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsCovered = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Judete", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalitateAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JudetCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    NormalizedAlias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SirutaCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalitateAliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Localitati",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirutaCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JudetCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Localitati", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Judete_Code",
                table: "Judete",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Judete_Name",
                table: "Judete",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalitateAliases_JudetCode_NormalizedAlias",
                table: "LocalitateAliases",
                columns: new[] { "JudetCode", "NormalizedAlias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Localitati_JudetCode_NormalizedName",
                table: "Localitati",
                columns: new[] { "JudetCode", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_Localitati_SirutaCode",
                table: "Localitati",
                column: "SirutaCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Judete");

            migrationBuilder.DropTable(
                name: "LocalitateAliases");

            migrationBuilder.DropTable(
                name: "Localitati");

            migrationBuilder.DropColumn(
                name: "GeoUnresolved",
                table: "Outages");
        }
    }
}
