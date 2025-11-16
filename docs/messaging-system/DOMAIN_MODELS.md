# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Distributed.Domain.Models`

---

## Table of Contents

1. [Message](#message)
2. [Topic](#topic)
3. [Subscription](#subscription)
4. [MessageSchema](#messageschema)
5. [BrokerNode](#brokernode)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## Message

Represents a message published to a topic.

**File:** `src/HotSwap.Distributed.Domain/Models/Message.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a message in the distributed messaging system.
/// </summary>
public class Message
{
    /// <summary>
    /// Unique message identifier (GUID format).
    /// </summary>
    public required string MessageId { get; set; }

    /// <summary>
    /// Target topic name.
    /// </summary>
    public required string TopicName { get; set; }

    /// <summary>
    /// Message payload (JSON format, max 1 MB).
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    /// Schema version used for validation (e.g., "1.0", "2.0").
    /// </summary>
    public required string SchemaVersion { get; set; }

    /// <summary>
    /// Publish timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Message metadata (key-value pairs).
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Request/reply correlation identifier (optional).
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Message priority (0-9, where 9 is highest priority).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Message expiration time (TTL). Null means no expiration.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Number of delivery attempts (incremented on retry).
    /// </summary>
    public int DeliveryAttempts { get; set; } = 0;

    /// <summary>
    /// Current message status.
    /// </summary>
    public MessageStatus Status { get; set; } = MessageStatus.Pending;

    /// <summary>
    /// Partition key for ordered delivery (optional).
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// Assigned partition number (set by broker).
    /// </summary>
    public int? Partition { get; set; }

    /// <summary>
    /// Timestamp when message was delivered to consumer (UTC).
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Timestamp when message was acknowledged (UTC).
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Acknowledgment deadline (UTC). Message requeued if not acked by this time.
    /// </summary>
    public DateTime? AckDeadline { get; set; }

    /// <summary>
    /// Validates the message for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(MessageId))
            errors.Add("MessageId is required");

        if (string.IsNullOrWhiteSpace(TopicName))
            errors.Add("TopicName is required");

        if (string.IsNullOrWhiteSpace(Payload))
            errors.Add("Payload is required");

        if (string.IsNullOrWhiteSpace(SchemaVersion))
            errors.Add("SchemaVersion is required");

        if (Priority < 0 || Priority > 9)
            errors.Add("Priority must be between 0 and 9");

        if (Payload.Length > 1048576) // 1 MB
            errors.Add("Payload exceeds maximum size of 1 MB");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the message has expired based on TTL.
    /// </summary>
    public bool IsExpired() => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}
```

---

## Topic

Represents a message topic (queue or pub/sub).

**File:** `src/HotSwap.Distributed.Domain/Models/Topic.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a messaging topic.
/// </summary>
public class Topic
{
    /// <summary>
    /// Unique topic name (alphanumeric, dots, dashes allowed).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current schema identifier for message validation.
    /// </summary>
    public required string SchemaId { get; set; }

    /// <summary>
    /// Topic type (Queue or PubSub).
    /// </summary>
    public TopicType Type { get; set; } = TopicType.PubSub;

    /// <summary>
    /// Delivery guarantee mode.
    /// </summary>
    public DeliveryGuarantee DeliveryGuarantee { get; set; } = DeliveryGuarantee.AtLeastOnce;

    /// <summary>
    /// Message retention period (default: 7 days).
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Number of partitions for parallel consumption (1-16).
    /// </summary>
    public int PartitionCount { get; set; } = 1;

    /// <summary>
    /// Replication factor for high availability (1-3).
    /// </summary>
    public int ReplicationFactor { get; set; } = 2;

    /// <summary>
    /// Topic-specific configuration settings.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>
    /// Topic creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total messages currently in the topic (all partitions).
    /// </summary>
    public long MessageCount { get; set; } = 0;

    /// <summary>
    /// Number of active subscriptions.
    /// </summary>
    public int SubscriptionCount { get; set; } = 0;

    /// <summary>
    /// Validates the topic configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z0-9._-]+$"))
            errors.Add("Name must contain only alphanumeric characters, dots, dashes, and underscores");

        if (string.IsNullOrWhiteSpace(SchemaId))
            errors.Add("SchemaId is required");

        if (PartitionCount < 1 || PartitionCount > 16)
            errors.Add("PartitionCount must be between 1 and 16");

        if (ReplicationFactor < 1 || ReplicationFactor > 3)
            errors.Add("ReplicationFactor must be between 1 and 3");

        if (RetentionPeriod < TimeSpan.FromHours(1))
            errors.Add("RetentionPeriod must be at least 1 hour");

        return errors.Count == 0;
    }

    /// <summary>
    /// Gets the dead letter queue name for this topic.
    /// </summary>
    public string GetDLQName() => $"{Name}.dlq";
}
```

---

## Subscription

Represents a consumer subscription to a topic.

**File:** `src/HotSwap.Distributed.Domain/Models/Subscription.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a subscription to a topic.
/// </summary>
public class Subscription
{
    /// <summary>
    /// Unique subscription identifier (GUID format).
    /// </summary>
    public required string SubscriptionId { get; set; }

    /// <summary>
    /// Subscribed topic name.
    /// </summary>
    public required string TopicName { get; set; }

    /// <summary>
    /// Consumer group name (unique per topic).
    /// </summary>
    public required string ConsumerGroup { get; set; }

    /// <summary>
    /// Consumer endpoint (webhook URL for Push, null for Pull).
    /// </summary>
    public required string ConsumerEndpoint { get; set; }

    /// <summary>
    /// Subscription type (Push or Pull).
    /// </summary>
    public SubscriptionType Type { get; set; } = SubscriptionType.Pull;

    /// <summary>
    /// Message filter for content-based routing (optional).
    /// </summary>
    public MessageFilter? Filter { get; set; }

    /// <summary>
    /// Maximum delivery retry attempts before moving to DLQ.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Acknowledgment timeout (message requeued if not acked).
    /// </summary>
    public TimeSpan AckTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether the subscription is active (paused if false).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Subscription creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last message consumption timestamp (UTC).
    /// </summary>
    public DateTime? LastConsumedAt { get; set; }

    /// <summary>
    /// Total messages consumed by this subscription.
    /// </summary>
    public long MessagesConsumed { get; set; } = 0;

    /// <summary>
    /// Total messages that failed delivery.
    /// </summary>
    public long MessagesFailed { get; set; } = 0;

    /// <summary>
    /// Validates the subscription configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SubscriptionId))
            errors.Add("SubscriptionId is required");

        if (string.IsNullOrWhiteSpace(TopicName))
            errors.Add("TopicName is required");

        if (string.IsNullOrWhiteSpace(ConsumerGroup))
            errors.Add("ConsumerGroup is required");

        if (Type == SubscriptionType.Push && string.IsNullOrWhiteSpace(ConsumerEndpoint))
            errors.Add("ConsumerEndpoint is required for Push subscriptions");

        if (Type == SubscriptionType.Push && ConsumerEndpoint != null && !Uri.IsWellFormedUriString(ConsumerEndpoint, UriKind.Absolute))
            errors.Add("ConsumerEndpoint must be a valid absolute URL");

        if (MaxRetries < 0 || MaxRetries > 10)
            errors.Add("MaxRetries must be between 0 and 10");

        if (AckTimeout < TimeSpan.FromSeconds(1) || AckTimeout > TimeSpan.FromMinutes(10))
            errors.Add("AckTimeout must be between 1 second and 10 minutes");

        return errors.Count == 0;
    }
}

/// <summary>
/// Message filter for content-based routing.
/// </summary>
public class MessageFilter
{
    /// <summary>
    /// Header key-value matches (all must match).
    /// </summary>
    public Dictionary<string, string> HeaderMatches { get; set; } = new();

    /// <summary>
    /// JSONPath query for payload filtering (optional).
    /// </summary>
    public string? PayloadQuery { get; set; }

    /// <summary>
    /// Evaluates whether a message matches this filter.
    /// </summary>
    public bool Matches(Message message)
    {
        // Check header matches
        foreach (var (key, value) in HeaderMatches)
        {
            if (!message.Headers.TryGetValue(key, out var messageValue) || messageValue != value)
                return false;
        }

        // Note: JSONPath query evaluation for PayloadQuery will be implemented in Epic 2
        // Current implementation: match succeeds if no payload query specified
        return string.IsNullOrWhiteSpace(PayloadQuery);
    }
}
```

---

## MessageSchema

Represents a message schema for validation.

**File:** `src/HotSwap.Distributed.Domain/Models/MessageSchema.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a message schema for validation and evolution.
/// </summary>
public class MessageSchema
{
    /// <summary>
    /// Unique schema identifier (e.g., "deployment.event.v1").
    /// </summary>
    public required string SchemaId { get; set; }

    /// <summary>
    /// JSON Schema definition (JSON format).
    /// </summary>
    public required string SchemaDefinition { get; set; }

    /// <summary>
    /// Schema version number (e.g., "1.0", "2.0").
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Schema compatibility mode.
    /// </summary>
    public SchemaCompatibility Compatibility { get; set; } = SchemaCompatibility.None;

    /// <summary>
    /// Current schema status (approval workflow).
    /// </summary>
    public SchemaStatus Status { get; set; } = SchemaStatus.Draft;

    /// <summary>
    /// Admin user who approved the schema (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Schema creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Schema deprecation timestamp (UTC).
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// Deprecation reason (if deprecated).
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Migration guide URL (if deprecated).
    /// </summary>
    public string? MigrationGuide { get; set; }

    /// <summary>
    /// Validates the schema configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SchemaId))
            errors.Add("SchemaId is required");

        if (string.IsNullOrWhiteSpace(SchemaDefinition))
            errors.Add("SchemaDefinition is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        // Validate JSON Schema format
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(SchemaDefinition);
        }
        catch
        {
            errors.Add("SchemaDefinition must be valid JSON");
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the schema is approved for production use.
    /// </summary>
    public bool IsApproved() => Status == SchemaStatus.Approved;

    /// <summary>
    /// Checks if the schema is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == SchemaStatus.Deprecated;
}
```

---

## BrokerNode

Represents a message broker node.

**File:** `src/HotSwap.Distributed.Domain/Models/BrokerNode.cs`

```csharp
namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a message broker node in the cluster.
/// </summary>
public class BrokerNode
{
    /// <summary>
    /// Unique node identifier (e.g., "broker-1").
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// Broker hostname or IP address.
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    /// Broker port number.
    /// </summary>
    public int Port { get; set; } = 5050;

    /// <summary>
    /// Broker role (Master or Replica).
    /// </summary>
    public BrokerRole Role { get; set; } = BrokerRole.Replica;

    /// <summary>
    /// Topics assigned to this broker node.
    /// </summary>
    public List<string> AssignedTopics { get; set; } = new();

    /// <summary>
    /// Current health status.
    /// </summary>
    public BrokerHealth Health { get; set; } = new();

    /// <summary>
    /// Performance metrics.
    /// </summary>
    public BrokerMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Broker startup timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if the broker is healthy based on heartbeat and health checks.
    /// </summary>
    public bool IsHealthy()
    {
        // Unhealthy if no heartbeat in last 2 minutes
        if (DateTime.UtcNow - LastHeartbeat > TimeSpan.FromMinutes(2))
            return false;

        return Health.IsHealthy;
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
/// Broker health information.
/// </summary>
public class BrokerHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Total messages queued across all topics.
    /// </summary>
    public int QueueDepth { get; set; } = 0;

    /// <summary>
    /// Number of active consumers connected to this broker.
    /// </summary>
    public int ActiveConsumers { get; set; } = 0;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsage { get; set; } = 0;

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsage { get; set; } = 0;
}

/// <summary>
/// Broker performance metrics.
/// </summary>
public class BrokerMetrics
{
    /// <summary>
    /// Total messages published to this broker.
    /// </summary>
    public long MessagesPublished { get; set; } = 0;

    /// <summary>
    /// Total messages delivered by this broker.
    /// </summary>
    public long MessagesDelivered { get; set; } = 0;

    /// <summary>
    /// Total failed message deliveries.
    /// </summary>
    public long MessagesFailed { get; set; } = 0;

    /// <summary>
    /// Average publish latency in milliseconds.
    /// </summary>
    public double AvgPublishLatencyMs { get; set; } = 0;

    /// <summary>
    /// Average delivery latency in milliseconds.
    /// </summary>
    public double AvgDeliveryLatencyMs { get; set; } = 0;

    /// <summary>
    /// Current throughput in messages per second.
    /// </summary>
    public double ThroughputMsgPerSec { get; set; } = 0;
}
```

---

## Enumerations

### MessageStatus

**File:** `src/HotSwap.Distributed.Domain/Enums/MessageStatus.cs`

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the current status of a message.
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// Message is queued, awaiting delivery.
    /// </summary>
    Pending,

    /// <summary>
    /// Message has been delivered to consumer.
    /// </summary>
    Delivered,

    /// <summary>
    /// Consumer has acknowledged successful processing.
    /// </summary>
    Acknowledged,

    /// <summary>
    /// Delivery failed after max retries (moved to DLQ).
    /// </summary>
    Failed,

    /// <summary>
    /// Message expired before delivery (TTL exceeded).
    /// </summary>
    Expired
}
```

### TopicType

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the type of a topic.
/// </summary>
public enum TopicType
{
    /// <summary>
    /// Point-to-point queue (single consumer per message).
    /// </summary>
    Queue,

    /// <summary>
    /// Publish-subscribe topic (all consumers receive message).
    /// </summary>
    PubSub
}
```

### DeliveryGuarantee

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the delivery guarantee mode for a topic.
/// </summary>
public enum DeliveryGuarantee
{
    /// <summary>
    /// Fire-and-forget (no retry, may lose messages).
    /// </summary>
    AtMostOnce,

    /// <summary>
    /// Retry until acknowledged (may deliver duplicates).
    /// </summary>
    AtLeastOnce,

    /// <summary>
    /// Exactly-once delivery (no duplicates, uses distributed locks).
    /// </summary>
    ExactlyOnce
}
```

### SubscriptionType

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the type of a subscription.
/// </summary>
public enum SubscriptionType
{
    /// <summary>
    /// Broker pushes messages to consumer (webhook).
    /// </summary>
    Push,

    /// <summary>
    /// Consumer polls for messages (HTTP GET).
    /// </summary>
    Pull
}
```

### SchemaCompatibility

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents schema compatibility mode.
/// </summary>
public enum SchemaCompatibility
{
    /// <summary>
    /// No compatibility checks.
    /// </summary>
    None,

    /// <summary>
    /// New schema can read old data.
    /// </summary>
    Backward,

    /// <summary>
    /// Old schema can read new data.
    /// </summary>
    Forward,

    /// <summary>
    /// Bidirectional compatibility (both backward and forward).
    /// </summary>
    Full
}
```

### SchemaStatus

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the status of a schema in the approval workflow.
/// </summary>
public enum SchemaStatus
{
    /// <summary>
    /// Schema is in draft state (not yet submitted for approval).
    /// </summary>
    Draft,

    /// <summary>
    /// Schema is pending admin approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Schema is approved for production use.
    /// </summary>
    Approved,

    /// <summary>
    /// Schema is deprecated (marked for removal).
    /// </summary>
    Deprecated
}
```

### BrokerRole

```csharp
namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the role of a broker node.
/// </summary>
public enum BrokerRole
{
    /// <summary>
    /// Master node (handles writes and coordination).
    /// </summary>
    Master,

    /// <summary>
    /// Replica node (handles reads and provides redundancy).
    /// </summary>
    Replica
}
```

---

## Value Objects

### RouteResult

**File:** `src/HotSwap.Distributed.Domain/ValueObjects/RouteResult.cs`

```csharp
namespace HotSwap.Distributed.Domain.ValueObjects;

/// <summary>
/// Result of a message routing operation.
/// </summary>
public class RouteResult
{
    /// <summary>
    /// Whether routing was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// List of consumers that received the message.
    /// </summary>
    public List<string> TargetConsumers { get; private set; } = new();

    /// <summary>
    /// Error message (if routing failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Routing timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static RouteResult SuccessResult(List<string> consumers)
    {
        return new RouteResult
        {
            Success = true,
            TargetConsumers = consumers
        };
    }

    public static RouteResult Failure(string errorMessage)
    {
        return new RouteResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
```

---

## Validation Examples

### Message Validation

```csharp
var message = new Message
{
    MessageId = Guid.NewGuid().ToString(),
    TopicName = "deployment.events",
    Payload = "{\"event\":\"deployment.completed\"}",
    SchemaVersion = "1.0"
};

if (!message.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Topic Validation

```csharp
var topic = new Topic
{
    Name = "deployment.events",
    SchemaId = "deployment.event.v1",
    Type = TopicType.PubSub,
    PartitionCount = 4,
    ReplicationFactor = 2
};

if (!topic.IsValid(out var errors))
{
    Console.WriteLine("Topic validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-16
**Namespace:** `HotSwap.Distributed.Domain`
