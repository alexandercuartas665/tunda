using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procesos_definicion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procesos_definicion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proceso_actividades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    detalle = table.Column<string>(type: "text", nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proceso_actividades", x => x.id);
                    table.ForeignKey(
                        name: "fk_proceso_actividades_procesos_definicion_proceso_id",
                        column: x => x.proceso_id,
                        principalTable: "procesos_definicion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proceso_instancias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    radicado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actividad_actual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proceso_instancias", x => x.id);
                    table.ForeignKey(
                        name: "fk_proceso_instancias_procesos_definicion_proceso_id",
                        column: x => x.proceso_id,
                        principalTable: "procesos_definicion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_proceso_instancias_radicados_radicado_id",
                        column: x => x.radicado_id,
                        principalTable: "radicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tareas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instancia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actividad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actividad_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    asignado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_completada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tareas", x => x.id);
                    table.ForeignKey(
                        name: "fk_tareas_proceso_actividades_actividad_id",
                        column: x => x.actividad_id,
                        principalTable: "proceso_actividades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tareas_proceso_instancias_instancia_id",
                        column: x => x.instancia_id,
                        principalTable: "proceso_instancias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_proceso_actividades_proceso_id",
                table: "proceso_actividades",
                column: "proceso_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_actividades_tenant_id_proceso_id_orden",
                table: "proceso_actividades",
                columns: new[] { "tenant_id", "proceso_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "ix_proceso_instancias_proceso_id",
                table: "proceso_instancias",
                column: "proceso_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_instancias_radicado_id",
                table: "proceso_instancias",
                column: "radicado_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_instancias_tenant_id_estado",
                table: "proceso_instancias",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_procesos_definicion_tenant_id_sucursal_codigo_version",
                table: "procesos_definicion",
                columns: new[] { "tenant_id", "sucursal", "codigo", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tareas_actividad_id",
                table: "tareas",
                column: "actividad_id");

            migrationBuilder.CreateIndex(
                name: "ix_tareas_instancia_id",
                table: "tareas",
                column: "instancia_id");

            migrationBuilder.CreateIndex(
                name: "ix_tareas_tenant_id_asignado_id_estado",
                table: "tareas",
                columns: new[] { "tenant_id", "asignado_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_tareas_tenant_id_instancia_id",
                table: "tareas",
                columns: new[] { "tenant_id", "instancia_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tareas");

            migrationBuilder.DropTable(
                name: "proceso_actividades");

            migrationBuilder.DropTable(
                name: "proceso_instancias");

            migrationBuilder.DropTable(
                name: "procesos_definicion");
        }
    }
}
