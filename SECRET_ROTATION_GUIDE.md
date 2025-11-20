# Secret Rotation System - Implementation Guide

**Version:** 1.0
**Date:** 2025-11-20
**Status:** ✅ Functional (Development/Testing Ready)

## Overview

This guide documents the secret rotation system implemented for HotSwap.Distributed, providing automatic rotation, versioning, and expiration monitoring for sensitive credentials.

## Table of Contents

1. [Architecture](#architecture)
2. [Configuration](#configuration)
3. [Usage Examples](#usage-examples)
4. [Rotation Policies](#rotation-policies)
5. [Manual Rotation Procedures](#manual-rotation-procedures)
6. [Monitoring & Alerts](#monitoring--alerts)
7. [Troubleshooting](#troubleshooting)
8. [Production Deployment](#production-deployment)

---

## Architecture

### Components

| Component | Purpose | Status |
|-----------|---------|--------|
| **ISecretService** | Abstraction layer for secret management | ✅ Complete |
| **InMemorySecretService** | Development/testing implementation | ✅ Complete |
| **VaultSecretService** | HashiCorp Vault integration | ⚠️ Partial (WIP) |
| **SecretRotationBackgroundService** | Automatic rotation scheduler | ✅ Complete |
| **SecretModels** | Domain models (versioning, policies) | ✅ Complete |

### Secret Lifecycle

```
┌─────────────┐
│   Create    │─────┐
│   Secret    │     │
└─────────────┘     │
                    ▼
┌─────────────────────────────────────┐
│       Active (Version N)            │
│  - Used for token generation        │
│  - Validates incoming tokens        │
└─────────────────────────────────────┘
                    │
         Rotation Trigger:
      - Policy interval reached
      - Manual rotation requested
      - Expiration approaching
                    │
                    ▼
┌─────────────────────────────────────┐
│   Rotation Window (24-48 hours)    │
│  - Current version: N (active)      │
│  - Previous version: N-1 (valid)    │
│  - Both keys validate tokens        │
└─────────────────────────────────────┘
                    │
         Window expires
                    │
                    ▼
┌─────────────────────────────────────┐
│    New Active (Version N+1)         │
│  - Previous version N-1 deprecated  │
│  - Only version N validates tokens  │
└─────────────────────────────────────┘
```

---

## Configuration

### appsettings.json

```json
{
  "SecretRotation": {
    "Enabled": true,
    "CheckIntervalMinutes": 60,
    "DefaultRotationPolicy": {
      "RotationIntervalDays": 90,
      "MaxAgeDays": 365,
      "NotificationThresholdDays": 7,
      "RotationWindowHours": 24,
      "EnableAutomaticRotation": true,
      "NotificationRecipients": [
        "security-team@example.com",
        "ops-team@example.com"
      ]
    },
    "JwtSigningKeyPolicy": {
      "RotationIntervalDays": 30,
      "MaxAgeDays": 90,
      "NotificationThresholdDays": 7,
      "RotationWindowHours": 48,
      "EnableAutomaticRotation": true,
      "NotificationRecipients": [
        "security-team@example.com"
      ]
    }
  }
}
```

### Configuration Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | bool | true | Enable/disable automatic rotation |
| `CheckIntervalMinutes` | int | 60 | How often to check for rotations (minutes) |
| `RotationIntervalDays` | int? | null | Days between rotations (null = no auto-rotation) |
| `MaxAgeDays` | int? | null | Maximum age before forced rotation |
| `NotificationThresholdDays` | int | 7 | Days before expiration to send warnings |
| `RotationWindowHours` | int | 24 | Hours both old/new keys are valid |
| `EnableAutomaticRotation` | bool | false | Auto-rotate on interval |
| `NotificationRecipients` | string[] | [] | List of email/webhook recipients |

---

## Usage Examples

### Creating a Secret with Rotation Policy

```csharp
var secretService = serviceProvider.GetRequiredService<ISecretService>();

var rotationPolicy = new RotationPolicy
{
    RotationIntervalDays = 30,
    MaxAgeDays = 90,
    NotificationThresholdDays = 7,
    RotationWindowHours = 24,
    EnableAutomaticRotation = true,
    NotificationRecipients = new List<string>
    {
        "security@example.com"
    }
};

var tags = new Dictionary<string, string>
{
    { "environment", "production" },
    { "service", "api" }
};

var secret = await secretService.SetSecretAsync(
    secretId: "jwt-signing-key",
    value: "your-secret-key-here",
    rotationPolicy: rotationPolicy,
    tags: tags
);

Console.WriteLine($"Secret created: Version {secret.Version}");
```

### Retrieving Current Secret

```csharp
// Get current active version
var currentSecret = await secretService.GetSecretAsync("jwt-signing-key");
if (currentSecret != null)
{
    Console.WriteLine($"Current Version: {currentSecret.Version}");
    Console.WriteLine($"Expires At: {currentSecret.ExpiresAt}");
}
```

### Retrieving Specific Version

```csharp
// Get specific version (e.g., during rotation window)
var previousSecret = await secretService.GetSecretVersionAsync(
    secretId: "jwt-signing-key",
    version: 5
);

if (previousSecret != null)
{
    Console.WriteLine($"Version 5 Value: {previousSecret.Value}");
}
```

### Manual Rotation

```csharp
// Rotate secret manually (generates new random value)
var result = await secretService.RotateSecretAsync("jwt-signing-key");

if (result.Success)
{
    Console.WriteLine($"Rotated from v{result.PreviousVersion} to v{result.NewVersion}");
    Console.WriteLine($"Rotation window ends: {result.RotationWindowEndsAt}");
}
else
{
    Console.WriteLine($"Rotation failed: {result.ErrorMessage}");
}

// Rotate with custom value
var customResult = await secretService.RotateSecretAsync(
    secretId: "jwt-signing-key",
    newValue: "your-new-secret-value"
);
```

### Checking Secret Metadata

```csharp
var metadata = await secretService.GetSecretMetadataAsync("jwt-signing-key");
if (metadata != null)
{
    Console.WriteLine($"Current Version: {metadata.CurrentVersion}");
    Console.WriteLine($"Previous Version: {metadata.PreviousVersion}");
    Console.WriteLine($"In Rotation Window: {metadata.IsInRotationWindow}");
    Console.WriteLine($"Days Until Expiration: {metadata.DaysUntilExpiration}");
    Console.WriteLine($"Next Rotation: {metadata.NextRotationAt}");
}
```

### Listing All Secrets

```csharp
// List all secrets
var allSecrets = await secretService.ListSecretsAsync();
Console.WriteLine($"Total secrets: {allSecrets.Count}");

// Filter by tags
var prodSecrets = await secretService.ListSecretsAsync(
    tags: new Dictionary<string, string> { { "environment", "production" } }
);
Console.WriteLine($"Production secrets: {prodSecrets.Count}");
```

### Extending Rotation Window

```csharp
// Extend rotation window by 24 hours (for gradual rollout)
var extended = await secretService.ExtendRotationWindowAsync(
    secretId: "jwt-signing-key",
    additionalHours: 24
);

if (extended)
{
    Console.WriteLine("Rotation window extended successfully");
}
```

---

## Rotation Policies

### Recommended Policies by Secret Type

| Secret Type | Rotation Interval | Max Age | Rotation Window | Auto-Rotate |
|-------------|-------------------|---------|-----------------|-------------|
| **JWT Signing Key** | 30 days | 90 days | 48 hours | Yes |
| **Database Password** | 90 days | 365 days | 24 hours | Yes |
| **API Key (External)** | 180 days | 730 days | 72 hours | No (manual) |
| **Encryption Key** | 365 days | 730 days | 7 days | Yes |
| **Service Account** | 60 days | 180 days | 24 hours | Yes |

### Policy Design Guidelines

1. **Rotation Interval**: Based on sensitivity and compliance requirements
   - Critical secrets: 30-60 days
   - Standard secrets: 90-180 days
   - Low-risk secrets: 180-365 days

2. **Rotation Window**: Based on deployment complexity
   - Simple apps: 24 hours
   - Distributed systems: 48-72 hours
   - Multi-region: 7 days

3. **Max Age**: Compliance-driven
   - PCI-DSS: 90 days for cryptographic keys
   - SOC 2: 90-180 days for service accounts
   - Custom policies: Define per-organization

---

## Manual Rotation Procedures

### Emergency Rotation (Suspected Compromise)

```bash
# 1. Immediately rotate the compromised secret
curl -X POST https://api.example.com/api/v1/secrets/jwt-signing-key/rotate \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "newValue": "emergency-generated-secure-value",
    "rotationWindowHours": 1
  }'

# 2. Monitor rotation status
curl https://api.example.com/api/v1/secrets/jwt-signing-key/metadata \
  -H "Authorization: Bearer $ADMIN_TOKEN"

# 3. After rotation window: Revoke old tokens (if applicable)
# This depends on your token revocation strategy
```

### Scheduled Maintenance Rotation

```csharp
// 1. Plan rotation during low-traffic window
var metadata = await secretService.GetSecretMetadataAsync("jwt-signing-key");
Console.WriteLine($"Current rotation due: {metadata.NextRotationAt}");

// 2. Perform rotation
var result = await secretService.RotateSecretAsync("jwt-signing-key");

// 3. Verify success
if (result.Success)
{
    Console.WriteLine("Rotation successful");
    Console.WriteLine($"Old version {result.PreviousVersion} valid until: {result.RotationWindowEndsAt}");
}

// 4. Monitor application metrics during rotation window
// - Check error rates
// - Verify both keys are being used
// - Confirm no authentication failures
```

---

## Monitoring & Alerts

### Key Metrics to Monitor

1. **Secret Age**
   - Current age of each secret
   - Alert: > 80% of MaxAgeDays

2. **Rotation Success Rate**
   - Percentage of successful rotations
   - Alert: < 95% success rate

3. **Rotation Window Health**
   - Number of secrets currently in rotation
   - Alert: > 10 simultaneous rotations

4. **Time Since Last Rotation**
   - Days since last successful rotation
   - Alert: Exceeds RotationIntervalDays + 7

### Log Monitoring Queries

```bash
# Check rotation activity (last 24 hours)
grep "Successfully rotated secret" app.log | grep "$(date -d '1 day ago' +%Y-%m-%d)"

# Find rotation failures
grep "Failed to rotate secret" app.log

# Check expiration warnings
grep "approaching expiration" app.log

# Monitor rotation window status
grep "Rotation window" app.log
```

### Health Check Endpoint

```bash
# Check secret rotation service health
curl https://api.example.com/health

# Expected response:
# {
#   "status": "Healthy",
#   "services": {
#     "SecretRotationService": "Running",
#     "LastCheck": "2025-11-20T10:30:00Z"
#   }
# }
```

---

## Troubleshooting

### Secret Rotation Fails

**Symptom**: Rotation returns `Success: false`

**Causes:**
1. Storage backend unavailable
2. Permissions insufficient
3. Secret not found
4. Policy validation failure

**Resolution:**
```bash
# 1. Check service logs
grep "Failed to rotate secret.*jwt-signing-key" app.log

# 2. Verify secret exists
curl https://api.example.com/api/v1/secrets/jwt-signing-key/metadata

# 3. Check service permissions
# Ensure ISecretService implementation has write access

# 4. Manually retry rotation
curl -X POST https://api.example.com/api/v1/secrets/jwt-signing-key/rotate
```

### Authentication Failures During Rotation

**Symptom**: 401 Unauthorized errors during rotation window

**Causes:**
1. Previous version not being validated
2. Rotation window expired
3. Token generated with wrong key version

**Resolution:**
```csharp
// 1. Verify rotation window is active
var metadata = await secretService.GetSecretMetadataAsync("jwt-signing-key");
if (metadata.IsInRotationWindow)
{
    Console.WriteLine("Rotation window active");
    Console.WriteLine($"Previous version: {metadata.PreviousVersion}");
}

// 2. Extend rotation window if needed
await secretService.ExtendRotationWindowAsync("jwt-signing-key", 24);

// 3. Check JWT service is using both keys
// (Requires Task 16.5 implementation - see TODO)
```

### Background Service Not Running

**Symptom**: No automatic rotations occurring

**Causes:**
1. Service disabled in configuration
2. Application not restarted
3. Service crashed

**Resolution:**
```bash
# 1. Check configuration
grep -A 5 '"SecretRotation"' appsettings.json

# 2. Check service registration
grep "SecretRotationBackgroundService" app.log

# 3. Restart application
systemctl restart hotswap-api

# 4. Verify service started
grep "Secret rotation background service starting" app.log
```

---

## Production Deployment

### Pre-Deployment Checklist

- [ ] **Configure Vault/Secret Store**
  - Set up HashiCorp Vault instance
  - Configure auth methods (AppRole recommended)
  - Create KV v2 secrets engine

- [ ] **Update Configuration**
  - Set production rotation policies
  - Configure notification recipients
  - Enable automatic rotation

- [ ] **Security Hardening**
  - Use environment variables for sensitive config
  - Enable TLS for Vault connections
  - Rotate default development secrets

- [ ] **Monitoring Setup**
  - Configure alerting for rotation failures
  - Set up secret age dashboards
  - Enable audit logging

- [ ] **Test Rotation**
  - Perform test rotation in staging
  - Verify application continues functioning
  - Test rollback procedures

### Deployment Steps

```bash
# 1. Deploy application with secret rotation enabled
dotnet publish -c Release
systemctl restart hotswap-api

# 2. Initialize secrets in production store
# (Use secure process - do not log secrets)

# 3. Verify service started
curl https://api.example.com/health

# 4. Monitor first automatic rotation
tail -f /var/log/hotswap/app.log | grep "Secret rotation"

# 5. Validate no service disruptions
# - Check error rates
# - Monitor authentication success rates
# - Verify API latency unchanged
```

### Rollback Procedures

```bash
# If rotation causes issues:

# 1. Extend current rotation window
curl -X POST https://api.example.com/api/v1/secrets/jwt-signing-key/extend \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"additionalHours": 48}'

# 2. Temporarily disable automatic rotation
# Update appsettings.json:
{
  "SecretRotation": {
    "Enabled": false
  }
}

# 3. Restart application
systemctl restart hotswap-api

# 4. Investigate root cause before re-enabling
```

---

## Implementation Status

### Completed (✅)

- [x] ISecretService abstraction layer (Task 16.1)
- [x] InMemorySecretService for development (Task 16.2)
- [x] SecretMetadata, SecretVersion, RotationPolicy models (Task 16.1)
- [x] SecretRotation configuration support (Task 16.3)
- [x] Automatic rotation background service (Task 16.4)
- [x] Secret expiration monitoring (Task 16.6)
- [x] Rotation result tracking and logging (Task 16.4)

### Partial (⚠️)

- [ ] VaultSecretService HashiCorp Vault integration (Task 16.2)
  - Implementation exists but needs API verification
  - Requires access to actual Vault instance for testing

### Pending (📋)

- [ ] JwtTokenService rotation key support (Task 16.5)
  - Update to validate with both current and previous keys
  - Implement during-rotation key fallback logic

- [ ] Comprehensive unit tests (Task 16.7)
  - Test all ISecretService implementations
  - Test rotation workflows and edge cases
  - Test background service logic

---

## Support & Contact

**Documentation**: See TASK_LIST.md for detailed implementation roadmap
**Issues**: Report via GitHub Issues
**Security**: For security-related issues, contact security@example.com

---

**Last Updated**: 2025-11-20
**Version**: 1.0
**Status**: Ready for Development/Testing Use
