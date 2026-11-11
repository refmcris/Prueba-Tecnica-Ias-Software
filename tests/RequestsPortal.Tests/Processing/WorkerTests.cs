using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestsPortal.Core.Enums;
using RequestsPortal.Worker.Configuration;
using Xunit;

namespace RequestsPortal.Tests.Processing;

public sealed class WorkerTests
{
    [Fact]
    public async Task Background_worker_uses_scoped_services_and_stops_cleanly()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Portal:DataDirectory"] = database.DataDirectory,
            ["Provider:DelaySeconds"] = "0"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequestProcessingServices(configuration);
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(database.Clock);
        services.AddSingleton<RequestsPortal.Worker.Worker>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var worker = provider.GetRequiredService<RequestsPortal.Worker.Worker>();

        await worker.StartAsync(CancellationToken.None);

        try
        {
            var timeout = Stopwatch.StartNew();

            while ((await database.ReadAsync(original.Id)).Status != RequestStatus.Completed)
            {
                Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(5), "El Worker no completó la solicitud.");
                await Task.Delay(10);
            }
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StopAsync(stopping.Token);
        }

        Assert.NotNull(worker.ExecuteTask);
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(1, await database.CountEffectsAsync());
    }
}
