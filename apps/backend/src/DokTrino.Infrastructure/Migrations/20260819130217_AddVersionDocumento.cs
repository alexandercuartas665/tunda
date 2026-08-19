using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_version_vigente",
                table: "archivos_digitales",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "archivos_digitales",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "version_grupo_id",
                table: "archivos_digitales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill: cada documento existente es su propio grupo, version 1, vigente.
            migrationBuilder.Sql(
                "UPDATE archivos_digitales SET version_grupo_id = id " +
                "WHERE version_grupo_id = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_version_grupo_id",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "version_grupo_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_tenant_id_version_grupo_id",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "es_version_vigente",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "version",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "version_grupo_id",
                table: "archivos_digitales");
        }
    }
}
