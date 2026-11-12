using RequestsPortal.Core.Options;
using RequestsPortal.Core.Processing;

namespace RequestsPortal.Worker;

public sealed class Worker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ProcessingOptions options;
    private readonly TimeProvider clock;
    private readonly ILogger<Worker> logger;

    public Worker(
        IServiceScopeFactory scopeFactory,
        ProcessingOptions options,
        TimeProvider clock,
        ILogger<Worker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.clock = clock;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker iniciado: consulta cada {PollSeconds}s y reserva de {LeaseSeconds}s.",
            options.PollIntervalSeconds, options.LeaseSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<RequestProcessor>();

                if (await processor.ProcessNextAsync(stoppingToken))
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falló el ciclo. La reserva podrá recuperarse cuando venza.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
