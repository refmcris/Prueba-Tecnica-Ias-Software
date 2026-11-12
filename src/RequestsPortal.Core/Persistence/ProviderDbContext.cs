using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Persistence.Configurations;

namespace RequestsPortal.Core.Persistence;

public sealed class ProviderDbContext : DbContext
{
    public ProviderDbContext(DbContextOptions<ProviderDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProviderOperation> Operations => Set<ProviderOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ProviderOperationConfiguration());
    }
}
