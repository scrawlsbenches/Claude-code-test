namespace HotSwap.Distributed.Orchestrator.Delivery;

/// <summary>
/// Configuration options for message delivery with retry logic.
/// </summary>
public class DeliveryOptions
{
    /// <summary>
    /// Maximum number of retry attempts before moving to DLQ.
    /// Default: 5
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Initial backoff delay in milliseconds.
    /// Default: 2000 (2 seconds)
    /// </summary>
    public int InitialBackoffMs { get; set; } = 2000;

    /// <summary>
    /// Maximum backoff delay in milliseconds.
    /// Default: 32000 (32 seconds)
    /// </summary>
    public int MaxBackoffMs { get; set; } = 32000;

    /// <summary>
    /// Backoff multiplier for exponential backoff.
    /// Default: 2.0 (doubles each time)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Validates the delivery options.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (MaxRetries < 0)
        {
            errors.Add("MaxRetries cannot be negative");
        }

        if (InitialBackoffMs < 0)
        {
            errors.Add("InitialBackoffMs cannot be negative");
        }

        if (MaxBackoffMs < InitialBackoffMs)
        {
            errors.Add("MaxBackoffMs must be greater than or equal to InitialBackoffMs");
        }

        if (BackoffMultiplier < 1.0)
        {
            errors.Add("BackoffMultiplier must be greater than or equal to 1.0");
        }

        return errors.Count == 0;
    }
}
