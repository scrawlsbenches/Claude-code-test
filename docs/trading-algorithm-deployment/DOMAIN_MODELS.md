# Trading Algorithm Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Distributed.Trading.Domain.Models`

---

## Table of Contents

1. [AlgorithmDeployment](#algorithmdeployment)
2. [Position](#position)
3. [Trade](#trade)
4. [RiskLimits](#risklimits)
5. [PerformanceMetrics](#performancemetrics)

---

## AlgorithmDeployment

Represents a trading algorithm deployment instance.

**File:** `src/HotSwap.Distributed.Trading.Domain/Models/AlgorithmDeployment.cs`

```csharp
namespace HotSwap.Distributed.Trading.Domain.Models;

/// <summary>
/// Represents a trading algorithm deployment.
/// </summary>
public class AlgorithmDeployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Algorithm identifier and version (e.g., "momentum-v2.1.0").
    /// </summary>
    public required string AlgorithmId { get; set; }

    /// <summary>
    /// Deployment environment.
    /// </summary>
    public DeploymentEnvironment Environment { get; set; }

    /// <summary>
    /// Deployment strategy used.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; }

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Total capital allocated to this algorithm.
    /// </summary>
    public decimal CapitalAllocated { get; set; }

    /// <summary>
    /// Capital allocation percentage (0.0 to 1.0).
    /// </summary>
    public decimal AllocationPercent { get; set; }

    /// <summary>
    /// Current canary stage (if using canary strategy).
    /// </summary>
    public string? CurrentStage { get; set; }

    /// <summary>
    /// Risk limits for this deployment.
    /// </summary>
    public required RiskLimits RiskLimits { get; set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment configuration.
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(AlgorithmId))
            errors.Add("AlgorithmId is required");

        if (CapitalAllocated < 0)
            errors.Add("CapitalAllocated cannot be negative");

        if (AllocationPercent < 0 || AllocationPercent > 1)
            errors.Add("AllocationPercent must be between 0 and 1");

        if (RiskLimits == null)
            errors.Add("RiskLimits are required");

        return errors.Count == 0;
    }
}

/// <summary>
/// Deployment environment.
/// </summary>
public enum DeploymentEnvironment
{
    PaperTrading,
    Staging,
    Production
}

/// <summary>
/// Deployment status.
/// </summary>
public enum DeploymentStatus
{
    Pending,
    Deploying,
    Active,
    Halted,
    RolledBack,
    Failed
}
```

---

## Position

Represents a trading position held by an algorithm.

```csharp
namespace HotSwap.Distributed.Trading.Domain.Models;

/// <summary>
/// Represents a trading position.
/// </summary>
public class Position
{
    /// <summary>
    /// Unique position identifier.
    /// </summary>
    public required string PositionId { get; set; }

    /// <summary>
    /// Algorithm that owns this position.
    /// </summary>
    public required string AlgorithmId { get; set; }

    /// <summary>
    /// Trading symbol (e.g., "AAPL", "BTC/USD").
    /// </summary>
    public required string Symbol { get; set; }

    /// <summary>
    /// Position quantity (positive = long, negative = short).
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Position side.
    /// </summary>
    public PositionSide Side => Quantity >= 0 ? PositionSide.Long : PositionSide.Short;

    /// <summary>
    /// Average entry price.
    /// </summary>
    public decimal AvgPrice { get; set; }

    /// <summary>
    /// Current market price.
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Position market value.
    /// </summary>
    public decimal MarketValue => Math.Abs(Quantity) * CurrentPrice;

    /// <summary>
    /// Unrealized profit/loss.
    /// </summary>
    public decimal UnrealizedPnL => Quantity * (CurrentPrice - AvgPrice);

    /// <summary>
    /// Realized profit/loss (from partial closes).
    /// </summary>
    public decimal RealizedPnL { get; set; }

    /// <summary>
    /// Exchange where position is held.
    /// </summary>
    public required string Exchange { get; set; }

    /// <summary>
    /// Position open timestamp (UTC).
    /// </summary>
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PositionSide
{
    Long,
    Short
}
```

---

## Trade

Represents an executed trade.

```csharp
namespace HotSwap.Distributed.Trading.Domain.Models;

/// <summary>
/// Represents a trade execution.
/// </summary>
public class Trade
{
    /// <summary>
    /// Unique trade identifier.
    /// </summary>
    public required string TradeId { get; set; }

    /// <summary>
    /// Algorithm that executed this trade.
    /// </summary>
    public required string AlgorithmId { get; set; }

    /// <summary>
    /// Trading symbol.
    /// </summary>
    public required string Symbol { get; set; }

    /// <summary>
    /// Trade side (Buy/Sell).
    /// </summary>
    public TradeSide Side { get; set; }

    /// <summary>
    /// Trade quantity.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Execution price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Trade value (Quantity × Price).
    /// </summary>
    public decimal Value => Quantity * Price;

    /// <summary>
    /// Commission/fees paid.
    /// </summary>
    public decimal Commission { get; set; }

    /// <summary>
    /// Exchange where trade was executed.
    /// </summary>
    public required string Exchange { get; set; }

    /// <summary>
    /// Order type (Market, Limit, etc.).
    /// </summary>
    public OrderType OrderType { get; set; }

    /// <summary>
    /// Execution timestamp (UTC).
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Trade attribution metadata.
    /// </summary>
    public required TradeAttribution Attribution { get; set; }
}

public enum TradeSide
{
    Buy,
    Sell
}

public enum OrderType
{
    Market,
    Limit,
    Stop,
    StopLimit
}

public class TradeAttribution
{
    public required string AlgorithmVersion { get; set; }
    public required string DeploymentId { get; set; }
    public string? SignalType { get; set; }
}
```

---

## RiskLimits

Defines risk limits for an algorithm deployment.

```csharp
namespace HotSwap.Distributed.Trading.Domain.Models;

/// <summary>
/// Risk limits for algorithm deployment.
/// </summary>
public class RiskLimits
{
    /// <summary>
    /// Maximum daily loss as percentage (e.g., 0.02 = 2%).
    /// </summary>
    public decimal MaxDailyLossPercent { get; set; } = 0.02m;

    /// <summary>
    /// Maximum daily loss in absolute value.
    /// </summary>
    public decimal? MaxDailyLossAbsolute { get; set; }

    /// <summary>
    /// Maximum drawdown threshold (e.g., 0.05 = 5%).
    /// </summary>
    public decimal MaxDrawdown { get; set; } = 0.05m;

    /// <summary>
    /// Maximum position size per symbol.
    /// </summary>
    public decimal MaxPositionSize { get; set; }

    /// <summary>
    /// Maximum portfolio concentration (per symbol).
    /// </summary>
    public decimal MaxConcentration { get; set; } = 0.20m;

    /// <summary>
    /// Maximum trade velocity (trades per minute).
    /// </summary>
    public int MaxTradeVelocity { get; set; } = 100;

    /// <summary>
    /// Action to take when limit is breached.
    /// </summary>
    public LimitBreachAction BreachAction { get; set; } = LimitBreachAction.HaltAlgorithm;

    /// <summary>
    /// Whether to flatten positions on breach.
    /// </summary>
    public bool FlattenPositionsOnBreach { get; set; } = false;
}

public enum LimitBreachAction
{
    Alert,
    HaltAlgorithm,
    ReduceAllocation,
    Rollback
}
```

---

## PerformanceMetrics

Performance metrics for an algorithm.

```csharp
namespace HotSwap.Distributed.Trading.Domain.Models;

/// <summary>
/// Algorithm performance metrics.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Daily profit/loss.
    /// </summary>
    public decimal DailyPnL { get; set; }

    /// <summary>
    /// Cumulative profit/loss since deployment.
    /// </summary>
    public decimal CumulativePnL { get; set; }

    /// <summary>
    /// Unrealized profit/loss from open positions.
    /// </summary>
    public decimal UnrealizedPnL { get; set; }

    /// <summary>
    /// Current drawdown from peak.
    /// </summary>
    public decimal CurrentDrawdown { get; set; }

    /// <summary>
    /// Maximum drawdown experienced.
    /// </summary>
    public decimal MaxDrawdown { get; set; }

    /// <summary>
    /// Sharpe ratio (risk-adjusted return).
    /// </summary>
    public decimal SharpeRatio { get; set; }

    /// <summary>
    /// Annualized volatility.
    /// </summary>
    public decimal Volatility { get; set; }

    /// <summary>
    /// Win rate (winning trades / total trades).
    /// </summary>
    public decimal WinRate { get; set; }

    /// <summary>
    /// Total number of trades executed.
    /// </summary>
    public int TotalTrades { get; set; }

    /// <summary>
    /// Average winning trade value.
    /// </summary>
    public decimal AvgWin { get; set; }

    /// <summary>
    /// Average losing trade value.
    /// </summary>
    public decimal AvgLoss { get; set; }

    /// <summary>
    /// Profit factor (total wins / total losses).
    /// </summary>
    public decimal ProfitFactor => AvgLoss != 0 ? Math.Abs(AvgWin / AvgLoss) : 0;

    /// <summary>
    /// Current order error rate.
    /// </summary>
    public decimal ErrorRate { get; set; }

    /// <summary>
    /// Metrics calculation timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
