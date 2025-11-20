# Security Hardening Skill

**Version:** 1.0.0
**Last Updated:** 2025-11-20
**Skill Type:** Security & Compliance
**Estimated Time:** 2-4 hours per implementation
**Complexity:** Medium-High

---

## Purpose

Comprehensive security hardening for .NET applications, covering secret rotation, OWASP Top 10 compliance, and security configuration validation.

**Use this skill when:**
- Implementing Azure Key Vault or HashiCorp Vault integration
- Rotating secrets (JWT keys, DB passwords, API keys)
- Reviewing OWASP Top 10 compliance
- Hardening security configurations
- Preparing for security audits

**This skill addresses:**
- Task #16: Secret Rotation System (2-3 days)
- Task #17: OWASP Top 10 Security Review (2-3 days)
- General security improvements

---

## Phase 1: Secret Rotation with Key Vault

### Step 1.1: Choose Secret Management Provider

**Azure Key Vault** (recommended for Azure deployments):
- Managed service, automatic backups
- Azure AD integration
- $0.03 per 10,000 operations

**HashiCorp Vault** (recommended for multi-cloud/on-prem):
- Open source, self-hosted
- Dynamic secrets support
- More complex setup

### Step 1.2: Install Azure Key Vault SDK

```bash
dotnet add package Azure.Identity
dotnet add package Azure.Security.KeyVault.Secrets
```

### Step 1.3: Implement Secret Service

```csharp
// Infrastructure/Security/ISecretService.cs
public interface ISecretService
{
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
    Task SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
    Task RotateSecretAsync(string secretName, CancellationToken cancellationToken = default);
}

// Infrastructure/Security/AzureKeyVaultSecretService.cs
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

public class AzureKeyVaultSecretService : ISecretService
{
    private readonly SecretClient _client;

    public AzureKeyVaultSecretService(string keyVaultUrl)
    {
        _client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        var secret = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        return secret.Value.Value;
    }

    public async Task SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken)
    {
        await _client.SetSecretAsync(secretName, secretValue, cancellationToken);
    }

    public async Task RotateSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        // Generate new secret value
        var newValue = GenerateSecureValue();
        
        // Store with version
        await _client.SetSecretAsync(secretName, newValue, cancellationToken);
    }

    private string GenerateSecureValue()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
```

### Step 1.4: Configure in Program.cs

```csharp
// Automatically load secrets from Key Vault
var keyVaultUrl = builder.Configuration["KeyVault:Url"];
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential()
    );
}

// Register secret service
builder.Services.AddSingleton<ISecretService>(sp =>
    new AzureKeyVaultSecretService(keyVaultUrl));
```

### Step 1.5: Use Secrets in Code

```csharp
// BEFORE (hardcoded)
var jwtKey = "super-secret-key-123"; // ❌ BAD

// AFTER (from Key Vault)
var jwtKey = await _secretService.GetSecretAsync("JwtSigningKey");
```

### Step 1.6: Implement Rotation Schedule

```csharp
// Infrastructure/BackgroundServices/SecretRotationBackgroundService.cs
public class SecretRotationBackgroundService : BackgroundService
{
    private readonly ISecretService _secretService;
    private readonly ILogger<SecretRotationBackgroundService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Rotate secrets every 90 days
                await Task.Delay(TimeSpan.FromDays(90), stoppingToken);

                _logger.LogInformation("Rotating secrets...");

                // Rotate JWT signing key
                await _secretService.RotateSecretAsync("JwtSigningKey", stoppingToken);

                // Rotate database password (coordinate with DB)
                await RotateDatabasePasswordAsync(stoppingToken);

                _logger.LogInformation("Secrets rotated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Secret rotation failed");
            }
        }
    }

    private async Task RotateDatabasePasswordAsync(CancellationToken cancellationToken)
    {
        // 1. Generate new password
        var newPassword = GenerateSecurePassword();

        // 2. Update database user password
        await UpdateDatabasePasswordAsync(newPassword, cancellationToken);

        // 3. Store new password in Key Vault
        await _secretService.SetSecretAsync("DatabasePassword", newPassword, cancellationToken);

        // 4. Restart application to pick up new password
        // (or use connection pool refresh)
    }
}
```

---

## Phase 2: OWASP Top 10 Compliance

### A01: Broken Access Control

