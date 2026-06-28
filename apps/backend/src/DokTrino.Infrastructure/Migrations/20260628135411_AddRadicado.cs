using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRadicado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "radicados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    asunto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    remitente = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tipologia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fecha_radicacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    legacy_reg = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_radicados", x => x.id);
                    table.ForeignKey(
                        name: "fk_radicados_tipologias_documentales_tipologia_id",
                        column: x => x.tipologia_id,
                        principalTable: "tipologias_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tenant_id_estado_fecha_radicacion",
                table: "radicados",
                columns: new[] { "tenant_id", "estado", "fecha_radicacion" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tenant_id_sucursal_numero",
                table: "radicados",
                columns: new[] { "tenant_id", "sucursal", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tipologia_id",
                table: "radicados",
                column: "tipologia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "radicados");
        }
    }
}
