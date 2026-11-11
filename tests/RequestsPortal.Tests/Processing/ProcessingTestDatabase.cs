using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using RequestsPortal.Core.Abstractions;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Options;
using RequestsPortal.Core.Persistence;
using RequestsPortal.Core.Processing;
using RequestsPortal.Core.Providers;

namespace RequestsPortal.Tests.Processing;

internal sealed class ProcessingTestDatabase : IAsyncDisposable
{
    private readonly DirectoryInfo directory;

    private ProcessingTestDatabase()
    {
        directory = Directory.CreateTempSubdirectory("processing-tests-");
        Requests = new RequestsDatabase(directory.FullName);
        Provider = new ProviderDatabase(directory.FullName);
    }

    public string DataDirectory => directory.FullName;

    public RequestsDatabase Requests { get; }

    public ProviderDatabase Provider { get; }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));

    public static async Task<ProcessingTestDatabase> CreateAsync()
    {
        var database = new ProcessingTestDatabase();

        try
        {
            await database.Requests.InitializeAsync();
            await database.Provider.InitializeAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async Task<ServiceRequest> CreateRequestAsync(string reference = "REFERENCE-1")
    {
        await using var context = Requests.CreateContext();
        var repository = new RequestRepository(context, Clock);
        var result = await repository.CreateAsync(new CreateRequestCommand
        {
            ExternalReference = reference,
            CustomerId = "CUSTOMER-1",
            ServiceCode = "INSTALL",
            Description = "Processing test"
        });

        return result.Request;
    }

    public SimulatedProvider CreateProvider(ProviderMode mode = ProviderMode.Success, double delaySeconds = 0)
    {
        return new SimulatedProvider(Provider, new ProviderOptions
        {
            Mode = mode,
            DelaySeconds = delaySeconds
        }, Clock);
    }

    public async Task<bool> ProcessNextAsync(
        IProvider? provider = null,
        ProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = Requests.CreateContext();
        var repository = new RequestProcessingRepository(context, Clock);
        var processor = new RequestProcessor(repository, provider ?? CreateProvider(), Clock,
            options ?? new ProcessingOptions(), NullLogger<RequestProcessor>.Instance);

        return await processor.ProcessNextAsync(cancellationToken);
    }

    public async Task<ServiceRequest> ReadAsync(Guid id)
    {
        await using var context = Requests.CreateContext();
        return await context.Requests.AsNoTracking().SingleAsync(request => request.Id == id);
    }

    public RequestsDbContext CreateInterceptedContext(IInterceptor interceptor)
    {
        using var source = Requests.CreateContext();
        var options = new DbContextOptionsBuilder<RequestsDbContext>()
            .UseSqlite(source.Database.GetConnectionString())
            .AddInterceptors(interceptor)
            .Options;
        return new RequestsDbContext(options);
    }

    public async Task<int> CountEffectsAsync()
    {
        await using var context = Provider.CreateContext();
        return await context.Operations.CountAsync(operation => operation.ReceiptId != null);
    }

    public ValueTask DisposeAsync()
    {
        directory.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }
}
