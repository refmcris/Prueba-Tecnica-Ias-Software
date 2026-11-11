using RequestsPortal.Api.Contracts;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Entities;

namespace RequestsPortal.Api.Mappings;

public static class RequestMappings
{
    public static CreateRequestCommand ToCommand(this CreateRequestBody body)
    {
        return new CreateRequestCommand
        {
            ExternalReference = body.ExternalReference ?? string.Empty,
            CustomerId = body.CustomerId ?? string.Empty,
            ServiceCode = body.ServiceCode ?? string.Empty,
            Description = body.Description ?? string.Empty
        };
    }

    public static RequestDto ToDto(this ServiceRequest request)
    {
        return new RequestDto
        {
            Id = request.Id,
            ExternalReference = request.ExternalReference,
            CustomerId = request.CustomerId,
            ServiceCode = request.ServiceCode,
            Description = request.Description,
            Status = request.Status.ToString(),
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt
        };
    }
}
