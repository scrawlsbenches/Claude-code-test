# WebSocket/SignalR Real-Time Updates Guide

**Last Updated**: 2025-11-20

This guide provides comprehensive documentation for the SignalR-based real-time deployment monitoring system in the HotSwap Distributed Orchestrator.

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Setup and Configuration](#setup-and-configuration)
4. [Hub Implementation](#hub-implementation)
5. [Notifier Service](#notifier-service)
6. [Event Types](#event-types)
7. [Client Implementation](#client-implementation)
8. [Subscription Management](#subscription-management)
9. [Testing](#testing)
10. [Deployment](#deployment)
11. [Troubleshooting](#troubleshooting)
12. [Best Practices](#best-practices)
13. [References](#references)

---

## Overview

### What is SignalR?

SignalR is a library for ASP.NET that enables real-time web functionality. It uses WebSockets when available and automatically falls back to other transport mechanisms (Server-Sent Events, Long Polling) when WebSockets aren't supported.

### Why SignalR for Deployment Monitoring?

Traditional HTTP polling is inefficient for real-time updates:
- **Wasteful**: Clients poll repeatedly even when nothing changes
- **Latency**: Updates delayed by polling interval
- **Resource intensive**: Unnecessary server load from empty polls

SignalR provides:
- **Instant updates**: Server pushes changes immediately
- **Efficient**: One persistent connection instead of repeated polls
- **Scalable**: Optimized for many concurrent connections
- **Automatic reconnection**: Resilient to network issues

### Use Cases

- **Real-time deployment monitoring**: Track deployment progress as it happens
- **Live status updates**: See status changes instantly (Running → Succeeded/Failed)
- **Progress tracking**: Visual progress bars updated in real-time
- **Multi-deployment monitoring**: Monitor all deployments simultaneously
- **Audit dashboards**: Live operational visibility

---

## Architecture

### Component Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      SignalR Architecture                    │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐                    ┌──────────────────┐   │
│  │   Clients    │◄───WebSocket──────►│ DeploymentHub    │   │
│  │  (JS/C#)     │                    │   (SignalR)      │   │
│  └──────────────┘                    └──────────────────┘   │
│                                              ▲                │
│                                              │                │
│                                              │ IHubContext    │
│  ┌──────────────────────────────────────────┴──────┐         │
│  │  SignalRDeploymentNotifier (IDeploymentNotifier)│         │
│  └──────────────────────────────────────────┬──────┘         │
│                                              │                │
│                                              │                │
│  ┌──────────────────────────────────────────▼──────┐         │
│  │           DeploymentPipeline                    │         │
│  │  (Orchestrates deployment stages)               │         │
│  └─────────────────────────────────────────────────┘         │
│                                                               │
└───────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Client Connects**: Establishes WebSocket connection to `/deploymentHub`
2. **Client Subscribes**: Calls `SubscribeToDeployment(executionId)` or `SubscribeToAllDeployments()`
3. **Hub Adds to Group**: Client added to SignalR group for targeted broadcasting
4. **Pipeline Execution**: Deployment pipeline executes stages
5. **Notifier Broadcasts**: SignalRDeploymentNotifier sends events to subscribed groups
6. **Client Receives**: Clients receive events and update UI

### Key Components

1. **DeploymentHub** (`src/HotSwap.Distributed.Api/Hubs/DeploymentHub.cs`)
   - SignalR hub for client connections
   - Manages subscription groups
   - Provides hub methods for subscribe/unsubscribe

2. **SignalRDeploymentNotifier** (`src/HotSwap.Distributed.Api/Services/SignalRDeploymentNotifier.cs`)
   - Implements `IDeploymentNotifier` interface
   - Broadcasts deployment events to subscribers
   - Uses `IHubContext<DeploymentHub>` to send messages

3. **IDeploymentNotifier Interface** (`src/HotSwap.Distributed.Infrastructure/Interfaces/IDeploymentNotifier.cs`)
   - Abstraction for deployment notifications
   - Enables loose coupling between layers
   - Prevents circular dependencies

4. **DeploymentPipeline Integration** (`src/HotSwap.Distributed.Orchestrator/Pipeline/DeploymentPipeline.cs`)
   - Calls notifier during pipeline execution
   - Sends status changes and progress updates
   - Non-blocking (failures don't stop pipeline)

---

## Setup and Configuration

### 1. Add SignalR Package

```bash
dotnet add package Microsoft.AspNetCore.SignalR --version 1.1.0
```

### 2. Register Services

In `Program.cs`:

```csharp
// Add SignalR service
builder.Services.AddSignalR();

// Register deployment notification service
builder.Services.AddSingleton<IDeploymentNotifier, SignalRDeploymentNotifier>();

// ... other service registrations

// Map SignalR hub
app.MapHub<DeploymentHub>("/deploymentHub");
```

### 3. Configure CORS (if needed)

For cross-origin requests:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:8080") // Client URL
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

app.UseCors("SignalRPolicy");
```

### 4. Configure Hub Options (optional)

```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Development only
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
});
```

---

## Hub Implementation

### DeploymentHub Overview

The `DeploymentHub` provides client-callable methods for subscribing to deployment updates.

**File**: `src/HotSwap.Distributed.Api/Hubs/DeploymentHub.cs`

### Hub Methods

#### 1. SubscribeToDeployment

Subscribe to updates for a specific deployment.

```csharp
/// <summary>
/// Subscribes the current connection to receive updates for a specific deployment.
/// </summary>
/// <param name="executionId">The unique identifier of the deployment execution to monitor.</param>
public async Task SubscribeToDeployment(string executionId)
{
    if (executionId == null)
        throw new ArgumentNullException(nameof(executionId), "Execution ID cannot be null.");

    if (string.IsNullOrWhiteSpace(executionId))
        throw new ArgumentException("Execution ID cannot be empty or whitespace.", nameof(executionId));

    var groupName = $"deployment-{executionId}";
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
}
```

**Usage (JavaScript)**:
```javascript
await connection.invoke("SubscribeToDeployment", "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f");
```

**Usage (C#)**:
```csharp
await connection.InvokeAsync("SubscribeToDeployment", "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f");
```

#### 2. UnsubscribeFromDeployment

Unsubscribe from deployment updates.

```csharp
/// <summary>
/// Unsubscribes the current connection from receiving updates for a specific deployment.
/// </summary>
/// <param name="executionId">The unique identifier of the deployment execution to stop monitoring.</param>
public async Task UnsubscribeFromDeployment(string executionId)
{
    if (executionId == null)
        throw new ArgumentNullException(nameof(executionId), "Execution ID cannot be null.");

    if (string.IsNullOrWhiteSpace(executionId))
        throw new ArgumentException("Execution ID cannot be empty or whitespace.", nameof(executionId));

    var groupName = $"deployment-{executionId}";
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
}
```

#### 3. SubscribeToAllDeployments

Subscribe to updates for all deployments (monitoring dashboard).

```csharp
/// <summary>
/// Subscribes the current connection to receive updates for all deployments.
/// </summary>
public async Task SubscribeToAllDeployments()
{
    await Groups.AddToGroupAsync(Context.ConnectionId, "all-deployments");
}
```

**Usage**:
```javascript
await connection.invoke("SubscribeToAllDeployments");
```

#### 4. UnsubscribeFromAllDeployments

Unsubscribe from all deployment updates.

```csharp
/// <summary>
/// Unsubscribes the current connection from receiving updates for all deployments.
/// </summary>
public async Task UnsubscribeFromAllDeployments()
{
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-deployments");
}
```

### Connection Lifecycle Events

Override these methods for custom connection handling:

```csharp
public override async Task OnConnectedAsync()
{
    // Log connection, initialize user state, etc.
    await base.OnConnectedAsync();
}

public override async Task OnDisconnectedAsync(Exception? exception)
{
    // Cleanup, log disconnection, etc.
    await base.OnDisconnectedAsync(exception);
}
```

---

## Notifier Service

### SignalRDeploymentNotifier Overview

The `SignalRDeploymentNotifier` broadcasts deployment events to subscribed clients.

**File**: `src/HotSwap.Distributed.Api/Services/SignalRDeploymentNotifier.cs`

### Implementation

```csharp
public class SignalRDeploymentNotifier : IDeploymentNotifier
{
    private readonly IHubContext<DeploymentHub> _hubContext;
    private readonly ILogger<SignalRDeploymentNotifier> _logger;

    public SignalRDeploymentNotifier(
        IHubContext<DeploymentHub> hubContext,
        ILogger<SignalRDeploymentNotifier> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyDeploymentStatusChanged(string executionId, PipelineExecutionState state)
    {
        // Validation...

        var message = new
        {
            ExecutionId = executionId,
            Status = state.Status,
            UpdatedAt = state.LastUpdated,
            Details = state
        };

        // Send to specific deployment group
        await _hubContext.Clients.Group($"deployment-{executionId}")
            .SendAsync("DeploymentStatusChanged", message);

        // Also send to "all deployments" group (dashboards)
        await _hubContext.Clients.Group("all-deployments")
            .SendAsync("DeploymentStatusChanged", message);
    }

    public async Task NotifyDeploymentProgress(string executionId, string stage, int progress)
    {
        // Validation...

        var message = new
        {
            ExecutionId = executionId,
            Stage = stage,
            Progress = progress,
            Timestamp = DateTime.UtcNow
        };

        // Send only to specific deployment group
        await _hubContext.Clients.Group($"deployment-{executionId}")
            .SendAsync("DeploymentProgress", message);
    }
}
```

### Broadcasting Strategy

- **DeploymentStatusChanged**: Sent to both `deployment-{id}` and `all-deployments` groups
- **DeploymentProgress**: Sent only to `deployment-{id}` group (too frequent for dashboard)

### Error Handling

The notifier uses try-catch blocks to prevent SignalR failures from breaking the deployment pipeline:

```csharp
if (_deploymentNotifier != null)
{
    try
    {
        await _deploymentNotifier.NotifyDeploymentStatusChanged(executionId, state);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to notify deployment status for execution {ExecutionId}", executionId);
        // Pipeline continues despite notification failure
    }
}
```

---

## Event Types

### 1. DeploymentStatusChanged

Triggered when deployment status changes (e.g., Running → Succeeded).

**Event Name**: `"DeploymentStatusChanged"`

**Payload**:
```json
{
  "ExecutionId": "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f",
  "Status": "Running",
  "UpdatedAt": "2025-11-20T12:00:00Z",
  "Details": {
    "ExecutionId": "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f",
    "Request": {
      "Module": {
        "Name": "TestModule",
        "Version": "1.0.0"
      },
      "TargetEnvironment": "Production",
      "RequesterEmail": "user@example.com"
    },
    "Status": "Running",
    "CurrentStage": "Verification",
    "Stages": [
      {
        "StageName": "Validation",
        "Status": "Succeeded",
        "StartTime": "2025-11-20T12:00:00Z",
        "EndTime": "2025-11-20T12:00:05Z"
      }
    ],
    "StartTime": "2025-11-20T12:00:00Z",
    "LastUpdated": "2025-11-20T12:00:10Z"
  }
}
```

**Trigger Conditions**:
- Deployment starts
- Deployment completes (success or failure)
- Deployment status changes

**Target Groups**:
- `deployment-{executionId}` - Specific deployment subscribers
- `all-deployments` - Dashboard subscribers

### 2. DeploymentProgress

Triggered when deployment progress updates within a stage.

**Event Name**: `"DeploymentProgress"`

**Payload**:
```json
{
  "ExecutionId": "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f",
  "Stage": "Verification",
  "Progress": 75,
  "Timestamp": "2025-11-20T12:00:10Z"
}
```

**Trigger Conditions**:
- Stage completes (progress calculation)
- Manual progress updates (if implemented)

**Target Groups**:
- `deployment-{executionId}` - Specific deployment subscribers only

**Progress Calculation**:
```csharp
private int CalculateProgress(string status, List<PipelineStageResult> stages)
{
    if (status == "Succeeded" || status == "Failed")
        return 100;

    if (!stages.Any())
        return 0;

    int completedStages = stages.Count(s =>
        s.Status == PipelineStageStatus.Succeeded ||
        s.Status == PipelineStageStatus.Failed);

    const int estimatedTotalStages = 8;
    return Math.Min(100, (completedStages * 100) / estimatedTotalStages);
}
```

---

## Client Implementation

### JavaScript Client

**File**: `examples/signalr-client.html`

#### Setup

```html
<!-- Include SignalR client library -->
<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
```

#### Connection

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/deploymentHub")
    .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
            if (retryContext.previousRetryCount === 0) return 0;
            if (retryContext.previousRetryCount === 1) return 2000;
            if (retryContext.previousRetryCount === 2) return 10000;
            return 30000;
        }
    })
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Register event handlers
connection.on("DeploymentStatusChanged", message => {
    console.log("Status changed:", message.Status);
    // Update UI...
});

connection.on("DeploymentProgress", message => {
    console.log("Progress:", message.Progress + "%");
    // Update progress bar...
});

// Start connection
await connection.start();
console.log("Connected. Connection ID:", connection.connectionId);

// Subscribe to deployment
await connection.invoke("SubscribeToDeployment", executionId);
```

#### Lifecycle Events

```javascript
connection.onreconnecting(error => {
    console.warn("Reconnecting...", error);
    // Show reconnecting UI
});

connection.onreconnected(connectionId => {
    console.log("Reconnected. New connection ID:", connectionId);
    // Re-subscribe if needed
});

connection.onclose(error => {
    console.error("Connection closed", error);
    // Show disconnected UI
});
```

### C# Client

**File**: `examples/SignalRClientExample/Program.cs`

#### Setup

```bash
dotnet add package Microsoft.AspNetCore.SignalR.Client
dotnet add package Microsoft.Extensions.Logging.Console
```

#### Connection

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/deploymentHub")
    .WithAutomaticReconnect(new[] {
        TimeSpan.Zero,           // 1st retry: immediate
        TimeSpan.FromSeconds(2), // 2nd retry: 2 seconds
        TimeSpan.FromSeconds(10), // 3rd retry: 10 seconds
        TimeSpan.FromSeconds(30)  // 4th+ retry: 30 seconds
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .Build();

// Register event handlers
connection.On<DeploymentStatusMessage>("DeploymentStatusChanged", message =>
{
    Console.WriteLine($"Status: {message.Status}");
});

connection.On<DeploymentProgressMessage>("DeploymentProgress", message =>
{
    Console.WriteLine($"Progress: {message.Progress}%");
});

// Start connection
await connection.StartAsync();

// Subscribe to deployment
await connection.InvokeAsync("SubscribeToDeployment", executionId);
```

#### Message Models

```csharp
public class DeploymentStatusMessage
{
    public string ExecutionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public DeploymentDetails? Details { get; set; }
}

public class DeploymentProgressMessage
{
    public string ExecutionId { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public int Progress { get; set; }
    public DateTime Timestamp { get; set; }
}
```

---

## Subscription Management

### Subscription Groups

SignalR uses groups to organize subscriptions:

| Group Name | Purpose | Receives |
|-----------|---------|----------|
| `deployment-{executionId}` | Specific deployment | Both status and progress |
| `all-deployments` | All deployments (dashboard) | Status only (not progress) |

### Subscription Lifecycle

1. **Connect**: Client establishes WebSocket connection
2. **Subscribe**: Client joins group(s) via hub methods
3. **Receive**: Client receives events targeted at its groups
4. **Unsubscribe**: Client leaves group(s)
5. **Disconnect**: All group memberships automatically removed

### Multiple Subscriptions

Clients can subscribe to multiple deployments:

```javascript
// Subscribe to multiple deployments
await connection.invoke("SubscribeToDeployment", "deployment-1");
await connection.invoke("SubscribeToDeployment", "deployment-2");

// Also subscribe to all deployments
await connection.invoke("SubscribeToAllDeployments");
```

### Automatic Cleanup

When a client disconnects, SignalR automatically:
- Removes client from all groups
- Cleans up connection resources
- Stops sending events to that client

---

## Testing

### Unit Tests

**File**: `tests/HotSwap.Distributed.Tests/Api/Hubs/DeploymentHubTests.cs`

```csharp
[Fact]
public async Task SubscribeToDeployment_WithValidExecutionId_AddsConnectionToGroup()
{
    // Arrange
    var executionId = "test-execution-123";
    var connectionId = "connection-456";
    _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);

    // Act
    await _hub.SubscribeToDeployment(executionId);

    // Assert
    _mockGroups.Verify(
        x => x.AddToGroupAsync(connectionId, $"deployment-{executionId}", default),
        Times.Once);
}
```

**Total Tests**: 26 SignalR tests
- 13 DeploymentHub tests
- 13 SignalRDeploymentNotifier tests

### Integration Tests

Test full end-to-end flow:

```csharp
[Fact]
public async Task FullDeploymentFlow_SendsRealTimeUpdates()
{
    // Arrange
    var client = CreateSignalRTestClient();
    await client.ConnectAsync();

    var statusReceived = new TaskCompletionSource<bool>();
    client.On("DeploymentStatusChanged", () => statusReceived.SetResult(true));

    await client.InvokeAsync("SubscribeToDeployment", testExecutionId);

    // Act
    await TriggerDeployment(testExecutionId);

    // Assert
    var received = await statusReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.True(received);
}
```

### Manual Testing

1. Start API:
   ```bash
   dotnet run --project src/HotSwap.Distributed.Api
   ```

2. Open JavaScript client:
   ```bash
   open examples/signalr-client.html
   ```

3. Trigger deployment via API:
   ```bash
   curl -X POST http://localhost:5000/api/deployments \
     -H "Content-Type: application/json" \
     -d '{"module": {...}, "targetEnvironment": "Development"}'
   ```

4. Observe real-time updates in client UI

---

## Deployment

### Production Configuration

#### 1. Use HTTPS

```csharp
var hubUrl = "https://api.production.com/deploymentHub";
```

#### 2. Authentication

Add JWT bearer token to connection:

**JavaScript**:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
        accessTokenFactory: () => getAuthToken()
    })
    .build();
```

**C#**:
```csharp
var connection = new HubConnectionBuilder()
    .WithUrl(hubUrl, options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(GetAuthToken());
    })
    .Build();
```

#### 3. Configure Hub Authorization

```csharp
[Authorize] // Require authentication
public class DeploymentHub : Hub
{
    public async Task SubscribeToDeployment(string executionId)
    {
        // Verify user has permission to monitor this deployment
        if (!await CanAccessDeployment(Context.User, executionId))
            throw new UnauthorizedAccessException();

        await Groups.AddToGroupAsync(Context.ConnectionId, $"deployment-{executionId}");
    }
}
```

#### 4. Scale Out with Azure SignalR Service

For high-traffic scenarios:

```bash
dotnet add package Microsoft.Azure.SignalR
```

```csharp
builder.Services.AddSignalR().AddAzureSignalR(
    builder.Configuration["Azure:SignalR:ConnectionString"]);
```

#### 5. Configure CORS for Production

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
            "https://app.production.com",
            "https://dashboard.production.com"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});
```

---

## Troubleshooting

### Connection Issues

#### Problem: "Failed to connect to hub"

**Possible Causes**:
- API not running
- Wrong URL
- CORS issues
- Firewall blocking WebSockets

**Solutions**:
```bash
# Verify API is running
curl http://localhost:5000/health

# Check SignalR endpoint
curl http://localhost:5000/deploymentHub/negotiate

# Check browser console for CORS errors
```

#### Problem: "Connection keeps reconnecting"

**Possible Causes**:
- Server crashes
- Network instability
- Idle timeout

**Solutions**:
```csharp
// Increase timeouts
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
});
```

### Subscription Issues

#### Problem: "Not receiving updates after subscription"

**Solutions**:
1. Verify subscription succeeded:
   ```javascript
   try {
       await connection.invoke("SubscribeToDeployment", executionId);
       console.log("Subscribed successfully");
   } catch (error) {
       console.error("Subscription failed:", error);
   }
   ```

2. Verify execution ID is correct (valid GUID format)

3. Ensure deployment happens AFTER subscription

4. Check SignalR group membership (server logs)

#### Problem: "Receiving duplicate events"

**Cause**: Subscribed multiple times

**Solution**: Unsubscribe before re-subscribing:
```javascript
await connection.invoke("UnsubscribeFromDeployment", executionId);
await connection.invoke("SubscribeToDeployment", executionId);
```

### Performance Issues

#### Problem: "Slow event delivery"

**Solutions**:
- Check server CPU/memory usage
- Verify network latency
- Consider Azure SignalR Service for scale-out
- Reduce event frequency (e.g., progress updates)

#### Problem: "Too many connections"

**Solutions**:
- Implement connection pooling
- Use `SubscribeToAllDeployments()` for dashboards instead of individual subscriptions
- Set `MaximumReceiveMessageSize` appropriately

---

## Best Practices

### 1. Error Handling

Always wrap SignalR operations in try-catch:

```javascript
try {
    await connection.start();
    await connection.invoke("SubscribeToDeployment", executionId);
} catch (error) {
    console.error("SignalR error:", error);
    // Show user-friendly error message
}
```

### 2. Reconnection Strategy

Use exponential backoff for reconnection:

```javascript
.withAutomaticReconnect({
    nextRetryDelayInMilliseconds: retryContext => {
        // 0s, 2s, 10s, 30s, 30s, ...
        if (retryContext.previousRetryCount === 0) return 0;
        if (retryContext.previousRetryCount === 1) return 2000;
        if (retryContext.previousRetryCount === 2) return 10000;
        return 30000;
    }
})
```

### 3. Event Throttling

Limit UI updates to avoid overwhelming the browser:

```javascript
let lastUpdate = 0;
const throttleMs = 100; // Max 10 updates/second

connection.on("DeploymentProgress", message => {
    const now = Date.now();
    if (now - lastUpdate < throttleMs) return;

    updateProgressBar(message.Progress);
    lastUpdate = now;
});
```

### 4. Graceful Shutdown

Clean up connections on page unload:

```javascript
window.addEventListener("beforeunload", async () => {
    if (connection) {
        await connection.stop();
    }
});
```

### 5. Logging

Enable appropriate logging level:

**Development**:
```csharp
.ConfigureLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Debug); // Verbose
    logging.AddConsole();
})
```

**Production**:
```csharp
.ConfigureLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Warning); // Errors only
    logging.AddConsole();
})
```

### 6. Security

- **Always use HTTPS** in production
- **Authenticate connections** with JWT tokens
- **Authorize subscriptions** (verify user can access deployment)
- **Validate input** in hub methods
- **Rate limit** subscriptions to prevent abuse

### 7. Monitoring

Track SignalR metrics:
- Connection count
- Message throughput
- Reconnection frequency
- Error rate
- Latency

---

## References

### Official Documentation

- [ASP.NET Core SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [SignalR JavaScript Client](https://docs.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
- [SignalR .NET Client](https://docs.microsoft.com/en-us/aspnet/core/signalr/dotnet-client)
- [SignalR Hub API](https://docs.microsoft.com/en-us/aspnet/core/signalr/hubs)

### HotSwap Project Files

- [DeploymentHub.cs](src/HotSwap.Distributed.Api/Hubs/DeploymentHub.cs)
- [SignalRDeploymentNotifier.cs](src/HotSwap.Distributed.Api/Services/SignalRDeploymentNotifier.cs)
- [IDeploymentNotifier.cs](src/HotSwap.Distributed.Infrastructure/Interfaces/IDeploymentNotifier.cs)
- [DeploymentPipeline.cs](src/HotSwap.Distributed.Orchestrator/Pipeline/DeploymentPipeline.cs)
- [DeploymentHubTests.cs](tests/HotSwap.Distributed.Tests/Api/Hubs/DeploymentHubTests.cs)
- [SignalRDeploymentNotifierTests.cs](tests/HotSwap.Distributed.Tests/Api/Services/SignalRDeploymentNotifierTests.cs)
- [JavaScript Client Example](examples/signalr-client.html)
- [C# Client Example](examples/SignalRClientExample/Program.cs)

### Related Guides

- [JavaScript Client README](examples/SIGNALR_CLIENT_README.md)
- [C# Client README](examples/SignalRClientExample/README.md)

---

## Changelog

### 2025-11-20
- Initial documentation creation
- Covers SignalR implementation in HotSwap Distributed Orchestrator
- Includes JavaScript and C# client examples
- 26 SignalR tests documented
- Production deployment guidance included
