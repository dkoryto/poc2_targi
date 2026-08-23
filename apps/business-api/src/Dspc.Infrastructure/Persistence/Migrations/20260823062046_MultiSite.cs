using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dspc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_planning_baselines_version",
                table: "planning_baselines");

            migrationBuilder.AlterColumn<string>(
                name: "city",
                table: "sites",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "featured_scenario_key",
                table: "sites",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "sites",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "profile_key",
                table: "sites",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "sequence",
                table: "sites",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "site_id",
                table: "planning_scenarios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "site_id",
                table: "planning_baselines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "site_id",
                table: "material_lots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // ---------------------------------------------------------------------------------------------
            // Attribute pre-existing rows to a plant BEFORE the foreign keys below are validated.
            // AddColumn above stamps existing rows with the all-zero sentinel, which references no site, so
            // "ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY" would fail with 23503 on any populated database.
            // Fresh databases have no rows here, so every statement is a harmless no-op.
            // ---------------------------------------------------------------------------------------------

            // A lot's plant is denormalised from its purchase-order line, so recover it from there first.
            migrationBuilder.Sql("""
                UPDATE material_lots ml
                   SET site_id = po.site_id
                  FROM purchase_order_lines pol
                  JOIN purchase_orders po ON po.id = pol.purchase_order_id
                 WHERE ml.purchase_order_line_id = pol.id
                   AND ml.site_id = '00000000-0000-0000-0000-000000000000';
                """);

            // Anything still unattributed (lots received without a purchase-order line, and the planning tables,
            // which have no natural parent) belongs to the default plant: SITE-01 when present, else the first site.
            foreach (var table in new[] { "material_lots", "planning_baselines", "planning_scenarios" })
            {
                migrationBuilder.Sql($"""
                    UPDATE {table}
                       SET site_id = COALESCE(
                             (SELECT id FROM sites WHERE code = 'SITE-01' LIMIT 1),
                             (SELECT id FROM sites ORDER BY code LIMIT 1))
                     WHERE site_id = '00000000-0000-0000-0000-000000000000'
                       AND EXISTS (SELECT 1 FROM sites);
                    """);
            }

            // Guard: rows can only remain unattributed if the sites table is empty while child tables are not,
            // which this application cannot produce. Fail with an actionable message rather than letting the
            // foreign key below raise an opaque 23503.
            migrationBuilder.Sql("""
                DO $$
                DECLARE orphans bigint;
                BEGIN
                    SELECT (SELECT count(*) FROM material_lots      WHERE site_id = '00000000-0000-0000-0000-000000000000')
                         + (SELECT count(*) FROM planning_baselines WHERE site_id = '00000000-0000-0000-0000-000000000000')
                         + (SELECT count(*) FROM planning_scenarios WHERE site_id = '00000000-0000-0000-0000-000000000000')
                      INTO orphans;
                    IF orphans > 0 THEN
                        RAISE EXCEPTION
                            'MultiSite migration cannot attribute % row(s) to a plant because the sites table is empty. Recreate the demo database (docker compose --profile demo down -v) and start again.', orphans;
                    END IF;
                END $$;
                """);

            // Records the seed version so an upgraded database re-seeds instead of keeping stale single-plant data.
            // Deliberately outside the EF model: the demo reset truncates every mapped table, and this marker must survive.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS seed_metadata (
                    id           integer     PRIMARY KEY,
                    seed_version text        NOT NULL,
                    seeded_at    timestamptz NOT NULL
                );
                """);

            migrationBuilder.CreateIndex(
                name: "ix_planning_scenarios_site_id",
                table: "planning_scenarios",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_planning_baselines_site_id_version",
                table: "planning_baselines",
                columns: new[] { "site_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_lots_site_id",
                table: "material_lots",
                column: "site_id");

            migrationBuilder.AddForeignKey(
                name: "fk_material_lots_sites_site_id",
                table: "material_lots",
                column: "site_id",
                principalTable: "sites",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_planning_baselines_sites_site_id",
                table: "planning_baselines",
                column: "site_id",
                principalTable: "sites",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_planning_scenarios_sites_site_id",
                table: "planning_scenarios",
                column: "site_id",
                principalTable: "sites",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS seed_metadata;");

            migrationBuilder.DropForeignKey(
                name: "fk_material_lots_sites_site_id",
                table: "material_lots");

            migrationBuilder.DropForeignKey(
                name: "fk_planning_baselines_sites_site_id",
                table: "planning_baselines");

            migrationBuilder.DropForeignKey(
                name: "fk_planning_scenarios_sites_site_id",
                table: "planning_scenarios");

            migrationBuilder.DropIndex(
                name: "ix_planning_scenarios_site_id",
                table: "planning_scenarios");

            migrationBuilder.DropIndex(
                name: "ix_planning_baselines_site_id_version",
                table: "planning_baselines");

            migrationBuilder.DropIndex(
                name: "ix_material_lots_site_id",
                table: "material_lots");

            migrationBuilder.DropColumn(
                name: "featured_scenario_key",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "is_default",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "profile_key",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "sequence",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "site_id",
                table: "planning_scenarios");

            migrationBuilder.DropColumn(
                name: "site_id",
                table: "planning_baselines");

            migrationBuilder.DropColumn(
                name: "site_id",
                table: "material_lots");

            migrationBuilder.AlterColumn<string>(
                name: "city",
                table: "sites",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.CreateIndex(
                name: "ix_planning_baselines_version",
                table: "planning_baselines",
                column: "version",
                unique: true);
        }
    }
}
