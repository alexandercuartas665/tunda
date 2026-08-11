using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevisionDependenciaBloqueo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "revision_cerrada",
                table: "dependencias",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "revision_iniciada_en",
                table: "dependencias",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revision_iniciada_por",
                table: "dependencias",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trazas_revision_dependencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trazas_revision_dependencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_trazas_revision_dependencia_dependencias_dependencia_id",
                        column: x => x.dependencia_id,
                        principalTable: "dependencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trazas_revision_dependencia_dependencia_id",
                table: "trazas_revision_dependencia",
                column: "dependencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_trazas_revision_dependencia_tenant_id_dependencia_id_fecha",
                table: "trazas_revision_dependencia",
                columns: new[] { "tenant_id", "dependencia_id", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trazas_revision_dependencia");

            migrationBuilder.DropColumn(
                name: "revision_cerrada",
                table: "dependencias");

            migrationBuilder.DropColumn(
                name: "revision_iniciada_en",
                table: "dependencias");

            migrationBuilder.DropColumn(
                name: "revision_iniciada_por",
                table: "dependencias");
        }
    }
}
