using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Options;
using RequestsPortal.Core.Persistence;
using RequestsPortal.Core.Providers;

namespace RequestsPortal.Tests.Providers;

internal sealed class ProviderTestDatabase : IAsyncDisposable
{
    private readonly DirectoryInfo directory;

    private ProviderTestDatabase()
    {
        directory = Directory.CreateTempSubdirectory("provider-tests-");
        Database = new ProviderDatabase(directory.FullName);
    }

    public ProviderDatabase Database { get; }

    public string DataDirectory => directory.FullName;

    public static async Task<ProviderTestDatabase> CreateAsync(string? targetMigration = null)
    {
        var database = new ProviderTestDatabase();

        try
        {
            if (targetMigration is null)
            {
                await database.Database.InitializeAsync();
            }
            else
            {
                await using var context = database.Database.CreateContext();
                await context.GetService<IMigrator>().MigrateAsync(targetMigration);
            }
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public SimulatedProvider CreateProvider(ProviderOptions? options = null, TimeProvider? clock = null)
    {
        return new SimulatedProvider(
            new ProviderDatabase(directory.FullName),
            options ?? new ProviderOptions { DelaySeconds = 0 },
            clock ?? TimeProvider.System);
    }

    public async Task<ProviderOperation?> ReadOperationAsync(Guid requestId)
    {
        await using var context = Database.CreateContext();
        return await context.Operations.AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.RequestId == requestId);
    }

    public async Task<int> CountEffectsAsync()
    {
        await using var context = Database.CreateContext();
        return await context.Operations.CountAsync(operation => operation.ReceiptId != null);
    }

    public ValueTask DisposeAsync()
    {
        directory.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }
}
