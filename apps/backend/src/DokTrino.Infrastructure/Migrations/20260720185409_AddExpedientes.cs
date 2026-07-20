using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpedientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "dependencia_id",
                table: "archivos_digitales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "expediente_id",
                table: "archivos_digitales",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "expedientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ABIERTO"),
                    fecha_apertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expedientes", x => x.id);
                    table.ForeignKey(
                        name: "fk_expedientes_dependencias_dependencia_id",
                        column: x => x.dependencia_id,
                        principalTable: "dependencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_expedientes_series_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_dependencia_id",
                table: "archivos_digitales",
                column: "dependencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_expediente_id",
                table: "archivos_digitales",
                column: "expediente_id");

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_dependencia_id",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "dependencia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_expediente_id",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "expediente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_dependencia_id",
                table: "expedientes",
                column: "dependencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_serie_id",
                table: "expedientes",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_tenant_id_codigo",
                table: "expedientes",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_tenant_id_estado",
                table: "expedientes",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.AddForeignKey(
                name: "fk_archivos_digitales_dependencias_dependencia_id",
                table: "archivos_digitales",
                column: "dependencia_id",
                principalTable: "dependencias",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_archivos_digitales_expedientes_expediente_id",
                table: "archivos_digitales",
                column: "expediente_id",
                principalTable: "expedientes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_archivos_digitales_dependencias_dependencia_id",
                table: "archivos_digitales");

            migrationBuilder.DropForeignKey(
                name: "fk_archivos_digitales_expedientes_expediente_id",
                table: "archivos_digitales");

            migrationBuilder.DropTable(
                name: "expedientes");

            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_dependencia_id",
                table: "archivos_digitales");

            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_expediente_id",
                table: "archivos_digitales");

            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_tenant_id_dependencia_id",
                table: "archivos_digitales");

            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_tenant_id_expediente_id",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "dependencia_id",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "expediente_id",
                table: "archivos_digitales");
        }
    }
}
