using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Configuration;

namespace RequestsPortal.Core.Persistence;

public sealed class ProviderDatabase
{
    private readonly DbContextOptions<ProviderDbContext> options;

    public ProviderDatabase(string? configuredPath = null)
    {
        var dataDirectory = PortalPaths.ResolveDataDirectory(configuredPath);
        Directory.CreateDirectory(dataDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "provider.db"),
            Pooling = false
        }.ToString();

        options = new DbContextOptionsBuilder<ProviderDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    public ProviderDbContext CreateContext()
    {
        return new ProviderDbContext(options);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
