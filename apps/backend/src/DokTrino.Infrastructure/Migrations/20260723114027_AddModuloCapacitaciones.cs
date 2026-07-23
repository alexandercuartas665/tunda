using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloCapacitaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cursos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    cuestionario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cursos", x => x.id);
                    table.ForeignKey(
                        name: "fk_cursos_cuestionarios_cuestionario_id",
                        column: x => x.cuestionario_id,
                        principalTable: "cuestionarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "configuraciones_curso_cliente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    intentos_max = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuraciones_curso_cliente", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuraciones_curso_cliente_cursos_curso_id",
                        column: x => x.curso_id,
                        principalTable: "cursos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "curso_modulos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curso_modulos", x => x.id);
                    table.ForeignKey(
                        name: "fk_curso_modulos_cursos_curso_id",
                        column: x => x.curso_id,
                        principalTable: "cursos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "curso_progresos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    colaborador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_aprobacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    mejor_nota = table.Column<int>(type: "integer", nullable: false),
                    aprobado = table.Column<bool>(type: "boolean", nullable: false),
                    bloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    desbloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    desbloqueado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_desbloqueo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curso_progresos", x => x.id);
                    table.ForeignKey(
                        name: "fk_curso_progresos_cursos_curso_id",
                        column: x => x.curso_id,
                        principalTable: "cursos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "curso_lecciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_modulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    objeto_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: true),
                    contenido = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curso_lecciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_curso_lecciones_curso_modulos_curso_modulo_id",
                        column: x => x.curso_modulo_id,
                        principalTable: "curso_modulos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuraciones_curso_cliente_curso_id",
                table: "configuraciones_curso_cliente",
                column: "curso_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuraciones_curso_cliente_tenant_id",
                table: "configuraciones_curso_cliente",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curso_lecciones_curso_modulo_id_orden",
                table: "curso_lecciones",
                columns: new[] { "curso_modulo_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "ix_curso_modulos_curso_id_orden",
                table: "curso_modulos",
                columns: new[] { "curso_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "ix_curso_progresos_curso_id_dependencia_id",
                table: "curso_progresos",
                columns: new[] { "curso_id", "dependencia_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curso_progresos_tenant_id_curso_id",
                table: "curso_progresos",
                columns: new[] { "tenant_id", "curso_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cursos_cuestionario_id",
                table: "cursos",
                column: "cuestionario_id");

            migrationBuilder.CreateIndex(
                name: "ix_cursos_tenant_id_activo",
                table: "cursos",
                columns: new[] { "tenant_id", "activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuraciones_curso_cliente");

            migrationBuilder.DropTable(
                name: "curso_lecciones");

            migrationBuilder.DropTable(
                name: "curso_progresos");

            migrationBuilder.DropTable(
                name: "curso_modulos");

            migrationBuilder.DropTable(
                name: "cursos");
        }
    }
}
