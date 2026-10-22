using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Postgres.Migraciones
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Modo = table.Column<string>(type: "text", nullable: false),
                    Puerto = table.Column<string>(type: "text", nullable: false),
                    Modelo = table.Column<string>(type: "text", nullable: true),
                    Baudios = table.Column<int>(type: "integer", nullable: false),
                    DataBits = table.Column<int>(type: "integer", nullable: false),
                    Paridad = table.Column<string>(type: "text", nullable: false),
                    StopBits = table.Column<int>(type: "integer", nullable: false),
                    Protocolo = table.Column<string>(type: "text", nullable: false),
                    Extraccion = table.Column<string>(type: "text", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Balanzas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelosBalanza",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Baudios = table.Column<int>(type: "integer", nullable: false),
                    DataBits = table.Column<int>(type: "integer", nullable: false),
                    Paridad = table.Column<string>(type: "text", nullable: false),
                    StopBits = table.Column<int>(type: "integer", nullable: false),
                    Protocolo = table.Column<string>(type: "text", nullable: false),
                    Extraccion = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
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