**Checklist:**
- [x] JWT authentication implemented (✅ Task #1)
- [x] Role-based authorization (Admin, Deployer, Viewer)
- [x] Authorization attributes on all controllers
- [ ] Implement IP whitelisting for admin endpoints
- [ ] Add MFA for Admin role

**Verify:**
```csharp
// Check all controllers have [Authorize]
grep -r "\[Authorize" src/HotSwap.Distributed.Api/Controllers/

// Test unauthorized access returns 401/403
curl -X POST http://localhost:5000/api/v1/deployments
# Expected: 401 Unauthorized
```

### A02: Cryptographic Failures

**Checklist:**
- [x] BCrypt for password hashing (✅ Task #1)
- [x] HTTPS/TLS 1.2+ (✅ Task #15)
- [x] RSA-2048 for module signatures
- [x] HMAC-SHA256 for JWT tokens
- [ ] Rotate secrets every 90 days (Task #16)

**Verify:**
```bash
# Check TLS version
openssl s_client -connect localhost:5001 -tls1_2

# Check password hashing
grep -r "BCrypt.HashPassword" src/
```

### A03: Injection

**Checklist:**
- [x] EF Core (parameterized queries by default)
- [x] Input validation on all API models
- [x] No raw SQL queries
- [x] JSON deserialization safe

**Verify:**
```csharp
// Check for raw SQL
grep -r "FromSqlRaw\|ExecuteSqlRaw" src/

// Check for validated models
grep -r "\[Required\]|\[StringLength\]" src/
```

### A04: Insecure Design

**Checklist:**
- [x] Approval workflow (✅ Task #2)
- [x] Module signature verification
- [x] Rate limiting (✅ Task #5)
- [x] Deployment strategies with rollback

### A05: Security Misconfiguration

**Checklist:**
- [x] HSTS headers (✅ Task #15)
- [x] Security headers (CSP, X-Frame-Options)
- [ ] Remove development endpoints in production
- [ ] Disable detailed error messages in production
- [ ] Configure StrictMode for module verification

**Fix:**
```csharp
// Program.cs
if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseExceptionHandler("/error"); // Don't expose stack traces
    // DON'T use app.UseDeveloperExceptionPage()
}

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "no-referrer");
    context.Response.Headers.Add("Content-Security-Policy", 
        "default-src 'self'; script-src 'self'; style-src 'self'");
    await next();
});
```

### A06: Vulnerable and Outdated Components

**Check for vulnerabilities:**
```bash
dotnet list package --vulnerable
# Fix any HIGH or CRITICAL vulnerabilities

# Update outdated packages
dotnet list package --outdated
dotnet add package PackageName --version LatestVersion
```

### A07: Identification and Authentication Failures

**Missing features (high priority):**
- [ ] Account lockout after 5 failed login attempts
- [ ] Multi-factor authentication (MFA) for Admin role
- [ ] Password complexity requirements

**Implement account lockout:**
```csharp
public class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<string, int> _failedAttempts = new();

    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        // Check if account is locked
        if (_failedAttempts.GetValueOrDefault(username, 0) >= 5)
        {
            throw new AccountLockedException($"Account {username} is locked due to too many failed attempts");
        }

        var user = await GetUserByUsernameAsync(username, cancellationToken);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // Increment failed attempts
            _failedAttempts[username] = _failedAttempts.GetValueOrDefault(username, 0) + 1;
            return null;
        }

        // Reset failed attempts on successful login
        _failedAttempts[username] = 0;
        return user;
    }
}
```

### A08: Software and Data Integrity Failures

**Checklist:**
- [x] Module signature verification (RSA-2048)
- [x] Audit logging for all changes
- [ ] Code signing for application binaries
- [ ] Verify NuGet package signatures

### A09: Security Logging and Monitoring Failures

**Checklist:**
- [x] Audit log all authentication events
- [x] Audit log all deployment events
- [x] Audit log all approval decisions
- [ ] Set up alerts for suspicious activity
- [ ] Integrate with SIEM

### A10: Server-Side Request Forgery (SSRF)

**Checklist:**
- [x] No user-controlled URLs in code
- [x] Module BinaryPath validated
- [x] No external HTTP requests based on user input

---

## Phase 3: Production Security Checklist

### Pre-Production (Sprint 2)

**Critical (must-fix before production):**
- [ ] Update outdated dependency: Microsoft.AspNetCore.Http.Abstractions 2.2.0 → 8.0.0
- [ ] Implement account lockout (5 failed attempts)
- [ ] Rotate JWT signing key to Key Vault

**High Priority (recommended before large-scale):**
- [ ] Add MFA for Admin role
- [ ] Configure IP whitelisting for admin endpoints
- [ ] Set up centralized logging (ELK/Splunk)

**Medium Priority (nice-to-have):**
- [ ] Implement password complexity requirements
- [ ] Add session timeout (idle 30 minutes)
- [ ] Configure WAF (Web Application Firewall)

### Post-Production Monitoring

**Set up alerts for:**
- 10+ failed login attempts in 5 minutes → Possible brute force
- Deployment to Production without approval → Policy violation
- Error rate >5% → Service degradation
- Unusual access patterns → Investigate

---

## Quick Reference: Security Commands

```bash
# Check for vulnerable packages
dotnet list package --vulnerable

# Check for outdated packages
dotnet list package --outdated

# Update all packages
dotnet outdated --upgrade

# Scan for secrets in code
git secrets --scan

# Test TLS configuration
openssl s_client -connect localhost:5001 -tls1_2

# Check security headers
curl -I https://localhost:5001
```

---

## Success Criteria

- [ ] Secrets stored in Key Vault (not code/config)
- [ ] Secret rotation schedule implemented
- [ ] OWASP Top 10 reviewed and addressed
- [ ] Security checklist complete
- [ ] Vulnerability scan clean (no HIGH/CRITICAL)
- [ ] Security audit report generated
- [ ] TASK_LIST.md updated (Tasks #16, #17 complete)

---

**Last Updated:** 2025-11-20
**Version:** 1.0.0
**Completes:** Tasks #16 (Secret Rotation), #17 (OWASP Review)
**Security Rating Target:** ⭐⭐⭐⭐⭐ EXCELLENT (5/5)
