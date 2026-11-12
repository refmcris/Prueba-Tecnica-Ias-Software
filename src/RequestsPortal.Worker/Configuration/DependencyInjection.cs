using RequestsPortal.Core.Abstractions;
using RequestsPortal.Core.Options;
using RequestsPortal.Core.Persistence;
using RequestsPortal.Core.Processing;
using RequestsPortal.Core.Providers;

namespace RequestsPortal.Worker.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddRequestProcessingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var processingOptions = configuration.GetSection("Processing").Get<ProcessingOptions>() ?? new();
        processingOptions.Validate();

        var providerOptions = configuration.GetSection("Provider").Get<ProviderOptions>() ?? new();
        providerOptions.Validate();

        services.AddSingleton(processingOptions);
        services.AddSingleton(providerOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new RequestsDatabase(configuration["Portal:DataDirectory"]));
        services.AddSingleton(new ProviderDatabase(configuration["Portal:DataDirectory"]));
        services.AddScoped(provider => provider.GetRequiredService<RequestsDatabase>().CreateContext());
        services.AddScoped<RequestProcessingRepository>();
        services.AddScoped<RequestProcessor>();
        services.AddSingleton<IProvider, SimulatedProvider>();

        return services;
    }
}
