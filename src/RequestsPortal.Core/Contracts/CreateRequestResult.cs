using RequestsPortal.Core.Entities;

namespace RequestsPortal.Core.Contracts;

public sealed record CreateRequestResult
{
    public required ServiceRequest Request { get; init; }

    public required bool Created { get; init; }
}
