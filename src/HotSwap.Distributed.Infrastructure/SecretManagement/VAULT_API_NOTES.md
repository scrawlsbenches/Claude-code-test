# VaultSecretService API Compatibility Notes

**Status**: Work In Progress (VaultSecretService.cs.wip)
**VaultSharp Version**: 1.17.5.1
**Last Updated**: 2025-11-20

## Overview

The `VaultSecretService.cs.wip` implementation requires API updates to work with VaultSharp 1.17.5.1. The implementation is architecturally complete but needs the following API compatibility fixes.

## Verified: Vault Works in This Environment ✅

HashiCorp Vault can run successfully in this environment:
- **Binary**: Downloaded and tested successfully (v1.15.4)
- **Dev Mode**: Starts and runs on http://127.0.0.1:8200
- **API**: Responds to HTTP requests
- **Authentication**: Token-based auth working

## API Incompatibilities to Fix

### 1. VaultApiException Namespace/Type

**Error**: `The type or namespace name 'VaultApiException' could not be found`
**Lines**: 93, 152, 231, 429, 631

**Investigation Needed**:
- Check if `VaultApiException` exists in VaultSharp 1.17.5.1
- Possible alternatives:
  - `VaultSharp.Core.VaultApiException`
  - Different exception type in newer versions
  - Check VaultSharp documentation for exception handling

**Fix**: Add correct using directive or update exception handling

### 2. ReadSecretVersionAsync Method

**Error**: `'IKeyValueSecretsEngineV2' does not contain a definition for 'ReadSecretVersionAsync'`
**Line**: 117

**Issue**: API method name or signature changed in VaultSharp 1.17.5.1

**Investigation Needed**:
- Check VaultSharp 1.17.5.1 API for reading specific secret versions
- Possible alternatives:
  - Method renamed to `ReadSecretAsync` with version parameter
  - Different method signature
  - Check `IKeyValueSecretsEngineV2` interface definition

**Current Code**:
```csharp
var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretVersionAsync(
    path: secretId,
    version: version,
    mountPoint: _mountPoint);
```

### 3. Metadata CreatedTime Type Mismatch

**Error**: `'string' does not contain a definition for 'AddDays'` and `Cannot implicitly convert type 'string' to 'System.DateTime'`
**Lines**: 206, 209, 218

**Issue**: `result.Data.CreatedTime` is a `DateTime`, not a `string`

**Fix**: Remove `.ToString()` and use directly:
```csharp
// Current (WRONG):
var createdTime = result.Data.CreatedTime;
// ...
nextRotationAt = createdTime.AddDays(...); // createdTime is DateTime, not string

// Correct:
var createdTime = result.Data.CreatedTime; // Already DateTime
nextRotationAt = createdTime.AddDays(rotationPolicy.RotationIntervalDays.Value);
```

### 4. WriteSecretMetadataAsync Parameter Name

**Error**: `The best overload for 'WriteSecretMetadataAsync' does not have a parameter named 'customMetadata'`
**Lines**: 302, 395, 561

**Issue**: Parameter name changed or method signature different in VaultSharp 1.17.5.1

**Investigation Needed**:
- Check actual parameter name for `WriteSecretMetadataAsync`
- Possible alternatives:
  - `metadata` instead of `customMetadata`
  - Different method entirely
  - Check `IKeyValueSecretsEngineV2` interface definition

**Current Code**:
```csharp
await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretMetadataAsync(
    path: secretId,
    customMetadata: customMetadata,  // Parameter name issue
    mountPoint: _mountPoint);
```

## How to Complete VaultSecretService

### Step 1: Investigate VaultSharp 1.17.5.1 API

```bash
# Check VaultSharp package documentation
dotnet list package | grep VaultSharp
# Version: 1.17.5.1

# Option 1: Check NuGet package page
# https://www.nuget.org/packages/VaultSharp/1.17.5.1

# Option 2: Decompile VaultSharp.dll to check API
# Location: ~/.nuget/packages/vaultsharp/1.17.5.1/lib/netstandard2.0/VaultSharp.dll
```

### Step 2: Fix API Calls

Update each method call to match VaultSharp 1.17.5.1 API:
1. Fix exception handling (VaultApiException)
2. Fix ReadSecretVersionAsync call
3. Remove string conversion for CreatedTime (it's already DateTime)
4. Fix WriteSecretMetadataAsync parameter name

### Step 3: Test Against Live Vault

```bash
# Start Vault in dev mode
/tmp/vault server -dev \
  -dev-root-token-id="dev-token-123" \
  -dev-listen-address="127.0.0.1:8200" &

# Run unit tests
dotnet test --filter "FullyQualifiedName~VaultSecretService"
```

### Step 4: Write Integration Tests

Create `tests/HotSwap.Distributed.IntegrationTests/Tests/VaultSecretServiceIntegrationTests.cs` to test:
- SetSecretAsync
- GetSecretAsync
- GetSecretVersionAsync
- RotateSecretAsync
- DeleteSecretAsync
- ListSecretsAsync
- Full lifecycle test

## Current Workaround

**InMemorySecretService is fully functional** and suitable for:
- Development environments
- Testing environments
- Single-instance deployments
- Proof-of-concept work

For production with distributed deployments, complete VaultSecretService or implement alternative secret store (Azure Key Vault, AWS Secrets Manager, etc.).

## Task Status

- ✅ **ISecretService interface** - Complete
- ✅ **InMemorySecretService** - Complete and tested
- ⚠️ **VaultSecretService** - Architecture complete, API compatibility pending
- ✅ **Secret versioning** - Working in InMemorySecretService
- ✅ **Rotation policies** - Working in InMemorySecretService
- ✅ **Background service** - Working with InMemorySecretService

## References

- VaultSharp GitHub: https://github.com/rajanadar/VaultSharp
- VaultSharp NuGet: https://www.nuget.org/packages/VaultSharp
- HashiCorp Vault API: https://developer.hashicorp.com/vault/api-docs
- KV Secrets Engine v2: https://developer.hashicorp.com/vault/api-docs/secret/kv/kv-v2
