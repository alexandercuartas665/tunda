using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelefonoYTokenPorColaborador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "colaborador_id",
                table: "tokens_dependencia",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefono",
                table: "colaboradores_dependencia",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "colaborador_id",
                table: "tokens_dependencia");

            migrationBuilder.DropColumn(
                name: "telefono",
                table: "colaboradores_dependencia");
        }
    }
}
