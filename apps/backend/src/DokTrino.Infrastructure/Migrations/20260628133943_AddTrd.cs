using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "series_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    legacy_reg = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series_documentales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "serie_disposiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ag_anios = table.Column<int>(type: "integer", nullable: true),
                    ac_anios = table.Column<int>(type: "integer", nullable: true),
                    conservacion_permanente = table.Column<bool>(type: "boolean", nullable: false),
                    eliminacion = table.Column<bool>(type: "boolean", nullable: false),
                    seleccion = table.Column<bool>(type: "boolean", nullable: false),
                    procedimiento = table.Column<string>(type: "text", nullable: true),
                    legacy_reg = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_serie_disposiciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_serie_disposiciones_series_documentales_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subseries_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    legacy_reg = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subseries_documentales", x => x.id);
                    table.ForeignKey(
                        name: "fk_subseries_documentales_series_documentales_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tipologias_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subserie_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_tipologia = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    legacy_reg = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipologias_documentales", x => x.id);
                    table.ForeignKey(
                        name: "fk_tipologias_documentales_series_documentales_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tipologias_documentales_subseries_documentales_subserie_id",
                        column: x => x.subserie_id,
                        principalTable: "subseries_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_serie_disposiciones_serie_id",
                table: "serie_disposiciones",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_serie_disposiciones_tenant_id_serie_id",
                table: "serie_disposiciones",
                columns: new[] { "tenant_id", "serie_id" });

            migrationBuilder.CreateIndex(
                name: "ix_series_documentales_tenant_id_sucursal_codigo",
                table: "series_documentales",
                columns: new[] { "tenant_id", "sucursal", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subseries_documentales_serie_id",
                table: "subseries_documentales",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_subseries_documentales_tenant_id_serie_id_codigo",
                table: "subseries_documentales",
                columns: new[] { "tenant_id", "serie_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipologias_documentales_serie_id",
                table: "tipologias_documentales",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipologias_documentales_subserie_id",
                table: "tipologias_documentales",
                column: "subserie_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipologias_documentales_tenant_id_sucursal_codigo",
                table: "tipologias_documentales",
                columns: new[] { "tenant_id", "sucursal", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "serie_disposiciones");

            migrationBuilder.DropTable(
                name: "tipologias_documentales");

            migrationBuilder.DropTable(
                name: "subseries_documentales");

            migrationBuilder.DropTable(
                name: "series_documentales");
        }
    }
}
