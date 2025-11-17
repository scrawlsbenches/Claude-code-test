using System.Diagnostics;
using System.Diagnostics.Metrics;
using HotSwap.Distributed.Domain.Models;
using OpenTelemetry.Trace;

namespace HotSwap.Distributed.Infrastructure.Telemetry;

/// <summary>
/// Provides OpenTelemetry-based distributed tracing and metrics for messaging operations.
/// Implements W3C trace context propagation to link producer and consumer spans.
/// </summary>
public class MessageTelemetryProvider : IDisposable
{
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    // Metrics
    private readonly Counter<long> _messagesPublished;
    private readonly Counter<long> _messagesDelivered;
    private readonly Counter<long> _messagesAcknowledged;
    private readonly Counter<long> _messagesFailed;
    private readonly Histogram<double> _deliveryDuration;

    public const string ServiceName = "HotSwap.Messaging";
    public const string ServiceVersion = "1.0.0";

    public MessageTelemetryProvider()
    {
        _activitySource = new ActivitySource(ServiceName, ServiceVersion);
        _meter = new Meter(ServiceName, ServiceVersion);

        // Initialize metrics
        _messagesPublished = _meter.CreateCounter<long>(
            "messages.published",
            description: "Total number of messages published");

        _messagesDelivered = _meter.CreateCounter<long>(
            "messages.delivered",
            description: "Total number of messages delivered");

        _messagesAcknowledged = _meter.CreateCounter<long>(
            "messages.acknowledged",
            description: "Total number of messages acknowledged");

        _messagesFailed = _meter.CreateCounter<long>(
            "messages.failed",
            description: "Total number of failed messages");

        _deliveryDuration = _meter.CreateHistogram<double>(
            "message.delivery.duration",
            unit: "milliseconds",
            description: "Message delivery duration in milliseconds");
    }

    #region Activity Creation

    /// <summary>
    /// Starts a publish activity for distributed tracing.
    /// Creates a Producer span for the message publishing operation.
    /// </summary>
    /// <param name="message">The message being published.</param>
    /// <returns>Activity for tracing, or null if message is null.</returns>
    public Activity? StartPublishActivity(Message? message)
    {
        if (message == null)
            return null;

        var activity = _activitySource.StartActivity(
            "message.publish",
            ActivityKind.Producer);

        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("topic.name", message.TopicName);
        activity?.SetTag("schema.version", message.SchemaVersion);
        activity?.SetTag("message.priority", message.Priority);

        return activity;
    }

    /// <summary>
    /// Starts a route activity for distributed tracing.
    /// Creates an Internal span for the message routing operation.
    /// </summary>
    /// <param name="message">The message being routed.</param>
    /// <returns>Activity for tracing, or null if message is null.</returns>
    public Activity? StartRouteActivity(Message? message)
    {
        if (message == null)
            return null;

        var activity = _activitySource.StartActivity(
            "message.route",
            ActivityKind.Internal);

        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("topic.name", message.TopicName);

        if (message.Partition.HasValue)
        {
            activity?.SetTag("partition", message.Partition.Value);
        }

        return activity;
    }

    /// <summary>
    /// Starts a deliver activity for distributed tracing.
    /// Creates a Consumer span for the message delivery operation.
    /// Links to parent span if trace context is present in message headers.
    /// </summary>
    /// <param name="message">The message being delivered.</param>
    /// <returns>Activity for tracing, or null if message is null.</returns>
    public Activity? StartDeliverActivity(Message? message)
    {
        if (message == null)
            return null;

        // Extract parent context from message headers
        var parentContext = ExtractTraceContext(message.Headers);

        Activity? activity;
        if (parentContext.HasValue)
        {
            // Create activity linked to parent trace
            activity = _activitySource.StartActivity(
                "message.deliver",
                ActivityKind.Consumer,
                parentContext.Value);
        }
        else
        {
            // Create new root activity
            activity = _activitySource.StartActivity(
                "message.deliver",
                ActivityKind.Consumer);
        }

        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("topic.name", message.TopicName);
        activity?.SetTag("delivery.attempts", message.DeliveryAttempts);

        return activity;
    }

    /// <summary>
    /// Starts an acknowledgment activity for distributed tracing.
    /// Creates an Internal span for the message acknowledgment operation.
    /// </summary>
    /// <param name="message">The message being acknowledged.</param>
    /// <param name="success">Whether acknowledgment succeeded.</param>
    /// <returns>Activity for tracing, or null if message is null.</returns>
    public Activity? StartAckActivity(Message? message, bool success)
    {
        if (message == null)
            return null;

        var activity = _activitySource.StartActivity(
            "message.ack",
            ActivityKind.Internal);

        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("ack.status", success ? "success" : "failure");

        return activity;
    }

