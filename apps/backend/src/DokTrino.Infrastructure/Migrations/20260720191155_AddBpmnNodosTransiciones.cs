using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmnNodosTransiciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "nodo_id",
                table: "tareas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bpmn_xml",
                table: "procesos_definicion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "publicado",
                table: "procesos_definicion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "proceso_nodos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    elemento_bpmn_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    responsable = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proceso_nodos", x => x.id);
                    table.ForeignKey(
                        name: "fk_proceso_nodos_procesos_definicion_proceso_id",
                        column: x => x.proceso_id,
                        principalTable: "procesos_definicion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proceso_transiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    elemento_bpmn_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    condicion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proceso_transiciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_proceso_transiciones_proceso_nodos_destino_id",
                        column: x => x.destino_id,
                        principalTable: "proceso_nodos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_proceso_transiciones_proceso_nodos_origen_id",
                        column: x => x.origen_id,
                        principalTable: "proceso_nodos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_proceso_transiciones_procesos_definicion_proceso_id",
                        column: x => x.proceso_id,
                        principalTable: "procesos_definicion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tareas_nodo_id",
                table: "tareas",
                column: "nodo_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_nodos_proceso_id_elemento_bpmn_id",
                table: "proceso_nodos",
                columns: new[] { "proceso_id", "elemento_bpmn_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_proceso_transiciones_destino_id",
                table: "proceso_transiciones",
                column: "destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_transiciones_origen_id",
                table: "proceso_transiciones",
                column: "origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_transiciones_proceso_id_elemento_bpmn_id",
                table: "proceso_transiciones",
                columns: new[] { "proceso_id", "elemento_bpmn_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_tareas_proceso_nodos_nodo_id",
                table: "tareas",
                column: "nodo_id",
                principalTable: "proceso_nodos",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tareas_proceso_nodos_nodo_id",
                table: "tareas");

            migrationBuilder.DropTable(
                name: "proceso_transiciones");

            migrationBuilder.DropTable(
                name: "proceso_nodos");

            migrationBuilder.DropIndex(
                name: "ix_tareas_nodo_id",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "nodo_id",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "bpmn_xml",
                table: "procesos_definicion");

            migrationBuilder.DropColumn(
                name: "publicado",
                table: "procesos_definicion");
        }
    }
}
