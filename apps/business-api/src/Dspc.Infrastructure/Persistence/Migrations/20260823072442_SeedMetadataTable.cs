using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dspc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedMetadataTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        // Creates seed_metadata for databases that already applied MultiSite before that migration
        // was amended to create it. EF never re-runs an applied migration, so such a database would
        // be missing the table permanently and POST /api/v1/demo/reset would fail with 500 on every
        // attempt, including after a restart. The table stays outside the EF model so the demo
        // reset's truncate-all cannot wipe it.
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS seed_metadata (
                id           integer     PRIMARY KEY,
                seed_version text        NOT NULL,
                seeded_at    timestamptz NOT NULL
            );
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        // No-op: MultiSite.Down() owns dropping this table. Dropping it here would break rolling
        // back to a MultiSite-era schema that legitimately owns it.
        }
    }
}
