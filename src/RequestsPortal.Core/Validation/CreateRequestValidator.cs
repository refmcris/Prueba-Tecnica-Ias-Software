using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Exceptions;

namespace RequestsPortal.Core.Validation;

public static class CreateRequestValidator
{
    public static CreateRequestCommand NormalizeAndValidate(CreateRequestCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        var normalized = new CreateRequestCommand
        {
            ExternalReference = ValidateField(
                command.ExternalReference,
                "externalReference",
                RequestFieldLimits.ExternalReference,
                errors),
            CustomerId = ValidateField(
                command.CustomerId,
                "customerId",
                RequestFieldLimits.CustomerId,
                errors),
            ServiceCode = ValidateField(
                command.ServiceCode,
                "serviceCode",
                RequestFieldLimits.ServiceCode,
                errors),
            Description = ValidateField(
                command.Description,
                "description",
                RequestFieldLimits.Description,
                errors)
        };

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return normalized;
    }

    private static string ValidateField(
        string? value,
        string field,
        int maxLength,
        Dictionary<string, string[]> errors)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
        {
            errors[field] = ["El campo es obligatorio."];
        }
        else if (normalized.Length > maxLength)
        {
            errors[field] = [$"El máximo es {maxLength} caracteres."];
        }

        return normalized;
    }
}
