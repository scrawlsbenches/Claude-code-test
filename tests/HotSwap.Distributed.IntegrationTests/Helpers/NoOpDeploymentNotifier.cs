using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;

namespace HotSwap.Distributed.IntegrationTests.Helpers;

/// <summary>
/// No-op implementation of IDeploymentNotifier for integration tests.
/// Does not perform any actual notifications, avoiding SignalR blocking issues.
/// </summary>
public class NoOpDeploymentNotifier : IDeploymentNotifier
{
    /// <inheritdoc />
    public Task NotifyDeploymentStatusChanged(string executionId, PipelineExecutionState state)
    {
        // No-op: Don't send notifications during integration tests
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task NotifyDeploymentProgress(string executionId, string stage, int progress)
    {
        // No-op: Don't send notifications during integration tests
        return Task.CompletedTask;
    }
}
