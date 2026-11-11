using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RequestsPortal.Core.Configuration;

namespace RequestsPortal.Core.Persistence.DesignTime;

public sealed class RequestsDbContextFactory : IDesignTimeDbContextFactory<RequestsDbContext>
{
    public RequestsDbContext CreateDbContext(string[] args)
    {
        var dataDirectory = PortalPaths.ResolveDataDirectory();
        Directory.CreateDirectory(dataDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "requests.db")
        }.ToString();

        var options = new DbContextOptionsBuilder<RequestsDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new RequestsDbContext(options);
    }
}
