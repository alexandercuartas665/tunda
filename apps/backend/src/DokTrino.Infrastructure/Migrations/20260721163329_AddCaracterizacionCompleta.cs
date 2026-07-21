using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaracterizacionCompleta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ddhh",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "descripcion_disposicion",
                table: "subseries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion_tiempo",
                table: "subseries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "disp_ct",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "disp_e",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "disp_s",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "rep",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "sig",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1admin",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1contable",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1fiscal",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1legal",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1tecnica",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val2cientifica",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val2cultural",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val2historica",
                table: "subseries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ddhh",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "descripcion_disposicion",
                table: "series",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion_tiempo",
                table: "series",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "disp_ct",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "disp_e",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "disp_s",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "rep",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "sig",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1admin",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1contable",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1fiscal",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1legal",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val1tecnica",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val2cientifica",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val2cultural",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "val2historica",
                table: "series",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ddhh",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "descripcion_disposicion",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "descripcion_tiempo",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "disp_ct",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "disp_e",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "disp_s",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "rep",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "sig",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "val1admin",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "val1contable",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "val1fiscal",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "val1legal",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "val1tecnica",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "val2cientifica",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "val2cultural",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "val2historica",
                table: "subseries");

            migrationBuilder.DropColumn(
                name: "ddhh",
                table: "series");

            migrationBuilder.DropColumn(
                name: "descripcion_disposicion",
                table: "series");

            migrationBuilder.DropColumn(
                name: "descripcion_tiempo",
                table: "series");

            migrationBuilder.DropColumn(
                name: "disp_ct",
                table: "series");

            migrationBuilder.DropColumn(
                name: "disp_e",
                table: "series");

            migrationBuilder.DropColumn(
                name: "disp_s",
                table: "series");

            migrationBuilder.DropColumn(
                name: "rep",
                table: "series");

            migrationBuilder.DropColumn(
                name: "sig",
                table: "series");

            migrationBuilder.DropColumn(
                name: "val1admin",
                table: "series");

            migrationBuilder.DropColumn(
                name: "val1contable",
                table: "series");

            migrationBuilder.DropColumn(
                name: "val1fiscal",
                table: "series");

            migrationBuilder.DropColumn(
                name: "val1legal",
                table: "series");

            migrationBuilder.DropColumn(
                name: "val1tecnica",
                table: "series");

            migrationBuilder.DropColumn(
                name: "val2cientifica",
                table: "series");

            migrationBuilder.DropColumn(
                name: "val2cultural",
                table: "series");

            migrationBuilder.DropColumn(
                name: "val2historica",
                table: "series");
        }
    }
}
