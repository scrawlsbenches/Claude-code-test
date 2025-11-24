using System.Threading.Channels;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Orchestrator.Core;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// Background service for processing deployment requests with proper supervision.
/// Replaces fire-and-forget Task.Run pattern with queue-based processing.
/// </summary>
public class DeploymentOrchestrationService : BackgroundService
{
    private readonly Channel<DeploymentJob> _jobQueue;
    private readonly DistributedKernelOrchestrator _orchestrator;
    private readonly IDeploymentTracker _deploymentTracker;
    private readonly ILogger<DeploymentOrchestrationService> _logger;

    public DeploymentOrchestrationService(
        DistributedKernelOrchestrator orchestrator,
        IDeploymentTracker deploymentTracker,
        ILogger<DeploymentOrchestrationService> logger)
    {
        _orchestrator = orchestrator;
        _deploymentTracker = deploymentTracker;
        _logger = logger;

        // Create unbounded channel for job queue
        _jobQueue = Channel.CreateUnbounded<DeploymentJob>(new UnboundedChannelOptions
        {
            SingleReader = true,  // Only one background task processes jobs
            SingleWriter = false  // Multiple API requests can queue jobs
        });
    }

    /// <summary>
    /// Queues a deployment for background processing.
    /// </summary>
    /// <param name="request">Deployment request to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution ID of the queued deployment</returns>
    public async Task<Guid> QueueDeploymentAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Queuing deployment {ExecutionId} for {ModuleName} v{Version} to {Environment}",
            request.ExecutionId, request.Module.Name, request.Module.Version, request.TargetEnvironment);

        var job = new DeploymentJob(request);
        await _jobQueue.Writer.WriteAsync(job, cancellationToken);

        return request.ExecutionId;
    }

    /// <summary>
    /// Gets the count of pending jobs in the queue.
    /// </summary>
    public int GetPendingJobCount()
    {
        // Note: Channel doesn't expose Count, this is approximate
        // For exact count, would need additional tracking
        return _jobQueue.Reader.CanRead ? 1 : 0;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deployment orchestration service started");

        try
        {
            await foreach (var job in _jobQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation(
                        "Processing deployment {ExecutionId} for {ModuleName} v{Version}",
                        job.Request.ExecutionId, job.Request.Module.Name, job.Request.Module.Version);

                    // Execute deployment pipeline
                    var result = await _orchestrator.ExecuteDeploymentPipelineAsync(
                        job.Request,
                        stoppingToken);

                    // Store result
                    await _deploymentTracker.StoreResultAsync(job.Request.ExecutionId, result);
                    await _deploymentTracker.RemoveInProgressAsync(job.Request.ExecutionId);

                    _logger.LogInformation(
                        "Deployment {ExecutionId} completed: {Status}",
                        job.Request.ExecutionId, result.Success ? "Success" : "Failed");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown - re-queue job for processing on restart
                    _logger.LogInformation(
                        "Deployment {ExecutionId} cancelled during shutdown, re-queuing",
                        job.Request.ExecutionId);

                    // Try to re-queue without cancellation token
                    try
                    {
                        await _jobQueue.Writer.WriteAsync(job, CancellationToken.None);
                    }
                    catch (Exception requeueEx)
                    {
                        _logger.LogWarning(requeueEx,
                            "Failed to re-queue deployment {ExecutionId} during shutdown",
                            job.Request.ExecutionId);
                    }

                    // Exit loop on shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Deployment {ExecutionId} failed with exception",
                        job.Request.ExecutionId);

                    // Store failure
                    try
                    {
                        await _deploymentTracker.StoreFailureAsync(
                            job.Request.ExecutionId,
                            ex,
                            stoppingToken);

                        await _deploymentTracker.RemoveInProgressAsync(
                            job.Request.ExecutionId,
                            stoppingToken);
                    }
                    catch (Exception trackingEx)
                    {
                        _logger.LogError(trackingEx,
                            "Failed to store failure for deployment {ExecutionId}",
                            job.Request.ExecutionId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Deployment orchestration service crashed");
            throw;
        }
        finally
        {
            _logger.LogInformation("Deployment orchestration service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deployment orchestration service stopping...");

        // Complete the channel writer to stop accepting new jobs
        _jobQueue.Writer.Complete();

        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Represents a deployment job in the processing queue.
/// </summary>
public record DeploymentJob(DeploymentRequest Request)
{
    public DateTime QueuedAt { get; init; } = DateTime.UtcNow;
}
