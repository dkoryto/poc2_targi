using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dspc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    response_body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    event_type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "passport_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_passport_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "planning_baselines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    horizon_start = table.Column<DateOnly>(type: "date", nullable: false),
                    horizon_end = table.Column<DateOnly>(type: "date", nullable: false),
                    approved_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_scenario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kpi_json = table.Column<string>(type: "jsonb", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planning_baselines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_pl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    serial_prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    family = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    otif_percent = table.Column<double>(type: "double precision", nullable: false),
                    quality_score = table.Column<double>(type: "double precision", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "traceability_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    from_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    from_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    to_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    to_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_traceability_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sites", x => x.id);
                    table.ForeignKey(
                        name: "fk_sites_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quality_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    passport_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title_pl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    mapping_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_requirements", x => x.id);
                    table.ForeignKey(
                        name: "fk_quality_requirements_passport_templates_passport_template_id",
                        column: x => x.passport_template_id,
                        principalTable: "passport_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planning_scenarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    preset_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_json = table.Column<string>(type: "jsonb", nullable: true),
                    response_json = table.Column<string>(type: "jsonb", nullable: true),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    kpi_before_json = table.Column<string>(type: "jsonb", nullable: true),
                    kpi_after_json = table.Column<string>(type: "jsonb", nullable: true),
                    explanations_json = table.Column<string>(type: "jsonb", nullable: true),
                    solver = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    elapsed_ms = table.Column<int>(type: "integer", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planning_scenarios", x => x.id);
                    table.CheckConstraint("ck_planningscenario_status", "status IN ('Draft', 'Running', 'Completed', 'Failed', 'Approved', 'Rejected', 'Saved')");
                    table.ForeignKey(
                        name: "fk_planning_scenarios_planning_baselines_baseline_id",
                        column: x => x.baseline_id,
                        principalTable: "planning_baselines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bom_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bom_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_bom_versions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_pl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    criticality = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    has_alternative_supplier = table.Column<bool>(type: "boolean", nullable: false),
                    primary_supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    alternative_supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    required_document_types_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parts", x => x.id);
                    table.CheckConstraint("ck_partdefinition_category", "category IN ('Mechanika', 'Elektronika', 'Materialy', 'Optyka', 'Zasilanie')");
                    table.CheckConstraint("ck_parts_criticality", "criticality BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_parts_suppliers_alternative_supplier_id",
                        column: x => x.alternative_supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_parts_suppliers_primary_supplier_id",
                        column: x => x.primary_supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "supplier_performances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    delivered_lines = table.Column<int>(type: "integer", nullable: false),
                    on_time_in_full_lines = table.Column<int>(type: "integer", nullable: false),
                    quality_rejections = table.Column<int>(type: "integer", nullable: false),
                    otif_percent = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_performances", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_performances_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assembly_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assembly_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_assembly_lines_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ordered_on = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_orders", x => x.id);
                    table.CheckConstraint("ck_purchaseorder_status", "status IN ('Open', 'PartiallyDelivered', 'Delivered', 'Closed', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_purchase_orders_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_purchase_orders_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_user_role", "role IN ('SupplierUser', 'InboundCoordinator', 'ProductionPlanner', 'QualityInspector', 'OperationsDirector', 'Auditor', 'Administrator', 'DemoPresenter')");
                    table.ForeignKey(
                        name: "fk_users_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_users_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "planning_recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    planning_scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    order_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    params_json = table.Column<string>(type: "jsonb", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planning_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "fk_planning_recommendations_planning_scenarios_planning_scenar~",
                        column: x => x.planning_scenario_id,
                        principalTable: "planning_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    planning_scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_scenario_changes_planning_scenarios_planning_scenario_id",
                        column: x => x.planning_scenario_id,
                        principalTable: "planning_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bom_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bom_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_per_unit = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    consumed_at_operation = table.Column<int>(type: "integer", nullable: false),
                    is_key_component = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bom_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_bom_items_bom_versions_bom_version_id",
                        column: x => x.bom_version_id,
                        principalTable: "bom_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bom_items_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    blocked = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    reserved = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_balances", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_balances_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_balances_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "production_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bom_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assembly_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    frozen = table.Column<bool>(type: "boolean", nullable: false),
                    customer_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_orders", x => x.id);
                    table.CheckConstraint("ck_production_orders_priority", "priority BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_productionorder_status", "status IN ('Planned', 'Released', 'InProgress', 'Completed', 'OnHold', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_production_orders_assembly_lines_assembly_line_id",
                        column: x => x.assembly_line_id,
                        principalTable: "assembly_lines",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_production_orders_bom_versions_bom_version_id",
                        column: x => x.bom_version_id,
                        principalTable: "bom_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_production_orders_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_production_orders_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_centers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_pl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assembly_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hours_per_day = table.Column<double>(type: "double precision", nullable: false),
                    shift_start_hour = table.Column<int>(type: "integer", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_centers", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_centers_assembly_lines_assembly_line_id",
                        column: x => x.assembly_line_id,
                        principalTable: "assembly_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_work_centers_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    carrier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    vehicle = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    planned_departure = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actual_departure = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eta = table.Column<DateOnly>(type: "date", nullable: false),
                    arrived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    progress = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipments", x => x.id);
                    table.CheckConstraint("ck_shipment_status", "status IN ('Advised', 'Departed', 'InTransit', 'AtBorder', 'Arrived', 'Received', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_shipments_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipments_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    message_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    params_json = table.Column<string>(type: "jsonb", nullable: false),
                    route = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "product_serials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bom_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_serials", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_serials_bom_versions_bom_version_id",
                        column: x => x.bom_version_id,
                        principalTable: "bom_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_serials_production_orders_production_order_id",
                        column: x => x.production_order_id,
                        principalTable: "production_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_serials_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "capacity_calendars",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    available_hours = table.Column<double>(type: "double precision", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capacity_calendars", x => x.id);
                    table.ForeignKey(
                        name: "fk_capacity_calendars_work_centers_work_center_id",
                        column: x => x.work_center_id,
                        principalTable: "work_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operation_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    name_pl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    work_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    duration_hours = table.Column<double>(type: "double precision", nullable: false),
                    frozen = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    material_requirements_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operation_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_operation_definitions_production_orders_production_order_id",
                        column: x => x.production_order_id,
                        principalTable: "production_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_operation_definitions_work_centers_work_center_id",
                        column: x => x.work_center_id,
                        principalTable: "work_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "logistics_risk_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    region = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_logistics_risk_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_logistics_risk_events_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_logistics_risk_events_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    delivered_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    required_date = table.Column<DateOnly>(type: "date", nullable: false),
                    eta = table.Column<DateOnly>(type: "date", nullable: false),
                    original_eta = table.Column<DateOnly>(type: "date", nullable: false),
                    progress_percent = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    lot_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    heat_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    produced_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    supplier_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    delivered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    risk_score = table.Column<int>(type: "integer", nullable: false),
                    risk_category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    last_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_order_lines", x => x.id);
                    table.CheckConstraint("ck_purchaseorderline_status", "status IN ('Confirmed', 'InProduction', 'QualityControl', 'ReadyToShip', 'Shipped', 'Delivered', 'OnHold')");
                    table.ForeignKey(
                        name: "fk_purchase_order_lines_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_purchase_order_lines_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_purchase_order_lines_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shipment_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    recorded_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipment_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipment_events_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "passports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_serial_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passport_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    approved_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invalidation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    invalidated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    deviations_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_passports", x => x.id);
                    table.CheckConstraint("ck_passport_status", "status IN ('Draft', 'PendingReview', 'Approved', 'Generated', 'Invalidated')");
                    table.ForeignKey(
                        name: "fk_passports_passport_templates_passport_template_id",
                        column: x => x.passport_template_id,
                        principalTable: "passport_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_passports_product_serials_product_serial_id",
                        column: x => x.product_serial_id,
                        principalTable: "product_serials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    planning_baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assembly_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    frozen = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduled_operations", x => x.id);
                    table.ForeignKey(
                        name: "fk_scheduled_operations_assembly_lines_assembly_line_id",
                        column: x => x.assembly_line_id,
                        principalTable: "assembly_lines",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_scheduled_operations_operation_definitions_operation_defini~",
                        column: x => x.operation_definition_id,
                        principalTable: "operation_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scheduled_operations_planning_baselines_planning_baseline_id",
                        column: x => x.planning_baseline_id,
                        principalTable: "planning_baselines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scheduled_operations_work_centers_work_center_id",
                        column: x => x.work_center_id,
                        principalTable: "work_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    heat_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    batch_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    remaining_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: true),
                    produced_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    country_of_origin = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    block_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_lots", x => x.id);
                    table.CheckConstraint("ck_materiallot_status", "status IN ('AwaitingInspection', 'Accepted', 'ConditionallyReleased', 'Blocked', 'Recalled')");
                    table.ForeignKey(
                        name: "fk_material_lots_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_material_lots_purchase_order_lines_purchase_order_line_id",
                        column: x => x.purchase_order_line_id,
                        principalTable: "purchase_order_lines",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_material_lots_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_line_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_order_line_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_order_line_changes_purchase_order_lines_purchase_o~",
                        column: x => x.purchase_order_line_id,
                        principalTable: "purchase_order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    previous_score = table.Column<int>(type: "integer", nullable: true),
                    factors_json = table.Column<string>(type: "jsonb", nullable: false),
                    endangered_orders_json = table.Column<string>(type: "jsonb", nullable: false),
                    trigger = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    assessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_risk_assessments_purchase_order_lines_purchase_order_line_id",
                        column: x => x.purchase_order_line_id,
                        principalTable: "purchase_order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "passport_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    passport_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    generated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_passport_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_passport_versions_passports_passport_id",
                        column: x => x.passport_id,
                        principalTable: "passports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_consumptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_serial_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_consumptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_consumptions_material_lots_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_material_consumptions_operation_definitions_operation_defin~",
                        column: x => x.operation_definition_id,
                        principalTable: "operation_definitions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_material_consumptions_product_serials_product_serial_id",
                        column: x => x.product_serial_id,
                        principalTable: "product_serials",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_material_consumptions_production_orders_production_order_id",
                        column: x => x.production_order_id,
                        principalTable: "production_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "non_conformances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    raised_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    raised_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    disposition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_non_conformances", x => x.id);
                    table.ForeignKey(
                        name: "fk_non_conformances_material_lots_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_non_conformances_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "quality_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    heat_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    uploaded_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verification_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ai_suggestion_json = table.Column<string>(type: "jsonb", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_documents", x => x.id);
                    table.CheckConstraint("ck_qualitydocument_status", "status IN ('Pending', 'Verifying', 'Accepted', 'Rejected', 'RequiresCompletion', 'Missing')");
                    table.CheckConstraint("ck_qualitydocument_type", "type IN ('MATERIAL_CERT', 'INSPECTION_REPORT', 'DECLARATION_OF_CONFORMITY', 'TRANSPORT_DOC')");
                    table.ForeignKey(
                        name: "fk_quality_documents_material_lots_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_quality_documents_purchase_order_lines_purchase_order_line_~",
                        column: x => x.purchase_order_line_id,
                        principalTable: "purchase_order_lines",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_quality_documents_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "quality_inspections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_serial_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    inspected_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    inspected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    measurements_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_inspections", x => x.id);
                    table.ForeignKey(
                        name: "fk_quality_inspections_material_lots_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_quality_inspections_product_serials_product_serial_id",
                        column: x => x.product_serial_id,
                        principalTable: "product_serials",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservations", x => x.id);
                    table.ForeignKey(
                        name: "fk_reservations_material_lots_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_reservations_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reservations_production_orders_production_order_id",
                        column: x => x.production_order_id,
                        principalTable: "production_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assembly_lines_code",
                table: "assembly_lines",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assembly_lines_site_id",
                table: "assembly_lines",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity_entity_code",
                table: "audit_events",
                columns: new[] { "entity", "entity_code" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_occurred_at",
                table: "audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_bom_items_bom_version_id_part_id",
                table: "bom_items",
                columns: new[] { "bom_version_id", "part_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bom_items_part_id",
                table: "bom_items",
                column: "part_id");

            migrationBuilder.CreateIndex(
                name: "ix_bom_versions_product_id_version",
                table: "bom_versions",
                columns: new[] { "product_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_capacity_calendars_work_center_id_date",
                table: "capacity_calendars",
                columns: new[] { "work_center_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_created_at",
                table: "idempotency_records",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_balances_part_id_site_id",
                table: "inventory_balances",
                columns: new[] { "part_id", "site_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_balances_site_id",
                table: "inventory_balances",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_logistics_risk_events_code",
                table: "logistics_risk_events",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_logistics_risk_events_shipment_id",
                table: "logistics_risk_events",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_logistics_risk_events_supplier_id",
                table: "logistics_risk_events",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_consumptions_material_lot_id",
                table: "material_consumptions",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_consumptions_operation_definition_id",
                table: "material_consumptions",
                column: "operation_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_consumptions_product_serial_id",
                table: "material_consumptions",
                column: "product_serial_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_consumptions_production_order_id",
                table: "material_consumptions",
                column: "production_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_lots_lot_number",
                table: "material_lots",
                column: "lot_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_lots_part_id",
                table: "material_lots",
                column: "part_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_lots_purchase_order_line_id",
                table: "material_lots",
                column: "purchase_order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_lots_supplier_id",
                table: "material_lots",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_non_conformances_code",
                table: "non_conformances",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_non_conformances_material_lot_id",
                table: "non_conformances",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_non_conformances_supplier_id",
                table: "non_conformances",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_target_role_is_read",
                table: "notifications",
                columns: new[] { "target_role", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_code",
                table: "operation_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_production_order_id",
                table: "operation_definitions",
                column: "production_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_work_center_id",
                table: "operation_definitions",
                column: "work_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_code",
                table: "organizations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at_next_attempt_at",
                table: "outbox_messages",
                columns: new[] { "processed_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_parts_alternative_supplier_id",
                table: "parts",
                column: "alternative_supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_parts_code",
                table: "parts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parts_primary_supplier_id",
                table: "parts",
                column: "primary_supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_passport_templates_code",
                table: "passport_templates",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_passport_versions_passport_id_version",
                table: "passport_versions",
                columns: new[] { "passport_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_passports_passport_template_id",
                table: "passports",
                column: "passport_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_passports_product_serial_id",
                table: "passports",
                column: "product_serial_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planning_baselines_version",
                table: "planning_baselines",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planning_recommendations_planning_scenario_id",
                table: "planning_recommendations",
                column: "planning_scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_planning_scenarios_baseline_id",
                table: "planning_scenarios",
                column: "baseline_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_serials_bom_version_id",
                table: "product_serials",
                column: "bom_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_serials_product_id",
                table: "product_serials",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_serials_production_order_id",
                table: "product_serials",
                column: "production_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_serials_serial_number",
                table: "product_serials",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_production_orders_assembly_line_id",
                table: "production_orders",
                column: "assembly_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_orders_bom_version_id",
                table: "production_orders",
                column: "bom_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_orders_code",
                table: "production_orders",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_production_orders_product_id",
                table: "production_orders",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_orders_site_id",
                table: "production_orders",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_code",
                table: "products",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_line_changes_purchase_order_line_id",
                table: "purchase_order_line_changes",
                column: "purchase_order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_part_id",
                table: "purchase_order_lines",
                column: "part_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_purchase_order_id_line_no",
                table: "purchase_order_lines",
                columns: new[] { "purchase_order_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_risk_score",
                table: "purchase_order_lines",
                column: "risk_score");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_shipment_id",
                table: "purchase_order_lines",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_code",
                table: "purchase_orders",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_site_id",
                table: "purchase_orders",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_supplier_id",
                table: "purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_documents_document_number",
                table: "quality_documents",
                column: "document_number");

            migrationBuilder.CreateIndex(
                name: "ix_quality_documents_material_lot_id",
                table: "quality_documents",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_documents_purchase_order_line_id",
                table: "quality_documents",
                column: "purchase_order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_documents_supplier_id",
                table: "quality_documents",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_inspections_code",
                table: "quality_inspections",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quality_inspections_material_lot_id",
                table: "quality_inspections",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_inspections_product_serial_id",
                table: "quality_inspections",
                column: "product_serial_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_requirements_passport_template_id_code",
                table: "quality_requirements",
                columns: new[] { "passport_template_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reservations_material_lot_id",
                table: "reservations",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_part_id",
                table: "reservations",
                column: "part_id");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_production_order_id",
                table: "reservations",
                column: "production_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessments_purchase_order_line_id_assessed_at",
                table: "risk_assessments",
                columns: new[] { "purchase_order_line_id", "assessed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_scenario_changes_planning_scenario_id",
                table: "scenario_changes",
                column: "planning_scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_operations_assembly_line_id",
                table: "scheduled_operations",
                column: "assembly_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_operations_operation_definition_id",
                table: "scheduled_operations",
                column: "operation_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_operations_planning_baseline_id_operation_definit~",
                table: "scheduled_operations",
                columns: new[] { "planning_baseline_id", "operation_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_operations_work_center_id",
                table: "scheduled_operations",
                column: "work_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipment_events_shipment_id",
                table: "shipment_events",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipments_code",
                table: "shipments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipments_purchase_order_id",
                table: "shipments",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipments_supplier_id",
                table: "shipments",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_sites_code",
                table: "sites",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sites_organization_id",
                table: "sites",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_performances_supplier_id_period_start",
                table: "supplier_performances",
                columns: new[] { "supplier_id", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_code",
                table: "suppliers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_traceability_links_from_type_from_id",
                table: "traceability_links",
                columns: new[] { "from_type", "from_id" });

            migrationBuilder.CreateIndex(
                name: "ix_traceability_links_to_type_to_id",
                table: "traceability_links",
                columns: new[] { "to_type", "to_id" });

            migrationBuilder.CreateIndex(
                name: "ix_users_site_id",
                table: "users",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_supplier_id",
                table: "users",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_centers_assembly_line_id",
                table: "work_centers",
                column: "assembly_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_centers_code",
                table: "work_centers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_centers_site_id",
                table: "work_centers",
                column: "site_id");
            // Append-only audit log: application role cannot UPDATE/DELETE rows (TRUNCATE stays allowed for demo reset).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit_events_immutable() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only (% not allowed)', TG_OP;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER trg_audit_events_immutable
                    BEFORE UPDATE OR DELETE ON audit_events
                    FOR EACH ROW EXECUTE FUNCTION audit_events_immutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "bom_items");

            migrationBuilder.DropTable(
                name: "capacity_calendars");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "inventory_balances");

            migrationBuilder.DropTable(
                name: "logistics_risk_events");

            migrationBuilder.DropTable(
                name: "material_consumptions");

            migrationBuilder.DropTable(
                name: "non_conformances");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "passport_versions");

            migrationBuilder.DropTable(
                name: "planning_recommendations");

            migrationBuilder.DropTable(
                name: "purchase_order_line_changes");

            migrationBuilder.DropTable(
                name: "quality_documents");

            migrationBuilder.DropTable(
                name: "quality_inspections");

            migrationBuilder.DropTable(
                name: "quality_requirements");

            migrationBuilder.DropTable(
                name: "reservations");

            migrationBuilder.DropTable(
                name: "risk_assessments");

            migrationBuilder.DropTable(
                name: "scenario_changes");

            migrationBuilder.DropTable(
                name: "scheduled_operations");

            migrationBuilder.DropTable(
                name: "shipment_events");

            migrationBuilder.DropTable(
                name: "supplier_performances");

            migrationBuilder.DropTable(
                name: "traceability_links");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "passports");

            migrationBuilder.DropTable(
                name: "material_lots");

            migrationBuilder.DropTable(
                name: "planning_scenarios");

            migrationBuilder.DropTable(
                name: "operation_definitions");

            migrationBuilder.DropTable(
                name: "passport_templates");

            migrationBuilder.DropTable(
                name: "product_serials");

            migrationBuilder.DropTable(
                name: "purchase_order_lines");

            migrationBuilder.DropTable(
                name: "planning_baselines");

            migrationBuilder.DropTable(
                name: "work_centers");

            migrationBuilder.DropTable(
                name: "production_orders");

            migrationBuilder.DropTable(
                name: "parts");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropTable(
                name: "assembly_lines");

            migrationBuilder.DropTable(
                name: "bom_versions");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
