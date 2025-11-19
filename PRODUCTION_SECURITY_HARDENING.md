# Production Security Hardening Guide

**Last Updated**: 2025-11-19
**Version**: 1.0
**For**: Distributed Kernel Orchestration System
**Security Rating**: ⭐⭐⭐⭐☆ GOOD (4/5) - See OWASP_SECURITY_REVIEW.md

---

## Overview

This guide provides step-by-step instructions for securing the Distributed Kernel Orchestration API for production deployment. It consolidates findings from the OWASP Top 10 2021 security review and provides actionable hardening steps.

**Target Audience:** DevOps engineers, system administrators, security teams
**Prerequisites:** Completed development, passing tests, OWASP review completed

---

## ✅ Security Improvements Implemented (Sprint 2)

### 1. Updated Vulnerable Dependency ✅
**Issue:** Microsoft.AspNetCore.Http.Abstractions 2.2.0 (outdated)
**Fixed:** Updated to 8.0.0 (2025-11-19)
**File:** `src/HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj`

### 2. Account Lockout Protection ✅
**Issue:** Missing brute-force protection
**Fixed:** Implemented account lockout after 5 failed login attempts (2025-11-19)
**Details:**
- **Lockout Threshold:** 5 consecutive failed attempts
- **Lockout Duration:** 15 minutes
- **Auto-Unlock:** Lockout expires automatically after 15 minutes
- **Tracking:** Failed attempts tracked per user
- **Reset:** Successful login resets failed attempt counter

**Files Modified:**
- `src/HotSwap.Distributed.Domain/Models/User.cs` (added lockout properties)
- `src/HotSwap.Distributed.Infrastructure/Authentication/InMemoryUserRepository.cs` (lockout logic)

**User Model Changes:**
```csharp
public int FailedLoginAttempts { get; set; } = 0;
public DateTime? LockoutEnd { get; set; }
public bool IsLockedOut() { /* auto-expire logic */ }
```

---

## 🔴 High-Priority Pre-Production Checklist

Complete these items **BEFORE** deploying to production.

### 1. ✅ Update Vulnerable Dependencies (COMPLETED)

**Status:** ✅ Completed (2025-11-19)

```xml
<!-- Already fixed in HotSwap.Distributed.Infrastructure.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="8.0.0" />
```

### 2. ✅ Implement Account Lockout (COMPLETED)

**Status:** ✅ Completed (2025-11-19)

Account lockout is now enforced:
- 5 failed attempts → 15-minute lockout
- Automatic expiration and reset
- Comprehensive logging of lockout events

### 3. 🔲 Enable Multi-Factor Authentication (MFA)

**Status:** ⏳ Pending (Estimated: 16 hours)
**Priority:** 🔴 Critical for Admin role

**Implementation Steps:**

1. **Add TOTP Package:**
   ```bash
   cd src/HotSwap.Distributed.Infrastructure
   dotnet add package Otp.NET --version 1.3.0
   ```

2. **Extend User Model:**
   ```csharp
   // Add to User.cs
   public string? MfaSecret { get; set; }  // TOTP secret (encrypted)
   public bool MfaEnabled { get; set; } = false;
   public List<string> RecoveryCodes { get; set; } = new();
   ```

3. **Create MFA Service:**
   ```csharp
   public interface IMfaService
   {
       string GenerateSecret();
       string GenerateQrCodeUrl(string username, string secret);
       bool ValidateTotp(string secret, string code);
       List<string> GenerateRecoveryCodes(int count = 8);
   }
   ```

4. **Add MFA Endpoints:**
   ```
   POST /api/v1/authentication/mfa/setup      - Generate TOTP secret and QR code
   POST /api/v1/authentication/mfa/enable     - Enable MFA with verification
   POST /api/v1/authentication/mfa/disable    - Disable MFA (requires current code)
   POST /api/v1/authentication/mfa/verify     - Verify TOTP code
   ```

5. **Update Login Flow:**
   ```csharp
   // Modified login process:
   // 1. Validate username/password
   // 2. If MFA enabled, require TOTP code
   // 3. Issue JWT only after MFA verification
   ```

6. **Enforce for Admin Role:**
   ```csharp
   // Require MFA for all Admin users
   if (user.IsAdmin() && !user.MfaEnabled)
   {
       return BadRequest("MFA is required for Admin role");
   }
   ```

**Testing:**
- Use Google Authenticator or Authy for testing
- Verify QR code scanning and code generation
- Test recovery codes functionality
- Test lockout after failed MFA attempts

