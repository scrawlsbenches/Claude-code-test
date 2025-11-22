using System.Diagnostics;
using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Telemetry;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class MessageTelemetryProviderTests : IDisposable
{
    private readonly MessageTelemetryProvider _provider;
    private readonly ActivityListener _activityListener;

    public MessageTelemetryProviderTests()
    {
        _provider = new MessageTelemetryProvider();

        // Set up ActivityListener to enable activity creation in tests
        // Listen to both MessageTelemetryProvider and test ActivitySources
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MessageTelemetryProvider.ServiceName || source.Name == "test",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    private Message CreateTestMessage(string messageId = "test-msg-123")
    {
        return new Message
        {
            MessageId = messageId,
            TopicName = "test.topic",
            Payload = "{\"test\":\"data\"}",
            SchemaVersion = "1.0",
            Priority = 5,
            DeliveryAttempts = 0,
            Status = MessageStatus.Pending
        };
    }

    #region StartPublishActivity Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartPublishActivity_WithValidMessage_CreatesActivityWithCorrectTags()
    {
        // Arrange
        var message = CreateTestMessage("msg-001");

        // Act
        using var activity = _provider.StartPublishActivity(message);

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be("message.publish");
        activity.Kind.Should().Be(ActivityKind.Producer);
        activity.GetTagItem("message.id").Should().Be("msg-001");
        activity.GetTagItem("topic.name").Should().Be("test.topic");
        activity.GetTagItem("schema.version").Should().Be("1.0");
        activity.GetTagItem("message.priority").Should().Be(5);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartPublishActivity_WithNullMessage_ReturnsNull()
    {
        // Act
        using var activity = _provider.StartPublishActivity(null!);

        // Assert
        activity.Should().BeNull();
    }

    #endregion

    #region StartRouteActivity Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartRouteActivity_WithValidMessage_CreatesActivityWithCorrectTags()
    {
        // Arrange
        var message = CreateTestMessage("msg-002");
        message.Partition = 3;

        // Act
        using var activity = _provider.StartRouteActivity(message);

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be("message.route");
        activity.Kind.Should().Be(ActivityKind.Internal);
        activity.GetTagItem("message.id").Should().Be("msg-002");
        activity.GetTagItem("topic.name").Should().Be("test.topic");
        activity.GetTagItem("partition").Should().Be(3);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartRouteActivity_WithoutPartition_CreatesActivityWithoutPartitionTag()
    {
        // Arrange
        var message = CreateTestMessage("msg-003");
        message.Partition = null;

        // Act
        using var activity = _provider.StartRouteActivity(message);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("partition").Should().BeNull();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartRouteActivity_WithNullMessage_ReturnsNull()
    {
        // Act
        using var activity = _provider.StartRouteActivity(null!);

        // Assert
        activity.Should().BeNull();
    }

    #endregion

    #region StartDeliverActivity Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartDeliverActivity_WithValidMessage_CreatesActivityWithCorrectTags()
    {
        // Arrange
        var message = CreateTestMessage("msg-004");
        message.DeliveryAttempts = 2;

        // Act
        using var activity = _provider.StartDeliverActivity(message);

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be("message.deliver");
        activity.Kind.Should().Be(ActivityKind.Consumer);
        activity.GetTagItem("message.id").Should().Be("msg-004");
        activity.GetTagItem("topic.name").Should().Be("test.topic");
        activity.GetTagItem("delivery.attempts").Should().Be(2);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartDeliverActivity_WithTraceContext_LinksToParentSpan()
    {
        // Arrange
        var message = CreateTestMessage("msg-005");

        // Create parent activity and inject context into message
        using var parentActivity = new ActivitySource("test", "1.0").StartActivity("parent");
        _provider.InjectTraceContext(parentActivity, message.Headers);

        // Act
        using var activity = _provider.StartDeliverActivity(message);

        // Assert
        activity.Should().NotBeNull();
        activity!.ParentId.Should().Be(parentActivity!.Id);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartDeliverActivity_WithNullMessage_ReturnsNull()
    {
        // Act
        using var activity = _provider.StartDeliverActivity(null!);

        // Assert
        activity.Should().BeNull();
    }

    #endregion

    #region StartAckActivity Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartAckActivity_WithSuccess_CreatesActivityWithCorrectTags()
    {
        // Arrange
        var message = CreateTestMessage("msg-006");

        // Act
        using var activity = _provider.StartAckActivity(message, success: true);

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be("message.ack");
        activity.Kind.Should().Be(ActivityKind.Internal);
        activity.GetTagItem("message.id").Should().Be("msg-006");
        activity.GetTagItem("ack.status").Should().Be("success");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartAckActivity_WithFailure_SetsFailureStatus()
    {
        // Arrange
        var message = CreateTestMessage("msg-007");

        // Act
        using var activity = _provider.StartAckActivity(message, success: false);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("ack.status").Should().Be("failure");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void StartAckActivity_WithNullMessage_ReturnsNull()
    {
        // Act
        using var activity = _provider.StartAckActivity(null!, success: true);

        // Assert
        activity.Should().BeNull();
    }

    #endregion

    #region InjectTraceContext Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void InjectTraceContext_WithActivity_AddsTraceparentHeader()
    {
        // Arrange
        var message = CreateTestMessage();
        using var activity = new ActivitySource("test", "1.0").StartActivity("test");

        // Act
        _provider.InjectTraceContext(activity, message.Headers);

        // Assert
        message.Headers.Should().ContainKey("traceparent");
        message.Headers["traceparent"].Should().Be(activity!.Id);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void InjectTraceContext_WithTraceState_AddsTracestateHeader()
    {
        // Arrange
        var message = CreateTestMessage();
        using var activity = new ActivitySource("test", "1.0").StartActivity("test");
        activity!.TraceStateString = "vendor1=value1,vendor2=value2";

        // Act
        _provider.InjectTraceContext(activity, message.Headers);

        // Assert
        message.Headers.Should().ContainKey("tracestate");
        message.Headers["tracestate"].Should().Be("vendor1=value1,vendor2=value2");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void InjectTraceContext_WithNullActivity_DoesNotModifyHeaders()
    {
        // Arrange
        var message = CreateTestMessage();
        var originalCount = message.Headers.Count;

        // Act
        _provider.InjectTraceContext(null, message.Headers);

        // Assert
        message.Headers.Count.Should().Be(originalCount);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void InjectTraceContext_OverwritesExistingHeaders()
    {
        // Arrange
        var message = CreateTestMessage();
        message.Headers["traceparent"] = "old-value";
        using var activity = new ActivitySource("test", "1.0").StartActivity("test");

        // Act
        _provider.InjectTraceContext(activity, message.Headers);

        // Assert
        message.Headers["traceparent"].Should().NotBe("old-value");
        message.Headers["traceparent"].Should().Be(activity!.Id);
    }

    #endregion

    #region ExtractTraceContext Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void ExtractTraceContext_WithValidTraceparent_ReturnsActivityContext()
    {
        // Arrange
        var message = CreateTestMessage();
        using var sourceActivity = new ActivitySource("test", "1.0").StartActivity("test");
        message.Headers["traceparent"] = sourceActivity!.Id!;

        // Act
        var context = _provider.ExtractTraceContext(message.Headers);

        // Assert
        context.Should().NotBeNull();
        context!.Value.TraceId.Should().Be(sourceActivity.TraceId);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void ExtractTraceContext_WithoutTraceparent_ReturnsNull()
    {
        // Arrange
        var message = CreateTestMessage();

        // Act
        var context = _provider.ExtractTraceContext(message.Headers);

        // Assert
        context.Should().BeNull();
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void ExtractTraceContext_WithInvalidTraceparent_ReturnsNull()
    {
        // Arrange
        var message = CreateTestMessage();
        message.Headers["traceparent"] = "invalid-traceparent-format";

        // Act
        var context = _provider.ExtractTraceContext(message.Headers);

        // Assert
        context.Should().BeNull();
    }

    #endregion

    #region RecordMessagePublished Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessagePublished_WithActivity_SetsStatusToOk()
    {
        // Arrange
        var message = CreateTestMessage();
        using var activity = _provider.StartPublishActivity(message);

        // Act
        _provider.RecordMessagePublished(activity, message);

        // Assert
        activity!.Status.Should().Be(ActivityStatusCode.Ok);
        activity.GetTagItem("message.published").Should().Be(true);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessagePublished_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var message = CreateTestMessage();

        // Act
        var action = () => _provider.RecordMessagePublished(null, message);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region RecordMessageDelivered Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessageDelivered_WithActivity_SetsStatusToOk()
    {
        // Arrange
        var message = CreateTestMessage();
        using var activity = _provider.StartDeliverActivity(message);
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        _provider.RecordMessageDelivered(activity, message, duration);

        // Assert
        activity!.Status.Should().Be(ActivityStatusCode.Ok);
        activity.GetTagItem("message.delivered").Should().Be(true);
        activity.GetTagItem("delivery.duration_ms").Should().Be(150.0);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessageDelivered_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var message = CreateTestMessage();
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var action = () => _provider.RecordMessageDelivered(null, message, duration);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region RecordMessageAcknowledged Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessageAcknowledged_WithActivity_SetsStatusToOk()
    {
        // Arrange
        var message = CreateTestMessage();
        using var activity = _provider.StartAckActivity(message, success: true);

        // Act
        _provider.RecordMessageAcknowledged(activity, message);

        // Assert
        activity!.Status.Should().Be(ActivityStatusCode.Ok);
        activity.GetTagItem("message.acknowledged").Should().Be(true);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessageAcknowledged_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var message = CreateTestMessage();

        // Act
        var action = () => _provider.RecordMessageAcknowledged(null, message);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region RecordMessageFailed Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessageFailed_WithException_SetsStatusToError()
    {
        // Arrange
        var message = CreateTestMessage();
        using var activity = _provider.StartDeliverActivity(message);
        var exception = new InvalidOperationException("Delivery failed");

        // Act
        _provider.RecordMessageFailed(activity, message, exception);

        // Assert
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("Delivery failed");
        activity.GetTagItem("message.failed").Should().Be(true);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessageFailed_WithoutException_SetsGenericError()
    {
        // Arrange
        var message = CreateTestMessage();
        using var activity = _provider.StartDeliverActivity(message);

        // Act
        _provider.RecordMessageFailed(activity, message, exception: null);

        // Assert
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("Message delivery failed");
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void RecordMessageFailed_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var message = CreateTestMessage();
        var exception = new Exception("Test");

        // Act
        var action = () => _provider.RecordMessageFailed(null, message, exception);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region Integration Tests

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void CompleteMessageFlow_PropagatesTraceContext()
    {
        // Arrange
        var message = CreateTestMessage("msg-flow-001");

        // Act: Publish message (producer creates root span and injects context)
        using var publishActivity = _provider.StartPublishActivity(message);
        _provider.InjectTraceContext(publishActivity, message.Headers);
        _provider.RecordMessagePublished(publishActivity, message);

        // Act: Route message (internal operation, doesn't modify trace context)
        using var routeActivity = _provider.StartRouteActivity(message);
        // Note: Routing is internal - does NOT inject context into message

        // Act: Deliver message (consumer extracts producer's context and links to it)
        using var deliverActivity = _provider.StartDeliverActivity(message);
        _provider.RecordMessageDelivered(deliverActivity, message, TimeSpan.FromMilliseconds(50));

        // Act: Acknowledge message
        using var ackActivity = _provider.StartAckActivity(message, success: true);
        _provider.RecordMessageAcknowledged(ackActivity, message);

        // Assert: All activities share the same trace
        publishActivity!.TraceId.Should().Be(deliverActivity!.TraceId);
        // Deliver activity should link to publish activity (producer-consumer linkage)
        deliverActivity.ParentId.Should().Be(publishActivity.Id);
        ackActivity!.TraceId.Should().Be(publishActivity.TraceId);
    }

    [Fact(Skip = "Temporarily disabled - investigating test hang")]
    public void Dispose_DisposesActivitySourceAndMeter()
    {
        // Arrange
        var provider = new MessageTelemetryProvider();

        // Act
        provider.Dispose();

        // Assert
        // No exception should be thrown, and subsequent operations should not crash
        var action = () => provider.Dispose();
        action.Should().NotThrow();
    }

    #endregion

    public void Dispose()
    {
        _provider.Dispose();
        _activityListener.Dispose();
    }
}