    #endregion

    #region Trace Context Propagation

    /// <summary>
    /// Injects W3C trace context into message headers for cross-process tracing.
    /// Adds 'traceparent' and optionally 'tracestate' headers.
    /// </summary>
    /// <param name="activity">The current activity to propagate.</param>
    /// <param name="headers">Message headers to inject context into.</param>
    public void InjectTraceContext(Activity? activity, Dictionary<string, string> headers)
    {
        if (activity == null || headers == null)
            return;

        // Inject W3C traceparent header
        if (!string.IsNullOrEmpty(activity.Id))
        {
            headers["traceparent"] = activity.Id;
        }

        // Inject tracestate if present
        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            headers["tracestate"] = activity.TraceStateString;
        }
    }

    /// <summary>
    /// Extracts W3C trace context from message headers.
    /// Parses 'traceparent' header to reconstruct parent span context.
    /// </summary>
    /// <param name="headers">Message headers containing trace context.</param>
    /// <returns>ActivityContext if valid traceparent found, otherwise null.</returns>
    public ActivityContext? ExtractTraceContext(Dictionary<string, string> headers)
    {
        if (headers == null || !headers.TryGetValue("traceparent", out var traceparent))
            return null;

        // Parse W3C traceparent format
        if (ActivityContext.TryParse(traceparent, null, out var context))
        {
            return context;
        }

        return null;
    }

    #endregion

    #region Recording Results

    /// <summary>
    /// Records a successful message publish operation.
    /// Increments published counter and sets activity status.
    /// </summary>
    /// <param name="activity">The publish activity.</param>
    /// <param name="message">The published message.</param>
    public void RecordMessagePublished(Activity? activity, Message message)
    {
        if (message == null)
            return;

        _messagesPublished.Add(1,
            new KeyValuePair<string, object?>("topic.name", message.TopicName),
            new KeyValuePair<string, object?>("priority", message.Priority));

        activity?.SetStatus(ActivityStatusCode.Ok, "Message published successfully");
        activity?.SetTag("message.published", true);
    }

    /// <summary>
    /// Records a successful message delivery operation.
    /// Increments delivered counter, records duration, and sets activity status.
    /// </summary>
    /// <param name="activity">The deliver activity.</param>
    /// <param name="message">The delivered message.</param>
    /// <param name="duration">Time taken to deliver the message.</param>
    public void RecordMessageDelivered(Activity? activity, Message message, TimeSpan duration)
    {
        if (message == null)
            return;

        _messagesDelivered.Add(1,
            new KeyValuePair<string, object?>("topic.name", message.TopicName),
            new KeyValuePair<string, object?>("delivery.attempts", message.DeliveryAttempts));

        _deliveryDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("topic.name", message.TopicName));

        activity?.SetStatus(ActivityStatusCode.Ok, "Message delivered successfully");
        activity?.SetTag("message.delivered", true);
        activity?.SetTag("delivery.duration_ms", duration.TotalMilliseconds);
    }

    /// <summary>
    /// Records a successful message acknowledgment operation.
    /// Increments acknowledged counter and sets activity status.
    /// </summary>
    /// <param name="activity">The acknowledgment activity.</param>
    /// <param name="message">The acknowledged message.</param>
    public void RecordMessageAcknowledged(Activity? activity, Message message)
    {
        if (message == null)
            return;

        _messagesAcknowledged.Add(1,
            new KeyValuePair<string, object?>("topic.name", message.TopicName));

        activity?.SetStatus(ActivityStatusCode.Ok, "Message acknowledged successfully");
        activity?.SetTag("message.acknowledged", true);
    }

    /// <summary>
    /// Records a failed message operation.
    /// Increments failed counter, sets error status, and records exception if present.
    /// </summary>
    /// <param name="activity">The operation activity.</param>
    /// <param name="message">The failed message.</param>
    /// <param name="exception">The exception that caused the failure (optional).</param>
    public void RecordMessageFailed(Activity? activity, Message message, Exception? exception)
    {
        if (message == null)
            return;

        _messagesFailed.Add(1,
            new KeyValuePair<string, object?>("topic.name", message.TopicName),
            new KeyValuePair<string, object?>("delivery.attempts", message.DeliveryAttempts));

        var errorMessage = exception?.Message ?? "Message delivery failed";
        activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
        activity?.SetTag("message.failed", true);

        if (exception != null)
        {
            activity?.RecordException(exception);
        }
    }

    #endregion

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}
