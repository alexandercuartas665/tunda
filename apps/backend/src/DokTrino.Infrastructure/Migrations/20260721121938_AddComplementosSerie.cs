using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComplementosSerie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cargos_serie",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    puede_subir = table.Column<bool>(type: "boolean", nullable: false),
                    puede_editar = table.Column<bool>(type: "boolean", nullable: false),
                    puede_eliminar = table.Column<bool>(type: "boolean", nullable: false),
                    puede_archivo_central = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cargos_serie", x => x.id);
                    table.ForeignKey(
                        name: "fk_cargos_serie_series_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "directorios_serie",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_directorios_serie", x => x.id);
                    table.ForeignKey(
                        name: "fk_directorios_serie_directorios_serie_padre_id",
                        column: x => x.padre_id,
                        principalTable: "directorios_serie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_directorios_serie_series_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "funcionarios_cargo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo_serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funcionarios_cargo", x => x.id);
                    table.ForeignKey(
                        name: "fk_funcionarios_cargo_cargos_serie_cargo_serie_id",
                        column: x => x.cargo_serie_id,
                        principalTable: "cargos_serie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cargos_serie_serie_id_nombre",
                table: "cargos_serie",
                columns: new[] { "serie_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_directorios_serie_padre_id",
                table: "directorios_serie",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "ix_directorios_serie_serie_id_padre_id_nombre",
                table: "directorios_serie",
                columns: new[] { "serie_id", "padre_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_funcionarios_cargo_cargo_serie_id_nombre",
                table: "funcionarios_cargo",
                columns: new[] { "cargo_serie_id", "nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "directorios_serie");

            migrationBuilder.DropTable(
                name: "funcionarios_cargo");

            migrationBuilder.DropTable(
                name: "cargos_serie");
        }
    }
}
