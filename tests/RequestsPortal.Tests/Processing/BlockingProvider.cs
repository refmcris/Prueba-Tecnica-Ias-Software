using RequestsPortal.Core.Abstractions;
using RequestsPortal.Core.Contracts;

namespace RequestsPortal.Tests.Processing;

internal sealed class BlockingProvider : IProvider
{
    private readonly IProvider inner;
    private readonly bool ignoreCancellation;
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BlockingProvider(IProvider inner, bool ignoreCancellation = false)
    {
        this.inner = inner;
        this.ignoreCancellation = ignoreCancellation;
    }

    public Task Started => started.Task;

    public void Release()
    {
        released.TrySetResult();
    }

    public Task<ProviderReceipt?> GetReceiptAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return inner.GetReceiptAsync(requestId, cancellationToken);
    }

    public async Task<ProviderReceipt> ProcessAsync(
        Guid requestId,
        int attemptNumber,
        CancellationToken cancellationToken = default)
    {
        started.TrySetResult();
        var token = ignoreCancellation ? CancellationToken.None : cancellationToken;
        await released.Task.WaitAsync(token);
        return await inner.ProcessAsync(requestId, attemptNumber, token);
    }
}
