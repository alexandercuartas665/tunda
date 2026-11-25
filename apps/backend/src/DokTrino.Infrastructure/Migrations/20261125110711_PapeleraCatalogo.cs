using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PapeleraCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trazas_catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    accion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    elemento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    nombre_anterior = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    campos_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    origen = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    actor = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    restaurado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    restaurado_por_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trazas_catalogo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trazas_catalogo_tenant_id_elemento_id",
                table: "trazas_catalogo",
                columns: new[] { "tenant_id", "elemento_id" });

            migrationBuilder.CreateIndex(
                name: "ix_trazas_catalogo_tenant_id_fecha",
                table: "trazas_catalogo",
                columns: new[] { "tenant_id", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trazas_catalogo");
        }
    }
}
