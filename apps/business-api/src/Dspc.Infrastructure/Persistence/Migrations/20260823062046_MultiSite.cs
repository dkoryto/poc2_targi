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
