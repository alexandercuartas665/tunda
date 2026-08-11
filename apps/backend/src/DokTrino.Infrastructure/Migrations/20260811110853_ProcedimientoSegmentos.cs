using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProcedimientoSegmentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "proc_conserva",
                table: "respuestas_tabla_documental",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proc_elimina",
                table: "respuestas_tabla_documental",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proc_que_es",
                table: "respuestas_tabla_documental",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "proc_conserva",
                table: "respuestas_tabla_documental");

            migrationBuilder.DropColumn(
                name: "proc_elimina",
                table: "respuestas_tabla_documental");

            migrationBuilder.DropColumn(
                name: "proc_que_es",
                table: "respuestas_tabla_documental");
        }
    }
}
