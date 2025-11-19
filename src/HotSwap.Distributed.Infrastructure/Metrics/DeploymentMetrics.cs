using System.Diagnostics.Metrics;

namespace HotSwap.Distributed.Infrastructure.Metrics;

/// <summary>
/// Custom metrics for deployment operations exposed via Prometheus.
/// </summary>
public class DeploymentMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _deploymentsStarted;
    private readonly Counter<long> _deploymentsCompleted;
    private readonly Counter<long> _deploymentsFailed;
    private readonly Counter<long> _deploymentsRolledBack;
    private readonly Histogram<double> _deploymentDuration;
    private readonly Counter<long> _approvalRequests;
    private readonly Counter<long> _approvalGranted;
    private readonly Counter<long> _approvalRejected;
    private readonly Counter<long> _modulesDeployed;
    private readonly Counter<long> _nodesUpdated;

    public const string MeterName = "HotSwap.Distributed.Orchestrator";
    public const string MeterVersion = "1.0.0";

    public DeploymentMetrics()
    {
        _meter = new Meter(MeterName, MeterVersion);

        // Deployment counters
        _deploymentsStarted = _meter.CreateCounter<long>(
            name: "deployments_started_total",
            unit: "deployments",
            description: "Total number of deployments started");

        _deploymentsCompleted = _meter.CreateCounter<long>(
            name: "deployments_completed_total",
            unit: "deployments",
            description: "Total number of deployments completed successfully");

        _deploymentsFailed = _meter.CreateCounter<long>(
            name: "deployments_failed_total",
            unit: "deployments",
            description: "Total number of deployments that failed");

        _deploymentsRolledBack = _meter.CreateCounter<long>(
            name: "deployments_rolled_back_total",
            unit: "deployments",
            description: "Total number of deployments rolled back");

        _deploymentDuration = _meter.CreateHistogram<double>(
            name: "deployment_duration_seconds",
            unit: "seconds",
            description: "Duration of deployment operations");

        // Approval counters
        _approvalRequests = _meter.CreateCounter<long>(
            name: "approval_requests_total",
            unit: "requests",
            description: "Total number of approval requests created");

        _approvalGranted = _meter.CreateCounter<long>(
            name: "approvals_granted_total",
            unit: "approvals",
            description: "Total number of approvals granted");

        _approvalRejected = _meter.CreateCounter<long>(
            name: "approvals_rejected_total",
            unit: "approvals",
            description: "Total number of approvals rejected");

        // Module and node counters
        _modulesDeployed = _meter.CreateCounter<long>(
            name: "modules_deployed_total",
            unit: "modules",
            description: "Total number of modules deployed");

        _nodesUpdated = _meter.CreateCounter<long>(
            name: "nodes_updated_total",
            unit: "nodes",
            description: "Total number of nodes updated");
    }

    /// <summary>
    /// Records a deployment start event.
    /// </summary>
    public void RecordDeploymentStarted(string environment, string strategy, string moduleName)
    {
        _deploymentsStarted.Add(1, new KeyValuePair<string, object?>("environment", environment),
                                   new KeyValuePair<string, object?>("strategy", strategy),
                                   new KeyValuePair<string, object?>("module", moduleName));
    }

    /// <summary>
    /// Records a deployment completion event.
    /// </summary>
    public void RecordDeploymentCompleted(string environment, string strategy, string moduleName, double durationSeconds)
    {
        _deploymentsCompleted.Add(1, new KeyValuePair<string, object?>("environment", environment),
                                      new KeyValuePair<string, object?>("strategy", strategy),
                                      new KeyValuePair<string, object?>("module", moduleName));

        _deploymentDuration.Record(durationSeconds, new KeyValuePair<string, object?>("environment", environment),
                                                      new KeyValuePair<string, object?>("strategy", strategy),
                                                      new KeyValuePair<string, object?>("status", "success"));
    }

    /// <summary>
    /// Records a deployment failure event.
    /// </summary>
    public void RecordDeploymentFailed(string environment, string strategy, string moduleName, double durationSeconds, string reason)
    {
        _deploymentsFailed.Add(1, new KeyValuePair<string, object?>("environment", environment),
                                  new KeyValuePair<string, object?>("strategy", strategy),
                                  new KeyValuePair<string, object?>("module", moduleName),
                                  new KeyValuePair<string, object?>("reason", reason));

        _deploymentDuration.Record(durationSeconds, new KeyValuePair<string, object?>("environment", environment),
                                                      new KeyValuePair<string, object?>("strategy", strategy),
                                                      new KeyValuePair<string, object?>("status", "failure"));
    }

    /// <summary>
    /// Records a deployment rollback event.
    /// </summary>
    public void RecordDeploymentRolledBack(string environment, string moduleName, int nodesAffected)
    {
        _deploymentsRolledBack.Add(1, new KeyValuePair<string, object?>("environment", environment),
                                      new KeyValuePair<string, object?>("module", moduleName));

        _nodesUpdated.Add(nodesAffected, new KeyValuePair<string, object?>("operation", "rollback"),
                                          new KeyValuePair<string, object?>("environment", environment));
    }

    /// <summary>
    /// Records an approval request event.
    /// </summary>
    public void RecordApprovalRequest(string environment, string moduleName)
    {
        _approvalRequests.Add(1, new KeyValuePair<string, object?>("environment", environment),
                                 new KeyValuePair<string, object?>("module", moduleName));
    }

    /// <summary>
    /// Records an approval granted event.
    /// </summary>
    public void RecordApprovalGranted(string environment, string moduleName, string approver)
    {
        _approvalGranted.Add(1, new KeyValuePair<string, object?>("environment", environment),
                                new KeyValuePair<string, object?>("module", moduleName),
                                new KeyValuePair<string, object?>("approver", approver));
    }

    /// <summary>
    /// Records an approval rejected event.
    /// </summary>
    public void RecordApprovalRejected(string environment, string moduleName, string approver, string reason)
    {
        _approvalRejected.Add(1, new KeyValuePair<string, object?>("environment", environment),
                                 new KeyValuePair<string, object?>("module", moduleName),
                                 new KeyValuePair<string, object?>("approver", approver),
                                 new KeyValuePair<string, object?>("reason", reason));
    }

    /// <summary>
    /// Records module deployment event.
    /// </summary>
    public void RecordModulesDeployed(int count, string environment, string moduleName)
    {
        _modulesDeployed.Add(count, new KeyValuePair<string, object?>("environment", environment),
                                    new KeyValuePair<string, object?>("module", moduleName));
    }

    /// <summary>
    /// Records nodes updated event.
    /// </summary>
    public void RecordNodesUpdated(int count, string environment, string operation = "deploy")
    {
        _nodesUpdated.Add(count, new KeyValuePair<string, object?>("environment", environment),
                                 new KeyValuePair<string, object?>("operation", operation));
    }
}
