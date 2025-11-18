# HTTPS/TLS Configuration Guide

**Last Updated:** 2025-11-15
**Status:** ✅ Fully Configured
**Related Tasks:** TASK_LIST.md #15

---

## Overview

This guide explains how to configure HTTPS/TLS for the Distributed Kernel Orchestration API. The application now supports both HTTP and HTTPS endpoints with proper security headers and TLS 1.2+ enforcement.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Development Setup](#development-setup)
3. [Production Setup](#production-setup)
4. [Configuration Details](#configuration-details)
5. [Docker Setup](#docker-setup)
6. [Troubleshooting](#troubleshooting)
7. [Security Best Practices](#security-best-practices)

---

## Quick Start

### For Local Development

```bash
# 1. Generate development SSL certificate
./generate-dev-cert.sh

# 2. Run the application
cd src/HotSwap.Distributed.Api
dotnet run

# 3. Access the API
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
# Swagger: https://localhost:5001/swagger
```

### For Docker

```bash
# 1. Generate development SSL certificate
./generate-dev-cert.sh

# 2. Start with Docker Compose
docker-compose up -d

# 3. Access the API
# HTTP:  http://localhost:5000
# HTTPS: https://localhost:5001
```

---

## Development Setup

### Generate Development SSL Certificate

The repository includes a script to generate self-signed SSL certificates for local development:

```bash
./generate-dev-cert.sh
```

**What this script does:**

1. Creates `src/HotSwap.Distributed.Api/certificates/` directory
2. Generates a self-signed certificate using .NET dev-certs (if available) or OpenSSL
3. Exports certificate in multiple formats:
   - `aspnetapp.pfx` - Kestrel certificate (used by the API)
   - `aspnetapp.pem` - PEM format (for other tools)
   - `aspnetapp.crt` - Certificate only
   - `aspnetapp.key` - Private key only

**Requirements:**

- **Option 1 (Preferred):** .NET SDK 8.0+ installed
  - Uses `dotnet dev-certs https` command
  - Automatically trusted on macOS
  - On Linux, run: `sudo dotnet dev-certs https --trust`

- **Option 2 (Fallback):** OpenSSL installed
  - Manual certificate generation
  - Requires manual trust in browser

**Certificate Details:**

- **Type:** Self-signed
- **Validity:** 365 days
- **Subject:** CN=localhost
- **SAN:** DNS:localhost, DNS:*.localhost, IP:127.0.0.1
- **Password:** None (empty password for development)

### Trust the Certificate

#### macOS
```bash
# Automatically trusted if using dotnet dev-certs
dotnet dev-certs https --trust
```

#### Linux
```bash
# Trust the certificate system-wide
sudo dotnet dev-certs https --trust

# Or manually add to Chrome/Brave
# 1. Open Chrome: chrome://settings/certificates
# 2. Go to "Authorities" tab
# 3. Import: src/HotSwap.Distributed.Api/certificates/aspnetapp.crt
```

#### Windows
```bash
# Automatically trusted if using dotnet dev-certs
dotnet dev-certs https --trust

# Or manually via certmgr.msc:
# 1. Run certmgr.msc
# 2. Right-click "Trusted Root Certification Authorities"
# 3. Import: src\HotSwap.Distributed.Api\certificates\aspnetapp.pfx
```

### Running with HTTPS

```bash
# Run normally - both HTTP and HTTPS are enabled
cd src/HotSwap.Distributed.Api
dotnet run

# Output:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://localhost:5000
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: https://localhost:5001
```

**Available Endpoints:**

- **HTTP:** http://localhost:5000
- **HTTPS:** https://localhost:5001
- **Swagger UI:** https://localhost:5001/swagger
- **Health Check:** https://localhost:5001/health

---

## Production Setup

⚠️ **CRITICAL:** Do NOT use self-signed certificates in production!

### Obtain a Valid SSL Certificate

**Option 1: Let's Encrypt (Free, Recommended)**

```bash
# Install Certbot
sudo apt-get install certbot

# Obtain certificate for your domain
sudo certbot certonly --standalone -d yourdomain.com -d www.yourdomain.com

# Certificates will be saved to:
# /etc/letsencrypt/live/yourdomain.com/fullchain.pem
# /etc/letsencrypt/live/yourdomain.com/privkey.pem
```

**Option 2: Commercial CA (DigiCert, Sectigo, etc.)**

Purchase an SSL certificate from a trusted Certificate Authority and follow their installation instructions.

### Convert Certificate to PFX Format (if needed)

Kestrel requires certificates in PFX format:

```bash
# Convert PEM certificate to PFX
openssl pkcs12 -export \
  -out certificate.pfx \
  -inkey privkey.pem \
  -in fullchain.pem \
  -passout pass:YourSecurePassword

# Move to application directory
sudo cp certificate.pfx /opt/distributed-kernel/certificates/
sudo chmod 600 /opt/distributed-kernel/certificates/certificate.pfx
```

### Update Production Configuration

Edit `appsettings.Production.json`:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:443",
        "Certificate": {
          "Path": "/opt/distributed-kernel/certificates/certificate.pfx",
          "Password": "YourSecurePassword"
        }
      }
    },
    "Protocols": "Http1AndHttp2"
  },
  "Hsts": {
    "Enabled": true,
    "MaxAge": 31536000,
    "IncludeSubDomains": true,
    "Preload": true
  }
}
```

**⚠️ Security Note:** Never commit passwords to source control! Use environment variables or secret management:

```bash
# Using environment variables
export ASPNETCORE_Kestrel__Certificates__Default__Password="YourSecurePassword"

# Or use Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, etc.
```

### Certificate Auto-Renewal

**For Let's Encrypt:**

```bash
# Test renewal
sudo certbot renew --dry-run

# Set up automatic renewal (crontab)
sudo crontab -e

# Add this line to renew daily and restart the API
0 0 * * * certbot renew --post-hook "systemctl restart distributed-kernel-api"
```

### HSTS Preload (Optional but Recommended)

Once HTTPS is stable in production, submit your domain to the HSTS preload list:

1. Ensure `Preload: true` in configuration
2. Test your domain: https://hstspreload.org/
3. Submit your domain to the preload list
4. Wait for inclusion in browser preload lists (can take weeks)

---

## Configuration Details

### Kestrel Configuration

The application uses Kestrel as the web server with the following configuration:

**File:** `appsettings.json`

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Https": {
        "Url": "https://localhost:5001",
        "Certificate": {
          "Path": "certificates/aspnetapp.pfx",
          "Password": ""
        }
      }
    },
    "Limits": {
      "MaxConcurrentConnections": 100,
      "MaxConcurrentUpgradedConnections": 100,
      "MaxRequestBodySize": 10485760,
      "KeepAliveTimeout": "00:02:00",
      "RequestHeadersTimeout": "00:00:30"
    },
    "Protocols": "Http1AndHttp2"
  }
}
```

**Endpoint Configuration:**

- **Http:** HTTP endpoint on port 5000
- **Https:** HTTPS endpoint on port 5001
- **Certificate Path:** Relative to application directory
- **Password:** Empty for development, use strong password in production

**Limits:**

- **MaxConcurrentConnections:** 100 simultaneous connections
- **MaxRequestBodySize:** 10 MB (10485760 bytes)
- **KeepAliveTimeout:** 2 minutes
- **RequestHeadersTimeout:** 30 seconds

**Protocols:**

- Supports both HTTP/1.1 and HTTP/2

### HSTS Configuration

HTTP Strict Transport Security (HSTS) forces browsers to use HTTPS:

**File:** `appsettings.json`

```json
{
  "Hsts": {
    "Enabled": true,
    "MaxAge": 31536000,
    "IncludeSubDomains": true,
    "Preload": false
  }
}
```

**Settings:**

- **Enabled:** Enable/disable HSTS (disabled in development, enabled in production)
- **MaxAge:** 31536000 seconds (1 year) - browsers will enforce HTTPS for this duration
- **IncludeSubDomains:** Apply HSTS to all subdomains
- **Preload:** Enable for HSTS preload list submission (set to `true` in production after testing)

**HSTS Headers Sent:**

```
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

### TLS Protocol Configuration

The application enforces TLS 1.2+ by default through .NET 8.0 defaults.

**To explicitly configure TLS versions** (if needed), update `appsettings.json`:

```json
{
  "Kestrel": {
    "EndpointDefaults": {
      "SslProtocols": ["Tls12", "Tls13"]
    }
  }
}
```

**Supported TLS Versions:**

- ✅ TLS 1.3 (recommended)
- ✅ TLS 1.2 (minimum)
- ❌ TLS 1.1 (deprecated, disabled)
- ❌ TLS 1.0 (deprecated, disabled)
- ❌ SSL 3.0 (deprecated, disabled)

---

## Docker Setup

### Building and Running with HTTPS

```bash
# 1. Generate certificates
./generate-dev-cert.sh

# 2. Build and start services
docker-compose up -d

# 3. View logs
docker-compose logs -f orchestrator-api

# 4. Test HTTPS endpoint
curl -k https://localhost:5001/health
```

### Docker Compose Configuration

**File:** `docker-compose.yml`

```yaml
orchestrator-api:
  build:
    context: .
    dockerfile: Dockerfile
  ports:
    - "5000:8080"   # HTTP
    - "5001:8081"   # HTTPS
  environment:
    - ASPNETCORE_URLS=http://+:8080;https://+:8081
    - ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certificates/aspnetapp.pfx
    - ASPNETCORE_Kestrel__Certificates__Default__Password=
  volumes:
    - ./src/HotSwap.Distributed.Api/certificates:/app/certificates:ro
```

**Key Points:**

- **Port Mapping:** 5000→8080 (HTTP), 5001→8081 (HTTPS)
- **Volume Mount:** Certificates are mounted read-only from host
- **Environment Variables:** Configure Kestrel to use mounted certificate

### Production Docker Deployment

For production, use Docker secrets or environment variables for certificate passwords:

```yaml
orchestrator-api:
  environment:
    - ASPNETCORE_Kestrel__Certificates__Default__Password=${CERT_PASSWORD}
  secrets:
    - cert_password

secrets:
  cert_password:
    external: true
```

---

## Troubleshooting

### Certificate Not Found

**Error:** `Unable to configure HTTPS endpoint. No server certificate was specified...`

**Solution:**

```bash
# Ensure certificate exists
ls -la src/HotSwap.Distributed.Api/certificates/aspnetapp.pfx

# If missing, regenerate
./generate-dev-cert.sh
```

### Browser Certificate Warning

**Issue:** Browser shows "Your connection is not private" or "NET::ERR_CERT_AUTHORITY_INVALID"

**Solution:** This is expected for self-signed certificates. You have three options:

1. **Trust the certificate** (recommended for development):
   ```bash
   dotnet dev-certs https --trust
   ```

2. **Bypass the warning** (temporary):
   - Chrome: Type `thisisunsafe` while on the warning page
   - Firefox: Click "Advanced" → "Accept the Risk and Continue"

3. **Use HTTP during development:**
   - Access http://localhost:5000 instead of HTTPS

### Port Already in Use

**Error:** `Address already in use`

**Solution:**

```bash
# Find process using port 5001
sudo lsof -i :5001

# Kill the process
sudo kill -9 <PID>

# Or change the port in appsettings.json
```

### Certificate Password Error

**Error:** `The password used to open the PKCS#12 file is not correct`

**Solution:**

```bash
# Development certificates have no password (empty string)
# Verify the Password field is "" in appsettings.json

# If you set a password during generation, update appsettings.json:
"Certificate": {
  "Path": "certificates/aspnetapp.pfx",
  "Password": "YourPassword"
}
```

### HSTS Issues

**Issue:** Cannot access HTTP endpoint, always redirected to HTTPS

**Cause:** Browser has cached HSTS header from previous session

**Solution:**

1. Clear HSTS settings in browser:
   - **Chrome:** Visit `chrome://net-internals/#hsts`
   - **Firefox:** Clear all site data for localhost
   - **Safari:** Clear all website data

2. Or wait for HSTS max-age to expire (default: 1 year)

3. Or use incognito/private browsing mode

---

## Security Best Practices

### Development

- ✅ Use self-signed certificates (generated by `generate-dev-cert.sh`)
- ✅ Empty certificate password is acceptable
- ✅ HSTS disabled or with short max-age
- ✅ Trust certificate locally for better developer experience

### Production

- ✅ **ALWAYS use certificates from trusted CAs** (Let's Encrypt, DigiCert, etc.)
- ✅ **Use strong certificate passwords** (stored in secrets management)
- ✅ **Enable HSTS with long max-age** (1 year minimum)
- ✅ **Enable HSTS preload** after thorough testing
- ✅ **Disable HTTP or redirect to HTTPS** immediately
- ✅ **Enforce TLS 1.2+ only** (disable TLS 1.0/1.1)
- ✅ **Rotate certificates before expiration**
- ✅ **Monitor certificate expiration** with automated alerts
- ✅ **Use strong cipher suites**

### Certificate Storage

**Development:**
```bash
# Certificates can be in source control if self-signed with no private data
git add src/HotSwap.Distributed.Api/certificates/aspnetapp.pfx
```

**Production:**
```bash
# NEVER commit production certificates to source control
echo "certificates/*.pfx" >> .gitignore
echo "appsettings.Production.json" >> .gitignore

# Store in secure location with restricted permissions
sudo chmod 600 /opt/distributed-kernel/certificates/certificate.pfx
sudo chown www-data:www-data /opt/distributed-kernel/certificates/certificate.pfx
```

### Environment-Specific Configuration

Use environment-specific appsettings files:

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides (relaxed security)
- `appsettings.Staging.json` - Staging overrides
- `appsettings.Production.json` - Production overrides (strict security)

**Example `appsettings.Production.json`:**

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": null,
      "Https": {
        "Url": "https://*:443",
        "Certificate": {
          "Path": "/opt/distributed-kernel/certificates/production.pfx",
          "Password": "${CERT_PASSWORD}"
        }
      }
    }
  },
  "Hsts": {
    "Enabled": true,
    "MaxAge": 63072000,
    "IncludeSubDomains": true,
    "Preload": true
  }
}
```

---

## Testing HTTPS Configuration

### Basic HTTPS Test

```bash
# Test HTTPS endpoint
curl -k https://localhost:5001/health

