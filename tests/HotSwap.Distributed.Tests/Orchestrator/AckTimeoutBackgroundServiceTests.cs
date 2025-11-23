using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

[Collection("BackgroundService Sequential")]
public class AckTimeoutBackgroundServiceTests
{
    private readonly Mock<IMessageQueue> _mockQueue;
    private readonly AckTimeoutBackgroundService _service;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan _ackTimeout = TimeSpan.FromSeconds(30);

    public AckTimeoutBackgroundServiceTests()
    {
        _mockQueue = new Mock<IMessageQueue>();
        _service = new AckTimeoutBackgroundService(
            _mockQueue.Object,
            NullLogger<AckTimeoutBackgroundService>.Instance,
            _checkInterval,
            _ackTimeout);
    }

    private Message CreateTestMessage(string messageId, DateTime? ackDeadline = null)
    {
        return new Message
        {
            MessageId = messageId,
            TopicName = "test.topic",
            Payload = "test payload",
            SchemaVersion = "1.0",
            Priority = 5,
            DeliveryAttempts = 1,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Pending,
            AckDeadline = ackDeadline,
            Headers = new Dictionary<string, string>()
        };
    }

    #region Service Lifecycle Tests

    [Fact]
    public async Task StartAsync_StartsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message>());

        // Act
        await _service.StartAsync(cts.Token);

        // Give service time to execute at least once
        await Task.Delay(200);

        // Assert
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        _mockQueue.Verify(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StopAsync_StopsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message>());

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(50);
        await _service.StopAsync(CancellationToken.None);

        var callCountBeforeStop = _mockQueue.Invocations.Count;

        // Wait to ensure no more calls after stop
        await Task.Delay(300);

        // Assert
        var callCountAfterStop = _mockQueue.Invocations.Count;
        callCountAfterStop.Should().Be(callCountBeforeStop);
    }

    #endregion

    #region Timeout Detection Tests

    [Fact]
    public async Task ExecuteAsync_WithExpiredMessage_RequeuesMessage()
    {
        // Arrange
        var expiredMessage = CreateTestMessage("msg-1", DateTime.UtcNow.AddSeconds(-1));
        Message? requeuedMessage = null;

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message> { expiredMessage });

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => requeuedMessage = msg)
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        requeuedMessage.Should().NotBeNull();
        requeuedMessage!.MessageId.Should().Be("msg-1");
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredMessage_IncrementsDeliveryAttempts()
    {
        // Arrange
        var expiredMessage = CreateTestMessage("msg-2", DateTime.UtcNow.AddSeconds(-1));
        expiredMessage.DeliveryAttempts = 2;
        Message? requeuedMessage = null;

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message> { expiredMessage });

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => requeuedMessage = msg)
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        requeuedMessage.Should().NotBeNull();
        requeuedMessage!.DeliveryAttempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredMessage_SetsNewAckDeadline()
    {
        // Arrange
        var expiredMessage = CreateTestMessage("msg-3", DateTime.UtcNow.AddSeconds(-1));
        Message? requeuedMessage = null;

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message> { expiredMessage });

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => requeuedMessage = msg)
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        requeuedMessage.Should().NotBeNull();
        requeuedMessage!.AckDeadline.Should().NotBeNull();
        requeuedMessage.AckDeadline.Should().BeAfter(DateTime.UtcNow);
        requeuedMessage.AckDeadline.Should().BeBefore(DateTime.UtcNow.Add(_ackTimeout).AddSeconds(5));
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExpiredMessage_DoesNotRequeue()
    {
        // Arrange
        var validMessage = CreateTestMessage("msg-4", DateTime.UtcNow.AddSeconds(30));

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message> { validMessage });

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _mockQueue.Verify(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithMessageNoAckDeadline_DoesNotRequeue()
    {
        // Arrange
        var message = CreateTestMessage("msg-5", ackDeadline: null);

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message> { message });

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _mockQueue.Verify(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Batch Processing Tests

    [Fact]
    public async Task ExecuteAsync_WithMultipleExpiredMessages_RequeuesAll()
    {
        // Arrange
        var expiredMessages = new List<Message>
        {
            CreateTestMessage("msg-6", DateTime.UtcNow.AddSeconds(-1)),
            CreateTestMessage("msg-7", DateTime.UtcNow.AddSeconds(-2)),
            CreateTestMessage("msg-8", DateTime.UtcNow.AddSeconds(-3))
        };

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredMessages);

        var requeuedCount = 0;
        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => requeuedCount++)
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        requeuedCount.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithMixedMessages_RequeuesOnlyExpired()
    {
        // Arrange
        var messages = new List<Message>
        {
            CreateTestMessage("msg-9", DateTime.UtcNow.AddSeconds(-1)), // Expired
            CreateTestMessage("msg-10", DateTime.UtcNow.AddSeconds(30)), // Valid
            CreateTestMessage("msg-11", DateTime.UtcNow.AddSeconds(-2)), // Expired
            CreateTestMessage("msg-12", null) // No deadline
        };

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        var requeuedMessageIds = new List<string>();
        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => requeuedMessageIds.Add(msg.MessageId))
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        requeuedMessageIds.Should().Contain("msg-9");
        requeuedMessageIds.Should().Contain("msg-11");
        requeuedMessageIds.Should().NotContain("msg-10");
        requeuedMessageIds.Should().NotContain("msg-12");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WithEmptyQueue_DoesNotThrow()
    {
        // Arrange
        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message>());

        var cts = new CancellationTokenSource();

        // Act & Assert
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // No exception should be thrown
        _mockQueue.Verify(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithQueuePeekFailure_ContinuesRunning()
    {
        // Arrange
        var callCount = 0;
        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ThrowsAsync(new InvalidOperationException("Queue unavailable"));

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(300);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - service should continue trying despite failures
        callCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithRequeueFailure_ContinuesProcessingOtherMessages()
    {
        // Arrange
        var expiredMessages = new List<Message>
        {
            CreateTestMessage("msg-13", DateTime.UtcNow.AddSeconds(-1)),
            CreateTestMessage("msg-14", DateTime.UtcNow.AddSeconds(-2))
        };

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredMessages);

        var requeuedCount = 0;
        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) =>
            {
                requeuedCount++;
                if (msg.MessageId == "msg-13")
                {
                    throw new InvalidOperationException("Requeue failed");
                }
            })
            .Returns(Task.CompletedTask);

        var cts = new CancellationTokenSource();

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - at least one message should be requeued
        requeuedCount.Should().BeGreaterOrEqualTo(1);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ExecuteAsync_WithCustomCheckInterval_UsesConfiguredInterval()
    {
        // Arrange
        var customInterval = TimeSpan.FromMilliseconds(50);
        var customService = new AckTimeoutBackgroundService(
            _mockQueue.Object,
            NullLogger<AckTimeoutBackgroundService>.Instance,
            customInterval,
            _ackTimeout);

        _mockQueue.Setup(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message>());

        var cts = new CancellationTokenSource();

        // Act
        await customService.StartAsync(cts.Token);
        await Task.Delay(200); // Should allow multiple checks with 50ms interval
        cts.Cancel();
        await customService.StopAsync(CancellationToken.None);

        // Assert - should execute multiple times within 200ms
        _mockQueue.Verify(x => x.PeekAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeast(3));
    }

    #endregion
}
