using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CursoProgresoPorColaborador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_curso_progresos_curso_id_dependencia_id",
                table: "curso_progresos");

            migrationBuilder.CreateIndex(
                name: "ix_curso_progresos_curso_id_dependencia_id_colaborador_id",
                table: "curso_progresos",
                columns: new[] { "curso_id", "dependencia_id", "colaborador_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_curso_progresos_curso_id_dependencia_id_colaborador_id",
                table: "curso_progresos");

            migrationBuilder.CreateIndex(
                name: "ix_curso_progresos_curso_id_dependencia_id",
                table: "curso_progresos",
                columns: new[] { "curso_id", "dependencia_id" },
                unique: true);
        }
    }
}
