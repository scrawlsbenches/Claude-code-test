# Message Routing Strategies Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-16

---

## Overview

The HotSwap Messaging System supports 5 routing strategies, each adapted from the existing deployment strategies in the kernel orchestration platform. Routing strategies determine how messages are delivered from brokers to consumers.

### Strategy Selection

| Topic Type | Default Strategy | Use Case |
|------------|------------------|----------|
| Queue | Load-Balanced | Command processing, work distribution |
| PubSub | Fan-Out | Event notifications, broadcasts |
| Custom | Any | Application-specific requirements |

---

## 1. Direct Routing Strategy

### Overview

**Based On:** Direct Deployment Strategy
**Pattern:** Point-to-Point
**Latency:** Lowest
**Complexity:** Low

**Use Case:** Single consumer scenarios where minimizing latency is critical.

### Behavior

- Routes message to **first available consumer**
- No load balancing
- Fastest delivery path
- Used for request-reply patterns

### Algorithm

```csharp
public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
{
    if (consumers.Count == 0)
        return RouteResult.Failure("No consumers available");

    var target = consumers.First(); // Direct to first consumer
    await DeliverAsync(message, target);
    return RouteResult.Success(new[] { target });
}
```

### Message Flow

```
Producer → [Queue] → Consumer1 (always receives all messages)
                  → Consumer2 (never receives messages)
                  → Consumer3 (never receives messages)
```

### Configuration

```json
{
  "topicName": "health.check.requests",
  "routingStrategy": "Direct",
  "config": {
    "primaryConsumer": "health-checker-1"
  }
}
```

### Performance Characteristics

- **Latency:** ~5ms (lowest)
- **Throughput:** Limited to single consumer capacity
- **Scalability:** No horizontal scaling
- **Fault Tolerance:** None (single point of failure)

### When to Use

✅ **Good For:**
- Request-reply (RPC-style) communication
- Single consumer per topic
- Latency-sensitive operations
- Development/testing environments

❌ **Not Good For:**
- High-throughput scenarios
- Load balancing requirements
- Multi-consumer scenarios
- Production workloads

---

## 2. Fan-Out Routing Strategy

### Overview

**Based On:** Direct Deployment (all nodes simultaneously)
**Pattern:** Publish-Subscribe
**Latency:** Medium
**Complexity:** Medium

**Use Case:** Event broadcasting where all subscribers must receive the message.

### Behavior

- Routes message to **ALL active consumers** in parallel
- Broadcast pattern
- Each consumer group receives independent copy
- No acknowledgment required (fire-and-forget or at-least-once)

### Algorithm

```csharp
public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
{
    if (consumers.Count == 0)
        return RouteResult.Failure("No consumers available");

    // Deliver to ALL consumers in parallel
    var deliveryTasks = consumers.Select(c => DeliverAsync(message, c));
    await Task.WhenAll(deliveryTasks);

    return RouteResult.Success(consumers);
}
```

### Message Flow

```
Producer → [Topic] → Consumer1 (receives message)
                  → Consumer2 (receives message)
                  → Consumer3 (receives message)

All consumers receive the same message simultaneously.
```

### Configuration

```json
{
  "topicName": "deployment.events",
  "type": "PubSub",
  "routingStrategy": "FanOut",
  "deliveryGuarantee": "AtLeastOnce"
}
```

### Performance Characteristics

- **Latency:** ~20ms (parallel delivery)
- **Throughput:** N consumers × single consumer throughput
- **Scalability:** Limited by broker capacity
- **Fault Tolerance:** Partial failure handling (some consumers may fail)

### Error Handling

**Partial Failures:**
- Some consumers succeed, some fail
- Failed consumers retried independently
- Successful deliveries not rolled back

**Example:**
```
Delivery Results:
- Consumer1: Success ✓
- Consumer2: Failed ✗ (retry)
- Consumer3: Success ✓

Overall Result: Partial Success
```

### When to Use

✅ **Good For:**
- Event notifications (deployment completed, user registered)
- System-wide broadcasts
- Pub/sub messaging patterns
- Audit log distribution
- Real-time notifications

❌ **Not Good For:**
- Command processing (use Queue instead)
- Load balancing requirements
- Exactly-once semantics (use with caution)

---

## 3. Load-Balanced Routing Strategy

### Overview

