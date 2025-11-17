using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace HotSwap.Distributed.Infrastructure.Metrics;

/// <summary>
/// Provides comprehensive Prometheus-compatible metrics for messaging operations.
/// Tracks message throughput, latency, queue depth, and consumer lag.
/// </summary>
public class MessageMetricsProvider : IDisposable
{
    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _messagesPublished;
    private readonly Counter<long> _messagesDelivered;
    private readonly Counter<long> _messagesFailed;

    // Histograms
    private readonly Histogram<double> _publishDuration;
    private readonly Histogram<double> _deliveryDuration;

    // Gauge state (ObservableGauge reads from these)
    private readonly ConcurrentDictionary<string, long> _queueDepthByTopic;
    private readonly ConcurrentDictionary<string, long> _consumerLagByTopic;

    public const string ServiceName = "HotSwap.Messaging.Metrics";
    public const string ServiceVersion = "1.0.0";

    public MessageMetricsProvider()
    {
        _meter = new Meter(ServiceName, ServiceVersion);

        _queueDepthByTopic = new ConcurrentDictionary<string, long>();
        _consumerLagByTopic = new ConcurrentDictionary<string, long>();

        // Initialize counters
        _messagesPublished = _meter.CreateCounter<long>(
            "messages.published.total",
            description: "Total number of messages published");

        _messagesDelivered = _meter.CreateCounter<long>(
            "messages.delivered.total",
            description: "Total number of messages delivered to consumers");

        _messagesFailed = _meter.CreateCounter<long>(
            "messages.failed.total",
            description: "Total number of failed message deliveries");

        // Initialize histograms
        _publishDuration = _meter.CreateHistogram<double>(
            "message.publish.duration",
            unit: "milliseconds",
            description: "Message publish duration in milliseconds");

        _deliveryDuration = _meter.CreateHistogram<double>(
            "message.delivery.duration",
            unit: "milliseconds",
            description: "Message delivery duration in milliseconds");

        // Initialize observable gauges
        _meter.CreateObservableGauge(
            "queue.depth",
            ObserveQueueDepth,
            description: "Current number of messages in queue per topic");

        _meter.CreateObservableGauge(
            "consumer.lag",
            ObserveConsumerLag,
            description: "Consumer lag (messages behind) per topic");
    }

    #region Counter Methods

    /// <summary>
    /// Increments the published message counter.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    public void IncrementPublished(string topicName)
    {
        _messagesPublished.Add(1, new KeyValuePair<string, object?>("topic.name", topicName));
    }

    /// <summary>
    /// Increments the delivered message counter.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    /// <param name="priority">Message priority (optional).</param>
    public void IncrementDelivered(string topicName, int? priority = null)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("topic.name", topicName)
        };

        if (priority.HasValue)
        {
            tags.Add(new("priority", priority.Value));
        }

        _messagesDelivered.Add(1, tags.ToArray());
    }

    /// <summary>
    /// Increments the failed message counter.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    /// <param name="reason">Failure reason.</param>
    public void IncrementFailed(string topicName, string reason)
    {
        _messagesFailed.Add(1,
            new KeyValuePair<string, object?>("topic.name", topicName),
            new KeyValuePair<string, object?>("failure.reason", reason));
    }

    #endregion

    #region Histogram Methods

    /// <summary>
    /// Records message publish duration.
    /// </summary>
    /// <param name="duration">Time taken to publish the message.</param>
    /// <param name="topicName">The topic name.</param>
    public void RecordPublishDuration(TimeSpan duration, string topicName)
    {
        _publishDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("topic.name", topicName));
    }

    /// <summary>
    /// Records message delivery duration.
    /// </summary>
    /// <param name="duration">Time taken to deliver the message.</param>
    /// <param name="topicName">The topic name.</param>
    public void RecordDeliveryDuration(TimeSpan duration, string topicName)
    {
        _deliveryDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("topic.name", topicName));
    }

    #endregion

    #region Gauge Methods

    /// <summary>
    /// Updates the queue depth gauge for a topic.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    /// <param name="depth">Current queue depth.</param>
    public void UpdateQueueDepth(string topicName, long depth)
    {
        _queueDepthByTopic.AddOrUpdate(
            topicName,
            depth,
            (key, oldValue) => depth);
    }

    /// <summary>
    /// Updates the consumer lag gauge for a topic.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    /// <param name="lag">Current consumer lag (messages behind).</param>
    public void UpdateConsumerLag(string topicName, long lag)
    {
        _consumerLagByTopic.AddOrUpdate(
            topicName,
            lag,
            (key, oldValue) => lag);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Removes all metrics for a topic (called when topic is deleted).
    /// </summary>
    /// <param name="topicName">The topic name to remove metrics for.</param>
    public void RemoveTopicMetrics(string topicName)
    {
        _queueDepthByTopic.TryRemove(topicName, out _);
        _consumerLagByTopic.TryRemove(topicName, out _);
    }

    /// <summary>
    /// Observable callback for queue depth gauge.
    /// </summary>
    private IEnumerable<Measurement<double>> ObserveQueueDepth()
    {
        foreach (var kvp in _queueDepthByTopic)
        {
            yield return new Measurement<double>(
                kvp.Value,
                new KeyValuePair<string, object?>("topic.name", kvp.Key));
        }
    }

    /// <summary>
    /// Observable callback for consumer lag gauge.
    /// </summary>
    private IEnumerable<Measurement<double>> ObserveConsumerLag()
    {
        foreach (var kvp in _consumerLagByTopic)
        {
            yield return new Measurement<double>(
                kvp.Value,
                new KeyValuePair<string, object?>("topic.name", kvp.Key));
        }
    }

    #endregion

    public void Dispose()
    {
        _meter.Dispose();
    }
}
