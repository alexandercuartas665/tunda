using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CerebrosPlantillas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bloqueado",
                table: "cerebros",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "plantilla_origen_id",
                table: "cerebros",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_plantilla",
                table: "cerebros",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cerebros_plantilla",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    categoria_nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    categoria_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    bloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    destino_modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destino_ids_json = table.Column<string>(type: "text", nullable: true),
                    version_publicada = table.Column<int>(type: "integer", nullable: false),
                    publicada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modificada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archivada = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cerebros_plantilla", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notas_cerebro_plantilla",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    titulo_clave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contenido = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notas_cerebro_plantilla", x => x.id);
                    table.ForeignKey(
                        name: "fk_notas_cerebro_plantilla_cerebros_plantilla_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "cerebros_plantilla",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cerebros_tenant_id_plantilla_origen_id",
                table: "cerebros",
                columns: new[] { "tenant_id", "plantilla_origen_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notas_cerebro_plantilla_plantilla_id_titulo_clave",
                table: "notas_cerebro_plantilla",
                columns: new[] { "plantilla_id", "titulo_clave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notas_cerebro_plantilla");

            migrationBuilder.DropTable(
                name: "cerebros_plantilla");

            migrationBuilder.DropIndex(
                name: "ix_cerebros_tenant_id_plantilla_origen_id",
                table: "cerebros");

            migrationBuilder.DropColumn(
                name: "bloqueado",
                table: "cerebros");

            migrationBuilder.DropColumn(
                name: "plantilla_origen_id",
                table: "cerebros");

            migrationBuilder.DropColumn(
                name: "version_plantilla",
                table: "cerebros");
        }
    }
}
