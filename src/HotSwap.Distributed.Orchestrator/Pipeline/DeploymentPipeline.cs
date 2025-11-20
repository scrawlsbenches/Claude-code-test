using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Telemetry;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Strategies;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HotSwap.Distributed.Orchestrator.Pipeline;

/// <summary>
/// Deployment pipeline that orchestrates the full CI/CD flow.
/// Build → Test → Security → Dev → QA → Staging → Production
/// </summary>
public class DeploymentPipeline : IDisposable
{
    private readonly ILogger<DeploymentPipeline> _logger;
    private readonly IClusterRegistry _clusterRegistry;
    private readonly IModuleVerifier _moduleVerifier;
    private readonly TelemetryProvider _telemetry;
    private readonly PipelineConfiguration _config;
    private readonly Dictionary<EnvironmentType, IDeploymentStrategy> _strategies;
    private readonly IApprovalService? _approvalService;
    private readonly IAuditLogService? _auditLogService;
    private readonly IDeploymentTracker? _deploymentTracker;
    private readonly IDeploymentNotifier? _deploymentNotifier;

    public DeploymentPipeline(
        ILogger<DeploymentPipeline> logger,
        IClusterRegistry clusterRegistry,
        IModuleVerifier moduleVerifier,
        TelemetryProvider telemetry,
        PipelineConfiguration config,
        Dictionary<EnvironmentType, IDeploymentStrategy> strategies,
        IApprovalService? approvalService = null,
        IAuditLogService? auditLogService = null,
        IDeploymentTracker? deploymentTracker = null,
        IDeploymentNotifier? deploymentNotifier = null)
    {
        _logger = logger;
        _clusterRegistry = clusterRegistry;
        _moduleVerifier = moduleVerifier;
        _telemetry = telemetry;
        _config = config;
        _strategies = strategies;
        _approvalService = approvalService;
        _auditLogService = auditLogService;
        _deploymentTracker = deploymentTracker;
        _deploymentNotifier = deploymentNotifier;
    }

    /// <summary>
    /// Executes the full deployment pipeline.
    /// </summary>
    public async Task<PipelineExecutionResult> ExecutePipelineAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new PipelineExecutionResult
        {
            ExecutionId = request.ExecutionId,
            ModuleName = request.Module.Name,
            Version = request.Module.Version,
            StartTime = DateTime.UtcNow
        };

        using var activity = _telemetry.StartDeploymentActivity(
            request.Module.Name,
            request.Module.Version,
            request.TargetEnvironment,
            "Pipeline");

        result.TraceId = activity?.TraceId.ToString();

        // Audit log: Pipeline started
        await LogDeploymentEventAsync(
            "PipelineStarted",
            "Running",
            request,
            "Pipeline",
            "Running",
            result.TraceId,
            cancellationToken);

