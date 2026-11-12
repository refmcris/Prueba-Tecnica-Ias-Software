using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Persistence;

namespace RequestsPortal.Tests.Api;

internal sealed class RequestApiFactory : WebApplicationFactory<Program>
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("requests-api-");
    private readonly TimeProvider? clock;

    public RequestApiFactory(TimeProvider? clock = null)
    {
        this.clock = clock;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Portal:DataDirectory"] = directory.FullName
            });
        });

        if (clock is not null)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            });
        }
    }

    public async Task<int> CountRequestsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RequestsDbContext>();
        return await context.Requests.CountAsync();
    }

    public async Task SetStatusAsync(Guid id, RequestStatus status)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RequestsDbContext>();
        var request = await context.Requests.SingleAsync(request => request.Id == id);
        request.Status = status;
        await context.SaveChangesAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        directory.Delete(recursive: true);
    }
}
