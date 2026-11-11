using Microsoft.EntityFrameworkCore.Design;

namespace RequestsPortal.Core.Persistence.DesignTime;

public sealed class ProviderDbContextFactory : IDesignTimeDbContextFactory<ProviderDbContext>
{
    public ProviderDbContext CreateDbContext(string[] args)
    {
        return new ProviderDatabase().CreateContext();
    }
}
