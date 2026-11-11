using RequestsPortal.Core.Persistence;
using RequestsPortal.Core.Processing;
using RequestsPortal.Worker;
using RequestsPortal.Worker.Configuration;

var initializeOnly = args.Contains("--init-db", StringComparer.Ordinal);
var processOnce = args.Contains("--once", StringComparer.Ordinal);
var hostArguments = args.Where(argument => argument is not ("--init-db" or "--once")).ToArray();

if (initializeOnly && processOnce)
{
    throw new ArgumentException("Usa --init-db o --once, no ambos a la vez.");
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = hostArguments,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddWindowsService(options => options.ServiceName = "RequestsPortal.Worker");
builder.Services.AddRequestProcessingServices(builder.Configuration);

if (!initializeOnly && !processOnce)
{
    builder.Services.AddHostedService<Worker>();
}

using var host = builder.Build();

await host.Services.GetRequiredService<RequestsDatabase>().InitializeAsync();
await host.Services.GetRequiredService<ProviderDatabase>().InitializeAsync();

if (initializeOnly)
{
    Console.WriteLine("Almacenamiento inicializado.");
    return;
}

if (processOnce)
{
    await using var scope = host.Services.CreateAsyncScope();
    var processor = scope.ServiceProvider.GetRequiredService<RequestProcessor>();
    var processed = await processor.ProcessNextAsync();
    Console.WriteLine(processed ? "Se procesó una solicitud." : "No hay solicitudes disponibles.");
    return;
}

await host.RunAsync();
