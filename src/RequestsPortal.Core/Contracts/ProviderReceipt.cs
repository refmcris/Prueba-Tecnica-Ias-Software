namespace RequestsPortal.Core.Contracts;

public sealed record ProviderReceipt
{
    public required Guid RequestId { get; init; }

    public required string ReceiptId { get; init; }

    public required DateTime CompletedAt { get; init; }
}