**Based On:** Rolling Deployment Strategy
**Pattern:** Work Queue
**Latency:** Low
**Complexity:** Medium

**Use Case:** Distribute workload evenly across multiple consumers.

### Behavior

- Routes each message to **one consumer** using round-robin
- Even load distribution
- Maximizes throughput
- Consumer group isolation

### Algorithm

```csharp
public class LoadBalancedRoutingStrategy : IRoutingStrategy
{
    private int _currentIndex = 0;
    private readonly object _lock = new object();

    public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
    {
        if (consumers.Count == 0)
            return RouteResult.Failure("No consumers available");

        // Thread-safe round-robin selection
        int index;
        lock (_lock)
        {
            index = _currentIndex % consumers.Count;
            _currentIndex++;
        }

        var target = consumers[index];
        await DeliverAsync(message, target);
        return RouteResult.Success(new[] { target });
    }
}
```

### Message Flow

```
Producer → [Queue] → Consumer1 (msg1, msg4, msg7)
                  → Consumer2 (msg2, msg5, msg8)
                  → Consumer3 (msg3, msg6, msg9)

Round-robin distribution.
```

### Advanced: Least-Loaded Algorithm

For uneven consumer capacity, use least-loaded algorithm:

```csharp
public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
{
    // Select consumer with lowest queue depth
    var target = consumers
        .OrderBy(c => c.CurrentQueueDepth)
        .ThenBy(c => c.LastDeliveryTime) // Tie-breaker
        .First();

    await DeliverAsync(message, target);
    return RouteResult.Success(new[] { target });
}
```

### Configuration

```json
{
  "topicName": "deployment.commands",
  "type": "Queue",
  "routingStrategy": "LoadBalanced",
  "config": {
    "algorithm": "RoundRobin",  // or "LeastLoaded"
    "rebalanceOnConsumerChange": true
  }
}
```

### Performance Characteristics

- **Latency:** ~10ms (single consumer delivery)
- **Throughput:** N consumers × single consumer throughput
- **Scalability:** Excellent (linear with consumer count)
- **Fault Tolerance:** Automatic redistribution on consumer failure

### When to Use

✅ **Good For:**
- Command processing queues
- Work distribution (image processing, data transformation)
- High-throughput scenarios
- Horizontal scaling requirements
- Production workloads

❌ **Not Good For:**
- Broadcast requirements (use Fan-Out)
- Ordered message processing (use partitions instead)
- Single consumer scenarios (use Direct)

---

## 4. Priority Routing Strategy

### Overview

**Based On:** Canary Deployment (gradual rollout)
**Pattern:** Priority Queue
**Latency:** Variable
**Complexity:** High

**Use Case:** Process high-priority messages before low-priority ones.

### Behavior

- Messages queued by priority (0-9, 9 highest)
- High-priority messages delivered first
- Same-priority messages delivered FIFO
- Prevents starvation (configurable)

### Algorithm

```csharp
public class PriorityRoutingStrategy : IRoutingStrategy
{
    private readonly PriorityQueue<Message, int> _queue = new();

    public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
    {
        if (consumers.Count == 0)
            return RouteResult.Failure("No consumers available");

        // Sort consumers by priority capacity (high-priority consumers first)
        var sortedConsumers = consumers
            .Where(c => c.CanHandlePriority(message.Priority))
            .OrderByDescending(c => c.MaxPriority)
            .ToList();

        if (sortedConsumers.Count == 0)
            return RouteResult.Failure("No consumers can handle this priority level");

        var target = sortedConsumers.First();
        await DeliverAsync(message, target);
        return RouteResult.Success(new[] { target });
    }
}
```

### Message Flow

```
Priority Queue:
[P9: Critical Alert]     → Delivered first
[P7: High Priority]      → Delivered second
[P5: Normal Priority]    → Delivered third
[P5: Normal Priority]    → Delivered fourth (FIFO within priority)
[P0: Low Priority]       → Delivered last

Consumer receives in priority order, not arrival order.
```

### Priority Levels

| Priority | Label | Use Case | SLA |
|----------|-------|----------|-----|
| 9 | Critical | System alerts, health failures | < 1s |
| 7-8 | High | User-facing operations | < 5s |
| 4-6 | Normal | Standard processing | < 30s |
| 1-3 | Low | Background tasks, analytics | < 5m |
| 0 | Deferred | Cleanup, archival | Best effort |

