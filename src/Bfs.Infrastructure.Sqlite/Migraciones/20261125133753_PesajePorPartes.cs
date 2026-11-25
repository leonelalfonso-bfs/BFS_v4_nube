using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Sqlite.Migraciones
{
    /// <inheritdoc />
    public partial class PesajePorPartes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PartesJson",
                table: "Pesadas",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PartesJson",
                table: "Pesadas");
        }
    }
}
