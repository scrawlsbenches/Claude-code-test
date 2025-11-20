using Microsoft.AspNetCore.SignalR;

namespace HotSwap.Distributed.Api.Hubs;

/// <summary>
/// SignalR hub for real-time deployment updates.
/// Clients can subscribe to specific deployment executions to receive live status updates.
/// </summary>
public class DeploymentHub : Hub
{
    /// <summary>
    /// Subscribes the current connection to receive updates for a specific deployment.
    /// </summary>
    /// <param name="executionId">The unique identifier of the deployment execution to monitor.</param>
    /// <exception cref="ArgumentNullException">Thrown when executionId is null.</exception>
    /// <exception cref="ArgumentException">Thrown when executionId is empty or whitespace.</exception>
    public async Task SubscribeToDeployment(string executionId)
    {
        if (executionId == null)
        {
            throw new ArgumentNullException(nameof(executionId), "Execution ID cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new ArgumentException("Execution ID cannot be empty or whitespace.", nameof(executionId));
        }

        var groupName = $"deployment-{executionId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Unsubscribes the current connection from receiving updates for a specific deployment.
    /// </summary>
    /// <param name="executionId">The unique identifier of the deployment execution to stop monitoring.</param>
    /// <exception cref="ArgumentNullException">Thrown when executionId is null.</exception>
    /// <exception cref="ArgumentException">Thrown when executionId is empty or whitespace.</exception>
    public async Task UnsubscribeFromDeployment(string executionId)
    {
        if (executionId == null)
        {
            throw new ArgumentNullException(nameof(executionId), "Execution ID cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new ArgumentException("Execution ID cannot be empty or whitespace.", nameof(executionId));
        }

        var groupName = $"deployment-{executionId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Subscribes the current connection to receive updates for all deployments.
    /// </summary>
    public async Task SubscribeToAllDeployments()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-deployments");
    }

    /// <summary>
    /// Unsubscribes the current connection from receiving updates for all deployments.
    /// </summary>
    public async Task UnsubscribeFromAllDeployments()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-deployments");
    }

    /// <summary>
    /// Called when a new connection is established.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a connection is terminated.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
