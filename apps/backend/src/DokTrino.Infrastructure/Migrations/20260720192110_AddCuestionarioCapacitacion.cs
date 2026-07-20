using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCuestionarioCapacitacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cuestionarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    puntaje_minimo = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuestionarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cuestionario_intentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuestionario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    puntaje = table.Column<int>(type: "integer", nullable: false),
                    aprobado = table.Column<bool>(type: "boolean", nullable: false),
                    respuestas_json = table.Column<string>(type: "jsonb", nullable: false),
                    fecha_intento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuestionario_intentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_cuestionario_intentos_cuestionarios_cuestionario_id",
                        column: x => x.cuestionario_id,
                        principalTable: "cuestionarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cuestionario_intentos_dependencias_dependencia_id",
                        column: x => x.dependencia_id,
                        principalTable: "dependencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cuestionario_preguntas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuestionario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    enunciado = table.Column<string>(type: "text", nullable: false),
                    opciones_json = table.Column<string>(type: "jsonb", nullable: false),
                    indice_correcto = table.Column<int>(type: "integer", nullable: false),
                    retroalimentacion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuestionario_preguntas", x => x.id);
                    table.ForeignKey(
                        name: "fk_cuestionario_preguntas_cuestionarios_cuestionario_id",
                        column: x => x.cuestionario_id,
                        principalTable: "cuestionarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cuestionario_intentos_cuestionario_id",
                table: "cuestionario_intentos",
                column: "cuestionario_id");

            migrationBuilder.CreateIndex(
                name: "ix_cuestionario_intentos_dependencia_id",
                table: "cuestionario_intentos",
                column: "dependencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_cuestionario_intentos_tenant_id_dependencia_id",
                table: "cuestionario_intentos",
                columns: new[] { "tenant_id", "dependencia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cuestionario_preguntas_cuestionario_id_orden",
                table: "cuestionario_preguntas",
                columns: new[] { "cuestionario_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "ix_cuestionarios_tenant_id_modulo",
                table: "cuestionarios",
                columns: new[] { "tenant_id", "modulo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cuestionario_intentos");

            migrationBuilder.DropTable(
                name: "cuestionario_preguntas");

            migrationBuilder.DropTable(
                name: "cuestionarios");
        }
    }
}
