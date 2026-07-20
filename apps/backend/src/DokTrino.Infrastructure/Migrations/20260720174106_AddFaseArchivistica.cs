using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFaseArchivistica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fase_archivistica",
                table: "archivos_digitales",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "GESTION");

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_fase_archivistica",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "fase_archivistica" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_tenant_id_fase_archivistica",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "fase_archivistica",
                table: "archivos_digitales");
        }
    }
}
