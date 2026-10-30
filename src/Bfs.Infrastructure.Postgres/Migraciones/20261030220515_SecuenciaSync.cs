using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Postgres.Migraciones
{
    /// <inheritdoc />
    public partial class SecuenciaSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Usuarios",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Transportes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Taras",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Productos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Pesadas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "ModelosBalanza",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Exportadores",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Empresas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "DatosVehiculos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "ContadoresTicket",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Configuraciones",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Clientes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SyncSeq",
                table: "Balanzas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_SyncSeq",
                table: "Usuarios",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Transportes_SyncSeq",
                table: "Transportes",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Taras_SyncSeq",
                table: "Taras",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_SyncSeq",
                table: "Productos",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Pesadas_SyncSeq",
                table: "Pesadas",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_ModelosBalanza_SyncSeq",
                table: "ModelosBalanza",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Exportadores_SyncSeq",
                table: "Exportadores",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_SyncSeq",
                table: "Empresas",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_DatosVehiculos_SyncSeq",
                table: "DatosVehiculos",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_ContadoresTicket_SyncSeq",
                table: "ContadoresTicket",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Configuraciones_SyncSeq",
                table: "Configuraciones",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_SyncSeq",
                table: "Clientes",
                column: "SyncSeq");

            migrationBuilder.CreateIndex(
                name: "IX_Balanzas_SyncSeq",
                table: "Balanzas",
                column: "SyncSeq");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_SyncSeq",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Transportes_SyncSeq",
                table: "Transportes");

            migrationBuilder.DropIndex(
                name: "IX_Taras_SyncSeq",
                table: "Taras");

            migrationBuilder.DropIndex(
                name: "IX_Productos_SyncSeq",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Pesadas_SyncSeq",
                table: "Pesadas");

            migrationBuilder.DropIndex(
                name: "IX_ModelosBalanza_SyncSeq",
                table: "ModelosBalanza");

            migrationBuilder.DropIndex(
                name: "IX_Exportadores_SyncSeq",
                table: "Exportadores");

            migrationBuilder.DropIndex(
                name: "IX_Empresas_SyncSeq",
                table: "Empresas");

            migrationBuilder.DropIndex(
                name: "IX_DatosVehiculos_SyncSeq",
                table: "DatosVehiculos");

            migrationBuilder.DropIndex(
                name: "IX_ContadoresTicket_SyncSeq",
                table: "ContadoresTicket");

            migrationBuilder.DropIndex(
                name: "IX_Configuraciones_SyncSeq",
                table: "Configuraciones");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_SyncSeq",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Balanzas_SyncSeq",
                table: "Balanzas");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Transportes");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Taras");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "ModelosBalanza");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Exportadores");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "DatosVehiculos");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "ContadoresTicket");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Configuraciones");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "SyncSeq",
                table: "Balanzas");
        }
    }
}
