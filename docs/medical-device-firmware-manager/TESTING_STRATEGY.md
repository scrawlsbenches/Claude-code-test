# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 90%+
**Test Count:** 500+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The medical device firmware manager follows **Test-Driven Development (TDD)** with comprehensive test coverage including FDA compliance tests.

### Test Pyramid

```
                 ▲
                / \
               /E2E\           4% - End-to-End Tests (20 tests)
              /_____\
             /       \
            /Integration\      16% - Integration Tests (80 tests)
           /___________\
          /             \
         /  Unit Tests   \     80% - Unit Tests (400 tests)
        /_________________\
```

**Total Tests:** 500+ tests (including 20 compliance-specific tests)

---

## Test Categories

### 1. Unit Tests (400+ tests)

**Scope:** Test individual components in isolation

**Components:**
- Domain models (validation, business logic)
- Approval workflow (multi-level approvals)
- Electronic signatures (FDA 21 CFR Part 11)
- Audit logging (tamper detection)
- Deployment strategies
- Rollback logic

**Example Test:**
```csharp
[Fact]
public void MedicalDevice_WithValidUDI_PassesValidation()
{
    // Arrange
    var device = new MedicalDevice
    {
        DeviceId = "DEV-001",
        UDI = "(01)00643169007222(21)SN12345",
        ModelNumber = "CardiacMonitor-X200",
        SerialNumber = "SN-2025-001",
        Manufacturer = "MedTech Corp",
        HospitalId = "HOSP-001",
        CurrentFirmwareVersion = "2.1.0"
    };

    // Act
    var isValid = device.IsValid(out var errors);

    // Assert
    isValid.Should().BeTrue();
    errors.Should().BeEmpty();
}
```

---

### 2. Integration Tests (80+ tests)

**Scope:** Test component interactions

**Test Areas:**
- API endpoints (CRUD operations)
- Database persistence (PostgreSQL)
- Firmware storage (MinIO)
- Approval workflow integration
- Audit log chaining
- Health monitoring

**Example Test:**
```csharp
[Fact]
public async Task RegisterDevice_WithValidData_CreatesAuditLogEntry()
{
    // Arrange
    var client = _factory.CreateClient();
    var device = new RegisterDeviceRequest { /* ... */ };

    // Act
    var response = await client.PostAsJsonAsync("/api/v1/devices", device);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    
    // Verify audit log created
    var auditLogs = await _auditLogRepository.GetByEntityType("Device");
    auditLogs.Should().ContainSingle(log => 
        log.Action == "DeviceRegistered" && 
        log.EntityId.Contains("DEV-"));
}
```

---

### 3. End-to-End Tests (20+ tests)

**Scope:** Test complete user workflows

**Scenarios:**
- Complete firmware deployment lifecycle
- Approval workflow (all levels)
- Rollback on error detection
- Compliance report generation

**Example Test:**
```csharp
[Fact]
public async Task CompleteFirmwareDeployment_WithProgressiveRollout_Succeeds()
{
    // 1. Upload firmware
    var firmware = await UploadFirmwareAsync("2.2.0");
    
    // 2. Request approvals (Clinical, QA, Regulatory)
    await RequestAndApproveAsync(firmware.FirmwareId, ApprovalLevel.Clinical);
    await RequestAndApproveAsync(firmware.FirmwareId, ApprovalLevel.QA);
    await RequestAndApproveAsync(firmware.FirmwareId, ApprovalLevel.Regulatory);
    
    // 3. Create progressive deployment
    var deployment = await CreateDeploymentAsync(firmware.FirmwareId);
    
    // 4. Validate deployment completes all phases
    await ValidatePhaseCompletionAsync(deployment.DeploymentId, "Pilot");
    await ValidatePhaseCompletionAsync(deployment.DeploymentId, "Regional");
    await ValidatePhaseCompletionAsync(deployment.DeploymentId, "Full");
    
    // 5. Verify audit trail
    var auditLogs = await GetAuditLogsAsync(firmware.FirmwareId);
    auditLogs.Should().HaveCountGreaterThan(10);
}
```

---

### 4. Compliance Tests (20+ tests)

**Scope:** Validate FDA 21 CFR Part 11 compliance

