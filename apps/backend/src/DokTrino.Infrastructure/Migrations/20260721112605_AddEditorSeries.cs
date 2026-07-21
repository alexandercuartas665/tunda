using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "formatos_json",
                table: "tipologias_documentales",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            // Las filas que existian antes de la columna quedan con un arreglo
            // vacio; un objeto o una cadena vacia no deserializan como lista.
            migrationBuilder.Sql(
                "UPDATE tipologias_documentales SET formatos_json = '[]'::jsonb " +
                "WHERE formatos_json IS NULL OR jsonb_typeof(formatos_json) <> 'array';");

            migrationBuilder.AddColumn<string>(
                name: "procedimiento",
                table: "subseries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tiempo_ac",
                table: "subseries",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tiempo_ag",
                table: "subseries",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "procedimiento",
                table: "series",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sin_subseries",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "tiempo_ac",
                table: "series",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tiempo_ag",
                table: "series",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orden",
                table: "catalogo_caracteristicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "catalogo_caracteristicas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "formatos_json",
                table: "tipologias_documentales");

            migrationBuilder.DropColumn(
                name: "procedimiento",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "tiempo_ac",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "tiempo_ag",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "procedimiento",
                table: "series");

            migrationBuilder.DropColumn(
                name: "sin_subseries",
                table: "series");

            migrationBuilder.DropColumn(
                name: "tiempo_ac",
                table: "series");

            migrationBuilder.DropColumn(
                name: "tiempo_ag",
                table: "series");

            migrationBuilder.DropColumn(
                name: "orden",
                table: "catalogo_caracteristicas");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "catalogo_caracteristicas");
        }
    }
}
