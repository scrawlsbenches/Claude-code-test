using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API controller for distributed messaging operations.
/// Provides endpoints for publishing, retrieving, acknowledging, and deleting messages.
/// </summary>
[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageQueue _messageQueue;
    private readonly IMessagePersistence _messagePersistence;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageQueue messageQueue,
        IMessagePersistence messagePersistence,
        ILogger<MessagesController> logger)
    {
        _messageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
        _messagePersistence = messagePersistence ?? throw new ArgumentNullException(nameof(messagePersistence));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Publishes a message to the messaging system.
    /// </summary>
    /// <param name="message">The message to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created message with location header</returns>
    [HttpPost("publish")]
    [ProducesResponseType(typeof(Message), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PublishMessage(
        [FromBody] Message message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate message ID if not provided
            if (string.IsNullOrWhiteSpace(message.MessageId))
            {
                message.MessageId = Guid.NewGuid().ToString();
            }

            // Set timestamp
            message.Timestamp = DateTime.UtcNow;

            // Validate message
            if (!message.IsValid(out var errors))
            {
                _logger.LogWarning("Invalid message: {Errors}", string.Join(", ", errors));
                return BadRequest(new { Errors = errors });
            }

            // Persist message
            await _messagePersistence.StoreAsync(message, cancellationToken);

            // Enqueue message for delivery
            await _messageQueue.EnqueueAsync(message, cancellationToken);

            _logger.LogInformation("Published message {MessageId} to topic {TopicName}",
                message.MessageId, message.TopicName);

            return CreatedAtAction(
                nameof(GetMessage),
                new { id = message.MessageId },
                message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to topic {TopicName}", message?.TopicName);
            return StatusCode(500, new { Error = "An error occurred while publishing the message" });
        }
    }

    /// <summary>
    /// Retrieves a message by its ID.
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The requested message</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Message), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMessage(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await _messagePersistence.RetrieveAsync(id, cancellationToken);

            if (message == null)
            {
                _logger.LogWarning("Message {MessageId} not found", id);
                return NotFound(new { Error = $"Message with ID '{id}' not found" });
            }

            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving message {MessageId}", id);
            return StatusCode(500, new { Error = "An error occurred while retrieving the message" });
        }
    }

    /// <summary>
    /// Retrieves messages for a specific topic.
    /// </summary>
    /// <param name="topicName">Topic name</param>
    /// <param name="limit">Maximum number of messages to retrieve (default: 100, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of messages for the topic</returns>
    [HttpGet("topic/{topicName}")]
    [ProducesResponseType(typeof(List<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMessagesByTopic(
        string topicName,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Cap limit at 1000
            var effectiveLimit = Math.Min(limit, 1000);

            var messages = await _messagePersistence.GetByTopicAsync(
                topicName,
                effectiveLimit,
                cancellationToken);

            _logger.LogInformation("Retrieved {Count} messages for topic {TopicName}",
                messages.Count, topicName);

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages for topic {TopicName}", topicName);
            return StatusCode(500, new { Error = "An error occurred while retrieving messages" });
        }
    }

    /// <summary>
    /// Acknowledges successful delivery of a message.
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated message</returns>
    [HttpPost("{id}/acknowledge")]
    [ProducesResponseType(typeof(Message), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AcknowledgeMessage(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await _messagePersistence.RetrieveAsync(id, cancellationToken);

            if (message == null)
            {
                _logger.LogWarning("Message {MessageId} not found for acknowledgment", id);
                return NotFound(new { Error = $"Message with ID '{id}' not found" });
            }

            // Update message status
            message.Status = MessageStatus.Acknowledged;
            message.AcknowledgedAt = DateTime.UtcNow;

            // Persist updated message
            await _messagePersistence.StoreAsync(message, cancellationToken);

            _logger.LogInformation("Acknowledged message {MessageId}", id);

            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging message {MessageId}", id);
            return StatusCode(500, new { Error = "An error occurred while acknowledging the message" });
        }
    }

    /// <summary>
    /// Deletes a message from the messaging system.
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMessage(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _messagePersistence.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                _logger.LogWarning("Message {MessageId} not found for deletion", id);
                return NotFound(new { Error = $"Message with ID '{id}' not found" });
            }

            _logger.LogInformation("Deleted message {MessageId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId}", id);
            return StatusCode(500, new { Error = "An error occurred while deleting the message" });
        }
    }
}