**Test Areas:**
- Electronic signature validation
- Audit trail completeness
- Tamper detection
- Record retention
- Signature binding

**Example Test:**
```csharp
[Fact]
public void AuditLog_TamperDetection_DetectsModification()
{
    // Arrange
    var log1 = CreateAuditLog(auditId: 1);
    var log2 = CreateAuditLog(auditId: 2, previousAuditId: 1);
    
    log1.TamperDetectionHash = log1.CalculateTamperDetectionHash(null);
    log2.TamperDetectionHash = log2.CalculateTamperDetectionHash(log1.TamperDetectionHash);

    // Act - Tamper with log1
    log1.Action = "ModifiedAction";

    // Assert - Tamper detection should fail
    log2.ValidateTamperDetectionHash(log1.TamperDetectionHash).Should().BeFalse();
}

[Fact]
public void ElectronicSignature_WithValidTwoFactorAuth_CreatesValidSignature()
{
    // Arrange
    var approval = CreateApprovalRecord();
    var password = "SecurePassword123!";
    var otpCode = "123456";

    // Act
    var signature = _signatureService.CreateSignature(
        approval,
        password,
        otpCode,
        "I approve this firmware for deployment"
    );

    // Assert
    signature.Should().NotBeNull();
    signature.SignedBy.Should().Be(approval.ReviewedBy);
    signature.SecondFactorType.Should().Be("OTP");
    signature.ValidateRecordBinding(approval.FirmwareId).Should().BeTrue();
}
```

---

### 5. Performance Tests

**Scope:** Validate performance targets

**Test Scenarios:**
- Concurrent deployments (100+)
- Large device fleet (1000+ devices)
- Audit log query performance
- Firmware upload (100MB in < 60s)

**Example Test:**
```csharp
[Fact]
public async Task ConcurrentDeployments_100Deployments_MeetsLatencyTarget()
{
    // Arrange
    var deployments = Enumerable.Range(1, 100)
        .Select(i => CreateDeploymentRequest())
        .ToList();

    var stopwatch = Stopwatch.StartNew();

    // Act
    var tasks = deployments.Select(d => _deploymentService.CreateAsync(d));
    await Task.WhenAll(tasks);

    stopwatch.Stop();

    // Assert
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000); // < 30 seconds
}
```

---

## TDD Workflow

### Red-Green-Refactor Cycle

1. **Red** - Write failing test first
2. **Green** - Write minimum code to pass test
3. **Refactor** - Improve code while keeping tests passing

**Example:**

```csharp
// 1. RED - Write failing test
[Fact]
public void ProgressiveDeployment_PhaseValidationFails_RollsBack()
{
    // This test will fail initially
    var deployment = new Deployment { /* ... */ };
    deployment.AdvanceToNextPhase().Should().BeFalse();
}

// 2. GREEN - Implement minimum code
public class Deployment
{
    public bool AdvanceToNextPhase()
    {
        // Minimum implementation to pass test
        return false;
    }
}

// 3. REFACTOR - Improve implementation
public class Deployment
{
    public bool AdvanceToNextPhase()
    {
        if (!ValidateCurrentPhase())
            return false;
            
        if (CurrentPhaseIndex < Phases.Count - 1)
        {
            CurrentPhaseIndex++;
            return true;
        }
        return false;
    }
}
```

---

## Test Coverage Requirements

**Minimum Coverage:**
- Domain Models: 95%+
- Approval Workflow: 95%+
- Audit Logging: 100% (critical for compliance)
- Deployment Strategies: 90%+
- API Endpoints: 85%+
- Overall: 90%+

**Coverage Enforcement:**
```bash
dotnet test /p:CollectCoverage=true /p:CoverageReportsDirectory=./coverage
```

---

## CI/CD Integration

### Test Execution

**Pre-Commit:**
```bash
# Run unit tests (fast feedback)
dotnet test --filter Category=Unit
```

**Pre-Merge:**
```bash
# Run all tests (unit + integration)
dotnet test
```

**Post-Merge:**
```bash
# Run all tests including E2E and compliance
dotnet test --filter Category!=Performance
```

**Nightly:**
```bash
# Run performance tests
dotnet test --filter Category=Performance
```

---

**Last Updated:** 2025-11-23
