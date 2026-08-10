using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DependenciaFechasAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_fin_estimada",
                table: "dependencias",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_inicio_estimada",
                table: "dependencias",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fecha_fin_estimada",
                table: "dependencias");

            migrationBuilder.DropColumn(
                name: "fecha_inicio_estimada",
                table: "dependencias");
        }
    }
}
