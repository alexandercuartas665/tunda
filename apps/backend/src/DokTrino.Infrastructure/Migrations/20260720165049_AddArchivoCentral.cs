using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivoCentral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "estado",
                table: "archivos_digitales");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "aprobado_en",
                table: "archivos_digitales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "aprobado_por",
                table: "archivos_digitales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "carpeta_archivo_id",
                table: "archivos_digitales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "concepto",
                table: "archivos_digitales",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_aprobacion",
                table: "archivos_digitales",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "flag_identificado",
                table: "archivos_digitales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "identificador_principal",
                table: "archivos_digitales",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rechazo_motivo",
                table: "archivos_digitales",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aprobaciones_documento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comentario = table.Column<string>(type: "text", nullable: true),
                    decidido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aprobaciones_documento", x => x.id);
                    table.ForeignKey(
                        name: "fk_aprobaciones_documento_archivos_digitales_archivo_id",
                        column: x => x.archivo_id,
                        principalTable: "archivos_digitales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "carpetas_archivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_carpetas_archivo", x => x.id);
                    table.ForeignKey(
                        name: "fk_carpetas_archivo_carpetas_archivo_padre_id",
                        column: x => x.padre_id,
                        principalTable: "carpetas_archivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    color_hex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    privado = table.Column<bool>(type: "boolean", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "archivo_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archivo_tags", x => x.id);
                    table.ForeignKey(
                        name: "fk_archivo_tags_archivos_digitales_archivo_id",
                        column: x => x.archivo_id,
                        principalTable: "archivos_digitales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_archivo_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_carpeta_archivo_id",
                table: "archivos_digitales",
                column: "carpeta_archivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_estado_aprobacion",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "estado_aprobacion" });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_flag_identificado",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "flag_identificado" });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_identificador_principal",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "identificador_principal" });

            migrationBuilder.CreateIndex(
                name: "ix_aprobaciones_documento_archivo_id",
                table: "aprobaciones_documento",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_aprobaciones_documento_tenant_id_archivo_id_decidido_en",
                table: "aprobaciones_documento",
                columns: new[] { "tenant_id", "archivo_id", "decidido_en" });

            migrationBuilder.CreateIndex(
                name: "ix_archivo_tags_archivo_id_tag_id",
                table: "archivo_tags",
                columns: new[] { "archivo_id", "tag_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_archivo_tags_tag_id",
                table: "archivo_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_carpetas_archivo_padre_id",
                table: "carpetas_archivo",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "ix_carpetas_archivo_tenant_id_padre_id_nombre",
                table: "carpetas_archivo",
                columns: new[] { "tenant_id", "padre_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_tenant_id_codigo",
                table: "tags",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_archivos_digitales_carpetas_archivo_carpeta_archivo_id",
                table: "archivos_digitales",
                column: "carpeta_archivo_id",
                principalTable: "carpetas_archivo",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_archivos_digitales_carpetas_archivo_carpeta_archivo_id",
                table: "archivos_digitales");

            migrationBuilder.DropTable(
                name: "aprobaciones_documento");

            migrationBuilder.DropTable(
                name: "archivo_tags");

            migrationBuilder.DropTable(
                name: "carpetas_archivo");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_carpeta_archivo_id",
                table: "archivos_digitales");

            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_tenant_id_estado_aprobacion",
                table: "archivos_digitales");

            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_tenant_id_flag_identificado",
                table: "archivos_digitales");

            migrationBuilder.DropIndex(
                name: "ix_archivos_digitales_tenant_id_identificador_principal",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "aprobado_en",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "aprobado_por",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "carpeta_archivo_id",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "concepto",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "estado_aprobacion",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "flag_identificado",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "identificador_principal",
                table: "archivos_digitales");

            migrationBuilder.DropColumn(
                name: "rechazo_motivo",
                table: "archivos_digitales");

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "archivos_digitales",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }
    }
}
