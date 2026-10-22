using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Sqlite.Migraciones
{
    /// <inheritdoc />
    public partial class BalanzasYModelos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Balanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Modo = table.Column<string>(type: "TEXT", nullable: false),
                    Puerto = table.Column<string>(type: "TEXT", nullable: false),
                    Modelo = table.Column<string>(type: "TEXT", nullable: true),
                    Baudios = table.Column<int>(type: "INTEGER", nullable: false),
                    DataBits = table.Column<int>(type: "INTEGER", nullable: false),
                    Paridad = table.Column<string>(type: "TEXT", nullable: false),
                    StopBits = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocolo = table.Column<string>(type: "TEXT", nullable: false),
                    Extraccion = table.Column<string>(type: "TEXT", nullable: true),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    ModificadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Eliminado = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Balanzas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelosBalanza",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Baudios = table.Column<int>(type: "INTEGER", nullable: false),
                    DataBits = table.Column<int>(type: "INTEGER", nullable: false),
                    Paridad = table.Column<string>(type: "TEXT", nullable: false),
                    StopBits = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocolo = table.Column<string>(type: "TEXT", nullable: false),
                    Extraccion = table.Column<string>(type: "TEXT", nullable: true),
                    CreadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    ModificadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Eliminado = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosBalanza", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Balanzas_ModificadoEn",
                table: "Balanzas",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Balanzas_Nombre",
                table: "Balanzas",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelosBalanza_ModificadoEn",
                table: "ModelosBalanza",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_ModelosBalanza_Nombre",
                table: "ModelosBalanza",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Balanzas");

            migrationBuilder.DropTable(
                name: "ModelosBalanza");
        }
    }
}
