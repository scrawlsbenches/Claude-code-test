using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Orchestrator.Delivery;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Orchestrator;

public class ExactlyOnceDeliveryServiceTests
{
    private readonly Mock<IDistributedLock> _mockLock;
    private readonly Mock<IIdempotencyStore> _mockIdempotencyStore;
    private readonly Mock<IDeliveryService> _mockDeliveryService;
    private readonly ExactlyOnceDeliveryService _service;

    public ExactlyOnceDeliveryServiceTests()
    {
        _mockLock = new Mock<IDistributedLock>();
        _mockIdempotencyStore = new Mock<IIdempotencyStore>();
        _mockDeliveryService = new Mock<IDeliveryService>();
        _service = new ExactlyOnceDeliveryService(
            _mockLock.Object,
            _mockIdempotencyStore.Object,
            _mockDeliveryService.Object,
            NullLogger<ExactlyOnceDeliveryService>.Instance);
    }

    private Message CreateTestMessage(string messageId, string? idempotencyKey = null)
    {
        var message = new Message
        {
            MessageId = messageId,
            TopicName = "test.topic",
            Payload = "test payload",
            SchemaVersion = "1.0",
            Priority = 5,
            Timestamp = DateTime.UtcNow
        };

        if (idempotencyKey != null)
        {
            message.Headers["Idempotency-Key"] = idempotencyKey;
        }

        return message;
    }

