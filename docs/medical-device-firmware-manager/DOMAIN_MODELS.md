# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.MedicalDevices.Domain.Models`

---

## Table of Contents

1. [MedicalDevice](#medicaldevice)
2. [Firmware](#firmware)
3. [Deployment](#deployment)
4. [ApprovalRecord](#approvalrecord)
5. [AuditLog](#auditlog)
6. [Hospital](#hospital)
7. [DeviceHealth](#devicehealth)
8. [ElectronicSignature](#electronicsignature)
9. [Enumerations](#enumerations)
10. [Value Objects](#value-objects)

---

## MedicalDevice

Represents a registered FDA-regulated medical device.

**File:** `src/HotSwap.MedicalDevices.Domain/Models/MedicalDevice.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Models;

/// <summary>
/// Represents a FDA-regulated medical device.
/// </summary>
public class MedicalDevice
{
    /// <summary>
    /// Unique device identifier (GUID format).
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// FDA Unique Device Identifier (UDI) per 21 CFR Part 801.
    /// </summary>
    public required string UDI { get; set; }

    /// <summary>
    /// Device model number (e.g., "CardiacMonitor-X200").
    /// </summary>
    public required string ModelNumber { get; set; }

    /// <summary>
    /// Device serial number (manufacturer-assigned).
    /// </summary>
    public required string SerialNumber { get; set; }

    /// <summary>
    /// Manufacturer name.
    /// </summary>
    public required string Manufacturer { get; set; }

    /// <summary>
    /// FDA device classification (Class I, II, or III).
    /// </summary>
    public DeviceClass FDAClass { get; set; } = DeviceClass.ClassII;

    /// <summary>
    /// Associated hospital identifier.
    /// </summary>
    public required string HospitalId { get; set; }

    /// <summary>
    /// Current firmware version installed on device.
    /// </summary>
    public required string CurrentFirmwareVersion { get; set; }

    /// <summary>
    /// Previous firmware version (before last update).
    /// </summary>
    public string? PreviousFirmwareVersion { get; set; }

    /// <summary>
    /// Device commissioning date (UTC).
    /// </summary>
    public DateTime CommissionedAt { get; set; }

    /// <summary>
    /// Device decommissioning date (UTC, null if active).
    /// </summary>
    public DateTime? DecommissionedAt { get; set; }

    /// <summary>
    /// Current device status.
    /// </summary>
    public DeviceStatus Status { get; set; } = DeviceStatus.Active;

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last firmware update timestamp (UTC).
    /// </summary>
    public DateTime? LastFirmwareUpdate { get; set; }

    /// <summary>
    /// Device location within hospital (e.g., "ICU Room 3").
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Device metadata (key-value pairs).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the device for required fields and FDA compliance.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeviceId))
            errors.Add("DeviceId is required");

        if (string.IsNullOrWhiteSpace(UDI))
            errors.Add("UDI is required (FDA 21 CFR Part 801)");
        else if (!ValidateUDI(UDI))
            errors.Add("UDI format is invalid");

        if (string.IsNullOrWhiteSpace(ModelNumber))
            errors.Add("ModelNumber is required");

        if (string.IsNullOrWhiteSpace(SerialNumber))
            errors.Add("SerialNumber is required");

        if (string.IsNullOrWhiteSpace(Manufacturer))
            errors.Add("Manufacturer is required");

        if (string.IsNullOrWhiteSpace(HospitalId))
            errors.Add("HospitalId is required");

        if (string.IsNullOrWhiteSpace(CurrentFirmwareVersion))
            errors.Add("CurrentFirmwareVersion is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates UDI format per FDA guidelines.
    /// </summary>
    private bool ValidateUDI(string udi)
    {
        // UDI format: (01)GTIN(21)SerialNumber or similar
        // Simplified validation - production should use FDA UDI database
        return udi.Length >= 14 && udi.Contains("(01)");
    }

    /// <summary>
    /// Checks if device is online based on heartbeat.
    /// </summary>
    public bool IsOnline()
    {
        // Device considered offline if no heartbeat in last 5 minutes
        return DateTime.UtcNow - LastHeartbeat < TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Checks if device is eligible for firmware update.
    /// </summary>
    public bool IsEligibleForUpdate()
    {
        return Status == DeviceStatus.Active &&
               IsOnline() &&
               DecommissionedAt == null;
    }
}
```

---

## Firmware

Represents a firmware package for medical devices.

**File:** `src/HotSwap.MedicalDevices.Domain/Models/Firmware.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Models;

/// <summary>
/// Represents a medical device firmware package.
/// </summary>
public class Firmware
{
    /// <summary>
    /// Unique firmware identifier (GUID format).
    /// </summary>
    public required string FirmwareId { get; set; }

    /// <summary>
    /// Firmware version (semantic versioning, e.g., "2.1.0").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Target device model number.
    /// </summary>
    public required string DeviceModel { get; set; }

    /// <summary>
    /// Firmware binary file path in storage (MinIO/S3).
    /// </summary>
    public required string BinaryFilePath { get; set; }

    /// <summary>
    /// SHA-256 checksum of firmware binary.
    /// </summary>
    public required string SHA256Checksum { get; set; }

    /// <summary>
    /// Cryptographic signature for firmware validation (RSA-4096 or ECDSA P-384).
    /// </summary>
    public required string CryptographicSignature { get; set; }

    /// <summary>
    /// Firmware binary file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Release notes describing changes (required for FDA submission).
    /// </summary>
    public required string ReleaseNotes { get; set; }

    /// <summary>
    /// Current approval status.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    /// <summary>
    /// FDA submission documentation file path (if applicable).
    /// </summary>
    public string? FDASubmissionPath { get; set; }

    /// <summary>
    /// Firmware upload timestamp (UTC).
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Firmware approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// User who uploaded the firmware.
    /// </summary>
    public required string UploadedBy { get; set; }

    /// <summary>
    /// User who approved the firmware (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Minimum compatible firmware version (for upgrade path validation).
    /// </summary>
    public string? MinCompatibleVersion { get; set; }

    /// <summary>
    /// Maximum compatible firmware version (for downgrade protection).
    /// </summary>
    public string? MaxCompatibleVersion { get; set; }

    /// <summary>
    /// Firmware metadata (key-value pairs).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Indicates if this is a critical security patch.
    /// </summary>
    public bool IsCriticalPatch { get; set; } = false;

    /// <summary>
    /// Validates the firmware for required fields and integrity.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(FirmwareId))
            errors.Add("FirmwareId is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");
        else if (!IsValidSemanticVersion(Version))
            errors.Add("Version must be semantic versioning (e.g., 2.1.0)");

        if (string.IsNullOrWhiteSpace(DeviceModel))
            errors.Add("DeviceModel is required");

        if (string.IsNullOrWhiteSpace(BinaryFilePath))
            errors.Add("BinaryFilePath is required");

        if (string.IsNullOrWhiteSpace(SHA256Checksum))
            errors.Add("SHA256Checksum is required");
        else if (SHA256Checksum.Length != 64)
            errors.Add("SHA256Checksum must be 64 characters");

        if (string.IsNullOrWhiteSpace(CryptographicSignature))
            errors.Add("CryptographicSignature is required");

        if (string.IsNullOrWhiteSpace(ReleaseNotes))
            errors.Add("ReleaseNotes are required for FDA compliance");

        if (FileSizeBytes <= 0)
            errors.Add("FileSizeBytes must be greater than 0");

        if (FileSizeBytes > 524288000) // 500 MB
            errors.Add("FileSizeBytes must not exceed 500 MB");

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates semantic versioning format.
    /// </summary>
    private bool IsValidSemanticVersion(string version)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"^\d+\.\d+\.\d+$");
        return regex.IsMatch(version);
    }

    /// <summary>
    /// Checks if firmware is approved for deployment.
    /// </summary>
    public bool IsApproved() => ApprovalStatus == ApprovalStatus.Approved;
}
```

---

## Deployment

Represents a firmware deployment operation.

**File:** `src/HotSwap.MedicalDevices.Domain/Models/Deployment.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Models;

/// <summary>
/// Represents a firmware deployment operation.
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Firmware being deployed.
    /// </summary>
    public required string FirmwareId { get; set; }

    /// <summary>
    /// Deployment strategy (Progressive, Emergency, Canary, Scheduled).
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.Progressive;

    /// <summary>
    /// Target device IDs for deployment.
    /// </summary>
    public List<string> TargetDevices { get; set; } = new();

    /// <summary>
    /// Deployment phases for progressive rollout.
    /// </summary>
    public List<DeploymentPhase> Phases { get; set; } = new();

    /// <summary>
    /// Current phase index (0-based).
    /// </summary>
    public int CurrentPhaseIndex { get; set; } = 0;

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Deployment initiation timestamp (UTC).
    /// </summary>
    public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// User who initiated the deployment.
    /// </summary>
    public required string InitiatedBy { get; set; }

    /// <summary>
    /// Deployment progress (0-100%).
    /// </summary>
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// Number of successful device updates.
    /// </summary>
    public int SuccessCount { get; set; } = 0;

    /// <summary>
    /// Number of failed device updates.
    /// </summary>
    public int FailureCount { get; set; } = 0;

    /// <summary>
    /// Error details for failed updates.
    /// </summary>
    public List<DeploymentError> Errors { get; set; } = new();

    /// <summary>
    /// Rollback timestamp (UTC, if rolled back).
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// User who initiated rollback.
    /// </summary>
    public string? RolledBackBy { get; set; }

    /// <summary>
    /// Rollback reason.
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Deployment metadata (key-value pairs).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(FirmwareId))
            errors.Add("FirmwareId is required");

        if (TargetDevices == null || TargetDevices.Count == 0)
            errors.Add("At least one target device is required");

        if (Strategy == DeploymentStrategy.Progressive && (Phases == null || Phases.Count == 0))
            errors.Add("Progressive deployment requires at least one phase");

        return errors.Count == 0;
    }

    /// <summary>
    /// Gets the current deployment phase.
    /// </summary>
    public DeploymentPhase? GetCurrentPhase()
    {
        if (Phases == null || CurrentPhaseIndex >= Phases.Count)
            return null;

        return Phases[CurrentPhaseIndex];
    }

    /// <summary>
    /// Advances to the next deployment phase.
    /// </summary>
    public bool AdvanceToNextPhase()
    {
        if (CurrentPhaseIndex < Phases.Count - 1)
        {
            CurrentPhaseIndex++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Calculates deployment success rate.
    /// </summary>
    public double GetSuccessRate()
    {
        int total = SuccessCount + FailureCount;
        return total > 0 ? (double)SuccessCount / total * 100 : 0;
    }
}

/// <summary>
/// Represents a deployment phase in progressive rollout.
/// </summary>
public class DeploymentPhase
{
    /// <summary>
    /// Phase name (e.g., "Pilot", "Regional", "Full").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Hospital cohort identifier.
    /// </summary>
    public required string HospitalCohort { get; set; }

    /// <summary>
    /// Percentage of devices to deploy in this phase (0-100).
    /// </summary>
    public int Percentage { get; set; }

    /// <summary>
    /// Target device IDs for this phase.
    /// </summary>
    public List<string> TargetDevices { get; set; } = new();

    /// <summary>
    /// Phase status.
    /// </summary>
    public PhaseStatus Status { get; set; } = PhaseStatus.Pending;

    /// <summary>
    /// Phase start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Phase completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Validation gate criteria (error rate threshold).
    /// </summary>
    public double MaxErrorRatePercent { get; set; } = 1.0;

    /// <summary>
    /// Monitoring duration before phase promotion (hours).
    /// </summary>
    public int MonitoringDurationHours { get; set; } = 24;
}

/// <summary>
/// Represents a deployment error.
/// </summary>
public class DeploymentError
{
    /// <summary>
    /// Device ID that failed.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string ErrorMessage { get; set; }

    /// <summary>
    /// Error code (for categorization).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Error timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Stack trace (for debugging).
    /// </summary>
    public string? StackTrace { get; set; }
}
```

---

## ApprovalRecord

Represents an approval record in the multi-level approval workflow.

**File:** `src/HotSwap.MedicalDevices.Domain/Models/ApprovalRecord.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Models;

/// <summary>
/// Represents an approval record for firmware deployment.
/// </summary>
public class ApprovalRecord
{
    /// <summary>
    /// Unique approval record identifier (GUID format).
    /// </summary>
    public required string ApprovalId { get; set; }

    /// <summary>
    /// Firmware ID being approved.
    /// </summary>
    public required string FirmwareId { get; set; }

    /// <summary>
    /// Approval level (Clinical, QA, Regulatory, Final).
    /// </summary>
    public ApprovalLevel Level { get; set; }

    /// <summary>
    /// Current approval status.
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.PendingReview;

    /// <summary>
    /// User who requested the approval.
    /// </summary>
    public required string RequestedBy { get; set; }

    /// <summary>
    /// Approval request timestamp (UTC).
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Assigned reviewer (user ID or role).
    /// </summary>
    public required string AssignedReviewer { get; set; }

    /// <summary>
    /// User who approved/rejected.
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Review timestamp (UTC).
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Approval decision (Approved or Rejected).
    /// </summary>
    public ApprovalDecision? Decision { get; set; }

    /// <summary>
    /// Comments from reviewer.
    /// </summary>
    public string? ReviewerComments { get; set; }

    /// <summary>
    /// Rejection reason (required if rejected).
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Electronic signature for this approval (FDA 21 CFR Part 11).
    /// </summary>
    public ElectronicSignature? Signature { get; set; }

    /// <summary>
    /// Approval expiration date (UTC).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Required documents for approval (checklist).
    /// </summary>
    public List<string> RequiredDocuments { get; set; } = new();

    /// <summary>
    /// Uploaded document file paths.
    /// </summary>
    public List<string> UploadedDocuments { get; set; } = new();

    /// <summary>
    /// Validates the approval record.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ApprovalId))
            errors.Add("ApprovalId is required");

        if (string.IsNullOrWhiteSpace(FirmwareId))
            errors.Add("FirmwareId is required");

        if (string.IsNullOrWhiteSpace(RequestedBy))
            errors.Add("RequestedBy is required");

        if (string.IsNullOrWhiteSpace(AssignedReviewer))
            errors.Add("AssignedReviewer is required");

        if (Decision == ApprovalDecision.Rejected && string.IsNullOrWhiteSpace(RejectionReason))
            errors.Add("RejectionReason is required when decision is Rejected");

        if (Decision == ApprovalDecision.Approved && Signature == null)
            errors.Add("Electronic signature is required for approval (FDA 21 CFR Part 11)");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if approval has expired.
    /// </summary>
    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Checks if approval is complete.
    /// </summary>
    public bool IsComplete() => Status == ApprovalStatus.Approved || Status == ApprovalStatus.Rejected;
}
```

---

## AuditLog

Represents an FDA 21 CFR Part 11 compliant audit log entry.

**File:** `src/HotSwap.MedicalDevices.Domain/Models/AuditLog.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Models;

/// <summary>
/// Represents an FDA 21 CFR Part 11 compliant audit log entry.
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Unique audit log entry identifier (auto-increment).
    /// </summary>
    public long AuditId { get; set; }

    /// <summary>
    /// Event timestamp (UTC, immutable).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who performed the action (who).
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// User role at time of action.
    /// </summary>
    public required string UserRole { get; set; }

    /// <summary>
    /// Action performed (what).
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Entity type affected (e.g., "Device", "Firmware", "Deployment").
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// Entity identifier (e.g., device ID, firmware ID).
    /// </summary>
    public required string EntityId { get; set; }

    /// <summary>
    /// Original value before change (for updates).
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// New value after change (for updates).
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Business justification (why) - required for critical actions.
    /// </summary>
    public string? Justification { get; set; }

    /// <summary>
    /// IP address of the user (where).
    /// </summary>
    public required string IPAddress { get; set; }

    /// <summary>
    /// Trace ID for correlation with distributed tracing.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Session ID for correlation.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Additional metadata (JSON format).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Cryptographic hash for tamper detection (SHA-256 of previous entry + current entry).
    /// </summary>
    public string? TamperDetectionHash { get; set; }

    /// <summary>
    /// Previous audit log entry ID (for chaining).
    /// </summary>
    public long? PreviousAuditId { get; set; }

    /// <summary>
    /// Calculates tamper detection hash.
    /// </summary>
    public string CalculateTamperDetectionHash(string? previousHash)
    {
        var data = $"{AuditId}|{Timestamp:O}|{UserId}|{Action}|{EntityType}|{EntityId}|{previousHash ?? ""}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Validates tamper detection hash.
    /// </summary>
    public bool ValidateTamperDetectionHash(string? previousHash)
    {
        var expectedHash = CalculateTamperDetectionHash(previousHash);
        return TamperDetectionHash == expectedHash;
    }
}
```

---

## Hospital

Represents a hospital or healthcare facility.

**File:** `src/HotSwap.MedicalDevices.Domain/Models/Hospital.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Models;

/// <summary>
/// Represents a hospital or healthcare facility.
/// </summary>
public class Hospital
{
    /// <summary>
    /// Unique hospital identifier (GUID format).
    /// </summary>
    public required string HospitalId { get; set; }

    /// <summary>
    /// Hospital name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Hospital address.
    /// </summary>
    public required string Address { get; set; }

    /// <summary>
    /// Hospital city.
    /// </summary>
    public required string City { get; set; }

    /// <summary>
    /// Hospital state/province.
    /// </summary>
    public required string State { get; set; }

    /// <summary>
    /// Hospital country.
    /// </summary>
    public required string Country { get; set; }

    /// <summary>
    /// Hospital ZIP/postal code.
    /// </summary>
    public required string PostalCode { get; set; }

    /// <summary>
    /// Hospital cohort for deployment (e.g., "pilot", "regional", "all").
    /// </summary>
    public string Cohort { get; set; } = "all";

    /// <summary>
    /// Number of registered devices at this hospital.
    /// </summary>
    public int DeviceCount { get; set; } = 0;

    /// <summary>
    /// Primary contact name.
    /// </summary>
    public required string ContactName { get; set; }

    /// <summary>
    /// Primary contact email.
    /// </summary>
    public required string ContactEmail { get; set; }

    /// <summary>
    /// Primary contact phone.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Hospital registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Hospital status.
    /// </summary>
    public HospitalStatus Status { get; set; } = HospitalStatus.Active;

    /// <summary>
    /// Validates the hospital record.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(HospitalId))
            errors.Add("HospitalId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(ContactEmail))
            errors.Add("ContactEmail is required");
        else if (!IsValidEmail(ContactEmail))
            errors.Add("ContactEmail format is invalid");

        return errors.Count == 0;
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
```

---

## DeviceHealth

Represents real-time health metrics for a medical device.

**File:** `src/HotSwap.MedicalDevices.Domain/Models/DeviceHealth.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Models;

/// <summary>
/// Represents real-time health metrics for a medical device.
/// </summary>
public class DeviceHealth
{
    /// <summary>
    /// Device identifier.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Health check timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Overall health status.
    /// </summary>
    public HealthStatus Status { get; set; } = HealthStatus.Healthy;

    /// <summary>
    /// Device uptime in seconds since last reboot.
    /// </summary>
    public long UptimeSeconds { get; set; }

    /// <summary>
    /// Error count in last hour.
    /// </summary>
    public int ErrorCountLastHour { get; set; } = 0;

    /// <summary>
    /// Critical error count in last hour.
    /// </summary>
    public int CriticalErrorCountLastHour { get; set; } = 0;

    /// <summary>
    /// Device CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Device memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsagePercent { get; set; }

    /// <summary>
    /// Network connectivity status.
    /// </summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>
    /// Battery level percentage (0-100, null for non-portable devices).
    /// </summary>
    public double? BatteryLevelPercent { get; set; }

    /// <summary>
    /// Sensor calibration status.
    /// </summary>
    public bool IsSensorCalibrated { get; set; } = true;

    /// <summary>
    /// Last error message (if any).
    /// </summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Last error timestamp (UTC).
    /// </summary>
    public DateTime? LastErrorAt { get; set; }

    /// <summary>
    /// Patient interaction count in last 24 hours (anonymized).
    /// </summary>
    public int PatientInteractionCount { get; set; } = 0;

    /// <summary>
    /// Determines if device health is critical and requires immediate attention.
    /// </summary>
    public bool IsCritical()
    {
        return Status == HealthStatus.Critical ||
               CriticalErrorCountLastHour > 0 ||
               !IsConnected ||
               (BatteryLevelPercent.HasValue && BatteryLevelPercent < 10);
    }
}
```

---

## ElectronicSignature

Represents an FDA 21 CFR Part 11 compliant electronic signature.

**File:** `src/HotSwap.MedicalDevices.Domain/Models/ElectronicSignature.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Models;

/// <summary>
/// Represents an FDA 21 CFR Part 11 compliant electronic signature.
/// </summary>
public class ElectronicSignature
{
    /// <summary>
    /// Unique signature identifier (GUID format).
    /// </summary>
    public required string SignatureId { get; set; }

    /// <summary>
    /// User who signed (signer identity).
    /// </summary>
    public required string SignedBy { get; set; }

    /// <summary>
    /// Signer's role at time of signature.
    /// </summary>
    public required string SignerRole { get; set; }

    /// <summary>
    /// Signature timestamp (UTC, immutable).
    /// </summary>
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Signature meaning (e.g., "I approve this firmware deployment").
    /// </summary>
    public required string Meaning { get; set; }

    /// <summary>
    /// Signature intent (specific action being signed).
    /// </summary>
    public required string Intent { get; set; }

    /// <summary>
    /// First factor authentication (password hash - never store plaintext).
    /// </summary>
    public required string FirstFactorHash { get; set; }

    /// <summary>
    /// Second factor authentication type (OTP, HardwareToken, Biometric).
    /// </summary>
    public string? SecondFactorType { get; set; }

    /// <summary>
    /// Cryptographic signature binding to signed record (SHA-256 hash of record + signature data).
    /// </summary>
    public required string RecordBinding { get; set; }

    /// <summary>
    /// Signed record identifier (e.g., firmware ID, deployment ID).
    /// </summary>
    public required string RecordId { get; set; }

    /// <summary>
    /// IP address of signer.
    /// </summary>
    public required string IPAddress { get; set; }

    /// <summary>
    /// Calculates cryptographic binding to record.
    /// </summary>
    public string CalculateRecordBinding(string recordData)
    {
        var data = $"{RecordId}|{SignedBy}|{SignedAt:O}|{Meaning}|{recordData}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Validates signature binding to record.
    /// </summary>
    public bool ValidateRecordBinding(string recordData)
    {
        var expectedBinding = CalculateRecordBinding(recordData);
        return RecordBinding == expectedBinding;
    }
}
```

---

## Enumerations

### DeviceClass

**File:** `src/HotSwap.MedicalDevices.Domain/Enums/DeviceClass.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// FDA device classification (21 CFR Part 860).
/// </summary>
public enum DeviceClass
{
    /// <summary>
    /// Class I - Low risk (e.g., bandages, examination gloves).
    /// </summary>
    ClassI,

    /// <summary>
    /// Class II - Moderate risk (e.g., infusion pumps, surgical drapes).
    /// </summary>
    ClassII,

    /// <summary>
    /// Class III - High risk (e.g., heart valves, pacemakers).
    /// </summary>
    ClassIII
}
```

### DeviceStatus

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Medical device status.
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// Device is active and in use.
    /// </summary>
    Active,

    /// <summary>
    /// Device is offline (no heartbeat).
    /// </summary>
    Offline,

    /// <summary>
    /// Device is in maintenance mode.
    /// </summary>
    Maintenance,

    /// <summary>
    /// Device is decommissioned.
    /// </summary>
    Decommissioned,

    /// <summary>
    /// Device has been recalled (FDA field correction).
    /// </summary>
    Recalled
}
```

### ApprovalStatus

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Approval status for firmware.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Draft - not yet submitted for approval.
    /// </summary>
    Draft,

    /// <summary>
    /// Pending review by assigned reviewer.
    /// </summary>
    PendingReview,

    /// <summary>
    /// Under review by assigned reviewer.
    /// </summary>
    InReview,

    /// <summary>
    /// Approved for deployment.
    /// </summary>
    Approved,

    /// <summary>
    /// Rejected - cannot be deployed.
    /// </summary>
    Rejected,

    /// <summary>
    /// Expired - approval expired before deployment.
    /// </summary>
    Expired
}
```

### ApprovalLevel

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Approval workflow level.
/// </summary>
public enum ApprovalLevel
{
    /// <summary>
    /// Clinical engineering review (safety validation).
    /// </summary>
    Clinical,

    /// <summary>
    /// Quality assurance review (testing verification).
    /// </summary>
    QA,

    /// <summary>
    /// Regulatory affairs review (FDA compliance).
    /// </summary>
    Regulatory,

    /// <summary>
    /// Final executive approval (sign-off).
    /// </summary>
    Final
}
```

### ApprovalDecision

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Approval decision.
/// </summary>
public enum ApprovalDecision
{
    /// <summary>
    /// Firmware approved for deployment.
    /// </summary>
    Approved,

    /// <summary>
    /// Firmware rejected.
    /// </summary>
    Rejected
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Firmware deployment strategy.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Progressive rollout to hospital cohorts (10% → 50% → 100%).
    /// </summary>
    Progressive,

    /// <summary>
    /// Emergency deployment to all devices (critical patches).
    /// </summary>
    Emergency,

    /// <summary>
    /// Canary deployment to test devices first.
    /// </summary>
    Canary,

    /// <summary>
    /// Scheduled deployment during maintenance windows.
    /// </summary>
    Scheduled
}
```

### DeploymentStatus

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Deployment status.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment pending initiation.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment paused (manual hold).
    /// </summary>
    Paused,

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
    RolledBack
}
```

### PhaseStatus

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Deployment phase status.
/// </summary>
public enum PhaseStatus
{
    /// <summary>
    /// Phase pending initiation.
    /// </summary>
    Pending,

    /// <summary>
    /// Phase in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Phase monitoring (validation gate).
    /// </summary>
    Monitoring,

    /// <summary>
    /// Phase completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Phase failed validation.
    /// </summary>
    Failed
}
```

### HealthStatus

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Device health status.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Device is healthy (no issues).
    /// </summary>
    Healthy,

    /// <summary>
    /// Device has warnings (non-critical issues).
    /// </summary>
    Warning,

    /// <summary>
    /// Device is degraded (performance issues).
    /// </summary>
    Degraded,

    /// <summary>
    /// Device is critical (requires immediate attention).
    /// </summary>
    Critical,

    /// <summary>
    /// Device is offline (no heartbeat).
    /// </summary>
    Offline
}
```

### HospitalStatus

```csharp
namespace HotSwap.MedicalDevices.Domain.Enums;

/// <summary>
/// Hospital status.
/// </summary>
public enum HospitalStatus
{
    /// <summary>
    /// Hospital is active.
    /// </summary>
    Active,

    /// <summary>
    /// Hospital is inactive (no deployments allowed).
    /// </summary>
    Inactive,

    /// <summary>
    /// Hospital is in pilot program.
    /// </summary>
    Pilot
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.MedicalDevices.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.MedicalDevices.Domain.ValueObjects;

/// <summary>
/// Result of a device firmware deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Device ID that was deployed to.
    /// </summary>
    public string DeviceId { get; private set; } = string.Empty;

    /// <summary>
    /// Error message (if deployment failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Deployment duration in milliseconds.
    /// </summary>
    public long DurationMs { get; private set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(string deviceId, long durationMs)
    {
        return new DeploymentResult
        {
            Success = true,
            DeviceId = deviceId,
            DurationMs = durationMs
        };
    }

    public static DeploymentResult Failure(string deviceId, string errorMessage)
    {
        return new DeploymentResult
        {
            Success = false,
            DeviceId = deviceId,
            ErrorMessage = errorMessage,
            DurationMs = 0
        };
    }
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.MedicalDevices.Domain`
