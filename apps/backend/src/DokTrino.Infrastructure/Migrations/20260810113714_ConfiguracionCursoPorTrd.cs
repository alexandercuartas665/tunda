using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracionCursoPorTrd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_configuraciones_curso_cliente_tenant_id",
                table: "configuraciones_curso_cliente");

            // La columna nace nullable para poder preservar la config existente.
            migrationBuilder.AddColumn<Guid>(
                name: "trd_id",
                table: "configuraciones_curso_cliente",
                type: "uuid",
                nullable: true);

            // La config antes era una por tenant; ahora es una por encuesta. Se
            // replica el curso configurado a cada TRD del tenant y se retiran las
            // filas globales originales (sin trd). Una config de un tenant sin
            // ninguna TRD simplemente desaparece: no hay encuesta que compuertar.
            migrationBuilder.Sql(@"
                INSERT INTO configuraciones_curso_cliente
                    (id, tenant_id, trd_id, curso_id, obligatorio, intentos_max, created_at)
                SELECT gen_random_uuid(), c.tenant_id, t.id, c.curso_id, c.obligatorio, c.intentos_max, now()
                FROM configuraciones_curso_cliente c
                JOIN tablas_retencion_documental t ON t.tenant_id = c.tenant_id
                WHERE c.trd_id IS NULL;

                DELETE FROM configuraciones_curso_cliente WHERE trd_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "trd_id",
                table: "configuraciones_curso_cliente",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuraciones_curso_cliente_tenant_id_trd_id",
                table: "configuraciones_curso_cliente",
                columns: new[] { "tenant_id", "trd_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuraciones_curso_cliente_trd_id",
                table: "configuraciones_curso_cliente",
                column: "trd_id");

            migrationBuilder.AddForeignKey(
                name: "fk_configuraciones_curso_cliente_tablas_retencion_documental_t",
                table: "configuraciones_curso_cliente",
                column: "trd_id",
                principalTable: "tablas_retencion_documental",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_configuraciones_curso_cliente_tablas_retencion_documental_t",
                table: "configuraciones_curso_cliente");

            migrationBuilder.DropIndex(
                name: "ix_configuraciones_curso_cliente_tenant_id_trd_id",
                table: "configuraciones_curso_cliente");

            migrationBuilder.DropIndex(
                name: "ix_configuraciones_curso_cliente_trd_id",
                table: "configuraciones_curso_cliente");

            migrationBuilder.DropColumn(
                name: "trd_id",
                table: "configuraciones_curso_cliente");

            migrationBuilder.CreateIndex(
                name: "ix_configuraciones_curso_cliente_tenant_id",
                table: "configuraciones_curso_cliente",
                column: "tenant_id",
                unique: true);
        }
    }
}
