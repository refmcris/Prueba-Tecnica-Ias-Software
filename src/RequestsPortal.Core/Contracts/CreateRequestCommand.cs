namespace RequestsPortal.Core.Contracts;

public sealed record CreateRequestCommand
{
    public string ExternalReference { get; init; } = string.Empty;

    public string CustomerId { get; init; } = string.Empty;

    public string ServiceCode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
