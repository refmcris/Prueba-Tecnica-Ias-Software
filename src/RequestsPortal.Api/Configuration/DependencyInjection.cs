using RequestsPortal.Api.ExceptionHandling;
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
        services.AddSingleton(provider => new RequestsDatabase(
            provider.GetRequiredService<IConfiguration>()["Portal:DataDirectory"]));
        services.AddScoped(provider => provider.GetRequiredService<RequestsDatabase>().CreateContext());

        return services;
    }
}
