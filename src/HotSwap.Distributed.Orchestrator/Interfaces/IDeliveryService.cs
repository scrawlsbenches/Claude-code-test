using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Orchestrator.Delivery;

namespace HotSwap.Distributed.Orchestrator.Interfaces;

/// <summary>
/// Service for delivering messages with retry logic.
/// </summary>
public interface IDeliveryService
{
    /// <summary>
    /// Delivers a message with retry logic and exponential backoff.
    /// </summary>
    /// <param name="message">The message to deliver.</param>
    /// <param name="deliveryFunc">Function that performs the actual delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Delivery result including retry information.</returns>
    Task<DeliveryResult> DeliverWithRetryAsync(
        Message message,
        Func<Message, CancellationToken, Task<DeliveryResult>> deliveryFunc,
        CancellationToken cancellationToken = default);
}
