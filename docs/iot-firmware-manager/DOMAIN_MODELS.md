# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.IoT.Firmware.Domain.Models`

---

## Table of Contents

1. [Firmware](#firmware)
2. [Device](#device)
3. [Deployment](#deployment)
4. [DeviceGroup](#devicegroup)
5. [FirmwareSignature](#firmwaresignature)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## Firmware

Represents a firmware version for IoT devices.

**File:** `src/HotSwap.IoT.Firmware.Domain/Models/Firmware.cs`

```csharp
namespace HotSwap.IoT.Firmware.Domain.Models;

/// <summary>
/// Represents a firmware version.
/// </summary>
public class Firmware
{
    /// <summary>
    /// Unique firmware identifier (GUID format).
    /// </summary>
    public required string FirmwareId { get; set; }

    /// <summary>
    /// Firmware version (semantic versioning: "1.2.3").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Target device model.
    /// </summary>
    public required string DeviceModel { get; set; }

    /// <summary>
    /// URL to firmware binary (S3/MinIO).
    /// </summary>
    public required string BinaryUrl { get; set; }

    /// <summary>
    /// SHA256 checksum of firmware binary.
    /// </summary>
    public required string Checksum { get; set; }

    /// <summary>
    /// RSA signature for verification.
    /// </summary>
    public required string Signature { get; set; }

    /// <summary>
    /// Firmware binary size in bytes.
    /// </summary>
    public long BinarySize { get; set; }

    /// <summary>
    /// Release notes (markdown format).
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Current firmware status.
    /// </summary>
    public FirmwareStatus Status { get; set; } = FirmwareStatus.Draft;

    /// <summary>
    /// Admin who approved the firmware (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Firmware creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Firmware deprecation timestamp (UTC).
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// Minimum compatible device firmware version.
    /// </summary>
    public string? MinimumDeviceVersion { get; set; }

    /// <summary>
    /// Additional metadata (JSON).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Validates the firmware for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(FirmwareId))
            errors.Add("FirmwareId is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");
        else if (!IsValidSemanticVersion(Version))
            errors.Add("Version must be valid semantic version (e.g., '1.2.3')");

        if (string.IsNullOrWhiteSpace(DeviceModel))
            errors.Add("DeviceModel is required");

        if (string.IsNullOrWhiteSpace(BinaryUrl))
            errors.Add("BinaryUrl is required");
        else if (!Uri.IsWellFormedUriString(BinaryUrl, UriKind.Absolute))
            errors.Add("BinaryUrl must be a valid absolute URL");

        if (string.IsNullOrWhiteSpace(Checksum))
            errors.Add("Checksum is required");
        else if (Checksum.Length != 64) // SHA256 = 64 hex characters
            errors.Add("Checksum must be valid SHA256 (64 hex characters)");

        if (string.IsNullOrWhiteSpace(Signature))
            errors.Add("Signature is required");

        if (BinarySize <= 0)
            errors.Add("BinarySize must be greater than 0");
        else if (BinarySize > 104857600) // 100 MB
            errors.Add("BinarySize exceeds maximum of 100 MB");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if firmware is approved for production use.
    /// </summary>
    public bool IsApproved() => Status == FirmwareStatus.Approved;

    /// <summary>
    /// Checks if firmware is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == FirmwareStatus.Deprecated;

    private static bool IsValidSemanticVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length != 3) return false;
        return parts.All(p => int.TryParse(p, out _));
    }
}
```

---

## Device

Represents an IoT device in the system.

**File:** `src/HotSwap.IoT.Firmware.Domain/Models/Device.cs`

```csharp
namespace HotSwap.IoT.Firmware.Domain.Models;

/// <summary>
/// Represents an IoT device.
/// </summary>
public class Device
{
    /// <summary>
    /// Unique device identifier (MAC address, serial number, or GUID).
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Human-readable device name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Device model (matches firmware DeviceModel).
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// Current firmware version installed on device.
    /// </summary>
    public required string CurrentFirmwareVersion { get; set; }

    /// <summary>
    /// Previous firmware version (for rollback).
    /// </summary>
    public string? PreviousFirmwareVersion { get; set; }

    /// <summary>
    /// Device region (US-EAST, US-WEST, EU, APAC).
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Device tags for grouping and filtering.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Current device status.
    /// </summary>
    public DeviceStatus Status { get; set; } = DeviceStatus.Online;

    /// <summary>
    /// Device health metrics.
    /// </summary>
    public DeviceHealth Health { get; set; } = new();

    /// <summary>
    /// Whether device is enabled for firmware updates.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Device registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last firmware update timestamp (UTC).
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    /// Additional device metadata (JSON).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Validates the device for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeviceId))
            errors.Add("DeviceId is required");

        if (string.IsNullOrWhiteSpace(Model))
            errors.Add("Model is required");

        if (string.IsNullOrWhiteSpace(CurrentFirmwareVersion))
            errors.Add("CurrentFirmwareVersion is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if device is online and healthy.
    /// </summary>
    public bool IsHealthy()
    {
        // Device is unhealthy if no heartbeat in last 5 minutes
        if (DateTime.UtcNow - LastHeartbeat > TimeSpan.FromMinutes(5))
            return false;

        return Status == DeviceStatus.Online && Health.IsHealthy;
    }

    /// <summary>
    /// Updates the heartbeat timestamp.
    /// </summary>
    public void RecordHeartbeat()
    {
        LastHeartbeat = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates firmware version after successful deployment.
    /// </summary>
    public void UpdateFirmware(string newVersion)
    {
        PreviousFirmwareVersion = CurrentFirmwareVersion;
        CurrentFirmwareVersion = newVersion;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rolls back to previous firmware version.
    /// </summary>
    public void Rollback()
    {
        if (string.IsNullOrWhiteSpace(PreviousFirmwareVersion))
            throw new InvalidOperationException("No previous firmware version to rollback to");

        var temp = CurrentFirmwareVersion;
        CurrentFirmwareVersion = PreviousFirmwareVersion;
        PreviousFirmwareVersion = temp;
        LastUpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Device health information.
/// </summary>
public class DeviceHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsage { get; set; } = 0;

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsage { get; set; } = 0;

    /// <summary>
    /// Device uptime in seconds.
    /// </summary>
    public long Uptime { get; set; } = 0;

    /// <summary>
    /// Application-specific metrics (sensor readings, error rates, etc.).
    /// </summary>
    public Dictionary<string, double> ApplicationMetrics { get; set; } = new();

    /// <summary>
    /// Health check timestamp (UTC).
    /// </summary>
    public DateTime LastCheckAt { get; set; } = DateTime.UtcNow;
}
```

---

## Deployment

Represents a firmware deployment to devices.

**File:** `src/HotSwap.IoT.Firmware.Domain/Models/Deployment.cs`

```csharp
namespace HotSwap.IoT.Firmware.Domain.Models;

/// <summary>
/// Represents a firmware deployment.
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Firmware version to deploy.
    /// </summary>
    public required string FirmwareVersion { get; set; }

    /// <summary>
    /// Device model being updated.
    /// </summary>
    public required string DeviceModel { get; set; }

    /// <summary>
    /// Deployment strategy.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.Direct;

    /// <summary>
    /// Strategy-specific configuration (JSON).
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>
    /// Target device IDs.
    /// </summary>
    public List<string> TargetDeviceIds { get; set; } = new();

    /// <summary>
    /// Target device group IDs (alternative to individual device IDs).
    /// </summary>
    public List<string> TargetGroupIds { get; set; } = new();

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment progress tracking.
    /// </summary>
    public DeploymentProgress Progress { get; set; } = new();

    /// <summary>
    /// Health check configuration.
    /// </summary>
    public HealthCheckConfig HealthConfig { get; set; } = new();

    /// <summary>
    /// Rollback configuration.
    /// </summary>
    public RollbackConfig RollbackConfig { get; set; } = new();

    /// <summary>
    /// Deployment creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// User who created the deployment.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Deployment error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Validates the deployment for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(FirmwareVersion))
            errors.Add("FirmwareVersion is required");

        if (string.IsNullOrWhiteSpace(DeviceModel))
            errors.Add("DeviceModel is required");

        if (TargetDeviceIds.Count == 0 && TargetGroupIds.Count == 0)
            errors.Add("Must specify either TargetDeviceIds or TargetGroupIds");

        if (string.IsNullOrWhiteSpace(CreatedBy))
            errors.Add("CreatedBy is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Calculates deployment success rate.
    /// </summary>
    public double GetSuccessRate()
    {
        int total = Progress.TotalDevices;
        if (total == 0) return 0;
        return (double)Progress.SuccessfulDevices / total * 100.0;
    }

    /// <summary>
    /// Checks if deployment has failed threshold.
    /// </summary>
    public bool HasExceededFailureThreshold()
    {
        return GetSuccessRate() < RollbackConfig.MinimumSuccessRate;
    }

    /// <summary>
    /// Calculates estimated time to completion.
    /// </summary>
    public TimeSpan? GetEstimatedTimeToCompletion()
    {
        if (!StartedAt.HasValue || Progress.TotalDevices == 0)
            return null;

        int completed = Progress.SuccessfulDevices + Progress.FailedDevices;
        if (completed == 0) return null;

        TimeSpan elapsed = DateTime.UtcNow - StartedAt.Value;
        double averageTimePerDevice = elapsed.TotalSeconds / completed;
        int remaining = Progress.TotalDevices - completed;

        return TimeSpan.FromSeconds(averageTimePerDevice * remaining);
    }
}

/// <summary>
/// Deployment progress tracking.
/// </summary>
public class DeploymentProgress
{
    /// <summary>
    /// Total devices in deployment.
    /// </summary>
    public int TotalDevices { get; set; } = 0;

    /// <summary>
    /// Devices with pending updates.
    /// </summary>
    public int PendingDevices { get; set; } = 0;

    /// <summary>
    /// Devices currently downloading firmware.
    /// </summary>
    public int DownloadingDevices { get; set; } = 0;

    /// <summary>
    /// Devices currently installing firmware.
    /// </summary>
    public int InstallingDevices { get; set; } = 0;

    /// <summary>
    /// Devices with successful updates.
    /// </summary>
    public int SuccessfulDevices { get; set; } = 0;

    /// <summary>
    /// Devices with failed updates.
    /// </summary>
    public int FailedDevices { get; set; } = 0;

    /// <summary>
    /// Devices with rollback in progress.
    /// </summary>
    public int RollingBackDevices { get; set; } = 0;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double ProgressPercentage =>
        TotalDevices == 0 ? 0 : (double)(SuccessfulDevices + FailedDevices) / TotalDevices * 100.0;
}

/// <summary>
/// Health check configuration for deployment.
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// Enable health monitoring during deployment.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval during deployment.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Post-deployment monitoring duration.
    /// </summary>
    public TimeSpan MonitoringDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum CPU usage threshold (%).
    /// </summary>
    public double MaxCpuUsage { get; set; } = 95.0;

    /// <summary>
    /// Maximum memory usage threshold (%).
    /// </summary>
    public double MaxMemoryUsage { get; set; } = 90.0;

    /// <summary>
    /// Maximum device offline duration.
    /// </summary>
    public TimeSpan MaxOfflineDuration { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Rollback configuration for deployment.
/// </summary>
public class RollbackConfig
{
    /// <summary>
    /// Enable automatic rollback on failures.
    /// </summary>
    public bool AutoRollbackEnabled { get; set; } = true;

    /// <summary>
    /// Minimum success rate threshold (0-100).
    /// </summary>
    public double MinimumSuccessRate { get; set; } = 95.0;

    /// <summary>
    /// Maximum consecutive failures before rollback.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 5;

    /// <summary>
    /// Rollback delay after failure detection.
    /// </summary>
    public TimeSpan RollbackDelay { get; set; } = TimeSpan.FromMinutes(1);
}
```

---

## DeviceGroup

Represents a group of devices for targeted deployments.

**File:** `src/HotSwap.IoT.Firmware.Domain/Models/DeviceGroup.cs`

```csharp
namespace HotSwap.IoT.Firmware.Domain.Models;

/// <summary>
/// Represents a device group (cohort).
/// </summary>
public class DeviceGroup
{
    /// <summary>
    /// Unique group identifier (GUID format).
    /// </summary>
    public required string GroupId { get; set; }

    /// <summary>
    /// Human-readable group name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Group description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Group type (Static or Dynamic).
    /// </summary>
    public GroupType Type { get; set; } = GroupType.Static;

    /// <summary>
    /// Device IDs in the group (for static groups).
    /// </summary>
    public List<string> DeviceIds { get; set; } = new();

    /// <summary>
    /// Dynamic group rules (for dynamic groups).
    /// </summary>
    public List<GroupRule> Rules { get; set; } = new();

    /// <summary>
    /// Group creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the group.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Validates the device group for required fields and constraints.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(GroupId))
            errors.Add("GroupId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (Type == GroupType.Static && DeviceIds.Count == 0)
            errors.Add("Static group must have at least one device");

        if (Type == GroupType.Dynamic && Rules.Count == 0)
            errors.Add("Dynamic group must have at least one rule");

        if (string.IsNullOrWhiteSpace(CreatedBy))
            errors.Add("CreatedBy is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Evaluates if a device matches this group's rules (for dynamic groups).
    /// </summary>
    public bool Matches(Device device)
    {
        if (Type == GroupType.Static)
            return DeviceIds.Contains(device.DeviceId);

        // For dynamic groups, device must match ALL rules (AND logic)
        return Rules.All(rule => rule.Matches(device));
    }
}

/// <summary>
/// Dynamic group rule for device matching.
/// </summary>
public class GroupRule
{
    /// <summary>
    /// Field to match (e.g., "Region", "Model", "Tags").
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// Operator (Equals, Contains, StartsWith, etc.).
    /// </summary>
    public RuleOperator Operator { get; set; } = RuleOperator.Equals;

    /// <summary>
    /// Value to match against.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Evaluates if a device matches this rule.
    /// </summary>
    public bool Matches(Device device)
    {
        string? fieldValue = Field.ToLowerInvariant() switch
        {
            "region" => device.Region,
            "model" => device.Model,
            "firmwareversion" => device.CurrentFirmwareVersion,
            "status" => device.Status.ToString(),
            _ => null
        };

        if (fieldValue == null && Field.ToLowerInvariant() == "tags")
        {
            // Special handling for Tags (list field)
            return Operator switch
            {
                RuleOperator.Contains => device.Tags.Contains(Value, StringComparer.OrdinalIgnoreCase),
                _ => false
            };
        }

        if (fieldValue == null) return false;

        return Operator switch
        {
            RuleOperator.Equals => fieldValue.Equals(Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.NotEquals => !fieldValue.Equals(Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.Contains => fieldValue.Contains(Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.StartsWith => fieldValue.StartsWith(Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.EndsWith => fieldValue.EndsWith(Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
```

---

## FirmwareSignature

Represents cryptographic signature details.

**File:** `src/HotSwap.IoT.Firmware.Domain/Models/FirmwareSignature.cs`

```csharp
namespace HotSwap.IoT.Firmware.Domain.Models;

/// <summary>
/// Represents firmware signature details.
/// </summary>
public class FirmwareSignature
{
    /// <summary>
    /// RSA signature (base64 encoded).
    /// </summary>
    public required string Signature { get; set; }

    /// <summary>
    /// Signing algorithm (e.g., "RSA-SHA256").
    /// </summary>
    public string Algorithm { get; set; } = "RSA-SHA256";

    /// <summary>
    /// Public key for verification (PEM format).
    /// </summary>
    public required string PublicKey { get; set; }

    /// <summary>
    /// Signature creation timestamp (UTC).
    /// </summary>
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who signed the firmware.
    /// </summary>
    public required string SignedBy { get; set; }

    /// <summary>
    /// Verifies firmware signature.
    /// </summary>
    public bool Verify(byte[] firmwareBytes)
    {
        try
        {
            var rsa = System.Security.Cryptography.RSA.Create();
            var publicKeyBytes = Convert.FromBase64String(PublicKey);
            rsa.ImportRSAPublicKey(publicKeyBytes, out _);

            var signatureBytes = Convert.FromBase64String(Signature);
            return rsa.VerifyData(firmwareBytes, signatureBytes,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }
}
```

---

## Enumerations

### FirmwareStatus

**File:** `src/HotSwap.IoT.Firmware.Domain/Enums/FirmwareStatus.cs`

```csharp
namespace HotSwap.IoT.Firmware.Domain.Enums;

/// <summary>
/// Represents the status of a firmware version.
/// </summary>
public enum FirmwareStatus
{
    /// <summary>
    /// Firmware is in draft state (not yet submitted for approval).
    /// </summary>
    Draft,

    /// <summary>
    /// Firmware is pending admin approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Firmware is approved for production deployment.
    /// </summary>
    Approved,

    /// <summary>
    /// Firmware is deprecated (marked for removal).
    /// </summary>
    Deprecated
}
```

### DeviceStatus

```csharp
namespace HotSwap.IoT.Firmware.Domain.Enums;

/// <summary>
/// Represents the current status of a device.
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// Device is online and healthy.
    /// </summary>
    Online,

    /// <summary>
    /// Device is offline (no heartbeat).
    /// </summary>
    Offline,

    /// <summary>
    /// Device is updating firmware.
    /// </summary>
    Updating,

    /// <summary>
    /// Device update failed.
    /// </summary>
    UpdateFailed,

    /// <summary>
    /// Device is disabled (no updates allowed).
    /// </summary>
    Disabled
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.IoT.Firmware.Domain.Enums;

/// <summary>
/// Represents the deployment strategy.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Direct deployment to single device.
    /// </summary>
    Direct,

    /// <summary>
    /// Regional deployment (by geography).
    /// </summary>
    Regional,

    /// <summary>
    /// Canary deployment (progressive percentage-based).
    /// </summary>
    Canary,

    /// <summary>
    /// Blue-green deployment (parallel fleet).
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Rolling deployment (batch-by-batch).
    /// </summary>
    Rolling
}
```

### DeploymentStatus

```csharp
namespace HotSwap.IoT.Firmware.Domain.Enums;

/// <summary>
/// Represents the current status of a deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is pending (not yet started).
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment is in progress.
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
    /// Deployment cancelled by user.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Deployment paused (awaiting health verification).
    /// </summary>
    Paused
}
```

### GroupType

```csharp
namespace HotSwap.IoT.Firmware.Domain.Enums;

/// <summary>
/// Represents the type of device group.
/// </summary>
public enum GroupType
{
    /// <summary>
    /// Static group (manually managed device list).
    /// </summary>
    Static,

    /// <summary>
    /// Dynamic group (auto-populated by rules).
    /// </summary>
    Dynamic
}
```

### RuleOperator

```csharp
namespace HotSwap.IoT.Firmware.Domain.Enums;

/// <summary>
/// Represents the operator for group rules.
/// </summary>
public enum RuleOperator
{
    /// <summary>
    /// Equals comparison.
    /// </summary>
    Equals,

    /// <summary>
    /// Not equals comparison.
    /// </summary>
    NotEquals,

    /// <summary>
    /// Contains substring.
    /// </summary>
    Contains,

    /// <summary>
    /// Starts with prefix.
    /// </summary>
    StartsWith,

    /// <summary>
    /// Ends with suffix.
    /// </summary>
    EndsWith
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.IoT.Firmware.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.IoT.Firmware.Domain.ValueObjects;

/// <summary>
/// Result of a deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// List of devices successfully updated.
    /// </summary>
    public List<string> SuccessfulDevices { get; private set; } = new();

    /// <summary>
    /// List of devices that failed to update.
    /// </summary>
    public List<string> FailedDevices { get; private set; } = new();

    /// <summary>
    /// Error message (if deployment failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(List<string> devices)
    {
        return new DeploymentResult
        {
            Success = true,
            SuccessfulDevices = devices
        };
    }

    public static DeploymentResult Failure(string errorMessage, List<string> failedDevices)
    {
        return new DeploymentResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            FailedDevices = failedDevices
        };
    }

    public static DeploymentResult PartialSuccess(List<string> successful, List<string> failed)
    {
        return new DeploymentResult
        {
            Success = failed.Count == 0,
            SuccessfulDevices = successful,
            FailedDevices = failed
        };
    }
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.IoT.Firmware.Domain`
