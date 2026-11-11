using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequestsPortal.Api.ExceptionHandling;
using RequestsPortal.Core.Configuration;
using RequestsPortal.Core.Persistence;

namespace RequestsPortal.Api.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddRequestServices(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<RequestExceptionHandler>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<RequestRepository>();

        services.AddDbContext<RequestsDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var dataDirectory = PortalPaths.ResolveDataDirectory(configuration["Portal:DataDirectory"]);
            Directory.CreateDirectory(dataDirectory);

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(dataDirectory, "requests.db"),
                Pooling = false
            }.ToString();

            options.UseSqlite(connectionString);
        });

        return services;
    }
}
