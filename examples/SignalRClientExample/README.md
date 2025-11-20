# SignalR C# Client Example

A complete .NET console application demonstrating real-time deployment monitoring using SignalR.

## Features

✅ **SignalR Connection Management**
- Automatic connection with retry logic
- Exponential backoff reconnection (0s, 2s, 10s, 30s)
- Graceful shutdown on Ctrl+C

✅ **Real-Time Event Handling**
- Deployment status changes
- Progress updates with visual progress bars
- Detailed deployment information display

✅ **Command-Line Configuration**
- Configurable API URL
- Monitor specific deployment or all deployments
- Debug logging support

✅ **Beautiful Console Output**
- Formatted deployment status with box drawing
- Visual progress bars (█░░░░)
- Color-coded messages (when terminal supports it)
- Timestamps for all events

## Prerequisites

- .NET 8.0 SDK or later
- HotSwap API running (default: http://localhost:5000)

## Building the Application

```bash
cd examples/SignalRClientExample
dotnet build
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Usage

### Basic Usage (Monitor All Deployments)

```bash
dotnet run
```

This connects to `http://localhost:5000` and monitors all deployments.

### Monitor Specific Deployment

```bash
dotnet run -- http://localhost:5000 d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f
```

### Custom API URL

```bash
dotnet run -- https://api.example.com
```

### Enable Debug Logging

```bash
dotnet run -- http://localhost:5000 "" debug
```

Or for a specific deployment with debug logging:

```bash
dotnet run -- http://localhost:5000 d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f debug
```

### Run Compiled Binary

After building, you can run the compiled executable:

```bash
# Linux/macOS
./bin/Debug/net8.0/SignalRClientExample

# Windows
.\bin\Debug\net8.0\SignalRClientExample.exe

# With arguments
./bin/Debug/net8.0/SignalRClientExample http://localhost:5000 <execution-id> debug
```

## Command-Line Arguments

| Argument | Position | Default | Description |
|----------|----------|---------|-------------|
| API URL | 1 | `http://localhost:5000` | The base URL of the HotSwap API |
| Execution ID | 2 | (empty = all) | Specific deployment ID to monitor |
| Log Level | 3 | `Information` | Set to `debug` for verbose logging |

## Output Examples

### Startup

```
===========================================
  HotSwap Deployment Monitor (SignalR)
===========================================

API URL: http://localhost:5000
Log Level: Information
Monitoring: All Deployments

[INFO] Connecting to SignalR hub: http://localhost:5000/deploymentHub
[SUCCESS] Connected to hub. Connection ID: abc123

[SUCCESS] Subscribed to all deployments

[INFO] Listening for deployment updates...
[INFO] Press Ctrl+C to exit.
```

### Deployment Status Changed

```
┌─────────────────────────────────────────────────────────────
│ [2025-11-20 12:30:45] DEPLOYMENT STATUS CHANGED
├─────────────────────────────────────────────────────────────
│ Execution ID: d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f
│ Status: Running
│ Updated At: 2025-11-20 12:30:45
│
│ Module: TestModule v1.0.0
│ Environment: Production
│ Requester: user@example.com
│ Current Stage: Verification
│ Completed Stages: 3 / 8
└─────────────────────────────────────────────────────────────
```

### Progress Update

```
[2025-11-20 12:31:00] PROGRESS UPDATE
  Deployment: d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f
  Stage: Verification
  Progress: [████████████░░░░░░░░] 60%
```

### Graceful Shutdown

```
^C

[SHUTDOWN] Disconnecting from SignalR hub...
[SHUTDOWN] Disconnected. Goodbye!
```

## Integration Examples

### Use in Script

```bash
#!/bin/bash
# monitor-deployment.sh

DEPLOYMENT_ID=$1
API_URL="http://localhost:5000"

if [ -z "$DEPLOYMENT_ID" ]; then
    echo "Usage: $0 <execution-id>"
    exit 1
fi

echo "Monitoring deployment: $DEPLOYMENT_ID"
dotnet run --project examples/SignalRClientExample -- $API_URL $DEPLOYMENT_ID
```

### Background Monitoring

```bash
# Run in background and log to file
dotnet run -- http://localhost:5000 > deployment.log 2>&1 &
MONITOR_PID=$!

# Later, stop monitoring
kill $MONITOR_PID
```

### Docker Container

Create a Dockerfile for the client:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SignalRClientExample.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SignalRClientExample.dll"]
```

Build and run:

```bash
docker build -t signalr-monitor .
docker run -it --rm signalr-monitor http://host.docker.internal:5000
```

## Advanced Usage

### Programmatic Integration

You can reference the SignalRClientExample project from other .NET applications:

```xml
<ItemGroup>
  <ProjectReference Include="../SignalRClientExample/SignalRClientExample.csproj" />
</ItemGroup>
```

Then use the classes:

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/deploymentHub")
    .Build();

connection.On<DeploymentStatusMessage>("DeploymentStatusChanged", message =>
{
    Console.WriteLine($"Deployment {message.ExecutionId}: {message.Status}");
});

await connection.StartAsync();
await connection.InvokeAsync("SubscribeToAllDeployments");

// Keep connection alive...
```

### Custom Event Handlers

Modify `RegisterEventHandlers()` to add custom logic:

```csharp
_connection.On<DeploymentStatusMessage>("DeploymentStatusChanged", message =>
{
    // Custom notification logic
    if (message.Status == "Failed")
    {
        SendEmailAlert(message);
        LogToDatabase(message);
    }

    // Original display logic
    Console.WriteLine($"Status: {message.Status}");
});
```

### Integration with Logging Framework

Replace console logging with structured logging:

```csharp
using Microsoft.Extensions.Logging;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/deployment.log")
    .CreateLogger();

// In HubConnectionBuilder
.ConfigureLogging(logging =>
{
    logging.AddSerilog();
    logging.SetMinimumLevel(LogLevel.Information);
})
```

## Testing

### Manual Testing

1. Start the API:
   ```bash
   cd src/HotSwap.Distributed.Api
   dotnet run
   ```

2. Run the client:
   ```bash
   cd examples/SignalRClientExample
   dotnet run
   ```

3. Trigger a deployment via API:
   ```bash
   curl -X POST http://localhost:5000/api/deployments \
     -H "Content-Type: application/json" \
     -d '{
       "module": {
         "name": "TestModule",
         "version": "1.0.0",
         "assemblyPath": "/path/to/module.dll",
         "packageUrl": "https://example.com/module.nupkg"
       },
       "targetEnvironment": "Development",
       "requesterEmail": "test@example.com"
     }'
   ```

4. Watch updates appear in the client console

### Automated Testing

Create a test script:

```csharp
using Xunit;

public class SignalRClientTests
{
    [Fact]
    public async Task CanConnectToHub()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/deploymentHub")
            .Build();

        await connection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync();
    }

    [Fact]
    public async Task CanSubscribeToDeployments()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/deploymentHub")
            .Build();

        await connection.StartAsync();

        // Should not throw
        await connection.InvokeAsync("SubscribeToAllDeployments");

        await connection.StopAsync();
    }
}
```

## Troubleshooting

### Connection Failed

**Error**: `Unable to connect to the remote server`

**Solutions**:
- Verify API is running: `curl http://localhost:5000/health`
- Check firewall settings
- Ensure correct API URL

### Subscription Failed

**Error**: `Failed to subscribe to deployment: Method not found`

**Solutions**:
- Verify hub method names match (case-sensitive)
- Ensure API version is compatible
- Check API logs for errors

### No Updates Received

**Problem**: Connected but no deployment updates

**Solutions**:
- Verify deployment is actually running
- Check execution ID is correct (valid GUID)
- Ensure deployment happens after subscription
- Check API SignalR configuration

### Reconnection Loop

**Problem**: Client keeps reconnecting

**Solutions**:
- Check API stability
- Review API logs for connection errors
- Verify network connectivity
- Increase reconnection delays

## Configuration

### Modify Reconnection Policy

Edit `Program.cs`:

```csharp
.WithAutomaticReconnect(new[] {
    TimeSpan.Zero,            // Immediate
    TimeSpan.FromSeconds(5),  // 5 seconds
    TimeSpan.FromSeconds(15), // 15 seconds
    TimeSpan.FromMinutes(1)   // 1 minute
})
```

### Change Progress Bar Style

Edit `GenerateProgressBar()`:

```csharp
// Percentage style
return $"{progress}% [{new string('#', filled)}{new string('-', empty)}]";

// Different characters
return $"[{new string('▓', filled)}{new string('░', empty)}]";

// Simple dots
return $"[{new string('●', filled)}{new string('○', empty)}]";
```

### Add Color Support

Use ANSI colors (Linux/macOS):

```csharp
const string GREEN = "\u001b[32m";
const string RED = "\u001b[31m";
const string RESET = "\u001b[0m";

Console.WriteLine($"{GREEN}[SUCCESS]{RESET} Connected");
Console.WriteLine($"{RED}[ERROR]{RESET} Failed");
```

## Project Structure

```
SignalRClientExample/
├── Program.cs                   # Main application logic
├── SignalRClientExample.csproj  # Project file with dependencies
└── README.md                    # This file
```

## Dependencies

- `Microsoft.AspNetCore.SignalR.Client` v10.0.0 - SignalR client library
- .NET 8.0 Runtime - Required to run the application

## Next Steps

- See [JavaScript client example](../signalr-client.html) for web integration
- Review [WebSocket Guide](../../WEBSOCKET_GUIDE.md) for complete documentation
- Check [DeploymentHub source](../../src/HotSwap.Distributed.Api/Hubs/DeploymentHub.cs)

## References

- [SignalR .NET Client Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/dotnet-client)
- [HubConnectionBuilder API](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.signalr.client.hubconnectionbuilder)
- [HotSwap API Documentation](../../src/HotSwap.Distributed.Api/README.md)
