using Microsoft.Extensions.Logging;
using RequestsPortal.Core.Abstractions;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Exceptions;
using RequestsPortal.Core.Options;
using RequestsPortal.Core.Persistence;

namespace RequestsPortal.Core.Processing;

public sealed class RequestProcessor
{
    private readonly RequestProcessingRepository repository;
    private readonly IProvider provider;
    private readonly TimeProvider clock;
    private readonly ProcessingOptions options;
    private readonly ILogger<RequestProcessor> logger;

    public RequestProcessor(
        RequestProcessingRepository repository,
        IProvider provider,
        TimeProvider clock,
        ProcessingOptions options,
        ILogger<RequestProcessor> logger)
    {
        options.Validate();
        this.repository = repository;
        this.provider = provider;
        this.clock = clock;
        this.options = options;
        this.logger = logger;
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var claimed = await repository.TryClaimAsync(TimeSpan.FromSeconds(options.LeaseSeconds), cancellationToken);

        if (claimed is null)
        {
            return false;
        }

        logger.LogInformation("Solicitud {RequestId}, intento {Attempt}: reserva adquirida.", claimed.Id, claimed.AttemptCount);

        var receipt = await provider.GetReceiptAsync(claimed.Id, cancellationToken);

        if (receipt is not null)
        {
            await CompleteAsync(claimed, receipt, cancellationToken);
            return true;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            receipt = await provider.ProcessAsync(claimed.Id, claimed.AttemptCount, linked.Token);
            await CompleteAsync(claimed, receipt, cancellationToken);
        }
        catch (ProviderTransientException)
        {
            await ReconcileOrRetryAsync(claimed, "Fallo transitorio del proveedor.", confirmed: true, cancellationToken);
        }
        catch (ProviderOutcomeUnknownException)
        {
            await ReconcileOrRetryAsync(claimed, "Resultado del proveedor pendiente de confirmación.", confirmed: false, cancellationToken);
        }
        catch (TimeoutException)
        {
            await ReconcileOrRetryAsync(claimed, "Tiempo de espera agotado; se requiere confirmar el resultado.", confirmed: false, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await ReconcileOrRetryAsync(claimed, "Tiempo de espera agotado; se requiere confirmar el resultado.", confirmed: false, cancellationToken);
        }

        return true;
    }

    private async Task CompleteAsync(
        ServiceRequest claimed,
        ProviderReceipt receipt,
        CancellationToken cancellationToken)
    {
        var saved = await repository.CompleteAsync(claimed, receipt, cancellationToken);
        logger.LogInformation("Solicitud {RequestId}: finalización guardada {Saved}.", claimed.Id, saved);
    }

    private async Task ReconcileOrRetryAsync(
        ServiceRequest claimed,
        string message,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        // Un error de comunicación puede ocurrir después de guardar el efecto.
        var receipt = await provider.GetReceiptAsync(claimed.Id, cancellationToken);

        if (receipt is not null)
        {
            await CompleteAsync(claimed, receipt, cancellationToken);
            return;
        }

        var retryDelay = TimeSpan.FromSeconds(Math.Min(claimed.ConfirmedFailureCount + 1, 2));
        var saved = await repository.RecordFailureAsync(
            claimed, message, confirmed, options.MaxAttempts, retryDelay, cancellationToken);

        logger.LogWarning("Solicitud {RequestId}: fallo confirmado {Confirmed}; actualización guardada {Saved}.",
            claimed.Id, confirmed, saved);
    }
}
