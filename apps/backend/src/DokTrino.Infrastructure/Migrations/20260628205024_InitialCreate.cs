using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_provider_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    api_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_provider_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    estimated_cost_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usage_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bodegas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bodegas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: true),
                    whats_app_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    smtp_host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    smtp_port = table.Column<int>(type: "integer", nullable: false),
                    smtp_user = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    smtp_password_encrypted = table.Column<string>(type: "text", nullable: true),
                    use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    from_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    from_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evolution_master_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    api_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    webhook_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Development"),
                    webhook_public_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    webhook_active_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    webhook_token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evolution_master_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "form_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    codigo_secundario = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    schema_json = table.Column<string>(type: "jsonb", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    prefill_routes_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "google_auth_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    client_secret_encrypted = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_google_auth_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    media_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    media_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    media_mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "paises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paises", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_closed_won = table.Column<bool>(type: "boolean", nullable: false),
                    is_closed_lost = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_stages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_brandings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tagline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    login_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    login_headline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    login_subtext = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_brandings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    google_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    auth_provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    platform_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    es_global = table.Column<bool>(type: "boolean", nullable: false),
                    documento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    username = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    primer_nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    segundo_nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    primer_apellido = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    segundo_apellido = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    celular = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    fijo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    direccion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "power_bi_reportes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embed_url = table.Column<string>(type: "text", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_power_bi_reportes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "procesos_definicion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procesos_definicion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quote_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    html_content = table.Column<string>(type: "text", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    send_as_image = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saas_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    monthly_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    yearly_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saas_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "segmentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_segmentos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sql_console_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    query = table.Column<string>(type: "text", nullable: false),
                    query_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rows_affected = table.Column<int>(type: "integer", nullable: true),
                    rows_returned = table.Column<int>(type: "integer", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sql_console_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sub_categorias_profesional",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sub_categorias_profesional", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sucursales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    telefono = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sucursales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "super_admin_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    action_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_super_admin_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "template_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_api_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key_hash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    api_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_api_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    config_value = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_evolution_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    use_master_server = table.Column<bool>(type: "boolean", nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    instance_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    api_token_encrypted = table.Column<string>(type: "text", nullable: true),
                    webhook_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_evolution_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    tax_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    country = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    slogan = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipologia_archivos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipologia_archivos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipos_profesional",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_profesional", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "whats_app_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    assigned_to_tenant_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_status_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_whats_app_lines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wompi_master_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    public_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    private_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    events_secret_encrypted = table.Column<string>(type: "text", nullable: true),
                    integrity_secret_encrypted = table.Column<string>(type: "text", nullable: true),
                    webhook_endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wompi_master_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wompi_webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    signature_valid = table.Column<bool>(type: "boolean", nullable: false),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: false),
                    processing_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wompi_webhook_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_prompts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    rule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agent_prompts", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_agent_prompts_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agent_resources", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_agent_resources_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    trigger = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    threshold_minutes = table.Column<int>(type: "integer", nullable: false),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    time_window_start = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    time_window_end = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    follow_up_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    template_category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    shift_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ai_agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revisar_al_guardar_parcial = table.Column<bool>(type: "boolean", nullable: false),
                    revisar_al_guardar_definitivo = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    execution_count = table.Column<int>(type: "integer", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_automation_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_automation_rules_ai_agents_ai_agent_id",
                        column: x => x.ai_agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cajas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cajas", x => x.id);
                    table.ForeignKey(
                        name: "fk_cajas_bodegas_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    message_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_by_tenant_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    media_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    media_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    media_mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_definition_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    schema_json = table.Column<string>(type: "jsonb", nullable: false),
                    prefill_routes_json = table.Column<string>(type: "jsonb", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    snapshot_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_by = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_definition_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_definition_snapshots_form_definitions_form_definition_",
                        column: x => x.form_definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relaciones_formulario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    formulario_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    formulario_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_relacion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relaciones_formulario", x => x.id);
                    table.ForeignKey(
                        name: "fk_relaciones_formulario_form_definitions_formulario_destino_id",
                        column: x => x.formulario_destino_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_relaciones_formulario_form_definitions_formulario_origen_id",
                        column: x => x.formulario_origen_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "departamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pais_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<int>(type: "integer", nullable: true),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departamentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_departamentos_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "leads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estimated_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_to_tenant_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    loss_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stage_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    field_values_json = table.Column<string>(type: "jsonb", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archive_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    archive_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    archived_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                    table.ForeignKey(
                        name: "fk_leads_pipeline_stages_stage_id",
                        column: x => x.stage_id,
                        principalTable: "pipeline_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_field_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    field_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    column = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    options = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    allow_multiple = table.Column<bool>(type: "boolean", nullable: false),
                    multi_with_detail = table.Column<bool>(type: "boolean", nullable: false),
                    total_source_keys = table.Column<string>(type: "text", nullable: true),
                    repeat_with_field_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_field_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_pipeline_field_definitions_pipeline_stages_stage_id",
                        column: x => x.stage_id,
                        principalTable: "pipeline_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proceso_actividades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    detalle = table.Column<string>(type: "text", nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proceso_actividades", x => x.id);
                    table.ForeignKey(
                        name: "fk_proceso_actividades_procesos_definicion_proceso_id",
                        column: x => x.proceso_id,
                        principalTable: "procesos_definicion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rol_permisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ver = table.Column<bool>(type: "boolean", nullable: false),
                    crear = table.Column<bool>(type: "boolean", nullable: false),
                    editar = table.Column<bool>(type: "boolean", nullable: false),
                    eliminar = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol_permisos", x => x.id);
                    table.ForeignKey(
                        name: "fk_rol_permisos_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saas_plan_limits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    limit_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    limit_value = table.Column<long>(type: "bigint", nullable: false),
                    limit_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    enforcement_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saas_plan_limits", x => x.id);
                    table.ForeignKey(
                        name: "fk_saas_plan_limits_saas_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "saas_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tablas_retencion_documental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consecutivo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    segmento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_novedad = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tablas_retencion_documental", x => x.id);
                    table.ForeignKey(
                        name: "fk_tablas_retencion_documental_segmentos_segmento_id",
                        column: x => x.segmento_id,
                        principalTable: "segmentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "subseries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subseries", x => x.id);
                    table.ForeignKey(
                        name: "fk_subseries_series_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    billing_frequency = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_period_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    grace_period_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    auto_renew = table.Column<bool>(type: "boolean", nullable: false),
                    wompi_payment_source_id = table.Column<long>(type: "bigint", nullable: true),
                    payment_method_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_subscriptions_saas_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "saas_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_subscriptions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profesionales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    primer_nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    segundo_nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    primer_apellido = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    segundo_apellido = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    nombre_completo = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    tipo_profesional_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registro_medico = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    celular = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    firma_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profesionales", x => x.id);
                    table.ForeignKey(
                        name: "fk_profesionales_tipos_profesional_tipo_profesional_id",
                        column: x => x.tipo_profesional_id,
                        principalTable: "tipos_profesional",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "municipios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    departamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<int>(type: "integer", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipios", x => x.id);
                    table.ForeignKey(
                        name: "fk_municipios_departamentos_departamento_id",
                        column: x => x.departamento_id,
                        principalTable: "departamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "follow_up_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    assigned_to_tenant_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_follow_up_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_follow_up_tasks_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_activities_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_files_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_notes_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dependencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trd_id = table.Column<Guid>(type: "uuid", nullable: false),
                    padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nivel = table.Column<short>(type: "smallint", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    nombre_cargo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dependencias", x => x.id);
                    table.ForeignKey(
                        name: "fk_dependencias_dependencias_padre_id",
                        column: x => x.padre_id,
                        principalTable: "dependencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dependencias_tablas_retencion_documental_trd_id",
                        column: x => x.trd_id,
                        principalTable: "tablas_retencion_documental",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tipologias_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subserie_id = table.Column<Guid>(type: "uuid", nullable: true),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: true),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipologias_documentales", x => x.id);
                    table.ForeignKey(
                        name: "fk_tipologias_documentales_series_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tipologias_documentales_subseries_subserie_id",
                        column: x => x.subserie_id,
                        principalTable: "subseries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    billing_period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    billing_period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_payments_tenant_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "tenant_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profesional_agencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profesional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profesional_agencias", x => x.id);
                    table.ForeignKey(
                        name: "fk_profesional_agencias_profesionales_profesional_id",
                        column: x => x.profesional_id,
                        principalTable: "profesionales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profesional_sub_categorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profesional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sub_categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profesional_sub_categorias", x => x.id);
                    table.ForeignKey(
                        name: "fk_profesional_sub_categorias_profesionales_profesional_id",
                        column: x => x.profesional_id,
                        principalTable: "profesionales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_profesional_sub_categorias_sub_categorias_profesional_sub_c",
                        column: x => x.sub_categoria_id,
                        principalTable: "sub_categorias_profesional",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    tenant_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    lead_visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    invitation_token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    invitation_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    coordina_terapias = table.Column<bool>(type: "boolean", nullable: false),
                    coordina_enfermeria = table.Column<bool>(type: "boolean", nullable: false),
                    coordina_consultas = table.Column<bool>(type: "boolean", nullable: false),
                    coordina_equipos = table.Column<bool>(type: "boolean", nullable: false),
                    profesional_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_users_platform_users_platform_user_id",
                        column: x => x.platform_user_id,
                        principalTable: "platform_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_users_profesionales_profesional_id",
                        column: x => x.profesional_id,
                        principalTable: "profesionales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tenant_users_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tenant_users_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "colaboradores_dependencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    rol = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_colaboradores_dependencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_colaboradores_dependencia_dependencias_dependencia_id",
                        column: x => x.dependencia_id,
                        principalTable: "dependencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tokens_dependencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trd_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    email_colaborador = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consumido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokens_dependencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_tokens_dependencia_dependencias_dependencia_id",
                        column: x => x.dependencia_id,
                        principalTable: "dependencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tokens_dependencia_tablas_retencion_documental_trd_id",
                        column: x => x.trd_id,
                        principalTable: "tablas_retencion_documental",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "carpetas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    caja_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipologia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_apertura = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_cierre = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carpetas", x => x.id);
                    table.ForeignKey(
                        name: "fk_carpetas_cajas_caja_id",
                        column: x => x.caja_id,
                        principalTable: "cajas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_carpetas_tipologias_documentales_tipologia_id",
                        column: x => x.tipologia_id,
                        principalTable: "tipologias_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateTable(
                name: "respuestas_tabla_documental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trd_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subserie_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipologia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sin_subserie = table.Column<bool>(type: "boolean", nullable: false),
                    tiempo_ag = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    tiempo_ac = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    tiempo_observ = table.Column<string>(type: "text", nullable: true),
                    disp_ct = table.Column<bool>(type: "boolean", nullable: false),
                    disp_s = table.Column<bool>(type: "boolean", nullable: false),
                    disp_e = table.Column<bool>(type: "boolean", nullable: false),
                    disp_d = table.Column<bool>(type: "boolean", nullable: false),
                    disp_observ = table.Column<string>(type: "text", nullable: true),
                    val1admin = table.Column<bool>(type: "boolean", nullable: false),
                    val1tecnica = table.Column<bool>(type: "boolean", nullable: false),
                    val1legal = table.Column<bool>(type: "boolean", nullable: false),
                    val1contable = table.Column<bool>(type: "boolean", nullable: false),
                    val1fiscal = table.Column<bool>(type: "boolean", nullable: false),
                    val2historica = table.Column<bool>(type: "boolean", nullable: false),
                    val2cientifica = table.Column<bool>(type: "boolean", nullable: false),
                    val2cultural = table.Column<bool>(type: "boolean", nullable: false),
                    representativo = table.Column<string>(type: "text", nullable: true),
                    serie_ddhh = table.Column<bool>(type: "boolean", nullable: false),
                    relacion_sig = table.Column<string>(type: "text", nullable: true),
                    extension = table.Column<string>(type: "jsonb", nullable: false),
                    fecha_reg = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_respuestas_tabla_documental", x => x.id);
                    table.ForeignKey(
                        name: "fk_respuestas_tabla_documental_dependencias_dependencia_id",
                        column: x => x.dependencia_id,
                        principalTable: "dependencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_respuestas_tabla_documental_series_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_respuestas_tabla_documental_subseries_subserie_id",
                        column: x => x.subserie_id,
                        principalTable: "subseries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_respuestas_tabla_documental_tablas_retencion_documental_trd",
                        column: x => x.trd_id,
                        principalTable: "tablas_retencion_documental",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_respuestas_tabla_documental_tipologias_documentales_tipolog",
                        column: x => x.tipologia_id,
                        principalTable: "tipologias_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tenant_user_sucursales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_user_sucursales", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_user_sucursales_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tenant_user_sucursales_tenant_users_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "formaciones_dependencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    colaborador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    superado = table.Column<bool>(type: "boolean", nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    fecha_superado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_formaciones_dependencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_formaciones_dependencia_colaboradores_dependencia_colaborad",
                        column: x => x.colaborador_id,
                        principalTable: "colaboradores_dependencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "archivos_digitales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    carpeta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipologia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    blob_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    mime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fecha_subida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_archivos_digitales", x => x.id);
                    table.ForeignKey(
                        name: "fk_archivos_digitales_carpetas_carpeta_id",
                        column: x => x.carpeta_id,
                        principalTable: "carpetas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_archivos_digitales_tipologias_documentales_tipologia_id",
                        column: x => x.tipologia_id,
                        principalTable: "tipologias_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "proceso_instancias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    radicado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actividad_actual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proceso_instancias", x => x.id);
                    table.ForeignKey(
                        name: "fk_proceso_instancias_procesos_definicion_proceso_id",
                        column: x => x.proceso_id,
                        principalTable: "procesos_definicion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_proceso_instancias_radicados_radicado_id",
                        column: x => x.radicado_id,
                        principalTable: "radicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "formatos_serie",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    respuesta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    soporte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    formato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_formatos_serie", x => x.id);
                    table.ForeignKey(
                        name: "fk_formatos_serie_respuestas_tabla_documental_respuesta_id",
                        column: x => x.respuesta_id,
                        principalTable: "respuestas_tabla_documental",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tareas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instancia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actividad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actividad_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    asignado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_completada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tareas", x => x.id);
                    table.ForeignKey(
                        name: "fk_tareas_proceso_actividades_actividad_id",
                        column: x => x.actividad_id,
                        principalTable: "proceso_actividades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tareas_proceso_instancias_instancia_id",
                        column: x => x.instancia_id,
                        principalTable: "proceso_instancias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_prompts_agent_id",
                table: "ai_agent_prompts",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_prompts_tenant_id_agent_id_sort_order",
                table: "ai_agent_prompts",
                columns: new[] { "tenant_id", "agent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_resources_agent_id",
                table: "ai_agent_resources",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_resources_tenant_id_agent_id_sort_order",
                table: "ai_agent_resources",
                columns: new[] { "tenant_id", "agent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agents_tenant_id_sort_order",
                table: "ai_agents",
                columns: new[] { "tenant_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_provider_configs_provider",
                table: "ai_provider_configs",
                column: "provider",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_tenant_id_agent_id",
                table: "ai_usage_logs",
                columns: new[] { "tenant_id", "agent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_tenant_id_created_at",
                table: "ai_usage_logs",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_carpeta_id",
                table: "archivos_digitales",
                column: "carpeta_id");

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_carpeta_id",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "carpeta_id" });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tenant_id_fecha_subida",
                table: "archivos_digitales",
                columns: new[] { "tenant_id", "fecha_subida" });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_digitales_tipologia_id",
                table: "archivos_digitales",
                column: "tipologia_id");

            migrationBuilder.CreateIndex(
                name: "ix_automation_rules_ai_agent_id",
                table: "automation_rules",
                column: "ai_agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_automation_rules_tenant_id_sort_order",
                table: "automation_rules",
                columns: new[] { "tenant_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_bodegas_tenant_id_sucursal_codigo",
                table: "bodegas",
                columns: new[] { "tenant_id", "sucursal", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cajas_bodega_id",
                table: "cajas",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "ix_cajas_tenant_id_codigo",
                table: "cajas",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_carpetas_caja_id",
                table: "carpetas",
                column: "caja_id");

            migrationBuilder.CreateIndex(
                name: "ix_carpetas_tenant_id_codigo",
                table: "carpetas",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_carpetas_tipologia_id",
                table: "carpetas",
                column: "tipologia_id");

            migrationBuilder.CreateIndex(
                name: "ix_colaboradores_dependencia_dependencia_id_email",
                table: "colaboradores_dependencia",
                columns: new[] { "dependencia_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversations_tenant_id_contact_phone",
                table: "conversations",
                columns: new[] { "tenant_id", "contact_phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_departamentos_pais_id_external_id",
                table: "departamentos",
                columns: new[] { "pais_id", "external_id" });

            migrationBuilder.CreateIndex(
                name: "ix_departamentos_pais_id_nombre",
                table: "departamentos",
                columns: new[] { "pais_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dependencias_padre_id",
                table: "dependencias",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "ix_dependencias_trd_id_padre_id_orden",
                table: "dependencias",
                columns: new[] { "trd_id", "padre_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_follow_up_tasks_lead_id",
                table: "follow_up_tasks",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_follow_up_tasks_tenant_id_due_at",
                table: "follow_up_tasks",
                columns: new[] { "tenant_id", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_follow_up_tasks_tenant_id_status",
                table: "follow_up_tasks",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_form_definition_snapshots_form_definition_id_snapshot_at",
                table: "form_definition_snapshots",
                columns: new[] { "form_definition_id", "snapshot_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_form_definitions_tenant_id_codigo",
                table: "form_definitions",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_form_definitions_tenant_id_codigo_secundario",
                table: "form_definitions",
                columns: new[] { "tenant_id", "codigo_secundario" });

            migrationBuilder.CreateIndex(
                name: "ix_formaciones_dependencia_colaborador_id",
                table: "formaciones_dependencia",
                column: "colaborador_id");

            migrationBuilder.CreateIndex(
                name: "ix_formaciones_dependencia_tenant_id_colaborador_id",
                table: "formaciones_dependencia",
                columns: new[] { "tenant_id", "colaborador_id" });

            migrationBuilder.CreateIndex(
                name: "ix_formatos_serie_respuesta_id_soporte_formato",
                table: "formatos_serie",
                columns: new[] { "respuesta_id", "soporte", "formato" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lead_activities_lead_id",
                table: "lead_activities",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_activities_tenant_id_lead_id",
                table: "lead_activities",
                columns: new[] { "tenant_id", "lead_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lead_files_lead_id",
                table: "lead_files",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_files_tenant_id_lead_id",
                table: "lead_files",
                columns: new[] { "tenant_id", "lead_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lead_notes_lead_id",
                table: "lead_notes",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_notes_tenant_id_lead_id",
                table: "lead_notes",
                columns: new[] { "tenant_id", "lead_id" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_assigned_to_tenant_user_id",
                table: "leads",
                column: "assigned_to_tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_stage_id",
                table: "leads",
                column: "stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_tenant_id_archived_at",
                table: "leads",
                columns: new[] { "tenant_id", "archived_at" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_tenant_id_stage_id",
                table: "leads",
                columns: new[] { "tenant_id", "stage_id" });

            migrationBuilder.CreateIndex(
                name: "ix_message_templates_tenant_id_category_sort_order",
                table: "message_templates",
                columns: new[] { "tenant_id", "category", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_id",
                table: "messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_tenant_id_conversation_id",
                table: "messages",
                columns: new[] { "tenant_id", "conversation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_tenant_id_external_id",
                table: "messages",
                columns: new[] { "tenant_id", "external_id" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_municipios_departamento_id_nombre",
                table: "municipios",
                columns: new[] { "departamento_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_paises_codigo",
                table: "paises",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_platform_user_id",
                table: "password_reset_tokens",
                column: "platform_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_field_definitions_stage_id_field_key",
                table: "pipeline_field_definitions",
                columns: new[] { "stage_id", "field_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_field_definitions_tenant_id_stage_id_sort_order",
                table: "pipeline_field_definitions",
                columns: new[] { "tenant_id", "stage_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stages_tenant_id_name",
                table: "pipeline_stages",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stages_tenant_id_sort_order",
                table: "pipeline_stages",
                columns: new[] { "tenant_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_platform_users_documento",
                table: "platform_users",
                column: "documento",
                unique: true,
                filter: "documento IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_platform_users_email",
                table: "platform_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_users_google_subject",
                table: "platform_users",
                column: "google_subject",
                unique: true,
                filter: "google_subject IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_platform_users_username",
                table: "platform_users",
                column: "username",
                unique: true,
                filter: "username IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_power_bi_reportes_tenant_id_orden",
                table: "power_bi_reportes",
                columns: new[] { "tenant_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "ix_proceso_actividades_proceso_id",
                table: "proceso_actividades",
                column: "proceso_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_actividades_tenant_id_proceso_id_orden",
                table: "proceso_actividades",
                columns: new[] { "tenant_id", "proceso_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "ix_proceso_instancias_proceso_id",
                table: "proceso_instancias",
                column: "proceso_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_instancias_radicado_id",
                table: "proceso_instancias",
                column: "radicado_id");

            migrationBuilder.CreateIndex(
                name: "ix_proceso_instancias_tenant_id_estado",
                table: "proceso_instancias",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_procesos_definicion_tenant_id_sucursal_codigo_version",
                table: "procesos_definicion",
                columns: new[] { "tenant_id", "sucursal", "codigo", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profesional_agencias_profesional_id",
                table: "profesional_agencias",
                column: "profesional_id");

            migrationBuilder.CreateIndex(
                name: "ix_profesional_agencias_tenant_id_profesional_id",
                table: "profesional_agencias",
                columns: new[] { "tenant_id", "profesional_id" });

            migrationBuilder.CreateIndex(
                name: "ix_profesional_sub_categorias_profesional_id",
                table: "profesional_sub_categorias",
                column: "profesional_id");

            migrationBuilder.CreateIndex(
                name: "ix_profesional_sub_categorias_sub_categoria_id",
                table: "profesional_sub_categorias",
                column: "sub_categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_profesional_sub_categorias_tenant_id_profesional_id",
                table: "profesional_sub_categorias",
                columns: new[] { "tenant_id", "profesional_id" });

            migrationBuilder.CreateIndex(
                name: "ix_profesionales_tenant_id_numero_documento",
                table: "profesionales",
                columns: new[] { "tenant_id", "numero_documento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profesionales_tipo_profesional_id",
                table: "profesionales",
                column: "tipo_profesional_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_templates_tenant_id_is_default",
                table: "quote_templates",
                columns: new[] { "tenant_id", "is_default" });

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

            migrationBuilder.CreateIndex(
                name: "ix_relaciones_formulario_formulario_destino_id",
                table: "relaciones_formulario",
                column: "formulario_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_relaciones_formulario_formulario_origen_id",
                table: "relaciones_formulario",
                column: "formulario_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_relaciones_formulario_tenant_id_formulario_origen_id_formul",
                table: "relaciones_formulario",
                columns: new[] { "tenant_id", "formulario_origen_id", "formulario_destino_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_tabla_documental_dependencia_id",
                table: "respuestas_tabla_documental",
                column: "dependencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_tabla_documental_serie_id",
                table: "respuestas_tabla_documental",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_tabla_documental_subserie_id",
                table: "respuestas_tabla_documental",
                column: "subserie_id");

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_tabla_documental_tenant_id_trd_id_dependencia_id",
                table: "respuestas_tabla_documental",
                columns: new[] { "tenant_id", "trd_id", "dependencia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_tabla_documental_tipologia_id",
                table: "respuestas_tabla_documental",
                column: "tipologia_id");

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_tabla_documental_trd_id",
                table: "respuestas_tabla_documental",
                column: "trd_id");

            migrationBuilder.CreateIndex(
                name: "ix_rol_permisos_rol_id_modulo",
                table: "rol_permisos",
                columns: new[] { "rol_id", "modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_nombre",
                table: "roles",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saas_plan_limits_plan_id_limit_key",
                table: "saas_plan_limits",
                columns: new[] { "plan_id", "limit_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_segmentos_tenant_id_codigo",
                table: "segmentos",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_series_tenant_id_codigo",
                table: "series",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sql_console_logs_executed_at",
                table: "sql_console_logs",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "ix_sql_console_logs_tenant_id_executed_at",
                table: "sql_console_logs",
                columns: new[] { "tenant_id", "executed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sub_categorias_profesional_tenant_id_nombre",
                table: "sub_categorias_profesional",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subseries_serie_id_codigo",
                table: "subseries",
                columns: new[] { "serie_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sucursales_tenant_id_codigo",
                table: "sucursales",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_super_admin_audit_logs_created_at",
                table: "super_admin_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_super_admin_audit_logs_tenant_id",
                table: "super_admin_audit_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tablas_retencion_documental_segmento_id",
                table: "tablas_retencion_documental",
                column: "segmento_id");

            migrationBuilder.CreateIndex(
                name: "ix_tablas_retencion_documental_tenant_id_consecutivo",
                table: "tablas_retencion_documental",
                columns: new[] { "tenant_id", "consecutivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tareas_actividad_id",
                table: "tareas",
                column: "actividad_id");

            migrationBuilder.CreateIndex(
                name: "ix_tareas_instancia_id",
                table: "tareas",
                column: "instancia_id");

            migrationBuilder.CreateIndex(
                name: "ix_tareas_tenant_id_asignado_id_estado",
                table: "tareas",
                columns: new[] { "tenant_id", "asignado_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_tareas_tenant_id_instancia_id",
                table: "tareas",
                columns: new[] { "tenant_id", "instancia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_template_assets_tenant_id_created_at",
                table: "template_assets",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_api_configs_api_key_hash",
                table: "tenant_api_configs",
                column: "api_key_hash");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_api_configs_tenant_id",
                table: "tenant_api_configs",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_configurations_tenant_id_config_key",
                table: "tenant_configurations",
                columns: new[] { "tenant_id", "config_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_evolution_configs_tenant_id",
                table: "tenant_evolution_configs",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_payments_subscription_id",
                table: "tenant_payments",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_payments_tenant_id",
                table: "tenant_payments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_subscriptions_plan_id",
                table: "tenant_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_subscriptions_tenant_id",
                table: "tenant_subscriptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_user_sucursales_sucursal_id",
                table: "tenant_user_sucursales",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_user_sucursales_tenant_user_id_sucursal_id",
                table: "tenant_user_sucursales",
                columns: new[] { "tenant_user_id", "sucursal_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_invitation_token",
                table: "tenant_users",
                column: "invitation_token");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_platform_user_id",
                table: "tenant_users",
                column: "platform_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_profesional_id",
                table: "tenant_users",
                column: "profesional_id",
                unique: true,
                filter: "profesional_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_rol_id",
                table: "tenant_users",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_sucursal_id",
                table: "tenant_users",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_tenant_id_email",
                table: "tenant_users",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_tenant_id_platform_user_id",
                table: "tenant_users",
                columns: new[] { "tenant_id", "platform_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipologia_archivos_tenant_id_activo",
                table: "tipologia_archivos",
                columns: new[] { "tenant_id", "activo" });

            migrationBuilder.CreateIndex(
                name: "ix_tipologia_archivos_tenant_id_nombre",
                table: "tipologia_archivos",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipologias_documentales_serie_id",
                table: "tipologias_documentales",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipologias_documentales_subserie_id",
                table: "tipologias_documentales",
                column: "subserie_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipologias_documentales_tenant_id_codigo",
                table: "tipologias_documentales",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipos_profesional_tenant_id_nombre",
                table: "tipos_profesional",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_dependencia_dependencia_id",
                table: "tokens_dependencia",
                column: "dependencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_dependencia_tenant_id_trd_id",
                table: "tokens_dependencia",
                columns: new[] { "tenant_id", "trd_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tokens_dependencia_token",
                table: "tokens_dependencia",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_dependencia_trd_id",
                table: "tokens_dependencia",
                column: "trd_id");

            migrationBuilder.CreateIndex(
                name: "ix_whats_app_lines_assigned_to_tenant_user_id",
                table: "whats_app_lines",
                column: "assigned_to_tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_whats_app_lines_tenant_id_instance_name",
                table: "whats_app_lines",
                columns: new[] { "tenant_id", "instance_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wompi_webhook_events_provider_event_id",
                table: "wompi_webhook_events",
                column: "provider_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_prompts");

            migrationBuilder.DropTable(
                name: "ai_agent_resources");

            migrationBuilder.DropTable(
                name: "ai_provider_configs");

            migrationBuilder.DropTable(
                name: "ai_usage_logs");

            migrationBuilder.DropTable(
                name: "archivos_digitales");

            migrationBuilder.DropTable(
                name: "automation_rules");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "email_configs");

            migrationBuilder.DropTable(
                name: "evolution_master_configs");

            migrationBuilder.DropTable(
                name: "follow_up_tasks");

            migrationBuilder.DropTable(
                name: "form_definition_snapshots");

            migrationBuilder.DropTable(
                name: "formaciones_dependencia");

            migrationBuilder.DropTable(
                name: "formatos_serie");

            migrationBuilder.DropTable(
                name: "google_auth_configs");

            migrationBuilder.DropTable(
                name: "lead_activities");

            migrationBuilder.DropTable(
                name: "lead_files");

            migrationBuilder.DropTable(
                name: "lead_notes");

            migrationBuilder.DropTable(
                name: "message_templates");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "municipios");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "pipeline_field_definitions");

            migrationBuilder.DropTable(
                name: "platform_brandings");

            migrationBuilder.DropTable(
                name: "power_bi_reportes");

            migrationBuilder.DropTable(
                name: "profesional_agencias");

            migrationBuilder.DropTable(
                name: "profesional_sub_categorias");

            migrationBuilder.DropTable(
                name: "quote_templates");

            migrationBuilder.DropTable(
                name: "relaciones_formulario");

            migrationBuilder.DropTable(
                name: "rol_permisos");

            migrationBuilder.DropTable(
                name: "saas_plan_limits");

            migrationBuilder.DropTable(
                name: "sql_console_logs");

            migrationBuilder.DropTable(
                name: "super_admin_audit_logs");

            migrationBuilder.DropTable(
                name: "tareas");

            migrationBuilder.DropTable(
                name: "template_assets");

            migrationBuilder.DropTable(
                name: "tenant_api_configs");

            migrationBuilder.DropTable(
                name: "tenant_configurations");

            migrationBuilder.DropTable(
                name: "tenant_evolution_configs");

            migrationBuilder.DropTable(
                name: "tenant_payments");

            migrationBuilder.DropTable(
                name: "tenant_user_sucursales");

            migrationBuilder.DropTable(
                name: "tipologia_archivos");

            migrationBuilder.DropTable(
                name: "tokens_dependencia");

            migrationBuilder.DropTable(
                name: "whats_app_lines");

            migrationBuilder.DropTable(
                name: "wompi_master_configs");

            migrationBuilder.DropTable(
                name: "wompi_webhook_events");

            migrationBuilder.DropTable(
                name: "carpetas");

            migrationBuilder.DropTable(
                name: "ai_agents");

            migrationBuilder.DropTable(
                name: "colaboradores_dependencia");

            migrationBuilder.DropTable(
                name: "respuestas_tabla_documental");

            migrationBuilder.DropTable(
                name: "leads");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "departamentos");

            migrationBuilder.DropTable(
                name: "sub_categorias_profesional");

            migrationBuilder.DropTable(
                name: "form_definitions");

            migrationBuilder.DropTable(
                name: "proceso_actividades");

            migrationBuilder.DropTable(
                name: "proceso_instancias");

            migrationBuilder.DropTable(
                name: "tenant_subscriptions");

            migrationBuilder.DropTable(
                name: "tenant_users");

            migrationBuilder.DropTable(
                name: "cajas");

            migrationBuilder.DropTable(
                name: "dependencias");

            migrationBuilder.DropTable(
                name: "pipeline_stages");

            migrationBuilder.DropTable(
                name: "paises");

            migrationBuilder.DropTable(
                name: "procesos_definicion");

            migrationBuilder.DropTable(
                name: "radicados");

            migrationBuilder.DropTable(
                name: "saas_plans");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "platform_users");

            migrationBuilder.DropTable(
                name: "profesionales");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "sucursales");

            migrationBuilder.DropTable(
                name: "bodegas");

            migrationBuilder.DropTable(
                name: "tablas_retencion_documental");

            migrationBuilder.DropTable(
                name: "tipologias_documentales");

            migrationBuilder.DropTable(
                name: "tipos_profesional");

            migrationBuilder.DropTable(
                name: "segmentos");

            migrationBuilder.DropTable(
                name: "subseries");

            migrationBuilder.DropTable(
                name: "series");
        }
    }
}
