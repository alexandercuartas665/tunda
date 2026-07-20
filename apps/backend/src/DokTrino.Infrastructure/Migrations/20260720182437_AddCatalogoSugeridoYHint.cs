using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogoSugeridoYHint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "tipologias_documentales",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MAESTRA");

            migrationBuilder.AddColumn<Guid>(
                name: "sugerida_por_dependencia_id",
                table: "tipologias_documentales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "subseries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MAESTRA");

            migrationBuilder.AddColumn<Guid>(
                name: "sugerida_por_dependencia_id",
                table: "subseries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "series",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MAESTRA");

            migrationBuilder.AddColumn<Guid>(
                name: "sugerida_por_dependencia_id",
                table: "series",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mostrar_hint",
                table: "formaciones_dependencia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_tipologias_documentales_tenant_id_estado",
                table: "tipologias_documentales",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_subseries_tenant_id_estado",
                table: "subseries",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_series_tenant_id_estado",
                table: "series",
                columns: new[] { "tenant_id", "estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tipologias_documentales_tenant_id_estado",
                table: "tipologias_documentales");

            migrationBuilder.DropIndex(
                name: "ix_subseries_tenant_id_estado",
                table: "subseries");

            migrationBuilder.DropIndex(
                name: "ix_series_tenant_id_estado",
                table: "series");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "tipologias_documentales");

            migrationBuilder.DropColumn(
                name: "sugerida_por_dependencia_id",
                table: "tipologias_documentales");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "sugerida_por_dependencia_id",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "series");

            migrationBuilder.DropColumn(
                name: "sugerida_por_dependencia_id",
                table: "series");

            migrationBuilder.DropColumn(
                name: "mostrar_hint",
                table: "formaciones_dependencia");
        }
    }
}
