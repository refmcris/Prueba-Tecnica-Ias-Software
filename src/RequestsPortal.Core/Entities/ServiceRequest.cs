using RequestsPortal.Core.Enums;

namespace RequestsPortal.Core.Entities;

public sealed class ServiceRequest
{
    public Guid Id { get; set; }

    public string ExternalReference { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public string ServiceCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int AttemptCount { get; set; }

    public int ConfirmedFailureCount { get; set; }

    public string? LastError { get; set; }

    public string? ProviderReceiptId { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public string? LeaseToken { get; set; }

    public DateTime? LeaseUntil { get; set; }
}
