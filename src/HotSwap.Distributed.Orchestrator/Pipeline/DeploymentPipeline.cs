using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Telemetry;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Strategies;
using Microsoft.Extensions.Logging;

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

    public DeploymentPipeline(
        ILogger<DeploymentPipeline> logger,
        IClusterRegistry clusterRegistry,
        IModuleVerifier moduleVerifier,
        TelemetryProvider telemetry,
        PipelineConfiguration config,
        Dictionary<EnvironmentType, IDeploymentStrategy> strategies)
    {
        _logger = logger;
        _clusterRegistry = clusterRegistry;
        _moduleVerifier = moduleVerifier;
        _telemetry = telemetry;
        _config = config;
        _strategies = strategies;
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

        try
        {
            _logger.LogInformation("Starting deployment pipeline for {ModuleName} v{Version} (Execution: {ExecutionId})",
                request.Module.Name, request.Module.Version, request.ExecutionId);

            // Stage 1: Build
            var buildStage = await ExecuteBuildStageAsync(request, cancellationToken);
            result.StageResults.Add(buildStage);

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

            if (securityStage.Status != PipelineStageStatus.Succeeded)
            {
                result.Success = false;
                result.Message = "Pipeline failed at Security Scan stage";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Stage 4-7: Deploy to environments based on target
            var deploymentStages = await ExecuteDeploymentStagesAsync(request, cancellationToken);
            result.StageResults.AddRange(deploymentStages);

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

            result.Success = validationStage.Status == PipelineStageStatus.Succeeded;
            result.Message = result.Success
                ? "Pipeline completed successfully"
                : "Pipeline failed at Validation stage";
            result.EndTime = DateTime.UtcNow;

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
        CancellationToken cancellationToken)
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

    public void Dispose()
    {
        // Cleanup resources
    }
}
