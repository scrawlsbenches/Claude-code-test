# SignalR JavaScript Client Example

This directory contains a complete JavaScript/HTML example demonstrating real-time deployment monitoring using SignalR.

## Files

- **signalr-client.html** - Complete HTML/JavaScript client for monitoring deployments in real-time

## Features

The SignalR client example demonstrates:

✅ **Connection Management**
- Connect/disconnect from SignalR hub
- Automatic reconnection with exponential backoff
- Connection status indicators

✅ **Subscription Management**
- Subscribe to specific deployment by execution ID
- Subscribe to all deployments
- Multiple subscription support

✅ **Real-Time Updates**
- Deployment status changes
- Progress updates with percentage
- Stage transitions

✅ **User Interface**
- Live deployment list with status badges
- Progress bars showing deployment completion
- Detailed deployment information
- Event log with timestamps
- Responsive design

## Usage

### Option 1: Open Directly in Browser

1. Ensure the HotSwap API is running:
   ```bash
   cd src/HotSwap.Distributed.Api
   dotnet run
   ```

2. Open `signalr-client.html` in your web browser:
   ```bash
   # Linux/macOS
   open examples/signalr-client.html

   # Windows
   start examples/signalr-client.html
   ```

3. Click "Connect" to establish SignalR connection

4. Subscribe to deployments:
   - **Specific deployment**: Enter execution ID and click "Subscribe"
   - **All deployments**: Click "Subscribe to All"

### Option 2: Serve via HTTP Server

For better CORS handling, serve the file via HTTP server:

```bash
# Using Python 3
cd examples
python3 -m http.server 8080

# Then open: http://localhost:8080/signalr-client.html
```

Or using Node.js:

```bash
# Install http-server globally
npm install -g http-server

# Serve examples directory
cd examples
http-server -p 8080

# Then open: http://localhost:8080/signalr-client.html
```

## Configuration

### API URL

Default: `http://localhost:5000`

To connect to a different API instance, update the "API URL" field before connecting.

### Execution ID Format

Execution IDs are GUIDs in the format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`

Example: `d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f`

## SignalR Hub Events

The client listens to these SignalR events:

### DeploymentStatusChanged

Triggered when a deployment's status changes (Running → Succeeded/Failed).

**Payload:**
```json
{
  "ExecutionId": "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f",
  "Status": "Running",
  "UpdatedAt": "2025-11-20T12:00:00Z",
  "Details": {
    "ExecutionId": "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f",
    "Request": {
      "Module": {
        "Name": "MyModule",
        "Version": "1.0.0"
      },
      "TargetEnvironment": "Production",
      "RequesterEmail": "user@example.com"
    },
    "Status": "Running",
    "CurrentStage": "Verification",
    "Stages": [...]
  }
}
```

### DeploymentProgress

Triggered when deployment progress updates.

**Payload:**
```json
{
  "ExecutionId": "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f",
  "Stage": "Verification",
  "Progress": 75,
  "Timestamp": "2025-11-20T12:00:00Z"
}
```

## SignalR Hub Methods

The client can invoke these hub methods:

### SubscribeToDeployment(string executionId)

Subscribe to updates for a specific deployment.

```javascript
await connection.invoke("SubscribeToDeployment", "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f");
```

### SubscribeToAllDeployments()

Subscribe to updates for all deployments.

```javascript
await connection.invoke("SubscribeToAllDeployments");
```

### UnsubscribeFromDeployment(string executionId)

Unsubscribe from a specific deployment (can be added to UI as enhancement).

```javascript
await connection.invoke("UnsubscribeFromDeployment", "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f");
```

## Connection Lifecycle

The client handles these connection states:

1. **Disconnected** - Initial state, no connection
2. **Connecting** - Attempting to establish connection
3. **Connected** - Active connection, can receive updates
4. **Reconnecting** - Connection lost, attempting automatic reconnection
5. **Disconnected** - Connection closed

### Automatic Reconnection

The client uses exponential backoff for reconnection attempts:
- 1st retry: Immediate
- 2nd retry: 2 seconds
- 3rd retry: 10 seconds
- 4th+ retry: 30 seconds

## Testing the Client

### 1. Start the API

```bash
cd src/HotSwap.Distributed.Api
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 2. Create a Test Deployment

