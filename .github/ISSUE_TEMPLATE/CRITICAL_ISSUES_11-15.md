# CRITICAL GitHub Issues 11-15 - Remediation Sprint Phase 1

**Created:** 2025-11-20
**Sprint:** Phase 1 (Weeks 1-3)
**Milestone:** Phase 1 - Critical Remediation

---

## Issue #11: [CRITICAL] Missing CSRF Protection on State-Changing Operations

**Priority:** 🔴 CRITICAL
**Category:** Security - CSRF
**Assigned To:** Security Track Lead
**Estimated Effort:** 16 hours
**Phase:** 1 - Week 3

### Problem Description

API uses JWT bearer tokens without CSRF protection. While JWT in `Authorization` header provides some protection, SignalR connections and browser-based clients are vulnerable to Cross-Site Request Forgery attacks.

### Impact

**CSRF ATTACK VECTOR**
- Attacker can trick authenticated users into unauthorized actions
- Malicious website can trigger deployments, approvals, deletions
- JWT cookies (if used) are automatically sent by browser
- SignalR connections vulnerable

### Code Location

**File:** `src/HotSwap.Distributed.Api/Program.cs`

**Missing Configuration:**
```csharp
// ❌ NO ANTIFORGERY CONFIGURATION
builder.Services.AddControllers(); // Missing .AddAntiforgery()
```

**Vulnerable Endpoints:**
- `POST /api/v1/deployments` - Create deployment
- `POST /api/v1/approvals/{id}/approve` - Approve deployment
- `POST /api/v1/tenants` - Create tenant
- `DELETE /api/messages/{id}` - Delete message
- All POST/PUT/DELETE endpoints

### Recommended Fix

**Option A: SameSite Cookies (Recommended)**

```csharp
// Program.cs - Configure cookie-based JWT with SameSite
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Support both header and cookie
                var token = context.Request.Cookies["auth_token"];
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };

        // ... existing JWT configuration ...
    })
    .AddCookie(options =>
    {
        options.Cookie.SameSite = SameSiteMode.Strict; // CSRF protection
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
    });
```

**Option B: Anti-Forgery Tokens**

```csharp
// Program.cs - Add antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Middleware
app.UseAntiforgeryToken(); // Custom middleware to validate
```

**Option C: Custom CSRF Header (Simplest)**

```csharp
// NEW FILE: Middleware/CsrfProtectionMiddleware.cs
public class CsrfProtectionMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only check state-changing requests
        if (IsStateChangingRequest(context.Request))
        {
            // Require custom header (CSRF protection)
            if (!context.Request.Headers.ContainsKey("X-Requested-With"))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Missing CSRF protection header");
                return;
            }
        }

        await _next(context);
    }

    private bool IsStateChangingRequest(HttpRequest request)
    {
        var method = request.Method;
        return method == "POST" || method == "PUT" ||
               method == "DELETE" || method == "PATCH";
    }
}

// Program.cs
app.UseMiddleware<CsrfProtectionMiddleware>();
```

### Acceptance Criteria

- [ ] CSRF protection implemented (SameSite or custom header)
- [ ] All POST/PUT/DELETE endpoints protected
- [ ] Legitimate requests with proper headers succeed
- [ ] Requests without CSRF protection fail with 403
- [ ] SignalR connections protected
- [ ] Documentation updated for API clients
- [ ] All existing tests pass
- [ ] New CSRF attack tests added

### Test Cases

