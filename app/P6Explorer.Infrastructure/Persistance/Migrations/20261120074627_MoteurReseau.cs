using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace P6Explorer.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class MoteurReseau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Filtrage",
                table: "ReglesNat",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Aucune");   // lignes existantes : redirection sans règle associée

            migrationBuilder.AddColumn<string>(
                name: "Services",
                table: "Noeuds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Affectation",
                table: "Interfaces",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Filtrage",
                table: "ReglesNat");

            migrationBuilder.DropColumn(
                name: "Services",
                table: "Noeuds");

            migrationBuilder.DropColumn(
                name: "Affectation",
                table: "Interfaces");
        }
    }
}
