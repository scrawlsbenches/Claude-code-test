# Appendix E: Comprehensive Troubleshooting Guide

**Last Updated**: 2025-11-16
**Part of**: CLAUDE.md.PROPOSAL.v2 Implementation
**Related Documents**: [CLAUDE.md](../CLAUDE.md), [appendices/A-DETAILED-SETUP.md](A-DETAILED-SETUP.md)

---

## Table of Contents

1. [Overview](#overview)
2. [.NET SDK Issues](#net-sdk-issues)
3. [Build Failures](#build-failures)
4. [Package and Dependency Issues](#package-and-dependency-issues)
5. [Test Failures](#test-failures)
6. [Runtime Issues](#runtime-issues)
7. [Git and Version Control Issues](#git-and-version-control-issues)
8. [Docker and Container Issues](#docker-and-container-issues)
9. [IDE and Tooling Issues](#ide-and-tooling-issues)
10. [Performance Issues](#performance-issues)
11. [Security and Authentication Issues](#security-and-authentication-issues)
12. [API and HTTP Issues](#api-and-http-issues)
13. [Logging and Debugging](#logging-and-debugging)
14. [Common Error Messages](#common-error-messages)
15. [Emergency Procedures](#emergency-procedures)

---

## Overview

This guide provides comprehensive troubleshooting solutions for common issues encountered while developing and running the HotSwap.Distributed project.

### Troubleshooting Philosophy

**Steps for any problem:**

1. **Read the error message** - It usually contains the solution
2. **Check recent changes** - What did you change last?
3. **Reproduce the issue** - Can you make it happen again?
4. **Isolate the problem** - Is it specific to one component?
5. **Search documentation** - Has someone else solved this?
6. **Ask for help** - Don't waste hours on a known issue

### Quick Diagnosis Flowchart

```
Issue Detected
    ↓
Can't build? → See Build Failures section
Can't run tests? → See Test Failures section
Can't run app? → See Runtime Issues section
Can't push to git? → See Git Issues section
Performance slow? → See Performance Issues section
Unknown error? → See Common Error Messages section
```

---

## .NET SDK Issues

### Issue: .NET SDK Not Found

**Symptoms:**

```bash
$ dotnet --version
bash: dotnet: command not found
```

**Solution:**

```bash
# Step 1: Install .NET SDK 8.0
# See appendices/A-DETAILED-SETUP.md for platform-specific instructions

# Ubuntu 24.04:
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
chmod 1777 /tmp
apt-get update
apt-get install -y dotnet-sdk-8.0

# Step 2: Verify installation
dotnet --version
# Expected: 8.0.121 or later

# Step 3: Add to PATH if needed (Linux/macOS)
echo 'export PATH="$PATH:/usr/share/dotnet"' >> ~/.bashrc
source ~/.bashrc
```

### Issue: Wrong .NET SDK Version

**Symptoms:**

```bash
$ dotnet --version
6.0.xxx  # Or 7.0.xxx

# Build fails with:
Error: The current .NET SDK does not support targeting .NET 8.0.
```

**Solution:**

```bash
# Step 1: List installed SDKs
dotnet --list-sdks

# Step 2: If 8.0 is installed but not selected:
# Create global.json to specify version
cat > global.json <<EOF
{
  "sdk": {
    "version": "8.0.121",
    "rollForward": "latestFeature"
  }
}
EOF

# Step 3: If 8.0 is not installed:
# Install .NET 8.0 SDK (see section above)

# Step 4: Verify
dotnet --version
# Should show 8.0.xxx
```

### Issue: Multiple .NET SDK Versions Conflict

**Symptoms:**

```bash
# Builds work sometimes, fail other times
# Different behavior in different directories
```

**Solution:**

```bash
# Step 1: List all installed SDKs
dotnet --list-sdks
# Output:
#   6.0.xxx [path]
#   7.0.xxx [path]
#   8.0.121 [path]

# Step 2: Create global.json to pin version
cat > global.json <<EOF
{
  "sdk": {
    "version": "8.0.121",
    "rollForward": "latestMinor"
  }
}
EOF

# Step 3: Verify correct SDK selected
dotnet --version
# Should show 8.0.121

# Step 4: Rebuild
dotnet clean
dotnet restore
dotnet build
```

---

## Build Failures

### Issue: Build Fails with "Missing References"

**Symptoms:**

```bash
$ dotnet build
Error CS0246: The type or namespace name 'OpenTelemetry' could not be found
```

**Solution:**

```bash
# Step 1: Restore NuGet packages
dotnet restore

# Step 2: If restore fails, clear NuGet cache
dotnet nuget locals all --clear

# Step 3: Restore again
dotnet restore

# Step 4: Rebuild
dotnet build

# Step 5: If still failing, check project references
dotnet list reference
# Ensure all required projects are referenced
```

### Issue: Build Fails with "File in Use"

**Symptoms:**

```bash
$ dotnet build
Error: The process cannot access the file 'bin/Debug/net8.0/HotSwap.dll'
because it is being used by another process.
```

**Solution:**

```bash
# Step 1: Stop all running instances
# Find process using the DLL
lsof bin/Debug/net8.0/HotSwap.dll  # Linux/macOS
# Or
netstat -ano | findstr :5000  # Windows

# Step 2: Kill the process
kill -9 [PID]  # Linux/macOS
taskkill /PID [PID] /F  # Windows

# Step 3: Clean and rebuild
dotnet clean
dotnet build

# Step 4: Prevention - always stop app before building
# Use Ctrl+C to stop running app
```

### Issue: Build Succeeds but with Warnings

**Symptoms:**

```bash
$ dotnet build
Build succeeded.
    5 Warning(s)
    0 Error(s)
```

**Solution:**

```bash
# Step 1: View warnings in detail
dotnet build --verbosity detailed

# Step 2: Common warnings and fixes:

# Warning CS1998: Async method lacks 'await' operator
# Fix: Either add await or remove async keyword
// Before:
public async Task<int> GetCountAsync() { return 42; }
// After:
public Task<int> GetCountAsync() { return Task.FromResult(42); }

# Warning CS8618: Non-nullable property is uninitialized
# Fix: Initialize property or make nullable
public string Name { get; set; } = string.Empty;  // Initialize
public string? Name { get; set; }  // Make nullable

# Warning CS8625: Cannot convert null to non-nullable reference
# Fix: Check for null or use null-forgiving operator
if (value != null) { ... }  // Check for null
var result = value!;  // Null-forgiving (only if certain)

# Step 3: Treat warnings as errors (best practice)
# Add to .csproj:
<PropertyGroup>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>

# Step 4: Rebuild
dotnet clean
dotnet build
```

### Issue: Incremental Build Issues

**Symptoms:**

```bash
# Changes not reflected in build output
# Old code still running after changes
```

**Solution:**

```bash
# Step 1: Clean all build artifacts
dotnet clean

# Step 2: Remove bin/ and obj/ directories manually
rm -rf **/bin **/obj  # Linux/macOS
del /s /q bin obj  # Windows

# Step 3: Restore packages
dotnet restore

# Step 4: Build without incremental
dotnet build --no-incremental

# Step 5: For future builds, use:
dotnet clean && dotnet build --no-incremental
```

---

## Package and Dependency Issues

### Issue: Package Restore Fails

**Symptoms:**

```bash
$ dotnet restore
error NU1301: Unable to load the service index for source
https://api.nuget.org/v3/index.json
```

**Solution:**

```bash
# Step 1: Check internet connectivity
ping api.nuget.org

# Step 2: Clear NuGet cache
dotnet nuget locals all --clear

# Step 3: Try restore again
dotnet restore

# Step 4: If behind proxy, configure NuGet
# Create/edit ~/.nuget/NuGet/NuGet.Config
cat > ~/.nuget/NuGet/NuGet.Config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="http_proxy" value="http://proxy:port" />
  </config>
</configuration>
EOF

# Step 5: Retry restore
dotnet restore

# Step 6: Alternative - use offline packages
dotnet restore --source /path/to/local/packages
```

### Issue: Package Version Conflict

**Symptoms:**

```bash
$ dotnet build
error NU1107: Version conflict detected for 'Microsoft.Extensions.Logging'.
```

**Solution:**

```bash
# Step 1: List all packages and versions
dotnet list package

# Step 2: Find conflicting package
dotnet list package --include-transitive | grep "Microsoft.Extensions.Logging"

# Step 3: Resolve conflict by pinning version
# Add to .csproj:
<ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
</ItemGroup>

# Step 4: Restore and rebuild
dotnet restore
dotnet build

# Step 5: Alternative - use Directory.Build.props for central management
# Create Directory.Build.props in solution root:
cat > Directory.Build.props <<EOF
<Project>
  <ItemGroup>
    <PackageReference Update="Microsoft.Extensions.Logging" Version="8.0.0" />
  </ItemGroup>
</Project>
EOF
```

### Issue: Package Not Found in .csproj

**Symptoms:**

```bash
# Code uses BCrypt but package not referenced in test project
$ dotnet test
Error CS0246: The type or namespace name 'BCrypt' could not be found
```

**Solution:**

```bash
# Step 1: Identify missing package
# Look at using statement in code:
using BCrypt.Net;  # Package is BCrypt.Net-Next

# Step 2: Add package to correct project
cd tests/HotSwap.Distributed.Tests/
dotnet add package BCrypt.Net-Next --version 4.0.3

# Step 3: Verify package added
dotnet list package | grep BCrypt

# Step 4: Restore and rebuild
dotnet restore
dotnet build

# Step 5: Run tests
dotnet test
```

---

## Test Failures

### Issue: All Tests Fail with "Collection Name Conflict"

**Symptoms:**

```bash
$ dotnet test
Error: The following test classes have the same test collection name
```

**Solution:**

```bash
# Problem: Multiple test classes using same collection name
[Collection("IntegrationTests")]
public class Tests1 { }

[Collection("IntegrationTests")]
public class Tests2 { }

# Solution: Use unique collection names OR share collection definition

# Option 1: Unique names
[Collection("IntegrationTests1")]
public class Tests1 { }

[Collection("IntegrationTests2")]
public class Tests2 { }

# Option 2: Shared collection (preferred for integration tests)
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<TestFixture>
{
}

[Collection("IntegrationTests")]
public class Tests1
{
    public Tests1(TestFixture fixture) { }
}

[Collection("IntegrationTests")]
public class Tests2
{
    public Tests2(TestFixture fixture) { }
}
```

### Issue: Tests Fail with "Null Reference Exception"

**Symptoms:**

```bash
$ dotnet test
System.NullReferenceException: Object reference not set to an instance of an object.
```

**Solution:**

```csharp
// Problem: Mock not set up correctly
var mockRepo = new Mock<IUserRepository>();
var service = new UserService(mockRepo.Object);

var result = await service.GetUserAsync("user123");  // NullReferenceException!

// Solution: Set up mock return value
var mockRepo = new Mock<IUserRepository>();
mockRepo.Setup(x => x.GetByIdAsync("user123", It.IsAny<CancellationToken>()))
    .ReturnsAsync(new User { UserId = "user123" });  // ← FIX: Return value

var service = new UserService(mockRepo.Object);
var result = await service.GetUserAsync("user123");  // ✅ Works now
```

### Issue: Async Tests Hang/Timeout

**Symptoms:**

```bash
$ dotnet test
Test run hangs indefinitely or times out after 30 seconds
```

**Solution:**

```csharp
// Problem: Not awaiting async call
[Fact]
public async Task MyTest()
{
    var task = service.MyMethodAsync();  // NOT AWAITED!
    task.Result.Should().Be(expected);  // DEADLOCK!
}

// Solution 1: Await the task
[Fact]
public async Task MyTest()
{
    var result = await service.MyMethodAsync();  // ✅ AWAITED
    result.Should().Be(expected);
}

// Solution 2: For methods that don't return values
[Fact]
public async Task MyTest()
{
    await service.MyMethodAsync();  // ✅ AWAITED
}

// Problem: CancellationToken not set up
mockService.Setup(x => x.MethodAsync(It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);  // Hangs if method uses CancellationToken!

// Solution: Use ReturnsAsync for async methods
mockService.Setup(x => x.MethodAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync();  // ✅ Proper async setup
```

### Issue: Tests Pass Locally but Fail in CI/CD

**Symptoms:**

```bash
# Local:
$ dotnet test
Passed!  - Failed: 0, Passed: 80

# GitHub Actions:
Error: Test failed - Expected 80 but got 79
```

**Solution:**

```bash
# Common causes:

# 1. Environment-specific configuration
# Problem: Tests rely on local environment variables
# Solution: Use test-specific configuration
public class TestBase
{
    protected IConfiguration GetTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Setting1"] = "TestValue1",
                ["Setting2"] = "TestValue2"
            })
            .Build();
    }
}

# 2. Timing/race conditions
# Problem: Tests rely on specific timing
Thread.Sleep(1000);  // ❌ Fragile
result.Should().Be(expected);

# Solution: Wait for condition with timeout
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await Task.Run(async () =>
{
    while (condition != expected && !cts.Token.IsCancellationRequested)
    {
        await Task.Delay(100, cts.Token);
    }
}, cts.Token);
result.Should().Be(expected);

# 3. File path differences (Windows vs Linux)
# Problem: Hard-coded paths
var path = "C:\\temp\\file.txt";  // ❌ Windows-only

# Solution: Use Path.Combine
var path = Path.Combine(Path.GetTempPath(), "file.txt");  // ✅ Cross-platform
```

---

## Runtime Issues

### Issue: Application Fails to Start

**Symptoms:**

```bash
$ dotnet run --project src/HotSwap.Distributed.Api/
Unhandled exception. System.InvalidOperationException:
Unable to resolve service for type 'IDeploymentOrchestrator'
```

**Solution:**

```csharp
// Problem: Service not registered in DI container

// Check Program.cs:
var builder = WebApplication.CreateBuilder(args);

// Missing service registration:
// builder.Services.AddScoped<IDeploymentOrchestrator, DeploymentOrchestrator>();

// Solution: Add service registration
builder.Services.AddScoped<IDeploymentOrchestrator, DeploymentOrchestrator>();
builder.Services.AddScoped<IDeploymentEngine, DeploymentEngine>();
builder.Services.AddScoped<IModuleRepository, InMemoryModuleRepository>();
// ... all other services

var app = builder.Build();
```

### Issue: Port Already in Use

**Symptoms:**

```bash
$ dotnet run
Unhandled exception. System.IO.IOException:
Failed to bind to address http://127.0.0.1:5000: address already in use.
```

**Solution:**

```bash
# Step 1: Find process using port 5000
lsof -i :5000  # Linux/macOS
netstat -ano | findstr :5000  # Windows

# Step 2: Kill the process
kill -9 [PID]  # Linux/macOS
taskkill /PID [PID] /F  # Windows

# Step 3: Alternative - run on different port
dotnet run --urls "http://localhost:5001"

# Step 4: Or update appsettings.json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5001"
      }
    }
  }
}
```

### Issue: API Returns 500 Internal Server Error

**Symptoms:**

```bash
$ curl http://localhost:5000/api/deployments
500 Internal Server Error
```

**Solution:**

```bash
# Step 1: Check logs
dotnet run --project src/HotSwap.Distributed.Api/
# Look for error stack trace in console output

# Step 2: Enable detailed error messages (development only!)
# In appsettings.Development.json:
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Debug"
    }
  }
}

# Step 3: Check Program.cs has exception handler
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();  // Shows detailed errors
}
else
{
    app.UseExceptionHandler("/error");
}

# Step 4: Add logging to controller
private readonly ILogger<DeploymentsController> _logger;

[HttpGet]
public async Task<IActionResult> Get()
{
    try
    {
        _logger.LogInformation("GetDeployments called");
        var result = await _orchestrator.ListDeploymentsAsync();
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in GetDeployments");
        throw;
    }
}

# Step 5: Check for missing dependencies in controller constructor
// Ensure all dependencies are registered in Program.cs
```

---

## Git and Version Control Issues

### Issue: Cannot Push - 403 Forbidden

**Symptoms:**

```bash
$ git push -u origin claude/my-branch-abc123
error: RPC failed; HTTP 403 forbidden
```

**Solution:**

```bash
# Cause 1: Branch doesn't match required pattern (claude/name-sessionid)
# Solution: Rename branch
git branch -m claude/correct-name-01ABC123
git push -u origin claude/correct-name-01ABC123

# Cause 2: Session ID doesn't match
# Solution: Use correct session ID from environment
# Branch MUST end with current session ID

# Cause 3: Network/authentication issue
# Solution: Check credentials
git config credential.helper store
git push -u origin claude/branch-name

# Cause 4: Transient network error
# Solution: Retry with exponential backoff
attempt=1
max_attempts=4
delay=2

while [ $attempt -le $max_attempts ]; do
    echo "Attempt $attempt of $max_attempts..."
    git push -u origin claude/branch-name && break
    echo "Push failed, waiting ${delay}s..."
    sleep $delay
    delay=$((delay * 2))
    attempt=$((attempt + 1))
done
```

### Issue: Merge Conflict

**Symptoms:**

```bash
$ git pull origin main
Auto-merging src/Services/MyService.cs
CONFLICT (content): Merge conflict in src/Services/MyService.cs
```

**Solution:**

```bash
# Step 1: Check conflicted files
git status
# Unmerged paths:
#   both modified:   src/Services/MyService.cs

# Step 2: Open file and find conflict markers
cat src/Services/MyService.cs
# <<<<<<< HEAD
# Your changes
# =======
# Their changes
# >>>>>>> main

# Step 3: Resolve conflict manually
# Edit file to combine changes or choose one version
# Remove conflict markers (<<<<<<, =======, >>>>>>>)

# Step 4: Mark as resolved
git add src/Services/MyService.cs

# Step 5: Complete merge
git commit -m "Merge main into claude/my-branch - resolved conflicts"

# Step 6: Verify build and tests
dotnet build
dotnet test

# Step 7: Push
git push -u origin claude/my-branch
```

### Issue: Accidentally Committed Large Files

**Symptoms:**

```bash
$ git push
error: file too large (>100 MB)
```

**Solution:**

```bash
# Step 1: Remove file from last commit (if not pushed yet)
git rm --cached path/to/large/file
git commit --amend -m "Remove large file"

# Step 2: Add to .gitignore
echo "path/to/large/file" >> .gitignore
echo "*.iso" >> .gitignore  # Ignore by extension
echo "large-files/" >> .gitignore  # Ignore directory

# Step 3: If already pushed, rewrite history (DANGEROUS!)
# ONLY if branch is not shared with others
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch path/to/large/file" \
  --prune-empty --tag-name-filter cat -- --all

# Step 4: Force push (ONLY on your feature branch!)
git push --force-with-lease origin claude/branch-name
```

---

## Docker and Container Issues

### Issue: Docker Build Fails

**Symptoms:**

```bash
$ docker-compose up --build
Error: failed to solve with frontend dockerfile.v0
```

**Solution:**

```bash
# Step 1: Check Docker daemon is running
docker ps
# If error "Cannot connect to Docker daemon", start Docker

# Step 2: Check Dockerfile syntax
cat Dockerfile
# Common issues:
# - Missing FROM statement
# - Invalid COPY source paths
# - Missing required files

# Step 3: Build with more verbose output
docker-compose build --no-cache --progress=plain

# Step 4: Verify .dockerignore doesn't exclude required files
cat .dockerignore
# Should NOT include:
# - *.csproj files
# - *.sln file
# - src/ directory

# Step 5: Test Docker build standalone
docker build -t hotswap-test -f Dockerfile .

# Step 6: If still failing, build individual layers
# Inspect Dockerfile and run commands step by step
```

### Issue: Container Exits Immediately

**Symptoms:**

```bash
$ docker-compose up
orchestrator-api exited with code 139
```

**Solution:**

```bash
# Step 1: Check container logs
docker-compose logs orchestrator-api

# Step 2: Run container interactively
docker-compose run --rm orchestrator-api /bin/bash
# Inside container:
dotnet --version  # Verify .NET runtime exists
ls -la  # Verify app files present
dotnet HotSwap.Distributed.Api.dll  # Try running manually

# Step 3: Check Dockerfile ENTRYPOINT/CMD
# Dockerfile should have:
ENTRYPOINT ["dotnet", "HotSwap.Distributed.Api.dll"]

# Step 4: Check for missing dependencies
# Ensure Dockerfile includes runtime dependencies:
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# Not just:
FROM mcr.microsoft.com/dotnet/sdk:8.0

# Step 5: Check environment variables
docker-compose run --rm orchestrator-api env
# Verify all required env vars are set
```

---

## IDE and Tooling Issues

### Issue: IntelliSense Not Working

**Symptoms:**

```
- No auto-completion
- Red squiggly lines on valid code
- "Cannot resolve symbol" errors
```

**Solution:**

```bash
# Visual Studio Code:
# Step 1: Reload window
Ctrl+Shift+P → "Developer: Reload Window"

# Step 2: Rebuild OmniSharp
Ctrl+Shift+P → "OmniSharp: Restart OmniSharp"

# Step 3: Restore packages
dotnet restore

# Step 4: Clean and rebuild
dotnet clean
dotnet build

# Step 5: Delete .vs/ and .vscode/ folders
rm -rf .vs .vscode/obj .vscode/bin

# Visual Studio:
# Step 1: Clean solution
Build → Clean Solution

# Step 2: Delete hidden folders
rm -rf .vs **/bin **/obj

# Step 3: Restore and rebuild
dotnet restore
dotnet build
```

---

## Performance Issues

### Issue: Slow Build Times

**Symptoms:**

```bash
$ dotnet build
# Takes 2-3 minutes (should be ~18 seconds)
```

**Solution:**

```bash
# Cause 1: Too many projects in solution
# Solution: Build only changed projects
dotnet build --no-dependencies src/HotSwap.Distributed.Api/

# Cause 2: Incremental build not working
# Solution: Ensure bin/obj folders writable
chmod -R 755 **/bin **/obj

# Cause 3: Antivirus scanning build folders
# Solution: Exclude bin/ and obj/ from antivirus

# Cause 4: Network drive or slow disk
# Solution: Move project to local SSD

# Measurement:
time dotnet build --no-incremental
# Should complete in <30 seconds for clean build
```

### Issue: Tests Run Slowly

**Symptoms:**

```bash
$ dotnet test
# Duration: 60 seconds (should be ~10 seconds for 80 tests)
```

**Solution:**

```bash
# Cause 1: Tests not parallelized
# Solution: Ensure xunit.runner.json allows parallelization
cat > xunit.runner.json <<EOF
{
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4
}
EOF

# Cause 2: Tests use Task.Delay instead of mocks
# Problem:
await Task.Delay(5000);  // ❌ Real 5-second delay in test!

# Solution: Mock time-dependent behavior
var mockTimeProvider = new Mock<ITimeProvider>();
mockTimeProvider.Setup(x => x.Delay(It.IsAny<TimeSpan>()))
    .Returns(Task.CompletedTask);  // ✅ Instant

# Cause 3: Integration tests hitting real resources
# Solution: Use in-memory implementations for tests
```

---

## Security and Authentication Issues

### Issue: JWT Token Validation Fails

**Symptoms:**

```bash
$ curl -H "Authorization: Bearer <token>" http://localhost:5000/api/deployments
401 Unauthorized
```

**Solution:**

```csharp
// Problem: JWT configuration mismatch

// In Program.cs, ensure configuration matches token generation:
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],  // Must match token issuer
            ValidAudience = configuration["Jwt:Audience"],  // Must match token audience
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"]))  // Same key!
        };
    });

// Check appsettings.json:
{
  "Jwt": {
    "SecretKey": "your-secret-key-min-32-characters-long",
    "Issuer": "HotSwapApi",
    "Audience": "HotSwapClient",
    "ExpirationMinutes": 60
  }
}

// Verify token is being sent correctly:
curl -H "Authorization: Bearer eyJhbGc..." http://localhost:5000/api/deployments
// Note: "Bearer " prefix is required!
```

---

## API and HTTP Issues

### Issue: CORS Errors

**Symptoms:**

```
Access to XMLHttpRequest at 'http://localhost:5000/api/deployments'
from origin 'http://localhost:3000' has been blocked by CORS policy
```

**Solution:**

```csharp
// In Program.cs:
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:8080")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// IMPORTANT: UseCors must be BEFORE UseAuthorization
app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

---

## Logging and Debugging

### Issue: Logs Not Appearing

**Symptoms:**

```csharp
_logger.LogInformation("This message doesn't appear");
```

**Solution:**

```json
// Check appsettings.Development.json:
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",  // Ensure level is appropriate
      "Microsoft.AspNetCore": "Warning"
    }
  }
}

// Verify logger is injected correctly:
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}

// Ensure logging is configured in Program.cs:
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
```

---

## Common Error Messages

### Error: "The type or namespace name 'X' could not be found"

**Cause**: Missing using statement or missing package reference

**Solution**:

```csharp
// Add using statement:
using HotSwap.Distributed.Domain.Models;

// Or add package reference:
dotnet add package [PackageName]
```

### Error: "Object reference not set to an instance of an object"

**Cause**: Null reference

**Solution**:

```csharp
// Check for null before use:
if (user != null)
{
    var name = user.Name;
}

// Or use null-conditional operator:
var name = user?.Name;

// Or use null-coalescing operator:
var name = user?.Name ?? "Unknown";
```

### Error: "A task was canceled"

**Cause**: CancellationToken was canceled or operation timed out

**Solution**:

```csharp
try
{
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await service.MethodAsync(cts.Token);
}
catch (OperationCanceledException)
{
    _logger.LogWarning("Operation was canceled or timed out");
    // Handle gracefully
}
```

---

## Emergency Procedures

### Nuclear Option: Complete Reset

**When**: Nothing else works, need to start fresh

```bash
# ⚠️ WARNING: This deletes ALL local changes!

# Step 1: Commit or stash current work
git status
git stash save "Emergency backup $(date +%Y-%m-%d-%H%M%S)"

# Step 2: Clean ALL build artifacts
git clean -fdx  # Removes all untracked files!

# Step 3: Reset to last known good commit
git reset --hard origin/main

# Step 4: Clear all caches
dotnet nuget locals all --clear

# Step 5: Fresh restore and build
dotnet restore
dotnet clean
dotnet build --no-incremental

# Step 6: Verify tests pass
dotnet test

# Step 7: If successful, recover stashed work
git stash list
git stash pop  # If you want to restore your changes
```

### Get Help

**If you're still stuck:**

1. Check GitHub Issues: https://github.com/scrawlsbenches/Claude-code-test/issues
2. Review recent commits for similar fixes
3. Ask in project discussions
4. Create a new issue with:
   - Error message (full stack trace)
   - Steps to reproduce
   - Environment details (OS, .NET version)
   - What you've tried

---

**Last Updated**: 2025-11-16
**Maintained by**: AI Assistants and Project Contributors
**Questions?**: Create an issue in the repository

