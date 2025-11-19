using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// Platform admin API for tenant management (requires Admin role).
/// </summary>
[ApiController]
[Route("api/tenants")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class TenantsController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantProvisioningService _provisioningService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantRepository tenantRepository,
        ITenantProvisioningService provisioningService,
        ILogger<TenantsController> logger)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _provisioningService = provisioningService ?? throw new ArgumentNullException(nameof(provisioningService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new tenant with resource provisioning.
    /// </summary>
    /// <param name="request">Tenant creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant information</returns>
    /// <response code="201">Tenant created successfully</response>
    /// <response code="400">Invalid request or subdomain already exists</response>
    /// <response code="500">Provisioning failed</response>
    [HttpPost]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating tenant: {Name} (Subdomain: {Subdomain})",
                request.Name, request.Subdomain);

            // Check subdomain availability
            var isAvailable = await _tenantRepository.IsSubdomainAvailableAsync(
                request.Subdomain, cancellationToken);

            if (!isAvailable)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = $"Subdomain '{request.Subdomain}' is already taken"
                });
            }

            // Create tenant entity
            var tenant = new Tenant
            {
                TenantId = Guid.NewGuid(),
                Name = request.Name,
                Subdomain = request.Subdomain.ToLowerInvariant(),
                CustomDomain = request.CustomDomain,
                Tier = request.Tier,
                ContactEmail = request.ContactEmail,
                ResourceQuota = ResourceQuota.CreateDefault(request.Tier),
                Metadata = request.Metadata ?? new Dictionary<string, string>()
            };

            // Provision tenant resources
            tenant = await _provisioningService.ProvisionTenantAsync(tenant, cancellationToken);

            var response = MapToTenantResponse(tenant);

            return CreatedAtAction(
                nameof(GetTenant),
                new { tenantId = tenant.TenantId },
                response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tenant: {Name}", request.Name);
            return StatusCode(500, new ErrorResponse
            {
                Error = "Tenant creation failed",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant information</returns>
    /// <response code="200">Tenant found</response>
    /// <response code="404">Tenant not found</response>
    [HttpGet("{tenantId}")]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);

        if (tenant == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Tenant {tenantId} not found"
            });
        }

        return Ok(MapToTenantResponse(tenant));
    }

    /// <summary>
    /// Lists all tenants with optional status filtering.
    /// </summary>
    /// <param name="status">Optional status filter (Active, Suspended, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenants</returns>
    /// <response code="200">Tenants retrieved</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<TenantResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTenants(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        TenantStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<TenantStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var tenants = await _tenantRepository.GetAllAsync(statusFilter, cancellationToken);

        var responses = tenants.Select(t => MapToTenantResponse(t)).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Updates a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant</returns>
    /// <response code="200">Tenant updated</response>
    /// <response code="404">Tenant not found</response>
    [HttpPut("{tenantId}")]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenant(
        Guid tenantId,
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);

        if (tenant == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Tenant {tenantId} not found"
            });
        }

        // Update fields
        if (!string.IsNullOrWhiteSpace(request.Name))
            tenant.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.ContactEmail))
            tenant.ContactEmail = request.ContactEmail;

        if (request.CustomDomain != null)
            tenant.CustomDomain = request.CustomDomain;

        if (request.Metadata != null)
        {
            foreach (var kvp in request.Metadata)
            {
                tenant.Metadata[kvp.Key] = kvp.Value;
            }
        }

        tenant = await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        return Ok(MapToTenantResponse(tenant));
    }

    /// <summary>
    /// Suspends a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant information</returns>
    /// <response code="200">Tenant suspended</response>
    /// <response code="404">Tenant not found</response>
    [HttpPost("{tenantId}/suspend")]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendTenant(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var success = await _provisioningService.SuspendTenantAsync(
            tenantId, "Suspended by administrator", cancellationToken);

        if (!success)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Tenant {tenantId} not found"
            });
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        return Ok(MapToTenantResponse(tenant!));
    }

    /// <summary>
    /// Activates a suspended tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant information</returns>
    /// <response code="200">Tenant activated</response>
    /// <response code="404">Tenant not found</response>
    [HttpPost("{tenantId}/activate")]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateTenant(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var success = await _provisioningService.ActivateTenantAsync(tenantId, cancellationToken);

        if (!success)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Tenant {tenantId} not found"
            });
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        return Ok(MapToTenantResponse(tenant!));
    }

    /// <summary>
    /// Updates a tenant's subscription tier.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="request">Subscription update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant information</returns>
    /// <response code="200">Subscription updated</response>
    /// <response code="404">Tenant not found</response>
    [HttpPut("{tenantId}/subscription")]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSubscription(
        Guid tenantId,
        [FromBody] UpdateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);

        if (tenant == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Tenant {tenantId} not found"
            });
        }

        // Update the tier
        tenant.Tier = request.Tier;

        // Update resource quota based on new tier
        tenant.ResourceQuota = ResourceQuota.CreateDefault(request.Tier);

        tenant = await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        return Ok(MapToTenantResponse(tenant));
    }

    /// <summary>
    /// Deletes a tenant (soft delete).
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    /// <response code="200">Tenant deleted</response>
    /// <response code="404">Tenant not found</response>
    [HttpDelete("{tenantId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTenant(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var success = await _tenantRepository.DeleteAsync(tenantId, cancellationToken);

        if (!success)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Tenant {tenantId} not found"
            });
        }

        return Ok(new { message = "Tenant deleted successfully" });
    }

    private static TenantResponse MapToTenantResponse(Tenant tenant)
    {
        return new TenantResponse
        {
            TenantId = tenant.TenantId,
            Name = tenant.Name,
            Subdomain = tenant.Subdomain,
            CustomDomain = tenant.CustomDomain,
            Status = tenant.Status.ToString(),
            Tier = tenant.Tier.ToString(),
            ResourceQuota = new ResourceQuotaResponse
            {
                MaxWebsites = tenant.ResourceQuota.MaxWebsites,
                StorageQuotaGB = tenant.ResourceQuota.StorageQuotaGB,
                BandwidthQuotaGB = tenant.ResourceQuota.BandwidthQuotaGB,
                MaxConcurrentDeployments = tenant.ResourceQuota.MaxConcurrentDeployments,
                MaxCustomDomains = tenant.ResourceQuota.MaxCustomDomains
            },
            CreatedAt = tenant.CreatedAt,
            SuspendedAt = tenant.SuspendedAt,
            ContactEmail = tenant.ContactEmail ?? string.Empty,
            Metadata = tenant.Metadata
        };
    }
}
