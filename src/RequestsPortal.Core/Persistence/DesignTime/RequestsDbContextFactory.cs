using Microsoft.EntityFrameworkCore.Design;

namespace RequestsPortal.Core.Persistence.DesignTime;

public sealed class RequestsDbContextFactory : IDesignTimeDbContextFactory<RequestsDbContext>
{
    public RequestsDbContext CreateDbContext(string[] args)
    {
        return new RequestsDatabase().CreateContext();
    }
}
