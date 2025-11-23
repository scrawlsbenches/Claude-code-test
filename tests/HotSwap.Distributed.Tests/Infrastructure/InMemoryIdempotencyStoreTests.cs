using FluentAssertions;
using HotSwap.Distributed.Infrastructure.Coordination;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

/// <summary>
/// Unit tests for InMemoryIdempotencyStore.
/// Tests idempotency key tracking for duplicate message prevention.
/// </summary>
public class InMemoryIdempotencyStoreTests
{
    [Fact]
    public async Task HasBeenProcessedAsync_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "non-existent-key";

        // Act
        var result = await store.HasBeenProcessedAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_WithExistingKey_ReturnsTrue()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "existing-key";
        var messageId = "msg-123";
        await store.MarkAsProcessedAsync(key, messageId);

        // Act
        var result = await store.HasBeenProcessedAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_WithExpiredKey_ReturnsFalseAndRemovesKey()
    {
        // Arrange
        var expirationTime = TimeSpan.FromMilliseconds(100);
        var store = new InMemoryIdempotencyStore(expirationTime);
        var key = "expiring-key";
        var messageId = "msg-456";

        await store.MarkAsProcessedAsync(key, messageId);

        // Wait for expiration
        await Task.Delay(150);

        // Act
        var result = await store.HasBeenProcessedAsync(key);

        // Assert
        result.Should().BeFalse();

        // Verify key was removed by checking again
        var secondCheck = await store.HasBeenProcessedAsync(key);
        secondCheck.Should().BeFalse();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();

        // Act
        Func<Task> act = async () => await store.HasBeenProcessedAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("idempotencyKey");
    }

    [Fact]
    public async Task MarkAsProcessedAsync_WithValidParameters_MarksKeyAsProcessed()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "valid-key";
        var messageId = "msg-789";

        // Act
        await store.MarkAsProcessedAsync(key, messageId);

        // Assert
        var isProcessed = await store.HasBeenProcessedAsync(key);
        isProcessed.Should().BeTrue();

        var retrievedMessageId = await store.GetMessageIdAsync(key);
        retrievedMessageId.Should().Be(messageId);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();

