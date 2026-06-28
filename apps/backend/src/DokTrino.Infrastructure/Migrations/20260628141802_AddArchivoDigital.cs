using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivoDigital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archivos_digitales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    carpeta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipologia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    blob_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    mime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fecha_subida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_archivos_digitales", x => x.id);
                    table.ForeignKey(
                        name: "fk_archivos_digitales_carpetas_carpeta_id",
                        column: x => x.carpeta_id,
                        principalTable: "carpetas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_archivos_digitales_tipologias_documentales_tipologia_id",
                        column: x => x.tipologia_id,
                        principalTable: "tipologias_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_carpeta_id",
                table: "archivos_digitales",
                column: "carpeta_id");

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_carpeta_id",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "carpeta_id" });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_fecha_subida",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "fecha_subida" });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tipologia_id",
                table: "archivos_digitales",
                column: "tipologia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archivos_digitales");
        }
    }
}
