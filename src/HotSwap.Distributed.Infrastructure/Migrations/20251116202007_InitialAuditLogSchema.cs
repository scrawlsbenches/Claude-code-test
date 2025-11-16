using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HotSwap.Distributed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditLogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    user_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resource_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    span_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    source_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    audit_log_id = table.Column<long>(type: "bigint", nullable: false),
                    approval_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deployment_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    module_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requester_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    approver_emails = table.Column<string[]>(type: "text[]", nullable: true),
                    approval_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    decision_by_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decision_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    timeout_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_expired = table.Column<bool>(type: "boolean", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_audit_events_audit_logs_audit_log_id",
                        column: x => x.audit_log_id,
                        principalTable: "audit_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "authentication_audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    audit_log_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    authentication_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    authentication_result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    token_issued = table.Column<bool>(type: "boolean", nullable: false),
                    token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    geo_location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_suspicious = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_authentication_audit_events_audit_logs_audit_log_id",
                        column: x => x.audit_log_id,
                        principalTable: "audit_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuration_audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    audit_log_id = table.Column<long>(type: "bigint", nullable: false),
                    configuration_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    configuration_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    changed_by_user = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    change_reason = table.Column<string>(type: "text", nullable: true),
                    approved_by_user = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_configuration_audit_events_audit_logs_audit_log_id",
                        column: x => x.audit_log_id,
                        principalTable: "audit_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deployment_audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    audit_log_id = table.Column<long>(type: "bigint", nullable: false),
                    deployment_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    module_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deployment_strategy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pipeline_stage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    stage_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nodes_targeted = table.Column<int>(type: "integer", nullable: true),
                    nodes_deployed = table.Column<int>(type: "integer", nullable: true),
                    nodes_failed = table.Column<int>(type: "integer", nullable: true),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    exception_details = table.Column<string>(type: "text", nullable: true),
                    requester_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_deployment_audit_events_audit_logs_audit_log_id",
                        column: x => x.audit_log_id,
                        principalTable: "audit_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_approval_audit_approval_id",
                table: "approval_audit_events",
                column: "approval_id");

            migrationBuilder.CreateIndex(
                name: "idx_approval_audit_approver",
                table: "approval_audit_events",
                column: "decision_by_email");

            migrationBuilder.CreateIndex(
                name: "idx_approval_audit_deployment_id",
                table: "approval_audit_events",
                column: "deployment_execution_id");

            migrationBuilder.CreateIndex(
                name: "idx_approval_audit_environment",
                table: "approval_audit_events",
                column: "target_environment");

            migrationBuilder.CreateIndex(
                name: "idx_approval_audit_requester",
                table: "approval_audit_events",
                column: "requester_email");

            migrationBuilder.CreateIndex(
                name: "idx_approval_audit_status",
                table: "approval_audit_events",
                column: "approval_status");

            migrationBuilder.CreateIndex(
                name: "IX_approval_audit_events_audit_log_id",
                table: "approval_audit_events",
                column: "audit_log_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_category",
                table: "audit_logs",
                column: "event_category");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_event_type",
                table: "audit_logs",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_resource",
                table: "audit_logs",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_timestamp",
                table: "audit_logs",
                column: "timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_trace_id",
                table: "audit_logs",
                column: "trace_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_user",
                table: "audit_logs",
                columns: new[] { "username", "user_email" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_event_id",
                table: "audit_logs",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_auth_audit_result",
                table: "authentication_audit_events",
                column: "authentication_result");

            migrationBuilder.CreateIndex(
                name: "idx_auth_audit_source_ip",
                table: "authentication_audit_events",
                column: "source_ip");

            migrationBuilder.CreateIndex(
                name: "idx_auth_audit_suspicious",
                table: "authentication_audit_events",
                column: "is_suspicious",
                filter: "is_suspicious = true");

            migrationBuilder.CreateIndex(
                name: "idx_auth_audit_timestamp",
                table: "authentication_audit_events",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_auth_audit_username",
                table: "authentication_audit_events",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "IX_authentication_audit_events_audit_log_id",
                table: "authentication_audit_events",
                column: "audit_log_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_config_audit_category",
                table: "configuration_audit_events",
                column: "configuration_category");

            migrationBuilder.CreateIndex(
                name: "idx_config_audit_key",
                table: "configuration_audit_events",
                column: "configuration_key");

            migrationBuilder.CreateIndex(
                name: "idx_config_audit_timestamp",
                table: "configuration_audit_events",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_config_audit_user",
                table: "configuration_audit_events",
                column: "changed_by_user");

            migrationBuilder.CreateIndex(
                name: "IX_configuration_audit_events_audit_log_id",
                table: "configuration_audit_events",
                column: "audit_log_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_deployment_audit_environment",
                table: "deployment_audit_events",
                column: "target_environment");

            migrationBuilder.CreateIndex(
                name: "idx_deployment_audit_execution_id",
                table: "deployment_audit_events",
                column: "deployment_execution_id");

            migrationBuilder.CreateIndex(
                name: "idx_deployment_audit_module",
                table: "deployment_audit_events",
                columns: new[] { "module_name", "module_version" });

            migrationBuilder.CreateIndex(
                name: "idx_deployment_audit_start_time",
                table: "deployment_audit_events",
                column: "start_time",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_deployment_audit_status",
                table: "deployment_audit_events",
                column: "stage_status");

            migrationBuilder.CreateIndex(
                name: "IX_deployment_audit_events_audit_log_id",
                table: "deployment_audit_events",
                column: "audit_log_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_audit_events");

            migrationBuilder.DropTable(
                name: "authentication_audit_events");

            migrationBuilder.DropTable(
                name: "configuration_audit_events");

            migrationBuilder.DropTable(
                name: "deployment_audit_events");

            migrationBuilder.DropTable(
                name: "audit_logs");
        }
    }
}
