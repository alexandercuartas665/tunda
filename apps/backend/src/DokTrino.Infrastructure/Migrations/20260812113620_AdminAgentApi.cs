using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminAgentApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tool_keys",
                table: "ai_agents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "whats_app_line_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    whats_app_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_whats_app_line_bindings", x => x.id);
                    table.ForeignKey(
                        name: "fk_whats_app_line_bindings_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_whats_app_line_bindings_whats_app_lines_whats_app_line_id",
                        column: x => x.whats_app_line_id,
                        principalTable: "whats_app_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_whats_app_line_bindings_agent_id",
                table: "whats_app_line_bindings",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_whats_app_line_bindings_tenant_id_whats_app_line_id",
                table: "whats_app_line_bindings",
                columns: new[] { "tenant_id", "whats_app_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_whats_app_line_bindings_whats_app_line_id",
                table: "whats_app_line_bindings",
                column: "whats_app_line_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whats_app_line_bindings");

            migrationBuilder.DropColumn(
                name: "tool_keys",
                table: "ai_agents");
        }
    }
}
