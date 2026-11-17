using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArchivoCargaDependencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archivos_carga_dependencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trd_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    registros = table.Column<int>(type: "integer", nullable: false),
                    formatos = table.Column<int>(type: "integer", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    archivado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archivado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    archivado_por_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    restaurado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    restaurado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    restaurado_por_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archivos_carga_dependencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_archivos_carga_dependencia_dependencias_dependencia_id",
                        column: x => x.dependencia_id,
                        principalTable: "dependencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_carga_dependencia_dependencia_id",
                table: "archivos_carga_dependencia",
                column: "dependencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_archivos_carga_dependencia_tenant_id_dependencia_id_archiva",
                table: "archivos_carga_dependencia",
                columns: new[] { "tenant_id", "dependencia_id", "archivado_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archivos_carga_dependencia");
        }
    }
}