**References:**
- [Otp.NET Documentation](https://github.com/kspearrin/Otp.NET)
- [RFC 6238 - TOTP](https://tools.ietf.org/html/rfc6238)

### 4. 🔲 Enable Security Strict Mode

**Status:** ⏳ Pending (Estimated: 1 hour)
**Priority:** 🟡 High

**Configuration Changes:**

```json
// appsettings.Production.json
{
  "Security": {
    "StrictMode": true  // Currently false in development
  }
}
```

**Impact:**
- Enforces module signature verification (no bypass)
- Rejects unsigned modules
- Stricter input validation
- Enhanced security logging

**Testing:**
```bash
# Verify strict mode is enabled in production
curl -H "Authorization: Bearer $TOKEN" \
  https://api.production.example.com/api/v1/clusters/production

# Should require valid module signatures
```

### 5. 🔲 Configure Production JWT Secret

**Status:** ⏳ Pending (Estimated: 30 minutes)
**Priority:** 🔴 Critical

**Action Required:**

```bash
# Generate a strong random secret (32+ characters)
openssl rand -base64 48

# Set as environment variable
export JWT__SECRETKEY="<generated-secret-here>"

# Or in appsettings.Production.json (encrypted)
{
  "Jwt": {
    "SecretKey": "<secret-from-key-vault>"
  }
}
```

**❌ Never:**
- Use the default development secret in production
- Commit secrets to source control
- Share secrets via email/chat

**✅ Recommended:**
- Use Azure Key Vault or HashiCorp Vault
- Rotate secrets every 90 days
- Use different secrets per environment

### 6. 🔲 Configure HTTPS with Valid Certificates

**Status:** ⏳ Pending (Estimated: 2 hours)
**Priority:** 🔴 Critical

**Steps:**

1. **Obtain SSL Certificate:**
   ```bash
   # Option 1: Let's Encrypt (free, automated)
   certbot certonly --standalone -d api.example.com

   # Option 2: Commercial CA (DigiCert, GlobalSign, etc.)
   # Purchase and download certificate
   ```

2. **Configure Kestrel:**
   ```json
   // appsettings.Production.json
   {
     "Kestrel": {
       "Endpoints": {
         "Https": {
           "Url": "https://+:443",
           "Certificate": {
             "Path": "/etc/ssl/certs/api.example.com.pfx",
             "Password": "<password-from-key-vault>"
           }
         }
       }
     }
   }
   ```

3. **Enable HSTS:**
   ```json
   {
     "Hsts": {
       "MaxAge": 31536000,        // 1 year
       "IncludeSubDomains": true,
       "Preload": true            // Set to true in production
     }
   }
   ```

4. **Verify:**
   ```bash
   # Test SSL configuration
   curl -I https://api.example.com/health

   # Check SSL Labs rating
   # https://www.ssllabs.com/ssltest/analyze.html?d=api.example.com
   ```

**See Also:** `HTTPS_SETUP_GUIDE.md` for detailed instructions

### 7. 🔲 Configure Centralized Logging

**Status:** ⏳ Pending (Estimated: 4-8 hours)
**Priority:** 🟡 High

**Options:**

**Option A: Azure Application Insights**
```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});
```

**Option B: ELK Stack (Elasticsearch, Logstash, Kibana)**
```bash
dotnet add package Serilog.Sinks.Elasticsearch
```

```csharp
// Program.cs
Log.Logger = new LoggerConfiguration()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("https://elasticsearch:9200"))
    {
        IndexFormat = "distributed-kernel-{0:yyyy.MM}",
        AutoRegisterTemplate = true,
        NumberOfShards = 2,
        NumberOfReplicas = 1
    })
    .CreateLogger();
```

**Retention Policy:**
- Security events: 90 days minimum
- Audit logs: 1 year minimum (compliance)
- General logs: 30 days

**Alert Configuration:**
```yaml
# Recommended alerts
- Failed authentication rate > 10/min
- Account lockouts > 5/hour
- Deployment failures > 3/hour
- HTTP 5xx rate > 1%
- Memory usage > 80%
```

---

## 🟡 Medium-Priority Security Improvements

Complete these items within 30 days of production deployment.

### 8. IP Whitelisting for Admin Endpoints

**Estimated:** 2 hours

```csharp
// Create AdminIpRestrictionMiddleware.cs
public class AdminIpRestrictionMiddleware
{
    private readonly List<string> _allowedIps = new()
    {
        "10.0.0.0/8",      // Internal network
        "192.168.1.0/24"   // Office network
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/admin"))
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            if (!IsAllowedIp(remoteIp))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied from this IP");
                return;
            }
        }
        await _next(context);
    }
}
```

### 9. Web Application Firewall (WAF)

**Estimated:** 4-8 hours (cloud setup)

**Options:**
- **Azure:** Azure Application Gateway with WAF
- **AWS:** AWS WAF
- **Cloudflare:** Cloudflare WAF
- **On-Premises:** ModSecurity with NGINX

**Recommended Rules:**
- OWASP Core Rule Set (CRS)
- SQL injection protection
- XSS protection
- Rate limiting (application layer)
- Geo-blocking (if applicable)

### 10. Database Connection Security

**Estimated:** 2 hours

**PostgreSQL Security:**
```bash
# Require SSL for all connections
# postgresql.conf
ssl = on
ssl_cert_file = '/etc/ssl/certs/server.crt'
ssl_key_file = '/etc/ssl/private/server.key'

# pg_hba.conf - Require SSL
hostssl all all 0.0.0.0/0 md5
```

**Connection String:**
```json
{
  "ConnectionStrings": {
    "AuditLogDatabase": "Host=db.example.com;Database=auditlogs;Username=appuser;Password=<from-key-vault>;SSL Mode=Require;Trust Server Certificate=false"
  }
}
```

### 11. Network Segmentation

**Estimated:** Varies (infrastructure dependent)

**Recommended Architecture:**
```
Internet
   ↓
[WAF / Load Balancer]
   ↓
[DMZ - API Servers]
   ↓
[Internal Network - PostgreSQL, Redis]
```

**Firewall Rules:**
- Internet → WAF: 443 (HTTPS only)
- WAF → API: 5000-5001 (HTTP/HTTPS)
- API → PostgreSQL: 5432 (SSL only)
- API → Redis: 6379 (TLS only, private network)
- Block all other traffic

### 12. Secrets Management

**Estimated:** 8-16 hours (full implementation)

**Current Status:** Using environment variables (basic)
**Target:** Azure Key Vault or HashiCorp Vault

**Azure Key Vault Integration:**
```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
```

```csharp
// Program.cs
if (builder.Environment.IsProduction())
{
    var keyVaultEndpoint = new Uri(builder.Configuration["KeyVault:Endpoint"]!);
    builder.Configuration.AddAzureKeyVault(
        keyVaultEndpoint,
        new DefaultAzureCredential());
}
```

**Secrets to Store in Vault:**
- JWT secret key
- PostgreSQL connection string
- Redis connection string
- Module signing certificate private key
- API keys for external services

**Secret Rotation:**
- JWT secret: Every 90 days
- Database passwords: Every 90 days
- API keys: Every 180 days
- Certificates: Before expiration (automated with Let's Encrypt)

---

## 🟢 Low-Priority Enhancements

Complete these items based on specific requirements.

### 13. Security Headers Hardening

Already implemented, but can be further hardened:

```csharp
// SecurityHeadersMiddleware.cs
context.Response.Headers["Content-Security-Policy"] =
    "default-src 'self'; " +
    "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +  // Remove unsafe-* in production
    "style-src 'self' 'unsafe-inline'; " +
    "img-src 'self' data: https:; " +
    "connect-src 'self'; " +
    "frame-ancestors 'none';";

// Add additional headers
context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
```

### 14. Penetration Testing

**Recommended:** Annual penetration testing

**Scope:**
- OWASP Top 10 vulnerabilities
- Authentication and authorization bypass
- API endpoint fuzzing
- Deployment workflow security
- Infrastructure security

**Tools:**
- OWASP ZAP (free, automated)
- Burp Suite Professional (paid, manual)
- Nessus (vulnerability scanning)

### 15. Security Monitoring and Alerting

**Metrics to Monitor:**
- Failed authentication attempts (rate and count)
- Account lockouts
- Deployment failures
- Unusual API usage patterns
- Certificate expiration (30 days warning)

**Alert Thresholds:**
```yaml
alerts:
  - name: high_failed_auth_rate
    condition: failed_auth_rate > 10/min
    severity: high

  - name: account_lockout_spike
    condition: lockouts > 5/hour
    severity: medium

  - name: deployment_failure_rate
    condition: deployment_failure_rate > 0.1
    severity: medium

  - name: certificate_expiring
    condition: cert_expiry_days < 30
    severity: high
```

---

## Production Deployment Checklist

Use this checklist before deploying to production.

### Pre-Deployment Security Checklist

- [ ] ✅ All dependencies updated to latest stable versions
- [ ] ✅ Account lockout implemented (5 attempts, 15min lockout)
- [ ] 🔲 MFA enabled for all Admin users
- [ ] 🔲 Production JWT secret configured (not default)
- [ ] 🔲 HTTPS configured with valid SSL certificate
- [ ] 🔲 HSTS enabled with Preload=true
- [ ] 🔲 Security.StrictMode=true in production config
- [ ] 🔲 Centralized logging configured (Application Insights / ELK)
- [ ] 🔲 Audit logging to PostgreSQL configured
- [ ] 🔲 Database connections use SSL
- [ ] 🔲 Redis connections use TLS
- [ ] 🔲 Secrets stored in Key Vault (not appsettings.json)
- [ ] 🔲 WAF configured and tested
- [ ] 🔲 Rate limiting tested under load
- [ ] 🔲 Network segmentation implemented
- [ ] 🔲 Firewall rules configured
- [ ] 🔲 IP whitelisting for admin endpoints
- [ ] 🔲 Security headers verified (CSP, HSTS, X-Frame-Options)
- [ ] 🔲 Error messages don't expose sensitive data
- [ ] 🔲 Debug mode disabled (ASPNETCORE_ENVIRONMENT=Production)
- [ ] 🔲 Demo users disabled in production
- [ ] 🔲 Penetration testing completed
- [ ] 🔲 Security incident response plan documented
- [ ] 🔲 Backup and disaster recovery plan tested

### Post-Deployment Security Verification

**Immediate (Day 1):**
- [ ] Verify HTTPS endpoints respond correctly
- [ ] Test authentication flow (success and failure)
- [ ] Verify account lockout works (5 failed attempts)
- [ ] Check security headers in responses
- [ ] Test rate limiting enforcement
- [ ] Verify audit logs are being written to PostgreSQL
- [ ] Check centralized logging dashboard

**Within 7 Days:**
- [ ] Review authentication logs for anomalies
- [ ] Verify alert thresholds are appropriate
- [ ] Test backup and restore procedures
- [ ] Review SSL Labs rating (target: A+)
- [ ] Conduct security smoke tests

**Within 30 Days:**
- [ ] Complete MFA implementation
- [ ] Review and adjust rate limits based on actual traffic
- [ ] Tune WAF rules (reduce false positives)
- [ ] Review audit logs for security events
- [ ] Update security documentation

**Quarterly:**
- [ ] Rotate all secrets (JWT, database passwords)
- [ ] Review dependency vulnerabilities (dotnet list package --vulnerable)
- [ ] Update SSL certificates if needed
- [ ] Review security logs and metrics
- [ ] Update security incident response plan

---

## Security Incident Response

### Suspected Breach Procedure

1. **Immediately:**
   - Lock affected user accounts
   - Review audit logs for suspicious activity
   - Check recent deployments for unauthorized changes
   - Isolate affected systems if needed

2. **Within 1 Hour:**
   - Notify security team and management
   - Begin root cause analysis
   - Review authentication logs
   - Check for data exfiltration

3. **Within 24 Hours:**
   - Rotate all secrets (JWT, database passwords, API keys)
   - Force logout of all users (invalidate JWT tokens)
   - Patch vulnerabilities if identified
   - Document incident timeline

4. **Within 7 Days:**
   - Complete incident report
   - Implement additional security controls
   - Conduct security review
   - Update incident response plan

### Emergency Contacts

```
Security Team:     security@example.com
On-Call Engineer:  +1-xxx-xxx-xxxx
Management:        management@example.com
Legal:             legal@example.com
```

---

## Security Metrics and KPIs

Track these metrics monthly:

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Failed auth rate | < 5% | > 10% |
| Account lockouts | < 10/day | > 50/day |
| Deployment success rate | > 95% | < 90% |
| SSL Labs rating | A+ | < A |
| Vulnerability count (High/Critical) | 0 | > 0 |
| Mean time to patch (days) | < 7 | > 14 |
| Security incidents | 0 | > 0 |
| Audit log retention compliance | 100% | < 100% |

---

## References

- [OWASP_SECURITY_REVIEW.md](OWASP_SECURITY_REVIEW.md) - Full security assessment
- [HTTPS_SETUP_GUIDE.md](HTTPS_SETUP_GUIDE.md) - SSL/TLS configuration
- [JWT_AUTHENTICATION_GUIDE.md](JWT_AUTHENTICATION_GUIDE.md) - Authentication details
- [APPROVAL_WORKFLOW_GUIDE.md](APPROVAL_WORKFLOW_GUIDE.md) - Approval security
- [AUDIT_LOG_SCHEMA.md](docs/AUDIT_LOG_SCHEMA.md) - Audit logging
- [PROMETHEUS_METRICS_GUIDE.md](docs/PROMETHEUS_METRICS_GUIDE.md) - Monitoring

**External Resources:**
- [OWASP Top 10 2021](https://owasp.org/Top10/)
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)
- [CIS Benchmarks](https://www.cisecurity.org/cis-benchmarks/)
- [Microsoft Security Best Practices](https://docs.microsoft.com/en-us/security/)

---

**Last Updated:** 2025-11-19
**Next Review:** 2025-12-19
**Maintainer:** Security Team
**License:** MIT

