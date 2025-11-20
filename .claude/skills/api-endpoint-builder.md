# API Endpoint Builder Skill

**Version:** 1.0.0
**Last Updated:** 2025-11-20
**Skill Type:** Core Development
**Estimated Time:** 1-2 hours per endpoint set
**Complexity:** Medium

---

## Purpose

Systematic scaffolding of REST API controllers with proper routing, validation, authorization, and documentation.

**Use this skill when:**
- Creating new API controllers
- Adding CRUD endpoints
- Implementing multi-tenant endpoints (Task #22)
- Need RESTful API best practices

**This skill addresses:**
- Task #22: Multi-tenant API endpoints (3-4 days)
- Any new API endpoint requirements

---

## Phase 1: Controller Scaffolding

### Step 1.1: Create Controller Class

```csharp
// src/HotSwap.Distributed.Api/Controllers/TenantsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize] // All endpoints require authentication
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantService tenantService,
        ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    // Endpoints will go here
}
```

### Step 1.2: Add GET (List) Endpoint

```csharp
/// <summary>
/// Gets all tenants
/// </summary>
/// <returns>List of tenants</returns>
/// <response code="200">Returns the list of tenants</response>
/// <response code="401">If not authenticated</response>
[HttpGet]
[ProducesResponseType(typeof(IEnumerable<TenantResponse>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<ActionResult<IEnumerable<TenantResponse>>> GetAll(
    CancellationToken cancellationToken)
{
    _logger.LogInformation("Getting all tenants");

    var tenants = await _tenantService.GetAllTenantsAsync(cancellationToken);

    return Ok(tenants.Select(t => new TenantResponse
    {
        Id = t.Id,
        Name = t.Name,
        Status = t.Status.ToString(),
        CreatedAt = t.CreatedAt
    }));
}
```

### Step 1.3: Add GET (Single) Endpoint

```csharp
/// <summary>
/// Gets a tenant by ID
/// </summary>
/// <param name="id">Tenant ID</param>
/// <response code="200">Returns the tenant</response>
/// <response code="404">If tenant not found</response>
[HttpGet("{id}")]
[ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<TenantResponse>> GetById(
    [FromRoute] string id,
    CancellationToken cancellationToken)
{
    var tenant = await _tenantService.GetTenantByIdAsync(id, cancellationToken);

    if (tenant == null)
    {
        return NotFound(new ErrorResponse
        {
            Message = $"Tenant {id} not found",
            ErrorCode = "TENANT_NOT_FOUND"
        });
    }

    return Ok(new TenantResponse
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Status = tenant.Status.ToString(),
        CreatedAt = tenant.CreatedAt
    });
}
```

### Step 1.4: Add POST (Create) Endpoint

```csharp
/// <summary>
/// Creates a new tenant
/// </summary>
/// <param name="request">Tenant creation request</param>
/// <response code="201">Tenant created successfully</response>
/// <response code="400">If request is invalid</response>
/// <response code="403">If not authorized (Admin-only)</response>
[HttpPost]
[Authorize(Roles = "Admin")] // Admin-only
[ProducesResponseType(typeof(TenantResponse), StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public async Task<ActionResult<TenantResponse>> Create(
    [FromBody] CreateTenantRequest request,
    CancellationToken cancellationToken)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    _logger.LogInformation("Creating tenant: {TenantName}", request.Name);

    var tenant = await _tenantService.CreateTenantAsync(
        request.Name,
        request.Configuration,
        cancellationToken);

    return CreatedAtAction(
        nameof(GetById),
        new { id = tenant.Id },
        new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Status = tenant.Status.ToString(),
            CreatedAt = tenant.CreatedAt
        });
}
```

### Step 1.5: Add PUT (Update) Endpoint

```csharp
/// <summary>
/// Updates a tenant
/// </summary>
/// <param name="id">Tenant ID</param>
/// <param name="request">Update request</param>
/// <response code="200">Tenant updated</response>
/// <response code="404">If tenant not found</response>
[HttpPut("{id}")]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<TenantResponse>> Update(
    [FromRoute] string id,
    [FromBody] UpdateTenantRequest request,
    CancellationToken cancellationToken)
{
    var tenant = await _tenantService.UpdateTenantAsync(
        id,
        request.Name,
        request.Configuration,
        cancellationToken);

    if (tenant == null)
    {
        return NotFound();
    }

    return Ok(new TenantResponse
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Status = tenant.Status.ToString(),
        CreatedAt = tenant.CreatedAt
    });
}
```

### Step 1.6: Add DELETE Endpoint

```csharp
/// <summary>
/// Deletes a tenant
/// </summary>
/// <param name="id">Tenant ID</param>
/// <response code="204">Tenant deleted</response>
/// <response code="404">If tenant not found</response>
[HttpDelete("{id}")]
[Authorize(Roles = "Admin")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Delete(
    [FromRoute] string id,
    CancellationToken cancellationToken)
{
    var deleted = await _tenantService.DeleteTenantAsync(id, cancellationToken);

    if (!deleted)
    {
        return NotFound();
    }

    return NoContent();
}
```

---

## Phase 2: Request/Response Models

### Step 2.1: Create Request DTOs

```csharp
// src/HotSwap.Distributed.Api/Models/CreateTenantRequest.cs
using System.ComponentModel.DataAnnotations;

public class CreateTenantRequest
{
    /// <summary>
    /// Tenant name (unique)
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Tenant configuration (JSON)
    /// </summary>
    public Dictionary<string, string>? Configuration { get; set; }
}

public class UpdateTenantRequest
{
    [StringLength(100, MinimumLength = 3)]
    public string? Name { get; set; }

    public Dictionary<string, string>? Configuration { get; set; }
}
```

### Step 2.2: Create Response DTOs

```csharp
// src/HotSwap.Distributed.Api/Models/TenantResponse.cs
public class TenantResponse
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

---

## Phase 3: Integration Tests

### Step 3.1: Create Test Class

```csharp
// tests/HotSwap.Distributed.IntegrationTests/TenantsControllerTests.cs
public class TenantsControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly string _adminToken;

    public TenantsControllerTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
        _adminToken = AuthHelper.GetAdminToken(_client).Result;
    }

    [Fact]
    public async Task GetAllTenants_ReturnsOk()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/v1/tenants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenants = await response.Content.ReadFromJsonAsync<List<TenantResponse>>();
        tenants.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateTenant_WithAdminRole_Returns201()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _adminToken);

        var request = new CreateTenantRequest
        {
            Name = "Test Tenant",
            Configuration = new Dictionary<string, string>
            {
                ["MaxUsers"] = "100"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        tenant.Should().NotBeNull();
        tenant.Name.Should().Be("Test Tenant");
    }

    [Fact]
    public async Task CreateTenant_WithViewerRole_Returns403()
    {
        // Arrange
        var viewerToken = await AuthHelper.GetViewerToken(_client);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", viewerToken);

        var request = new CreateTenantRequest { Name = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

---

## Quick Reference: RESTful Conventions

```
GET    /api/v1/tenants          → 200 OK (list)
GET    /api/v1/tenants/{id}     → 200 OK (single) or 404 Not Found
POST   /api/v1/tenants          → 201 Created (with Location header)
PUT    /api/v1/tenants/{id}     → 200 OK or 404 Not Found
DELETE /api/v1/tenants/{id}     → 204 No Content or 404 Not Found

Special actions:
POST   /api/v1/tenants/{id}/suspend   → 200 OK
POST   /api/v1/tenants/{id}/activate  → 200 OK
```

---

## Success Criteria

- [ ] Controller created with all CRUD endpoints
- [ ] Request/Response models with validation
- [ ] XML documentation comments added
- [ ] Authorization configured (roles)
- [ ] Integration tests written and passing
- [ ] Swagger UI shows endpoints correctly
- [ ] Task #22 integration tests un-skipped

---

**Last Updated:** 2025-11-20
**Version:** 1.0.0
**Completes:** Task #22 (Multi-Tenant API)
