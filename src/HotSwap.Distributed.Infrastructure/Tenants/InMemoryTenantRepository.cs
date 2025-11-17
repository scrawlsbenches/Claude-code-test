using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Tenants;

/// <summary>
/// In-memory implementation of tenant repository for development and testing.
/// In production, replace with a database-backed implementation.
/// </summary>
public class InMemoryTenantRepository : ITenantRepository
{
    private readonly Dictionary<Guid, Tenant> _tenants = new();
    private readonly ILogger<InMemoryTenantRepository> _logger;
    private readonly object _lock = new();

    public InMemoryTenantRepository(ILogger<InMemoryTenantRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogWarning("Using in-memory tenant repository. Replace with database-backed repository for production use.");
    }

    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _tenants.TryGetValue(tenantId, out var tenant);
            return Task.FromResult(tenant);
        }
    }

    public Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
            return Task.FromResult<Tenant?>(null);

        lock (_lock)
        {
            var tenant = _tenants.Values.FirstOrDefault(t =>
                t.Subdomain.Equals(subdomain, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(tenant);
        }
    }

    public Task<List<Tenant>> GetAllAsync(TenantStatus? status = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var tenants = status.HasValue
                ? _tenants.Values.Where(t => t.Status == status.Value).ToList()
                : _tenants.Values.ToList();
            return Task.FromResult(tenants);
        }
    }

    public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (tenant == null)
            throw new ArgumentNullException(nameof(tenant));

        lock (_lock)
        {
            if (tenant.TenantId == Guid.Empty)
                tenant.TenantId = Guid.NewGuid();

            // Check subdomain uniqueness
            if (_tenants.Values.Any(t => t.Subdomain.Equals(tenant.Subdomain, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Tenant with subdomain '{tenant.Subdomain}' already exists");

            tenant.CreatedAt = DateTime.UtcNow;
            _tenants[tenant.TenantId] = tenant;

            _logger.LogInformation("Created tenant: {TenantName} (ID: {TenantId}, Subdomain: {Subdomain})",
                tenant.Name, tenant.TenantId, tenant.Subdomain);

            return Task.FromResult(tenant);
        }
    }

    public Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (tenant == null)
            throw new ArgumentNullException(nameof(tenant));

        lock (_lock)
        {
            if (!_tenants.ContainsKey(tenant.TenantId))
                throw new KeyNotFoundException($"Tenant with ID {tenant.TenantId} not found");

            _tenants[tenant.TenantId] = tenant;

            _logger.LogInformation("Updated tenant: {TenantName} (ID: {TenantId})",
                tenant.Name, tenant.TenantId);

            return Task.FromResult(tenant);
        }
    }

    public Task<bool> DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_tenants.TryGetValue(tenantId, out var tenant))
                return Task.FromResult(false);

            // Soft delete
            tenant.Status = TenantStatus.Deleted;
            tenant.DeletedAt = DateTime.UtcNow;

            _logger.LogInformation("Soft deleted tenant: {TenantName} (ID: {TenantId})",
                tenant.Name, tenant.TenantId);

            return Task.FromResult(true);
        }
    }

    public Task<bool> IsSubdomainAvailableAsync(string subdomain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
            return Task.FromResult(false);

        lock (_lock)
        {
            var exists = _tenants.Values.Any(t =>
                t.Subdomain.Equals(subdomain, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(!exists);
        }
    }

    public Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // In-memory implementation doesn't support transactions
        // Return a no-op disposable
        return Task.FromResult<IAsyncDisposable>(new NoOpTransaction());
    }

    private class NoOpTransaction : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