Using curl:

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

Response:
```json
{
  "executionId": "d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f",
  "status": "Accepted",
  "message": "Deployment request accepted and queued for processing"
}
```

### 3. Monitor in Client

1. Copy the `executionId` from the response
2. Open `signalr-client.html` in browser
3. Click "Connect"
4. Paste execution ID and click "Subscribe"
5. Watch real-time updates appear

## Troubleshooting

### Connection Fails

**Error**: "Failed: Error: Failed to complete negotiation with the server"

**Solutions:**
- Ensure API is running (`dotnet run` in Api project)
- Check API URL is correct (default: `http://localhost:5000`)
- Verify no CORS issues (API should allow SignalR connections)
- Check browser console for detailed error messages

### No Updates Received

**Problem**: Connected but not receiving deployment updates

**Solutions:**
- Ensure you subscribed to deployment (check event log)
- Verify execution ID is correct (must be valid GUID)
- Check deployment is actually running (not completed before subscription)
- Look for errors in browser console

### Reconnection Issues

**Problem**: Client keeps reconnecting

**Solutions:**
- Check API is stable and not crashing
- Review API logs for SignalR errors
- Verify network connectivity
- Check for firewall/proxy blocking WebSocket traffic

## Browser Compatibility

The SignalR client works with:
- ✅ Chrome 90+
- ✅ Firefox 88+
- ✅ Edge 90+
- ✅ Safari 14+
- ⚠️ IE 11 (requires SignalR polyfills)

## Advanced Usage

### Custom Event Handlers

Add custom handlers for events:

```javascript
connection.on("DeploymentStatusChanged", (message) => {
    // Custom logic for status changes
    if (message.Status === "Failed") {
        sendNotification("Deployment failed!");
    }
});
```

### Programmatic Subscription

Subscribe to deployments programmatically:

```javascript
async function monitorDeployment(executionId) {
    await connection.invoke("SubscribeToDeployment", executionId);

    return new Promise((resolve, reject) => {
        const handler = (message) => {
            if (message.ExecutionId === executionId) {
                if (message.Status === "Succeeded" || message.Status === "Failed") {
                    connection.off("DeploymentStatusChanged", handler);
                    resolve(message);
                }
            }
        };

        connection.on("DeploymentStatusChanged", handler);

        // Timeout after 5 minutes
        setTimeout(() => {
            connection.off("DeploymentStatusChanged", handler);
            reject(new Error("Deployment timeout"));
        }, 300000);
    });
}

// Usage
try {
    const result = await monitorDeployment("d355c24f-8b11-4c5a-a4e7-1a2b3c4d5e6f");
    console.log("Deployment completed:", result.Status);
} catch (error) {
    console.error("Deployment monitoring failed:", error);
}
```

### Multiple Connections

Create multiple connections for different purposes:

```javascript
// Connection for deployments
const deploymentConnection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/deploymentHub")
    .build();

// Connection for other events (future enhancement)
// const eventConnection = new signalR.HubConnectionBuilder()
//     .withUrl("http://localhost:5000/eventHub")
//     .build();
```

## Security Considerations

For production use:

1. **Use HTTPS** - Replace `http://` with `https://`
2. **Authentication** - Add bearer token to connection:
   ```javascript
   .withUrl(hubUrl, {
       accessTokenFactory: () => yourAuthToken
   })
   ```
3. **CORS** - Configure API CORS policy for production domains
4. **Rate Limiting** - Implement client-side rate limiting for subscriptions

## Next Steps

- See [C# client example](./SignalRClientExample/Program.cs) for .NET integration
- Review [WebSocket Guide](../WEBSOCKET_GUIDE.md) for complete documentation
- Check [API documentation](../README.md) for deployment API endpoints

## References

- [SignalR JavaScript Client Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
- [HotSwap.Distributed.Api Documentation](../src/HotSwap.Distributed.Api/README.md)
- [DeploymentHub Source](../src/HotSwap.Distributed.Api/Hubs/DeploymentHub.cs)
