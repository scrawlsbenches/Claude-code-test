using System.Diagnostics;
using System.Diagnostics.Metrics;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using OpenTelemetry.Trace;

namespace HotSwap.Distributed.Infrastructure.Telemetry;

/// <summary>
/// Provides OpenTelemetry-based distributed tracing and metrics.
/// </summary>
public class TelemetryProvider : IDisposable
{
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    // Metrics
    private readonly Counter<long> _deploymentsTotal;
    private readonly Counter<long> _deploymentsFailures;
    private readonly Counter<long> _rollbacksTotal;
    private readonly Histogram<double> _deploymentDuration;
    private readonly Histogram<double> _healthCheckDuration;

    public const string ServiceName = "HotSwap.DistributedKernel";
    public const string ServiceVersion = "1.0.0";

    public TelemetryProvider()
    {
        _activitySource = new ActivitySource(ServiceName, ServiceVersion);
        _meter = new Meter(ServiceName, ServiceVersion);

        // Initialize metrics
        _deploymentsTotal = _meter.CreateCounter<long>(
            "deployments.total",
            description: "Total number of deployments");

        _deploymentsFailures = _meter.CreateCounter<long>(
            "deployments.failures",
            description: "Total number of failed deployments");

        _rollbacksTotal = _meter.CreateCounter<long>(
            "rollbacks.total",
            description: "Total number of rollbacks");

        _deploymentDuration = _meter.CreateHistogram<double>(
            "deployment.duration",
            unit: "seconds",
            description: "Deployment duration in seconds");

        _healthCheckDuration = _meter.CreateHistogram<double>(
            "node.healthcheck.duration",
            unit: "seconds",
            description: "Health check duration in seconds");
    }

    #region Activity Creation

    /// <summary>
    /// Starts a deployment activity for distributed tracing.
    /// </summary>
    public Activity? StartDeploymentActivity(
        string moduleName,
        Version version,
        EnvironmentType environment,
        string strategy)
    {
        var activity = _activitySource.StartActivity(
            "deployment",
            ActivityKind.Internal);

        activity?.SetTag("module.name", moduleName);
        activity?.SetTag("module.version", version.ToString());
        activity?.SetTag("environment", environment.ToString());
        activity?.SetTag("strategy", strategy);

        return activity;
    }

    /// <summary>
    /// Starts a node-level activity.
    /// </summary>
    public Activity? StartNodeActivity(
        string operationName,
        Guid nodeId,
        string hostname)
    {
        var activity = _activitySource.StartActivity(
            operationName,
            ActivityKind.Internal);

        activity?.SetTag("node.id", nodeId.ToString());
        activity?.SetTag("node.hostname", hostname);

        return activity;
    }

    /// <summary>
    /// Starts a pipeline stage activity.
    /// </summary>
    public Activity? StartStageActivity(
        string stageName,
        EnvironmentType? environment = null)
    {
        var activity = _activitySource.StartActivity(
            $"stage.{stageName}",
            ActivityKind.Internal);

        activity?.SetTag("stage.name", stageName);

        if (environment.HasValue)
        {
            activity?.SetTag("environment", environment.Value.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Starts a health check activity.
    /// </summary>
    public Activity? StartHealthCheckActivity(Guid nodeId)
    {
        var activity = _activitySource.StartActivity(
            "health.check",
            ActivityKind.Internal);

        activity?.SetTag("node.id", nodeId.ToString());

        return activity;
    }

    /// <summary>
    /// Starts a rollback activity.
    /// </summary>
    public Activity? StartRollbackActivity(
        string moduleName,
        EnvironmentType environment)
    {
        var activity = _activitySource.StartActivity(
            "rollback",
            ActivityKind.Internal);

        activity?.SetTag("module.name", moduleName);
        activity?.SetTag("environment", environment.ToString());

        return activity;
    }

    #endregion

    #region Recording Results

    /// <summary>
    /// Records a successful deployment.
    /// </summary>
    public void RecordDeploymentSuccess(
        Activity? activity,
        DeploymentResult result)
    {
        _deploymentsTotal.Add(1,
            new KeyValuePair<string, object?>("environment", result.Environment.ToString()),
            new KeyValuePair<string, object?>("strategy", result.Strategy),
            new KeyValuePair<string, object?>("success", true));

        _deploymentDuration.Record(result.Duration.TotalSeconds,
            new KeyValuePair<string, object?>("environment", result.Environment.ToString()),
            new KeyValuePair<string, object?>("strategy", result.Strategy));

        activity?.SetStatus(ActivityStatusCode.Ok, "Deployment succeeded");
        activity?.SetTag("deployment.success", true);
        activity?.SetTag("deployment.nodes.total", result.NodeResults.Count);
        activity?.SetTag("deployment.nodes.succeeded", result.NodeResults.Count(r => r.Success));
    }

    /// <summary>
    /// Records a failed deployment.
    /// </summary>
    public void RecordDeploymentFailure(
        Activity? activity,
        DeploymentResult result,
        Exception? exception = null)
    {
        _deploymentsTotal.Add(1,
            new KeyValuePair<string, object?>("environment", result.Environment.ToString()),
            new KeyValuePair<string, object?>("strategy", result.Strategy),
            new KeyValuePair<string, object?>("success", false));

        _deploymentsFailures.Add(1,
            new KeyValuePair<string, object?>("environment", result.Environment.ToString()),
            new KeyValuePair<string, object?>("strategy", result.Strategy));

        activity?.SetStatus(ActivityStatusCode.Error, exception?.Message ?? result.Message);
        activity?.SetTag("deployment.success", false);

        if (exception != null)
        {
            activity?.RecordException(exception);
        }
    }

    /// <summary>
    /// Records a rollback operation.
    /// </summary>
    public void RecordRollback(
        Activity? activity,
        string moduleName,
        EnvironmentType environment,
        int nodesAffected,
        bool success)
    {
        _rollbacksTotal.Add(1,
            new KeyValuePair<string, object?>("environment", environment.ToString()),
            new KeyValuePair<string, object?>("success", success));

        activity?.SetTag("rollback.success", success);
        activity?.SetTag("rollback.nodes", nodesAffected);
    }

    /// <summary>
    /// Records a health check.
    /// </summary>
    public void RecordHealthCheck(
        Activity? activity,
        TimeSpan duration,
        bool healthy)
    {
        _healthCheckDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("healthy", healthy));

        activity?.SetTag("health.status", healthy ? "healthy" : "unhealthy");
    }

    #endregion

    #region Context Propagation

    /// <summary>
    /// Extracts trace context from headers/dictionary.
    /// </summary>
    public ActivityContext? ExtractTraceContext(Dictionary<string, string> headers)
    {
        if (headers.TryGetValue("traceparent", out var traceparent))
        {
            return ActivityContext.TryParse(traceparent, null, out var context)
                ? context
                : null;
        }

        return null;
    }

    /// <summary>
    /// Injects trace context into headers/dictionary.
    /// </summary>
    public void InjectTraceContext(Activity? activity, Dictionary<string, string> headers)
    {
        if (activity != null)
        {
            headers["traceparent"] = activity.Id ?? string.Empty;

            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                headers["tracestate"] = activity.TraceStateString;
            }
        }
    }

    #endregion

    #region Baggage

    /// <summary>
    /// Sets baggage for context propagation.
    /// </summary>
    public void SetBaggage(string key, string value)
    {
        Activity.Current?.SetBaggage(key, value);
    }

    /// <summary>
    /// Gets baggage value.
    /// </summary>
    public string? GetBaggage(string key)
    {
        return Activity.Current?.GetBaggageItem(key);
    }

    #endregion

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}
