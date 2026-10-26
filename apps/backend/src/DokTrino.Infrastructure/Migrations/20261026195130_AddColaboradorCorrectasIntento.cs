using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddColaboradorCorrectasIntento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "colaborador_id",
                table: "cuestionario_intentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "correctas",
                table: "cuestionario_intentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total",
                table: "cuestionario_intentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_cuestionario_intentos_tenant_id_colaborador_id",
                table: "cuestionario_intentos",
                columns: new[] { "tenant_id", "colaborador_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cuestionario_intentos_tenant_id_colaborador_id",
                table: "cuestionario_intentos");

            migrationBuilder.DropColumn(
                name: "colaborador_id",
                table: "cuestionario_intentos");

            migrationBuilder.DropColumn(
                name: "correctas",
                table: "cuestionario_intentos");

            migrationBuilder.DropColumn(
                name: "total",
                table: "cuestionario_intentos");
        }
    }
}
