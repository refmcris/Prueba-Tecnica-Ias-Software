using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Persistence.Configurations;

namespace RequestsPortal.Core.Persistence;

public sealed class RequestsDbContext : DbContext
{
    public RequestsDbContext(DbContextOptions<RequestsDbContext> options)
        : base(options)
    {
    }

    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ServiceRequestConfiguration());
    }
}
