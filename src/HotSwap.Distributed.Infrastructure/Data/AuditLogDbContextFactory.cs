using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotSwap.Distributed.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating AuditLogDbContext instances during EF Core migrations.
/// This enables migrations to be generated without running the full application.
/// </summary>
public class AuditLogDbContextFactory : IDesignTimeDbContextFactory<AuditLogDbContext>
{
    public AuditLogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuditLogDbContext>();

        // Use a default connection string for design-time operations
        // This is only used for generating migrations, not at runtime
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=hotswap_audit;Username=postgres;Password=postgres",
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("HotSwap.Distributed.Infrastructure");
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "audit");
            });

        return new AuditLogDbContext(optionsBuilder.Options);
    }
}
