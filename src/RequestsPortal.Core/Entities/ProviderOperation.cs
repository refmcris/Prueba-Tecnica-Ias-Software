namespace RequestsPortal.Core.Entities;

public sealed class ProviderOperation
{
    public Guid RequestId { get; set; }

    public int InvocationCount { get; set; }

    public string? ReceiptId { get; set; }

    public DateTime? CompletedAt { get; set; }
}
