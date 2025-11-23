# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.LiveEvents.Domain.Models`

---

## Table of Contents

1. [LiveEvent](#liveevent)
2. [EventConfiguration](#eventconfiguration)
3. [EventDeployment](#eventdeployment)
4. [PlayerSegment](#playersegment)
5. [EventMetrics](#eventmetrics)
6. [Region](#region)
7. [Enumerations](#enumerations)
8. [Value Objects](#value-objects)

---

## LiveEvent

Represents a live event in the game.

**File:** `src/HotSwap.LiveEvents.Domain/Models/LiveEvent.cs`

```csharp
namespace HotSwap.LiveEvents.Domain.Models;

/// <summary>
/// Represents a live event in the game system.
/// </summary>
public class LiveEvent
{
    /// <summary>
    /// Unique event identifier (slug format: "summer-fest-2025").
    /// </summary>
    public required string EventId { get; set; }

    /// <summary>
    /// Display name shown to players.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Detailed description of the event (supports markdown).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Event type classification.
    /// </summary>
    public EventType Type { get; set; } = EventType.SeasonalPromotion;

    /// <summary>
    /// Event category for grouping.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Event start time (UTC).
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Event end time (UTC).
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Timezone for display purposes (e.g., "America/New_York").
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Current event state.
    /// </summary>
    public EventState State { get; set; } = EventState.Draft;

    /// <summary>
    /// Event configuration (rewards, parameters, etc.).
    /// </summary>
    public required EventConfiguration Configuration { get; set; }

    /// <summary>
    /// Player segments this event targets (null = all players).
    /// </summary>
    public List<string> TargetSegments { get; set; } = new();

    /// <summary>
    /// Localization data (language → localized content).
    /// </summary>
    public Dictionary<string, EventLocalization> Localizations { get; set; } = new();

    /// <summary>
    /// Event priority (higher priority events shown first).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Maximum number of participants (null = unlimited).
    /// </summary>
    public int? MaxParticipants { get; set; }

    /// <summary>
    /// Current participant count.
    /// </summary>
    public int CurrentParticipants { get; set; } = 0;

    /// <summary>
    /// Event tags for filtering (e.g., "pvp", "pve", "seasonal").
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Event creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the event.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Approval status for production deployment.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// Admin who approved the event (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Validates the event configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(EventId))
            errors.Add("EventId is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(EventId, @"^[a-z0-9-]+$"))
            errors.Add("EventId must contain only lowercase letters, numbers, and dashes");

        if (string.IsNullOrWhiteSpace(DisplayName))
            errors.Add("DisplayName is required");

        if (StartTime >= EndTime)
            errors.Add("EndTime must be after StartTime");

        if (StartTime < DateTime.UtcNow.AddMinutes(-5) && State == EventState.Draft)
            errors.Add("StartTime must be in the future for new events");

        if (Configuration == null)
            errors.Add("Configuration is required");
        else if (!Configuration.IsValid(out var configErrors))
            errors.AddRange(configErrors.Select(e => $"Configuration: {e}"));

        if (MaxParticipants.HasValue && MaxParticipants.Value <= 0)
            errors.Add("MaxParticipants must be positive");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the event is currently active.
    /// </summary>
    public bool IsActive()
    {
        var now = DateTime.UtcNow;
        return State == EventState.Active && now >= StartTime && now < EndTime;
    }

    /// <summary>
    /// Checks if the event has ended.
    /// </summary>
    public bool HasEnded()
    {
        return State == EventState.Completed || State == EventState.Cancelled || DateTime.UtcNow >= EndTime;
    }

    /// <summary>
    /// Checks if player can participate based on segment targeting.
    /// </summary>
    public bool CanPlayerParticipate(List<string> playerSegments)
    {
        // If no target segments, all players can participate
        if (TargetSegments.Count == 0)
            return true;

        // Player must belong to at least one target segment
        return TargetSegments.Intersect(playerSegments).Any();
    }
}

/// <summary>
/// Event localization data for a specific language.
/// </summary>
public class EventLocalization
{
    public required string Language { get; set; }
    public required string DisplayName { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = new();
}
```

---

## EventConfiguration

Represents event configuration parameters.

**File:** `src/HotSwap.LiveEvents.Domain/Models/EventConfiguration.cs`

```csharp
namespace HotSwap.LiveEvents.Domain.Models;

/// <summary>
/// Represents event configuration parameters.
/// </summary>
public class EventConfiguration
{
    /// <summary>
    /// Configuration version (for schema evolution).
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Reward configuration.
    /// </summary>
    public RewardConfiguration? Rewards { get; set; }

    /// <summary>
    /// Game parameter multipliers (XP, currency, drop rates).
    /// </summary>
    public Dictionary<string, double> Multipliers { get; set; } = new();

    /// <summary>
    /// Unlocked features during event (items, game modes, maps).
    /// </summary>
    public List<string> UnlockedFeatures { get; set; } = new();

    /// <summary>
    /// Custom configuration parameters (JSON serializable).
    /// </summary>
    public Dictionary<string, object> CustomParameters { get; set; } = new();

    /// <summary>
    /// Asset references (images, audio, videos).
    /// </summary>
    public AssetReferences? Assets { get; set; }

    /// <summary>
    /// UI configuration (banner position, colors, animations).
    /// </summary>
    public UIConfiguration? UI { get; set; }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");

        // Validate multipliers are positive
        foreach (var (key, value) in Multipliers)
        {
            if (value <= 0)
                errors.Add($"Multiplier '{key}' must be positive");
        }

        // Validate rewards if present
        if (Rewards != null && !Rewards.IsValid(out var rewardErrors))
            errors.AddRange(rewardErrors.Select(e => $"Rewards: {e}"));

        return errors.Count == 0;
    }
}

/// <summary>
/// Reward configuration.
/// </summary>
public class RewardConfiguration
{
    /// <summary>
    /// Daily login bonus (currency amount).
    /// </summary>
    public int? DailyLoginBonus { get; set; }

    /// <summary>
    /// Quest completion multiplier.
    /// </summary>
    public double? QuestMultiplier { get; set; }

    /// <summary>
    /// Item rewards (item ID → quantity).
    /// </summary>
    public Dictionary<string, int> Items { get; set; } = new();

    /// <summary>
    /// Currency rewards (currency type → amount).
    /// </summary>
    public Dictionary<string, int> Currency { get; set; } = new();

    /// <summary>
    /// Cosmetic rewards (skin IDs, emote IDs).
    /// </summary>
    public List<string> Cosmetics { get; set; } = new();

    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (DailyLoginBonus.HasValue && DailyLoginBonus.Value <= 0)
            errors.Add("DailyLoginBonus must be positive");

        if (QuestMultiplier.HasValue && QuestMultiplier.Value <= 0)
            errors.Add("QuestMultiplier must be positive");

        foreach (var (itemId, quantity) in Items)
        {
            if (quantity <= 0)
                errors.Add($"Item '{itemId}' quantity must be positive");
        }

        foreach (var (currencyType, amount) in Currency)
        {
            if (amount <= 0)
                errors.Add($"Currency '{currencyType}' amount must be positive");
        }

        return errors.Count == 0;
    }
}

/// <summary>
/// Asset references for event visuals.
/// </summary>
public class AssetReferences
{
    public string? BannerImageUrl { get; set; }
    public string? IconImageUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? BackgroundMusicUrl { get; set; }
    public string? TrailerVideoUrl { get; set; }
}

/// <summary>
/// UI configuration for event display.
/// </summary>
public class UIConfiguration
{
    public string? BannerPosition { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AnimationStyle { get; set; }
    public bool ShowCountdown { get; set; } = true;
}
```

---

## EventDeployment

Represents an event deployment to regions.

**File:** `src/HotSwap.LiveEvents.Domain/Models/EventDeployment.cs`

```csharp
namespace HotSwap.LiveEvents.Domain.Models;

/// <summary>
/// Represents an event deployment to geographic regions.
/// </summary>
public class EventDeployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Event being deployed.
    /// </summary>
    public required string EventId { get; set; }

    /// <summary>
    /// Target regions for this deployment.
    /// </summary>
    public required List<string> Regions { get; set; }

    /// <summary>
    /// Rollout strategy to use.
    /// </summary>
    public RolloutStrategy Strategy { get; set; } = RolloutStrategy.Canary;

    /// <summary>
    /// Rollout configuration parameters.
    /// </summary>
    public required RolloutConfiguration Configuration { get; set; }

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment progress (0-100).
    /// </summary>
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// Current rollout batch index.
    /// </summary>
    public int CurrentBatch { get; set; } = 0;

    /// <summary>
    /// Total number of batches.
    /// </summary>
    public int TotalBatches { get; set; }

    /// <summary>
    /// Regional deployment statuses.
    /// </summary>
    public Dictionary<string, RegionDeploymentStatus> RegionStatuses { get; set; } = new();

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// User who initiated the deployment.
    /// </summary>
    public required string DeployedBy { get; set; }

    /// <summary>
    /// Rollback information (if deployment was rolled back).
    /// </summary>
    public RollbackInfo? Rollback { get; set; }

    /// <summary>
    /// Deployment error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Checks if deployment is complete.
    /// </summary>
    public bool IsComplete()
    {
        return Status == DeploymentStatus.Completed ||
               Status == DeploymentStatus.Failed ||
               Status == DeploymentStatus.RolledBack;
    }

    /// <summary>
    /// Checks if deployment can be rolled back.
    /// </summary>
    public bool CanRollback()
    {
        return Status == DeploymentStatus.InProgress ||
               Status == DeploymentStatus.Completed;
    }
}

/// <summary>
/// Rollout configuration parameters.
/// </summary>
public class RolloutConfiguration
{
    /// <summary>
    /// Canary rollout batches (e.g., [10, 30, 50, 100]).
    /// </summary>
    public List<int> CanaryBatches { get; set; } = new() { 10, 30, 50, 100 };

    /// <summary>
    /// Delay between batches (ISO 8601 duration, e.g., "PT5M" = 5 minutes).
    /// </summary>
    public string BatchDelay { get; set; } = "PT5M";

    /// <summary>
    /// Automatic rollback threshold configuration.
    /// </summary>
    public RollbackThreshold? AutoRollback { get; set; }

    /// <summary>
    /// Region deployment order (for rolling strategy).
    /// </summary>
    public List<string>? RegionOrder { get; set; }

    /// <summary>
    /// Local time for geographic rollout (e.g., "12:00:00").
    /// </summary>
    public string? LocalTime { get; set; }
}

/// <summary>
/// Automatic rollback threshold configuration.
/// </summary>
public class RollbackThreshold
{
    /// <summary>
    /// Participation rate drop threshold (0.0 - 1.0).
    /// </summary>
    public double? ParticipationRateDrop { get; set; }

    /// <summary>
    /// Error rate increase threshold (0.0 - 1.0).
    /// </summary>
    public double? ErrorRateIncrease { get; set; }

    /// <summary>
    /// Negative feedback count threshold (absolute count).
    /// </summary>
    public int? NegativeFeedbackCount { get; set; }

    /// <summary>
    /// Server error count threshold (absolute count).
    /// </summary>
    public int? ServerErrorCount { get; set; }
}

/// <summary>
/// Regional deployment status.
/// </summary>
public class RegionDeploymentStatus
{
    public required string Region { get; set; }
    public DeploymentStatus Status { get; set; }
    public int PlayerPercentage { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Rollback information.
/// </summary>
public class RollbackInfo
{
    public DateTime RolledBackAt { get; set; }
    public required string RolledBackBy { get; set; }
    public required string Reason { get; set; }
    public bool WasAutomatic { get; set; }
}
```

---

## PlayerSegment

Represents a player cohort for targeting.

**File:** `src/HotSwap.LiveEvents.Domain/Models/PlayerSegment.cs`

```csharp
namespace HotSwap.LiveEvents.Domain.Models;

/// <summary>
/// Represents a player segment for event targeting.
/// </summary>
public class PlayerSegment
{
    /// <summary>
    /// Unique segment identifier (e.g., "vip-tier-3").
    /// </summary>
    public required string SegmentId { get; set; }

    /// <summary>
    /// Display name for the segment.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Segment description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Segment type.
    /// </summary>
    public SegmentType Type { get; set; }

    /// <summary>
    /// Segment targeting criteria.
    /// </summary>
    public required SegmentCriteria Criteria { get; set; }

    /// <summary>
    /// Estimated player count in this segment.
    /// </summary>
    public long EstimatedPlayerCount { get; set; }

    /// <summary>
    /// Last time segment membership was recalculated.
    /// </summary>
    public DateTime LastCalculated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Segment creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the segment.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Validates the segment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SegmentId))
            errors.Add("SegmentId is required");

        if (string.IsNullOrWhiteSpace(DisplayName))
            errors.Add("DisplayName is required");

        if (Criteria == null)
            errors.Add("Criteria is required");
        else if (!Criteria.IsValid(out var criteriaErrors))
            errors.AddRange(criteriaErrors.Select(e => $"Criteria: {e}"));

        return errors.Count == 0;
    }
}

/// <summary>
/// Segment targeting criteria.
/// </summary>
public class SegmentCriteria
{
    /// <summary>
    /// Player level range (min, max).
    /// </summary>
    public (int Min, int Max)? LevelRange { get; set; }

    /// <summary>
    /// Account age range in days (min, max).
    /// </summary>
    public (int Min, int Max)? AccountAgeDays { get; set; }

    /// <summary>
    /// Lifetime spend range (min, max).
    /// </summary>
    public (decimal Min, decimal Max)? LifetimeSpend { get; set; }

    /// <summary>
    /// Days since last login (min, max).
    /// </summary>
    public (int Min, int Max)? DaysSinceLastLogin { get; set; }

    /// <summary>
    /// Session count in last N days.
    /// </summary>
    public (int Days, int MinSessions)? RecentSessionCount { get; set; }

    /// <summary>
    /// Player country codes (ISO 3166-1 alpha-2).
    /// </summary>
    public List<string> CountryCodes { get; set; } = new();

    /// <summary>
    /// Player platforms (pc, mobile, console).
    /// </summary>
    public List<string> Platforms { get; set; } = new();

    /// <summary>
    /// Custom criteria (key → value).
    /// </summary>
    public Dictionary<string, string> CustomCriteria { get; set; } = new();

    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (LevelRange.HasValue && LevelRange.Value.Min > LevelRange.Value.Max)
            errors.Add("LevelRange min must be <= max");

        if (AccountAgeDays.HasValue && AccountAgeDays.Value.Min > AccountAgeDays.Value.Max)
            errors.Add("AccountAgeDays min must be <= max");

        if (LifetimeSpend.HasValue && LifetimeSpend.Value.Min > LifetimeSpend.Value.Max)
            errors.Add("LifetimeSpend min must be <= max");

        return errors.Count == 0;
    }

    /// <summary>
    /// Evaluates whether a player matches this criteria.
    /// </summary>
    public bool Matches(PlayerProfile player)
    {
        if (LevelRange.HasValue &&
            (player.Level < LevelRange.Value.Min || player.Level > LevelRange.Value.Max))
            return false;

        if (AccountAgeDays.HasValue)
        {
            var accountAge = (DateTime.UtcNow - player.CreatedAt).Days;
            if (accountAge < AccountAgeDays.Value.Min || accountAge > AccountAgeDays.Value.Max)
                return false;
        }

        if (LifetimeSpend.HasValue &&
            (player.LifetimeSpend < LifetimeSpend.Value.Min || player.LifetimeSpend > LifetimeSpend.Value.Max))
            return false;

        if (DaysSinceLastLogin.HasValue)
        {
            var daysSinceLogin = (DateTime.UtcNow - player.LastLoginAt).Days;
            if (daysSinceLogin < DaysSinceLastLogin.Value.Min || daysSinceLogin > DaysSinceLastLogin.Value.Max)
                return false;
        }

        if (CountryCodes.Count > 0 && !CountryCodes.Contains(player.CountryCode))
            return false;

        if (Platforms.Count > 0 && !Platforms.Contains(player.Platform))
            return false;

        return true;
    }
}

/// <summary>
/// Player profile for segment matching.
/// </summary>
public class PlayerProfile
{
    public required string PlayerId { get; set; }
    public int Level { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal LifetimeSpend { get; set; }
    public DateTime LastLoginAt { get; set; }
    public required string CountryCode { get; set; }
    public required string Platform { get; set; }
}
```

---

## EventMetrics

Represents event engagement metrics.

**File:** `src/HotSwap.LiveEvents.Domain/Models/EventMetrics.cs`

```csharp
namespace HotSwap.LiveEvents.Domain.Models;

/// <summary>
/// Represents event engagement metrics.
/// </summary>
public class EventMetrics
{
    /// <summary>
    /// Event identifier.
    /// </summary>
    public required string EventId { get; set; }

    /// <summary>
    /// Region (null = global).
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Metrics time window start (UTC).
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// Metrics time window end (UTC).
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// Engagement metrics.
    /// </summary>
    public EngagementMetrics Engagement { get; set; } = new();

    /// <summary>
    /// Revenue metrics.
    /// </summary>
    public RevenueMetrics Revenue { get; set; } = new();

    /// <summary>
    /// Player sentiment metrics.
    /// </summary>
    public SentimentMetrics Sentiment { get; set; } = new();

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Engagement metrics.
/// </summary>
public class EngagementMetrics
{
    /// <summary>
    /// Total active players during event window.
    /// </summary>
    public long ActivePlayers { get; set; }

    /// <summary>
    /// Players who participated in event.
    /// </summary>
    public long Participants { get; set; }

    /// <summary>
    /// Participation rate (0.0 - 1.0).
    /// </summary>
    public double ParticipationRate => ActivePlayers > 0 ? (double)Participants / ActivePlayers : 0;

    /// <summary>
    /// Players who completed event objectives.
    /// </summary>
    public long Completions { get; set; }

    /// <summary>
    /// Completion rate (0.0 - 1.0).
    /// </summary>
    public double CompletionRate => Participants > 0 ? (double)Completions / Participants : 0;

    /// <summary>
    /// Average session duration during event (seconds).
    /// </summary>
    public double AvgSessionDuration { get; set; }

    /// <summary>
    /// Daily active users.
    /// </summary>
    public long DAU { get; set; }

    /// <summary>
    /// Monthly active users.
    /// </summary>
    public long MAU { get; set; }
}

/// <summary>
/// Revenue metrics.
/// </summary>
public class RevenueMetrics
{
    /// <summary>
    /// Total revenue during event (USD).
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Baseline revenue (before event).
    /// </summary>
    public decimal BaselineRevenue { get; set; }

    /// <summary>
    /// Revenue uplift percentage.
    /// </summary>
    public double RevenueUplift => BaselineRevenue > 0 ?
        (double)((TotalRevenue - BaselineRevenue) / BaselineRevenue) : 0;

    /// <summary>
    /// Average revenue per user (ARPU).
    /// </summary>
    public decimal ARPU { get; set; }

    /// <summary>
    /// Average revenue per paying user (ARPPU).
    /// </summary>
    public decimal ARPPU { get; set; }

    /// <summary>
    /// Conversion rate (paying users / active users).
    /// </summary>
    public double ConversionRate { get; set; }

    /// <summary>
    /// Number of purchases.
    /// </summary>
    public long PurchaseCount { get; set; }
}

/// <summary>
/// Sentiment metrics.
/// </summary>
public class SentimentMetrics
{
    /// <summary>
    /// Positive feedback count.
    /// </summary>
    public long PositiveFeedback { get; set; }

    /// <summary>
    /// Negative feedback count.
    /// </summary>
    public long NegativeFeedback { get; set; }

    /// <summary>
    /// Neutral feedback count.
    /// </summary>
    public long NeutralFeedback { get; set; }

    /// <summary>
    /// Net Promoter Score (-100 to 100).
    /// </summary>
    public double NPS { get; set; }

    /// <summary>
    /// Player retention rate (players who returned after event).
    /// </summary>
    public double RetentionRate { get; set; }
}
```

---

## Region

Represents a geographic region.

**File:** `src/HotSwap.LiveEvents.Domain/Models/Region.cs`

```csharp
namespace HotSwap.LiveEvents.Domain.Models;

/// <summary>
/// Represents a geographic region.
/// </summary>
public class Region
{
    /// <summary>
    /// Region identifier (e.g., "us-east").
    /// </summary>
    public required string RegionId { get; set; }

    /// <summary>
    /// Display name (e.g., "US East Coast").
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Timezone identifier (e.g., "America/New_York").
    /// </summary>
    public required string Timezone { get; set; }

    /// <summary>
    /// Country codes in this region (ISO 3166-1 alpha-2).
    /// </summary>
    public List<string> CountryCodes { get; set; } = new();

    /// <summary>
    /// Estimated player population.
    /// </summary>
    public long PlayerPopulation { get; set; }

    /// <summary>
    /// Region health status.
    /// </summary>
    public RegionHealth Health { get; set; } = new();

    /// <summary>
    /// Region is enabled for deployments.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Region health information.
/// </summary>
public class RegionHealth
{
    public bool IsHealthy { get; set; } = true;
    public double ServerLoad { get; set; }
    public int ActivePlayers { get; set; }
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
}
```

---

## Enumerations

### EventType

**File:** `src/HotSwap.LiveEvents.Domain/Enums/EventType.cs`

```csharp
namespace HotSwap.LiveEvents.Domain.Enums;

/// <summary>
/// Represents the type of a live event.
/// </summary>
public enum EventType
{
    /// <summary>
    /// Seasonal event (holidays, anniversaries).
    /// </summary>
    SeasonalPromotion,

    /// <summary>
    /// Limited-time offer (flash sale, discount weekend).
    /// </summary>
    LimitedTimeOffer,

    /// <summary>
    /// Competitive event (tournament, leaderboard challenge).
    /// </summary>
    CompetitiveEvent,

    /// <summary>
    /// Content release (new feature, game mode, map).
    /// </summary>
    ContentRelease,

    /// <summary>
    /// Player retention campaign (re-engagement, win-back).
    /// </summary>
    RetentionCampaign,

    /// <summary>
    /// A/B test variant.
    /// </summary>
    ABTestVariant
}
```

### EventState

```csharp
namespace HotSwap.LiveEvents.Domain.Enums;

/// <summary>
/// Represents the current state of a live event.
/// </summary>
public enum EventState
{
    /// <summary>
    /// Event created but not deployed.
    /// </summary>
    Draft,

    /// <summary>
    /// Event scheduled for future activation.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Event currently running.
    /// </summary>
    Active,

    /// <summary>
    /// Event temporarily disabled.
    /// </summary>
    Paused,

    /// <summary>
    /// Event ended naturally.
    /// </summary>
    Completed,

    /// <summary>
    /// Event ended prematurely.
    /// </summary>
    Cancelled
}
```

### RolloutStrategy

```csharp
namespace HotSwap.LiveEvents.Domain.Enums;

/// <summary>
/// Represents the rollout strategy for event deployment.
/// </summary>
public enum RolloutStrategy
{
    /// <summary>
    /// Progressive rollout with validation (10% → 30% → 50% → 100%).
    /// </summary>
    Canary,

    /// <summary>
    /// Instant switch between blue/green environments.
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Region-by-region sequential rollout.
    /// </summary>
    Rolling,

    /// <summary>
    /// Time-zone aware rollout (same local time across regions).
    /// </summary>
    Geographic,

    /// <summary>
    /// Player segment-based rollout.
    /// </summary>
    Segmented
}
```

### DeploymentStatus

```csharp
namespace HotSwap.LiveEvents.Domain.Enums;

/// <summary>
/// Represents the status of an event deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment queued, not yet started.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Deployment failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Deployment rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Deployment cancelled before completion.
    /// </summary>
    Cancelled
}
```

### SegmentType

```csharp
namespace HotSwap.LiveEvents.Domain.Enums;

/// <summary>
/// Represents the type of player segment.
/// </summary>
public enum SegmentType
{
    /// <summary>
    /// VIP/high-value players.
    /// </summary>
    VIP,

    /// <summary>
    /// New players (account age < threshold).
    /// </summary>
    NewPlayer,

    /// <summary>
    /// Inactive players (not logged in recently).
    /// </summary>
    Inactive,

    /// <summary>
    /// Highly engaged players (frequent sessions).
    /// </summary>
    HighEngagement,

    /// <summary>
    /// Players at risk of churning.
    /// </summary>
    AtRisk,

    /// <summary>
    /// Custom segment defined by criteria.
    /// </summary>
    Custom
}
```

### ApprovalStatus

```csharp
namespace HotSwap.LiveEvents.Domain.Enums;

/// <summary>
/// Represents the approval status for production deployment.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Awaiting approval.
    /// </summary>
    Pending,

    /// <summary>
    /// Approved for production deployment.
    /// </summary>
    Approved,

    /// <summary>
    /// Approval rejected.
    /// </summary>
    Rejected
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.LiveEvents.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.LiveEvents.Domain.ValueObjects;

/// <summary>
/// Result of an event deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Deployment identifier.
    /// </summary>
    public string? DeploymentId { get; private set; }

    /// <summary>
    /// Regions successfully deployed.
    /// </summary>
    public List<string> SuccessfulRegions { get; private set; } = new();

    /// <summary>
    /// Regions that failed deployment.
    /// </summary>
    public List<string> FailedRegions { get; private set; } = new();

    /// <summary>
    /// Error message (if deployment failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(string deploymentId, List<string> regions)
    {
        return new DeploymentResult
        {
            Success = true,
            DeploymentId = deploymentId,
            SuccessfulRegions = regions
        };
    }

    public static DeploymentResult PartialSuccess(string deploymentId, List<string> successful, List<string> failed)
    {
        return new DeploymentResult
        {
            Success = false,
            DeploymentId = deploymentId,
            SuccessfulRegions = successful,
            FailedRegions = failed,
            ErrorMessage = $"Deployment partially failed: {failed.Count} regions failed"
        };
    }

    public static DeploymentResult Failure(string errorMessage)
    {
        return new DeploymentResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
```

---

## Validation Examples

### Event Validation

```csharp
var liveEvent = new LiveEvent
{
    EventId = "summer-fest-2025",
    DisplayName = "Summer Festival 2025",
    Type = EventType.SeasonalPromotion,
    StartTime = DateTime.UtcNow.AddDays(7),
    EndTime = DateTime.UtcNow.AddDays(37),
    Configuration = new EventConfiguration
    {
        Rewards = new RewardConfiguration
        {
            DailyLoginBonus = 100,
            QuestMultiplier = 2.0
        }
    },
    CreatedBy = "gamedesigner@example.com"
};

if (!liveEvent.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Segment Validation

```csharp
var segment = new PlayerSegment
{
    SegmentId = "vip-tier-3",
    DisplayName = "VIP Tier 3",
    Type = SegmentType.VIP,
    Criteria = new SegmentCriteria
    {
        LifetimeSpend = (100m, 500m),
        AccountAgeDays = (30, int.MaxValue)
    },
    CreatedBy = "admin@example.com"
};

if (!segment.IsValid(out var errors))
{
    Console.WriteLine("Segment validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.LiveEvents.Domain`