### Configuration

```json
{
  "topicName": "processing.queue",
  "routingStrategy": "Priority",
  "config": {
    "enableStarvationPrevention": true,
    "maxWaitTimeForLowPriority": "PT5M",  // 5 minutes
    "priorityThresholds": {
      "critical": 9,
      "high": 7,
      "normal": 5,
      "low": 3
    }
  }
}
```

### Starvation Prevention

Prevent low-priority messages from waiting indefinitely:

```csharp
// Boost priority after max wait time
if (DateTime.UtcNow - message.Timestamp > MaxWaitTime)
{
    message.Priority = Math.Min(message.Priority + 2, 9);
    _logger.LogInformation("Boosted priority for message {MessageId} to {Priority}",
        message.MessageId, message.Priority);
}
```

### Performance Characteristics

- **Latency:** Variable (P9: ~5ms, P0: minutes)
- **Throughput:** Same as single consumer (no parallelization within queue)
- **Scalability:** Limited (priority queue is sequential)
- **Fault Tolerance:** Retry with priority preservation

### When to Use

✅ **Good For:**
- SLA-sensitive operations
- Mixed workload priorities (alerts + background tasks)
- Health check processing
- User-facing vs background tasks

❌ **Not Good For:**
- All messages have same priority (use Load-Balanced)
- High-throughput scenarios (use Load-Balanced with partitions)
- Simple queue requirements

---

## 5. Content-Based Routing Strategy

### Overview

**Based On:** Blue-Green Deployment (environment selection)
**Pattern:** Message Filter
**Latency:** Medium
**Complexity:** High

**Use Case:** Route messages to consumers based on message content or headers.

### Behavior

- Evaluates message against consumer filters
- Routes to consumers with matching filters
- Supports header-based and payload-based filtering
- Multiple consumers may match

### Algorithm

```csharp
public class ContentBasedRoutingStrategy : IRoutingStrategy
{
    public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
    {
        // Filter consumers by subscription filters
        var matchingConsumers = consumers
            .Where(c => c.Subscription.Filter?.Matches(message) ?? true)
            .ToList();

        if (matchingConsumers.Count == 0)
            return RouteResult.Failure("No consumers match filter criteria");

        // Deliver to all matching consumers
        await Task.WhenAll(matchingConsumers.Select(c => DeliverAsync(message, c)));
        return RouteResult.Success(matchingConsumers);
    }
}
```

### Message Flow

```
Producer → [Topic: deployment.events]
  ├─ Consumer1 (filter: environment=Production) → msg1, msg3
  ├─ Consumer2 (filter: event=deployment.failed) → msg2
  └─ Consumer3 (filter: priority>=7) → msg1, msg2

Messages routed based on filter criteria.
```

### Filter Types

#### 1. Header-Based Filtering

Filter on message headers:

```json
{
  "filter": {
    "headerMatches": {
      "environment": "Production",
      "region": "us-east-1"
    }
  }
}
```

**Matching Logic:**
- ALL header matches must match (AND logic)
- Header values are case-sensitive
- Missing headers = no match

#### 2. Payload-Based Filtering

Filter on message payload using JSONPath:

```json
{
  "filter": {
    "payloadQuery": "$.event == 'deployment.completed' && $.status == 'success'"
  }
}
```

**Supported Operators:**
- `==` (equals)
- `!=` (not equals)
- `>`, `<`, `>=`, `<=` (comparisons)
- `&&` (AND), `||` (OR)
- `contains()` (substring match)

#### 3. Combined Filtering

Combine header and payload filters:

```json
{
  "filter": {
    "headerMatches": {
      "environment": "Production"
    },
    "payloadQuery": "$.severity >= 7"
  }
}
```

### Configuration

```json
{
  "topicName": "deployment.events",
  "routingStrategy": "ContentBased",
  "subscriptions": [
    {
      "consumerGroup": "production-monitors",
      "filter": {
        "headerMatches": {
          "environment": "Production"
        }
      }
    },
    {
      "consumerGroup": "failure-alerts",
      "filter": {
        "payloadQuery": "$.status == 'failed'"
      }
    }
  ]
}
```

### Performance Characteristics

- **Latency:** ~15ms (filter evaluation overhead)
- **Throughput:** Depends on filter complexity
- **Scalability:** Good (filters evaluated in parallel)
- **Fault Tolerance:** Partial failure handling

