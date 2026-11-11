namespace RequestsPortal.Api.Contracts;

public sealed record CreateRequestBody
{
    public string? ExternalReference { get; init; }

    public string? CustomerId { get; init; }

    public string? ServiceCode { get; init; }

    public string? Description { get; init; }
}
