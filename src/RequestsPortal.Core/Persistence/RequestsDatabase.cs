using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Configuration;

namespace RequestsPortal.Core.Persistence;

public sealed class RequestsDatabase
{
    private readonly DbContextOptions<RequestsDbContext> options;

    public RequestsDatabase(string? configuredPath = null)
    {
        var dataDirectory = PortalPaths.ResolveDataDirectory(configuredPath);
        Directory.CreateDirectory(dataDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "requests.db"),
            Pooling = false
        }.ToString();

        options = new DbContextOptionsBuilder<RequestsDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    public RequestsDbContext CreateContext()
    {
        return new RequestsDbContext(options);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
