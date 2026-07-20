using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTopografia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "niveles_topograficos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    prefijo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    capacidad_por_defecto = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_niveles_topograficos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "elementos_topograficos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nivel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    codigo_topografico = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    capacidad = table.Column<int>(type: "integer", nullable: false),
                    ocupacion = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "DISPONIBLE"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_elementos_topograficos", x => x.id);
                    table.ForeignKey(
                        name: "fk_elementos_topograficos_elementos_topograficos_padre_id",
                        column: x => x.padre_id,
                        principalTable: "elementos_topograficos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_elementos_topograficos_niveles_topograficos_nivel_id",
                        column: x => x.nivel_id,
                        principalTable: "niveles_topograficos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_elementos_topograficos_nivel_id",
                table: "elementos_topograficos",
                column: "nivel_id");

            migrationBuilder.CreateIndex(
                name: "ix_elementos_topograficos_padre_id",
                table: "elementos_topograficos",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "ix_elementos_topograficos_tenant_id_codigo_topografico",
                table: "elementos_topograficos",
                columns: new[] { "tenant_id", "codigo_topografico" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_elementos_topograficos_tenant_id_padre_id",
                table: "elementos_topograficos",
                columns: new[] { "tenant_id", "padre_id" });

            migrationBuilder.CreateIndex(
                name: "ix_niveles_topograficos_tenant_id_orden",
                table: "niveles_topograficos",
                columns: new[] { "tenant_id", "orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "elementos_topograficos");

            migrationBuilder.DropTable(
                name: "niveles_topograficos");
        }
    }
}
