using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DependenciaPropiedades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_raiz_documental",
                table: "dependencias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gerente_email",
                table: "dependencias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gerente_nombre",
                table: "dependencias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "dependencias",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codigo_raiz_documental",
                table: "dependencias");

            migrationBuilder.DropColumn(
                name: "gerente_email",
                table: "dependencias");

            migrationBuilder.DropColumn(
                name: "gerente_nombre",
                table: "dependencias");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "dependencias");
        }
    }
}
