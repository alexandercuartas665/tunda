using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CerebrosModulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categorias_cerebro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    archivada = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias_cerebro", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cerebros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_por_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    archivado = table.Column<bool>(type: "boolean", nullable: false),
                    archivado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archivado_por_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cerebros", x => x.id);
                    table.ForeignKey(
                        name: "fk_cerebros_categorias_cerebro_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categorias_cerebro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notas_cerebro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cerebro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    titulo_clave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contenido = table.Column<string>(type: "text", nullable: false),
                    actualizado_por_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    archivada = table.Column<bool>(type: "boolean", nullable: false),
                    archivada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archivada_por_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notas_cerebro", x => x.id);
                    table.ForeignKey(
                        name: "fk_notas_cerebro_cerebros_cerebro_id",
                        column: x => x.cerebro_id,
                        principalTable: "cerebros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_categorias_cerebro_tenant_id_nombre",
                table: "categorias_cerebro",
                columns: new[] { "tenant_id", "nombre" },
                unique: true,
                filter: "archivada = false");

            migrationBuilder.CreateIndex(
                name: "ix_cerebros_categoria_id",
                table: "cerebros",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_cerebros_tenant_id_slug",
                table: "cerebros",
                columns: new[] { "tenant_id", "slug" },
                unique: true,
                filter: "archivado = false");

            migrationBuilder.CreateIndex(
                name: "ix_notas_cerebro_cerebro_id_titulo_clave",
                table: "notas_cerebro",
                columns: new[] { "cerebro_id", "titulo_clave" },
                unique: true,
                filter: "archivada = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notas_cerebro");

            migrationBuilder.DropTable(
                name: "cerebros");

            migrationBuilder.DropTable(
                name: "categorias_cerebro");
        }
    }
}
