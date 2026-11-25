using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokTrino.Infrastructure.Migrations
{
    /// <summary>
    /// Restaura el versionado automatico de form_definitions. El trigger se creaba con SQL a
    /// mano en la migracion de origen 20260626134310_AddFormDefinitionSnapshots (Visal); al
    /// compactar las migraciones en InitialCreate, EF regenero la tabla de snapshots pero ese
    /// SQL se perdio, asi que toda base creada desde cero quedaba sin trigger (y sin versiones).
    /// Es idempotente: si la funcion o el trigger ya existen (bases aplicadas con el script
    /// scripts/sql/2026-06-26_AddFormDefinitionSnapshots.sql) se reemplazan sin error.
    /// </summary>
    public partial class TriggerSnapshotFormularios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solo genera snapshot si cambio algun campo "de contenido" (IS DISTINCT FROM):
            // un UPDATE que solo toca updated_at/updated_by no versiona. Tras insertar, rota
            // en el mismo trigger para conservar los 20 mas recientes por formulario.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_snapshot_form_definition() RETURNS trigger AS $$
                BEGIN
                    IF (OLD.schema_json IS DISTINCT FROM NEW.schema_json
                        OR OLD.codigo  IS DISTINCT FROM NEW.codigo
                        OR OLD.nombre  IS DISTINCT FROM NEW.nombre
                        OR OLD.version IS DISTINCT FROM NEW.version
                        OR OLD.tipo    IS DISTINCT FROM NEW.tipo
                        OR OLD.activo  IS DISTINCT FROM NEW.activo
                        OR OLD.prefill_routes_json IS DISTINCT FROM NEW.prefill_routes_json) THEN
                        INSERT INTO form_definition_snapshots
                            (id, tenant_id, form_definition_id,
                             codigo, nombre, version, tipo, schema_json, prefill_routes_json, activo,
                             snapshot_at, snapshot_by, motivo,
                             created_at, created_by, updated_at, updated_by)
                        VALUES
                            (gen_random_uuid(), OLD.tenant_id, OLD.id,
                             OLD.codigo, OLD.nombre, OLD.version, OLD.tipo,
                             OLD.schema_json, OLD.prefill_routes_json, OLD.activo,
                             NOW(), NULL, 'auto-trigger',
                             NOW(), NULL, NOW(), NULL);

                        DELETE FROM form_definition_snapshots
                        WHERE form_definition_id = OLD.id
                          AND id NOT IN (
                            SELECT id FROM form_definition_snapshots
                            WHERE form_definition_id = OLD.id
                            ORDER BY snapshot_at DESC
                            LIMIT 20
                          );
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_form_definition_snapshot ON form_definitions;
                CREATE TRIGGER trg_form_definition_snapshot
                    BEFORE UPDATE ON form_definitions
                    FOR EACH ROW EXECUTE FUNCTION fn_snapshot_form_definition();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_form_definition_snapshot ON form_definitions;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_snapshot_form_definition();");
        }
    }
}
