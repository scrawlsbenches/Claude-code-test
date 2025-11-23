# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Gaming.Domain.Models`

---

## Table of Contents

1. [GameConfiguration](#gameconfiguration)
2. [ConfigDeployment](#configdeployment)
3. [GameServer](#gameserver)
4. [LiveEvent](#liveevent)
5. [PlayerMetrics](#playermetrics)
6. [ABTest](#abtest)
7. [Enumerations](#enumerations)
8. [Value Objects](#value-objects)

---

## GameConfiguration

Represents a game configuration (balance, economy, matchmaking, etc.).

**File:** `src/HotSwap.Gaming.Domain/Models/GameConfiguration.cs`

```csharp
namespace HotSwap.Gaming.Domain.Models;

/// <summary>
/// Represents a game configuration that can be deployed to game servers.
/// </summary>
public class GameConfiguration
{
    /// <summary>
    /// Unique configuration identifier.
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Human-readable configuration name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Game/project identifier.
    /// </summary>
    public required string GameId { get; set; }

    /// <summary>
    /// Configuration type (GameBalance, Economy, Matchmaking, etc.).
    /// </summary>
    public ConfigurationType ConfigType { get; set; }

    /// <summary>
    /// Configuration content (JSON format).
    /// </summary>
    public required string Configuration { get; set; }

    /// <summary>
    /// Semantic version (e.g., "2.1.0").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// JSON Schema ID for validation.
    /// </summary>
    public required string SchemaId { get; set; }

    /// <summary>
    /// Description of changes in this version.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// Configuration status (Draft, PendingApproval, Approved, Deprecated).
    /// </summary>
    public ConfigurationStatus Status { get; set; } = ConfigurationStatus.Draft;

    /// <summary>
    /// Admin user who approved the configuration.
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Configuration creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tags for categorization (e.g., "hotfix", "seasonal").
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConfigId))
            errors.Add("ConfigId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(GameId))
            errors.Add("GameId is required");

        if (string.IsNullOrWhiteSpace(Configuration))
            errors.Add("Configuration is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        if (string.IsNullOrWhiteSpace(SchemaId))
            errors.Add("SchemaId is required");

        // Validate JSON format
        try
        {
            System.Text.Json.JsonDocument.Parse(Configuration);
        }
        catch
        {
            errors.Add("Configuration must be valid JSON");
        }

        // Validate semantic version format
        if (!System.Text.RegularExpressions.Regex.IsMatch(Version, @"^\d+\.\d+\.\d+$"))
            errors.Add("Version must be in semantic format (e.g., 1.0.0)");

        if (Configuration.Length > 10485760) // 10 MB
            errors.Add("Configuration exceeds maximum size of 10 MB");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the configuration is approved for production.
    /// </summary>
    public bool IsApproved() => Status == ConfigurationStatus.Approved;
}
```

---

## ConfigDeployment

Represents a deployment of a configuration to game servers.

**File:** `src/HotSwap.Gaming.Domain/Models/ConfigDeployment.cs`

```csharp
namespace HotSwap.Gaming.Domain.Models;

/// <summary>
/// Represents a deployment of a game configuration.
/// </summary>
public class ConfigDeployment
{
    /// <summary>
    /// Unique deployment identifier.
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Configuration being deployed.
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Deployment strategy (Canary, Geographic, BlueGreen, ABTest).
    /// </summary>
    public DeploymentStrategy Strategy { get; set; }

    /// <summary>
    /// Target regions for deployment.
    /// </summary>
    public List<string> TargetRegions { get; set; } = new();

    /// <summary>
    /// Target server tags (optional filtering).
    /// </summary>
    public List<string> TargetServerTags { get; set; } = new();

    /// <summary>
    /// Current deployment phase (0-based).
    /// </summary>
    public int CurrentPhase { get; set; } = 0;

    /// <summary>
    /// Total deployment phases.
    /// </summary>
    public int TotalPhases { get; set; } = 4;

    /// <summary>
    /// Current deployment percentage (0-100).
    /// </summary>
    public int CurrentPercentage { get; set; } = 0;

    /// <summary>
    /// Deployment status (Pending, InProgress, Completed, RolledBack, Failed).
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Rollback timestamp (UTC).
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// Rollback reason.
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Automatic progression enabled (based on metrics).
    /// </summary>
    public bool AutoProgressEnabled { get; set; } = true;

    /// <summary>
    /// Evaluation period per phase (ISO 8601 duration).
    /// </summary>
    public TimeSpan EvaluationPeriod { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Rollback thresholds configuration.
    /// </summary>
    public RollbackThresholds Thresholds { get; set; } = new();

    /// <summary>
    /// Deployment metrics snapshot.
    /// </summary>
    public DeploymentMetrics? Metrics { get; set; }

    /// <summary>
    /// User who initiated the deployment.
    /// </summary>
    public required string DeployedBy { get; set; }

    /// <summary>
    /// Deployment notes/comments.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Checks if deployment can progress to next phase.
    /// </summary>
    public bool CanProgress()
    {
        return Status == DeploymentStatus.InProgress &&
               CurrentPhase < TotalPhases - 1 &&
               AutoProgressEnabled;
    }

    /// <summary>
    /// Checks if deployment should rollback based on metrics.
    /// </summary>
    public bool ShouldRollback(PlayerMetrics currentMetrics, PlayerMetrics baselineMetrics)
    {
        if (Metrics == null) return false;

        // Check churn rate increase
        double churnIncrease = currentMetrics.ChurnRate - baselineMetrics.ChurnRate;
        if (churnIncrease > Thresholds.ChurnRateIncreaseMax)
            return true;

        // Check crash rate increase
        double crashIncrease = currentMetrics.CrashRate - baselineMetrics.CrashRate;
        if (crashIncrease > Thresholds.CrashRateIncreaseMax)
            return true;

        // Check session duration decrease
        double sessionDecrease = (baselineMetrics.AvgSessionDurationMinutes - currentMetrics.AvgSessionDurationMinutes)
                                 / baselineMetrics.AvgSessionDurationMinutes * 100;
        if (sessionDecrease > Thresholds.SessionDurationDecreaseMaxPercent)
            return true;

        // Check player complaints
        double complaintRate = (double)currentMetrics.PlayerComplaints / currentMetrics.ActivePlayers * 100;
        if (complaintRate > Thresholds.ComplaintRateMaxPercent)
            return true;

        return false;
    }
}

/// <summary>
/// Rollback thresholds for automatic rollback decisions.
/// </summary>
public class RollbackThresholds
{
    /// <summary>
    /// Maximum churn rate increase (percentage points).
    /// </summary>
    public double ChurnRateIncreaseMax { get; set; } = 5.0;

    /// <summary>
    /// Maximum crash rate increase (percentage points).
    /// </summary>
    public double CrashRateIncreaseMax { get; set; } = 10.0;

    /// <summary>
    /// Maximum session duration decrease (percent).
    /// </summary>
    public double SessionDurationDecreaseMaxPercent { get; set; } = 20.0;

    /// <summary>
    /// Maximum complaint rate (percent of active players).
    /// </summary>
    public double ComplaintRateMaxPercent { get; set; } = 1.0;

    /// <summary>
    /// Maximum engagement score decrease (percent).
    /// </summary>
    public double EngagementDecreaseMaxPercent { get; set; } = 25.0;
}
```

---

## GameServer

Represents a game server that can receive configurations.

**File:** `src/HotSwap.Gaming.Domain/Models/GameServer.cs`

```csharp
namespace HotSwap.Gaming.Domain.Models;

/// <summary>
/// Represents a game server in the cluster.
/// </summary>
public class GameServer
{
    /// <summary>
    /// Unique server identifier.
    /// </summary>
    public required string ServerId { get; set; }

    /// <summary>
    /// Server hostname.
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    /// Server IP address.
    /// </summary>
    public required string IpAddress { get; set; }

    /// <summary>
    /// Server port.
    /// </summary>
    public int Port { get; set; } = 7777;

    /// <summary>
    /// Geographic region (e.g., "NA-WEST").
    /// </summary>
    public required string Region { get; set; }

    /// <summary>
    /// Server tags for targeted deployments.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Current configuration ID deployed to this server.
    /// </summary>
    public string? CurrentConfigId { get; set; }

    /// <summary>
    /// Configuration version.
    /// </summary>
    public string? ConfigVersion { get; set; }

    /// <summary>
    /// Current player count on this server.
    /// </summary>
    public int PlayerCount { get; set; } = 0;

    /// <summary>
    /// Maximum player capacity.
    /// </summary>
    public int MaxPlayers { get; set; } = 100;

    /// <summary>
    /// Server health status.
    /// </summary>
    public ServerHealth Health { get; set; } = new();

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Server startup timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Server registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if the server is healthy.
    /// </summary>
    public bool IsHealthy()
    {
        // Unhealthy if no heartbeat in last 2 minutes
        if (DateTime.UtcNow - LastHeartbeat > TimeSpan.FromMinutes(2))
            return false;

        return Health.IsHealthy;
    }

    /// <summary>
    /// Checks if the server is available for new deployments.
    /// </summary>
    public bool IsAvailable()
    {
        return IsHealthy() && PlayerCount < MaxPlayers * 0.9; // 90% capacity threshold
    }

    /// <summary>
    /// Updates the heartbeat timestamp.
    /// </summary>
    public void RecordHeartbeat()
    {
        LastHeartbeat = DateTime.UtcNow;
    }
}

/// <summary>
/// Server health information.
/// </summary>
public class ServerHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsage { get; set; } = 0;

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsage { get; set; } = 0;

    /// <summary>
    /// Average FPS (frames per second).
    /// </summary>
    public double AvgFps { get; set; } = 60;

    /// <summary>
    /// Server tick rate.
    /// </summary>
    public double TickRate { get; set; } = 64;

    /// <summary>
    /// Average network latency (milliseconds).
    /// </summary>
    public double AvgLatencyMs { get; set; } = 50;
}
```

---

## LiveEvent

Represents a time-limited live event.

**File:** `src/HotSwap.Gaming.Domain/Models/LiveEvent.cs`

```csharp
namespace HotSwap.Gaming.Domain.Models;

/// <summary>
/// Represents a live event with time-limited configuration.
/// </summary>
public class LiveEvent
{
    /// <summary>
    /// Unique event identifier.
    /// </summary>
    public required string EventId { get; set; }

    /// <summary>
    /// Event name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Game/project identifier.
    /// </summary>
    public required string GameId { get; set; }

    /// <summary>
    /// Event description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Event configuration ID.
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Event start time (UTC).
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Event end time (UTC).
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Target regions for the event.
    /// </summary>
    public List<string> TargetRegions { get; set; } = new();

    /// <summary>
    /// Event status (Scheduled, Active, Completed, Cancelled).
    /// </summary>
    public EventStatus Status { get; set; } = EventStatus.Scheduled;

    /// <summary>
    /// Whether the event recurs.
    /// </summary>
    public bool IsRecurring { get; set; } = false;

    /// <summary>
    /// Recurrence pattern (e.g., "daily", "weekly").
    /// </summary>
    public string? RecurrencePattern { get; set; }

    /// <summary>
    /// Event-specific rewards configuration.
    /// </summary>
    public string? RewardsConfig { get; set; }

    /// <summary>
    /// Event creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the event.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Event activation timestamp (UTC).
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>
    /// Event deactivation timestamp (UTC).
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Player participation count.
    /// </summary>
    public long ParticipationCount { get; set; } = 0;

    /// <summary>
    /// Checks if the event is currently active.
    /// </summary>
    public bool IsActive()
    {
        var now = DateTime.UtcNow;
        return Status == EventStatus.Active && now >= StartTime && now <= EndTime;
    }

    /// <summary>
    /// Checks if the event should start.
    /// </summary>
    public bool ShouldStart()
    {
        return Status == EventStatus.Scheduled && DateTime.UtcNow >= StartTime;
    }

    /// <summary>
    /// Checks if the event should end.
    /// </summary>
    public bool ShouldEnd()
    {
        return Status == EventStatus.Active && DateTime.UtcNow >= EndTime;
    }

    /// <summary>
    /// Validates the event configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(EventId))
            errors.Add("EventId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(GameId))
            errors.Add("GameId is required");

        if (string.IsNullOrWhiteSpace(ConfigId))
            errors.Add("ConfigId is required");

        if (EndTime <= StartTime)
            errors.Add("EndTime must be after StartTime");

        if (StartTime < DateTime.UtcNow.AddMinutes(-5)) // Allow 5 minute buffer
            errors.Add("StartTime cannot be in the past");

        return errors.Count == 0;
    }
}
```

---

## PlayerMetrics

Represents aggregated player metrics for monitoring.

**File:** `src/HotSwap.Gaming.Domain/Models/PlayerMetrics.cs`

```csharp
namespace HotSwap.Gaming.Domain.Models;

/// <summary>
/// Represents aggregated player metrics for a time window.
/// </summary>
public class PlayerMetrics
{
    /// <summary>
    /// Deployment ID these metrics are associated with.
    /// </summary>
    public string? DeploymentId { get; set; }

    /// <summary>
    /// Server or region these metrics represent.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Metrics timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time window for aggregation (ISO 8601 duration).
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);

    // Engagement Metrics

    /// <summary>
    /// Number of active players.
    /// </summary>
    public long ActivePlayers { get; set; }

    /// <summary>
    /// Average sessions per player.
    /// </summary>
    public double AvgSessionsPerPlayer { get; set; }

    /// <summary>
    /// Average session duration (minutes).
    /// </summary>
    public double AvgSessionDurationMinutes { get; set; }

    /// <summary>
    /// Day 1 retention rate (percent).
    /// </summary>
    public double RetentionD1 { get; set; }

    /// <summary>
    /// Day 7 retention rate (percent).
    /// </summary>
    public double RetentionD7 { get; set; }

    /// <summary>
    /// Engagement score (0-100).
    /// </summary>
    public double EngagementScore { get; set; }

    // Satisfaction Metrics

    /// <summary>
    /// Player churn rate (percent).
    /// </summary>
    public double ChurnRate { get; set; }

    /// <summary>
    /// Number of player complaints.
    /// </summary>
    public long PlayerComplaints { get; set; }

    /// <summary>
    /// Average player rating (1-5 stars).
    /// </summary>
    public double AvgPlayerRating { get; set; }

    /// <summary>
    /// Social sentiment score (-100 to +100).
    /// </summary>
    public double SentimentScore { get; set; }

    // Performance Metrics

    /// <summary>
    /// Average FPS (frames per second).
    /// </summary>
    public double AvgFps { get; set; }

    /// <summary>
    /// P95 network latency (milliseconds).
    /// </summary>
    public double P95LatencyMs { get; set; }

    /// <summary>
    /// Crash rate (percent).
    /// </summary>
    public double CrashRate { get; set; }

    /// <summary>
    /// Average server tick rate.
    /// </summary>
    public double AvgTickRate { get; set; }

    // Monetization Metrics

    /// <summary>
    /// Revenue (in-app purchases).
    /// </summary>
    public decimal Revenue { get; set; }

    /// <summary>
    /// Conversion rate (percent).
    /// </summary>
    public double ConversionRate { get; set; }

    /// <summary>
    /// Average revenue per user.
    /// </summary>
    public decimal AvgRevenuePerUser { get; set; }

    /// <summary>
    /// Calculates engagement score from metrics.
    /// </summary>
    public void CalculateEngagementScore()
    {
        // Engagement Score = (SessionsPerPlayer × AvgSessionDuration × RetentionD7) / 100
        EngagementScore = Math.Min(100, (AvgSessionsPerPlayer * AvgSessionDurationMinutes * RetentionD7) / 100);
    }
}
```

---

## ABTest

Represents an A/B test with multiple configuration variants.

**File:** `src/HotSwap.Gaming.Domain/Models/ABTest.cs`

```csharp
namespace HotSwap.Gaming.Domain.Models;

/// <summary>
/// Represents an A/B test for comparing configuration variants.
/// </summary>
public class ABTest
{
    /// <summary>
    /// Unique test identifier.
    /// </summary>
    public required string TestId { get; set; }

    /// <summary>
    /// Test name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Game/project identifier.
    /// </summary>
    public required string GameId { get; set; }

    /// <summary>
    /// Test description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Test variants (A, B, C, etc.).
    /// </summary>
    public List<TestVariant> Variants { get; set; } = new();

    /// <summary>
    /// Test duration (ISO 8601 duration).
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Success metric to optimize (engagement, retention, revenue, etc.).
    /// </summary>
    public required string SuccessMetric { get; set; }

    /// <summary>
    /// Target improvement percentage.
    /// </summary>
    public double TargetImprovement { get; set; } = 5.0;

    /// <summary>
    /// Test status (Draft, Running, Completed, Cancelled).
    /// </summary>
    public TestStatus Status { get; set; } = TestStatus.Draft;

    /// <summary>
    /// Test start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Test completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Winning variant ID (if declared).
    /// </summary>
    public string? WinnerVariantId { get; set; }

    /// <summary>
    /// Test results summary.
    /// </summary>
    public ABTestResults? Results { get; set; }

    /// <summary>
    /// User who created the test.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Test creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the A/B test configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(TestId))
            errors.Add("TestId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (Variants.Count < 2)
            errors.Add("At least 2 variants required");

        // Validate weights sum to 100
        var totalWeight = Variants.Sum(v => v.Weight);
        if (Math.Abs(totalWeight - 100) > 0.01)
            errors.Add("Variant weights must sum to 100");

        return errors.Count == 0;
    }
}

/// <summary>
/// Represents a test variant in an A/B test.
/// </summary>
public class TestVariant
{
    /// <summary>
    /// Variant identifier (e.g., "A", "B").
    /// </summary>
    public required string VariantId { get; set; }

    /// <summary>
    /// Variant name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Configuration ID for this variant.
    /// </summary>
    public required string ConfigId { get; set; }

    /// <summary>
    /// Traffic weight (percentage, 0-100).
    /// </summary>
    public double Weight { get; set; } = 50.0;

    /// <summary>
    /// Server IDs assigned to this variant.
    /// </summary>
    public List<string> AssignedServers { get; set; } = new();

    /// <summary>
    /// Metrics for this variant.
    /// </summary>
    public PlayerMetrics? Metrics { get; set; }
}

/// <summary>
/// A/B test results and statistical analysis.
/// </summary>
public class ABTestResults
{
    /// <summary>
    /// Statistical significance (p-value).
    /// </summary>
    public double PValue { get; set; }

    /// <summary>
    /// Whether results are statistically significant (p < 0.05).
    /// </summary>
    public bool IsSignificant => PValue < 0.05;

    /// <summary>
    /// Winner variant ID (based on success metric).
    /// </summary>
    public string? WinnerVariantId { get; set; }

    /// <summary>
    /// Improvement percentage over control.
    /// </summary>
    public double ImprovementPercent { get; set; }

    /// <summary>
    /// Confidence level (percent).
    /// </summary>
    public double ConfidenceLevel { get; set; } = 95.0;

    /// <summary>
    /// Sample size (total players).
    /// </summary>
    public long SampleSize { get; set; }

    /// <summary>
    /// Results summary.
    /// </summary>
    public string? Summary { get; set; }
}
```

---

## Enumerations

### ConfigurationType

**File:** `src/HotSwap.Gaming.Domain/Enums/ConfigurationType.cs`

```csharp
namespace HotSwap.Gaming.Domain.Enums;

/// <summary>
/// Represents the type of game configuration.
/// </summary>
public enum ConfigurationType
{
    /// <summary>
    /// Game balance (weapons, characters, abilities).
    /// </summary>
    GameBalance,

    /// <summary>
    /// Economy (prices, rewards, loot tables).
    /// </summary>
    Economy,

    /// <summary>
    /// Matchmaking configuration.
    /// </summary>
    Matchmaking,

    /// <summary>
    /// Live event configuration.
    /// </summary>
    LiveEvent,

    /// <summary>
    /// Performance and graphics settings.
    /// </summary>
    Performance,

    /// <summary>
    /// Custom configuration type.
    /// </summary>
    Custom
}
```

### ConfigurationStatus

```csharp
namespace HotSwap.Gaming.Domain.Enums;

/// <summary>
/// Represents the status of a configuration.
/// </summary>
public enum ConfigurationStatus
{
    /// <summary>
    /// Configuration is in draft state.
    /// </summary>
    Draft,

    /// <summary>
    /// Configuration is pending approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Configuration is approved for deployment.
    /// </summary>
    Approved,

    /// <summary>
    /// Configuration is deprecated.
    /// </summary>
    Deprecated
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.Gaming.Domain.Enums;

/// <summary>
/// Represents the deployment strategy.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Canary deployment (10% → 30% → 50% → 100%).
    /// </summary>
    Canary,

    /// <summary>
    /// Geographic region-based deployment.
    /// </summary>
    Geographic,

    /// <summary>
    /// Blue-green deployment (instant switchover).
    /// </summary>
    BlueGreen,

    /// <summary>
    /// A/B testing deployment.
    /// </summary>
    ABTest,

    /// <summary>
    /// Direct deployment (all servers immediately).
    /// </summary>
    Direct
}
```

### DeploymentStatus

```csharp
namespace HotSwap.Gaming.Domain.Enums;

/// <summary>
/// Represents the status of a deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is pending.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Deployment was rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Deployment failed.
    /// </summary>
    Failed
}
```

### EventStatus

```csharp
namespace HotSwap.Gaming.Domain.Enums;

/// <summary>
/// Represents the status of a live event.
/// </summary>
public enum EventStatus
{
    /// <summary>
    /// Event is scheduled.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Event is currently active.
    /// </summary>
    Active,

    /// <summary>
    /// Event has completed.
    /// </summary>
    Completed,

    /// <summary>
    /// Event was cancelled.
    /// </summary>
    Cancelled
}
```

### TestStatus

```csharp
namespace HotSwap.Gaming.Domain.Enums;

/// <summary>
/// Represents the status of an A/B test.
/// </summary>
public enum TestStatus
{
    /// <summary>
    /// Test is in draft state.
    /// </summary>
    Draft,

    /// <summary>
    /// Test is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Test has completed.
    /// </summary>
    Completed,

    /// <summary>
    /// Test was cancelled.
    /// </summary>
    Cancelled
}
```

---

## Value Objects

### DeploymentMetrics

**File:** `src/HotSwap.Gaming.Domain/ValueObjects/DeploymentMetrics.cs`

```csharp
namespace HotSwap.Gaming.Domain.ValueObjects;

/// <summary>
/// Snapshot of metrics during a deployment.
/// </summary>
public class DeploymentMetrics
{
    /// <summary>
    /// Metrics before deployment (baseline).
    /// </summary>
    public PlayerMetrics Baseline { get; set; } = new();

    /// <summary>
    /// Current metrics during deployment.
    /// </summary>
    public PlayerMetrics Current { get; set; } = new();

    /// <summary>
    /// Percentage change in key metrics.
    /// </summary>
    public MetricsComparison Comparison { get; set; } = new();

    /// <summary>
    /// Last metrics update timestamp (UTC).
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Comparison of metrics between baseline and current.
/// </summary>
public class MetricsComparison
{
    /// <summary>
    /// Churn rate change (percentage points).
    /// </summary>
    public double ChurnRateChange { get; set; }

    /// <summary>
    /// Session duration change (percent).
    /// </summary>
    public double SessionDurationChangePercent { get; set; }

    /// <summary>
    /// Engagement score change (percent).
    /// </summary>
    public double EngagementChangePercent { get; set; }

    /// <summary>
    /// Crash rate change (percentage points).
    /// </summary>
    public double CrashRateChange { get; set; }

    /// <summary>
    /// Overall health score (0-100, 100 = no degradation).
    /// </summary>
    public double HealthScore { get; set; } = 100;
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.Gaming.Domain`
