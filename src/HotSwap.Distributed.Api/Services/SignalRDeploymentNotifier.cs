using Microsoft.AspNetCore.SignalR;
using HotSwap.Distributed.Api.Hubs;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Api.Services;

/// <summary>
/// SignalR-based implementation of deployment notification service.
/// Broadcasts deployment updates to subscribed clients in real-time.
/// </summary>
public class SignalRDeploymentNotifier : IDeploymentNotifier
{
    private readonly IHubContext<DeploymentHub> _hubContext;
    private readonly ILogger<SignalRDeploymentNotifier> _logger;

    /// <summary>
    /// Initializes a new instance of the SignalRDeploymentNotifier class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context for broadcasting messages.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public SignalRDeploymentNotifier(
        IHubContext<DeploymentHub> hubContext,
        ILogger<SignalRDeploymentNotifier> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task NotifyDeploymentStatusChanged(string executionId, PipelineExecutionState state)
    {
        // Validate inputs
        if (executionId == null)
        {
            throw new ArgumentNullException(nameof(executionId), "Execution ID cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new ArgumentException("Execution ID cannot be empty or whitespace.", nameof(executionId));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state), "Deployment state cannot be null.");
        }

        _logger.LogInformation("Broadcasting status update for deployment {ExecutionId}: {Status}",
            executionId, state.Status);

        var message = new
        {
            ExecutionId = executionId,
            Status = state.Status,
            UpdatedAt = DateTime.UtcNow,
            Details = state
        };

        // Send to deployment-specific group
        await _hubContext.Clients
            .Group($"deployment-{executionId}")
            .SendAsync("DeploymentStatusChanged", message);

        // Also broadcast to "all-deployments" group for dashboard monitoring
        await _hubContext.Clients
            .Group("all-deployments")
            .SendAsync("DeploymentStatusChanged", message);

        _logger.LogDebug("Status update broadcast completed for deployment {ExecutionId}", executionId);
    }

    /// <inheritdoc />
    public async Task NotifyDeploymentProgress(string executionId, string stage, int progress)
    {
        // Validate inputs
        if (executionId == null)
        {
            throw new ArgumentNullException(nameof(executionId), "Execution ID cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new ArgumentException("Execution ID cannot be empty or whitespace.", nameof(executionId));
        }

        if (stage == null)
        {
            throw new ArgumentNullException(nameof(stage), "Stage cannot be null.");
        }

        if (progress < 0 || progress > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progress), progress,
                "Progress must be between 0 and 100.");
        }

        _logger.LogInformation("Broadcasting progress update for deployment {ExecutionId}: {Stage} - {Progress}%",
            executionId, stage, progress);

        var message = new
        {
            ExecutionId = executionId,
            Stage = stage,
            Progress = progress,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients
            .Group($"deployment-{executionId}")
            .SendAsync("DeploymentProgress", message);

        _logger.LogDebug("Progress update broadcast completed for deployment {ExecutionId}", executionId);
    }
}
