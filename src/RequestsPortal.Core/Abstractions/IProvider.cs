using RequestsPortal.Core.Contracts;

namespace RequestsPortal.Core.Abstractions;

public interface IProvider
{
    Task<ProviderReceipt> ProcessAsync(Guid requestId, int attemptNumber, CancellationToken cancellationToken = default);

    Task<ProviderReceipt?> GetReceiptAsync(Guid requestId, CancellationToken cancellationToken = default);
}
