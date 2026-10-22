using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bfs.Infrastructure.Postgres.Migraciones
{
    /// <inheritdoc />
    public partial class UsuariosEmailYCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Username",
                table: "Usuarios",
                newName: "Email");

            migrationBuilder.RenameIndex(
                name: "IX_Usuarios_Username",
                table: "Usuarios",
                newName: "IX_Usuarios_Email");

            migrationBuilder.AddColumn<Guid>(
                name: "ClienteId",
                table: "Usuarios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClienteId",
                table: "Pesadas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ClienteId",
                table: "Usuarios",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pesadas_ClienteId_EntradaEn",
                table: "Pesadas",
                columns: new[] { "ClienteId", "EntradaEn" });

            migrationBuilder.AddForeignKey(
                name: "FK_Pesadas_Clientes_ClienteId",
                table: "Pesadas",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Clientes_ClienteId",
                table: "Usuarios",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pesadas_Clientes_ClienteId",
                table: "Pesadas");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Clientes_ClienteId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_ClienteId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Pesadas_ClienteId_EntradaEn",
                table: "Pesadas");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Pesadas");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Usuarios",
                newName: "Username");

            migrationBuilder.RenameIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                newName: "IX_Usuarios_Username");
        }
    }
}
