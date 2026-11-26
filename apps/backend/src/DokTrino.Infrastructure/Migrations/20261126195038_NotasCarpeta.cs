using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotasCarpeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "carpeta",
                table: "notas_cerebro_plantilla",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carpeta",
                table: "notas_cerebro",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carpeta",
                table: "notas_cerebro_plantilla");

            migrationBuilder.DropColumn(
                name: "carpeta",
                table: "notas_cerebro");
        }
    }
}
