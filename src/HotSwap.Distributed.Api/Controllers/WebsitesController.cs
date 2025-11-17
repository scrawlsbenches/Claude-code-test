using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotSwap.Distributed.Api.Controllers;

/// <summary>
/// API for managing websites within a tenant.
/// </summary>
[ApiController]
[Route("api/v1/websites")]
[Produces("application/json")]
[Authorize]
public class WebsitesController : ControllerBase
{
    private readonly IWebsiteRepository _websiteRepository;
    private readonly IWebsiteProvisioningService _provisioningService;
    private readonly ITenantContextService _tenantContext;
    private readonly ILogger<WebsitesController> _logger;

    public WebsitesController(
        IWebsiteRepository websiteRepository,
        IWebsiteProvisioningService provisioningService,
        ITenantContextService tenantContext,
        ILogger<WebsitesController> logger)
    {
        _websiteRepository = websiteRepository;
        _provisioningService = provisioningService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new website for the current tenant.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WebsiteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWebsite(
        [FromBody] CreateWebsiteRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == null)
            return BadRequest(new ErrorResponse { Error = "Tenant context required" });

        var website = new Website
        {
            TenantId = tenantId.Value,
            Name = request.Name,
            Subdomain = request.Subdomain.ToLowerInvariant(),
            CustomDomains = request.CustomDomains ?? new List<string>(),
            CurrentThemeId = request.ThemeId ?? Guid.Empty
        };

        website = await _provisioningService.ProvisionWebsiteAsync(website, cancellationToken);

        var response = MapToWebsiteResponse(website);
        return CreatedAtAction(nameof(GetWebsite), new { websiteId = website.WebsiteId }, response);
    }

    /// <summary>
    /// Gets a website by ID.
    /// </summary>
    [HttpGet("{websiteId}")]
    [ProducesResponseType(typeof(WebsiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWebsite(
        Guid websiteId,
        CancellationToken cancellationToken)
    {
        var website = await _websiteRepository.GetByIdAsync(websiteId, cancellationToken);
        if (website == null)
            return NotFound(new ErrorResponse { Error = $"Website {websiteId} not found" });

        return Ok(MapToWebsiteResponse(website));
    }

    /// <summary>
    /// Lists all websites for the current tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<WebsiteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListWebsites(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == null)
            return BadRequest(new ErrorResponse { Error = "Tenant context required" });

        var websites = await _websiteRepository.GetByTenantIdAsync(tenantId.Value, cancellationToken: cancellationToken);
        var responses = websites.Select(MapToWebsiteResponse).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Deletes a website.
    /// </summary>
    [HttpDelete("{websiteId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWebsite(
        Guid websiteId,
        CancellationToken cancellationToken)
    {
        var success = await _provisioningService.DeprovisionWebsiteAsync(websiteId, cancellationToken);
        if (!success)
            return NotFound(new ErrorResponse { Error = $"Website {websiteId} not found" });

        return Ok(new { message = "Website deleted successfully" });
    }

    private static WebsiteResponse MapToWebsiteResponse(Website website)
    {
        return new WebsiteResponse
        {
            WebsiteId = website.WebsiteId,
            Name = website.Name,
            Subdomain = website.Subdomain,
            CustomDomains = website.CustomDomains,
            Status = website.Status.ToString(),
            CurrentThemeId = website.CurrentThemeId,
            CreatedAt = website.CreatedAt
        };
    }
}
