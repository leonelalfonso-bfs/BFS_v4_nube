using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Sqlite.Migraciones
{
    /// <inheritdoc />
    public partial class PesajeCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Aduana_BultoBruto",
                table: "Pesadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_BultoId",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Aduana_BultoTara",
                table: "Pesadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_ChoferNacionalidad",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_ChoferTipoDocumento",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Aduana_ContenedorBruto",
                table: "Pesadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_ContenedorId",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Aduana_ContenedorNeto",
                table: "Pesadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Aduana_ContenedorTara",
                table: "Pesadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_Escenario",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_Exportador",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_ExportadorCuit",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aduana_OperacionId",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AnuladaEn",
                table: "Pesadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnuladaPor",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BalanzaSalida",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntradaManualPor",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modo",
                table: "Pesadas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperadorSalida",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalidaManualPor",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoOperacion",
                table: "Pesadas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ContadoresTicket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Puesto = table.Column<int>(type: "INTEGER", nullable: false),
                    Proximo = table.Column<int>(type: "INTEGER", nullable: false),
                    CreadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    ModificadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Eliminado = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContadoresTicket", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pesadas_Estado_EntradaEn",
                table: "Pesadas",
                columns: new[] { "Estado", "EntradaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_ContadoresTicket_EmpresaId_Puesto",
                table: "ContadoresTicket",
                columns: new[] { "EmpresaId", "Puesto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContadoresTicket_ModificadoEn",
                table: "ContadoresTicket",
                column: "ModificadoEn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContadoresTicket");

            migrationBuilder.DropIndex(
                name: "IX_Pesadas_Estado_EntradaEn",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_BultoBruto",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_BultoId",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_BultoTara",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_ChoferNacionalidad",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_ChoferTipoDocumento",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_ContenedorBruto",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_ContenedorId",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_ContenedorNeto",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_ContenedorTara",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_Escenario",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_Exportador",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_ExportadorCuit",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Aduana_OperacionId",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "AnuladaEn",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "AnuladaPor",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "BalanzaSalida",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "EntradaManualPor",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Modo",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "OperadorSalida",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "SalidaManualPor",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "TipoOperacion",
                table: "Pesadas");
        }
    }
}