        try
        {
            _logger.LogInformation("Starting deployment pipeline for {ModuleName} v{Version} (Execution: {ExecutionId})",
                request.Module.Name, request.Module.Version, request.ExecutionId);

            // Initialize pipeline state tracking
            await UpdatePipelineStateAsync(request, "Running", null, result.StageResults);

            // Check if approval is required BEFORE starting any stages
            if (RequiresApprovalUpfront(request))
            {
                _logger.LogInformation("Deployment requires approval before proceeding with pipeline stages");

                var approvalStage = await ExecuteApprovalStageAsync(
                    request,
                    request.TargetEnvironment,
                    cancellationToken,
                    result.StageResults);

                result.StageResults.Add(approvalStage);

                if (approvalStage.Status != PipelineStageStatus.Succeeded)
                {
                    result.Success = false;
                    result.Message = "Pipeline failed at Approval stage";
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }
            }

            // Stage 1: Build
            var buildStage = await ExecuteBuildStageAsync(request, cancellationToken);
            result.StageResults.Add(buildStage);
            await UpdatePipelineStateAsync(request, "Running", buildStage.StageName, result.StageResults);

            if (buildStage.Status != PipelineStageStatus.Succeeded)
            {
                result.Success = false;
                result.Message = "Pipeline failed at Build stage";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Stage 2: Test
            var testStage = await ExecuteTestStageAsync(request, cancellationToken);
            result.StageResults.Add(testStage);
            await UpdatePipelineStateAsync(request, "Running", testStage.StageName, result.StageResults);

            if (testStage.Status != PipelineStageStatus.Succeeded)
            {
                result.Success = false;
                result.Message = "Pipeline failed at Test stage";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Stage 3: Security Scan
            var securityStage = await ExecuteSecurityScanStageAsync(request, cancellationToken);
            result.StageResults.Add(securityStage);
            await UpdatePipelineStateAsync(request, "Running", securityStage.StageName, result.StageResults);

            if (securityStage.Status != PipelineStageStatus.Succeeded)
            {
                result.Success = false;
                result.Message = "Pipeline failed at Security Scan stage";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Stage 4-7: Deploy to environments based on target
            var deploymentStages = await ExecuteDeploymentStagesAsync(request, cancellationToken, result.StageResults);
            result.StageResults.AddRange(deploymentStages);
            await UpdatePipelineStateAsync(request, "Running", "Deployments", result.StageResults);

            var failedDeployment = deploymentStages.FirstOrDefault(s => s.Status == PipelineStageStatus.Failed);

            if (failedDeployment != null)
            {
                result.Success = false;
                result.Message = $"Pipeline failed at {failedDeployment.StageName}";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Stage: Final Validation
            var validationStage = await ExecuteValidationStageAsync(request, cancellationToken);
            result.StageResults.Add(validationStage);
            await UpdatePipelineStateAsync(request, "Running", validationStage.StageName, result.StageResults);

            result.Success = validationStage.Status == PipelineStageStatus.Succeeded;
            result.Message = result.Success
                ? "Pipeline completed successfully"
                : "Pipeline failed at Validation stage";
            result.EndTime = DateTime.UtcNow;

            // Update pipeline state with final status (Succeeded or Failed)
            await UpdatePipelineStateAsync(
                request,
                result.Success ? "Succeeded" : "Failed",
                "Completed",
                result.StageResults);

            _telemetry.RecordDeploymentSuccess(activity, new DeploymentResult
            {
                Success = result.Success,
                Environment = request.TargetEnvironment,
                Strategy = "Pipeline",
                StartTime = result.StartTime,
                EndTime = result.EndTime
            });

            _logger.LogInformation("Deployment pipeline completed for {ModuleName} v{Version}: {Success}",
                request.Module.Name, request.Module.Version, result.Success ? "SUCCESS" : "FAILED");

            // Audit log: Pipeline completed
            await LogDeploymentEventAsync(
                "PipelineCompleted",
                result.Success ? "Success" : "Failure",
                request,
                "Pipeline",
                result.Success ? "Succeeded" : "Failed",
                result.TraceId,
                cancellationToken,
                durationMs: (int)(result.EndTime - result.StartTime).TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment pipeline failed with exception for {ModuleName}",
                request.Module.Name);

            result.Success = false;
            result.Message = $"Pipeline failed with exception: {ex.Message}";
            result.EndTime = DateTime.UtcNow;

            _telemetry.RecordDeploymentFailure(activity, new DeploymentResult
            {
                Success = false,
                Environment = request.TargetEnvironment,
                Strategy = "Pipeline",
                Exception = ex
            }, ex);

            // Audit log: Pipeline failed with exception
            await LogDeploymentEventAsync(
                "PipelineFailed",
                "Failure",
                request,
                "Pipeline",
                "Failed",
                result.TraceId,
                cancellationToken,
                errorMessage: ex.Message,
                exceptionDetails: ex.ToString());

            return result;
        }
    }

    private async Task<PipelineStageResult> ExecuteBuildStageAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken)
    {
        var stage = new PipelineStageResult
        {
            StageName = "Build",
            Status = PipelineStageStatus.Running,
            StartTime = DateTime.UtcNow
        };

        using var activity = _telemetry.StartStageActivity("Build");

        try
        {
            _logger.LogInformation("Executing Build stage for {ModuleName}", request.Module.Name);

            // Simulate build process
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            stage.Status = PipelineStageStatus.Succeeded;
            stage.Message = "Build completed successfully";
            stage.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Build stage completed in {Duration}ms",
                stage.Duration.TotalMilliseconds);

            return stage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build stage failed");

            stage.Status = PipelineStageStatus.Failed;
            stage.Message = $"Build failed: {ex.Message}";
            stage.Exception = ex;
            stage.EndTime = DateTime.UtcNow;

            return stage;
        }
    }

    private async Task<PipelineStageResult> ExecuteTestStageAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken)
    {
        var stage = new PipelineStageResult
        {
            StageName = "Test",
            Status = PipelineStageStatus.Running,
            StartTime = DateTime.UtcNow
        };

        using var activity = _telemetry.StartStageActivity("Test");

        try
        {
            _logger.LogInformation("Executing Test stage for {ModuleName}", request.Module.Name);

            // Simulate test execution
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

            stage.Status = PipelineStageStatus.Succeeded;
            stage.Message = "All tests passed";
            stage.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Test stage completed in {Duration}ms",
                stage.Duration.TotalMilliseconds);

            return stage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test stage failed");

            stage.Status = PipelineStageStatus.Failed;
            stage.Message = $"Tests failed: {ex.Message}";
            stage.Exception = ex;
            stage.EndTime = DateTime.UtcNow;

            return stage;
        }
    }

    private async Task<PipelineStageResult> ExecuteSecurityScanStageAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken)
    {
        var stage = new PipelineStageResult
        {
            StageName = "Security Scan",
            Status = PipelineStageStatus.Running,
            StartTime = DateTime.UtcNow
        };

        using var activity = _telemetry.StartStageActivity("SecurityScan");

        try
        {
            _logger.LogInformation("Executing Security Scan stage for {ModuleName}", request.Module.Name);

            // Verify module signature
            var moduleData = new byte[] { 1, 2, 3 }; // Simulated module data
            var validation = await _moduleVerifier.ValidateModuleAsync(
                request.Module,
                moduleData,
                cancellationToken);

            if (!validation.IsValid)
            {
                stage.Status = PipelineStageStatus.Failed;
                stage.Message = $"Security validation failed: {string.Join(", ", validation.ValidationMessages)}";
                stage.EndTime = DateTime.UtcNow;
                return stage;
            }

            stage.Status = PipelineStageStatus.Succeeded;
            stage.Message = "Security scan passed";
            stage.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Security Scan stage completed in {Duration}ms",
                stage.Duration.TotalMilliseconds);

            return stage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security Scan stage failed");

            stage.Status = PipelineStageStatus.Failed;
            stage.Message = $"Security scan failed: {ex.Message}";
            stage.Exception = ex;
            stage.EndTime = DateTime.UtcNow;

            return stage;
        }
    }

    private async Task<List<PipelineStageResult>> ExecuteDeploymentStagesAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken,
        List<PipelineStageResult>? previousStages = null)
    {
        var stages = new List<PipelineStageResult>();

        // Define deployment order based on target environment
        var environments = request.TargetEnvironment switch
        {
            EnvironmentType.Development => new[] { EnvironmentType.Development },
            EnvironmentType.QA => new[] { EnvironmentType.Development, EnvironmentType.QA },
            EnvironmentType.Staging => new[] { EnvironmentType.Development, EnvironmentType.QA, EnvironmentType.Staging },
            EnvironmentType.Production => new[] { EnvironmentType.Development, EnvironmentType.QA, EnvironmentType.Staging, EnvironmentType.Production },
            _ => new[] { request.TargetEnvironment }
        };

        foreach (var environment in environments)
        {
            // Check if approval is required for this environment
            if (RequiresApproval(request, environment))
            {
                // Combine previous stages with current deployment stages for full context
                var allPreviousStages = new List<PipelineStageResult>();
                if (previousStages != null)
                {
                    allPreviousStages.AddRange(previousStages);
                }
                allPreviousStages.AddRange(stages);

                var approvalStage = await ExecuteApprovalStageAsync(
                    request,
                    environment,
                    cancellationToken,
                    allPreviousStages);

                stages.Add(approvalStage);

                if (approvalStage.Status != PipelineStageStatus.Succeeded)
                {
                    break; // Stop pipeline if approval was not granted
                }
            }

            var deployStage = await ExecuteDeploymentToEnvironmentAsync(
                request,
                environment,
                cancellationToken);

            stages.Add(deployStage);

            if (deployStage.Status == PipelineStageStatus.Failed)
            {
                break; // Stop pipeline on failure
            }
        }

        return stages;
    }

    private async Task<PipelineStageResult> ExecuteDeploymentToEnvironmentAsync(
        DeploymentRequest request,
        EnvironmentType environment,
        CancellationToken cancellationToken)
    {
        var stage = new PipelineStageResult
        {
            StageName = $"Deploy to {environment}",
            Status = PipelineStageStatus.Running,
            StartTime = DateTime.UtcNow
        };

        using var activity = _telemetry.StartStageActivity($"Deploy_{environment}", environment);

        try
        {
            _logger.LogInformation("Executing deployment to {Environment}", environment);

            var cluster = await _clusterRegistry.GetClusterAsync(environment, cancellationToken);
            var strategy = _strategies[environment];

            stage.Strategy = strategy.StrategyName;

            var deploymentRequest = new ModuleDeploymentRequest
            {
                ModuleName = request.Module.Name,
                Version = request.Module.Version,
                Descriptor = request.Module
            };

            var deploymentResult = await strategy.DeployAsync(
                deploymentRequest,
                cluster,
                cancellationToken);

            stage.NodesDeployed = deploymentResult.NodeResults.Count(r => r.Success);
            stage.NodesFailed = deploymentResult.NodeResults.Count(r => !r.Success);

            if (deploymentResult.Success)
            {
                stage.Status = PipelineStageStatus.Succeeded;
                stage.Message = deploymentResult.Message;
            }
            else
            {
                stage.Status = PipelineStageStatus.Failed;
                stage.Message = deploymentResult.Message;
                stage.Exception = deploymentResult.Exception;
            }

            stage.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Deployment to {Environment} completed: {Status} in {Duration}ms",
                environment, stage.Status, stage.Duration.TotalMilliseconds);

            return stage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment to {Environment} failed", environment);

            stage.Status = PipelineStageStatus.Failed;
            stage.Message = $"Deployment failed: {ex.Message}";
            stage.Exception = ex;
            stage.EndTime = DateTime.UtcNow;

            return stage;
        }
    }

    private async Task<PipelineStageResult> ExecuteValidationStageAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken)
    {
        var stage = new PipelineStageResult
        {
            StageName = "Validation",
            Status = PipelineStageStatus.Running,
            StartTime = DateTime.UtcNow
        };

        using var activity = _telemetry.StartStageActivity("Validation");

        try
        {
            _logger.LogInformation("Executing Validation stage for {ModuleName}", request.Module.Name);

            // Simulate validation
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            stage.Status = PipelineStageStatus.Succeeded;
            stage.Message = "Validation completed successfully";
            stage.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Validation stage completed in {Duration}ms",
                stage.Duration.TotalMilliseconds);

            return stage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation stage failed");

            stage.Status = PipelineStageStatus.Failed;
            stage.Message = $"Validation failed: {ex.Message}";
            stage.Exception = ex;
            stage.EndTime = DateTime.UtcNow;

            return stage;
        }
    }

    /// <summary>
    /// Determines if approval is required BEFORE starting the pipeline (upfront approval).
    /// This is used for deployments that should pause at PendingApproval before any stages execute.
    /// </summary>
    private bool RequiresApprovalUpfront(DeploymentRequest request)
    {
        // Approval required upfront if:
        // 1. Approval service is available AND
        // 2. RequireApproval flag is set AND
        // 3. Target environment is Staging or Production
        return _approvalService != null &&
               request.RequireApproval &&
               (request.TargetEnvironment == EnvironmentType.Staging ||
                request.TargetEnvironment == EnvironmentType.Production);
    }

    /// <summary>
    /// Determines if approval is required for deploying to the specified environment.
    /// </summary>
    private bool RequiresApproval(DeploymentRequest request, EnvironmentType environment)
    {
        // Approval required if:
        // 1. Approval service is available AND
        // 2. RequireApproval flag is set AND
        // 3. Environment is Staging or Production AND
        // 4. We haven't already done upfront approval (to avoid double approval)

        // If upfront approval was done, skip environment-specific approval
        if (RequiresApprovalUpfront(request))
        {
            return false; // Already handled upfront
        }

        return _approvalService != null &&
               request.RequireApproval &&
               (environment == EnvironmentType.Staging || environment == EnvironmentType.Production);
    }

    /// <summary>
    /// Executes the approval stage for a deployment.
    /// </summary>
    private async Task<PipelineStageResult> ExecuteApprovalStageAsync(
        DeploymentRequest request,
        EnvironmentType environment,
        CancellationToken cancellationToken,
        List<PipelineStageResult>? previousStages = null)
    {
        var stage = new PipelineStageResult
        {
            StageName = "Approval",
            Status = PipelineStageStatus.Pending,
            StartTime = DateTime.UtcNow
        };

        using var activity = _telemetry.StartStageActivity($"Approval_{environment}", environment);

        try
        {
            if (_approvalService == null)
            {
                throw new InvalidOperationException("Approval service is not configured");
            }

            _logger.LogInformation(
                "Requesting approval for deployment to {Environment} for {ModuleName} v{Version}",
                environment, request.Module.Name, request.Module.Version);

            // Create approval request
            var approvalRequest = new ApprovalRequest
            {
                DeploymentExecutionId = request.ExecutionId,
                ModuleName = request.Module.Name,
                Version = request.Module.Version,
                TargetEnvironment = environment,
                RequesterEmail = request.RequesterEmail,
                ApproverEmails = new List<string>(), // Would be configured based on environment
                Metadata = request.Metadata,
                TimeoutAt = DateTime.UtcNow.Add(_config.ApprovalTimeout)
            };

            // Create the approval request
            await _approvalService.CreateApprovalRequestAsync(approvalRequest, cancellationToken);

            _logger.LogInformation(
                "Approval request {ApprovalId} created, waiting for decision (timeout: {Timeout})",
                approvalRequest.ApprovalId, approvalRequest.TimeoutAt);

            // Update pipeline state to PendingApproval (includes all previous stages + current approval stage)
            var stagesWithApproval = previousStages != null
                ? new List<PipelineStageResult>(previousStages) { stage }
                : new List<PipelineStageResult> { stage };

            await UpdatePipelineStateAsync(request, "PendingApproval", stage.StageName, stagesWithApproval);

            // Wait for approval decision
            var result = await _approvalService.WaitForApprovalAsync(
                request.ExecutionId,
                cancellationToken);

            stage.EndTime = DateTime.UtcNow;

            if (result.Status == ApprovalStatus.Approved)
            {
                stage.Status = PipelineStageStatus.Succeeded;
                stage.Message = $"Approved by {result.RespondedByEmail}. {result.ResponseReason ?? string.Empty}";

                _logger.LogInformation(
                    "Deployment to {Environment} approved by {Approver}",
                    environment, result.RespondedByEmail);
            }
            else if (result.Status == ApprovalStatus.Rejected)
            {
                stage.Status = PipelineStageStatus.Failed;
                stage.Message = $"Rejected by {result.RespondedByEmail}. {result.ResponseReason ?? string.Empty}";

                _logger.LogWarning(
                    "Deployment to {Environment} rejected by {Approver}. Reason: {Reason}",
                    environment, result.RespondedByEmail, result.ResponseReason);
            }
            else if (result.Status == ApprovalStatus.Expired)
            {
                stage.Status = PipelineStageStatus.Failed;
                stage.Message = $"Approval request expired after {_config.ApprovalTimeout.TotalHours} hours";

                _logger.LogWarning(
                    "Deployment to {Environment} failed: approval request expired",
                    environment);
            }

            return stage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approval stage failed for {Environment}", environment);

            stage.Status = PipelineStageStatus.Failed;
            stage.Message = $"Approval stage failed: {ex.Message}";
            stage.Exception = ex;
            stage.EndTime = DateTime.UtcNow;

            return stage;
        }
    }

    /// <summary>
    /// Helper method to log deployment events to the audit log.
    /// </summary>
    private async Task LogDeploymentEventAsync(
        string eventType,
        string result,
        DeploymentRequest request,
        string pipelineStage,
        string stageStatus,
        string? traceId,
        CancellationToken cancellationToken,
        int? durationMs = null,
        int? nodesDeployed = null,
        int? nodesFailed = null,
        int? nodesTargeted = null,
        string? errorMessage = null,
        string? exceptionDetails = null)
    {
        if (_auditLogService == null)
        {
            return; // Audit logging is optional
        }

        try
        {
            var auditLog = new AuditLog
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                EventCategory = "Deployment",
                Severity = result == "Success" ? "Information" : (result == "Failure" ? "Error" : "Warning"),
                UserId = null, // Pipeline is system-initiated
                Username = "System",
                UserEmail = request.RequesterEmail,
                ResourceType = "DeploymentPipeline",
                ResourceId = request.ExecutionId.ToString(),
                Action = "ExecutePipeline",
                Result = result,
                Message = $"{eventType} for {request.Module.Name} v{request.Module.Version}",
                TraceId = traceId,
                SpanId = Activity.Current?.SpanId.ToString(),
                SourceIp = null, // Not applicable for pipeline execution
                UserAgent = null,
                CreatedAt = DateTime.UtcNow
            };

            var deploymentEvent = new DeploymentAuditEvent
            {
                DeploymentExecutionId = request.ExecutionId,
                ModuleName = request.Module.Name,
                ModuleVersion = request.Module.Version.ToString(),
                TargetEnvironment = request.TargetEnvironment.ToString(),
                DeploymentStrategy = null, // Set at stage level
                PipelineStage = pipelineStage,
                StageStatus = stageStatus,
                NodesTargeted = nodesTargeted,
                NodesDeployed = nodesDeployed,
                NodesFailed = nodesFailed,
                StartTime = null, // Set at stage level
                EndTime = null,
                DurationMs = durationMs,
                ErrorMessage = errorMessage,
                ExceptionDetails = exceptionDetails,
                RequesterEmail = request.RequesterEmail,
                CreatedAt = DateTime.UtcNow
            };

            await _auditLogService.LogDeploymentEventAsync(auditLog, deploymentEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the pipeline due to audit logging issues
            _logger.LogError(ex, "Failed to write audit log for deployment event {EventType}", eventType);
        }
    }

    /// <summary>
    /// Updates the deployment tracker with current pipeline state and notifies clients via SignalR.
    /// </summary>
    private async Task UpdatePipelineStateAsync(
        DeploymentRequest request,
        string status,
        string? currentStage,
        List<PipelineStageResult> stages)
    {
        var state = new PipelineExecutionState
        {
            ExecutionId = request.ExecutionId,
            Request = request,
            Status = status,
            CurrentStage = currentStage,
            Stages = stages.ToList(), // Create a copy
            StartTime = stages.FirstOrDefault()?.StartTime ?? DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        // Update deployment tracker if configured
        if (_deploymentTracker != null)
        {
            try
            {
                await _deploymentTracker.UpdatePipelineStateAsync(request.ExecutionId, state);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the pipeline due to state tracking issues
                _logger.LogError(ex, "Failed to update pipeline state for execution {ExecutionId}", request.ExecutionId);
            }
        }

        // Notify clients via SignalR if notifier is configured
        if (_deploymentNotifier != null)
        {
            try
            {
                await _deploymentNotifier.NotifyDeploymentStatusChanged(request.ExecutionId.ToString(), state);

                // Calculate progress percentage based on completed stages
                if (currentStage != null && stages.Any())
                {
                    int progress = CalculateProgress(status, stages);
                    await _deploymentNotifier.NotifyDeploymentProgress(
                        request.ExecutionId.ToString(),
                        currentStage,
                        progress);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the pipeline due to notification issues
                _logger.LogError(ex, "Failed to notify deployment status for execution {ExecutionId}", request.ExecutionId);
            }
        }
    }

    /// <summary>
    /// Calculates progress percentage based on pipeline status and completed stages.
    /// </summary>
    private int CalculateProgress(string status, List<PipelineStageResult> stages)
    {
        if (status == "Succeeded" || status == "Failed")
        {
            return 100;
        }

        if (!stages.Any())
        {
            return 0;
        }

        // Count completed stages (succeeded or failed)
        int completedStages = stages.Count(s =>
            s.Status == PipelineStageStatus.Succeeded ||
            s.Status == PipelineStageStatus.Failed);

        // Estimate total stages (typical pipeline has 7-10 stages depending on target environment)
        // Using a conservative estimate of 8 stages total
        const int estimatedTotalStages = 8;

        return Math.Min(100, (completedStages * 100) / estimatedTotalStages);
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}
