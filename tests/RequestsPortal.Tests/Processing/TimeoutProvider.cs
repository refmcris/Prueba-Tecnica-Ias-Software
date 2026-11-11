using RequestsPortal.Core.Abstractions;
using RequestsPortal.Core.Contracts;

namespace RequestsPortal.Tests.Processing;

internal sealed class TimeoutProvider : IProvider
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => started.Task;

    public Task<ProviderReceipt?> GetReceiptAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ProviderReceipt?>(null);
    }

    public async Task<ProviderReceipt> ProcessAsync(Guid requestId, int attemptNumber, CancellationToken cancellationToken = default)
    {
        started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("La llamada de prueba debía cancelarse.");
    }
}