    private Mock<ILockHandle> CreateMockLockHandle(string resource)
    {
        var mockHandle = new Mock<ILockHandle>();
        mockHandle.Setup(h => h.Resource).Returns(resource);
        mockHandle.Setup(h => h.AcquiredAt).Returns(DateTime.UtcNow);
        mockHandle.Setup(h => h.IsHeld).Returns(true);
        mockHandle.Setup(h => h.ReleaseAsync()).Returns(Task.CompletedTask);
        mockHandle.Setup(h => h.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return mockHandle;
    }

    #region DeliverExactlyOnceAsync - Success Cases

    [Fact]
    public async Task DeliverExactlyOnceAsync_WithNewIdempotencyKey_DeliversMessage()
    {
        // Arrange
        var message = CreateTestMessage("msg-1", "idem-key-1");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1")));

        var mockHandle = CreateMockLockHandle("idem-key-1");
        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-1", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("idem-key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockIdempotencyStore.Setup(x => x.MarkAsProcessedAsync("idem-key-1", "msg-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.MessageId.Should().Be("msg-1");

        _mockIdempotencyStore.Verify(x => x.MarkAsProcessedAsync("idem-key-1", "msg-1", It.IsAny<CancellationToken>()), Times.Once);
        mockHandle.Verify(h => h.ReleaseAsync(), Times.Once);
    }

    [Fact]
    public async Task DeliverExactlyOnceAsync_WithDuplicateIdempotencyKey_RejectsDuplicate()
    {
        // Arrange
        var message = CreateTestMessage("msg-2", "idem-key-2");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1")));

        var mockHandle = CreateMockLockHandle("idem-key-2");
        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-2", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("idem-key-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.Should().NotBeNull();
        result.IsDuplicate.Should().BeTrue();
        result.MessageId.Should().Be("msg-2");

        // Delivery function should NOT be called for duplicates
        _mockIdempotencyStore.Verify(x => x.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mockHandle.Verify(h => h.ReleaseAsync(), Times.Once);
    }

    [Fact]
    public async Task DeliverExactlyOnceAsync_WithoutIdempotencyKey_UsesMessageId()
    {
        // Arrange - no idempotency key header, should fall back to MessageId
        var message = CreateTestMessage("msg-3");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1")));

        var mockHandle = CreateMockLockHandle("msg-3");
        _mockLock.Setup(x => x.AcquireLockAsync("msg-3", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("msg-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockIdempotencyStore.Setup(x => x.MarkAsProcessedAsync("msg-3", "msg-3", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();

        _mockIdempotencyStore.Verify(x => x.MarkAsProcessedAsync("msg-3", "msg-3", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DeliverExactlyOnceAsync - Lock Handling

    [Fact]
    public async Task DeliverExactlyOnceAsync_WhenLockCannotBeAcquired_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage("msg-4", "idem-key-4");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1")));

        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-4", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILockHandle?)null); // Lock timeout

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Could not acquire lock");

        // Should not attempt delivery or mark as processed
        _mockIdempotencyStore.Verify(x => x.HasBeenProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockIdempotencyStore.Verify(x => x.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeliverExactlyOnceAsync_ReleasesLockOnSuccess()
    {
        // Arrange
        var message = CreateTestMessage("msg-5", "idem-key-5");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1")));

        var mockHandle = CreateMockLockHandle("idem-key-5");
        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-5", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("idem-key-5", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockIdempotencyStore.Setup(x => x.MarkAsProcessedAsync("idem-key-5", "msg-5", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mockHandle.Verify(h => h.ReleaseAsync(), Times.Once);
    }

    [Fact]
    public async Task DeliverExactlyOnceAsync_ReleasesLockOnFailure()
    {
        // Arrange
        var message = CreateTestMessage("msg-6", "idem-key-6");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Delivery failed")));

        var mockHandle = CreateMockLockHandle("idem-key-6");
        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-6", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("idem-key-6", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();
        mockHandle.Verify(h => h.ReleaseAsync(), Times.Once);
    }

    [Fact]
    public async Task DeliverExactlyOnceAsync_ReleasesLockOnException()
    {
        // Arrange
        var message = CreateTestMessage("msg-7", "idem-key-7");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => throw new InvalidOperationException("Delivery exception"));

        var mockHandle = CreateMockLockHandle("idem-key-7");
        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-7", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("idem-key-7", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Delivery exception");
        mockHandle.Verify(h => h.ReleaseAsync(), Times.Once);
    }

    #endregion

    #region DeliverExactlyOnceAsync - Idempotency Store

    [Fact]
    public async Task DeliverExactlyOnceAsync_OnlyMarksProcessedAfterSuccessfulDelivery()
    {
        // Arrange
        var message = CreateTestMessage("msg-8", "idem-key-8");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Failure(msg.MessageId, "Failed")));

        var mockHandle = CreateMockLockHandle("idem-key-8");
        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-8", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("idem-key-8", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();

        // Should NOT mark as processed because delivery failed
        _mockIdempotencyStore.Verify(x => x.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeliverExactlyOnceAsync_WithIdempotencyStoreFailure_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage("msg-9", "idem-key-9");
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1")));

        var mockHandle = CreateMockLockHandle("idem-key-9");
        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-9", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("idem-key-9", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockIdempotencyStore.Setup(x => x.MarkAsProcessedAsync("idem-key-9", "msg-9", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Store unavailable"));

        // Act
        var result = await _service.DeliverExactlyOnceAsync(message, deliveryFunc);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Store unavailable");
        mockHandle.Verify(h => h.ReleaseAsync(), Times.Once);
    }

    #endregion

    #region DeliverExactlyOnceAsync - Concurrency

    [Fact]
    public async Task DeliverExactlyOnceAsync_WithConcurrentRequests_OnlyDeliversOnce()
    {
        // Arrange
        var message1 = CreateTestMessage("msg-10", "idem-key-10");
        var message2 = CreateTestMessage("msg-11", "idem-key-10"); // Same idempotency key

        var deliveryCount = 0;
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(async (msg, ct) =>
        {
            deliveryCount++;
            await Task.Delay(10); // Simulate work
            return DeliveryResult.Success(msg.MessageId, "consumer-1");
        });

        var mockHandle1 = CreateMockLockHandle("idem-key-10");
        var mockHandle2 = CreateMockLockHandle("idem-key-10");

        var lockCallCount = 0;
        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-10", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                lockCallCount++;
                return lockCallCount == 1 ? mockHandle1.Object : mockHandle2.Object;
            });

        var hasBeenProcessedCallCount = 0;
        _mockIdempotencyStore.Setup(x => x.HasBeenProcessedAsync("idem-key-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                hasBeenProcessedCallCount++;
                return hasBeenProcessedCallCount > 1; // Second call sees it as processed
            });

        _mockIdempotencyStore.Setup(x => x.MarkAsProcessedAsync("idem-key-10", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - simulate concurrent requests
        var task1 = _service.DeliverExactlyOnceAsync(message1, deliveryFunc);
        var task2 = _service.DeliverExactlyOnceAsync(message2, deliveryFunc);
        var results = await Task.WhenAll(task1, task2);

        // Assert
        var successCount = results.Count(r => r.IsSuccess && !r.IsDuplicate);
        var duplicateCount = results.Count(r => r.IsDuplicate);

        successCount.Should().Be(1); // Only one should succeed
        duplicateCount.Should().Be(1); // One should be detected as duplicate
        deliveryCount.Should().Be(1); // Delivery function called only once
    }

    #endregion

    #region DeliverExactlyOnceAsync - Edge Cases

    [Fact]
    public async Task DeliverExactlyOnceAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success("test", "consumer-1")));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.DeliverExactlyOnceAsync(null!, deliveryFunc));
    }

    [Fact]
    public async Task DeliverExactlyOnceAsync_WithNullDeliveryFunc_ThrowsArgumentNullException()
    {
        // Arrange
        var message = CreateTestMessage("msg-12", "idem-key-12");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.DeliverExactlyOnceAsync(message, null!));
    }

    [Fact]
    public async Task DeliverExactlyOnceAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var message = CreateTestMessage("msg-13", "idem-key-13");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var deliveryFunc = new Func<Message, CancellationToken, Task<DeliveryResult>>(
            (msg, ct) => Task.FromResult(DeliveryResult.Success(msg.MessageId, "consumer-1")));

        _mockLock.Setup(x => x.AcquireLockAsync("idem-key-13", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _service.DeliverExactlyOnceAsync(message, deliveryFunc, cts.Token));
    }

    #endregion

    #region GetIdempotencyKey Tests

    [Fact]
    public void GetIdempotencyKey_WithHeaderPresent_ReturnsHeaderValue()
    {
        // Arrange
        var message = CreateTestMessage("msg-14", "custom-idem-key");

        // Act
        var key = ExactlyOnceDeliveryService.GetIdempotencyKey(message);

        // Assert
        key.Should().Be("custom-idem-key");
    }

    [Fact]
    public void GetIdempotencyKey_WithoutHeader_ReturnsMessageId()
    {
        // Arrange
        var message = CreateTestMessage("msg-15");

        // Act
        var key = ExactlyOnceDeliveryService.GetIdempotencyKey(message);

        // Assert
        key.Should().Be("msg-15");
    }

    [Fact]
    public void GetIdempotencyKey_WithEmptyHeader_ReturnsMessageId()
    {
        // Arrange
        var message = CreateTestMessage("msg-16");
        message.Headers["Idempotency-Key"] = "";

        // Act
        var key = ExactlyOnceDeliveryService.GetIdempotencyKey(message);

        // Assert
        key.Should().Be("msg-16");
    }

    #endregion
}