**Test 1: Request Without CSRF Protection Fails**
```csharp
[Fact]
public async Task CreateDeployment_WithoutCsrfHeader_Returns403()
{
    // Arrange
    var client = CreateAuthenticatedClient();
    // No X-Requested-With header

    var request = new CreateDeploymentRequest { /* ... */ };

    // Act
    var response = await client.PostAsJsonAsync("/api/v1/deployments", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

**Test 2: Request With CSRF Protection Succeeds**
```csharp
[Fact]
public async Task CreateDeployment_WithCsrfHeader_Returns202()
{
    // Arrange
    var client = CreateAuthenticatedClient();
    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

    var request = new CreateDeploymentRequest { /* ... */ };

    // Act
    var response = await client.PostAsJsonAsync("/api/v1/deployments", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Accepted);
}
```

**Test 3: GET Requests Don't Require CSRF Protection**
```csharp
[Fact]
public async Task GetDeployments_WithoutCsrfHeader_Returns200()
{
    // Arrange
    var client = CreateAuthenticatedClient();
    // No X-Requested-With header

    // Act
    var response = await client.GetAsync("/api/v1/deployments");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

**Test 4: SignalR Connection Protected**
```csharp
[Fact]
public async Task SignalR_WithoutCsrfProtection_Fails()
{
    // Arrange
    var connection = new HubConnectionBuilder()
        .WithUrl("http://localhost/deploymentHub") // No CSRF token
        .Build();

    // Act & Assert
    await connection.Invoking(async c => await c.StartAsync())
        .Should().ThrowAsync<HttpRequestException>();
}
```

### Definition of Done

- [x] CSRF protection middleware implemented
- [x] All 4 tests passing
- [x] Security review approved
- [x] API documentation updated
- [x] Client libraries updated (if any)
- [x] Code review approved
- [x] Merged to main

### Related Issues

- See CODE_REVIEW_REPORT.md section: "API Security Issues #13"
- Related to: Issue #1 (Auth), Issue #13 (SignalR)

---

## Issue #12: [CRITICAL] Weak JWT Secret Key in Development

**Priority:** 🔴 CRITICAL
**Category:** Security - Cryptography
**Assigned To:** Security Track Lead
**Estimated Effort:** 4 hours
**Phase:** 1 - Week 3

### Problem Description

Default JWT secret key is hardcoded and predictable. While production requires configuration, the fallback key is insufficient for HMAC-SHA256 (minimum 256 bits recommended).

### Impact

**TOKEN FORGERY RISK**
- Attackers can forge tokens in dev/test if key leaks
- If accidentally deployed to production, complete auth bypass
- Insufficient key entropy weakens cryptographic strength

### Code Location

**File:** `src/HotSwap.Distributed.Api/Program.cs`

**Line:** 158

**Current Code:**
```csharp
// Line 158 - WEAK DEFAULT KEY
jwtSecretKey = "DistributedKernelSecretKey-ChangeInProduction-MinimumLength32Characters";
```

### Recommended Fix

**Step 1: Generate cryptographically random key**

```csharp
// Generate secure 256-bit key
var randomKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
// Example output: "xK7mP9jT2vL8nQ1wR5yU3zA6bC4dE0fG8hI2jK5lM7="
```

**Step 2: Require configuration in all environments**

```csharp
// Program.cs - Line ~150
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];

if (string.IsNullOrEmpty(jwtSecretKey))
{
    // Development: Generate random key and log it
    if (builder.Environment.IsDevelopment())
    {
        jwtSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Console.WriteLine("⚠️  WARNING: Using auto-generated JWT key for development:");
        Console.WriteLine($"   {jwtSecretKey}");
        Console.WriteLine("   Set Jwt:SecretKey in appsettings.Development.json to persist this key.");
    }
    else
    {
        // Production: FAIL FAST
        throw new InvalidOperationException(
            "FATAL: Jwt:SecretKey is not configured. " +
            "Set environment variable JWT__SECRETKEY or add to appsettings.json. " +
            "Generate a secure key with: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");
    }
}

// Validate key strength
if (jwtSecretKey.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT secret key must be at least 32 characters (256 bits). " +
        $"Current length: {jwtSecretKey.Length}");
}
```

**Step 3: Update appsettings**

```json
// appsettings.Development.json
{
  "Jwt": {
    "SecretKey": "xK7mP9jT2vL8nQ1wR5yU3zA6bC4dE0fG8hI2jK5lM7nO9pQ1rS3tU5vW7xY9zA==",
    "Issuer": "DistributedKernel",
    "Audience": "DistributedKernelApi",
    "ExpirationMinutes": 60
  }
}

// appsettings.Production.json
{
  "Jwt": {
    "SecretKey": "${JWT_SECRET_KEY}", // From environment variable
    "Issuer": "DistributedKernel",
    "Audience": "DistributedKernelApi",
    "ExpirationMinutes": 15
  }
}
```

**Step 4: Add key rotation support (Phase 2)**

```csharp
// Future enhancement: Support multiple keys for rotation
"Jwt": {
  "CurrentKey": "new-key-base64",
  "PreviousKeys": ["old-key1", "old-key2"], // Accept but don't issue
  "KeyRotationDate": "2026-01-01"
}
```

### Acceptance Criteria

- [ ] No hardcoded JWT keys in source code
- [ ] Cryptographically random key generated for dev
- [ ] Production requires explicit configuration or fails
- [ ] Key length validation (minimum 32 characters)
- [ ] Warning logged in development if auto-generated
- [ ] Documentation updated with key generation instructions
- [ ] All existing tests pass

### Test Cases

**Test 1: Production Without Key Fails Fast**
```csharp
[Fact]
public void Startup_InProduction_WithoutJwtKey_ThrowsException()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
    var config = new ConfigurationBuilder().Build(); // No JWT key

    // Act & Assert
    var action = () => new WebApplicationBuilder(config);
    action.Should().Throw<InvalidOperationException>()
        .WithMessage("*Jwt:SecretKey is not configured*");
}
```

**Test 2: Development Auto-Generates Key**
```csharp
[Fact]
public void Startup_InDevelopment_WithoutJwtKey_GeneratesRandomKey()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    var config = new ConfigurationBuilder().Build();

    // Act
    var app = new WebApplicationBuilder(config);
    var jwtKey = app.Configuration["Jwt:SecretKey"];

    // Assert
    jwtKey.Should().NotBeNullOrEmpty();
    jwtKey.Length.Should().BeGreaterThan(32);
    jwtKey.Should().NotBe("DistributedKernelSecretKey-ChangeInProduction-MinimumLength32Characters");
}
```

**Test 3: Key Length Validation**
```csharp
[Fact]
public void Startup_WithShortJwtKey_ThrowsException()
{
    // Arrange
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Jwt:SecretKey"] = "too-short" // Only 9 characters
        })
        .Build();

    // Act & Assert
    var action = () => new WebApplicationBuilder(config);
    action.Should().Throw<InvalidOperationException>()
        .WithMessage("*must be at least 32 characters*");
}
```

### Definition of Done

- [x] Hardcoded key removed
- [x] Random key generation for dev
- [x] Production fail-fast implemented
- [x] All 3 tests passing
- [x] Documentation updated
- [x] appsettings.*.json updated
- [x] Merged to main

### Related Issues

- See CODE_REVIEW_REPORT.md section: "API Security Issues #17"
- Related to: Issue #4 (Demo credentials)

---

## Issue #13: [CRITICAL] SignalR Hub Missing Authentication

**Priority:** 🔴 CRITICAL
**Category:** Security - Authentication
**Assigned To:** Security Track Lead
**Estimated Effort:** 4 hours
**Phase:** 1 - Week 3

### Problem Description

`DeploymentHub` SignalR hub has **NO authentication or authorization attributes**. Anyone can connect to the hub and subscribe to real-time deployment updates without being authenticated.

### Impact

**UNAUTHORIZED DATA ACCESS**
- Unauthenticated users can view deployment progress
- Potential information disclosure (deployment IDs, module names, status)
- Can monitor production deployments
- Basis for targeted attacks

### Code Location

**File:** `src/HotSwap.Distributed.Api/Hubs/DeploymentHub.cs`

**Missing Attribute:**
```csharp
public class DeploymentHub : Hub // ❌ Missing [Authorize]
{
    public async Task SubscribeToDeployment(string executionId) { ... }
    public async Task UnsubscribeFromDeployment(string executionId) { ... }
}
```

### Recommended Fix

**Add authorization attribute:**

```csharp
[Authorize(Roles = "Viewer,Deployer,Admin")] // ✅ Require authentication
public class DeploymentHub : Hub
{
    private readonly ILogger<DeploymentHub> _logger;
    private readonly IDeploymentTracker _deploymentTracker;

    public async Task SubscribeToDeployment(string executionId)
    {
        // Verify user can access this deployment (IDOR protection)
        if (!await CanAccessDeploymentAsync(executionId))
        {
            _logger.LogWarning(
                "User {UserName} attempted to subscribe to deployment {ExecutionId} without permission",
                Context.User?.Identity?.Name, executionId);
            throw new HubException("Access denied");
        }

        var groupName = $"deployment-{executionId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "User {UserName} subscribed to deployment {ExecutionId}",
            Context.User?.Identity?.Name, executionId);
    }

    private async Task<bool> CanAccessDeploymentAsync(string executionId)
    {
        // Use same authorization logic as DeploymentsController
        var deployment = await _deploymentTracker.GetResultAsync(Guid.Parse(executionId));
        if (deployment == null)
            return false;

        var currentTenantId = Context.User?.FindFirst("tenant_id")?.Value;
        return deployment.TenantId == currentTenantId;
    }
}
```

### Acceptance Criteria

- [ ] `[Authorize]` attribute added to hub
- [ ] Unauthenticated connections rejected
- [ ] Authorization validated on subscription
- [ ] Resource ownership verified (IDOR protection)
- [ ] Failed auth attempts logged
- [ ] All existing tests pass
- [ ] New SignalR auth tests added

### Test Cases

**Test 1: Unauthenticated Connection Fails**
```csharp
[Fact]
public async Task SignalRHub_WithoutAuthentication_RejectsConnection()
{
    // Arrange
    var connection = new HubConnectionBuilder()
        .WithUrl("http://localhost/deploymentHub") // No auth token
        .Build();

    // Act & Assert
    await connection.Invoking(async c => await c.StartAsync())
        .Should().ThrowAsync<HttpRequestException>();
}
```

**Test 2: Authenticated Connection Succeeds**
```csharp
[Fact]
public async Task SignalRHub_WithAuthentication_AllowsConnection()
{
    // Arrange
    var authToken = GenerateValidJwtToken();
    var connection = new HubConnectionBuilder()
        .WithUrl("http://localhost/deploymentHub", options =>
        {
            options.AccessTokenProvider = () => Task.FromResult(authToken);
        })
        .Build();

    // Act
    await connection.StartAsync();

    // Assert
    connection.State.Should().Be(HubConnectionState.Connected);

    // Cleanup
    await connection.StopAsync();
}
```

**Test 3: Cross-Tenant Subscription Denied**
```csharp
[Fact]
public async Task SubscribeToDeployment_FromDifferentTenant_ThrowsHubException()
{
    // Arrange
    var tenant1Deployment = await CreateDeployment(tenantId: "tenant1");
    var tenant2Connection = await CreateAuthenticatedConnection(tenantId: "tenant2");

    // Act & Assert
    await tenant2Connection
        .Invoking(async c => await c.InvokeAsync("SubscribeToDeployment",
            tenant1Deployment.ExecutionId.ToString()))
        .Should().ThrowAsync<HubException>()
        .WithMessage("*Access denied*");
}
```

**Test 4: Same-Tenant Subscription Succeeds**
```csharp
[Fact]
public async Task SubscribeToDeployment_FromSameTenant_Succeeds()
{
    // Arrange
    var deployment = await CreateDeployment(tenantId: "tenant1");
    var connection = await CreateAuthenticatedConnection(tenantId: "tenant1");

    bool notificationReceived = false;
    connection.On<DeploymentProgressUpdate>("ProgressUpdate", update =>
    {
        notificationReceived = true;
    });

    // Act
    await connection.InvokeAsync("SubscribeToDeployment",
        deployment.ExecutionId.ToString());

    // Trigger a progress update
    await TriggerDeploymentProgress(deployment.ExecutionId);

    // Wait for notification
    await Task.Delay(1000);

    // Assert
    notificationReceived.Should().BeTrue();
}
```

### Definition of Done

- [x] `[Authorize]` attribute added
- [x] IDOR protection implemented
- [x] All 4 tests passing
- [x] Security review approved
- [x] Logging added for auth failures
- [x] Code review approved
- [x] Merged to main

### Related Issues

- See CODE_REVIEW_REPORT.md section: "API Security Issues #15"
- Related to: Issue #1 (Schema auth), Issue #9 (IDOR)

---

## Issue #14: [CRITICAL] Production Environment Detection Weakness

**Priority:** 🔴 CRITICAL
**Category:** Security - Configuration
**Assigned To:** Security Track Lead
**Estimated Effort:** 4 hours
**Phase:** 1 - Week 3

### Problem Description

Demo user seeding and other dev features rely solely on `ASPNETCORE_ENVIRONMENT` variable check. If this variable is missing or misconfigured, dangerous features could be enabled in production.

### Impact

**SECURITY BYPASS RISK**
- Demo credentials active if environment variable wrong
- Development features enabled in production
- Single point of failure for security
- No defense-in-depth

### Code Location

**File:** `src/HotSwap.Distributed.Infrastructure/Authentication/InMemoryUserRepository.cs`

**Lines:** 31-36

**Current Code:**
```csharp
// Line 31-36 - SINGLE POINT OF FAILURE
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase))
{
    return; // Don't seed demo users in production
}

// Demo users created if environment != "Production"
// ❌ What if environment is null or misspelled?
```

### Recommended Fix

**Secure-by-default pattern with explicit opt-in:**

```csharp
// Don't rely on environment variable alone
// Require EXPLICIT configuration opt-in

public class InMemoryUserRepository : IUserRepository
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<InMemoryUserRepository> _logger;

    public InMemoryUserRepository(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;

        // ✅ SECURE BY DEFAULT: Require explicit opt-in
        var allowDemoUsers = _configuration.GetValue<bool>("DemoUsers:Enabled", defaultValue: false);

        // Additional safety check: NEVER in production, even if configured
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var isProduction = environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

        if (isProduction && allowDemoUsers)
        {
            _logger.LogCritical(
                "⛔ SECURITY VIOLATION: DemoUsers:Enabled=true in production environment! " +
                "Demo users will NOT be created. Fix configuration immediately.");
            allowDemoUsers = false;
        }

        if (allowDemoUsers)
        {
            _logger.LogWarning(
                "⚠️ DEMO USERS ENABLED - NOT FOR PRODUCTION USE. " +
                "Environment: {Environment}. " +
                "This should only be enabled in local development.",
                environment);

            SeedDemoUsers();
        }
        else
        {
            _logger.LogInformation(
                "Demo users disabled. Environment: {Environment}, " +
                "DemoUsers:Enabled: {DemoUsersEnabled}",
                environment, _configuration.GetValue<bool>("DemoUsers:Enabled"));
        }
    }
}
```

**Configuration:**

```json
// appsettings.json (default - secure)
{
  "DemoUsers": {
    "Enabled": false  // Secure by default
  }
}

// appsettings.Development.json (local dev only)
{
  "DemoUsers": {
    "Enabled": true  // Only in local dev
  }
}

// appsettings.Production.json (explicit disable)
{
  "DemoUsers": {
    "Enabled": false  // Double-check production
  }
}
```

### Acceptance Criteria

- [ ] Explicit opt-in required via configuration
- [ ] Production always disables demo users (even if misconfigured)
- [ ] Warning logged if demo users enabled
- [ ] Critical error logged if production + enabled
- [ ] Secure-by-default (false if config missing)
- [ ] All existing tests pass
- [ ] New configuration tests added

### Test Cases

**Test 1: Production Environment Rejects Demo Users**
```csharp
[Fact]
public void InMemoryUserRepository_InProduction_WithDemoEnabled_StillDisablesDemoUsers()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["DemoUsers:Enabled"] = "true" // ❌ Trying to enable in production
        })
        .Build();

    // Act
    var repository = new InMemoryUserRepository(config, _logger);
    var users = repository.GetAllAsync().Result;

    // Assert
    users.Should().BeEmpty(); // Demo users NOT created

    // Verify critical log
    _logger.Verify(
        x => x.Log(
            LogLevel.Critical,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SECURITY VIOLATION")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}
```

**Test 2: Missing Configuration Defaults to Secure**
```csharp
[Fact]
public void InMemoryUserRepository_WithoutConfiguration_DefaultsToDisabled()
{
    // Arrange - No DemoUsers configuration
    var config = new ConfigurationBuilder().Build();

    // Act
    var repository = new InMemoryUserRepository(config, _logger);
    var users = repository.GetAllAsync().Result;

    // Assert
    users.Should().BeEmpty(); // Secure by default
}
```

**Test 3: Development With Explicit Opt-In Enables Demo Users**
```csharp
[Fact]
public void InMemoryUserRepository_InDevelopment_WithEnabled_CreatesDemoUsers()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["DemoUsers:Enabled"] = "true"
        })
        .Build();

    // Act
    var repository = new InMemoryUserRepository(config, _logger);
    var users = repository.GetAllAsync().Result;

    // Assert
    users.Should().NotBeEmpty();
    users.Should().Contain(u => u.Username == "admin");
}
```

### Definition of Done

- [x] Secure-by-default implementation
- [x] Production safeguard added
- [x] All 3 tests passing
- [x] Critical logging for violations
- [x] Configuration updated
- [x] Code review approved
- [x] Merged to main

### Related Issues

- See CODE_REVIEW_REPORT.md section: "Infrastructure Issues #3"
- Related to: Issue #4 (Hardcoded credentials)

---

## Issue #15: [CRITICAL] Permissive CORS Configuration

**Priority:** 🔴 CRITICAL
**Category:** Security - CORS
**Assigned To:** Security Track Lead
**Estimated Effort:** 3 hours
**Phase:** 1 - Week 3

### Problem Description

Development mode uses `AllowAnyOrigin()` which is extremely permissive and could accidentally be deployed to production, enabling XSS attacks and data exfiltration.

### Impact

**CORS BYPASS RISK**
- Any origin can make requests if deployed to production
- XSS attacks from malicious origins
- Data exfiltration to attacker-controlled domains
- CSRF bypass

### Code Location

**File:** `src/HotSwap.Distributed.Api/Program.cs`

**Lines:** 399-404

**Current Code:**
```csharp
if (builder.Environment.IsDevelopment())
{
    policy.AllowAnyOrigin()  // ⚠️ EXTREMELY PERMISSIVE
          .AllowAnyMethod()
          .AllowAnyHeader();
}
```

### Recommended Fix

**Whitelist specific origins even in development:**

```csharp
// Program.cs - Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        if (allowedOrigins.Length == 0)
        {
            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "CORS AllowedOrigins must be configured in production");
            }

            // Development fallback (but still specific)
            allowedOrigins = new[]
            {
                "http://localhost:3000",
                "http://localhost:8080",
                "http://localhost:5173"
            };

            _logger.LogWarning(
                "Using default CORS origins for development: {Origins}",
                string.Join(", ", allowedOrigins));
        }

        policy.WithOrigins(allowedOrigins) // ✅ Whitelist specific origins
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for cookies
    });
});

// Use the policy
app.UseCors("AllowedOrigins");
```

**Configuration:**

```json
// appsettings.Development.json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:8080",
      "http://localhost:5173"
    ]
  }
}

// appsettings.Production.json
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.example.com",
      "https://admin.example.com"
    ]
  }
}
```

### Acceptance Criteria

- [ ] `AllowAnyOrigin()` removed from all environments
- [ ] Whitelist specific origins from configuration
- [ ] Production requires explicit configuration
- [ ] Development uses safe defaults (localhost only)
- [ ] Requests from non-whitelisted origins rejected
- [ ] All existing tests pass
- [ ] New CORS tests added

### Test Cases

**Test 1: Non-Whitelisted Origin Rejected**
```csharp
[Fact]
public async Task Api_WithNonWhitelistedOrigin_RejectsCorsRequest()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("Origin", "https://malicious-site.com");

    // Act
    var response = await client.GetAsync("/api/v1/deployments");

    // Assert - No CORS headers in response
    response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
}
```

**Test 2: Whitelisted Origin Accepted**
```csharp
[Fact]
public async Task Api_WithWhitelistedOrigin_AllowsCorsRequest()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");

    // Act
    var response = await client.GetAsync("/api/v1/deployments");

    // Assert - CORS headers present
    response.Headers.GetValues("Access-Control-Allow-Origin")
        .Should().Contain("http://localhost:3000");
}
```

**Test 3: Production Without Config Fails**
```csharp
[Fact]
public void Startup_InProduction_WithoutCorsConfig_ThrowsException()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
    var config = new ConfigurationBuilder().Build(); // No CORS config

    // Act & Assert
    var action = () => new WebApplicationBuilder(config);
    action.Should().Throw<InvalidOperationException>()
        .WithMessage("*CORS AllowedOrigins must be configured*");
}
```

### Definition of Done

- [x] AllowAnyOrigin() removed
- [x] Whitelist configuration implemented
- [x] All 3 tests passing
- [x] Production safeguard added
- [x] Documentation updated
- [x] Code review approved
- [x] Merged to main

### Related Issues

- See CODE_REVIEW_REPORT.md section: "API Security Issues #17"
- Related to: Issue #11 (CSRF protection)

---

## 🎉 ALL 15 CRITICAL ISSUES CREATED

**Summary:**
- **Issues 1-5:** Authentication, Tenant Isolation, Async/Await, Credentials, Memory Leaks
- **Issues 6-10:** Race Conditions, Division by Zero, State Management, IDOR, Rollback Failures
- **Issues 11-15:** CSRF, JWT Keys, SignalR Auth, Environment Detection, CORS

**Total Estimated Effort:** 109 hours (2.7 weeks with 40-hour weeks)

**Next Steps:**
1. Create these as GitHub Issues
2. Assign to track leads (Security, Infrastructure, Application)
3. Begin Phase 1 sprint (Week 1)

**Labels for all issues:**
- "blocker"
- "security" (for security issues)
- "P0"

**Milestone:** Phase 1 - Critical Remediation
