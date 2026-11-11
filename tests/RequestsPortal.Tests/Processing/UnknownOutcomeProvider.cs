using RequestsPortal.Core.Abstractions;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Exceptions;

namespace RequestsPortal.Tests.Processing;

internal sealed class UnknownOutcomeProvider : IProvider
{
    private readonly IProvider inner;
    private int calls;

    public UnknownOutcomeProvider(IProvider inner)
    {
        this.inner = inner;
    }

    public Task<ProviderReceipt?> GetReceiptAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return inner.GetReceiptAsync(requestId, cancellationToken);
    }

    public Task<ProviderReceipt> ProcessAsync(Guid requestId, int attemptNumber, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref calls) <= 3)
        {
            throw new ProviderOutcomeUnknownException();
        }

        return inner.ProcessAsync(requestId, attemptNumber, cancellationToken);
    }
}