# Expected output:
# Healthy
```

### Test HSTS Headers

```bash
# Test HSTS header (production only)
curl -I https://yourdomain.com/health

# Expected header (not in development):
# Strict-Transport-Security: max-age=31536000; includeSubDomains
```

### Test TLS Version

```bash
# Test TLS 1.2 (should work)
openssl s_client -connect localhost:5001 -tls1_2

# Test TLS 1.1 (should fail)
openssl s_client -connect localhost:5001 -tls1_1

# Test TLS 1.3 (should work if supported)
openssl s_client -connect localhost:5001 -tls1_3
```

### Test Certificate Details

```bash
# View certificate details
openssl s_client -connect localhost:5001 -showcerts

# Extract and view certificate
echo | openssl s_client -connect localhost:5001 2>/dev/null | openssl x509 -noout -text
```

---

## Integration with Authentication

HTTPS is required for JWT authentication in production. The application automatically enforces this:

**File:** `Program.cs`

```csharp
options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
```

**Behavior:**

- **Development:** JWT works over HTTP (for testing)
- **Production:** JWT requires HTTPS

**Testing with JWT over HTTPS:**

```bash
# 1. Login to get token
curl -k https://localhost:5001/api/v1/authentication/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}'

# 2. Use token in subsequent requests
curl -k https://localhost:5001/api/v1/deployments \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

---

## Related Documentation

- [CLAUDE.md](CLAUDE.md) - Development environment setup
- [JWT_AUTHENTICATION_GUIDE.md](JWT_AUTHENTICATION_GUIDE.md) - JWT authentication
- [TASK_LIST.md](TASK_LIST.md) - Task #15: HTTPS/TLS Configuration
- [PROJECT_STATUS_REPORT.md](PROJECT_STATUS_REPORT.md) - Production readiness

---

## Changelog

### 2025-11-15 - Initial HTTPS Configuration

- ✅ Added Kestrel HTTPS endpoint configuration
- ✅ Added HSTS middleware with configurable settings
- ✅ Created `generate-dev-cert.sh` for development certificates
- ✅ Updated docker-compose.yml for HTTPS support
- ✅ Enforced TLS 1.2+ through .NET 8.0 defaults
- ✅ Created comprehensive HTTPS setup documentation

---

**Status:** ✅ Complete
**Next Steps:** Test deployment with HTTPS, consider Task #16 (Secret Rotation System)
