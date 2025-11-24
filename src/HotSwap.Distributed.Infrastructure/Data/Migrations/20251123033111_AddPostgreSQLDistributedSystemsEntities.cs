using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HotSwap.Distributed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPostgreSQLDistributedSystemsEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    DeploymentExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterEmail = table.Column<string>(type: "text", nullable: false),
                    TargetEnvironment = table.Column<string>(type: "text", nullable: false),
                    ModuleName = table.Column<string>(type: "text", nullable: false),
                    ModuleVersion = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ApproverEmails = table.Column<List<string>>(type: "text[]", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeoutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedByEmail = table.Column<string>(type: "text", nullable: true),
                    ResponseReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.DeploymentExecutionId);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeploymentId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingInstance = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<string>(type: "text", nullable: false),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingInstance = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_approval_requests_pending",
                table: "ApprovalRequests",
                column: "TimeoutAt",
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "idx_approval_requests_requested_at",
                table: "ApprovalRequests",
                column: "RequestedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_approval_requests_requester",
                table: "ApprovalRequests",
                column: "RequesterEmail");

            migrationBuilder.CreateIndex(
                name: "idx_approval_requests_status_timeout",
                table: "ApprovalRequests",
                columns: new[] { "Status", "TimeoutAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ApprovalId",
                table: "ApprovalRequests",
                column: "ApprovalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_deployment_jobs_claimable",
                table: "DeploymentJobs",
                column: "CreatedAt",
                filter: "status IN ('Pending', 'Failed')");

            migrationBuilder.CreateIndex(
                name: "idx_deployment_jobs_lock",
                table: "DeploymentJobs",
                column: "LockedUntil",
                filter: "status = 'Running'");

            migrationBuilder.CreateIndex(
                name: "idx_deployment_jobs_pending",
                table: "DeploymentJobs",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentJobs_DeploymentId",
                table: "DeploymentJobs",
                column: "DeploymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_messages_lock",
                table: "Messages",
                column: "LockedUntil",
                filter: "status = 'Processing'");

            migrationBuilder.CreateIndex(
                name: "idx_messages_pending",
                table: "Messages",
                columns: new[] { "Topic", "Priority" },
                descending: new bool[0],
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "idx_messages_topic_priority",
                table: "Messages",
                columns: new[] { "Topic", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_MessageId",
                table: "Messages",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRequests");

            migrationBuilder.DropTable(
                name: "DeploymentJobs");

            migrationBuilder.DropTable(
                name: "Messages");
        }
    }
}
