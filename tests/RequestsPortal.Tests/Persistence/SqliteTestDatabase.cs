using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Persistence;

namespace RequestsPortal.Tests.Persistence;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly DirectoryInfo directory;
    private readonly DbContextOptions<RequestsDbContext> options;

    private SqliteTestDatabase()
    {
        directory = Directory.CreateTempSubdirectory("requests-persistence-");

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory.FullName, "requests.db"),
            Pooling = false
        }.ToString();

        options = new DbContextOptionsBuilder<RequestsDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var database = new SqliteTestDatabase();

        try
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public RequestsDbContext CreateContext()
    {
        return new RequestsDbContext(options);
    }

    public ValueTask DisposeAsync()
    {
        directory.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }
}
