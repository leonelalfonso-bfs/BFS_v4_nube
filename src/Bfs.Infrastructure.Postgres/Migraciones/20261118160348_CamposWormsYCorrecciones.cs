using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Postgres.Migraciones
{
    /// <inheritdoc />
    public partial class CamposWormsYCorrecciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aduana_Ata",
                table: "Pesadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_AtaCuit",
                table: "Pesadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_DestinacionAduanera",
                table: "Pesadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_IdDestino",
                table: "Pesadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_Lot",
                table: "Pesadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_ProcedenciaDestino",
                table: "Pesadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorreccionesJson",
                table: "Pesadas",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Nacionalidad",
                table: "Pesadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoDocumento",
                table: "Pesadas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aduana_Ata",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_AtaCuit",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_DestinacionAduanera",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_IdDestino",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_Lot",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_ProcedenciaDestino",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "CorreccionesJson",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Nacionalidad",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "TipoDocumento",
                table: "Pesadas");
        }
    }
}
