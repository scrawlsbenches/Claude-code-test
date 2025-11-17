using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Delivery;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class DeliveryServiceTests
{
    private readonly Mock<IMessageQueue> _mockQueue;
    private readonly DeliveryService _service;

    public DeliveryServiceTests()
    {
        _mockQueue = new Mock<IMessageQueue>();
        var options = new DeliveryOptions
        {
            MaxRetries = 5,
            InitialBackoffMs = 100,
            MaxBackoffMs = 5000,
            BackoffMultiplier = 2.0
        };
        _service = new DeliveryService(_mockQueue.Object, options, NullLogger<DeliveryService>.Instance);
    }

    private Message CreateTestMessage(string messageId, int deliveryAttempts = 0)
    {
        return new Message
        {
            MessageId = messageId,
            TopicName = "test.topic",
            Payload = "test payload",
            SchemaVersion = "1.0",
            Priority = 5,
            DeliveryAttempts = deliveryAttempts,
            Timestamp = DateTime.UtcNow
        };
    }

    #region DeliverWithRetryAsync - Success Cases

    [Fact]
    public async Task DeliverWithRetryAsync_WithSuccessfulDelivery_ReturnsSuccessOnFirstAttempt()
    {
        // Arrange
        var message = CreateTestMessage("msg-1");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1")));

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.MessageId.Should().Be("msg-1");
        result.ConsumerId.Should().Be("consumer-1");
        result.DeliveryAttempts.Should().Be(1);
        result.TotalDelayMs.Should().Be(0);
    }

    [Fact]
    public async Task DeliverWithRetryAsync_WithSuccessOnSecondAttempt_ReturnsSuccessWithRetryInfo()
    {
        // Arrange
        var message = CreateTestMessage("msg-2");
        var attemptCount = 0;
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>((msg, ct) =>
        {
            attemptCount++;
            if (attemptCount == 1)
                return Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Temporary network error"));
            return Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1"));
        });

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.DeliveryAttempts.Should().Be(2);
        result.TotalDelayMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeliverWithRetryAsync_WithSuccessAfterMultipleRetries_TracksAllAttempts()
    {
        // Arrange
        var message = CreateTestMessage("msg-3");
        var attemptCount = 0;
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>((msg, ct) =>
        {
            attemptCount++;
            if (attemptCount < 4)
                return Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Retry needed"));
            return Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1"));
        });

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.DeliveryAttempts.Should().Be(4);
        result.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region DeliverWithRetryAsync - Retry Logic

    [Fact]
    public async Task DeliverWithRetryAsync_ImplementsExponentialBackoff()
    {
        // Arrange
        var message = CreateTestMessage("msg-4");
        var attemptCount = 0;
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>((msg, ct) =>
        {
            attemptCount++;
            if (attemptCount < 4)
                return Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Retry"));
            return Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1"));
        });

        var startTime = DateTime.UtcNow;

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        var elapsed = DateTime.UtcNow - startTime;
        result.IsSuccess.Should().BeTrue();
        result.DeliveryAttempts.Should().Be(4);
        // Total backoff: 100ms + 200ms + 400ms = 700ms minimum
        result.TotalDelayMs.Should().BeGreaterOrEqualTo(700);
    }

    [Fact]
    public async Task DeliverWithRetryAsync_WithMaxRetriesExceeded_MovesToDLQ()
    {
        // Arrange
        var message = CreateTestMessage("msg-5");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Permanent failure")));

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.DeliveryAttempts.Should().Be(6); // Max retries + 1
        result.MovedToDLQ.Should().BeTrue();
        result.ErrorMessage.Should().Be("Permanent failure");

        _mockQueue.Verify(x => x.EnqueueAsync(It.Is<Message>(m => m.TopicName == "test.topic.dlq"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeliverWithRetryAsync_UpdatesMessageDeliveryAttempts()
    {
        // Arrange
        var message = CreateTestMessage("msg-6", deliveryAttempts: 2);
        var attemptCount = 0;
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>((msg, ct) =>
        {
            attemptCount++;
            if (attemptCount < 3)
                return Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Retry"));
            return Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1"));
        });

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.DeliveryAttempts.Should().Be(5); // 2 existing + 3 new attempts
    }

    #endregion

    #region DeliverWithRetryAsync - Backoff Calculation

    [Fact]
    public async Task DeliverWithRetryAsync_CalculatesBackoffCorrectly()
    {
        // Arrange
        var message = CreateTestMessage("msg-7");
        var attemptCount = 0;
        var delays = new List<int>();
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>((msg, ct) =>
        {
            attemptCount++;
            if (attemptCount < 5)
                return Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Retry"));
            return Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1"));
        });

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        // Expected backoff: 100ms, 200ms, 400ms, 800ms
        result.TotalDelayMs.Should().BeGreaterOrEqualTo(1500);
        result.TotalDelayMs.Should().BeLessThan(2000); // Allow for timing variance
    }

    [Fact]
    public async Task DeliverWithRetryAsync_RespectsMaxBackoff()
    {
        // Arrange - use custom options with low max backoff
        var options = new DeliveryOptions
        {
            MaxRetries = 10,
            InitialBackoffMs = 100,
            MaxBackoffMs = 500,
            BackoffMultiplier = 2.0
        };
        var service = new DeliveryService(_mockQueue.Object, options, NullLogger<DeliveryService>.Instance);

        var message = CreateTestMessage("msg-8");
        var attemptCount = 0;
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>((msg, ct) =>
        {
            attemptCount++;
            if (attemptCount < 6)
                return Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Retry"));
            return Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1"));
        });

        // Act
        var result = await service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        // Backoff sequence: 100, 200, 400, 500 (capped), 500 (capped)
        result.TotalDelayMs.Should().BeGreaterOrEqualTo(1700);
        result.TotalDelayMs.Should().BeLessThan(2200);
    }

    #endregion

    #region DeliverWithRetryAsync - DLQ Handling

    [Fact]
    public async Task DeliverWithRetryAsync_WithDLQFailure_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage("msg-9");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Fail")));

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DLQ unavailable"));

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.MovedToDLQ.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Fail");
    }

    [Fact]
    public async Task DeliverWithRetryAsync_PreservesOriginalErrorInDLQ()
    {
        // Arrange
        var message = CreateTestMessage("msg-10");
        Message? dlqMessage = null;
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Original error message")));

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((msg, ct) => dlqMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.MovedToDLQ.Should().BeTrue();
        dlqMessage.Should().NotBeNull();
        dlqMessage!.DeliveryAttempts.Should().Be(6);
    }

    #endregion

    #region DeliverWithRetryAsync - Cancellation

    [Fact]
    public async Task DeliverWithRetryAsync_WithCancellation_StopsRetrying()
    {
        // Arrange
        var message = CreateTestMessage("msg-11");
        var attemptCount = 0;
        var cts = new CancellationTokenSource();

        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(async (msg, ct) =>
        {
            attemptCount++;
            if (attemptCount == 2)
            {
                cts.Cancel();
                await Task.Delay(50, CancellationToken.None);
            }
            return DeliveryResult.Failure(msg.MessageId, "Retry");
        });

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _service.DeliverWithRetryAsync(message, deliveryFunc, cts.Token));

        attemptCount.Should().BeLessThan(6); // Should not reach max retries
    }

    #endregion

    #region DeliverWithRetryAsync - Edge Cases

    [Fact]
    public async Task DeliverWithRetryAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success("test", "consumer-1")));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.DeliverWithRetryAsync(null!, deliveryFunc));
    }

    [Fact]
    public async Task DeliverWithRetryAsync_WithNullDeliveryFunc_ThrowsArgumentNullException()
    {
        // Arrange
        var message = CreateTestMessage("msg-12");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.DeliverWithRetryAsync(message, null!));
    }

    [Fact]
    public async Task DeliverWithRetryAsync_WithZeroMaxRetries_FailsImmediately()
    {
        // Arrange
        var options = new DeliveryOptions { MaxRetries = 0 };
        var service = new DeliveryService(_mockQueue.Object, options, NullLogger<DeliveryService>.Instance);

        var message = CreateTestMessage("msg-13");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Fail")));

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.DeliveryAttempts.Should().Be(1);
        result.MovedToDLQ.Should().BeTrue();
    }

    [Fact]
    public async Task DeliverWithRetryAsync_WithExceptionInDeliveryFunc_TreatsAsFailure()
    {
        // Arrange
        var message = CreateTestMessage("msg-14");
        var attemptCount = 0;
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>((msg, ct) =>
        {
            attemptCount++;
            if (attemptCount < 3)
                throw new InvalidOperationException("Delivery failed");
            return Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1"));
        });

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.DeliveryAttempts.Should().Be(3);
    }

    [Fact]
    public async Task DeliverWithRetryAsync_WithPermanentException_RetriesUntilMaxRetries()
    {
        // Arrange
        var message = CreateTestMessage("msg-15");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => throw new InvalidOperationException("Permanent error"));

        _mockQueue.Setup(x => x.EnqueueAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeliverWithRetryAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.DeliveryAttempts.Should().Be(6);
        result.MovedToDLQ.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Permanent error");
    }

    #endregion

    #region GetDLQTopicName Tests

    [Fact]
    public void GetDLQTopicName_ReturnsCorrectFormat()
    {
        // Arrange
        var topicName = "orders.created";

        // Act
        var dlqName = DeliveryService.GetDLQTopicName(topicName);

        // Assert
        dlqName.Should().Be("orders.created.dlq");
    }

    [Fact]
    public void GetDLQTopicName_WithNullTopic_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DeliveryService.GetDLQTopicName(null!));
    }

    #endregion
}
