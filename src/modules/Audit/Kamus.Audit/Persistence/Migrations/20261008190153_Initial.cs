using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Audit.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "entries",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    module = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    actor_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entries_action",
                schema: "audit",
                table: "entries",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "ix_entries_actor_id_occurred_at",
                schema: "audit",
                table: "entries",
                columns: new[] { "actor_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_entries_module_occurred_at",
                schema: "audit",
                table: "entries",
                columns: new[] { "module", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_entries_occurred_at_id",
                schema: "audit",
                table: "entries",
                columns: new[] { "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_entries_subject_type_subject_id_occurred_at",
                schema: "audit",
                table: "entries",
                columns: new[] { "subject_type", "subject_id", "occurred_at" });

            // Somente inclusão: o próprio banco recusa UPDATE e TRUNCATE, e só deixa apagar registros
            // com mais de 365 dias (piso de segurança da retenção; o worker usa a retenção configurada).
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.prevent_changes() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.occurred_at < now() - interval '365 days' THEN
                            RETURN OLD;
                        END IF;
                        RAISE EXCEPTION 'A auditoria é somente inclusão: só registros com mais de 365 dias podem ser apagados.'
                            USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    RAISE EXCEPTION 'A auditoria é somente inclusão: % não é permitido em audit.entries.', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;

                CREATE TRIGGER entries_prevent_update_delete
                    BEFORE UPDATE OR DELETE ON audit.entries
                    FOR EACH ROW EXECUTE FUNCTION audit.prevent_changes();

                CREATE TRIGGER entries_prevent_truncate
                    BEFORE TRUNCATE ON audit.entries
                    FOR EACH STATEMENT EXECUTE FUNCTION audit.prevent_changes();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS entries_prevent_truncate ON audit.entries;
                DROP TRIGGER IF EXISTS entries_prevent_update_delete ON audit.entries;
                DROP FUNCTION IF EXISTS audit.prevent_changes();
                """);

            migrationBuilder.DropTable(
                name: "entries",
                schema: "audit");
        }
    }
}
