namespace RequestsPortal.Api.Contracts;

public sealed record RequestDto
{
    public required Guid Id { get; init; }

    public required string ExternalReference { get; init; }

    public required string CustomerId { get; init; }

    public required string ServiceCode { get; init; }

    public required string Description { get; init; }

    public required string Status { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }
}
