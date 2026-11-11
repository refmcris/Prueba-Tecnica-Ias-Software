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

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
