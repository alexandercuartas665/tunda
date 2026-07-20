using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBiServicios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "power_bi_reportes");

            migrationBuilder.CreateTable(
                name: "bi_servicios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    schema_consulta = table.Column<string>(type: "jsonb", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bi_servicios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bi_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_uso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracion_ms = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bi_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_bi_logs_bi_servicios_servicio_id",
                        column: x => x.servicio_id,
                        principalTable: "bi_servicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bi_tokens_uso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parametros = table.Column<string>(type: "jsonb", nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bi_tokens_uso", x => x.id);
                    table.ForeignKey(
                        name: "fk_bi_tokens_uso_bi_servicios_servicio_id",
                        column: x => x.servicio_id,
                        principalTable: "bi_servicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bi_logs_servicio_id",
                table: "bi_logs",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_bi_logs_tenant_id_servicio_id_fecha",
                table: "bi_logs",
                columns: new[] { "tenant_id", "servicio_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_bi_servicios_tenant_id_codigo",
                table: "bi_servicios",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bi_tokens_uso_servicio_id",
                table: "bi_tokens_uso",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_bi_tokens_uso_tenant_id_servicio_id",
                table: "bi_tokens_uso",
                columns: new[] { "tenant_id", "servicio_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bi_tokens_uso_token",
                table: "bi_tokens_uso",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bi_logs");

            migrationBuilder.DropTable(
                name: "bi_tokens_uso");

            migrationBuilder.DropTable(
                name: "bi_servicios");

            migrationBuilder.CreateTable(
                name: "power_bi_reportes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    embed_url = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_power_bi_reportes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_power_bi_reportes_tenant_id_orden",
                table: "power_bi_reportes",
                columns: new[] { "tenant_id", "orden" });
        }
    }
}
