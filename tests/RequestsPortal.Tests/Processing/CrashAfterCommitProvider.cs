using RequestsPortal.Core.Abstractions;
using RequestsPortal.Core.Contracts;

namespace RequestsPortal.Tests.Processing;

internal sealed class CrashAfterCommitProvider : IProvider
{
    private readonly IProvider inner;

    public CrashAfterCommitProvider(IProvider inner)
    {
        this.inner = inner;
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
        await inner.ProcessAsync(requestId, attemptNumber, cancellationToken);
        throw new InvalidOperationException("Fallo simulado después de guardar el efecto del proveedor.");
    }
}
