# Appendix B: No .NET SDK Checklist (Detailed)

**Purpose**: Comprehensive checklist for development without local .NET SDK

**Last Updated**: 2025-11-16

---

## When to Use This Checklist

**Use this checklist when .NET SDK is NOT available**, such as:
- Claude Code web environment without SDK installed
- Restricted development environments
- Environments where SDK installation is not permitted
- Remote systems without build tools

**⚠️ CRITICAL WARNING**: Since you cannot run build/test locally, you MUST be extra careful with code review and validation. You are relying entirely on CI/CD for build verification.

---

## Quick Overview

When working without .NET SDK:

1. **✅ Read ALL type definitions** before using them (don't guess)
2. **✅ Verify package references** in .csproj files manually
3. **✅ Review code changes** carefully for syntax errors
4. **✅ Check project references** are correct
5. **✅ Validate test code** mock setups and dependencies
6. **✅ Document CI/CD dependency** in commit message
7. **✅ Monitor GitHub Actions** immediately after push

**If ANY step is skipped → High risk of CI/CD build failure**

---

## Detailed Checklist

### Step 1: Verify Contracts Before Use

**⚠️ CRITICAL**: This is the #1 cause of build failures without local SDK. Always read type definitions before using them - **NEVER guess** property/method/parameter names!

#### Before Using ANY Type (class, interface, enum)

**Do these 4 things**:

1. **Read the definition file** - Don't assume property/method names
2. **Check required vs optional** - Note nullability (`string?`) and `required` keyword
3. **Verify parameter types** - Especially for methods and constructors
4. **Use exact names** - Property names are case-sensitive

#### Quick Verification Process

```bash
# Step 1: Find the type definition
grep -r "class ErrorResponse" src/
grep -r "interface IUserService" src/
grep -r "enum DeploymentStatus" src/

# Step 2: Read the COMPLETE definition
cat src/HotSwap.Distributed.Api/Models/ApiModels.cs

# Step 3: Use EXACT names from the definition
# Don't guess, don't assume, don't approximate
```

#### Common Contract Mistakes

```csharp
// ❌ WRONG: Guessing property names without reading class
var response = new ErrorResponse { Message = "error" };
// Property might be called "Error", not "Message"!

// ✅ CORRECT: Read ErrorResponse class first
// File shows: public required string Error { get; set; }
var response = new ErrorResponse { Error = "error" };

// ❌ WRONG: Guessing method signature
await _service.GetUserAsync("username");
// Method might require CancellationToken parameter!

// ✅ CORRECT: Read IUserService interface first
// Interface shows: Task<User> GetUserAsync(string username, CancellationToken cancellationToken);
await _service.GetUserAsync("username", CancellationToken.None);

// ❌ WRONG: Guessing enum value
var status = DeploymentStatus.InProgress;
// Enum might use "Running", not "InProgress"!

// ✅ CORRECT: Read DeploymentStatus enum first
// Enum shows: Running, Completed, Failed, Cancelled
var status = DeploymentStatus.Running;
```

#### Key Rules for Contract Verification

- **Never guess** property/method/parameter names
- **Always verify** nullability (`string?` vs `string`, `required` keyword)
- **Check method signatures** match when setting up mocks
- **Verify enum values** exist before using them
- **Read constructors** to know required parameters
- **Check inheritance** to see inherited properties/methods

#### Example: Complete Contract Verification

```bash
# Scenario: Need to create a User object

# Step 1: Find User class
grep -r "class User" src/HotSwap.Distributed.Domain/

# Output: src/HotSwap.Distributed.Domain/Models/User.cs

# Step 2: Read complete file
cat src/HotSwap.Distributed.Domain/Models/User.cs

# Step 3: Note exact property names and types
# - Id: Guid (required)
# - Username: string (required)
# - Email: string (required)
# - FullName: string? (optional, nullable)
# - PasswordHash: string (required)
# - Roles: List<UserRole> (required)

# Step 4: Use exact names in code
var user = new User
{
    Id = Guid.NewGuid(),
    Username = "testuser",           // ✅ Matches property name
    Email = "test@example.com",       // ✅ Matches property name
    FullName = "Test User",           // ✅ Matches property name
    PasswordHash = "hash",            // ✅ Matches property name
    Roles = new List<UserRole> { UserRole.Deployer }  // ✅ Matches property type
};
```

---

### Step 2: Verify All Package References

Manually check that all project files have correct package references.

#### For NEW Test Files

**Rule**: If test code uses a type directly (not through project reference), the test project MUST have a package reference.

```bash
# Example: Test uses BCrypt.Net.BCrypt.HashPassword()
# Check if test project has BCrypt.Net-Next package

cat tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj | grep "BCrypt"

# If not found, add to .csproj:
# <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
```

#### Common Packages Needed in Tests

| Type Used in Test | Package Required | Version |
|-------------------|------------------|---------|
| `BCrypt.Net.BCrypt` | `BCrypt.Net-Next` | 4.0.3 |
| `ILogger<T>` | `Microsoft.Extensions.Logging.Abstractions` | 8.0.0 |
| `Mock<T>` | `Moq` | 4.20.70 |
| `Should()` | `FluentAssertions` | 6.12.0 |
| `[Fact]`, `[Theory]` | `xUnit` | 2.6.2 |

#### Package Reference Checklist

**Before committing test code, verify**:

- ✅ Check `tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj`
- ✅ If test uses `BCrypt.Net.BCrypt` → Ensure `BCrypt.Net-Next` package reference exists
- ✅ If test uses `ILogger<T>` → Ensure `Microsoft.Extensions.Logging.Abstractions` exists
- ✅ If test uses any NuGet package type → Ensure package reference exists
- ✅ If test uses project type → Ensure project reference exists (not package)

#### How to Add Package Reference

```xml
<!-- In tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj -->

<ItemGroup>
  <!-- Existing packages -->
  <PackageReference Include="xUnit" Version="2.6.2" />
  <PackageReference Include="Moq" Version="4.20.70" />
  <PackageReference Include="FluentAssertions" Version="6.12.0" />

  <!-- Add new package here -->
  <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
</ItemGroup>
```

---

### Step 3: Review All Code Changes

Carefully review every code change before committing.

#### Review Commands

```bash
# Show all unstaged changes
git diff

# Show all staged changes
git diff --cached

# Show changes in specific file
git diff path/to/file.cs
```

#### Code Review Checklist

**Check for these common issues**:

##### Using Statements
```csharp
// ❌ Missing using statement
public List<User> Users { get; set; }
// Needs: using System.Collections.Generic;

// ✅ Correct
using System.Collections.Generic;
public List<User> Users { get; set; }
```

##### Namespace Matches Folder
```csharp
// ❌ Wrong namespace (file is in Domain/Models/)
namespace HotSwap.Distributed.Api.Models;

// ✅ Correct (matches folder structure)
namespace HotSwap.Distributed.Domain.Models;
```

##### Syntax Errors
```csharp
// ❌ Missing semicolon
var user = new User { Username = "test" }

// ✅ Correct
var user = new User { Username = "test" };

// ❌ Mismatched braces
if (condition) {
    DoSomething();

// ✅ Correct
if (condition) {
    DoSomething();
}
```

##### Async/Await Usage
```csharp
// ❌ Async method returns void
public async void ProcessAsync() { }

// ✅ Async methods return Task
public async Task ProcessAsync() { }

// ❌ Not awaiting async call
public async Task CallServiceAsync()
{
    _service.DoWorkAsync();  // Missing await!
}

// ✅ Await async calls
public async Task CallServiceAsync()
{
    await _service.DoWorkAsync();
}
```

##### Interface Implementations
```csharp
// If you added a new interface:
// ✅ Verify implementation exists
// ✅ Verify implementation registered in Program.cs

// Example:
// Added: IUserService interface
// Check: UserService class implements IUserService
// Check: Program.cs has: builder.Services.AddScoped<IUserService, UserService>();
```

##### Mock Setups Match Signatures
```csharp
// ❌ Mock setup doesn't match actual signature
_mockRepo.Setup(x => x.GetUserAsync(It.IsAny<string>()))
    .ReturnsAsync(user);
// GetUserAsync actually has 2 parameters: (string username, CancellationToken token)

// ✅ Mock setup matches actual signature
_mockRepo.Setup(x => x.GetUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(user);
```

##### FluentAssertions Syntax
```csharp
// ❌ Wrong syntax
result.Should.NotBeNull();  // Missing parentheses

// ✅ Correct syntax
result.Should().NotBeNull();
result.Property.Should().Be("expected");
```

---

### Step 4: Verify Project References

Ensure all project references are correct in .csproj files.

#### Project Architecture

```
Domain (no dependencies)
  ↑
Infrastructure (references Domain)
  ↑
Orchestrator (references Domain, Infrastructure)
  ↑
Api (references Domain, Infrastructure, Orchestrator)

Tests (references ALL projects it tests)
```

#### For NEW Source Files

**Check project references match architecture**:

```xml
<!-- Domain project: NO project references (core layer) -->
<!-- src/HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj -->
<ItemGroup>
  <!-- NO ProjectReference elements -->
</ItemGroup>

<!-- Infrastructure project: References Domain only -->
<!-- src/HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj -->
<ItemGroup>
  <ProjectReference Include="../HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj" />
</ItemGroup>

<!-- Api project: References Domain, Infrastructure, Orchestrator -->
<!-- src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj -->
<ItemGroup>
  <ProjectReference Include="../HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj" />
  <ProjectReference Include="../HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj" />
  <ProjectReference Include="../HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj" />
</ItemGroup>
```

#### For NEW Test Files

**Test project should reference ALL projects whose types are used**:

```xml
<!-- tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj -->
<ItemGroup>
  <!-- Reference all projects being tested -->
  <ProjectReference Include="../../src/HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj" />
  <ProjectReference Include="../../src/HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj" />
  <ProjectReference Include="../../src/HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj" />
  <ProjectReference Include="../../src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj" />
</ItemGroup>
```

---

### Step 5: Check for Common Build Errors

Review code for patterns that commonly cause build failures.

#### Namespace Issues

```csharp
// Check: Namespace matches folder structure

// File location: src/HotSwap.Distributed.Domain/Models/User.cs
// ✅ Correct namespace
namespace HotSwap.Distributed.Domain.Models;

// ❌ Wrong namespace
namespace HotSwap.Distributed.Api.Models;  // Doesn't match folder!
```

#### Missing Using Statements

```csharp
// ❌ Missing using statements
public class UserService
{
    private readonly ILogger<UserService> _logger;  // Needs: using Microsoft.Extensions.Logging;
    private readonly List<User> _users;              // Needs: using System.Collections.Generic;

    public async Task ProcessAsync() { }            // Needs: using System.Threading.Tasks;
}

// ✅ All using statements present
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class UserService { ... }
```

#### Async/Await Issues

```csharp
// ❌ Async method doesn't return Task
public async void SaveAsync()
{
    await _repo.SaveChangesAsync();
}

// ✅ Async method returns Task
public async Task SaveAsync()
{
    await _repo.SaveChangesAsync();
}

// ❌ Using async in non-async method
public User GetUser()
{
    return await _repo.GetUserAsync();  // Can't use await without async!
}

// ✅ Make method async or use .Result (blocking)
public async Task<User> GetUserAsync()
{
    return await _repo.GetUserAsync();
}
```

---

### Step 6: Validate Test Code

Review test code for common issues.

#### Mock Setup Signature Mismatch

```csharp
// Read actual method signature FIRST
// IUserRepository:
// Task<User?> GetUserAsync(string username, CancellationToken cancellationToken);

// ❌ Mock setup missing CancellationToken parameter
_mockRepo.Setup(x => x.GetUserAsync(It.IsAny<string>()))
    .ReturnsAsync(user);

// ✅ Mock setup matches actual signature
_mockRepo.Setup(x => x.GetUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(user);
```

#### Test Package Dependencies

```csharp
// ❌ Test uses package type but package not referenced
[Fact]
public void HashPassword_Works()
{
    var hash = BCrypt.Net.BCrypt.HashPassword("test");
    // If BCrypt.Net-Next not in test project .csproj → Build fails!
}

// ✅ Package reference added to test project
// tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj:
// <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
```

---

### Step 7: Document CI/CD Dependency

Add a note to your commit message indicating reliance on CI/CD.

```bash
git commit -m "feat: add user authentication service

- Implement JWT token generation
- Add password hashing with BCrypt
- Add comprehensive unit tests

Note: Build and tests will run in GitHub Actions CI/CD pipeline.
Package references verified manually (no local .NET SDK available)."
```

---

### Step 8: Monitor CI/CD Build

**IMMEDIATELY after pushing**, monitor GitHub Actions.

```bash
# Push changes
git push -u origin claude/your-feature-sessionid

# IMMEDIATELY navigate to GitHub Actions
# https://github.com/scrawlsbenches/Claude-code-test/actions

# Check build status of your branch
# - Look for your commit message
# - Click on workflow run
# - Monitor build logs in real-time
```

#### If CI/CD Build Fails

**Follow these steps**:

1. **Read error message** in GitHub Actions logs carefully
2. **Identify root cause**:
   - Missing package reference?
   - Property name typo?
   - Wrong namespace?
   - Mock signature mismatch?
3. **Fix locally** using this checklist
4. **Commit fix**:
   ```bash
   git add .
   git commit -m "fix: add missing package reference for BCrypt in tests"
   ```
5. **Push fix**:
   ```bash
   git push -u origin claude/your-feature-sessionid
   ```
6. **Monitor build again**

---

### Step 9: Pre-Commit Validation Summary

**Before committing without .NET SDK, verify ALL of these**:

- ✅ **Contracts verified** - Read all type definitions before use (Step 1)
- ✅ **Package references correct** - Test project has packages for types used
- ✅ **Code reviewed** - No syntax errors, proper async/await, correct namespaces
- ✅ **Project references correct** - Architecture dependencies maintained
- ✅ **Test code validated** - Mock setups match signatures
- ✅ **CI/CD noted** - Commit message mentions CI/CD dependency
- ✅ **No secrets** - No hardcoded passwords, API keys, connection strings
- ✅ **No TODOs** - No `// TODO: Fix before commit` comments
- ✅ **Interfaces registered** - New interfaces have implementations in Program.cs

**Only commit if you answer YES to ALL checks above.**

**After pushing, monitor GitHub Actions IMMEDIATELY.**

---

## What NOT to Commit

**❌ NEVER commit if**:

- You didn't verify contracts (read type definitions)
- You added new files without checking package references
- Code contains `// TODO: Fix this before commit`
- Code contains hardcoded secrets or environment values
- You changed interfaces without updating implementations
- Test project missing package references for types used
- You didn't note CI/CD dependency in commit message
- You're not prepared to monitor GitHub Actions immediately

---

## Emergency Fixes

If CI/CD fails after push:

```bash
# 1. Pull latest (if others pushed)
git pull origin claude/your-branch

# 2. If .NET SDK becomes available, use it
dotnet clean
dotnet restore
dotnet build --no-incremental
dotnet test

# 3. Fix errors based on CI/CD logs

# 4. Commit fix
git add .
git commit -m "fix: resolve CI/CD build failure - [specific issue]"

# 5. Push
git push -u origin claude/your-branch

# 6. Monitor GitHub Actions again
```

---

## Summary: The Golden Rule

**Without local build/test verification, you MUST be 10x more careful.**

**Checklist shorthand** (memorize this):
1. **Read contracts** - Don't guess property names
2. **Check packages** - Test project has all package references
3. **Review code** - Syntax, namespaces, async/await correct
4. **Verify refs** - Project references match architecture
5. **Validate tests** - Mock setups match signatures
6. **Note CI/CD** - Commit message mentions reliance
7. **Monitor builds** - Watch GitHub Actions immediately

**If build fails → Fix → Push → Monitor again**

---

## See Also

- [Pre-Commit Checklist](../workflows/pre-commit-checklist.md) - Standard checklist with SDK
- [Detailed Setup](A-DETAILED-SETUP.md) - How to install .NET SDK
- [Troubleshooting](E-TROUBLESHOOTING.md) - Common issues and fixes

**Back to**: [Main CLAUDE.md](../CLAUDE.md#alternative-no-sdk-checklist)