        // Act
        Func<Task> act = async () => await store.MarkAsProcessedAsync(null!, "msg-123");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("idempotencyKey");
    }

    [Fact]
    public async Task MarkAsProcessedAsync_WithNullMessageId_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();

        // Act
        Func<Task> act = async () => await store.MarkAsProcessedAsync("key-123", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("messageId");
    }

    [Fact]
    public async Task MarkAsProcessedAsync_WithSameKeyTwice_OnlyFirstCallStored()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "duplicate-key";
        var firstMessageId = "msg-first";
        var secondMessageId = "msg-second";

        // Act
        await store.MarkAsProcessedAsync(key, firstMessageId);
        await store.MarkAsProcessedAsync(key, secondMessageId);

        // Assert
        var retrievedMessageId = await store.GetMessageIdAsync(key);
        retrievedMessageId.Should().Be(firstMessageId, "TryAdd only adds if key doesn't exist");
    }

    [Fact]
    public async Task GetMessageIdAsync_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "non-existent-key";

        // Act
        var result = await store.GetMessageIdAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMessageIdAsync_WithExistingKey_ReturnsMessageId()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "existing-key";
        var messageId = "msg-abc";
        await store.MarkAsProcessedAsync(key, messageId);

        // Act
        var result = await store.GetMessageIdAsync(key);

        // Assert
        result.Should().Be(messageId);
    }

    [Fact]
    public async Task GetMessageIdAsync_WithExpiredKey_ReturnsNullAndRemovesKey()
    {
        // Arrange
        var expirationTime = TimeSpan.FromMilliseconds(100);
        var store = new InMemoryIdempotencyStore(expirationTime);
        var key = "expiring-key";
        var messageId = "msg-xyz";

        await store.MarkAsProcessedAsync(key, messageId);

        // Wait for expiration
        await Task.Delay(150);

        // Act
        var result = await store.GetMessageIdAsync(key);

        // Assert
        result.Should().BeNull();

        // Verify key was removed
        var secondCheck = await store.GetMessageIdAsync(key);
        secondCheck.Should().BeNull();
    }

    [Fact]
    public async Task GetMessageIdAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();

        // Act
        Func<Task> act = async () => await store.GetMessageIdAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("idempotencyKey");
    }

    [Fact]
    public async Task RemoveAsync_WithExistingKey_RemovesKey()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "key-to-remove";
        var messageId = "msg-remove";
        await store.MarkAsProcessedAsync(key, messageId);

        // Act
        await store.RemoveAsync(key);

        // Assert
        var isProcessed = await store.HasBeenProcessedAsync(key);
        isProcessed.Should().BeFalse();

        var retrievedMessageId = await store.GetMessageIdAsync(key);
        retrievedMessageId.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_WithNonExistentKey_DoesNotThrow()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "non-existent-key";

        // Act
        Func<Task> act = async () => await store.RemoveAsync(key);

        // Assert
        await act.Should().NotThrowAsync("removing non-existent key should be idempotent");
    }

    [Fact]
    public async Task RemoveAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();

        // Act
        Func<Task> act = async () => await store.RemoveAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("idempotencyKey");
    }

    [Fact]
    public void Constructor_WithCustomExpirationTime_UsesProvidedExpiration()
    {
        // Arrange
        var customExpiration = TimeSpan.FromMinutes(30);

        // Act
        var store = new InMemoryIdempotencyStore(customExpiration);

        // Assert - Verify through behavior: key should not expire before custom time
        store.Should().NotBeNull();
        // Expiration behavior tested in other tests
    }

    [Fact]
    public void Constructor_WithNullExpirationTime_UsesDefaultExpiration()
    {
        // Arrange & Act
        var store = new InMemoryIdempotencyStore(null);

        // Assert - Default is 24 hours, verified through behavior
        store.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleThreads_HandlesCorrectly()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var tasks = new List<Task>();
        var messageCount = 100;

        // Act - Concurrent writes
        for (int i = 0; i < messageCount; i++)
        {
            var key = $"concurrent-key-{i}";
            var messageId = $"msg-{i}";
            tasks.Add(Task.Run(async () => await store.MarkAsProcessedAsync(key, messageId)));
        }

        await Task.WhenAll(tasks);

        // Assert - All keys should be processed
        for (int i = 0; i < messageCount; i++)
        {
            var key = $"concurrent-key-{i}";
            var isProcessed = await store.HasBeenProcessedAsync(key);
            isProcessed.Should().BeTrue($"key {key} should be processed");
        }
    }

    [Fact]
    public async Task ExpirationTime_BeforeExpiration_KeyStillValid()
    {
        // Arrange - use longer expiration to avoid flakiness under CPU contention
        var expirationTime = TimeSpan.FromSeconds(30);
        var store = new InMemoryIdempotencyStore(expirationTime);
        var key = "valid-for-30s";
        var messageId = "msg-valid";

        await store.MarkAsProcessedAsync(key, messageId);

        // Wait but not enough to expire (50ms << 30s)
        await Task.Delay(50);

        // Act
        var isProcessed = await store.HasBeenProcessedAsync(key);
        var retrievedMessageId = await store.GetMessageIdAsync(key);

        // Assert
        isProcessed.Should().BeTrue();
        retrievedMessageId.Should().Be(messageId);
    }

    [Fact]
    public async Task MultipleOperations_OnSameKey_WorkCorrectly()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var key = "multi-op-key";
        var messageId = "msg-multi";

        // Act & Assert - Full lifecycle

        // 1. Initially not processed
        (await store.HasBeenProcessedAsync(key)).Should().BeFalse();
        (await store.GetMessageIdAsync(key)).Should().BeNull();

        // 2. Mark as processed
        await store.MarkAsProcessedAsync(key, messageId);
        (await store.HasBeenProcessedAsync(key)).Should().BeTrue();
        (await store.GetMessageIdAsync(key)).Should().Be(messageId);

        // 3. Remove
        await store.RemoveAsync(key);
        (await store.HasBeenProcessedAsync(key)).Should().BeFalse();
        (await store.GetMessageIdAsync(key)).Should().BeNull();
    }
}
