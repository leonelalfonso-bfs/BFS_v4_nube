using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Postgres.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
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
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuraciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuraciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Cuit = table.Column<string>(type: "text", nullable: true),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    ProximoTicket = table.Column<int>(type: "integer", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Destino = table.Column<string>(type: "text", nullable: false),
                    PesadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketNumero = table.Column<string>(type: "text", nullable: false),
                    Evento = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProximoIntento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UltimoError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transportes",
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
                    table.PrimaryKey("PK_Transportes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pesadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketNumero = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    PatenteChasis = table.Column<string>(type: "text", nullable: false),
                    PatenteAcoplado = table.Column<string>(type: "text", nullable: true),
                    Conductor = table.Column<string>(type: "text", nullable: true),
                    Documento = table.Column<string>(type: "text", nullable: true),
                    Cliente = table.Column<string>(type: "text", nullable: true),
                    Transporte = table.Column<string>(type: "text", nullable: true),
                    Producto = table.Column<string>(type: "text", nullable: true),
                    Procedencia = table.Column<string>(type: "text", nullable: true),
                    Destino = table.Column<string>(type: "text", nullable: true),
                    Comprobante = table.Column<string>(type: "text", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Operador = table.Column<string>(type: "text", nullable: false),
                    Balanza = table.Column<string>(type: "text", nullable: true),
                    PesoEntrada = table.Column<int>(type: "integer", nullable: false),
                    PesoSalida = table.Column<int>(type: "integer", nullable: true),
                    Bruto = table.Column<int>(type: "integer", nullable: true),
                    Tara = table.Column<int>(type: "integer", nullable: true),
                    TaraPallets = table.Column<int>(type: "integer", nullable: false),
                    Neto = table.Column<int>(type: "integer", nullable: true),
                    DatosExtraJson = table.Column<string>(type: "jsonb", nullable: false),
                    EntradaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SalidaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pesadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pesadas_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenNodoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ModificadoEn",
                table: "Clientes",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_RazonSocial",
                table: "Clientes",
                column: "RazonSocial");

            migrationBuilder.CreateIndex(
                name: "IX_Configuraciones_Clave_EmpresaId",
                table: "Configuraciones",
                columns: new[] { "Clave", "EmpresaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Configuraciones_ModificadoEn",
                table: "Configuraciones",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_ModificadoEn",
                table: "Empresas",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Nombre",
                table: "Empresas",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Destino_TicketNumero_Evento",
                table: "Outbox",
                columns: new[] { "Destino", "TicketNumero", "Evento" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Estado_ProximoIntento",
                table: "Outbox",
                columns: new[] { "Estado", "ProximoIntento" });

            migrationBuilder.CreateIndex(
                name: "IX_Pesadas_EmpresaId_TicketNumero",
                table: "Pesadas",
                columns: new[] { "EmpresaId", "TicketNumero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pesadas_ModificadoEn",
                table: "Pesadas",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_ModificadoEn",
                table: "Productos",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Nombre",
                table: "Productos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transportes_ModificadoEn",
                table: "Transportes",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId",
                table: "Usuarios",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ModificadoEn",
                table: "Usuarios",
                column: "ModificadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Username",
                table: "Usuarios",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Configuraciones");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "Pesadas");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Transportes");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Empresas");
        }
    }
}
