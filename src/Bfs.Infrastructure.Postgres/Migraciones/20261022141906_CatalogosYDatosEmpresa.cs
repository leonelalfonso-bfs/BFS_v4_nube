using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Postgres.Migraciones
{
    /// <inheritdoc />
    public partial class CatalogosYDatosEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoAta",
                table: "Transportes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsAta",
                table: "Transportes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Localidad",
                table: "Empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provincia",
                table: "Empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Empresas",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DatosVehiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatenteChasis = table.Column<string>(type: "text", nullable: false),
                    PatenteAcoplado = table.Column<string>(type: "text", nullable: true),
                    Conductor = table.Column<string>(type: "text", nullable: true),
                    Documento = table.Column<string>(type: "text", nullable: true),
                    TipoDocumento = table.Column<string>(type: "text", nullable: true),
                    Nacionalidad = table.Column<string>(type: "text", nullable: true),
                    Transporte = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatosVehiculos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exportadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RazonSocial = table.Column<string>(type: "text", nullable: false),
                    Cuit = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exportadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Taras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identificador = table.Column<string>(type: "text", nullable: false),
                    Kilogramos = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    TomadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taras", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transportes_RazonSocial",
                table: "Transportes",
                column: "RazonSocial");

            migrationBuilder.CreateIndex(
                name: "IX_DatosVehiculos_ModificadoEn",
                table: "DatosVehiculos",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_DatosVehiculos_PatenteChasis",
                table: "DatosVehiculos",
                column: "PatenteChasis",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exportadores_ModificadoEn",
                table: "Exportadores",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Exportadores_RazonSocial",
                table: "Exportadores",
                column: "RazonSocial",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Taras_Identificador",
                table: "Taras",
                column: "Identificador",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Taras_ModificadoEn",
                table: "Taras",
                column: "ModificadoEn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatosVehiculos");

            migrationBuilder.DropTable(
                name: "Exportadores");

            migrationBuilder.DropTable(
                name: "Taras");

            migrationBuilder.DropIndex(
                name: "IX_Transportes_RazonSocial",
                table: "Transportes");

            migrationBuilder.DropColumn(
                name: "CodigoAta",
                table: "Transportes");

            migrationBuilder.DropColumn(
                name: "EsAta",
                table: "Transportes");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Localidad",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Provincia",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Empresas");
        }
    }
}
