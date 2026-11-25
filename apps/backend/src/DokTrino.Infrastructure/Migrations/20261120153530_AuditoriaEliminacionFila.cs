using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaEliminacionFila : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trazas_eliminacion_respuesta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trd_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    serie_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    subserie_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tipologia_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    formatos = table.Column<int>(type: "integer", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    actor = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trazas_eliminacion_respuesta", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trazas_eliminacion_respuesta_tenant_id_dependencia_id_fecha",
                table: "trazas_eliminacion_respuesta",
                columns: new[] { "tenant_id", "dependencia_id", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trazas_eliminacion_respuesta");
        }
    }
}
