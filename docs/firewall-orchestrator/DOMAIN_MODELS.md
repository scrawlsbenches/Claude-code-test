# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Firewall.Domain.Models`

---

## Table of Contents

1. [FirewallRule](#firewallrule)
2. [RuleSet](#ruleset)
3. [Deployment](#deployment)
4. [DeploymentTarget](#deploymenttarget)
5. [ValidationResult](#validationresult)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## FirewallRule

Represents a single firewall rule.

**File:** `src/HotSwap.Firewall.Domain/Models/FirewallRule.cs`

```csharp
namespace HotSwap.Firewall.Domain.Models;

/// <summary>
/// Represents a firewall rule for network traffic control.
/// </summary>
public class FirewallRule
{
    /// <summary>
    /// Unique rule identifier (GUID format).
    /// </summary>
    public required string RuleId { get; set; }

    /// <summary>
    /// Human-readable rule name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Rule description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Rule action (Allow, Deny, Reject).
    /// </summary>
    public RuleAction Action { get; set; } = RuleAction.Deny;

    /// <summary>
    /// Network protocol (TCP, UDP, ICMP, ALL).
    /// </summary>
    public Protocol Protocol { get; set; } = Protocol.ALL;

    /// <summary>
    /// Source IP address or CIDR range.
    /// Example: "192.168.1.0/24", "10.0.0.5", "::/0"
    /// </summary>
    public required string SourceAddress { get; set; }

    /// <summary>
    /// Destination IP address or CIDR range.
    /// </summary>
    public required string DestinationAddress { get; set; }

    /// <summary>
    /// Source port or port range.
    /// Examples: "80", "1024-65535", "any"
    /// </summary>
    public string SourcePort { get; set; } = "any";

    /// <summary>
    /// Destination port or port range.
    /// </summary>
    public string DestinationPort { get; set; } = "any";

    /// <summary>
    /// Rule priority (0-10000, lower number = higher priority).
    /// </summary>
    public int Priority { get; set; } = 1000;

    /// <summary>
    /// Whether this rule is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to log traffic matching this rule.
    /// </summary>
    public bool LogEnabled { get; set; } = false;

    /// <summary>
    /// IP version (IPv4 or IPv6).
    /// </summary>
    public IpVersion IpVersion { get; set; } = IpVersion.IPv4;

    /// <summary>
    /// Rule tags for categorization.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Rule creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the rule.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Validates the firewall rule for correctness.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(RuleId))
            errors.Add("RuleId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(SourceAddress))
            errors.Add("SourceAddress is required");
        else if (!IsValidIpOrCidr(SourceAddress))
            errors.Add($"SourceAddress '{SourceAddress}' is not a valid IP or CIDR");

        if (string.IsNullOrWhiteSpace(DestinationAddress))
            errors.Add("DestinationAddress is required");
        else if (!IsValidIpOrCidr(DestinationAddress))
            errors.Add($"DestinationAddress '{DestinationAddress}' is not a valid IP or CIDR");

        if (Priority < 0 || Priority > 10000)
            errors.Add("Priority must be between 0 and 10000");

        if (!IsValidPort(SourcePort))
            errors.Add($"SourcePort '{SourcePort}' is not valid");

        if (!IsValidPort(DestinationPort))
            errors.Add($"DestinationPort '{DestinationPort}' is not valid");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if this rule is overly permissive (security risk).
    /// </summary>
    public bool IsOverlyPermissive()
    {
        return Action == RuleAction.Allow &&
               (SourceAddress == "0.0.0.0/0" || SourceAddress == "::/0") &&
               (DestinationAddress == "0.0.0.0/0" || DestinationAddress == "::/0") &&
               Protocol == Protocol.ALL &&
               DestinationPort == "any";
    }

    private bool IsValidIpOrCidr(string address)
    {
        // Simplified validation - real implementation would use IPAddress.TryParse
        return !string.IsNullOrWhiteSpace(address);
    }

    private bool IsValidPort(string port)
    {
        if (port == "any") return true;
        if (int.TryParse(port, out int p))
            return p >= 1 && p <= 65535;
        if (port.Contains("-"))
        {
            var parts = port.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int start) &&
                int.TryParse(parts[1], out int end))
                return start >= 1 && end <= 65535 && start < end;
        }
        return false;
    }
}
```

---

## RuleSet

Represents a collection of firewall rules.

**File:** `src/HotSwap.Firewall.Domain/Models/RuleSet.cs`

```csharp
namespace HotSwap.Firewall.Domain.Models;

/// <summary>
/// Represents a collection of firewall rules for deployment.
/// </summary>
public class RuleSet
{
    /// <summary>
    /// Unique rule set name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Rule set version (semantic versioning).
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Target environment (Development, QA, Staging, Production).
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Target firewall type (CloudFirewall, OnPremiseFirewall).
    /// </summary>
    public TargetType TargetType { get; set; } = TargetType.CloudFirewall;

    /// <summary>
    /// Collection of firewall rules.
    /// </summary>
    public List<FirewallRule> Rules { get; set; } = new();

    /// <summary>
    /// Rule set metadata (key-value pairs).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Rule set creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the rule set.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Current status of the rule set.
    /// </summary>
    public RuleSetStatus Status { get; set; } = RuleSetStatus.Draft;

    /// <summary>
    /// Validates the rule set.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z0-9._-]+$"))
            errors.Add("Name must contain only alphanumeric characters, dots, dashes, and underscores");

        if (Rules.Count == 0)
            errors.Add("Rule set must contain at least one rule");

        // Validate all rules
        foreach (var rule in Rules)
        {
            if (!rule.IsValid(out var ruleErrors))
            {
                errors.AddRange(ruleErrors.Select(e => $"Rule '{rule.Name}': {e}"));
            }
        }

        // Check for priority conflicts
        var priorityGroups = Rules.GroupBy(r => r.Priority).Where(g => g.Count() > 1);
        foreach (var group in priorityGroups)
        {
            errors.Add($"Multiple rules have priority {group.Key}: {string.Join(", ", group.Select(r => r.Name))}");
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Detects conflicting rules (Allow + Deny for same traffic).
    /// </summary>
    public List<RuleConflict> DetectConflicts()
    {
        var conflicts = new List<RuleConflict>();

        for (int i = 0; i < Rules.Count; i++)
        {
            for (int j = i + 1; j < Rules.Count; j++)
            {
                var rule1 = Rules[i];
                var rule2 = Rules[j];

                if (rule1.Action != rule2.Action &&
                    RulesOverlap(rule1, rule2))
                {
                    conflicts.Add(new RuleConflict
                    {
                        Rule1 = rule1.Name,
                        Rule2 = rule2.Name,
                        ConflictType = "ActionConflict",
                        Description = $"Rule '{rule1.Name}' {rule1.Action}s traffic that '{rule2.Name}' {rule2.Action}s"
                    });
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Detects shadowed rules (rules that will never be matched).
    /// </summary>
    public List<string> DetectShadowedRules()
    {
        var shadowed = new List<string>();
        var sortedRules = Rules.OrderBy(r => r.Priority).ToList();

        for (int i = 0; i < sortedRules.Count; i++)
        {
            for (int j = 0; j < i; j++)
            {
                if (RuleCovers(sortedRules[j], sortedRules[i]))
                {
                    shadowed.Add(sortedRules[i].Name);
                    break;
                }
            }
        }

        return shadowed;
    }

    private bool RulesOverlap(FirewallRule rule1, FirewallRule rule2)
    {
        // Simplified overlap detection
        return rule1.Protocol == rule2.Protocol &&
               rule1.SourceAddress == rule2.SourceAddress &&
               rule1.DestinationAddress == rule2.DestinationAddress;
    }

    private bool RuleCovers(FirewallRule higherPriority, FirewallRule lowerPriority)
    {
        // Simplified coverage check
        return RulesOverlap(higherPriority, lowerPriority);
    }
}

/// <summary>
/// Represents a rule conflict.
/// </summary>
public class RuleConflict
{
    public required string Rule1 { get; set; }
    public required string Rule2 { get; set; }
    public required string ConflictType { get; set; }
    public required string Description { get; set; }
}
```

---

## Deployment

Represents a firewall rule set deployment.

**File:** `src/HotSwap.Firewall.Domain/Models/Deployment.cs`

```csharp
namespace HotSwap.Firewall.Domain.Models;

/// <summary>
/// Represents a firewall rule deployment operation.
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Rule set name being deployed.
    /// </summary>
    public required string RuleSetName { get; set; }

    /// <summary>
    /// Rule set version being deployed.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Target environment (Development, QA, Staging, Production).
    /// </summary>
    public required string TargetEnvironment { get; set; }

    /// <summary>
    /// Deployment strategy (Direct, Canary, BlueGreen, Rolling, AB).
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.Direct;

    /// <summary>
    /// Canary deployment percentage (for Canary strategy).
    /// </summary>
    public int? CanaryPercentage { get; set; }

    /// <summary>
    /// Deployment targets (firewall instances).
    /// </summary>
    public List<string> TargetIds { get; set; } = new();

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Validation checks to perform.
    /// </summary>
    public List<string> ValidationChecks { get; set; } = new();

    /// <summary>
    /// Deployment configuration options.
    /// </summary>
    public DeploymentConfig Config { get; set; } = new();

    /// <summary>
    /// User who initiated the deployment.
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Deployment creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Approval workflow ID (if required).
    /// </summary>
    public string? ApprovalId { get; set; }

    /// <summary>
    /// Approval status (for production deployments).
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.NotRequired;

    /// <summary>
    /// Deployment error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Rollback information (if rolled back).
    /// </summary>
    public RollbackInfo? Rollback { get; set; }

    /// <summary>
    /// Deployment metrics and telemetry.
    /// </summary>
    public DeploymentMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Calculates deployment duration.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (StartedAt.HasValue && CompletedAt.HasValue)
            return CompletedAt.Value - StartedAt.Value;
        return null;
    }

    /// <summary>
    /// Checks if deployment requires approval.
    /// </summary>
    public bool RequiresApproval()
    {
        return TargetEnvironment == "Production" ||
               TargetEnvironment == "Staging";
    }
}

/// <summary>
/// Deployment configuration options.
/// </summary>
public class DeploymentConfig
{
    /// <summary>
    /// Enable dry-run mode (validate without deploying).
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Timeout for deployment (seconds).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Enable automatic rollback on failures.
    /// </summary>
    public bool AutoRollback { get; set; } = true;

    /// <summary>
    /// Wait time between deployment stages (seconds).
    /// </summary>
    public int StageWaitSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum allowed error rate (percentage).
    /// </summary>
    public double MaxErrorRate { get; set; } = 0.01; // 1%

    /// <summary>
    /// Enable parallel deployment to targets.
    /// </summary>
    public bool ParallelDeployment { get; set; } = false;
}

/// <summary>
/// Deployment metrics.
/// </summary>
public class DeploymentMetrics
{
    /// <summary>
    /// Number of targets successfully deployed.
    /// </summary>
    public int TargetsSucceeded { get; set; } = 0;

    /// <summary>
    /// Number of targets that failed deployment.
    /// </summary>
    public int TargetsFailed { get; set; } = 0;

    /// <summary>
    /// Number of rules deployed.
    /// </summary>
    public int RulesDeployed { get; set; } = 0;

    /// <summary>
    /// Average deployment time per target (milliseconds).
    /// </summary>
    public double AvgDeploymentTimeMs { get; set; } = 0;

    /// <summary>
    /// Total validation checks performed.
    /// </summary>
    public int ValidationChecksPassed { get; set; } = 0;

    /// <summary>
    /// Failed validation checks.
    /// </summary>
    public int ValidationChecksFailed { get; set; } = 0;
}

/// <summary>
/// Rollback information.
/// </summary>
public class RollbackInfo
{
    /// <summary>
    /// Rollback trigger reason.
    /// </summary>
    public required string Reason { get; set; }

    /// <summary>
    /// Timestamp when rollback was triggered (UTC).
    /// </summary>
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Previous version rolled back to.
    /// </summary>
    public string? PreviousVersion { get; set; }

    /// <summary>
    /// Whether rollback was successful.
    /// </summary>
    public bool Success { get; set; } = false;

    /// <summary>
    /// Rollback duration (seconds).
    /// </summary>
    public double DurationSeconds { get; set; }
}
```

---

## DeploymentTarget

Represents a firewall instance target for deployment.

**File:** `src/HotSwap.Firewall.Domain/Models/DeploymentTarget.cs`

```csharp
namespace HotSwap.Firewall.Domain.Models;

/// <summary>
/// Represents a firewall target for rule deployment.
/// </summary>
public class DeploymentTarget
{
    /// <summary>
    /// Unique target identifier.
    /// </summary>
    public required string TargetId { get; set; }

    /// <summary>
    /// Human-readable target name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Target type (AWS, Azure, GCP, PaloAlto, FortiGate, etc.).
    /// </summary>
    public required string ProviderType { get; set; }

    /// <summary>
    /// Environment (Development, QA, Staging, Production).
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Region or datacenter location.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Provider-specific configuration.
    /// </summary>
    public Dictionary<string, string> ProviderConfig { get; set; } = new();

    /// <summary>
    /// Current health status.
    /// </summary>
    public TargetHealth Health { get; set; } = new();

    /// <summary>
    /// Currently deployed rule set version.
    /// </summary>
    public string? CurrentVersion { get; set; }

    /// <summary>
    /// Last deployment timestamp (UTC).
    /// </summary>
    public DateTime? LastDeployedAt { get; set; }

    /// <summary>
    /// Target tags for grouping and selection.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Whether target is enabled for deployments.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Target creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if target is healthy and ready for deployment.
    /// </summary>
    public bool IsHealthy()
    {
        return Enabled && Health.IsHealthy;
    }
}

/// <summary>
/// Target health information.
/// </summary>
public class TargetHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Last health check timestamp (UTC).
    /// </summary>
    public DateTime LastCheckAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Health check error message (if unhealthy).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Connectivity status.
    /// </summary>
    public bool ConnectivityOk { get; set; } = true;

    /// <summary>
    /// API availability status.
    /// </summary>
    public bool ApiAvailable { get; set; } = true;

    /// <summary>
    /// Authentication status.
    /// </summary>
    public bool AuthenticationOk { get; set; } = true;
}
```

---

## ValidationResult

Represents the result of rule validation.

**File:** `src/HotSwap.Firewall.Domain/Models/ValidationResult.cs`

```csharp
namespace HotSwap.Firewall.Domain.Models;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors.
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings (non-blocking).
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Validation timestamp (UTC).
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static ValidationResult Failure(params ValidationError[] errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }
}

/// <summary>
/// Validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Error code.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Field or property that failed validation.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Severity level.
    /// </summary>
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;
}

/// <summary>
/// Validation warning.
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Warning code.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Warning message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Recommendation to resolve the warning.
    /// </summary>
    public string? Recommendation { get; set; }
}
```

---

## Enumerations

### RuleAction

**File:** `src/HotSwap.Firewall.Domain/Enums/RuleAction.cs`

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// Firewall rule action.
/// </summary>
public enum RuleAction
{
    /// <summary>
    /// Allow the traffic.
    /// </summary>
    Allow,

    /// <summary>
    /// Deny the traffic silently (drop packets).
    /// </summary>
    Deny,

    /// <summary>
    /// Reject the traffic with ICMP error response.
    /// </summary>
    Reject
}
```

### Protocol

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// Network protocol.
/// </summary>
public enum Protocol
{
    /// <summary>
    /// All protocols.
    /// </summary>
    ALL,

    /// <summary>
    /// Transmission Control Protocol.
    /// </summary>
    TCP,

    /// <summary>
    /// User Datagram Protocol.
    /// </summary>
    UDP,

    /// <summary>
    /// Internet Control Message Protocol.
    /// </summary>
    ICMP,

    /// <summary>
    /// Encapsulating Security Payload (IPsec).
    /// </summary>
    ESP,

    /// <summary>
    /// Authentication Header (IPsec).
    /// </summary>
    AH,

    /// <summary>
    /// Generic Routing Encapsulation.
    /// </summary>
    GRE
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// Deployment strategy type.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Direct deployment to all targets immediately.
    /// </summary>
    Direct,

    /// <summary>
    /// Canary deployment (progressive rollout).
    /// </summary>
    Canary,

    /// <summary>
    /// Blue-green deployment (parallel environments).
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Rolling deployment (sequential updates).
    /// </summary>
    Rolling,

    /// <summary>
    /// A/B testing deployment (traffic split).
    /// </summary>
    AB
}
```

### DeploymentStatus

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// Deployment status.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment pending approval.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment awaiting approval.
    /// </summary>
    AwaitingApproval,

    /// <summary>
    /// Deployment approved, ready to start.
    /// </summary>
    Approved,

    /// <summary>
    /// Deployment in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment validating connectivity.
    /// </summary>
    Validating,

    /// <summary>
    /// Deployment succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Deployment failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Deployment rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Deployment cancelled.
    /// </summary>
    Cancelled
}
```

### RuleSetStatus

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// Rule set status.
/// </summary>
public enum RuleSetStatus
{
    /// <summary>
    /// Rule set in draft state.
    /// </summary>
    Draft,

    /// <summary>
    /// Rule set validated and ready.
    /// </summary>
    Ready,

    /// <summary>
    /// Rule set deployed to at least one target.
    /// </summary>
    Deployed,

    /// <summary>
    /// Rule set deprecated.
    /// </summary>
    Deprecated,

    /// <summary>
    /// Rule set archived.
    /// </summary>
    Archived
}
```

### TargetType

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// Deployment target type.
/// </summary>
public enum TargetType
{
    /// <summary>
    /// Cloud-based firewall (AWS, Azure, GCP).
    /// </summary>
    CloudFirewall,

    /// <summary>
    /// On-premise firewall appliance.
    /// </summary>
    OnPremiseFirewall
}
```

### ApprovalStatus

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// Approval status for deployments.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Approval not required.
    /// </summary>
    NotRequired,

    /// <summary>
    /// Awaiting approval.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment approved.
    /// </summary>
    Approved,

    /// <summary>
    /// Deployment rejected.
    /// </summary>
    Rejected
}
```

### IpVersion

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// IP protocol version.
/// </summary>
public enum IpVersion
{
    /// <summary>
    /// Internet Protocol version 4.
    /// </summary>
    IPv4,

    /// <summary>
    /// Internet Protocol version 6.
    /// </summary>
    IPv6
}
```

### ErrorSeverity

```csharp
namespace HotSwap.Firewall.Domain.Enums;

/// <summary>
/// Validation error severity.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning (non-blocking).
    /// </summary>
    Warning,

    /// <summary>
    /// Error (blocking).
    /// </summary>
    Error,

    /// <summary>
    /// Critical error (immediate action required).
    /// </summary>
    Critical
}
```

---

## Value Objects

### ConnectivityTest

**File:** `src/HotSwap.Firewall.Domain/ValueObjects/ConnectivityTest.cs`

```csharp
namespace HotSwap.Firewall.Domain.ValueObjects;

/// <summary>
/// Represents a connectivity validation test.
/// </summary>
public class ConnectivityTest
{
    /// <summary>
    /// Test type (Ping, TCP, HTTP, HTTPS, DNS).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Target address or hostname.
    /// </summary>
    public required string Target { get; set; }

    /// <summary>
    /// Port number (for TCP/HTTP tests).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Protocol (TCP, UDP, ICMP).
    /// </summary>
    public string Protocol { get; set; } = "TCP";

    /// <summary>
    /// Test timeout (seconds).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Expected result (Success, Blocked).
    /// </summary>
    public string ExpectedResult { get; set; } = "Success";

    /// <summary>
    /// Test execution result.
    /// </summary>
    public TestResult? Result { get; set; }
}

/// <summary>
/// Connectivity test result.
/// </summary>
public class TestResult
{
    /// <summary>
    /// Whether test passed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Test execution time (milliseconds).
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Test timestamp (UTC).
    /// </summary>
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.Firewall.Domain`
