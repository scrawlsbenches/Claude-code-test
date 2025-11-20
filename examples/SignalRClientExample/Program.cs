using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SignalRClientExample;

/// <summary>
/// SignalR client example for monitoring HotSwap deployment updates in real-time.
/// </summary>
class Program
{
    private static HubConnection? _connection;
    private static bool _isRunning = true;

    static async Task Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("  HotSwap Deployment Monitor (SignalR)");
        Console.WriteLine("===========================================\n");

        // Parse command-line arguments
        var apiUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
        var executionId = args.Length > 1 ? args[1] : null;
        var logLevel = args.Length > 2 && args[2].ToLower() == "debug"
            ? LogLevel.Debug
            : LogLevel.Information;

        Console.WriteLine($"API URL: {apiUrl}");
        Console.WriteLine($"Log Level: {logLevel}");
        if (!string.IsNullOrEmpty(executionId))
        {
            Console.WriteLine($"Monitoring Deployment: {executionId}");
        }
        else
        {
            Console.WriteLine("Monitoring: All Deployments");
        }
        Console.WriteLine();

        // Handle Ctrl+C for graceful shutdown
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true;
            _isRunning = false;
            Console.WriteLine("\n\n[SHUTDOWN] Disconnecting from SignalR hub...");

            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }

            Console.WriteLine("[SHUTDOWN] Disconnected. Goodbye!");
        };

        try
        {
            // Build SignalR connection
            var hubUrl = $"{apiUrl}/deploymentHub";
            Console.WriteLine($"[INFO] Connecting to SignalR hub: {hubUrl}");

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new[] {
                    TimeSpan.Zero,           // 1st retry: immediate
                    TimeSpan.FromSeconds(2), // 2nd retry: 2 seconds
                    TimeSpan.FromSeconds(10), // 3rd retry: 10 seconds
                    TimeSpan.FromSeconds(30)  // 4th+ retry: 30 seconds
                })
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(logLevel);
                    logging.AddConsole();
                })
                .Build();

            // Register event handlers
            RegisterEventHandlers();

            // Start connection
            await _connection.StartAsync();
            Console.WriteLine($"[SUCCESS] Connected to hub. Connection ID: {_connection.ConnectionId}\n");

            // Subscribe to deployments
            if (!string.IsNullOrEmpty(executionId))
            {
                await SubscribeToDeployment(executionId);
            }
            else
            {
                await SubscribeToAllDeployments();
            }

            // Keep the application running
            Console.WriteLine("\n[INFO] Listening for deployment updates...");
            Console.WriteLine("[INFO] Press Ctrl+C to exit.\n");

            while (_isRunning)
            {
                await Task.Delay(1000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ERROR] Fatal error: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Registers SignalR event handlers for deployment updates.
    /// </summary>
    private static void RegisterEventHandlers()
    {
        if (_connection == null) return;

        // Handle deployment status changes
        _connection.On<DeploymentStatusMessage>("DeploymentStatusChanged", message =>
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"┌─────────────────────────────────────────────────────────────");
            Console.WriteLine($"│ [{timestamp}] DEPLOYMENT STATUS CHANGED");
            Console.WriteLine($"├─────────────────────────────────────────────────────────────");
            Console.WriteLine($"│ Execution ID: {message.ExecutionId}");
            Console.WriteLine($"│ Status: {message.Status}");
            Console.WriteLine($"│ Updated At: {message.UpdatedAt}");

            if (message.Details != null)
            {
                Console.WriteLine($"│");
                Console.WriteLine($"│ Module: {message.Details.Request?.Module?.Name} v{message.Details.Request?.Module?.Version}");
                Console.WriteLine($"│ Environment: {message.Details.Request?.TargetEnvironment}");
                Console.WriteLine($"│ Requester: {message.Details.Request?.RequesterEmail}");
                Console.WriteLine($"│ Current Stage: {message.Details.CurrentStage ?? "N/A"}");

                if (message.Details.Stages != null && message.Details.Stages.Count > 0)
                {
                    Console.WriteLine($"│ Completed Stages: {message.Details.Stages.Count(s => s.Status == "Succeeded")} / {message.Details.Stages.Count}");
                }
            }

            Console.WriteLine($"└─────────────────────────────────────────────────────────────\n");
        });

        // Handle deployment progress updates
        _connection.On<DeploymentProgressMessage>("DeploymentProgress", message =>
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var progressBar = GenerateProgressBar(message.Progress);

            Console.WriteLine($"[{timestamp}] PROGRESS UPDATE");
            Console.WriteLine($"  Deployment: {message.ExecutionId}");
            Console.WriteLine($"  Stage: {message.Stage}");
            Console.WriteLine($"  Progress: {progressBar} {message.Progress}%");
            Console.WriteLine();
        });

        // Connection lifecycle events
        _connection.Reconnecting += error =>
        {
            Console.WriteLine($"\n[WARNING] Connection lost. Reconnecting...");
            if (error != null)
            {
                Console.WriteLine($"[WARNING] Error: {error.Message}");
            }
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            Console.WriteLine($"[SUCCESS] Reconnected. New connection ID: {connectionId}\n");
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            if (error != null && _isRunning)
            {
                Console.WriteLine($"\n[ERROR] Connection closed: {error.Message}");
                _isRunning = false;
            }
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Subscribes to a specific deployment by execution ID.
    /// </summary>
    private static async Task SubscribeToDeployment(string executionId)
    {
        if (_connection == null)
        {
            Console.WriteLine("[ERROR] No active connection");
            return;
        }

        try
        {
            await _connection.InvokeAsync("SubscribeToDeployment", executionId);
            Console.WriteLine($"[SUCCESS] Subscribed to deployment: {executionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to subscribe to deployment: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Subscribes to all deployment updates.
    /// </summary>
    private static async Task SubscribeToAllDeployments()
    {
        if (_connection == null)
        {
            Console.WriteLine("[ERROR] No active connection");
            return;
        }

        try
        {
            await _connection.InvokeAsync("SubscribeToAllDeployments");
            Console.WriteLine("[SUCCESS] Subscribed to all deployments");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to subscribe to all deployments: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Generates a visual progress bar for console output.
    /// </summary>
    private static string GenerateProgressBar(int progress, int width = 20)
    {
        var filled = (int)(progress / 100.0 * width);
        var empty = width - filled;

        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }
}

/// <summary>
/// Message received when deployment status changes.
/// </summary>
public class DeploymentStatusMessage
{
    public string ExecutionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public DeploymentDetails? Details { get; set; }
}

/// <summary>
/// Message received when deployment progress updates.
/// </summary>
public class DeploymentProgressMessage
{
    public string ExecutionId { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public int Progress { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Detailed information about a deployment.
/// </summary>
public class DeploymentDetails
{
    public Guid ExecutionId { get; set; }
    public DeploymentRequest? Request { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CurrentStage { get; set; }
    public List<PipelineStageResult>? Stages { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Deployment request information.
/// </summary>
public class DeploymentRequest
{
    public ModuleDescriptor? Module { get; set; }
    public string TargetEnvironment { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
}

/// <summary>
/// Module information.
/// </summary>
public class ModuleDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Pipeline stage result.
/// </summary>
public class PipelineStageResult
{
    public string StageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
