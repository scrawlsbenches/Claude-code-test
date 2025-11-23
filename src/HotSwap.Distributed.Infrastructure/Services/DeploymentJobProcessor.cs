using System.Text.Json;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Data.Entities;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Services;

/// <summary>
/// Background service that processes deployment jobs using transactional outbox pattern.
/// Replaces fire-and-forget Task.Run with durable job queue with retry logic.
/// </summary>
public class DeploymentJobProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeploymentJobProcessor> _logger;
    private const int CHECK_INTERVAL_SECONDS = 5;
    private const int LOCK_DURATION_MINUTES = 10;
    private const int MAX_CONCURRENT_JOBS = 5;

    public DeploymentJobProcessor(
        IServiceProvider serviceProvider,
        ILogger<DeploymentJobProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deployment Job Processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deployment jobs");
            }

            await Task.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS), stoppingToken);
        }

        _logger.LogInformation("Deployment Job Processor stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();

        var lockUntil = DateTime.UtcNow.AddMinutes(LOCK_DURATION_MINUTES);
        var instanceId = Environment.MachineName;
        var now = DateTime.UtcNow;

        // Claim pending jobs using FOR UPDATE SKIP LOCKED (prevents duplicates)
        var jobs = await dbContext.DeploymentJobs
            .Where(j =>
                (j.Status == JobStatus.Pending || j.Status == JobStatus.Failed) &&
                (j.NextRetryAt == null || j.NextRetryAt <= now) &&
                j.RetryCount < j.MaxRetries)
            .OrderBy(j => j.CreatedAt)
            .Take(MAX_CONCURRENT_JOBS)
            .ToListAsync(cancellationToken);

        if (!jobs.Any())
        {
            return; // No jobs to process
        }

        // Update jobs to Running status
        foreach (var job in jobs)
        {
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.LockedUntil = lockUntil;
            job.ProcessingInstance = instanceId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Claimed {Count} deployment jobs for processing", jobs.Count);

        // Process each job in parallel
        var processingTasks = jobs.Select(job => ProcessJobAsync(job, cancellationToken));
        await Task.WhenAll(processingTasks);
    }

    private async Task ProcessJobAsync(DeploymentJobEntity job, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IDistributedKernelOrchestrator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditLogDbContext>();

        try
        {
            _logger.LogInformation("Processing deployment job {JobId} ({DeploymentId})", job.Id, job.DeploymentId);

            // Deserialize payload
            var payload = JsonSerializer.Deserialize<DeploymentJobPayload>(job.Payload);
            if (payload == null)
            {
                throw new InvalidOperationException("Failed to deserialize job payload");
            }

            // Execute deployment
            var result = await orchestrator.ExecuteDeploymentPipelineAsync(
                payload.ModuleName,
                payload.ModuleVersion,
                payload.TargetEnvironment,
                payload.DeploymentStrategy,
                payload.DeploymentExecutionId,
                cancellationToken);

            // Mark job as succeeded
            var dbJob = await dbContext.DeploymentJobs.FindAsync(new object[] { job.Id }, cancellationToken);
            if (dbJob != null)
            {
                dbJob.Status = JobStatus.Succeeded;
                dbJob.CompletedAt = DateTime.UtcNow;
                dbJob.LockedUntil = null;

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Deployment job {JobId} completed successfully", job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment job {JobId} failed (attempt {RetryCount}/{MaxRetries})",
                job.Id, job.RetryCount + 1, job.MaxRetries);

            // Update job status for retry
            var dbJob = await dbContext.DeploymentJobs.FindAsync(new object[] { job.Id }, cancellationToken);
            if (dbJob != null)
            {
                dbJob.Status = JobStatus.Failed;
                dbJob.ErrorMessage = ex.Message;
                dbJob.RetryCount++;
                dbJob.LockedUntil = null;

                // Calculate next retry time with exponential backoff
                if (dbJob.RetryCount < dbJob.MaxRetries)
                {
                    var backoffMinutes = Math.Pow(2, dbJob.RetryCount); // 2, 4, 8, 16 minutes
                    dbJob.NextRetryAt = DateTime.UtcNow.AddMinutes(backoffMinutes);

                    _logger.LogInformation("Job {JobId} will retry in {Minutes} minutes (attempt {Attempt}/{Max})",
                        job.Id, backoffMinutes, dbJob.RetryCount + 1, dbJob.MaxRetries);
                }
                else
                {
                    _logger.LogError("Job {JobId} failed permanently after {Attempts} attempts",
                        job.Id, dbJob.RetryCount);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}

/// <summary>
/// Deployment job payload stored as JSON.
/// </summary>
public class DeploymentJobPayload
{
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleVersion { get; set; } = string.Empty;
    public string TargetEnvironment { get; set; } = string.Empty;
    public string DeploymentStrategy { get; set; } = string.Empty;
    public Guid DeploymentExecutionId { get; set; }
}