### Filter Performance Optimization

**Best Practices:**
1. Use header filters when possible (faster than payload filters)
2. Avoid complex JSONPath queries
3. Cache filter evaluation results for duplicate messages
4. Index frequently used header keys

**Performance:**
```
Header Filter Evaluation:    ~1ms
Simple Payload Filter:       ~3ms
Complex JSONPath Query:      ~10ms
```

### When to Use

✅ **Good For:**
- Multi-environment deployments (Production, Staging, QA)
- Error-specific consumers (errors, warnings, info)
- Regional routing (route by region/datacenter)
- A/B testing (route by user segment)

❌ **Not Good For:**
- Simple routing requirements (use Fan-Out or Load-Balanced)
- High-frequency message evaluation (filter overhead)
- Static routing patterns

---

## Strategy Comparison

| Strategy | Latency | Throughput | Scalability | Complexity | Use Case |
|----------|---------|------------|-------------|------------|----------|
| Direct | ⭐⭐⭐⭐⭐ | ⭐ | ⭐ | ⭐ | Request-reply |
| Fan-Out | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | Event broadcast |
| Load-Balanced | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | Work distribution |
| Priority | ⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ | SLA-sensitive |
| Content-Based | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Complex routing |

---

## Custom Routing Strategies

To implement a custom routing strategy:

### 1. Implement IRoutingStrategy Interface

```csharp
public class CustomRoutingStrategy : IRoutingStrategy
{
    public string Name => "Custom";

    public async Task<RouteResult> RouteAsync(Message message, List<Consumer> consumers)
    {
        // Your custom routing logic here
        var target = SelectConsumer(message, consumers);
        await DeliverAsync(message, target);
        return RouteResult.Success(new[] { target });
    }

    private Consumer SelectConsumer(Message message, List<Consumer> consumers)
    {
        // Custom selection algorithm
        // Example: Hash-based partitioning
        var hash = HashCode.Combine(message.PartitionKey ?? message.MessageId);
        var index = Math.Abs(hash) % consumers.Count;
        return consumers[index];
    }
}
```

### 2. Register Strategy

```csharp
// In Program.cs
services.AddSingleton<IRoutingStrategy, CustomRoutingStrategy>();
```

### 3. Configure Topic

```json
{
  "topicName": "custom.topic",
  "routingStrategy": "Custom",
  "config": {
    "customParameter": "value"
  }
}
```

---

## Best Practices

### 1. Choose the Right Strategy

**Decision Tree:**
```
Is message a command or event?
├─ Command (single consumer) → Load-Balanced or Direct
└─ Event (broadcast) → Fan-Out

Do you have mixed priorities?
└─ Yes → Priority

Do you need content-based routing?
└─ Yes → Content-Based

Default: Load-Balanced (Queue) or Fan-Out (PubSub)
```

### 2. Monitor Strategy Performance

Track metrics per strategy:
- Message routing latency
- Consumer selection time
- Filter evaluation time
- Delivery success rate

### 3. Test Strategy Switching

Ensure zero message loss when changing strategies:
1. Drain existing queue
2. Pause message production
3. Switch routing strategy
4. Resume message production

### 4. Optimize for Your Workload

- **High Throughput:** Load-Balanced with partitions
- **Low Latency:** Direct
- **Mixed Workload:** Priority
- **Complex Requirements:** Content-Based

---

## Troubleshooting

### Issue: Uneven Load Distribution

**Symptom:** Some consumers overloaded, others idle

**Solutions:**
1. Switch from Round-Robin to Least-Loaded algorithm
2. Add partitions to distribute load
3. Monitor consumer lag and rebalance

### Issue: High Routing Latency

**Symptom:** Slow message delivery

**Solutions:**
1. Simplify Content-Based filters (use header filters)
2. Cache filter evaluation results
3. Switch to simpler strategy (Load-Balanced)
4. Add consumer capacity

### Issue: Message Loss During Consumer Failure

**Symptom:** Messages lost when consumer crashes

**Solutions:**
1. Enable at-least-once delivery guarantee
2. Configure retry with exponential backoff
3. Enable dead letter queue
4. Monitor consumer health proactively

---

**Last Updated:** 2025-11-16
**Version:** 1.0.0
