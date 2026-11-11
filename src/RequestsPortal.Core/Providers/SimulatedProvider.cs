using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Abstractions;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Exceptions;
using RequestsPortal.Core.Options;
using RequestsPortal.Core.Persistence;

namespace RequestsPortal.Core.Providers;

public sealed class SimulatedProvider : IProvider
{
    private readonly ProviderDatabase database;
    private readonly ProviderOptions options;
    private readonly TimeProvider clock;

    public SimulatedProvider(ProviderDatabase database, ProviderOptions options, TimeProvider clock)
    {
        options.Validate();
        this.database = database;
        this.options = options;
        this.clock = clock;
    }

    public async Task<ProviderReceipt?> GetReceiptAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequestId(requestId);
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = database.CreateContext();
        var operation = await context.Operations.AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.RequestId == requestId, cancellationToken);

        return ToReceipt(operation);
    }

    public async Task<ProviderReceipt> ProcessAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await GetReceiptAsync(requestId, cancellationToken);

        if (receipt is not null)
        {
            return receipt;
        }

        await Task.Delay(TimeSpan.FromSeconds(options.DelaySeconds), clock, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = database.CreateContext();

        // La transacción serializa el acceso al recibo entre instancias.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var operation = await context.Operations.SingleOrDefaultAsync(
            operation => operation.RequestId == requestId,
            cancellationToken);

        var existing = ToReceipt(operation);

        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        if (operation is null)
        {
            operation = new ProviderOperation { RequestId = requestId };
            context.Operations.Add(operation);
        }

        operation.InvocationCount++;
        var transientFailure = options.Mode == ProviderMode.AlwaysTransient
            || (options.Mode == ProviderMode.TransientThenSuccess && operation.InvocationCount == 1);

        if (!transientFailure)
        {
            operation.ReceiptId = $"receipt-{requestId:N}";
            operation.CompletedAt = clock.GetUtcNow().UtcDateTime;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (transientFailure)
        {
            throw new ProviderTransientException();
        }

        // El efecto ya quedó guardado; solo falla la confirmación.
        if (options.Mode == ProviderMode.ResponseLostAfterCommit)
        {
            throw new ProviderOutcomeUnknownException();
        }

        return ToReceipt(operation)!;
    }

    private static ProviderReceipt? ToReceipt(ProviderOperation? operation)
    {
        if (operation?.ReceiptId is null || operation.CompletedAt is null)
        {
            return null;
        }

        return new ProviderReceipt
        {
            RequestId = operation.RequestId,
            ReceiptId = operation.ReceiptId,
            CompletedAt = operation.CompletedAt.Value
        };
    }

    private static void ValidateRequestId(Guid requestId)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la solicitud no puede estar vacío.", nameof(requestId));
        }
    }
}
