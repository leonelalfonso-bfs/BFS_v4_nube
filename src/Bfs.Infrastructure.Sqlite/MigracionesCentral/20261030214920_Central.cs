using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Sqlite.MigracionesCentral
{
    /// <inheritdoc />
    public partial class Central : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Superadmins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Superadmins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", nullable: false),
                    MotivoEstado = table.Column<string>(type: "TEXT", nullable: true),
                    CreadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    EstadoDesde = table.Column<long>(type: "INTEGER", nullable: true),
                    Notas = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Logins",
                columns: table => new
                {
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logins", x => x.Email);
                    table.ForeignKey(
                        name: "FK_Logins_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Nodos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    CodigoActivacionHash = table.Column<string>(type: "TEXT", nullable: true),
                    CodigoVence = table.Column<long>(type: "INTEGER", nullable: true),
                    TokenHash = table.Column<string>(type: "TEXT", nullable: true),
                    Maquina = table.Column<string>(type: "TEXT", nullable: true),
                    CreadoEn = table.Column<long>(type: "INTEGER", nullable: false),
                    ActivadoEn = table.Column<long>(type: "INTEGER", nullable: true),
                    UltimaSincronizacion = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nodos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Logins_TenantId",
                table: "Logins",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodos_TenantId",
                table: "Nodos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Superadmins_Email",
                table: "Superadmins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Codigo",
                table: "Tenants",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Logins");

            migrationBuilder.DropTable(
                name: "Nodos");

            migrationBuilder.DropTable(
                name: "Superadmins");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
