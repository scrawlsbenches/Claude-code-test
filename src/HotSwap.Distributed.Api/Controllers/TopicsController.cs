using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API controller for managing messaging topics.
/// </summary>
[ApiController]
[Route("api/v1/topics")]
[Produces("application/json")]
public class TopicsController : ControllerBase
{
    private readonly ITopicService _topicService;
    private readonly ILogger<TopicsController> _logger;

    public TopicsController(
        ITopicService topicService,
        ILogger<TopicsController> logger)
    {
        _topicService = topicService ?? throw new ArgumentNullException(nameof(topicService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new topic.
    /// </summary>
    /// <param name="topic">The topic to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created topic.</returns>
    /// <response code="201">Topic created successfully.</response>
    /// <response code="400">Invalid topic configuration.</response>
    /// <response code="409">Topic already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Topic), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Topic>> CreateTopic(
        [FromBody] Topic topic,
        CancellationToken cancellationToken = default)
    {
        if (topic == null)
        {
            return BadRequest(new { error = "Topic cannot be null" });
        }

        // Validate topic configuration
        if (!topic.IsValid(out var errors))
        {
            return BadRequest(new { error = "Invalid topic configuration", details = errors });
        }

        try
        {
            var createdTopic = await _topicService.CreateTopicAsync(topic, cancellationToken);

            _logger.LogInformation(
                "Topic '{TopicName}' created successfully",
                createdTopic.Name);

            return CreatedAtAction(
                nameof(GetTopic),
                new { name = createdTopic.Name },
                createdTopic);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create topic '{TopicName}': {Error}",
                topic.Name,
                ex.Message);

            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all topics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all topics.</returns>
    /// <response code="200">Topics retrieved successfully.</response>
    /// <response code="500">Internal server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Topic>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<Topic>>> ListTopics(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var topics = await _topicService.ListTopicsAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} topics", topics.Count);

            return Ok(topics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list topics");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Failed to retrieve topics", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets a specific topic by name.
    /// </summary>
    /// <param name="name">The topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The topic if found.</returns>
    /// <response code="200">Topic found.</response>
    /// <response code="400">Invalid topic name.</response>
    /// <response code="404">Topic not found.</response>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Topic), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Topic>> GetTopic(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "Topic name cannot be empty" });
        }

        var topic = await _topicService.GetTopicAsync(name, cancellationToken);

        if (topic == null)
        {
            _logger.LogWarning("Topic '{TopicName}' not found", name);
            return NotFound(new { error = $"Topic '{name}' not found" });
        }

        return Ok(topic);
    }

    /// <summary>
    /// Updates an existing topic.
    /// </summary>
    /// <param name="name">The topic name.</param>
    /// <param name="topic">The updated topic data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated topic.</returns>
    /// <response code="200">Topic updated successfully.</response>
    /// <response code="400">Invalid topic configuration or name mismatch.</response>
    /// <response code="404">Topic not found.</response>
    [HttpPut("{name}")]
    [ProducesResponseType(typeof(Topic), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Topic>> UpdateTopic(
        string name,
        [FromBody] Topic topic,
        CancellationToken cancellationToken = default)
    {
        if (topic == null)
        {
            return BadRequest(new { error = "Topic cannot be null" });
        }

        if (name != topic.Name)
        {
            return BadRequest(new { error = "Topic name in URL does not match topic name in body" });
        }

        // Validate topic configuration
        if (!topic.IsValid(out var errors))
        {
            return BadRequest(new { error = "Invalid topic configuration", details = errors });
        }

        var updatedTopic = await _topicService.UpdateTopicAsync(name, topic, cancellationToken);

        if (updatedTopic == null)
        {
            _logger.LogWarning("Topic '{TopicName}' not found for update", name);
            return NotFound(new { error = $"Topic '{name}' not found" });
        }

        _logger.LogInformation("Topic '{TopicName}' updated successfully", name);

        return Ok(updatedTopic);
    }

    /// <summary>
    /// Deletes a topic.
    /// </summary>
    /// <param name="name">The topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Topic deleted successfully.</response>
    /// <response code="400">Invalid topic name.</response>
    /// <response code="404">Topic not found.</response>
    /// <response code="409">Topic has active subscriptions.</response>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteTopic(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "Topic name cannot be empty" });
        }

        try
        {
            var deleted = await _topicService.DeleteTopicAsync(name, cancellationToken);

            if (!deleted)
            {
                _logger.LogWarning("Topic '{TopicName}' not found for deletion", name);
                return NotFound(new { error = $"Topic '{name}' not found" });
            }

            _logger.LogInformation("Topic '{TopicName}' deleted successfully", name);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete topic '{TopicName}': {Error}",
                name,
                ex.Message);

            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets metrics for a specific topic.
    /// </summary>
    /// <param name="name">The topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Topic metrics.</returns>
    /// <response code="200">Metrics retrieved successfully.</response>
    /// <response code="400">Invalid topic name.</response>
    /// <response code="404">Topic not found.</response>
    [HttpGet("{name}/metrics")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Dictionary<string, object>>> GetTopicMetrics(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "Topic name cannot be empty" });
        }

        var metrics = await _topicService.GetTopicMetricsAsync(name, cancellationToken);

        if (metrics == null)
        {
            _logger.LogWarning("Topic '{TopicName}' not found for metrics", name);
            return NotFound(new { error = $"Topic '{name}' not found" });
        }

        return Ok(metrics);
    }
}
