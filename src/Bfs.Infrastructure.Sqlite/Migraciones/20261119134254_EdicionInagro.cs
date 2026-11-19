using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Sqlite.Migraciones
{
    /// <inheritdoc />
    public partial class EdicionInagro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CondicionIva",
                table: "Transportes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Transportes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Localidad",
                table: "Transportes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_Analisis",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_ClienteCondicionIva",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_ClienteCuit",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_ClienteDireccion",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_ClienteLocalidad",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_CuilChofer",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Inagro_Movimiento",
                table: "Pesadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_ObservacionesRemito",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Inagro_PesoOrigen",
                table: "Pesadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_Planta",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_Precintos",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_ProductoCodigo",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_Tanque",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_TransporteCondicionIva",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_TransporteCuit",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_TransporteDireccion",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inagro_TransporteLocalidad",
                table: "Pesadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoSociedad",
                table: "Empresas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CondicionIva",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Localidad",
                table: "Clientes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sucursales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    CreadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    ModificadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Eliminado = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SyncSeq = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sucursales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tanques",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    CreadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    ModificadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Eliminado = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SyncSeq = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tanques", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_ModificadoEn",
                table: "Sucursales",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_Nombre",
                table: "Sucursales",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_SyncSeq",
                table: "Sucursales",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Tanques_ModificadoEn",
                table: "Tanques",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Tanques_Nombre",
                table: "Tanques",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tanques_SyncSeq",
                table: "Tanques",
                column: "SyncSeq");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sucursales");

            migrationBuilder.DropTable(
                name: "Tanques");

            migrationBuilder.DropColumn(
                name: "CondicionIva",
                table: "Transportes");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Transportes");

            migrationBuilder.DropColumn(
                name: "Localidad",
                table: "Transportes");

            migrationBuilder.DropColumn(
                name: "Inagro_Analisis",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_ClienteCondicionIva",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_ClienteCuit",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_ClienteDireccion",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_ClienteLocalidad",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_CuilChofer",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_Movimiento",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_ObservacionesRemito",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_PesoOrigen",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_Planta",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_Precintos",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_ProductoCodigo",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_Tanque",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_TransporteCondicionIva",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_TransporteCuit",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_TransporteDireccion",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "Inagro_TransporteLocalidad",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "CodigoSociedad",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CondicionIva",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Localidad",
                table: "Clientes");
        }
    }
}
