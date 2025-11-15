# JWT Authentication & Authorization Guide

**Status**: ✅ Implemented
**Date**: 2025-11-15
**Version**: 1.0

---

## Overview

The Distributed Kernel Orchestration API now includes comprehensive JWT (JSON Web Token) bearer authentication with role-based access control (RBAC). All API endpoints are protected and require valid authentication tokens.

## Table of Contents

1. [Authentication Flow](#authentication-flow)
2. [User Roles](#user-roles)
3. [Demo Credentials](#demo-credentials)
4. [API Endpoints](#api-endpoints)
5. [Using Swagger UI](#using-swagger-ui)
6. [Using cURL](#using-curl)
7. [Using C# HttpClient](#using-c-httpclient)
8. [Configuration](#configuration)
9. [Production Deployment](#production-deployment)
10. [Troubleshooting](#troubleshooting)

---

## Authentication Flow

### 1. Login

Send a POST request to `/api/v1/authentication/login` with username and password:

```json
POST /api/v1/authentication/login
Content-Type: application/json

{
  "username": "admin",
  "password": "Admin123!"
}
```

### 2. Receive JWT Token

The API returns a JWT token and user information:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-11-15T14:30:00Z",
  "user": {
    "id": "00000000-0000-0000-0000-000000000001",
    "username": "admin",
    "email": "admin@example.com",
    "fullName": "System Administrator",
    "roles": ["Admin", "Deployer", "Viewer"]
  }
}
```

### 3. Use Token for API Requests

Include the token in the `Authorization` header for all subsequent requests:

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## User Roles

The system implements three user roles with hierarchical permissions:

### Viewer (Read-Only Access)
**Permissions:**
- View deployment status and history
- View cluster health and metrics
- View pending approvals
- View API documentation

**Cannot:**
- Create or modify deployments
- Approve or reject deployments
- Manage users

### Deployer (Deployment Management)
**Permissions:**
- All Viewer permissions
- Create new deployments
- Rollback deployments
- View deployment execution details

**Cannot:**
- Approve or reject deployments (requires Admin)
- Manage users

### Admin (Full Access)
**Permissions:**
- All Deployer permissions
- Approve or reject deployment approvals
- Manage approval workflows
- Full system administration
- User management (future feature)

---

## Demo Credentials

The system includes three pre-configured demo users for testing:

| Username | Password | Roles | Description |
|----------|----------|-------|-------------|
| `admin` | `Admin123!` | Admin, Deployer, Viewer | Full administrative access |
| `deployer` | `Deploy123!` | Deployer, Viewer | Can create and manage deployments |
| `viewer` | `Viewer123!` | Viewer | Read-only access |

**⚠️ WARNING**: These demo credentials are for development/testing only. Replace with secure user management before production deployment.

To get demo credentials programmatically:

```http
GET /api/v1/authentication/demo-credentials
```

---

## API Endpoints

### Authentication Endpoints

#### Login
```http
POST /api/v1/authentication/login
Content-Type: application/json

{
  "username": "string",
  "password": "string"
}
```

**Response:**
- `200 OK` - Authentication successful, returns JWT token
- `400 Bad Request` - Invalid request format
- `401 Unauthorized` - Invalid credentials

#### Get Current User
```http
GET /api/v1/authentication/me
Authorization: Bearer {token}
```

**Response:**
- `200 OK` - Returns current user information
- `401 Unauthorized` - Invalid or missing token

#### Get Demo Credentials
```http
GET /api/v1/authentication/demo-credentials
```

**Response:**
- `200 OK` - Returns list of demo users (development only)
- `403 Forbidden` - Production environment

### Protected API Endpoints

All deployment, approval, and cluster endpoints require authentication:

#### Deployments (Requires: Deployer or Admin)
- `POST /api/v1/deployments` - Create deployment
- `POST /api/v1/deployments/{id}/rollback` - Rollback deployment

#### Deployments - Read Only (Requires: Viewer, Deployer, or Admin)
- `GET /api/v1/deployments` - List deployments
- `GET /api/v1/deployments/{id}` - Get deployment status

#### Approvals - Manage (Requires: Admin)
- `POST /api/v1/approvals/deployments/{id}/approve` - Approve deployment
- `POST /api/v1/approvals/deployments/{id}/reject` - Reject deployment

#### Approvals - Read Only (Requires: Viewer, Deployer, or Admin)
- `GET /api/v1/approvals/pending` - Get pending approvals
- `GET /api/v1/approvals/deployments/{id}` - Get approval details

#### Clusters (Requires: Viewer, Deployer, or Admin)
- `GET /api/v1/clusters` - List all clusters
- `GET /api/v1/clusters/{environment}` - Get cluster details
- `GET /api/v1/clusters/{environment}/metrics` - Get cluster metrics

---

## Using Swagger UI

### 1. Navigate to Swagger UI

Open your browser and go to:
```
http://localhost:5000/swagger
```

### 2. Get Authentication Token

1. Expand the **Authentication** section
2. Click on `POST /api/v1/authentication/login`
3. Click **Try it out**
4. Enter credentials (e.g., `admin` / `Admin123!`)
5. Click **Execute**
6. Copy the `token` value from the response

### 3. Authorize Swagger UI

1. Click the **Authorize** button at the top of Swagger UI
2. In the dialog, enter: `Bearer {your-token-here}`
   - Example: `Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
3. Click **Authorize**
4. Click **Close**

### 4. Make Authenticated Requests

All subsequent requests from Swagger UI will include the authorization token automatically.

---

## Using cURL

### Login
```bash
# Login and save token
curl -X POST http://localhost:5000/api/v1/authentication/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}'

# Response:
# {
#   "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
#   "expiresAt": "2025-11-15T14:30:00Z",
#   ...
# }
```

### Use Token
```bash
# Store token in variable
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# List deployments
curl -X GET http://localhost:5000/api/v1/deployments \
  -H "Authorization: Bearer $TOKEN"

# Create deployment
curl -X POST http://localhost:5000/api/v1/deployments \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "moduleName": "network_filter",
    "version": "1.0.0",
    "targetEnvironment": "Staging",
    "requesterEmail": "admin@example.com"
  }'
```

---

## Using C# HttpClient

```csharp
using System.Net.Http.Headers;
using System.Text.Json;

// Login
var loginRequest = new
{
    Username = "admin",
    Password = "Admin123!"
};

var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

var loginResponse = await httpClient.PostAsJsonAsync(
    "/api/v1/authentication/login",
    loginRequest);

loginResponse.EnsureSuccessStatusCode();

var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResponse>();
var token = authResponse.Token;

// Set default authorization header
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

// Make authenticated requests
var deploymentsResponse = await httpClient.GetAsync("/api/v1/deployments");
deploymentsResponse.EnsureSuccessStatusCode();

var deployments = await deploymentsResponse.Content.ReadAsStringAsync();
Console.WriteLine(deployments);
```

---

## Configuration

### JWT Settings

Configure JWT settings in `appsettings.json` or environment variables:

```json
{
  "Jwt": {
    "SecretKey": "YourSecretKey-MinimumLength32Characters-ChangeInProduction",
    "Issuer": "DistributedKernelOrchestrator",
    "Audience": "DistributedKernelApi",
    "ExpirationMinutes": 60
  }
}
```

### Environment Variables

```bash
# Linux/macOS
export Jwt__SecretKey="YourSecretKey-MinimumLength32Characters"
export Jwt__ExpirationMinutes="120"

# Windows (PowerShell)
$env:Jwt__SecretKey="YourSecretKey-MinimumLength32Characters"
$env:Jwt__ExpirationMinutes="120"

# Docker
docker run -e Jwt__SecretKey="..." -e Jwt__ExpirationMinutes="60" ...
```

### Security Requirements

**Secret Key:**
- **Minimum length**: 32 characters (256 bits)
- **Recommendation**: 64+ characters with high entropy
- **Storage**: Store in Azure Key Vault, HashiCorp Vault, or similar secrets management system
- **Rotation**: Rotate keys periodically (every 90 days recommended)

**Token Expiration:**
- **Development**: 60-120 minutes
- **Production**: 15-30 minutes (with refresh token mechanism)

---

## Production Deployment

### Security Checklist

- [ ] **Replace demo users** - Implement database-backed user management
- [ ] **Change JWT secret key** - Use cryptographically secure random key (64+ characters)
- [ ] **Enable HTTPS** - All authentication must use HTTPS in production
- [ ] **Store secrets securely** - Use Azure Key Vault, AWS Secrets Manager, etc.
- [ ] **Implement token refresh** - Add refresh token mechanism for better security
- [ ] **Add rate limiting** - Limit login attempts to prevent brute force attacks
- [ ] **Enable audit logging** - Log all authentication and authorization events
- [ ] **Implement password policies** - Enforce strong password requirements
- [ ] **Add multi-factor authentication (MFA)** - For admin accounts at minimum

### Replacing In-Memory User Store

The current implementation uses `InMemoryUserRepository` for simplicity. In production, replace with a database-backed implementation:

```csharp
// Program.cs - Replace this line:
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

// With database implementation:
builder.Services.AddScoped<IUserRepository, DatabaseUserRepository>();
```

Create `DatabaseUserRepository` implementing `IUserRepository` with:
- Entity Framework Core + PostgreSQL/SQL Server
- Proper password hashing (BCrypt already implemented)
- User management CRUD operations
- Role assignment

### Docker Deployment

```yaml
# docker-compose.yml
services:
  orchestrator-api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Jwt__SecretKey=${JWT_SECRET_KEY}  # From env file or secrets
      - Jwt__ExpirationMinutes=30
      - ASPNETCORE_URLS=https://+:443;http://+:80
      - ASPNETCORE_HTTPS_PORT=443
```

---

## Troubleshooting

### 401 Unauthorized - Missing Token

**Error:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

**Solutions:**
- Ensure you're including the `Authorization` header
- Format must be: `Authorization: Bearer {token}`
- Include the word "Bearer" followed by a space and the token

### 401 Unauthorized - Invalid Token

**Possible causes:**
- Token has expired (check `expiresAt` from login response)
- Token was generated with different secret key
- Token is malformed or tampered with

**Solutions:**
- Login again to get a new token
- Check token expiration time
- Verify JWT secret key configuration matches across restarts

### 403 Forbidden - Insufficient Permissions

**Error:**
```json
{
  "error": "Forbidden",
  "message": "User does not have the required role"
}
```

**Solutions:**
- Check endpoint required roles (documented in Swagger)
- Login with a user that has appropriate roles
- Viewers cannot create deployments (need Deployer or Admin)
- Only Admins can approve/reject deployments

### Token Appears in Logs

**Security Risk**: Tokens should never appear in logs

**Solutions:**
- Check Serilog configuration to exclude Authorization headers
- Review logging middleware to sanitize sensitive data
- Ensure debug logging is disabled in production

### "Secret key must be at least 32 characters" Error

**Error on startup:**
```
ArgumentException: JWT SecretKey must be at least 32 characters long
```

**Solutions:**
- Update `appsettings.json` with a longer secret key
- Set `Jwt__SecretKey` environment variable with 32+ character value
- Use cryptographically random key generation:
  ```bash
  # Generate secure 64-character key (Linux/macOS)
  openssl rand -base64 48

  # Or using PowerShell
  -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | % {[char]$_})
  ```

### Demo Credentials Not Working in Production

**Error 403 when accessing `/api/v1/authentication/demo-credentials`:**

This is expected behavior. Demo credentials endpoint is disabled in production for security.

**Solutions:**
- Use the hardcoded demo credentials directly (for testing only)
- Implement proper user management for production
- Store user credentials in secure database

---

## Additional Resources

- [JWT.io](https://jwt.io/) - Decode and verify JWT tokens
- [JWT RFC 7519](https://tools.ietf.org/html/rfc7519) - JWT specification
- [OAuth 2.0 RFC 6749](https://tools.ietf.org/html/rfc6749) - OAuth 2.0 framework
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)

---

## Support

For issues or questions regarding authentication:

1. Check this guide first
2. Review Swagger API documentation at `/swagger`
3. Check application logs for detailed error messages
4. Open an issue on GitHub with:
   - Error message
   - Steps to reproduce
   - Environment (Development/Production)
   - Token details (DO NOT share actual tokens!)

---

**Last Updated:** 2025-11-15
**Next Review:** After production deployment
